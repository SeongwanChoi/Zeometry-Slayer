using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HandManager : Singleton<HandManager>
{
    [Header("손패 기본 설정")]
    [SerializeField] private RectTransform handContainer;   // 카드가 생성될 부모 RectTransform (화면 하단 중앙)
    [SerializeField] private Card cardPrefab;               // 카드 프리팹
    [SerializeField] private Sprite defaultCardSprite;      // 기본 카드 스프라이트 (card_neon_blue_blank)
    [SerializeField] private Vector2 cardSize = new Vector2(180f, 256f); // 카드 크기 (원본 520:740 비율 유지)

    [Header("HandManager 카드 스프라이트 (RED / BLUE / GREEN)")]
    [SerializeField] private Sprite redCardSprite;          // card_neon_red_blank
    [SerializeField] private Sprite blueCardSprite;         // card_neon_blue_blank
    [SerializeField] private Sprite greenCardSprite;        // card_neon_green_blank

    [Header("카드 장수 설정 (플레이어당 0 ~ 6장)")]
    [SerializeField] private int maxHandSize = 6;           // 플레이어 1명당 최대 손패 장수 (6장)
    [Range(0, 6)]
    [SerializeField] private int targetHandCount = 6;       // 현재 선택된 플레이어의 손패 장수 표시용

    [Header("슬레이 더 스파이어풍 부채꼴 배치 설정")]
    [SerializeField] private float cardSpacing = 125f;      // 카드가 늘어날 때 좌우로 밀리는 간격 (px)
    [SerializeField] private float angleStep = 5.5f;        // 카드 1장당 회전 각도 차이 (도)
    [SerializeField] private float arcDropPerStep = 14f;    // 중앙에서 멀어질수록 아래로 내려가는 곡선 깊이 (px)
    [SerializeField] private float baseYOffset = 20f;       // 손패 전체의 기본 높이 보정값 (px)

    [Header("테스트 단축키 설정")]
    [SerializeField] private bool enableTestHotkeys = false;

    // 각 플레이어(0, 1, 2)가 현재 손에 들고 있는 카드 색상 데이터 리스트
    private readonly Dictionary<int, List<ECardColor>> playerHandData = new Dictionary<int, List<ECardColor>>();
    private readonly Dictionary<int, SelectableUnit> playerUnitMap = new Dictionary<int, SelectableUnit>();

    // 화면 하단(PlayerHand)에서 재사용되는 실제 UI 카드 오브젝트 리스트 (최대 6개만 유지하고 스프라이트만 교체)
    private readonly List<Card> handCards = new List<Card>();
    private int currentSelectedPlayerIndex = 0;

    public int CurrentHandCount => handCards.Count;
    public int CurrentSelectedPlayerIndex => currentSelectedPlayerIndex;

    protected override void Awake()
    {
        base.Awake();

        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();

        EnsureSpritesFromPrefabIfNeeded();
    }

    private void Start()
    {
        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();

        EnsureSpritesFromPrefabIfNeeded();

        // 씬에 이미 자식 카드로 들어있는 경우 재사용 리스트에 등록
        for (int i = 0; i < handContainer.childCount; i++)
        {
            Card existingCard = handContainer.GetChild(i).GetComponent<Card>();
            if (existingCard != null && !existingCard.IsInAttackBox && !handCards.Contains(existingCard))
            {
                existingCard.SetHandCardSize(cardSize);
                handCards.Add(existingCard);
            }
        }

        // BattleManager 및 PauseMenuManager(ESC 메뉴)가 시작 시 반드시 활성화되도록 보장
        _ = BattleManager.Ins;
        _ = PauseMenuManager.Ins;
    }

    private void EnsureSpritesFromPrefabIfNeeded()
    {
        if (cardPrefab != null)
        {
            if (redCardSprite == null)   redCardSprite   = cardPrefab.GetBuiltInSprite(ECardColor.RED);
            if (blueCardSprite == null)  blueCardSprite  = cardPrefab.GetBuiltInSprite(ECardColor.BLUE);
            if (greenCardSprite == null) greenCardSprite = cardPrefab.GetBuiltInSprite(ECardColor.GREEN);
        }

        if (blueCardSprite == null && defaultCardSprite != null)
            blueCardSprite = defaultCardSprite;
        if (defaultCardSprite == null && blueCardSprite != null)
            defaultCardSprite = blueCardSprite;
    }

    private void Update()
    {
        if (!enableTestHotkeys || Keyboard.current == null) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            DrawCard();
        }
        else if (Keyboard.current.backspaceKey.wasPressedThisFrame)
        {
            RemoveLastCard();
        }
    }

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
    /// 매 플레이어 턴 시작 시 모든 플레이어(Player_1, Player_2, Player_3)의 손패 색상 데이터를 랜덤(RED/GREEN/BLUE)으로 6장씩 생성합니다.
    /// 오브젝트는 18개를 새로 만들지 않고, HandManager의 카드 오브젝트(최대 6개)와 스프라이트를 재사용하여 교체합니다.
    /// </summary>
    public void DealRandomHandsForAllPlayers(IReadOnlyList<SelectableUnit> players)
    {
        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();

        EnsureSpritesFromPrefabIfNeeded();

        playerHandData.Clear();
        playerUnitMap.Clear();

        int count = (players != null && players.Count > 0) ? players.Count : 3;
        int firstAliveIndex = 0;
        bool foundAlive = false;

        for (int pIdx = 0; pIdx < count; pIdx++)
        {
            SelectableUnit unit = (players != null && pIdx < players.Count) ? players[pIdx] : null;
            playerUnitMap[pIdx] = unit;

            bool isAlive = (unit == null || !unit.IsDead);
            if (isAlive && !foundAlive)
            {
                firstAliveIndex = pIdx;
                foundAlive = true;
            }

            List<ECardColor> colors = new List<ECardColor>(maxHandSize);
            if (isAlive)
            {
                for (int cIdx = 0; cIdx < maxHandSize; cIdx++)
                {
                    ECardColor randomColor = (ECardColor)Random.Range(0, 3); // 0: RED, 1: BLUE, 2: GREEN
                    colors.Add(randomColor);
                }
            }
            playerHandData[pIdx] = colors;
        }

        // 기본으로 첫 번째 생존 플레이어의 손패를 즉시 화면에 표시
        ShowPlayerHand(firstAliveIndex);
    }

    /// <summary>
    /// 특정 플레이어를 클릭/선택했을 때, 새 카드 오브젝트를 불필요하게 생성하지 않고
    /// 현재 HandManager의 카드 오브젝트들의 스프라이트(RED / BLUE / GREEN)와 소유자 정보만 해당 플레이어의 손패 데이터로 교체합니다.
    /// </summary>
    public void ShowPlayerHand(int playerIndex)
    {
        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();

        EnsureSpritesFromPrefabIfNeeded();

        currentSelectedPlayerIndex = playerIndex;

        if (!playerHandData.TryGetValue(playerIndex, out List<ECardColor> colors))
        {
            colors = new List<ECardColor>();
            playerHandData[playerIndex] = colors;
        }

        playerUnitMap.TryGetValue(playerIndex, out SelectableUnit ownerUnit);

        // 현재 선택된 플레이어의 남은 손패 장수만큼만 카드 오브젝트 개수 맞춤 (부족하면 생성, 남으면 제거)
        SyncCardObjectCount(colors.Count);

        // 각 카드 오브젝트에 HandManager의 카드 스프라이트(RED/BLUE/GREEN)를 그대로 입혀서 표시
        for (int i = 0; i < colors.Count; i++)
        {
            Card card = handCards[i];
            ECardColor color = colors[i];
            Sprite sprite = GetSpriteForColor(color);

            card.gameObject.SetActive(true);
            card.name = $"{color.GetCardName()}_Card_{i + 1}";
            card.SetHandCardSize(cardSize);
            card.SetupCard(playerIndex, ownerUnit, color, sprite, i);
        }

        targetHandCount = handCards.Count;
        UpdateHandLayout();
    }

    /// <summary>
    /// 공격 페이즈/적 턴 중에 손패 영역의 카드들을 숨기거나 다시 표시합니다.
    /// </summary>
    public void SetHandVisibility(bool visible)
    {
        for (int i = 0; i < handCards.Count; i++)
        {
            if (handCards[i] != null && !handCards[i].IsInAttackBox)
            {
                handCards[i].gameObject.SetActive(visible);
            }
        }
    }

    /// <summary>
    /// 카드를 Attack Box로 드래그해서 넣었을 때:
    /// 해당 플레이어의 손패 데이터에서 그 카드 색상을 분리하고, 남은 손패를 다시 정렬합니다.
    /// </summary>
    public void DetachCardFromHand(Card targetCard)
    {
        if (targetCard == null) return;

        int handIdx = handCards.IndexOf(targetCard);
        int ownerIdx = targetCard.OwnerPlayerIndex;

        if (handIdx >= 0)
        {
            targetCard.SavedHandIndex = handIdx;
            handCards.RemoveAt(handIdx);

            if (playerHandData.TryGetValue(ownerIdx, out List<ECardColor> colors))
            {
                if (handIdx < colors.Count)
                    colors.RemoveAt(handIdx);
                else
                    colors.Remove(targetCard.CardColor);
            }
        }

        targetHandCount = handCards.Count;
        UpdateHandLayout();
    }

    /// <summary>
    /// Attack Box에서 다시 빼서 허공에 놓은 카드를 자동으로 '그 카드를 가진 플레이어'의 원래 손패 위치로 복귀시킵니다.
    /// </summary>
    public void ReturnCardToHand(Card targetCard)
    {
        if (targetCard == null) return;

        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();

        int ownerIdx = targetCard.OwnerPlayerIndex;
        if (!playerHandData.TryGetValue(ownerIdx, out List<ECardColor> ownerColors))
        {
            ownerColors = new List<ECardColor>();
            playerHandData[ownerIdx] = ownerColors;
        }

        int insertIndex = Mathf.Clamp(targetCard.SavedHandIndex, 0, ownerColors.Count);
        ownerColors.Insert(insertIndex, targetCard.CardColor);

        // 만약 현재 보고 있는 플레이어가 이 카드의 원래 주인이라면 -> 카드 오브젝트를 그대로 손패의 원래 자리로 복귀
        if (ownerIdx == currentSelectedPlayerIndex)
        {
            targetCard.transform.SetParent(handContainer, false);
            targetCard.SetHandCardSize(cardSize);
            targetCard.gameObject.SetActive(true);

            int cardInsertIdx = Mathf.Clamp(insertIndex, 0, handCards.Count);
            if (!handCards.Contains(targetCard))
                handCards.Insert(cardInsertIdx, targetCard);

            // 각 카드의 손패 순서 인덱스 재동기화 및 부채꼴 배치
            playerUnitMap.TryGetValue(ownerIdx, out SelectableUnit ownerUnit);
            for (int i = 0; i < handCards.Count && i < ownerColors.Count; i++)
            {
                handCards[i].SetupCard(ownerIdx, ownerUnit, ownerColors[i], GetSpriteForColor(ownerColors[i]), i);
            }

            targetHandCount = handCards.Count;
            UpdateHandLayout();
        }
        else
        {
            // 현재 다른 플레이어를 보고 있는 상태에서 뺐다면 -> 주인 플레이어의 손패 데이터(ownerColors)의 원래 자리에 복구해 두고,
            // 빠져나온 임시 카드 오브젝트는 정리 (주인 플레이어를 클릭하면 ShowPlayerHand에서 그 자리에 그대로 표시됨!)
            Destroy(targetCard.gameObject);
        }
    }

    private void SyncCardObjectCount(int desiredCount)
    {
        desiredCount = Mathf.Clamp(desiredCount, 0, maxHandSize);

        // null이거나 AttackBox로 넘어간 카드 정리
        for (int i = handCards.Count - 1; i >= 0; i--)
        {
            if (handCards[i] == null || handCards[i].IsInAttackBox)
                handCards.RemoveAt(i);
        }

        while (handCards.Count < desiredCount)
        {
            Card newCard = CreateCardInstance(handCards.Count + 1);
            handCards.Add(newCard);
        }

        while (handCards.Count > desiredCount)
        {
            int lastIndex = handCards.Count - 1;
            Card lastCard = handCards[lastIndex];
            handCards.RemoveAt(lastIndex);
            if (lastCard != null)
                Destroy(lastCard.gameObject);
        }
    }

    [ContextMenu("카드 1장 뽑기 (Draw Card)")]
    public Card DrawCard()
    {
        if (handCards.Count >= maxHandSize) return null;

        ECardColor randomColor = (ECardColor)Random.Range(0, 3);
        Card newCard = CreateCardInstance(handCards.Count + 1);
        newCard.SetupCard(currentSelectedPlayerIndex, null, randomColor, GetSpriteForColor(randomColor), handCards.Count);
        handCards.Add(newCard);
        targetHandCount = handCards.Count;

        UpdateHandLayout();
        return newCard;
    }

    [ContextMenu("카드 1장 버리기 (Remove Card)")]
    public void RemoveLastCard()
    {
        if (handCards.Count == 0) return;
        RemoveCard(handCards[handCards.Count - 1]);
    }

    public void RemoveCard(Card targetCard)
    {
        if (targetCard == null) return;

        handCards.Remove(targetCard);
        Destroy(targetCard.gameObject);
        targetHandCount = handCards.Count;

        UpdateHandLayout();
    }

    [ContextMenu("손패 모두 비우기 (Clear Hand)")]
    public void ClearHand()
    {
        SyncCardObjectCount(0);
        targetHandCount = 0;
    }

    public void SyncHandCountWithTarget(int targetCount)
    {
        SyncCardObjectCount(targetCount);
        for (int i = 0; i < handCards.Count; i++)
        {
            ECardColor randomColor = (ECardColor)Random.Range(0, 3);
            handCards[i].SetupCard(0, null, randomColor, GetSpriteForColor(randomColor), i);
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

        float centerIndex = (count - 1) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            Card card = handCards[i];
            if (card == null) continue;

            float offsetFromCenter = i - centerIndex;
            float posX = offsetFromCenter * cardSpacing;
            float posY = baseYOffset - (offsetFromCenter * offsetFromCenter * arcDropPerStep);
            float rotZ = -offsetFromCenter * angleStep;

            Vector2 targetPos = new Vector2(posX, posY);
            Quaternion targetRot = Quaternion.Euler(0f, 0f, rotZ);

            card.SetHandCardSize(cardSize);
            card.SetCardTransform(targetPos, targetRot, i);
        }
    }

    public Sprite GetSpriteForColor(ECardColor color)
    {
        EnsureSpritesFromPrefabIfNeeded();

        switch (color)
        {
            case ECardColor.RED:
                return redCardSprite != null ? redCardSprite : defaultCardSprite;
            case ECardColor.BLUE:
                return blueCardSprite != null ? blueCardSprite : defaultCardSprite;
            case ECardColor.GREEN:
                return greenCardSprite != null ? greenCardSprite : defaultCardSprite;
            default:
                return defaultCardSprite;
        }
    }

    private Card CreateCardInstance(int cardNumber)
    {
        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();

        Card instance;
        if (cardPrefab != null)
        {
            // 중요: 세 번째 인자 worldPositionStays를 반드시 false로 주어야
            // Screen Space - Camera 모드에서 Scale이 108배로 커지거나 Pos Z가 -9720으로 날아가지 않습니다!
            instance = Instantiate(cardPrefab, handContainer, false);
        }
        else
        {
            GameObject cardObj = new GameObject($"Card_Slot_{cardNumber}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(Card));
            cardObj.layer = LayerMask.NameToLayer("UI");
            cardObj.transform.SetParent(handContainer, false);

            Image img = cardObj.GetComponent<Image>();
            img.sprite = defaultCardSprite;
            img.raycastTarget = true;

            instance = cardObj.GetComponent<Card>();
        }

        instance.gameObject.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.localPosition = Vector3.zero;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = cardSize;

        instance.SetHandCardSize(cardSize);
        return instance;
    }
}
