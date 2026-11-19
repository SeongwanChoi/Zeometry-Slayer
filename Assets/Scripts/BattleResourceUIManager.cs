using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 자원(코스트 & 크리티컬 스타) 및 하단 UI 매니저 (리메이크 명세 반영):
/// 1) 코스트 UI (CostTrayUI):
///    - 턴 시작 시 기본 코스트(3) + 직전 턴 Arts 카드로 누적된 추가 코스트(nextTurnBonusCost)를 획득합니다.
///    - Arts 카드(1코스트) 사용 시 다음 턴 획득할 코스트가 +1 추가됩니다.
///    - Quick 카드(1코스트), Buster / SuperBuster 카드(2코스트) 사용 시 해당 코스트를 소비합니다.
/// 2) 스타 UI (StarCounterUI):
///    - Quick 카드 공격 및 Player_2 스킬로 스타를 획득/소비합니다.
/// </summary>
public class BattleResourceUIManager : Singleton<BattleResourceUIManager>
{
    [Header("1. 코스트(Cost) 설정 및 UI 참조")]
    [SerializeField] private int baseTurnCost = 3;
    [SerializeField] private int maxCost = 10;
    [SerializeField] private int currentCost = 3;
    [SerializeField] private int nextTurnBonusCost = 0;
    [SerializeField] private RectTransform costTrayRoot;
    [SerializeField] private Text costValueText;
    [SerializeField] private Text nextTurnCostBadgeText;

    [Header("2. 크리티컬 스타(Star) 설정 및 UI 참조")]
    [SerializeField] private int currentStars = 0;
    [SerializeField] private RectTransform starCounterRoot;
    [SerializeField] private Image starIconImage;
    [SerializeField] private Text starCountText;
    [SerializeField] private Sprite criticalStarSprite;

    private static Sprite cachedCostTrayBgSprite;
    private static Sprite cachedCostBadgeSprite;
    private static Sprite cachedCriticalStarSprite;

    private Coroutine starIconPunchCoroutine;
    private Coroutine costOrbPunchCoroutine;

    public int BaseTurnCost => baseTurnCost;
    public int MaxCost => maxCost;
    public int CurrentCost => currentCost;
    public int NextTurnBonusCost => nextTurnBonusCost;
    public int CurrentStars => currentStars;
    public RectTransform StarIconRect => starIconImage != null ? starIconImage.rectTransform : starCounterRoot;
    public Sprite CriticalStarSprite => criticalStarSprite != null ? criticalStarSprite : GetOrCreateCriticalStarSprite();

    protected override void Awake()
    {
        base.Awake();
        EnsureResourceUI();
        RefreshAllUI();
    }

    private void Start()
    {
        EnsureResourceUI();
        RefreshAllUI();
    }

    /// <summary>
    /// 매 플레이어 턴 시작 시 기본 코스트(3) + 이전 턴 Arts 카드로 예약된 추가 코스트를 충전합니다.
    /// </summary>
    public void RefillCostForNewTurn()
    {
        currentCost = Mathf.Clamp(baseTurnCost + nextTurnBonusCost, 0, maxCost);
        nextTurnBonusCost = 0;
        RefreshCostUI();
        if (HandManager.Ins != null)
            HandManager.Ins.RefreshAllCardCostsAndLabels();
    }

    /// <summary>
    /// Arts 카드 사용 시 다음 턴에 획득할 코스트를 추가합니다.
    /// </summary>
    public void AddNextTurnBonusCost(int amount = 1)
    {
        if (amount <= 0) return;
        nextTurnBonusCost = Mathf.Clamp(nextTurnBonusCost + amount, 0, maxCost);
        RefreshCostUI();
        PunchCostOrb(1.18f, 0.18f);
    }

    /// <summary>
    /// 즉시 코스트를 회복합니다 (예: Player_1의 2스킬 코스트 +2 획득).
    /// </summary>
    public void AddCost(int amount = 1)
    {
        currentCost = Mathf.Clamp(currentCost + amount, 0, maxCost);
        RefreshCostUI();
        PunchCostOrb(1.22f, 0.18f);
        if (HandManager.Ins != null)
            HandManager.Ins.RefreshAllCardCostsAndLabels();
    }

    public bool TrySpendCost(int amount)
    {
        int needed = Mathf.Max(0, amount);
        if (currentCost < needed)
        {
            PunchCostOrb(1.22f, 0.22f);
            return false;
        }

        currentCost -= needed;
        RefreshCostUI();
        if (HandManager.Ins != null)
            HandManager.Ins.RefreshAllCardCostsAndLabels();
        return true;
    }

