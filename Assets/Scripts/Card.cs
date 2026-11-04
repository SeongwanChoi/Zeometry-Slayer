using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Card : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("카드 속성 정보 (Buster / Arts / Quick 및 소유 플레이어)")]
    [SerializeField] private string cardName = "Arts";
    [SerializeField] private ECardColor cardColor = ECardColor.ARTS;
    [SerializeField] private int ownerPlayerIndex = 0;
    [SerializeField] private SelectableUnit ownerPlayerUnit;
    [SerializeField] private int assignedStars = 0; // Attack 페이즈 시작 시 부여된 크리티컬 스타 개수 (1개당 크리티컬 확률 +10%)

    [Header("카드 색상별 기본 스프라이트 (프리팹 내장 폴백)")]
    [SerializeField] private Sprite redCardSprite;
    [SerializeField] private Sprite blueCardSprite;
    [SerializeField] private Sprite greenCardSprite;

    [Header("호버(마우스 오버) 설정")]
    [SerializeField] private float hoverScale = 1.15f;   // 마우스 올렸을 때 커지는 배율
    [SerializeField] private float hoverYOffset = 40f;   // 마우스 올렸을 때 위로 올라가는 높이

    [Header("AttackBox 진입 시 색상, 순서 및 크리티컬 확률 표시")]
    [SerializeField] private Color normalCardColor = Color.white; // 손패에 있을 때 원래 색상 (255, 255, 255)
    [SerializeField] private Color attackBoxDarkColor = new Color32(156, 152, 152, 255); // AttackBox 안에서 어두워지는 색상 (156, 152, 152)
    [SerializeField] private Image orderNumberImage;     // 카드 위에 띄울 금색 순서 숫자 이미지 컴포넌트
    [SerializeField] private Vector2 orderNumberSize = new Vector2(68f, 68f); // 금색 숫자 이미지 크기
    [SerializeField] private Text cardTypeLabelText;     // 카드 이름(Buster / Arts / Quick) 표시 텍스트
    [SerializeField] private Text critStarBadgeText;     // AttackBox에서 부여된 스타/크리티컬 확률 표시 텍스트

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Image cardImage;

    private Vector2 originPosition;     // 현재 속한 영역(손패 or AttackBox)에서의 원래 위치
    private Quaternion originRotation;  // 현재 속한 영역에서의 원래 회전값
    private int originSiblingIndex;     // 원래 하이어라키 순서 (겹침 순서 복구용)

    private Vector2 handCardSize = new Vector2(180f, 256f); // 손패에 있을 때의 원래 카드 사이즈
    private int savedHandIndex = -1;    // AttackBox로 이동하기 전 손패에서의 원래 순서(자리) 기억
    private bool isInAttackBox = false; // 현재 AttackBox 안에 들어있는 상태인지 여부

    private bool isDragging = false;
    private bool isHovered = false;
    private bool isInitialized = false;

    private static Font cachedCardFont;

    public string CardName => cardColor.GetCardName();
    public ECardColor CardColor => cardColor;
    public int OwnerPlayerIndex { get => ownerPlayerIndex; set => ownerPlayerIndex = value; }
    public SelectableUnit OwnerPlayerUnit { get => ownerPlayerUnit; set => ownerPlayerUnit = value; }
    public bool IsInAttackBox => isInAttackBox;
    public int SavedHandIndex { get => savedHandIndex; set => savedHandIndex = value; }
    public int AssignedStars => assignedStars;
    public float CriticalChancePercent => Mathf.Clamp(assignedStars * 10f, 0f, 100f);

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

        EnsureOrderNumberImage();
        EnsureCardTextElements();
    }

    /// <summary>
    /// 카드의 소유 플레이어 정보, 카드 종류(Buster / Arts / Quick) 및 스프라이트를 갱신합니다.
    /// </summary>
    public void SetupCard(int playerIndex, SelectableUnit ownerUnit, ECardColor color, Sprite sprite, int initialHandIndex)
    {
        InitComponents();
        ownerPlayerIndex = playerIndex;
        ownerPlayerUnit = ownerUnit;
        cardColor = color;
        cardName = color.GetCardName();
        savedHandIndex = initialHandIndex;
        SetAssignedStars(0);

        Sprite resolvedSprite = sprite != null ? sprite : GetBuiltInSprite(color);
        if (cardImage != null)
        {
            if (resolvedSprite != null)
                cardImage.sprite = resolvedSprite;
            cardImage.color = isInAttackBox ? attackBoxDarkColor : normalCardColor;
            cardImage.enabled = true;
        }

        RefreshCardTypeLabel();
    }

    /// <summary>
    /// Attack 페이즈 진입 시 이 카드에 부여된 스타 개수를 설정하고 크리티컬 확률(스타 1개당 10%) UI를 갱신합니다.
    /// </summary>
    public void SetAssignedStars(int stars)
    {
        assignedStars = Mathf.Max(0, stars);

        if (critStarBadgeText != null)
        {
            if (assignedStars > 0 && isInAttackBox)
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

    private void RefreshCardTypeLabel()
    {
        if (cardTypeLabelText == null) return;

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
        cardTypeLabelText.fontSize = isInAttackBox ? 15 : 22;
    }

    public Sprite GetBuiltInSprite(ECardColor color)
    {
        switch (color)
        {
            case ECardColor.RED:   return redCardSprite != null ? redCardSprite : blueCardSprite;
            case ECardColor.BLUE:  return blueCardSprite;
            case ECardColor.GREEN: return greenCardSprite != null ? greenCardSprite : blueCardSprite;
            default:               return blueCardSprite;
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
            numRect.localPosition = Vector3.zero;
            numRect.localScale = Vector3.one;
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
                cardTypeLabelText = lblTr.GetComponent<Text>();
        }

        if (cardTypeLabelText == null)
        {
            GameObject lblObj = new GameObject("CardTypeLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            lblObj.layer = gameObject.layer;
            lblObj.transform.SetParent(transform, false);

            RectTransform rt = lblObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 18f);
            rt.sizeDelta = new Vector2(150f, 32f);
            rt.localScale = Vector3.one;

            cardTypeLabelText = lblObj.GetComponent<Text>();
            cardTypeLabelText.font = GetOrCreateCardFont();
            cardTypeLabelText.fontSize = 22;
            cardTypeLabelText.fontStyle = FontStyle.Bold;
            cardTypeLabelText.alignment = TextAnchor.MiddleCenter;
            cardTypeLabelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            cardTypeLabelText.verticalOverflow = VerticalWrapMode.Overflow;
            cardTypeLabelText.raycastTarget = false;

            Outline outline = lblObj.GetComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.04f, 0.10f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        if (critStarBadgeText == null)
        {
            Transform critTr = transform.Find("CritStarBadge");
            if (critTr != null)
                critStarBadgeText = critTr.GetComponent<Text>();
        }

        if (critStarBadgeText == null)
        {
            GameObject critObj = new GameObject("CritStarBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            critObj.layer = gameObject.layer;
            critObj.transform.SetParent(transform, false);

            RectTransform rt = critObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -8f);
            rt.sizeDelta = new Vector2(110f, 24f);
            rt.localScale = Vector3.one;

            critStarBadgeText = critObj.GetComponent<Text>();
            critStarBadgeText.font = GetOrCreateCardFont();
            critStarBadgeText.fontSize = 14;
            critStarBadgeText.fontStyle = FontStyle.Bold;
            critStarBadgeText.alignment = TextAnchor.MiddleCenter;
            critStarBadgeText.color = new Color(1f, 0.88f, 0.25f, 1f);
            critStarBadgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            critStarBadgeText.verticalOverflow = VerticalWrapMode.Overflow;
            critStarBadgeText.raycastTarget = false;

            Outline outline = critObj.GetComponent<Outline>();
            outline.effectColor = new Color(0.15f, 0.06f, 0.01f, 0.98f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);

            critObj.SetActive(false);
        }
    }

    private static Font GetOrCreateCardFont()
    {
        if (cachedCardFont != null) return cachedCardFont;

        string[] fontCandidates = { "Malgun Gothic", "맑은 고딕", "Arial" };
        cachedCardFont = Font.CreateDynamicFontFromOSFont(fontCandidates, 20);
        if (cachedCardFont == null)
            cachedCardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return cachedCardFont;
    }

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
        if (PauseMenuManager.Ins != null && PauseMenuManager.Ins.IsPaused)
            return false;

        return BattleManager.Ins == null || BattleManager.Ins.IsPlayerInputAllowed;
    }

    private void ApplyAnchoredPositionZeroZ(Vector2 anchoredPos)
    {
        rectTransform.anchoredPosition = anchoredPos;
        Vector3 lp = rectTransform.localPosition;
        if (lp.z != 0f)
            rectTransform.localPosition = new Vector3(lp.x, lp.y, 0f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanInteract()) return;
        if (isDragging) return;

        isHovered = true;
        originSiblingIndex = transform.GetSiblingIndex();
        transform.SetAsLastSibling();

        if (isInAttackBox)
        {
            transform.localScale = Vector3.one * 1.08f;
            ApplyAnchoredPositionZeroZ(originPosition + new Vector2(0f, 10f));
        }
        else
        {
            transform.localScale = Vector3.one * hoverScale;
            rectTransform.localRotation = Quaternion.identity;
            ApplyAnchoredPositionZeroZ(originPosition + new Vector2(0f, hoverYOffset));
        }
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanInteract()) return;

        if (!isHovered)
            originSiblingIndex = transform.GetSiblingIndex();

        isDragging = true;
        isHovered = false;

        transform.localScale = Vector3.one * (isInAttackBox ? 1.08f : hoverScale);
        rectTransform.localRotation = Quaternion.identity;
        canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        RectTransform parentRect = rectTransform.parent as RectTransform;
        if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            rectTransform.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
        }
        else
        {
            transform.position = eventData.position;
        }

        if (AttackBoxManager.Ins != null && AttackBoxManager.Ins.IsPointerOverBox(eventData))
        {
            transform.localScale = Vector3.one;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;
        canvasGroup.blocksRaycasts = true;
        transform.localScale = Vector3.one;

        if (!CanInteract())
        {
            ApplyAnchoredPositionZeroZ(originPosition);
            rectTransform.localRotation = originRotation;
            transform.SetSiblingIndex(originSiblingIndex);
            return;
        }

        bool isOverAttackBox = AttackBoxManager.Ins != null && AttackBoxManager.Ins.IsPointerOverBox(eventData);

        if (isOverAttackBox)
        {
            if (AttackBoxManager.Ins.TryAddCard(this))
            {
                return;
            }
        }
        else if (isInAttackBox)
        {
            AttackBoxManager.Ins.ReturnCardToHand(this);
            return;
        }

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
        SetAssignedStars(0);

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

        RefreshCardTypeLabel();

        if (!isDragging && !isHovered)
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

        if (orderNumberImage != null)
        {
            if (orderSprite != null)
            {
                orderNumberImage.sprite = orderSprite;
                orderNumberImage.rectTransform.sizeDelta = orderNumberSize;
                orderNumberImage.rectTransform.anchoredPosition = Vector2.zero;
                orderNumberImage.rectTransform.localPosition = Vector3.zero;
                orderNumberImage.transform.SetAsLastSibling();
                orderNumberImage.gameObject.SetActive(true);
            }
            else
            {
                orderNumberImage.gameObject.SetActive(false);
            }
        }

        RefreshCardTypeLabel();
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
