using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AttackBoxManager : Singleton<AttackBoxManager>
{
    [Header("Attack Box 기본 설정")]
    [SerializeField] private RectTransform boxRectTransform;        // Attack Box의 RectTransform
    [SerializeField] private int requiredCardCount = 6;             // 최대 수용 장수이자 행동 실행에 반드시 필요한 카드 장수 (6장)

    [Header("박스 내부 카드 크기 및 왼쪽 정렬 설정")]
    [SerializeField] private Vector2 boxCardSize = new Vector2(105f, 150f); // Attack Box 안에 들어갈 때 작아지는 카드 크기
    [SerializeField] private float leftPadding = 25f;               // 왼쪽 테두리에서 첫 번째 카드까지의 여백 (px)
    [SerializeField] private float cardSpacing = 120f;              // 왼쪽에서부터 차례대로 쌓일 때 카드 간 간격 (px)
    [SerializeField] private float cardYOffset = 0f;                // 박스 중앙 기준 상하 보정값

    [Header("카드 순서 표시용 금색 숫자 이미지 (1 ~ 6)")]
    [SerializeField] private Sprite[] orderNumberSprites = new Sprite[6]; // 1번~6번 금색 숫자 스프라이트 배열

    [Header("시각 피드백 (6장 충족 여부)")]
    [SerializeField] private Image boxFrameImage;                   // Attack Box 테두리 이미지
    [SerializeField] private Color normalFrameColor = Color.white;  // 기본 상태 색상 (0~5장)
    [SerializeField] private Color readyFrameColor = new Color(0.4f, 1f, 1f, 1f); // 6장 모두 찼을 때 강조 색상

    private readonly List<Card> queuedCards = new List<Card>();

    public int CurrentCardCount => queuedCards.Count;
    public int RequiredCardCount => requiredCardCount;

    /// <summary>
    /// 이번 차례 공격 행동이 가능한지 여부 (반드시 6장이 들어와야 true)
    /// </summary>
    public bool IsReadyToExecute => queuedCards.Count == requiredCardCount;

    public IReadOnlyList<Card> QueuedCards => queuedCards;

    private void Start()
    {
        if (boxRectTransform == null)
            boxRectTransform = GetComponent<RectTransform>();

        if (boxFrameImage == null)
            boxFrameImage = GetComponent<Image>();

        EnsureRuntimeOrderNumberSprites();
        UpdateBoxLayout();
    }

    /// <summary>
    /// 마우스 포인터(eventData)가 현재 Attack Box 영역 위에 올라가 있는지 판정합니다.
    /// </summary>
    public bool IsPointerOverBox(PointerEventData eventData)
    {
        if (boxRectTransform == null)
            boxRectTransform = GetComponent<RectTransform>();

        if (boxRectTransform == null || eventData == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            boxRectTransform,
            eventData.position,
            eventData.pressEventCamera
        );
    }

    /// <summary>
    /// 카드를 Attack Box에 넣을 수 있는지 확인하고, 가능하면 왼쪽부터 차례대로 쌓습니다.
    /// </summary>
    public bool TryAddCard(Card card)
    {
        if (card == null) return false;

        // 이미 Attack Box 안에 들어있던 카드를 다시 박스 안에 놓은 경우 -> 그대로 유지하고 재정렬
        if (queuedCards.Contains(card))
        {
            UpdateBoxLayout();
            return true;
        }

        // 최대 6장 제한 체크
        if (queuedCards.Count >= requiredCardCount)
        {
            Debug.Log($"[AttackBox] 박스가 가득 찼습니다! (최대 {requiredCardCount}장)");
            return false;
        }

        // 손패(HandManager) 목록에서 분리
        if (HandManager.Ins != null)
        {
            HandManager.Ins.DetachCardFromHand(card);
        }

        queuedCards.Add(card);
        UpdateBoxLayout();

        if (IsReadyToExecute)
        {
            Debug.Log($"[AttackBox] 6장의 카드가 모두 장전되었습니다! ({queuedCards.Count}/{requiredCardCount})");
        }

        return true;
    }

    /// <summary>
    /// Attack Box 안에 있던 카드를 빼내어 손패의 원래 자리로 돌려보냅니다.
    /// </summary>
    public void ReturnCardToHand(Card card)
    {
        if (card == null) return;

        if (queuedCards.Contains(card))
        {
            queuedCards.Remove(card);
            UpdateBoxLayout(); // 빠져나간 빈자리를 메우도록 남은 카드들을 왼쪽으로 다시 정렬하고 번호(1~N)도 갱신
        }

        if (HandManager.Ins != null)
        {
            HandManager.Ins.ReturnCardToHand(card);
        }
    }

    /// <summary>
    /// Attack Box 안의 모든 카드를 왼쪽에서부터 차례대로 작아진 크기로 정렬하고,
    /// 카드 색상을 (156, 152, 152)로 어둡게 만든 뒤 그 위에 순서(1~6) 금색 숫자 이미지를 띄웁니다.
    /// </summary>
    public void UpdateBoxLayout()
    {
        if (boxRectTransform == null)
            boxRectTransform = GetComponent<RectTransform>();

        EnsureRuntimeOrderNumberSprites();

        float boxWidth = boxRectTransform.rect.width;
        if (boxWidth <= 0f)
            boxWidth = boxRectTransform.sizeDelta.x;

        // 박스 중심(0,0) 기준 가장 왼쪽 끝 X 좌표 계산
        float leftEdgeX = -boxWidth * 0.5f;
        float firstCardCenterX = leftEdgeX + leftPadding + (boxCardSize.x * 0.5f);

        for (int i = 0; i < queuedCards.Count; i++)
        {
            Card card = queuedCards[i];
            if (card == null) continue;

            // 왼쪽(0번)부터 오른쪽으로 차례대로 쌓이는 좌표
            float posX = firstCardCenterX + (i * cardSpacing);
            Vector2 targetPos = new Vector2(posX, cardYOffset);

            // 순서에 맞는 금색 숫자 스프라이트 (i=0 -> 1번 이미지, i=1 -> 2번 이미지 ...)
            Sprite orderSprite = GetOrderNumberSprite(i);

            card.SetAttackBoxTransform(boxRectTransform, targetPos, boxCardSize, i, orderSprite);
        }

        if (boxFrameImage != null)
        {
            boxFrameImage.color = IsReadyToExecute ? readyFrameColor : normalFrameColor;
        }
    }

    private Sprite GetOrderNumberSprite(int zeroBasedIndex)
    {
        if (orderNumberSprites != null && zeroBasedIndex >= 0 && zeroBasedIndex < orderNumberSprites.Length)
        {
            return orderNumberSprites[zeroBasedIndex];
        }
        return null;
    }

    /// <summary>
    /// 만약 인스펙터에 금색 숫자 스프라이트가 비어있더라도 런타임에 즉시 생성하여 표시되도록 보장합니다.
    /// </summary>
    private void EnsureRuntimeOrderNumberSprites()
    {
        if (orderNumberSprites == null || orderNumberSprites.Length < requiredCardCount)
            orderNumberSprites = new Sprite[requiredCardCount];

        for (int i = 0; i < requiredCardCount; i++)
        {
            if (orderNumberSprites[i] == null)
            {
                Texture2D tex = GoldNumberTextureGenerator.CreateGoldNumberTexture(i + 1, 128, 128);
                orderNumberSprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                orderNumberSprites[i].name = $"order_num_gold_{i + 1}";
            }
        }
    }

    private void OnValidate()
    {
        requiredCardCount = Mathf.Max(1, requiredCardCount);
        if (Application.isPlaying && boxRectTransform != null)
        {
            UpdateBoxLayout();
        }
    }
}