    public void PunchCostOrb(float multiplier = 1.2f, float duration = 0.18f)
    {
        if (costTrayRoot == null || !gameObject.activeInHierarchy) return;
        Transform badgeTr = costTrayRoot.Find("CostNumberBadge");
        RectTransform targetRt = badgeTr != null ? badgeTr.GetComponent<RectTransform>() : costTrayRoot;
        if (targetRt == null) return;

        if (costOrbPunchCoroutine != null)
            StopCoroutine(costOrbPunchCoroutine);
        costOrbPunchCoroutine = StartCoroutine(CoPunchRect(targetRt, multiplier, duration));
    }

    /// <summary>
    /// 스타를 획득합니다.
    /// </summary>
    public void AddStars(int amount)
    {
        if (amount <= 0) return;
        currentStars = Mathf.Max(0, currentStars + amount);
        RefreshStarUI();
    }

    /// <summary>
    /// 스타를 차감합니다.
    /// </summary>
    public void DeductStars(int amount = 1)
    {
        if (amount <= 0) return;
        currentStars = Mathf.Max(0, currentStars - amount);
        RefreshStarUI();
    }

    /// <summary>
    /// 스타 카운터에 남아있는 스타와 현재 손패 카드들에 배정된 스타의 총합을 반환합니다. (Player_2 3스킬 20스타 조건 판정용)
    /// </summary>
    public int GetTotalAvailableStarsIncludingHand()
    {
        int handStars = HandManager.Ins != null ? HandManager.Ins.GetTotalAssignedStarsInHand() : 0;
        return currentStars + handStars;
    }

    /// <summary>
    /// Player_2의 3스킬(스타 20개 삭제) 등에서 호출되어 보유 스타(부족 시 손패 배정 스타 포함)를 소모합니다.
    /// </summary>
    public bool TrySpendStarsIncludingHand(int amount)
    {
        if (amount <= 0) return true;
        if (GetTotalAvailableStarsIncludingHand() < amount)
            return false;

        int fromCounter = Mathf.Min(currentStars, amount);
        currentStars -= fromCounter;
        int remainToDeduct = amount - fromCounter;

        if (remainToDeduct > 0 && HandManager.Ins != null)
        {
            HandManager.Ins.DeductAssignedStarsFromHandCards(remainToDeduct);
        }

        RefreshStarUI();
        PunchStarIcon(1.25f, 0.2f);
        return true;
    }

    public void PunchStarIcon(float multiplier = 1.25f, float duration = 0.18f)
    {
        if (StarIconRect == null || !gameObject.activeInHierarchy) return;
        if (starIconPunchCoroutine != null)
        {
            StopCoroutine(starIconPunchCoroutine);
        }
        starIconPunchCoroutine = StartCoroutine(CoPunchRect(StarIconRect, multiplier, duration));
    }

    private IEnumerator CoPunchRect(RectTransform rt, float multiplier, float duration)
    {
        if (rt == null) yield break;

        Vector3 originalScale = Vector3.one;
        float half = Mathf.Max(0.01f, duration * 0.45f);
        float elapsed = 0f;

        while (elapsed < half)
        {
            if (rt == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            rt.localScale = Vector3.Lerp(originalScale, originalScale * multiplier, t);
            yield return null;
        }

        elapsed = 0f;
        float remain = Mathf.Max(0.01f, duration - half);
        while (elapsed < remain)
        {
            if (rt == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / remain);
            rt.localScale = Vector3.Lerp(originalScale * multiplier, originalScale, t);
            yield return null;
        }

        if (rt != null)
            rt.localScale = originalScale;
    }

    public int ConsumeAllStarsForAttackPhase()
    {
        int stars = currentStars;
        currentStars = 0;
        RefreshStarUI();
        return stars;
    }

    public void RefreshAllUI()
    {
        EnsureResourceUI();
        RefreshCostUI();
        RefreshStarUI();
    }

    public void RefreshCostUI()
    {
        currentCost = Mathf.Clamp(currentCost, 0, maxCost);

        if (costValueText != null)
        {
            costValueText.text = $"{currentCost}/{baseTurnCost}";
        }

        if (nextTurnCostBadgeText != null)
        {
            if (nextTurnBonusCost > 0)
            {
                nextTurnCostBadgeText.text = $"다음턴 +{nextTurnBonusCost}";
                nextTurnCostBadgeText.gameObject.SetActive(true);
            }
            else
            {
                nextTurnCostBadgeText.gameObject.SetActive(false);
            }
        }
    }

    public void RefreshStarUI()
    {
        currentStars = Mathf.Max(0, currentStars);

        if (starCountText != null)
        {
            starCountText.text = currentStars.ToString();
        }
    }

    public void EnsureResourceUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        EnsureCostTrayUI(canvas.transform);
        EnsureStarCounterUI(canvas.transform);
    }

