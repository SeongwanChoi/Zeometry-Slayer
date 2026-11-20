using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Card : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler
{
    public static event System.Action<Card> OnCardClickedEvent;
    public static event System.Action<Card, SelectableUnit> OnCardUsedOnEnemyEvent;

    [Header("카드 속성 정보 (Buster / Arts / Quick / SuperBuster 및 소유 플레이어)")]
    [SerializeField] private string cardName = "Arts";
    [SerializeField] private ECardColor cardColor = ECardColor.ARTS;
    [SerializeField] private int ownerPlayerIndex = 0;
    [SerializeField] private SelectableUnit ownerPlayerUnit;
    [SerializeField] private int assignedStars = 0; // 부여된 크리티컬 스타 개수 (1개당 크리티컬 확률 +10%)
    [SerializeField] private float accumulatedBonusDamage = 0f; // 슈퍼Buster가 흡수한 Buster 카드들의 누적 공격력
    [SerializeField] private int absorbedBusterCount = 0;       // 슈퍼Buster가 흡수한 Buster 카드 장수

    [Header("카드 색상별 기본 스프라이트 (프리팹 내장 폴백)")]
    [SerializeField] private Sprite redCardSprite;
    [SerializeField] private Sprite blueCardSprite;
    [SerializeField] private Sprite greenCardSprite;

    [Header("호버(마우스 오버) 설정 (방법 B: 하단 끝선이 들리지 않도록 확대량 >= 상승량 유지)")]
    [SerializeField] private float hoverScale = 1.22f;
    [SerializeField] private float hoverYOffset = 16f;

    [Header("카드 내부 UI 요소 참조")]
    [SerializeField] private Color normalCardColor = Color.white;
    [SerializeField] private Color attackBoxDarkColor = new Color32(156, 152, 152, 255);
    [SerializeField] private Image ownerPortraitImage;   // 카드 상단 그림칸에 들어가는 소유 플레이어 도형 이미지
    [SerializeField] private Image orderNumberImage;     // (레거시 호환) 순서 숫자 이미지
    [SerializeField] private Vector2 orderNumberSize = new Vector2(68f, 68f);
    [SerializeField] private TextMeshProUGUI cardTypeLabelText;     // 카드 이름(Buster / Arts / Quick / Super Buster) 표시 텍스트
    [SerializeField] private TextMeshProUGUI cardEffectDescText;    // 카드 효과 요약 텍스트
    [SerializeField] private TextMeshProUGUI critStarBadgeText;     // 부여된 스타/크리티컬 확률 표시 텍스트

    [Header("슬레이 더 스파이어2 스타일 코스트(Cost) 설정 (좌측 상단 젬)")]
    [SerializeField] private int baseCardCost = 1;
    [SerializeField] private bool hasZeroCostOverride = false;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Vector2 handCostOffset = new Vector2(26.5f, -26.5f);
    [SerializeField] private Vector2 boxCostOffset = new Vector2(15.5f, -15.5f);

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Image cardImage;

    private Vector2 originPosition;
    private Quaternion originRotation;
    private int originSiblingIndex;

    private Vector2 handCardSize = new Vector2(180f, 256f);
    private int savedHandIndex = -1;
    private bool isInAttackBox = false;

    private bool isDragging = false;
    private bool isInTargetingMode = false;
    private bool isHovered = false;
    private bool isInitialized = false;
    private bool isAnimatingFlight = false;

    private static Sprite cachedSuperBusterPortraitSprite;
    private static Sprite cachedPlayer1Sprite;
    private static Sprite cachedPlayer2Sprite;
    private static Sprite cachedPlayer3Sprite;

    public string CardName => cardColor.GetCardName();
    public ECardColor CardColor => cardColor;
    public bool IsSuperBuster => cardColor == ECardColor.SUPER_BUSTER;
    public int OwnerPlayerIndex { get => ownerPlayerIndex; set => ownerPlayerIndex = value; }
    public SelectableUnit OwnerPlayerUnit { get => ownerPlayerUnit; set => ownerPlayerUnit = value; }
    public bool IsInAttackBox => isInAttackBox;
    public bool IsAnimatingFlight { get => isAnimatingFlight; set => isAnimatingFlight = value; }
    public int SavedHandIndex { get => savedHandIndex; set => savedHandIndex = value; }
    public int AssignedStars => assignedStars;
    public float CriticalChancePercent => Mathf.Clamp(assignedStars * 10f, 0f, 100f);
    public float AccumulatedBonusDamage => accumulatedBonusDamage;
    public int AbsorbedBusterCount => absorbedBusterCount;
    public RectTransform CritBadgeRect => critStarBadgeText != null ? critStarBadgeText.rectTransform : rectTransform;
    public Vector3 CritBadgeWorldPosition => CritBadgeRect != null ? CritBadgeRect.position : transform.position;

    /// <summary>
    /// 현재 실제로 지불해야 하는 코스트 (Player_1의 1스킬 0코스트 버프 반영)
    /// </summary>
    public int EffectiveCost
    {
        get
        {
            if (hasZeroCostOverride)
                return 0;
            if (HandManager.Ins != null && HandManager.Ins.IsZeroCostQuickBusterActive)
            {
                if (cardColor == ECardColor.QUICK || cardColor == ECardColor.BUSTER || cardColor == ECardColor.SUPER_BUSTER)
                    return 0;
            }
            return Mathf.Max(0, baseCardCost);
        }
    }

    public int CardCost => EffectiveCost;

    public void SetCost(int newCost)
    {
        baseCardCost = Mathf.Max(0, newCost);
        RefreshCostText();
    }

    public void SetZeroCostOverride(bool zeroCost)
    {
        hasZeroCostOverride = zeroCost;
        RefreshCostText();
    }

    private Coroutine punchScaleCoroutine;

    private void Awake()
    {
        InitComponents();
    }

    private void InitComponents()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (cardImage == null)
            cardImage = GetComponent<Image>();

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // 방법 B 호버 떨림 방지: 기존 프리팹 직렬화 값(1.15 / 40~42)이 남아있으면 안전한 수치(1.22 / 16)로 자동 보정
        if (hoverScale <= 1.16f && hoverYOffset >= 35f)
        {
            hoverScale = 1.22f;
            hoverYOffset = 16f;
        }

        EnsureOwnerPortraitImage();
        EnsureOrderNumberImage();
        EnsureCardTextElements();
    }

    /// <summary>
    /// 카드의 소유 플레이어 정보, 카드 종류(Buster / Arts / Quick / SuperBuster), 프레임 스프라이트 및 그림칸 플레이어 이미지를 갱신합니다.
    /// </summary>
    public void SetupCard(
        int playerIndex,
        SelectableUnit ownerUnit,
        ECardColor color,
        Sprite frameSprite,
        int initialHandIndex,
        float bonusDamage = 0f,
        int absorbedCount = 0)
    {
        InitComponents();
        ownerPlayerIndex = playerIndex;
        ownerPlayerUnit = ownerUnit;
        cardColor = color;
        cardName = color.GetCardName();
        savedHandIndex = initialHandIndex;
        baseCardCost = color.GetBaseCost();
        hasZeroCostOverride = false;
        accumulatedBonusDamage = Mathf.Max(0f, bonusDamage);
        absorbedBusterCount = Mathf.Max(0, absorbedCount);

        Sprite resolvedFrameSprite = frameSprite != null ? frameSprite : GetBuiltInSprite(color);
        if (cardImage != null)
        {
            if (resolvedFrameSprite != null)
                cardImage.sprite = resolvedFrameSprite;
            cardImage.color = isInAttackBox ? attackBoxDarkColor : normalCardColor;
            cardImage.enabled = true;
        }

        RefreshOwnerPortrait();
        RefreshCardTypeLabel();
        RefreshCostText();
        SetAssignedStars(assignedStars);
    }

    /// <summary>
    /// 슈퍼Buster 카드가 일반 Buster 카드를 흡수할 때 호출되어 공격력을 누적시킵니다.
    /// </summary>
    public void AbsorbBusterCardDamage(float addedBusterDamage)
    {
        accumulatedBonusDamage += Mathf.Max(0f, addedBusterDamage);
        absorbedBusterCount++;
        RefreshCardTypeLabel();
        PunchScale(1.24f, 0.22f);
    }

    /// <summary>
    /// 카드 상단 그림칸에 해당 카드를 소유한 플레이어 오브젝트의 이미지(또는 슈퍼Buster 전용 이미지)를 넣습니다.
    /// </summary>
    public void RefreshOwnerPortrait()
    {
        EnsureOwnerPortraitImage();
        if (ownerPortraitImage == null) return;

        Sprite portrait = null;
        if (cardColor == ECardColor.SUPER_BUSTER)
        {
            portrait = GetOrCreateSuperBusterPortraitSprite();
        }

        if (portrait == null && ownerPlayerUnit != null && ownerPlayerUnit.UnitPortraitSprite != null)
        {
            portrait = ownerPlayerUnit.UnitPortraitSprite;
        }

        if (portrait == null)
        {
            portrait = GetFallbackPlayerSprite(ownerPlayerIndex);
        }

        if (portrait != null)
        {
            ownerPortraitImage.sprite = portrait;
            ownerPortraitImage.color = Color.white;
            ownerPortraitImage.gameObject.SetActive(true);
        }
        else
        {
            ownerPortraitImage.gameObject.SetActive(false);
        }
    }

    private static Sprite GetOrCreateSuperBusterPortraitSprite()
    {
        if (cachedSuperBusterPortraitSprite == null)
        {
            cachedSuperBusterPortraitSprite = UIHelper.LoadSpriteFromAssetOrDisk("Assets/Resource/Charactor/player_SuperBuster.png", "player_SuperBuster");
        }
        return cachedSuperBusterPortraitSprite;
    }

    private static Sprite GetFallbackPlayerSprite(int playerIndex)
    {
        switch (Mathf.Clamp(playerIndex, 0, 2))
        {
            case 0:
                if (cachedPlayer1Sprite == null)
                    cachedPlayer1Sprite = UIHelper.LoadSpriteFromAssetOrDisk("Assets/Resource/Charactor/player_Arts.png", "player_Arts");
                return cachedPlayer1Sprite;
            case 1:
                if (cachedPlayer2Sprite == null)
                    cachedPlayer2Sprite = UIHelper.LoadSpriteFromAssetOrDisk("Assets/Resource/Charactor/player_Quick.png", "player_Quick");
                return cachedPlayer2Sprite;
            default:
                if (cachedPlayer3Sprite == null)
                    cachedPlayer3Sprite = UIHelper.LoadSpriteFromAssetOrDisk("Assets/Resource/Charactor/player_Buster.png", "player_Buster");
                return cachedPlayer3Sprite;
        }
    }

    /// <summary>
    /// 이 카드에 부여된 스타 개수를 설정하고 크리티컬 확률(스타 1개당 10%) UI를 갱신합니다.
    /// </summary>
    public void SetAssignedStars(int stars)
    {
        assignedStars = Mathf.Max(0, stars);

        if (critStarBadgeText != null)
        {
            if (assignedStars > 0)
            {
                int critPct = Mathf.Clamp(assignedStars * 10, 0, 100);
                critStarBadgeText.text = $"★{assignedStars} ({critPct}%)";
                critStarBadgeText.transform.SetAsLastSibling();
                critStarBadgeText.gameObject.SetActive(true);
            }
            else
            {
                critStarBadgeText.gameObject.SetActive(false);
            }
        }
    }

    public void AddOneAssignedStar()
    {
        SetAssignedStars(assignedStars + 1);
        PunchScale(1.16f, 0.16f);
    }

    public void PunchScale(float multiplier = 1.15f, float duration = 0.18f)
    {
        if (!gameObject.activeInHierarchy) return;
        if (punchScaleCoroutine != null)
        {
            StopCoroutine(punchScaleCoroutine);
        }
        punchScaleCoroutine = StartCoroutine(CoPunchScale(multiplier, duration));
    }

    private IEnumerator CoPunchScale(float multiplier, float duration)
    {
        Vector3 baseScale = isInAttackBox ? Vector3.one : (isHovered ? Vector3.one * hoverScale : Vector3.one);
        float half = Mathf.Max(0.01f, duration * 0.45f);
        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            transform.localScale = Vector3.Lerp(baseScale, baseScale * multiplier, t);
            yield return null;
        }

        elapsed = 0f;
        float remain = Mathf.Max(0.01f, duration - half);
        while (elapsed < remain)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / remain);
            transform.localScale = Vector3.Lerp(baseScale * multiplier, baseScale, t);
            yield return null;
        }

        transform.localScale = baseScale;
        punchScaleCoroutine = null;
    }

    public void RefreshCardTypeLabel()
    {
        if (cardTypeLabelText != null)
        {
            if (cardColor == ECardColor.SUPER_BUSTER)
            {
                cardTypeLabelText.text = "SUPER BUSTER";
                cardTypeLabelText.color = new Color(1f, 0.86f, 0.28f, 1f);
                cardTypeLabelText.fontSize = isInAttackBox ? 13 : 18;
            }
            else
            {
                cardTypeLabelText.text = cardColor.GetCardName().ToUpperInvariant();
                switch (cardColor)
                {
                    case ECardColor.BUSTER:
                        cardTypeLabelText.color = new Color(1f, 0.72f, 0.76f, 0.95f);
                        break;
                    case ECardColor.ARTS:
                        cardTypeLabelText.color = new Color(0.68f, 0.92f, 1f, 0.95f);
                        break;
                    case ECardColor.QUICK:
                        cardTypeLabelText.color = new Color(0.68f, 1f, 0.80f, 0.95f);
                        break;
                }
                cardTypeLabelText.fontSize = isInAttackBox ? 15 : 20;
            }
        }

        if (cardEffectDescText != null)
        {
            float ownerAtk = ownerPlayerUnit != null ? ownerPlayerUnit.CalculateAttackDamage() : 5f;
            switch (cardColor)
            {
                case ECardColor.ARTS:
                    cardEffectDescText.text = $"피해 {UIHelper.FormatCompactNumber(ownerAtk)}\n드로우+1 / 다음턴 코스트+1";
                    cardEffectDescText.color = new Color(0.82f, 0.94f, 1f, 0.95f);
                    break;
                case ECardColor.QUICK:
                    cardEffectDescText.text = $"피해 {UIHelper.FormatCompactNumber(ownerAtk)}\n크리티컬 스타 생성";
                    cardEffectDescText.color = new Color(0.82f, 1f, 0.88f, 0.95f);
                    break;
                case ECardColor.BUSTER:
                    float busterDmg = ownerAtk * 2f;
                    cardEffectDescText.text = $"피해 {UIHelper.FormatCompactNumber(busterDmg)}\n(공격력 × 2)";
                    cardEffectDescText.color = new Color(1f, 0.84f, 0.86f, 0.95f);
                    break;
                case ECardColor.SUPER_BUSTER:
                    float totalSuperDmg = (ownerAtk * 2f) + accumulatedBonusDamage;
                    cardEffectDescText.text = $"피해 {UIHelper.FormatCompactNumber(totalSuperDmg)}\n(Buster {absorbedBusterCount}장 흡수 누적)";
                    cardEffectDescText.color = new Color(1f, 0.92f, 0.45f, 1f);
                    break;
            }
            cardEffectDescText.gameObject.SetActive(!isInAttackBox);
        }
    }

    public float CalculateCardRawDamage()
    {
        float baseAtk = ownerPlayerUnit != null ? ownerPlayerUnit.CalculateAttackDamage() : 5f;
        switch (cardColor)
        {
            case ECardColor.BUSTER:
                return baseAtk * 2f; // PDF 1p: 사용시 공격력에 *2를 연산하여 계산하여 공격한다
            case ECardColor.SUPER_BUSTER:
                return (baseAtk * 2f) + accumulatedBonusDamage;
            case ECardColor.ARTS:
            case ECardColor.QUICK:
            default:
                return baseAtk;
        }
    }

    public Sprite GetBuiltInSprite(ECardColor color)
    {
        switch (color)
        {
            case ECardColor.RED:
            case ECardColor.SUPER_BUSTER:
                return redCardSprite != null ? redCardSprite : blueCardSprite;
            case ECardColor.BLUE:
                return blueCardSprite;
            case ECardColor.GREEN:
                return greenCardSprite != null ? greenCardSprite : blueCardSprite;
            default:
                return blueCardSprite;
        }
    }

    private void EnsureOwnerPortraitImage()
    {
        if (ownerPortraitImage == null)
        {
            Transform child = transform.Find("OwnerPortrait");
            if (child != null)
                ownerPortraitImage = child.GetComponent<Image>();
        }

        if (ownerPortraitImage == null)
        {
            GameObject portObj = new GameObject("OwnerPortrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            portObj.layer = gameObject.layer;
            portObj.transform.SetParent(transform, false);

            RectTransform rt = portObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 34f); // 카드 상단 그림칸 정중앙
            rt.sizeDelta = new Vector2(106f, 106f);
            rt.localScale = Vector3.one;

            ownerPortraitImage = portObj.GetComponent<Image>();
            ownerPortraitImage.raycastTarget = false;
            ownerPortraitImage.preserveAspect = true;
        }
    }

    private void EnsureOrderNumberImage()
    {
        if (orderNumberImage == null)
        {
            Transform child = transform.Find("OrderNumber");
            if (child != null)
                orderNumberImage = child.GetComponent<Image>();
        }

        if (orderNumberImage == null)
        {
            GameObject numObj = new GameObject("OrderNumber", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            numObj.layer = gameObject.layer;
            numObj.transform.SetParent(transform, false);

            RectTransform numRect = numObj.GetComponent<RectTransform>();
            numRect.anchorMin = new Vector2(0.5f, 0.5f);
            numRect.anchorMax = new Vector2(0.5f, 0.5f);
            numRect.pivot = new Vector2(0.5f, 0.5f);
            numRect.anchoredPosition = Vector2.zero;
            numRect.sizeDelta = orderNumberSize;

            orderNumberImage = numObj.GetComponent<Image>();
            orderNumberImage.raycastTarget = false;
            orderNumberImage.preserveAspect = true;
            numObj.SetActive(false);
        }
    }

    private void EnsureCardTextElements()
    {
        if (cardTypeLabelText == null)
        {
            Transform lblTr = transform.Find("CardTypeLabel");
            if (lblTr != null)
                cardTypeLabelText = UIHelper.GetOrAddTMPText(lblTr.gameObject);
        }

        if (cardTypeLabelText == null)
        {
            GameObject lblObj = new GameObject("CardTypeLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            lblObj.layer = gameObject.layer;
            lblObj.transform.SetParent(transform, false);

            RectTransform rt = lblObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 68f);
            rt.sizeDelta = new Vector2(150f, 28f);
            rt.localScale = Vector3.one;

            cardTypeLabelText = lblObj.GetComponent<TextMeshProUGUI>();
            cardTypeLabelText.font = GetOrCreateCardFont();
            cardTypeLabelText.fontSize = 20;
            cardTypeLabelText.fontStyle = FontStyles.Bold;
            cardTypeLabelText.alignment = TextAlignmentOptions.Midline;
            cardTypeLabelText.textWrappingMode = TextWrappingModes.NoWrap;
            cardTypeLabelText.overflowMode = TextOverflowModes.Overflow;
            cardTypeLabelText.raycastTarget = false;
        }
        else
        {
            cardTypeLabelText.rectTransform.anchoredPosition = new Vector2(0f, 68f);
        }

        if (cardEffectDescText == null)
        {
            Transform descTr = transform.Find("CardEffectDesc");
            if (descTr != null)
                cardEffectDescText = UIHelper.GetOrAddTMPText(descTr.gameObject);
        }

        if (cardEffectDescText == null)
        {
            GameObject descObj = new GameObject("CardEffectDesc", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            descObj.layer = gameObject.layer;
            descObj.transform.SetParent(transform, false);

            RectTransform rt = descObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 22f);
            rt.sizeDelta = new Vector2(148f, 42f);
            rt.localScale = Vector3.one;

            cardEffectDescText = descObj.GetComponent<TextMeshProUGUI>();
            cardEffectDescText.font = GetOrCreateCardFont();
            cardEffectDescText.fontSize = 12;
            cardEffectDescText.fontStyle = FontStyles.Bold;
            cardEffectDescText.alignment = TextAlignmentOptions.Midline;
            cardEffectDescText.textWrappingMode = TextWrappingModes.NoWrap;
            cardEffectDescText.overflowMode = TextOverflowModes.Overflow;
            cardEffectDescText.raycastTarget = false;
        }

        if (critStarBadgeText == null)
        {
            Transform critTr = transform.Find("CritStarBadge");
            if (critTr != null)
                critStarBadgeText = UIHelper.GetOrAddTMPText(critTr.gameObject);
        }

        if (critStarBadgeText == null)
        {
            GameObject critObj = new GameObject("CritStarBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            critObj.layer = gameObject.layer;
            critObj.transform.SetParent(transform, false);

            RectTransform rt = critObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(12f, -8f);
            rt.sizeDelta = new Vector2(110f, 24f);
            rt.localScale = Vector3.one;

            critStarBadgeText = critObj.GetComponent<TextMeshProUGUI>();
            critStarBadgeText.font = GetOrCreateCardFont();
            critStarBadgeText.fontSize = 15;
            critStarBadgeText.fontStyle = FontStyles.Bold;
            critStarBadgeText.alignment = TextAlignmentOptions.Midline;
            critStarBadgeText.color = new Color(1f, 0.88f, 0.25f, 1f);
            critStarBadgeText.textWrappingMode = TextWrappingModes.NoWrap;
            critStarBadgeText.overflowMode = TextOverflowModes.Overflow;
            critStarBadgeText.raycastTarget = false;

            critObj.SetActive(false);
        }

        EnsureCostTextElement();
    }

    private void EnsureCostTextElement()
    {
        if (costText == null)
        {
            Transform costTr = transform.Find("CostText");
            if (costTr != null)
                costText = UIHelper.GetOrAddTMPText(costTr.gameObject);
        }

        if (costText == null)
        {
            GameObject costObj = new GameObject("CostText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            costObj.layer = gameObject.layer;
            costObj.transform.SetParent(transform, false);

            RectTransform rt = costObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = isInAttackBox ? boxCostOffset : handCostOffset;
            rt.sizeDelta = new Vector2(40f, 40f);
            rt.localScale = Vector3.one;

            costText = costObj.GetComponent<TextMeshProUGUI>();
            costText.font = GetOrCreateCardFont();
            costText.fontSize = isInAttackBox ? 14 : 22;
            costText.fontStyle = FontStyles.Bold;
            costText.alignment = TextAlignmentOptions.Midline;
            costText.color = Color.white;
            costText.textWrappingMode = TextWrappingModes.NoWrap;
            costText.overflowMode = TextOverflowModes.Overflow;
            costText.raycastTarget = false;
        }

        RefreshCostText();
    }

    public void RefreshCostText()
    {
        if (costText == null) return;

        int effCost = EffectiveCost;
        costText.text = effCost.ToString();
        costText.fontSize = isInAttackBox ? 14 : 22;
        costText.rectTransform.anchoredPosition = isInAttackBox ? boxCostOffset : handCostOffset;

        // 0코스트 할인 적용 중이면 연두색으로 강조, 코스트 부족이면 붉은빛으로 표시
        if (effCost < baseCardCost)
        {
            costText.color = new Color(0.45f, 1f, 0.55f, 1f);
        }
        else if (BattleResourceUIManager.Ins != null && BattleResourceUIManager.Ins.CurrentCost < effCost)
        {
            costText.color = new Color(1f, 0.42f, 0.42f, 1f);
        }
        else
        {
            costText.color = Color.white;
        }

        costText.transform.SetAsLastSibling();
        costText.gameObject.SetActive(true);
    }

    private static TMP_FontAsset GetOrCreateCardFont() => UIHelper.GetOrCreateTMPFont();

    private void Start()
    {
        InitComponents();

        if (!isInitialized)
        {
            originPosition = rectTransform.anchoredPosition;
            originRotation = rectTransform.localRotation;
            originSiblingIndex = transform.GetSiblingIndex();
            if (rectTransform.sizeDelta.sqrMagnitude > 0f)
                handCardSize = rectTransform.sizeDelta;
        }
    }

    private bool CanInteract()
    {
        if (isAnimatingFlight) return false;
        if (PauseMenuManager.Ins != null && PauseMenuManager.Ins.IsPaused) return false;
        if (SkillConfirmModalUI.Ins != null && SkillConfirmModalUI.Ins.IsModalOpen) return false;
        if (DeckViewerModalUI.Ins != null && DeckViewerModalUI.Ins.IsModalOpen) return false;

        return BattleManager.Ins == null || BattleManager.Ins.IsPlayerInputAllowed;
    }

    private void ApplyAnchoredPositionZeroZ(Vector2 anchoredPos)
    {
        rectTransform.anchoredPosition = anchoredPos;
        Vector3 lp = rectTransform.localPosition;
        if (lp.z != 0f)
            rectTransform.localPosition = new Vector3(lp.x, lp.y, 0f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CanInteract()) return;
        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayCardPickup();
        OnCardClickedEvent?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanInteract()) return;
        if (isDragging) return;

        isHovered = true;
        originSiblingIndex = transform.GetSiblingIndex();
        transform.SetAsLastSibling();

        RefreshCardTypeLabel();
        RefreshCostText();

        transform.localScale = Vector3.one * hoverScale;
        rectTransform.localRotation = Quaternion.identity;
        ApplyAnchoredPositionZeroZ(originPosition + new Vector2(0f, hoverYOffset));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDragging) return;
        if (!isHovered) return;

        isHovered = false;
        transform.localScale = Vector3.one;

        ApplyAnchoredPositionZeroZ(originPosition);
        rectTransform.localRotation = originRotation;
        transform.SetSiblingIndex(originSiblingIndex);
    }

    // =========================================================================
    // PDF 5페이지 명세: 카드 드래그 & PlayerHand 영역 밖 이동 시 중앙 하단 고정 + Spline 화살표 타겟팅
    // =========================================================================
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanInteract()) return;

        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayCardPickup();

        if (!isHovered)
            originSiblingIndex = transform.GetSiblingIndex();

        isDragging = true;
        isInTargetingMode = false;
        isHovered = false;

        transform.localScale = Vector3.one * hoverScale;
        rectTransform.localRotation = Quaternion.identity;
        canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling();

        OnCardClickedEvent?.Invoke(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        bool isOutsideHandArea = HandManager.Ins != null && !HandManager.Ins.IsScreenPointInsideHandArea(eventData.position, eventData.pressEventCamera);

        if (isOutsideHandArea)
        {
            // PDF 5p:
            // - 카드를 잡고 있는 상태에서 마우스가 PlayerHand 영역 밖으로 나가면 집은 카드를 사용하는 것으로 간주
            // - 사용하려는 카드는 손패의 가운데로 올라오고 카드는 화면 하단에 위치시킨다
            // - spline을 사용해서 사용하려는 카드에서 적 방향으로 화살표를 그린다
            isInTargetingMode = true;
            Vector2 centerBottomPos = new Vector2(0f, 62f);
            ApplyAnchoredPositionZeroZ(centerBottomPos);
            rectTransform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * 1.18f;

            if (SplineTargetingArrow.Ins != null)
            {
                Vector2 arrowStartCanvasPos = HandManager.Ins.GetCardTopCenterCanvasLocalPos(rectTransform);
                SplineTargetingArrow.Ins.UpdateTargetingArrow(arrowStartCanvasPos, eventData.position);
            }
        }
        else
        {
            // PlayerHand 영역 내부에서는 카드를 마우스로 들고 자유롭게 움직일 수 있다
            isInTargetingMode = false;
            if (SplineTargetingArrow.Ins != null && SplineTargetingArrow.Ins.IsArrowActive)
            {
                SplineTargetingArrow.Ins.HideArrow();
            }

            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            {
                rectTransform.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;
        canvasGroup.blocksRaycasts = true;

        SelectableUnit hoveredEnemy = (isInTargetingMode && SplineTargetingArrow.Ins != null)
            ? SplineTargetingArrow.Ins.CurrentHoveredEnemy
            : null;

        if (SplineTargetingArrow.Ins != null)
        {
            SplineTargetingArrow.Ins.HideArrow();
        }

        isInTargetingMode = false;

        if (CanInteract() && hoveredEnemy != null && !hoveredEnemy.IsDead)
        {
            // 적 위에 놓았을 때 카드 사용 시도
            if (BattleManager.Ins != null && BattleManager.Ins.TryPlayCardOnEnemy(this, hoveredEnemy))
            {
                OnCardUsedOnEnemyEvent?.Invoke(this, hoveredEnemy);
                return;
            }
        }

        // 사용되지 않았으면 손패 원래 위치로 복귀
        transform.localScale = Vector3.one;
        ApplyAnchoredPositionZeroZ(originPosition);
        rectTransform.localRotation = originRotation;
        transform.SetSiblingIndex(originSiblingIndex);
    }

    public void SetNewOriginPosition(Vector2 newAnchoredPos)
    {
        SetCardTransform(newAnchoredPos, Quaternion.identity, transform.GetSiblingIndex());
    }

    public void SetCardTransform(Vector2 newAnchoredPos, Quaternion newRotation, int siblingIndex)
    {
        InitComponents();
        isInitialized = true;
        isInAttackBox = false;

        originPosition = newAnchoredPos;
        originRotation = newRotation;
        originSiblingIndex = siblingIndex;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = handCardSize;

        if (cardImage != null)
            cardImage.color = normalCardColor;

        if (orderNumberImage != null)
            orderNumberImage.gameObject.SetActive(false);

        RefreshOwnerPortrait();
        RefreshCardTypeLabel();
        RefreshCostText();
        SetAssignedStars(assignedStars);

        if (!isDragging && !isHovered && !isAnimatingFlight)
        {
            ApplyAnchoredPositionZeroZ(originPosition);
            rectTransform.localRotation = originRotation;
            transform.localScale = Vector3.one;
            transform.SetSiblingIndex(originSiblingIndex);
        }
    }

    public void SetHandCardSize(Vector2 size)
    {
        InitComponents();
        handCardSize = size;
        if (!isInAttackBox)
            rectTransform.sizeDelta = handCardSize;
    }

    public void SetAttackBoxTransform(RectTransform boxParent, Vector2 boxAnchoredPos, Vector2 boxCardSize, int siblingIndex, Sprite orderSprite = null)
    {
        InitComponents();
        isInitialized = true;
        isInAttackBox = true;

        if (transform.parent != boxParent)
            transform.SetParent(boxParent, false);

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = boxCardSize;

        if (cardImage != null)
            cardImage.color = attackBoxDarkColor;

        RefreshCardTypeLabel();
        RefreshCostText();
        SetAssignedStars(assignedStars);

        originPosition = boxAnchoredPos;
        originRotation = Quaternion.identity;
        originSiblingIndex = siblingIndex;

        if (!isDragging && !isHovered)
        {
            ApplyAnchoredPositionZeroZ(originPosition);
            rectTransform.localRotation = originRotation;
            transform.localScale = Vector3.one;
            transform.SetSiblingIndex(originSiblingIndex);
        }
    }
}
