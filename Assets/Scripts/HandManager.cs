using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HandManager : Singleton<HandManager>
{
    [Header("손패 기본 설정")]
    [SerializeField] private RectTransform handContainer;   // 카드가 생성될 부모 RectTransform (화면 하단 중앙)
    [SerializeField] private Card cardPrefab;               // 카드 프리팹 (없으면 cardSprite로 자동 생성)
    [SerializeField] private Sprite defaultCardSprite;      // 기본 카드 스프라이트 (card_neon_blue_blank)
    [SerializeField] private Vector2 cardSize = new Vector2(180f, 256f); // 카드 크기 (원본 520:740 비율 유지)

    [Header("카드 장수 설정 (0 ~ 6장)")]
    [SerializeField] private int maxHandSize = 6;           // 최대 손패 장수 (6장)
    [Range(0, 6)]
    [SerializeField] private int targetHandCount = 6;       // 현재 목표 손패 장수 (0장~6장, 인스펙터 슬라이더로 실시간 테스트 가능)

    [Header("슬레이 더 스파이어풍 부채꼴 배치 설정")]
    [SerializeField] private float cardSpacing = 125f;      // 카드가 늘어날 때 좌우로 밀리는 간격 (px)
    [SerializeField] private float angleStep = 5.5f;        // 카드 1장당 회전 각도 차이 (도)
    [SerializeField] private float arcDropPerStep = 14f;    // 중앙에서 멀어질수록 아래로 내려가는 곡선 깊이 (px)
    [SerializeField] private float baseYOffset = 20f;       // 손패 전체의 기본 높이 보정값 (px)

    [Header("테스트 단축키 설정")]
    [SerializeField] private bool enableTestHotkeys = true; // Space: 카드 드로우(+1), Backspace: 카드 제거(-1)

    private readonly List<Card> handCards = new List<Card>();
    public int CurrentHandCount => handCards.Count;

    private void Start()
    {
        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();

        // 씬에 이미 자식 카드로 들어있는 경우 리스트에 등록
        handCards.Clear();
        for (int i = 0; i < handContainer.childCount; i++)
        {
            Card existingCard = handContainer.GetChild(i).GetComponent<Card>();
            if (existingCard != null && handCards.Count < maxHandSize)
            {
                existingCard.SetHandCardSize(cardSize);
                handCards.Add(existingCard);
            }
        }

        // 초기 설정된 장수(targetHandCount)에 맞춰 카드 생성 또는 제거
        SyncHandCountWithTarget(targetHandCount);
    }

    private void Update()
    {
        // 인스펙터에서 targetHandCount(0~6장)를 변경했을 때 실시간으로 카드가 추가/제거되며 부채꼴 정렬되도록 동기화
        if (handCards.Count != targetHandCount)
        {
            SyncHandCountWithTarget(targetHandCount);
        }

        if (!enableTestHotkeys || Keyboard.current == null) return;

        // Space 키를 누르면 카드 1장 드로우 (최대 6장)
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            DrawCard();
        }
        // Backspace 키를 누르면 카드 1장 제거 (최소 0장)
        else if (Keyboard.current.backspaceKey.wasPressedThisFrame)
        {
            RemoveLastCard();
        }
    }

    // 인스펙터에서 간격, 각도, 곡선 값을 조절할 때 실시간으로 부채꼴 모양 확인 가능
    private void OnValidate()
    {
        maxHandSize = Mathf.Max(1, maxHandSize);
        targetHandCount = Mathf.Clamp(targetHandCount, 0, maxHandSize);

        if (Application.isPlaying && handContainer != null)
        {
            UpdateHandLayout();
        }
    }

    /// <summary>
    /// 손패에 카드를 1장 추가(드로우)합니다. (최대 6장)
    /// 카드가 들어올 때마다 기존 카드들이 좌우로 밀리며 부채꼴 형태를 잡습니다.
    /// </summary>
    [ContextMenu("카드 1장 뽑기 (Draw Card)")]
    public Card DrawCard()
    {
        if (handCards.Count >= maxHandSize)
        {
            Debug.Log($"[HandManager] 손패가 가득 찼습니다! (최대 {maxHandSize}장)");
            return null;
        }

        Card newCard = CreateCardInstance(handCards.Count + 1);
        handCards.Add(newCard);
        targetHandCount = handCards.Count;

        UpdateHandLayout();
        return newCard;
    }

    /// <summary>
    /// 손패에서 마지막 카드를 1장 제거합니다. (최소 0장)
    /// </summary>
    [ContextMenu("카드 1장 버리기 (Remove Card)")]
    public void RemoveLastCard()
    {
        if (handCards.Count == 0)
        {
            Debug.Log("[HandManager] 손패가 이미 0장입니다.");
            return;
        }

        int lastIndex = handCards.Count - 1;
        RemoveCard(handCards[lastIndex]);
    }

    /// <summary>
    /// 특정 카드를 손패에서 완전히 삭제하고 남은 카드를 다시 부채꼴로 정렬합니다.
    /// </summary>
    public void RemoveCard(Card targetCard)
    {
        if (targetCard == null || !handCards.Contains(targetCard)) return;

        handCards.Remove(targetCard);
        Destroy(targetCard.gameObject);
        targetHandCount = handCards.Count;

        UpdateHandLayout();
    }

    /// <summary>
    /// 카드를 파괴하지 않고 Attack Box 등으로 이동시킬 때 손패 목록에서 분리합니다.
    /// </summary>
    public void DetachCardFromHand(Card targetCard)
    {
        if (targetCard == null) return;

        int index = handCards.IndexOf(targetCard);
        if (index >= 0)
        {
            targetCard.SavedHandIndex = index; // 나중에 다시 빼낼 때 돌아올 원래 손패 순서 기억
            handCards.RemoveAt(index);
            targetHandCount = handCards.Count;
            UpdateHandLayout();
        }
    }

    /// <summary>
    /// Attack Box에서 다시 빼낸 카드를 손패의 원래 자리로 복귀시킵니다.
    /// </summary>
    public void ReturnCardToHand(Card targetCard)
    {
        if (targetCard == null) return;
        if (handCards.Contains(targetCard)) return;

        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();

        targetCard.transform.SetParent(handContainer, false);
        targetCard.SetHandCardSize(cardSize);

        // 기억해 둔 원래 자리(SavedHandIndex)로 복귀
        int insertIndex = targetCard.SavedHandIndex;
        if (insertIndex < 0 || insertIndex > handCards.Count)
            insertIndex = handCards.Count;

        handCards.Insert(insertIndex, targetCard);
        targetHandCount = handCards.Count;

        UpdateHandLayout();
    }

    /// <summary>
    /// 손패의 모든 카드를 비웁니다 (0장 상태).
    /// </summary>
    [ContextMenu("손패 모두 비우기 (Clear Hand)")]
    public void ClearHand()
    {
        for (int i = handCards.Count - 1; i >= 0; i--)
        {
            if (handCards[i] != null)
                Destroy(handCards[i].gameObject);
        }

        handCards.Clear();
        targetHandCount = 0;
        UpdateHandLayout();
    }

    /// <summary>
    /// 목표 장수(0~6장)에 맞게 손패를 맞춥니다.
    /// </summary>
    public void SyncHandCountWithTarget(int targetCount)
    {
        targetCount = Mathf.Clamp(targetCount, 0, maxHandSize);

        while (handCards.Count < targetCount)
        {
            Card newCard = CreateCardInstance(handCards.Count + 1);
            handCards.Add(newCard);
        }

        while (handCards.Count > targetCount)
        {
            int lastIndex = handCards.Count - 1;
            Card lastCard = handCards[lastIndex];
            handCards.RemoveAt(lastIndex);
            if (lastCard != null)
                Destroy(lastCard.gameObject);
        }

        targetHandCount = handCards.Count;
        UpdateHandLayout();
    }

    /// <summary>
    /// 현재 손패 개수(0~6장)에 맞춰 카드들을 중앙 기준으로 좌우로 밀어내며 부채꼴(아치형)로 배치합니다.
    /// </summary>
    public void UpdateHandLayout()
    {
        int count = handCards.Count;
        if (count == 0) return;

        // 중앙 인덱스 계산 (예: 1장->0, 2장->0.5, 3장->1, 4장->1.5, 5장->2, 6장->2.5)
        // 카드가 추가될 때마다 centerIndex가 0.5씩 커지면서 기존 카드들이 좌우 대칭으로 밀려납니다.
        float centerIndex = (count - 1) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            Card card = handCards[i];
            if (card == null) continue;

            // 중앙으로부터 떨어진 상대적 순서 (음수: 왼쪽, 0: 정중앙, 양수: 오른쪽)
            float offsetFromCenter = i - centerIndex;

            // 1. 좌우(X) 위치: 중앙(0)을 기준으로 좌우 대칭으로 밀려남
            float posX = offsetFromCenter * cardSpacing;

            // 2. 상하(Y) 위치: 포물선(이차함수) 공식을 적용해 양 끝으로 갈수록 자연스럽게 아래로 내려감 (부채꼴 아치)
            float posY = baseYOffset - (offsetFromCenter * offsetFromCenter * arcDropPerStep);

            // 3. 회전(Z) 각도: 왼쪽 카드는 왼쪽(+Z)으로, 오른쪽 카드는 오른쪽(-Z)으로 기울어짐
            float rotZ = -offsetFromCenter * angleStep;

            Vector2 targetPos = new Vector2(posX, posY);
            Quaternion targetRot = Quaternion.Euler(0f, 0f, rotZ);

            card.SetHandCardSize(cardSize);
            card.SetCardTransform(targetPos, targetRot, i);
        }
    }

    private Card CreateCardInstance(int cardNumber)
    {
        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();

        if (cardPrefab != null)
        {
            Card instance = Instantiate(cardPrefab, handContainer);
            instance.name = $"Card_Neon_Blue_{cardNumber}";
            instance.SetHandCardSize(cardSize);
            return instance;
        }

        // 프리팹이 할당되지 않은 경우에도 defaultCardSprite(card_neon_blue_blank)로 즉시 생성
        GameObject cardObj = new GameObject($"Card_Neon_Blue_{cardNumber}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(Card));
        cardObj.transform.SetParent(handContainer, false);

        RectTransform rect = cardObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = cardSize;

        Image img = cardObj.GetComponent<Image>();
        img.sprite = defaultCardSprite;
        img.raycastTarget = true;

        Card cardComp = cardObj.GetComponent<Card>();
        cardComp.SetHandCardSize(cardSize);
        return cardComp;
    }
}
