using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 자원(코스트 & 크리티컬 스타) 및 하단 좌/우측 UI 매니저:
/// 1) 좌측 하단 코스트 UI (CostTrayUI):
///    - 첫 번째 참고 이미지 스타일: 좌측 파란 캡슐 배지("0/10" ~ "10/10") + 우측 10칸의 파란 보석(Gem) 슬롯
///    - 파란색 Arts 카드로 공격 시 코스트 +1 획득
/// 2) 우측 하단 스타 UI (StarCounterUI):
///    - 두 번째 참고 이미지 스타일: 황금빛 사각 별(4-pointed Star) 아이콘 + "x" + 스타 개수 숫자 + 하단 "CRITICAL" 텍스트
///    - 초록색 Quick 카드로 공격 시 스타 랜덤(1~10) 획득 -> 다음 턴 Attack 페이즈 진입 시 AttackBox 카드들에 랜덤 부여 (1개당 크리티컬 확률 +10%)
/// </summary>
public class BattleResourceUIManager : Singleton<BattleResourceUIManager>
{
    [Header("1. 코스트(Cost) 설정 및 좌측 하단 UI 참조")]
    [SerializeField] private int maxCost = 10;
    [SerializeField] private int currentCost = 0;
    [SerializeField] private RectTransform costTrayRoot;
    [SerializeField] private Text costValueText;
    [SerializeField] private RectTransform costGemsContainer;
    [SerializeField] private Sprite costGemSprite;
    [SerializeField] private List<Image> costGemImages = new List<Image>();

    [Header("2. 크리티컬 스타(Star) 설정 및 우측 하단 UI 참조")]
    [SerializeField] private int currentStars = 0;
    [SerializeField] private RectTransform starCounterRoot;
    [SerializeField] private Image starIconImage;
    [SerializeField] private Text starCountText;
    [SerializeField] private Sprite criticalStarSprite;

    private static Sprite cachedCostTrayBgSprite;
    private static Sprite cachedCostBadgeSprite;
    private static Sprite cachedCostGemSprite;
    private static Sprite cachedCriticalStarSprite;
    private static Font cachedFont;

    public int MaxCost => maxCost;
    public int CurrentCost => currentCost;
    public int CurrentStars => currentStars;

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
    /// Arts(파란색) 카드 공격 시 코스트를 획득합니다. (기본 +1, 최대 10)
    /// </summary>
    public void AddCost(int amount = 1)
    {
        currentCost = Mathf.Clamp(currentCost + amount, 0, maxCost);
        RefreshCostUI();
    }

    public bool TrySpendCost(int amount)
    {
        if (currentCost < amount) return false;
        currentCost -= amount;
        RefreshCostUI();
        return true;
    }

    /// <summary>
    /// Quick(초록색) 카드 공격 시 스타를 획득합니다. (랜덤 1~10)
    /// 획득한 스타는 우측 하단 스타 UI에 누적되며, 다음 턴 Attack 페이즈로 넘어갈 때 AttackBox 카드들에 부여됩니다.
    /// </summary>
    public void AddStars(int amount)
    {
        if (amount <= 0) return;
        currentStars = Mathf.Max(0, currentStars + amount);
        RefreshStarUI();
    }