    private void EnsureCostTrayUI(Transform canvasTransform)
    {
        if (costTrayRoot == null)
        {
            Transform existing = canvasTransform.Find("CostTrayUI");
            if (existing != null)
            {
                costTrayRoot = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject trayObj = new GameObject("CostTrayUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                trayObj.layer = LayerMask.NameToLayer("UI");
                trayObj.transform.SetParent(canvasTransform, false);

                costTrayRoot = trayObj.GetComponent<RectTransform>();
                costTrayRoot.anchorMin = new Vector2(0f, 0f);
                costTrayRoot.anchorMax = new Vector2(0f, 0f);
                costTrayRoot.pivot = new Vector2(0f, 0f);
                costTrayRoot.anchoredPosition = new Vector2(226f, 125f);
                costTrayRoot.sizeDelta = new Vector2(248f, 92f);
                costTrayRoot.localScale = Vector3.one;

                Image trayImg = trayObj.GetComponent<Image>();
                trayImg.sprite = null;
                trayImg.color = Color.clear;
                trayImg.raycastTarget = false;
                trayImg.enabled = false;
            }
        }

        Image trayBackground = costTrayRoot.GetComponent<Image>();
        if (trayBackground != null)
        {
            trayBackground.sprite = null;
            trayBackground.color = Color.clear;
            trayBackground.raycastTarget = false;
            trayBackground.enabled = false;
        }

        Transform badgeTr = costTrayRoot.Find("CostNumberBadge");
        RectTransform badgeRect;
        if (badgeTr == null)
        {
            GameObject badgeObj = new GameObject("CostNumberBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badgeObj.layer = LayerMask.NameToLayer("UI");
            badgeObj.transform.SetParent(costTrayRoot, false);

            badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(44f, 0f);
            badgeRect.sizeDelta = new Vector2(82f, 82f);
            badgeRect.localScale = Vector3.one;

            Image badgeImg = badgeObj.GetComponent<Image>();
            badgeImg.sprite = GetOrCreateCostBadgeSprite();
            badgeImg.color = Color.white;
            badgeImg.raycastTarget = false;
        }
        else
        {
            badgeRect = badgeTr.GetComponent<RectTransform>();
        }

        if (badgeRect != null)
        {
            Image orbImage = badgeRect.GetComponent<Image>();
            if (orbImage != null)
            {
                orbImage.sprite = GetOrCreateCostBadgeSprite();
                orbImage.type = Image.Type.Simple;
                orbImage.preserveAspect = true;
                orbImage.color = Color.white;
                orbImage.raycastTarget = false;
            }
        }

        if (costValueText == null && badgeRect != null)
        {
            Transform txtTr = badgeRect.Find("CostValueText");
            if (txtTr == null)
            {
                GameObject txtObj = new GameObject("CostValueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
                txtObj.layer = LayerMask.NameToLayer("UI");
                txtObj.transform.SetParent(badgeRect, false);

                RectTransform txtRt = txtObj.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;
                txtRt.localScale = Vector3.one;

                costValueText = txtObj.GetComponent<Text>();
                costValueText.font = GetOrCreateFont();
                costValueText.fontSize = 22;
                costValueText.fontStyle = FontStyle.Bold;
                costValueText.alignment = TextAnchor.MiddleCenter;
                costValueText.color = Color.white;
                costValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
                costValueText.verticalOverflow = VerticalWrapMode.Overflow;
                costValueText.raycastTarget = false;

                Outline outline = txtObj.GetComponent<Outline>();
                outline.effectColor = new Color(0.02f, 0.05f, 0.20f, 0.98f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            else
            {
                costValueText = txtTr.GetComponent<Text>();
            }
        }

        if (nextTurnCostBadgeText == null && badgeRect != null)
        {
            Transform ntTr = badgeRect.Find("NextTurnBonusText");
            if (ntTr == null)
            {
                nextTurnCostBadgeText = UIHelper.CreateOutlinedText(
                    badgeRect,
                    "NextTurnBonusText",
                    "다음턴 +1",
                    14,
                    FontStyle.Bold,
                    new Color(0.55f, 0.95f, 1f, 1f),
                    new Vector2(0f, 52f),
                    new Vector2(110f, 24f),
                    TextAnchor.MiddleCenter,
                    new Color(0.02f, 0.05f, 0.16f, 0.98f),
                    new Vector2(1.4f, -1.4f)
                );
                nextTurnCostBadgeText.gameObject.SetActive(false);
            }
            else
            {
                nextTurnCostBadgeText = ntTr.GetComponent<Text>();
            }
        }
    }

    private void EnsureStarCounterUI(Transform canvasTransform)
    {
        if (starCounterRoot == null)
        {
            Transform existing = canvasTransform.Find("StarCounterUI");
            if (existing != null)
            {
                starCounterRoot = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject starRootObj = new GameObject("StarCounterUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                starRootObj.layer = LayerMask.NameToLayer("UI");
                starRootObj.transform.SetParent(canvasTransform, false);

                starCounterRoot = starRootObj.GetComponent<RectTransform>();
                starCounterRoot.anchorMin = new Vector2(1f, 0f);
                starCounterRoot.anchorMax = new Vector2(1f, 0f);
                starCounterRoot.pivot = new Vector2(1f, 0f);
                starCounterRoot.anchoredPosition = new Vector2(-1484f, 17f);
                starCounterRoot.sizeDelta = new Vector2(210f, 92f);
                starCounterRoot.localScale = Vector3.one;

                Image bgImg = starRootObj.GetComponent<Image>();
                bgImg.sprite = UnitStatusUI.GetOrCreateSolidWhiteSprite();
                bgImg.color = new Color(0.02f, 0.05f, 0.10f, 0.65f);
                bgImg.raycastTarget = false;
            }
        }

        if (criticalStarSprite == null)
            criticalStarSprite = GetOrCreateCriticalStarSprite();

        if (starIconImage == null)
        {
            Transform iconTr = starCounterRoot.Find("StarIcon");
            if (iconTr == null)
            {
                GameObject iconObj = new GameObject("StarIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObj.layer = LayerMask.NameToLayer("UI");
                iconObj.transform.SetParent(starCounterRoot, false);

                RectTransform rt = iconObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(44f, 4f);
                rt.sizeDelta = new Vector2(82f, 82f);
                rt.localScale = Vector3.one;

                starIconImage = iconObj.GetComponent<Image>();
                starIconImage.sprite = criticalStarSprite;
                starIconImage.preserveAspect = true;
                starIconImage.raycastTarget = false;
            }
            else
            {
                starIconImage = iconTr.GetComponent<Image>();
                if (starIconImage != null && criticalStarSprite != null)
                    starIconImage.sprite = criticalStarSprite;
            }
        }

        Transform xLabelTr = starCounterRoot.Find("StarXLabel");
        if (xLabelTr == null)
        {
            CreateStyledText(
                starCounterRoot,
                "StarXLabel",
                "x",
                24,
                FontStyle.Bold,
                new Color(0.92f, 0.95f, 1.0f, 1f),
                new Vector2(83f, 4f),
                new Vector2(30f, 36f),
                TextAnchor.MiddleCenter,
                new Color(0f, 0f, 0f, 0.95f)
            );
        }

        if (starCountText == null)
        {
            Transform countTr = starCounterRoot.Find("StarCountText");
            if (countTr == null)
            {
                starCountText = CreateStyledText(
                    starCounterRoot,
                    "StarCountText",
                    currentStars.ToString(),
                    44,
                    FontStyle.BoldAndItalic,
                    Color.white,
                    new Vector2(148f, 10f),
                    new Vector2(96f, 56f),
                    TextAnchor.MiddleCenter,
                    new Color(0.05f, 0.08f, 0.14f, 0.98f)
                );
            }
            else
            {
                starCountText = countTr.GetComponent<Text>();
            }
        }

        Transform critLabelTr = starCounterRoot.Find("CriticalLabel");
        if (critLabelTr == null)
        {
            CreateStyledText(
                starCounterRoot,
                "CriticalLabel",
                "CRITICAL",
                19,
                FontStyle.Bold,
                new Color(0.96f, 0.72f, 0.28f, 1f),
                new Vector2(114f, -24f),
                new Vector2(140f, 28f),
                TextAnchor.MiddleCenter,
                new Color(0.18f, 0.08f, 0.01f, 0.98f)
            );
        }
    }

    private static Text CreateStyledText(
        Transform parent,
        string name,
        string content,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        Vector2 anchoredPosFromLeftCenter,
        Vector2 sizeDelta,
        TextAnchor alignment,
        Color outlineColor)
    {
        return UIHelper.CreateOutlinedText(
            parent,
            name,
            content,
            fontSize,
            fontStyle,
            color,
            anchoredPosFromLeftCenter,
            sizeDelta,
            alignment,
            outlineColor,
            new Vector2(1.8f, -1.8f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );
    }

    private static Font GetOrCreateFont() => UIHelper.GetOrCreateFont();

    public static Sprite GetOrCreateCostTrayBgSprite()
    {
        if (cachedCostTrayBgSprite != null) return cachedCostTrayBgSprite;

        int w = 416;
        int h = 52;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;

        tex.SetPixels(pixels);
        tex.Apply();

        cachedCostTrayBgSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        cachedCostTrayBgSprite.name = "ui_cost_tray_bg";
        return cachedCostTrayBgSprite;
    }

    public static Sprite GetOrCreateCostBadgeSprite()
    {
        if (cachedCostBadgeSprite != null) return cachedCostBadgeSprite;

        int w = 96;
        int h = 96;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[w * h];
        Color darkRim = new Color(0.015f, 0.055f, 0.16f, 1f);
        Color neonCyan = new Color(0.22f, 0.86f, 1f, 1f);
        Color innerBlue = new Color(0.035f, 0.17f, 0.38f, 1f);
        Color innerGlow = new Color(0.10f, 0.52f, 0.82f, 1f);

        for (int y = 0; y < h; y++)
        {
            float ny = ((y + 0.5f) / h - 0.5f) * 2f;
            for (int x = 0; x < w; x++)
            {
                float nx = ((x + 0.5f) / w - 0.5f) * 2f;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);
                Color c = Color.clear;
                if (dist <= 1.18f)
                {
                    if (dist > 1f)
                    {
                        c = neonCyan;
                        c.a = (1f - (dist - 1f) / 0.18f) * 0.18f;
                    }
                    else if (dist > 0.86f)
                    {
                        c = darkRim;
                        c.a = Mathf.Clamp01((1f - dist) / 0.02f);
                    }
                    else if (dist > 0.78f)
                    {
                        c = Color.Lerp(darkRim, neonCyan, 1f - (dist - 0.78f) / 0.08f);
                        c.a = 1f;
                    }
                    else if (dist > 0.70f)
                    {
                        c = darkRim;
                        c.a = 1f;
                    }
                    else
                    {
                        float radialGlow = Mathf.Clamp01(1f - dist / 0.72f);
                        float topLight = Mathf.Clamp01((ny + 0.8f) / 1.8f);
                        c = Color.Lerp(innerBlue, innerGlow, radialGlow * 0.62f + topLight * 0.18f);
                        c.a = 1f;
                    }
                }
                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedCostBadgeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        cachedCostBadgeSprite.name = "ui_energy_orb";
        return cachedCostBadgeSprite;
    }

    public static Sprite GetOrCreateCriticalStarSprite()
    {
        if (cachedCriticalStarSprite != null) return cachedCriticalStarSprite;

        Texture2D tex = CreateCriticalStarTexture(96, 96);
        cachedCriticalStarSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        cachedCriticalStarSprite.name = "icon_critical_star";
        return cachedCriticalStarSprite;
    }

    public static Texture2D CreateCriticalStarTexture(int width = 96, int height = 96)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        Color darkGoldOutline = new Color(0.28f, 0.14f, 0.02f, 0.98f);
        Color warmAmber = new Color(0.95f, 0.52f, 0.08f, 1.0f);
        Color brightGold = new Color(1.0f, 0.84f, 0.25f, 1.0f);
        Color coreCream = new Color(1.0f, 0.98f, 0.82f, 1.0f);

        for (int y = 0; y < height; y++)
        {
            float ny = ((y + 0.5f) / height - 0.5f) * 2f;
            for (int x = 0; x < width; x++)
            {
                float nx = ((x + 0.5f) / width - 0.5f) * 2f;
                float ax = Mathf.Abs(nx) / 0.82f;
                float ay = Mathf.Abs(ny) / 0.95f;
                float starDist = Mathf.Pow(Mathf.Max(0.0001f, ax), 0.56f) + Mathf.Pow(Mathf.Max(0.0001f, ay), 0.56f);

                Color c = Color.clear;
                if (starDist <= 1.04f)
                {
                    float edge = 1.04f - starDist;
                    float alpha = Mathf.Clamp01(edge / 0.06f);

                    if (edge < 0.07f)
                    {
                        c = darkGoldOutline;
                    }
                    else
                    {
                        float ridgeDist = Mathf.Min(ax, ay);
                        float ridgeGlow = Mathf.Clamp01(1f - (ridgeDist / 0.16f));
                        float facetShade = (ny - nx * 0.5f) > 0f ? 0.75f : 0.25f;
                        c = Color.Lerp(warmAmber, brightGold, facetShade);
                        c = Color.Lerp(c, coreCream, ridgeGlow * 0.85f);
                    }

                    c.a = alpha;
                }

                pixels[y * width + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
