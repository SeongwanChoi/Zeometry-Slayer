using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[System.Serializable]
public struct DeckCardEntry
{
    public int ownerPlayerIndex;
    public SelectableUnit ownerUnit;
    public ECardColor cardColor;

    public DeckCardEntry(int playerIdx, SelectableUnit unit, ECardColor color)
    {
        ownerPlayerIndex = playerIdx;
        ownerUnit = unit;
        cardColor = color;
    }
}

public class HandManager : Singleton<HandManager>
{
    [Header("손패 기본 설정")]
    [SerializeField] private RectTransform handContainer;
    [SerializeField] private Card cardPrefab;
    [SerializeField] private Sprite defaultCardSprite;
    [SerializeField] private Vector2 cardSize = new Vector2(180f, 256f);

    [Header("HandManager 카드 스프라이트 (RED / BLUE / GREEN)")]
    [SerializeField] private Sprite redCardSprite;
    [SerializeField] private Sprite blueCardSprite;
    [SerializeField] private Sprite greenCardSprite;

    [Header("카드 장수 설정 (턴 시작 5장 드로우, 손패 최대 10장)")]
    [SerializeField] private int turnStartDrawCount = 5;
    [SerializeField] private int maxHandSize = 10;
    [Range(0, 10)]
    [SerializeField] private int targetHandCount = 5;

    [Header("완만한 부채꼴 배치 설정 (최대 10장 대응)")]
    [SerializeField] private float cardSpacing = 122f;
    [SerializeField] private float angleStep = 4.0f;
    [SerializeField] private float arcDropPerStep = 9f;
    [SerializeField] private float baseYOffset = 22f;

    [Header("덱(Draw Pile) / 버린 카드(Discard Pile) / 턴 종료 버튼 UI 참조")]
    [SerializeField] private RectTransform drawPileRoot;
    [SerializeField] private Text drawPileCountText;
    [SerializeField] private RectTransform discardPileRoot;
    [SerializeField] private Text discardPileCountText;
    [SerializeField] private RectTransform turnEndButtonRoot;
    [SerializeField] private Button turnEndButton;

    [Header("테스트 단축키 설정")]
    [SerializeField] private bool enableTestHotkeys = false;

    // 통합 덱(Draw Pile) 및 버린 카드 더미(Discard Pile)
    private readonly List<DeckCardEntry> drawPile = new List<DeckCardEntry>();
    private readonly List<DeckCardEntry> discardPile = new List<DeckCardEntry>();
    private readonly Dictionary<int, SelectableUnit> playerUnitMap = new Dictionary<int, SelectableUnit>();

    // 현재 손에 들고 있는 카드 리스트 (최대 10장)
    private readonly List<Card> handCards = new List<Card>();
    private int currentSelectedPlayerIndex = 0;
    private bool isDeckInitialized = false;
    private bool isZeroCostQuickBusterActive = false;
    private bool isSuperBusterActiveThisTurn = false;

    private Canvas cachedCanvas;
    private RectTransform cachedCanvasRect;

    private static Sprite cachedDeckPileSprite;
    private static Sprite cachedDiscardPileSprite;
    private static Sprite cachedCountBadgeSprite;
    private static Sprite cachedTurnEndBtnSprite;

    public int CurrentHandCount => handCards.Count;
    public int DrawPileCount => drawPile.Count;
    public int DiscardPileCount => discardPile.Count;
    public int CurrentSelectedPlayerIndex => currentSelectedPlayerIndex;
    public bool IsZeroCostQuickBusterActive => isZeroCostQuickBusterActive;
    public IReadOnlyList<Card> HandCards => handCards;
    public RectTransform DrawPileRect => drawPileRoot;
    public RectTransform DiscardPileRect => discardPileRoot;
    public RectTransform TurnEndButtonRect => turnEndButtonRoot;

    protected override void Awake()
    {
        base.Awake();
        maxHandSize = 10;

        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();

        EnsureSpritesFromPrefabIfNeeded();
        EnsureDeckAndDiscardUI();
    }

    private void Start()
    {
        maxHandSize = 10;
        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();

        // 10장까지 담을 수 있도록 PlayerHand 영역 폭 확보
        if (handContainer != null && handContainer.sizeDelta.x < 1050f)
        {
            handContainer.sizeDelta = new Vector2(1080f, 300f);
        }

        EnsureSpritesFromPrefabIfNeeded();
        EnsureDeckAndDiscardUI();

        // 씬에 미리 배치되어 있던 정적 자식 카드는 시작 시 정리하여 덱 드로우 시스템과 충돌 방지
        for (int i = handContainer.childCount - 1; i >= 0; i--)
        {
            Card existingCard = handContainer.GetChild(i).GetComponent<Card>();
            if (existingCard != null && !handCards.Contains(existingCard))
            {
                Destroy(existingCard.gameObject);
            }
        }

        _ = BattleManager.Ins;
        _ = PauseMenuManager.Ins;
        _ = SkillConfirmModalUI.Ins;
        _ = SplineTargetingArrow.Ins;
    }