    /// <summary>
    /// Attack 페이즈로 넘어갈 때 현재 보유한 모든 스타를 꺼내어 소모(0으로 초기화)하고 그 개수를 반환합니다.
    /// </summary>
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
            costValueText.text = $"{currentCost}/{maxCost}";
        }

        Color activeGemColor = Color.white;
        Color emptyGemColor = new Color(0.16f, 0.22f, 0.35f, 0.45f);

        for (int i = 0; i < costGemImages.Count; i++)
        {
            if (costGemImages[i] == null) continue;
            bool isFilled = i < currentCost;
            costGemImages[i].color = isFilled ? activeGemColor : emptyGemColor;
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

    // =========================================================================
    // 1. 좌측 하단 코스트 UI 생성/연결 (참고 이미지 1: 8/10 배지 + 10개 파란 크리스탈 보석)
    // =========================================================================
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
                costTrayRoot.anchoredPosition = new Vector2(22f, 18f);
                costTrayRoot.sizeDelta = new Vector2(416f, 52f);
                costTrayRoot.localScale = Vector3.one;
                costTrayRoot.localPosition = Vector3.zero;

                Image trayImg = trayObj.GetComponent<Image>();
                trayImg.sprite = GetOrCreateCostTrayBgSprite();
                trayImg.color = Color.white;
                trayImg.raycastTarget = false;
            }
        }

        // 좌측 파란 알약 배지 ("0/10")
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
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(10f, 0f);
            badgeRect.sizeDelta = new Vector2(78f, 36f);
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
                costValueText.fontSize = 19;
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

        // 우측 10개 파란 보석(Gem) 슬롯 영역
        if (costGemsContainer == null)
        {
            Transform gemsTr = costTrayRoot.Find("CostGemsContainer");
            if (gemsTr == null)
            {
                GameObject gemsObj = new GameObject("CostGemsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                gemsObj.layer = LayerMask.NameToLayer("UI");
                gemsObj.transform.SetParent(costTrayRoot, false);

                costGemsContainer = gemsObj.GetComponent<RectTransform>();
                costGemsContainer.anchorMin = new Vector2(0f, 0.5f);
                costGemsContainer.anchorMax = new Vector2(0f, 0.5f);
                costGemsContainer.pivot = new Vector2(0f, 0.5f);
                costGemsContainer.anchoredPosition = new Vector2(96f, 0f);
                costGemsContainer.sizeDelta = new Vector2(308f, 36f);
                costGemsContainer.localScale = Vector3.one;

                HorizontalLayoutGroup hlg = gemsObj.GetComponent<HorizontalLayoutGroup>();
                hlg.spacing = 2f;
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = false;
            }
            else
            {
                costGemsContainer = gemsTr.GetComponent<RectTransform>();
            }
        }

        if (costGemSprite == null)
            costGemSprite = GetOrCreateCostGemSprite();

        // 10개의 코스트 보석 아이콘 연결/생성
        costGemImages.Clear();
        if (costGemsContainer != null)
        {
            for (int i = 0; i < costGemsContainer.childCount; i++)
            {
                Image img = costGemsContainer.GetChild(i).GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = costGemSprite;
                    costGemImages.Add(img);
                }
            }

            while (costGemImages.Count < maxCost)
            {
                int idx = costGemImages.Count + 1;
                GameObject gemObj = new GameObject($"CostGem_{idx}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                gemObj.layer = LayerMask.NameToLayer("UI");
                gemObj.transform.SetParent(costGemsContainer, false);

                RectTransform rt = gemObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(28f, 30f);
                rt.localScale = Vector3.one;

                LayoutElement le = gemObj.GetComponent<LayoutElement>();
                le.preferredWidth = 28f;
                le.preferredHeight = 30f;

                Image gemImg = gemObj.GetComponent<Image>();
                gemImg.sprite = costGemSprite;
                gemImg.preserveAspect = true;
                gemImg.raycastTarget = false;

                costGemImages.Add(gemImg);
            }
        }
    }

    // =========================================================================
    // 2. 우측 하단 스타 UI 생성/연결 (참고 이미지 2: 황금 4각 별 + x + 숫자 + CRITICAL)
    // =========================================================================
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
                starCounterRoot.anchoredPosition = new Vector2(-24f, 16f);
                starCounterRoot.sizeDelta = new Vector2(210f, 92f);
                starCounterRoot.localScale = Vector3.one;
                starCounterRoot.localPosition = Vector3.zero;

                Image bgImg = starRootObj.GetComponent<Image>();
                bgImg.sprite = UnitStatusUI.GetOrCreateSolidWhiteSprite();
                bgImg.color = new Color(0.02f, 0.05f, 0.10f, 0.65f);
                bgImg.raycastTarget = false;
            }
        }

        if (criticalStarSprite == null)
            criticalStarSprite = GetOrCreateCriticalStarSprite();

        // 좌측 황금빛 4각 별(Star) 이미지
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

        // 중앙 "x" 기호
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

        // 우측 큰 스타 개수 텍스트 ("0", "10" 등)
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

        // 하단 황금색 "CRITICAL" 텍스트
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
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        obj.layer = LayerMask.NameToLayer("UI");
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosFromLeftCenter;
        rt.sizeDelta = sizeDelta;
        rt.localScale = Vector3.one;

        Text txt = obj.GetComponent<Text>();
        txt.font = GetOrCreateFont();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.fontStyle = fontStyle;
        txt.color = color;
        txt.alignment = alignment;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;

        Outline outline = obj.GetComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(1.8f, -1.8f);

        return txt;
    }

    private static Font GetOrCreateFont()
    {
        if (cachedFont != null) return cachedFont;

        string[] fontCandidates = { "Malgun Gothic", "맑은 고딕", "Arial" };
        cachedFont = Font.CreateDynamicFontFromOSFont(fontCandidates, 24);
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return cachedFont;
    }

    // =========================================================================
    // 3. 코스트 트레이 / 코스트 보석(Gem) / 크리티컬 스타(Star) 텍스처 생성기
    // =========================================================================
    public static Sprite GetOrCreateCostTrayBgSprite()
    {
        if (cachedCostTrayBgSprite != null) return cachedCostTrayBgSprite;

        int w = 416;
        int h = 52;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];
        Color outerRim = new Color(0.76f, 0.78f, 0.90f, 0.96f);
        Color frameMid = new Color(0.58f, 0.62f, 0.78f, 0.96f);
        Color innerSlotDark = new Color(0.10f, 0.13f, 0.24f, 0.94f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float radius = 16f;
                float cx = Mathf.Clamp(x, radius, w - 1 - radius);
                float cy = Mathf.Clamp(y, radius, h - 1 - radius);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                Color c = Color.clear;
                if (dist <= radius)
                {
                    float edge = radius - dist;
                    float alpha = Mathf.Clamp01(edge / 1.5f);

                    // 우측 보석 슬롯 홈 영역 (x >= 92, y: 8 ~ h-8)
                    bool inGemGroove = (x >= 92 && x <= w - 10 && y >= 8 && y <= h - 9);
                    if (inGemGroove)
                    {
                        c = innerSlotDark;
                    }
                    else if (edge < 3.5f)
                    {
                        c = outerRim;
                    }
                    else
                    {
                        c = frameMid;
                    }
                    c.a *= alpha;
                }
                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedCostTrayBgSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        cachedCostTrayBgSprite.name = "ui_cost_tray_bg";
        return cachedCostTrayBgSprite;
    }

    public static Sprite GetOrCreateCostBadgeSprite()
    {
        if (cachedCostBadgeSprite != null) return cachedCostBadgeSprite;

        int w = 78;
        int h = 36;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];
        Color darkRim = new Color(0.06f, 0.10f, 0.28f, 1f);
        Color deepBlue = new Color(0.14f, 0.26f, 0.68f, 1f);
        Color topGloss = new Color(0.45f, 0.66f, 0.98f, 1f);

        float r = h * 0.48f;
        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / (h - 1);
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x, r, w - 1 - r);
                float cy = (h - 1) * 0.5f;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                Color c = Color.clear;
                if (dist <= r)
                {
                    float edge = r - dist;
                    float alpha = Mathf.Clamp01(edge / 1.4f);

                    if (edge < 2.2f)
                    {
                        c = darkRim;
                    }
                    else
                    {
                        c = Color.Lerp(deepBlue, topGloss, Mathf.Pow(ny, 1.6f) * 0.75f);
                    }
                    c.a = alpha;
                }
                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedCostBadgeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        cachedCostBadgeSprite.name = "ui_cost_badge";
        return cachedCostBadgeSprite;
    }

    public static Sprite GetOrCreateCostGemSprite()
    {
        if (cachedCostGemSprite != null) return cachedCostGemSprite;

        Texture2D tex = CreateCostGemTexture(48, 48);
        cachedCostGemSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        cachedCostGemSprite.name = "icon_cost_gem";
        return cachedCostGemSprite;
    }

    /// <summary>
    /// 참고 이미지 1의 파란 다면체 마나/코스트 수정(Gem) 아이콘 텍스처 생성
    /// </summary>
    public static Texture2D CreateCostGemTexture(int width = 48, int height = 48)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        Color borderDark = new Color(0.04f, 0.12f, 0.32f, 1f);
        Color deepSapphire = new Color(0.08f, 0.36f, 0.85f, 1f);
        Color midCyan = new Color(0.22f, 0.68f, 1.0f, 1f);
        Color highlightWhite = new Color(0.82f, 0.96f, 1.0f, 1f);

        for (int y = 0; y < height; y++)
        {
            float ny = ((y + 0.5f) / height - 0.5f) * 2f; // -1 ~ 1
            for (int x = 0; x < width; x++)
            {
                float nx = ((x + 0.5f) / width - 0.5f) * 2f; // -1 ~ 1

                // 팔각형 보석 외곽 거리 계산
                float ax = Mathf.Abs(nx) * 1.08f;
                float ay = Mathf.Abs(ny) * 0.96f;
                float octDist = Mathf.Max(Mathf.Max(ax, ay), (ax + ay) * 0.73f);

                Color c = Color.clear;
                if (octDist <= 0.92f)
                {
                    float edge = 0.92f - octDist;
                    float alpha = Mathf.Clamp01(edge / 0.06f);

                    if (edge < 0.08f)
                    {
                        c = borderDark;
                    }
                    else
                    {
                        // 보석의 다면체(Facet) 반사광 패턴 연출
                        float angle = Mathf.Atan2(ny, nx);
                        float facet = Mathf.Sin(angle * 4f) * 0.5f + 0.5f;
                        float topRightLight = Mathf.Clamp01((nx * 0.6f + ny * 0.8f + 0.5f));

                        c = Color.Lerp(deepSapphire, midCyan, facet * 0.6f + topRightLight * 0.4f);

                        // 중앙 및 우상단 크리스탈 하이라이트
                        if (octDist < 0.42f || (nx > 0.1f && ny > 0.1f && facet > 0.65f))
                        {
                            float spec = Mathf.Clamp01(topRightLight * 0.85f);
                            c = Color.Lerp(c, highlightWhite, spec * 0.75f);
                        }
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

    public static Sprite GetOrCreateCriticalStarSprite()
    {
        if (cachedCriticalStarSprite != null) return cachedCriticalStarSprite;

        Texture2D tex = CreateCriticalStarTexture(96, 96);
        cachedCriticalStarSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        cachedCriticalStarSprite.name = "icon_critical_star";
        return cachedCriticalStarSprite;
    }

    /// <summary>
    /// 참고 이미지 2의 황금빛 4각 별(FGO 스타일 Critical Star) 아이콘 텍스처 생성
    /// </summary>
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
            float ny = ((y + 0.5f) / height - 0.5f) * 2f; // -1 ~ 1
            for (int x = 0; x < width; x++)
            {
                float nx = ((x + 0.5f) / width - 0.5f) * 2f; // -1 ~ 1

                // 세로로 살짝 더 긴 샤프한 4각 별(Four-point star) 형태
                float ax = Mathf.Abs(nx) / 0.82f;
                float ay = Mathf.Abs(ny) / 0.95f;

                // 오목한 다이아몬드(Astroid 변형) 거리 함수: pow(ax, 0.58) + pow(ay, 0.58) <= 1
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
                        // 십자 중심 능선(Ridge) 입체 음영 연출
                        float ridgeDist = Mathf.Min(ax, ay);
                        float ridgeGlow = Mathf.Clamp01(1f - (ridgeDist / 0.16f));

                        // 상단/좌측 밝은 면과 하단/우측 따뜻한 호박색 음영 대비
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
