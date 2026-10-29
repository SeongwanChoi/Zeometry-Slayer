using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Card : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("호버(마우스 오버) 설정")]
    [SerializeField] private float hoverScale = 1.15f;   // 마우스 올렸을 때 커지는 배율
    [SerializeField] private float hoverYOffset = 40f;   // 마우스 올렸을 때 위로 올라가는 높이

    [Header("AttackBox 진입 시 색상 및 순서 표시")]
    [SerializeField] private Color normalCardColor = Color.white; // 손패에 있을 때 원래 색상 (255, 255, 255)
    [SerializeField] private Color attackBoxDarkColor = new Color32(156, 152, 152, 255); // AttackBox 안에서 어두워지는 색상 (156, 152, 152)
    [SerializeField] private Image orderNumberImage;     // 카드 위에 띄울 금색 순서 숫자 이미지 컴포넌트
    [SerializeField] private Vector2 orderNumberSize = new Vector2(68f, 68f); // 금색 숫자 이미지 크기

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

    public bool IsInAttackBox => isInAttackBox;
    public int SavedHandIndex { get => savedHandIndex; set => savedHandIndex = value; }

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
            orderNumberImage.raycastTarget = false; // 숫자 이미지가 마우스 드래그/호버를 가로채지 않도록 설정
            orderNumberImage.preserveAspect = true;
            numObj.SetActive(false);
        }
    }

    private void Start()
    {
        InitComponents();

        // HandManager 등에서 아직 위치를 지정받지 않았다면 현재 Inspector 좌표를 기억해 둠
        if (!isInitialized)
        {
            originPosition = rectTransform.anchoredPosition;
            originRotation = rectTransform.localRotation;
            originSiblingIndex = transform.GetSiblingIndex();
            if (rectTransform.sizeDelta.sqrMagnitude > 0f)
                handCardSize = rectTransform.sizeDelta;
        }
    }

    // 1. 마우스를 카드 위에 올렸을 때 (Hover 시작)
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 드래그 중일 때는 호버 효과가 중복 실행되지 않게 막음
        if (isDragging) return;

        isHovered = true;
        originSiblingIndex = transform.GetSiblingIndex();
        transform.SetAsLastSibling(); // 호버된 카드가 옆 카드에 가려지지 않게 맨 위로 표시

        if (isInAttackBox)
        {
            // Attack Box 안에 있을 때는 박스 밖으로 튀어나가지 않도록 살짝만 확대
            transform.localScale = Vector3.one * 1.08f;
            rectTransform.anchoredPosition = originPosition + new Vector2(0f, 10f);
        }
        else
        {
            transform.localScale = Vector3.one * hoverScale;
            rectTransform.localRotation = Quaternion.identity; // 호버 시 카드를 똑바로 세움
            rectTransform.anchoredPosition = originPosition + new Vector2(0f, hoverYOffset);
        }
    }

    // 2. 마우스가 카드 밖으로 나갔을 때 (Hover 끝)
    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDragging) return;

        isHovered = false;
        transform.localScale = Vector3.one;

        // 원래 위치, 회전, 겹침 순서로 정확히 복구
        rectTransform.anchoredPosition = originPosition;
        rectTransform.localRotation = originRotation;
        transform.SetSiblingIndex(originSiblingIndex);
    }

    // 3. 카드를 클릭해서 끌기 시작할 때
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isHovered)
            originSiblingIndex = transform.GetSiblingIndex();

        isDragging = true;
        isHovered = false;

        transform.localScale = Vector3.one * (isInAttackBox ? 1.08f : hoverScale);
        rectTransform.localRotation = Quaternion.identity;
        canvasGroup.blocksRaycasts = false; // 내 밑에 있는 Attack Box가 마우스를 감지할 수 있게 통과시킴
        transform.SetAsLastSibling();       // 다른 카드들보다 맨 위에 그려지게 함
    }

    // 4. 마우스를 끌고 이동하는 중
    public void OnDrag(PointerEventData eventData)
    {
        // 마우스 화면 좌표를 그대로 따라감
        transform.position = eventData.position;

        // OnDrag 상태일 때 마우스 포인터가 Attack Box 위에 올라가 있다면
        // Attack Box 안에 들어갈 수 있는 크기로 실시간 축소하여 시각적으로 보여줌
        if (AttackBoxManager.Ins != null && AttackBoxManager.Ins.IsPointerOverBox(eventData))
        {
            transform.localScale = Vector3.one;
        }
    }

    // 5. 마우스 버튼을 놓았을 때 (드롭)
    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        canvasGroup.blocksRaycasts = true;
        transform.localScale = Vector3.one;

        bool isOverAttackBox = AttackBoxManager.Ins != null && AttackBoxManager.Ins.IsPointerOverBox(eventData);

        if (isOverAttackBox)
        {
            // 마우스 포인터가 Attack Box 위에 올라가 있는 상태로 놓았다면 Attack Box 왼쪽부터 차례대로 쌓음
            if (AttackBoxManager.Ins.TryAddCard(this))
            {
                return;
            }
        }
        else if (isInAttackBox)
        {
            // Attack Box 안에 있던 카드를 밖(허공)으로 빼내어 놓았을 경우 -> 손패의 원래 자리로 복귀
            AttackBoxManager.Ins.ReturnCardToHand(this);
            return;
        }

        // 그 외 허공에 놓았거나 박스가 가득 찬 경우 원래 자리로 돌아감
        rectTransform.anchoredPosition = originPosition;
        rectTransform.localRotation = originRotation;
        transform.SetSiblingIndex(originSiblingIndex);
    }

    // 나중에 슬롯 스크립트에서 이 카드를 새 슬롯 위치에 고정시킬 때 부를 함수
    public void SetNewOriginPosition(Vector2 newAnchoredPos)
    {
        SetCardTransform(newAnchoredPos, Quaternion.identity, transform.GetSiblingIndex());
    }

    // 손패(HandManager)에서 부채꼴 정렬 위치, 회전각, 정렬 순서를 갱신할 때 부르는 함수
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

        // 손패로 돌아왔을 때는 원래 밝기(255, 255, 255)로 복구하고 금색 숫자 이미지를 숨김
        if (cardImage != null)
            cardImage.color = normalCardColor;

        if (orderNumberImage != null)
            orderNumberImage.gameObject.SetActive(false);

        if (!isDragging && !isHovered)
        {
            rectTransform.anchoredPosition = originPosition;
            rectTransform.localRotation = originRotation;
            transform.localScale = Vector3.one;
            transform.SetSiblingIndex(originSiblingIndex);
        }
    }

    // 손패에서의 원래 카드 크기를 설정하는 함수
    public void SetHandCardSize(Vector2 size)
    {
        InitComponents();
        handCardSize = size;
        if (!isInAttackBox)
            rectTransform.sizeDelta = handCardSize;
    }

    // Attack Box 안에 들어갈 때 박스 크기에 맞게 축소, 색상 어둡게(156, 152, 152), 금색 순서 숫자를 띄우는 함수
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
        rectTransform.sizeDelta = boxCardSize; // Attack Box 안에 들어가는 사이즈로 변경

        // 카드 오브젝트 색상을 (156, 152, 152)로 어두워지게 변경
        if (cardImage != null)
            cardImage.color = attackBoxDarkColor;

        // 카드 중앙 위에 금색 순서 숫자 이미지 표시
        if (orderNumberImage != null)
        {
            if (orderSprite != null)
            {
                orderNumberImage.sprite = orderSprite;
                orderNumberImage.rectTransform.sizeDelta = orderNumberSize;
                orderNumberImage.rectTransform.anchoredPosition = Vector2.zero;
                orderNumberImage.transform.SetAsLastSibling();
                orderNumberImage.gameObject.SetActive(true);
            }
            else
            {
                orderNumberImage.gameObject.SetActive(false);
            }
        }

        originPosition = boxAnchoredPos;
        originRotation = Quaternion.identity;
        originSiblingIndex = siblingIndex;

        if (!isDragging && !isHovered)
        {
            rectTransform.anchoredPosition = originPosition;
            rectTransform.localRotation = originRotation;
            transform.localScale = Vector3.one;
            transform.SetSiblingIndex(originSiblingIndex);
        }
    }
}