    private void EnsureSpritesFromPrefabIfNeeded()
    {
        if (cardPrefab != null)
        {
            if (redCardSprite == null)   redCardSprite   = cardPrefab.GetBuiltInSprite(ECardColor.RED);
            if (blueCardSprite == null)  blueCardSprite  = cardPrefab.GetBuiltInSprite(ECardColor.BLUE);
            if (greenCardSprite == null) greenCardSprite = cardPrefab.GetBuiltInSprite(ECardColor.GREEN);
        }

        if (redCardSprite == null)
            redCardSprite = UIHelper.LoadSpriteFromAssetOrDisk("Assets/Resource/UI/card_neon_red_blank.png");
        if (blueCardSprite == null)
            blueCardSprite = UIHelper.LoadSpriteFromAssetOrDisk("Assets/Resource/UI/card_neon_blue_blank.png");
        if (greenCardSprite == null)
            greenCardSprite = UIHelper.LoadSpriteFromAssetOrDisk("Assets/Resource/UI/card_neon_green_blank.png");

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
            DrawCards(1);
        }
        else if (Keyboard.current.backspaceKey.wasPressedThisFrame)
        {
            RemoveLastCard();
        }
    }

    private void OnValidate()
    {
        maxHandSize = Mathf.Max(10, maxHandSize);
        targetHandCount = Mathf.Clamp(targetHandCount, 0, maxHandSize);

        if (Application.isPlaying && handContainer != null)
        {
            UpdateHandLayout();
        }
    }

    // =========================================================================
    // 1. 통합 덱(5~15장) 초기화 및 턴 시작 5장 드로우 (PDF 1~2페이지 명세)
    // =========================================================================
    public void InitializeSharedDeck(IReadOnlyList<SelectableUnit> players)
    {
        drawPile.Clear();
        discardPile.Clear();
        playerUnitMap.Clear();

        if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                SelectableUnit unit = players[i];
                if (unit == null) continue;

                playerUnitMap[i] = unit;
                if (unit.IsDead) continue;

                List<ECardColor> pattern = unit.GetOwnedCardPattern();
                for (int c = 0; c < pattern.Count; c++)
                {
                    drawPile.Add(new DeckCardEntry(i, unit, pattern[c]));
                }
            }
        }

        ShuffleDeckList(drawPile);
        isDeckInitialized = true;
        RefreshPileCountUI();
    }

    private void PurgeDeadPlayerCardsFromPiles()
    {
        drawPile.RemoveAll(entry => entry.ownerUnit != null && entry.ownerUnit.IsDead);
        discardPile.RemoveAll(entry => entry.ownerUnit != null && entry.ownerUnit.IsDead);
    }

    private static void ShuffleDeckList(List<DeckCardEntry> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIdx = Random.Range(0, i + 1);
            DeckCardEntry temp = list[i];
            list[i] = list[swapIdx];
            list[swapIdx] = temp;
        }
    }

    /// <summary>
    /// 턴 시작 시 호출:
    /// - 모든 생존 플레이어의 카드를 합친 덱에서 5장의 카드를 랜덤으로 뽑아 손에 들고 시작합니다.
    /// </summary>
    public void DealRandomHandsForAllPlayers(IReadOnlyList<SelectableUnit> players)
    {
        EnsureSpritesFromPrefabIfNeeded();
        EnsureDeckAndDiscardUI();

        isZeroCostQuickBusterActive = false;
        isSuperBusterActiveThisTurn = false;

        if (!isDeckInitialized || (drawPile.Count == 0 && discardPile.Count == 0 && handCards.Count == 0))
        {
            InitializeSharedDeck(players);
        }
        else if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null)
                    playerUnitMap[i] = players[i];
            }
        }

        // 남아있던 손패가 있으면 버린 카드 더미로 회수 (미사용 스타는 카운터로 반환)
        DiscardAllRemainingHandImmediate();
        PurgeDeadPlayerCardsFromPiles();

        SetHandVisibility(true);
        if (turnEndButton != null)
            turnEndButton.interactable = true;

        // 5장 드로우 실행
        DrawCards(turnStartDrawCount);
    }

    /// <summary>
    /// 지정한 장수만큼 덱(Draw Pile)에서 카드를 뽑아 손패에 추가하고 드로우 비행 연출을 재생합니다.
    /// (덱이 비면 버린 카드 더미를 섞어서 보충하며, 손패 최대 10장 제한을 준수합니다.)
    /// </summary>
    public List<Card> DrawCards(int countToDraw)
    {
        EnsureSpritesFromPrefabIfNeeded();
        EnsureDeckAndDiscardUI();

        List<Card> newlyDrawn = new List<Card>();
        if (countToDraw <= 0) return newlyDrawn;

        PurgeDeadPlayerCardsFromPiles();

        for (int n = 0; n < countToDraw; n++)
        {
            if (handCards.Count >= maxHandSize)
                break;

            if (drawPile.Count == 0 && discardPile.Count > 0)
            {
                // 버린 카드 더미를 섞어서 덱으로 보충
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                ShuffleDeckList(drawPile);
            }

            if (drawPile.Count == 0)
                break;

            DeckCardEntry entry = drawPile[0];
            drawPile.RemoveAt(0);

            Card cardObj = CreateCardInstance(handCards.Count + 1);
            Sprite frameSp = GetSpriteForColor(entry.cardColor);
            cardObj.SetHandCardSize(cardSize);
            cardObj.SetupCard(entry.ownerPlayerIndex, entry.ownerUnit, entry.cardColor, frameSp, handCards.Count);

            handCards.Add(cardObj);
            newlyDrawn.Add(cardObj);
        }

        targetHandCount = handCards.Count;
        RefreshPileCountUI();
        UpdateHandLayout();

        // 드로우 비행 연출 재생
        for (int i = 0; i < newlyDrawn.Count; i++)
        {
            Card c = newlyDrawn[i];
            if (c != null && gameObject.activeInHierarchy)
            {
                StartCoroutine(CoAnimateDrawCardFromPile(c, i * 0.055f));
            }
        }

        // 이번 턴 슈퍼Buster가 활성화되어 있다면 새로 뽑힌 Buster 카드 흡수 체크
        if (isSuperBusterActiveThisTurn && gameObject.activeInHierarchy)
        {
            StartCoroutine(CoDelayedAbsorbBusterCards(newlyDrawn.Count * 0.06f + 0.26f));
        }

        return newlyDrawn;
    }

    [ContextMenu("카드 1장 뽑기 (Draw Card)")]
    public Card DrawCard()
    {
        List<Card> drawn = DrawCards(1);
        return drawn.Count > 0 ? drawn[0] : null;
    }

    private IEnumerator CoAnimateDrawCardFromPile(Card card, float delay)
    {
        if (card == null) yield break;
        RectTransform cardRt = card.GetComponent<RectTransform>();
        if (cardRt == null) yield break;

        card.IsAnimatingFlight = true;

        Vector2 startLocalPos = GetPileLocalPosInHandContainer(drawPileRoot, new Vector2(-760f, -40f));
        Vector2 targetPos = cardRt.anchoredPosition;
        Quaternion targetRot = cardRt.localRotation;

        cardRt.anchoredPosition = startLocalPos;
        cardRt.localRotation = Quaternion.Euler(0f, 0f, 25f);
        cardRt.localScale = Vector3.one * 0.25f;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (card == null || cardRt == null) yield break;

        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayCardPickup();

        PunchPileIcon(drawPileRoot, 1.16f, 0.14f);

        float duration = 0.24f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (card == null || cardRt == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            int idx = handCards.IndexOf(card);
            if (idx >= 0)
            {
                ComputeCardSlotTransform(idx, handCards.Count, out targetPos, out targetRot);
            }

            Vector2 mid = (startLocalPos + targetPos) * 0.5f + new Vector2(0f, 95f);
            float u = 1f - eased;
            Vector2 curPos = u * u * startLocalPos + 2f * u * eased * mid + eased * eased * targetPos;

            cardRt.anchoredPosition = curPos;
            cardRt.localRotation = Quaternion.Slerp(Quaternion.Euler(0f, 0f, 25f), targetRot, eased);
            cardRt.localScale = Vector3.Lerp(Vector3.one * 0.25f, Vector3.one, eased);

            yield return null;
        }

        if (card != null)
        {
            card.IsAnimatingFlight = false;
            UpdateHandLayout();
        }
    }

    // =========================================================================
    // 2. 사용한 카드 및 버린 카드 더미(Discard Pile) 비행 애니메이션 (PDF 2p 슬더스2 참고)
    // =========================================================================
    public void ConsumeAndDiscardCard(Card usedCard)
    {
        if (usedCard == null) return;

        int idx = handCards.IndexOf(usedCard);
        if (idx >= 0)
        {
            handCards.RemoveAt(idx);
        }

        // 슈퍼Buster 카드는 일회성 생성 카드이므로 버린 카드 더미에 넣지 않고 소멸, 일반 카드는 버린 카드 더미에 추가
        if (!usedCard.IsSuperBuster)
        {
            discardPile.Add(new DeckCardEntry(usedCard.OwnerPlayerIndex, usedCard.OwnerPlayerUnit, usedCard.CardColor));
        }

        targetHandCount = handCards.Count;
        UpdateHandLayout();

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(CoFlyCardToDiscardPile(usedCard));
        }
        else
        {
            Destroy(usedCard.gameObject);
            RefreshPileCountUI();
        }
    }

    private IEnumerator CoFlyCardToDiscardPile(Card card)
    {
        if (card == null) yield break;

        card.IsAnimatingFlight = true;
        RectTransform cardRt = card.GetComponent<RectTransform>();
        CanvasGroup cg = card.GetComponent<CanvasGroup>();
        if (cg != null) cg.blocksRaycasts = false;

        Vector2 startPos = cardRt.anchoredPosition;
        Vector2 targetDiscardPos = GetPileLocalPosInHandContainer(discardPileRoot, new Vector2(760f, -40f));
        Vector2 controlPt = (startPos + targetDiscardPos) * 0.5f + new Vector2(0f, 140f);

        float duration = 0.26f;
        float elapsed = 0f;
        float startAngle = cardRt.localEulerAngles.z;

        while (elapsed < duration)
        {
            if (card == null || cardRt == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);

            float u = 1f - eased;
            Vector2 pos = u * u * startPos + 2f * u * eased * controlPt + eased * eased * targetDiscardPos;
            cardRt.anchoredPosition = pos;
            cardRt.localRotation = Quaternion.Euler(0f, 0f, startAngle - eased * 240f);
            cardRt.localScale = Vector3.Lerp(Vector3.one * 0.95f, Vector3.one * 0.22f, eased);

            yield return null;
        }

        if (card != null)
            Destroy(card.gameObject);

        RefreshPileCountUI();
        PunchPileIcon(discardPileRoot, 1.2f, 0.16f);
    }

    /// <summary>
    /// 턴 종료 시 남아있는 손패 카드를 모두 버린 카드 더미로 날려 보냅니다.
    /// (미사용 카드에 배정되어 있던 스타는 스타 카운터로 반환합니다.)
    /// </summary>
    public IEnumerator CoDiscardRemainingHandAtTurnEnd()
    {
        if (turnEndButton != null)
            turnEndButton.interactable = false;

        if (handCards.Count == 0)
            yield break;

        List<Card> toDiscard = new List<Card>(handCards);
        handCards.Clear();
        targetHandCount = 0;

        for (int i = 0; i < toDiscard.Count; i++)
        {
            Card c = toDiscard[i];
            if (c == null) continue;

            // 미사용 스타 반환
            if (c.AssignedStars > 0 && BattleResourceUIManager.Ins != null)
            {
                BattleResourceUIManager.Ins.AddStars(c.AssignedStars);
                c.SetAssignedStars(0);
            }

            if (!c.IsSuperBuster)
            {
                discardPile.Add(new DeckCardEntry(c.OwnerPlayerIndex, c.OwnerPlayerUnit, c.CardColor));
            }

            StartCoroutine(CoFlyCardToDiscardPile(c));
            yield return new WaitForSeconds(0.035f);
        }

        yield return new WaitForSeconds(0.22f);
        RefreshPileCountUI();
    }

    private void DiscardAllRemainingHandImmediate()
    {
        for (int i = handCards.Count - 1; i >= 0; i--)
        {
            Card c = handCards[i];
            if (c == null) continue;

            if (c.AssignedStars > 0 && BattleResourceUIManager.Ins != null)
            {
                BattleResourceUIManager.Ins.AddStars(c.AssignedStars);
            }

            if (!c.IsSuperBuster)
            {
                discardPile.Add(new DeckCardEntry(c.OwnerPlayerIndex, c.OwnerPlayerUnit, c.CardColor));
            }

            Destroy(c.gameObject);
        }
        handCards.Clear();
        targetHandCount = 0;
        RefreshPileCountUI();
    }

    // =========================================================================
    // 3. 플레이어 고유 스킬 연동 (Player_1 1스킬/3스킬, Player_3 3스킬 슈퍼Buster)
    // =========================================================================

    /// <summary>
    /// Player_1의 1스킬: 이번 턴 동안 손패의 Quick && Buster 카드들의 필요 코스트를 0으로 만든다.
    /// </summary>
    public void ActivateZeroCostQuickBusterForTurn()
    {
        isZeroCostQuickBusterActive = true;
        for (int i = 0; i < handCards.Count; i++)
        {
            if (handCards[i] != null)
            {
                handCards[i].RefreshCostText();
                if (handCards[i].CardColor == ECardColor.QUICK || handCards[i].CardColor.IsBusterAttribute())
                {
                    handCards[i].PunchScale(1.15f, 0.18f);
                }
            }
        }
    }

    /// <summary>
    /// Player_1의 3스킬: 손패를 모두 버리고 5장을 새로 드로우한다.
    /// </summary>
    public void DiscardHandAndRedraw(int redrawCount = 5)
    {
        StartCoroutine(CoDiscardHandAndRedraw(redrawCount));
    }

    private IEnumerator CoDiscardHandAndRedraw(int redrawCount)
    {
        List<Card> currentCards = new List<Card>(handCards);
        handCards.Clear();
        targetHandCount = 0;

        for (int i = 0; i < currentCards.Count; i++)
        {
            Card c = currentCards[i];
            if (c == null) continue;

            if (c.AssignedStars > 0 && BattleResourceUIManager.Ins != null)
            {
                BattleResourceUIManager.Ins.AddStars(c.AssignedStars);
                c.SetAssignedStars(0);
            }

            if (!c.IsSuperBuster)
            {
                discardPile.Add(new DeckCardEntry(c.OwnerPlayerIndex, c.OwnerPlayerUnit, c.CardColor));
            }

            StartCoroutine(CoFlyCardToDiscardPile(c));
        }

        if (currentCards.Count > 0)
            yield return new WaitForSeconds(0.28f);

        DrawCards(redrawCount);
    }

    /// <summary>
    /// Player_3의 3스킬: 이번 턴 슈퍼Buster 카드를 생성하고, 손패에 있는 Buster 카드들을 흡수해 공격력을 누적시킨다.
    /// </summary>
    public void CreateSuperBusterCard(SelectableUnit player3Unit)
    {
        isSuperBusterActiveThisTurn = true;

        Card superCard = FindSuperBusterCardInHand();
        if (superCard == null)
        {
            int pIdx = player3Unit != null ? player3Unit.UnitIndex : 2;
            superCard = CreateCardInstance(handCards.Count + 1);
            superCard.SetHandCardSize(cardSize);
            superCard.SetupCard(pIdx, player3Unit, ECardColor.SUPER_BUSTER, GetSpriteForColor(ECardColor.RED), handCards.Count);

            handCards.Add(superCard);
            targetHandCount = handCards.Count;
            UpdateHandLayout();
            superCard.PunchScale(1.25f, 0.25f);
        }

        StartCoroutine(CoDelayedAbsorbBusterCards(0.12f));
    }

    private Card FindSuperBusterCardInHand()
    {
        for (int i = 0; i < handCards.Count; i++)
        {
            if (handCards[i] != null && handCards[i].IsSuperBuster)
                return handCards[i];
        }
        return null;
    }

    private IEnumerator CoDelayedAbsorbBusterCards(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Card superCard = FindSuperBusterCardInHand();
        if (superCard == null) yield break;

        List<Card> bustersToAbsorb = new List<Card>();
        for (int i = 0; i < handCards.Count; i++)
        {
            Card c = handCards[i];
            if (c != null && c != superCard && c.CardColor == ECardColor.BUSTER)
            {
                bustersToAbsorb.Add(c);
            }
        }

        if (bustersToAbsorb.Count == 0) yield break;

        for (int i = 0; i < bustersToAbsorb.Count; i++)
        {
            Card buster = bustersToAbsorb[i];
            if (buster == null) continue;

            handCards.Remove(buster);
            // 덱 총량 유지를 위해 흡수된 일반 Buster 카드는 버린 카드 더미 데이터에는 기록
            discardPile.Add(new DeckCardEntry(buster.OwnerPlayerIndex, buster.OwnerPlayerUnit, ECardColor.BUSTER));

            float busterAttackDmg = buster.CalculateCardRawDamage();
            int busterStars = buster.AssignedStars;

            StartCoroutine(CoAnimateAbsorbBusterIntoSuper(buster, superCard, busterAttackDmg, busterStars));
        }

        targetHandCount = handCards.Count;
        RefreshPileCountUI();
        yield return new WaitForSeconds(0.24f);
        UpdateHandLayout();
    }

    private IEnumerator CoAnimateAbsorbBusterIntoSuper(Card busterCard, Card superCard, float addedDmg, int busterStars)
    {
        if (busterCard == null) yield break;
        busterCard.IsAnimatingFlight = true;

        RectTransform bRt = busterCard.GetComponent<RectTransform>();
        RectTransform sRt = superCard != null ? superCard.GetComponent<RectTransform>() : null;

        Vector2 startPos = bRt.anchoredPosition;
        float duration = 0.22f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (busterCard == null || bRt == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t;

            Vector2 targetPos = sRt != null ? sRt.anchoredPosition : Vector2.zero;
            bRt.anchoredPosition = Vector2.Lerp(startPos, targetPos, eased);
            bRt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.2f, eased);
            yield return null;
        }

        if (busterCard != null)
            Destroy(busterCard.gameObject);

        if (superCard != null)
        {
            superCard.AbsorbBusterCardDamage(addedDmg);
            if (busterStars > 0)
            {
                int newStars = Mathf.Min(10, superCard.AssignedStars + busterStars);
                int leftover = (superCard.AssignedStars + busterStars) - newStars;
                superCard.SetAssignedStars(newStars);
                if (leftover > 0 && BattleResourceUIManager.Ins != null)
                    BattleResourceUIManager.Ins.AddStars(leftover);
            }
        }

        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayCardEquip();
    }

    public void RefreshSuperBusterCardDisplay()
    {
        RefreshAllCardCostsAndLabels();
    }

    public void RefreshAllCardCostsAndLabels()
    {
        for (int i = 0; i < handCards.Count; i++)
        {
            if (handCards[i] != null)
            {
                handCards[i].RefreshCardTypeLabel();
                handCards[i].RefreshCostText();
            }
        }
    }

    // =========================================================================
    // 4. 스타 -> 손패 카드 치명타 확률 부여 연동
    // =========================================================================
    private IEnumerator CoDelayedDistributeStarsToHand(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        DistributeAvailableStarsToHandCards();
    }

    public void DistributeAvailableStarsToHandCards()
    {
        if (BattleResourceUIManager.Ins == null || handCards.Count == 0) return;

        int availableStars = BattleResourceUIManager.Ins.CurrentStars;
        if (availableStars <= 0) return;

        // 아직 스타 10개(100%) 미만인 손패 카드들에 부여할 수 있는 최대 스타 개수 계산
        List<int> targetCardIndices = new List<int>();
        int[] simulatedCounts = new int[handCards.Count];
        for (int i = 0; i < handCards.Count; i++)
        {
            simulatedCounts[i] = handCards[i] != null ? handCards[i].AssignedStars : 10;
        }

        for (int s = 0; s < availableStars; s++)
        {
            List<int> candidates = new List<int>();
            for (int i = 0; i < handCards.Count; i++)
            {
                if (handCards[i] != null && simulatedCounts[i] < 10)
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
                break;

            int pick = candidates[Random.Range(0, candidates.Count)];
            simulatedCounts[pick]++;
            targetCardIndices.Add(pick);
        }

        if (targetCardIndices.Count > 0 && CombatEffectManager.Ins != null && gameObject.activeInHierarchy)
        {
            StartCoroutine(CombatEffectManager.Ins.CoDistributeStarsToCards(handCards, targetCardIndices.ToArray()));
        }
    }

    public int GetTotalAssignedStarsInHand()
    {
        int sum = 0;
        for (int i = 0; i < handCards.Count; i++)
        {
            if (handCards[i] != null)
                sum += handCards[i].AssignedStars;
        }
        return sum;
    }

    public void DeductAssignedStarsFromHandCards(int amount)
    {
        int remain = amount;
        for (int i = handCards.Count - 1; i >= 0 && remain > 0; i--)
        {
            Card c = handCards[i];
            if (c != null && c.AssignedStars > 0)
            {
                int take = Mathf.Min(c.AssignedStars, remain);
                c.SetAssignedStars(c.AssignedStars - take);
                remain -= take;
            }
        }
    }

    // =========================================================================
    // 5. 완만한 부채꼴 배치 (최대 10장 화면 내 배치) 및 영역 판정
    // =========================================================================
    public bool IsScreenPointInsideHandArea(Vector2 screenPoint, Camera eventCamera)
    {
        if (handContainer == null)
            handContainer = GetComponent<RectTransform>();
        if (handContainer == null) return false;

        Camera cam = eventCamera;
        if (cam == null)
        {
            if (cachedCanvas == null) cachedCanvas = GetComponentInParent<Canvas>();
            if (cachedCanvas != null && cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = cachedCanvas.worldCamera;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(handContainer, screenPoint, cam, out Vector2 localPt))
            return false;

        // 손패 상단 경계선(y <= 155f) 안쪽이면 손패 영역 내부로 간주, 그 위로 벗어나면 카드 사용(타겟팅 화살표) 모드로 전환!
        return localPt.y <= 155f;
    }

    public Vector2 GetCardTopCenterCanvasLocalPos(RectTransform cardRt)
    {
        if (cachedCanvas == null)
            cachedCanvas = GetComponentInParent<Canvas>() ?? Object.FindFirstObjectByType<Canvas>();
        if (cachedCanvas == null) return Vector2.zero;
        if (cachedCanvasRect == null)
            cachedCanvasRect = cachedCanvas.GetComponent<RectTransform>();

        Vector3 worldTop = cardRt.TransformPoint(new Vector3(0f, cardRt.rect.height * 0.35f, 0f));
        Camera uiCam = cachedCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cachedCanvas.worldCamera;
        Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(uiCam, worldTop);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(cachedCanvasRect, screenPt, uiCam, out Vector2 canvasLocal);
        return canvasLocal;
    }

    private void ComputeCardSlotTransform(int index, int totalCount, out Vector2 pos, out Quaternion rot)
    {
        if (totalCount <= 0)
        {
            pos = new Vector2(0f, baseYOffset);
            rot = Quaternion.identity;
            return;
        }

        float centerIndex = (totalCount - 1) * 0.5f;
        float offsetFromCenter = index - centerIndex;

        // 1~5장은 기본 간격, 6~10장은 최대 10장까지 화면에 여유롭게 담기도록 완만한 부채꼴로 자동 조정
        float crowdRatio = Mathf.Clamp01((totalCount - 5f) / 5f);
        float dynamicSpacing = Mathf.Lerp(cardSpacing, 82f, crowdRatio);
        float dynamicAngleStep = Mathf.Lerp(angleStep, 2.1f, crowdRatio);
        float dynamicArcDrop = Mathf.Lerp(arcDropPerStep, 3.6f, crowdRatio);

        float posX = offsetFromCenter * dynamicSpacing;
        float posY = baseYOffset - (offsetFromCenter * offsetFromCenter * dynamicArcDrop);
        float rotZ = -offsetFromCenter * dynamicAngleStep;

        pos = new Vector2(posX, posY);
        rot = Quaternion.Euler(0f, 0f, rotZ);
    }

    public void UpdateHandLayout()
    {
        int count = handCards.Count;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            Card card = handCards[i];
            if (card == null) continue;

            ComputeCardSlotTransform(i, count, out Vector2 targetPos, out Quaternion targetRot);
            card.SetHandCardSize(cardSize);
            card.SetCardTransform(targetPos, targetRot, i);
        }
    }

    public void ShowPlayerHand(int playerIndex)
    {
        currentSelectedPlayerIndex = playerIndex;
        UpdateHandLayout();
    }

    public void SetHandVisibility(bool visible)
    {
        for (int i = 0; i < handCards.Count; i++)
        {
            if (handCards[i] != null)
            {
                handCards[i].gameObject.SetActive(visible);
            }
        }
    }

    public void DetachCardFromHand(Card targetCard)
    {
        if (targetCard == null) return;
        handCards.Remove(targetCard);
        targetHandCount = handCards.Count;
        UpdateHandLayout();
    }

    public void ReturnCardToHand(Card targetCard)
    {
        if (targetCard == null) return;
        if (!handCards.Contains(targetCard))
        {
            targetCard.transform.SetParent(handContainer, false);
            handCards.Add(targetCard);
        }
        targetHandCount = handCards.Count;
        UpdateHandLayout();
    }

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

    public void ClearHand()
    {
        for (int i = handCards.Count - 1; i >= 0; i--)
        {
            if (handCards[i] != null)
                Destroy(handCards[i].gameObject);
        }
        handCards.Clear();
        targetHandCount = 0;
    }

    public Sprite GetSpriteForColor(ECardColor color)
    {
        EnsureSpritesFromPrefabIfNeeded();

        switch (color)
        {
            case ECardColor.RED:
            case ECardColor.SUPER_BUSTER:
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

    // =========================================================================
    // 6. 슬더스2 스타일 덱(DrawPileUI), 버린 카드(DiscardPileUI), 턴 종료 버튼 생성 및 관리
    // =========================================================================
    public void EnsureDeckAndDiscardUI()
    {
        if (cachedCanvas == null)
            cachedCanvas = GetComponentInParent<Canvas>() ?? Object.FindFirstObjectByType<Canvas>();
        if (cachedCanvas == null) return;

        cachedCanvasRect = cachedCanvas.GetComponent<RectTransform>();
        Transform canvasTr = cachedCanvas.transform;

        // 기존 AttackBox는 리메이크에서 직접 카드 타겟팅 방식으로 대체되었으므로 화면을 가리지 않도록 비활성화
        Transform oldAttackBox = canvasTr.Find("AttackBox");
        if (oldAttackBox != null && oldAttackBox.gameObject.activeSelf)
        {
            oldAttackBox.gameObject.SetActive(false);
        }

        // 1) 좌측 하단 덱(Draw Pile) UI
        if (drawPileRoot == null)
        {
            Transform existing = canvasTr.Find("DrawPileUI");
            if (existing != null)
            {
                drawPileRoot = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject dpObj = new GameObject("DrawPileUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                dpObj.layer = LayerMask.NameToLayer("UI");
                dpObj.transform.SetParent(canvasTr, false);

                drawPileRoot = dpObj.GetComponent<RectTransform>();
                drawPileRoot.anchorMin = new Vector2(0f, 0f);
                drawPileRoot.anchorMax = new Vector2(0f, 0f);
                drawPileRoot.pivot = new Vector2(0f, 0f);
                drawPileRoot.anchoredPosition = new Vector2(24f, 20f);
                drawPileRoot.sizeDelta = new Vector2(86f, 96f);
                drawPileRoot.localScale = Vector3.one;

                Image dpImg = dpObj.GetComponent<Image>();
                dpImg.sprite = GetOrCreateDeckPileSprite(false);
                dpImg.preserveAspect = true;
                dpImg.raycastTarget = false;
            }
        }

        if (drawPileCountText == null && drawPileRoot != null)
        {
            drawPileCountText = EnsurePileBadgeText(drawPileRoot, "DrawCountBadge", new Vector2(68f, 18f), new Color(0.88f, 0.25f, 0.22f, 1f));
        }

        // 2) 우측 하단 버린 카드 더미(Discard Pile) UI
        if (discardPileRoot == null)
        {
            Transform existing = canvasTr.Find("DiscardPileUI");
            if (existing != null)
            {
                discardPileRoot = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject dcpObj = new GameObject("DiscardPileUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                dcpObj.layer = LayerMask.NameToLayer("UI");
                dcpObj.transform.SetParent(canvasTr, false);

                discardPileRoot = dcpObj.GetComponent<RectTransform>();
                discardPileRoot.anchorMin = new Vector2(1f, 0f);
                discardPileRoot.anchorMax = new Vector2(1f, 0f);
                discardPileRoot.pivot = new Vector2(1f, 0f);
                discardPileRoot.anchoredPosition = new Vector2(-24f, 20f);
                discardPileRoot.sizeDelta = new Vector2(86f, 96f);
                discardPileRoot.localScale = Vector3.one;

                Image dcpImg = dcpObj.GetComponent<Image>();
                dcpImg.sprite = GetOrCreateDeckPileSprite(true);
                dcpImg.preserveAspect = true;
                dcpImg.raycastTarget = false;
            }
        }

        if (discardPileCountText == null && discardPileRoot != null)
        {
            discardPileCountText = EnsurePileBadgeText(discardPileRoot, "DiscardCountBadge", new Vector2(-68f, 18f), new Color(0.75f, 0.22f, 0.35f, 1f), true);
        }

        // 3) 우측 하단 슬더스2 스타일 [턴 종료] 버튼
        if (turnEndButtonRoot == null)
        {
            Transform existing = canvasTr.Find("TurnEndButtonUI");
            if (existing != null)
            {
                turnEndButtonRoot = existing.GetComponent<RectTransform>();
                turnEndButton = existing.GetComponent<Button>();
            }
            else
            {
                GameObject btnObj = new GameObject("TurnEndButtonUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                btnObj.layer = LayerMask.NameToLayer("UI");
                btnObj.transform.SetParent(canvasTr, false);

                turnEndButtonRoot = btnObj.GetComponent<RectTransform>();
                turnEndButtonRoot.anchorMin = new Vector2(1f, 0f);
                turnEndButtonRoot.anchorMax = new Vector2(1f, 0f);
                turnEndButtonRoot.pivot = new Vector2(1f, 0f);
                turnEndButtonRoot.anchoredPosition = new Vector2(-126f, 132f);
                turnEndButtonRoot.sizeDelta = new Vector2(174f, 64f);
                turnEndButtonRoot.localScale = Vector3.one;

                Image btnImg = btnObj.GetComponent<Image>();
                btnImg.sprite = GetOrCreateTurnEndButtonSprite();
                btnImg.type = Image.Type.Sliced;
                btnImg.color = Color.white;
                btnImg.raycastTarget = true;

                turnEndButton = btnObj.GetComponent<Button>();
                turnEndButton.targetGraphic = btnImg;

                UIHelper.CreateOutlinedText(
                    btnObj.transform,
                    "TurnEndLabel",
                    "턴 종료",
                    22,
                    FontStyle.Bold,
                    new Color(0.95f, 0.98f, 1f, 1f),
                    Vector2.zero,
                    new Vector2(160f, 54f),
                    TextAnchor.MiddleCenter,
                    new Color(0.02f, 0.06f, 0.16f, 0.98f),
                    new Vector2(1.8f, -1.8f)
                );
            }
        }

        if (turnEndButton != null)
        {
            turnEndButton.onClick.RemoveAllListeners();
            turnEndButton.onClick.AddListener(OnTurnEndButtonClicked);
        }

        RefreshPileCountUI();
    }

    private void OnTurnEndButtonClicked()
    {
        if (BattleManager.Ins == null || !BattleManager.Ins.IsPlayerInputAllowed)
            return;

        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayUIClick();

        BattleManager.Ins.TurnEnd();
    }

    private Text EnsurePileBadgeText(RectTransform pileRoot, string badgeName, Vector2 anchoredPos, Color badgeColor, bool rightAnchored = false)
    {
        Transform existing = pileRoot.Find(badgeName);
        if (existing != null)
        {
            Transform txtTr = existing.Find("CountText");
            if (txtTr != null) return txtTr.GetComponent<Text>();
        }

        GameObject badgeObj = new GameObject(badgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        badgeObj.layer = LayerMask.NameToLayer("UI");
        badgeObj.transform.SetParent(pileRoot, false);

        RectTransform rt = badgeObj.GetComponent<RectTransform>();
        rt.anchorMin = rightAnchored ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
        rt.anchorMax = rightAnchored ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(34f, 34f);

        Image img = badgeObj.GetComponent<Image>();
        img.sprite = GetOrCreateCountBadgeSprite();
        img.color = badgeColor;
        img.raycastTarget = false;

        return UIHelper.CreateOutlinedText(
            badgeObj.transform,
            "CountText",
            "0",
            18,
            FontStyle.Bold,
            Color.white,
            Vector2.zero,
            new Vector2(34f, 34f),
            TextAnchor.MiddleCenter,
            new Color(0.05f, 0.02f, 0.02f, 0.98f),
            new Vector2(1.4f, -1.4f)
        );
    }

    public void RefreshPileCountUI()
    {
        if (drawPileCountText != null)
            drawPileCountText.text = drawPile.Count.ToString();

        if (discardPileCountText != null)
            discardPileCountText.text = discardPile.Count.ToString();
    }

    private Vector2 GetPileLocalPosInHandContainer(RectTransform pileRt, Vector2 fallback)
    {
        if (pileRt == null || handContainer == null) return fallback;

        if (cachedCanvas == null)
            cachedCanvas = GetComponentInParent<Canvas>();
        Camera uiCam = (cachedCanvas != null && cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? cachedCanvas.worldCamera
            : null;

        Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(uiCam, pileRt.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(handContainer, screenPt, uiCam, out Vector2 localPos))
            return localPos;

        return fallback;
    }

    private void PunchPileIcon(RectTransform pileRt, float multiplier, float duration)
    {
        if (pileRt == null || !gameObject.activeInHierarchy) return;
        StartCoroutine(CoPunchPile(pileRt, multiplier, duration));
    }

    private IEnumerator CoPunchPile(RectTransform rt, float multiplier, float duration)
    {
        if (rt == null) yield break;
        float half = duration * 0.45f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            if (rt == null) yield break;
            elapsed += Time.deltaTime;
            rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * multiplier, elapsed / half);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < duration - half)
        {
            if (rt == null) yield break;
            elapsed += Time.deltaTime;
            rt.localScale = Vector3.Lerp(Vector3.one * multiplier, Vector3.one, elapsed / (duration - half));
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    // =========================================================================
    // 슬더스2 스타일 덱/버린카드/턴종료 텍스처 생성기
    // =========================================================================
    private static Sprite GetOrCreateDeckPileSprite(bool isDiscard)
    {
        if (!isDiscard && cachedDeckPileSprite != null) return cachedDeckPileSprite;
        if (isDiscard && cachedDiscardPileSprite != null) return cachedDiscardPileSprite;

        int w = 88;
        int h = 96;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];

        Color cardBackMain = isDiscard
            ? new Color(0.24f, 0.30f, 0.42f, 0.96f)
            : new Color(0.58f, 0.36f, 0.24f, 0.96f);
        Color cardRim = isDiscard
            ? new Color(0.48f, 0.72f, 0.95f, 0.98f)
            : new Color(0.92f, 0.76f, 0.52f, 0.98f);
        Color darkOutline = new Color(0.06f, 0.07f, 0.12f, 0.98f);

        // 겹쳐진 3장의 카드 더미 렌더링
        for (int layer = 0; layer < 3; layer++)
        {
            float cx = w * 0.5f + (layer - 1) * (isDiscard ? -6f : 6f);
            float cy = h * 0.5f + (layer - 1) * 4f;
            float angle = (layer - 1) * (isDiscard ? 9f : -9f) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float halfW = 24f;
            float halfH = 33f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float rx = Mathf.Abs(dx * cos + dy * sin);
                    float ry = Mathf.Abs(-dx * sin + dy * cos);

                    if (rx <= halfW && ry <= halfH)
                    {
                        float edge = Mathf.Min(halfW - rx, halfH - ry);
                        if (edge < 2.2f)
                            pixels[y * w + x] = darkOutline;
                        else if (edge < 5.0f)
                            pixels[y * w + x] = cardRim;
                        else
                            pixels[y * w + x] = cardBackMain;
                    }
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sp.name = isDiscard ? "ui_discard_pile" : "ui_draw_pile";
        if (isDiscard) cachedDiscardPileSprite = sp;
        else cachedDeckPileSprite = sp;
        return sp;
    }

    private static Sprite GetOrCreateCountBadgeSprite()
    {
        if (cachedCountBadgeSprite != null) return cachedCountBadgeSprite;

        int size = 48;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
                if (dist <= half)
                {
                    float edge = half - dist;
                    float alpha = Mathf.Clamp01(edge / 1.5f);
                    if (edge < 3.0f)
                        pixels[y * size + x] = new Color(0.15f, 0.12f, 0.10f, alpha);
                    else
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedCountBadgeSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        cachedCountBadgeSprite.name = "ui_pile_count_badge";
        return cachedCountBadgeSprite;
    }

    private static Sprite GetOrCreateTurnEndButtonSprite()
    {
        if (cachedTurnEndBtnSprite != null) return cachedTurnEndBtnSprite;

        int w = 180;
        int h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];

        Color rimGold = new Color(0.75f, 0.68f, 0.52f, 0.98f);
        Color innerTop = new Color(0.16f, 0.30f, 0.46f, 0.96f);
        Color innerBot = new Color(0.08f, 0.16f, 0.28f, 0.98f);

        int chamfer = 14;
        for (int y = 0; y < h; y++)
        {
            float ty = (float)y / (h - 1);
            Color bodyCol = Color.Lerp(innerBot, innerTop, ty);
            for (int x = 0; x < w; x++)
            {
                int dx = Mathf.Min(x, w - 1 - x);
                int dy = Mathf.Min(y, h - 1 - y);
                int corner = dx + dy - chamfer;
                int dist = Mathf.Min(Mathf.Min(dx, dy), corner);

                if (dist >= 0)
                {
                    if (dist < 3)
                        pixels[y * w + x] = rimGold;
                    else
                        pixels[y * w + x] = bodyCol;
                }
                else
                {
                    pixels[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        cachedTurnEndBtnSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
        cachedTurnEndBtnSprite.name = "ui_sts2_turn_end_btn";
        return cachedTurnEndBtnSprite;
    }
}

public static class CardColorHelperExtensions
{
    public static bool IsBusterAttribute(this ECardColor color)
    {
        return color == ECardColor.BUSTER || color == ECardColor.SUPER_BUSTER;
    }
}
