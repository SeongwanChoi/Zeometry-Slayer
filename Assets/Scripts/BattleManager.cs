using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BattleManager : Singleton<BattleManager>
{
    public static event System.Action OnPlayerTurnEndedEvent;
    public static System.Func<SelectableUnit, IEnumerator> EnemyTurnTutorialHook;

    [Header("현재 턴 상태")]
    [SerializeField] private ETurn currunt_Turn = ETurn.PLAYER;
    [SerializeField] private int currentTurnNumber = 0;

    [Header("1. 턴 UI 및 배너 중앙 유지 시간 (Inspector 조정 가능)")]
    [SerializeField] private TurnUIController turnUIController;
    [Tooltip("턴 시작 시 화면 가운데에 배너가 머무르는 일정 시간 (초)")]
    [SerializeField] private float turnBannerCenterHoldTime = 1.0f;

    [Header("2. 플레이어 및 적 오브젝트 참조")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private List<SelectableUnit> playerUnits = new List<SelectableUnit>();
    [SerializeField] private List<SelectableUnit> enemyUnits = new List<SelectableUnit>();

    [Header("3. 캐릭터/적 선택 시 크기 확대 설정")]
    [Tooltip("선택된 플레이어/적 오브젝트가 커지는 배율")]
    [SerializeField] private float selectedScaleMultiplier = 1.22f;

    [Header("4. 페이즈 진행 속도 설정")]
    [Tooltip("카드 공격 연출 간격 (초)")]
    [SerializeField] private float attackStepInterval = 0.18f;
    [Tooltip("Enemy 턴에서 적의 각 공격 사이의 간격 (초)")]
    [SerializeField] private float enemyAttackInterval = 0.22f;
    [Tooltip("적 전체 행동 횟수 (3명 합쳐서 6번)")]
    [SerializeField] private int totalEnemyActions = 6;
    [Tooltip("적 1명당 최대 행동 횟수 (최대 2번)")]
    [SerializeField] private int maxActionsPerEnemy = 2;

    private SelectableUnit selectedPlayer;
    private SelectableUnit selectedEnemy;
    private bool isPhaseTransitioning = false;
    private bool isAttackPhaseRunning = false;
    private bool isCardAttackAnimating = false;
    private bool isBattleEnded = false;
    private bool isDoubleStarGainActiveThisTurn = false;
    private Camera mainCamera;

    /// <summary>
    /// 플레이어 턴이고 공격/적 턴 진행 중이 아니며, 모달/일시정지/결과창이 열려있지 않을 때 조작을 허용합니다.
    /// </summary>
    public bool IsPlayerInputAllowed =>
        !isBattleEnded &&
        (currunt_Turn == ETurn.PLAYER) &&
        !isPhaseTransitioning &&
        !isAttackPhaseRunning &&
        !isCardAttackAnimating &&
        (PauseMenuManager.Ins == null || !PauseMenuManager.Ins.IsPaused) &&
        (BattleResultUIManager.Ins == null || !BattleResultUIManager.Ins.IsResultShowing);

    public SelectableUnit SelectedPlayer => selectedPlayer;
    public SelectableUnit SelectedEnemy => selectedEnemy;
    public ETurn CurrentTurn => currunt_Turn;
    public bool IsDoubleStarGainActiveThisTurn => isDoubleStarGainActiveThisTurn;
    public IReadOnlyList<SelectableUnit> PlayerUnits => playerUnits;
    public IReadOnlyList<SelectableUnit> EnemyUnits => enemyUnits;

    public override void Init()
    {
        base.Init();
        mainCamera = Camera.main;
        EnsureSceneReferences();
    }

    private void Start()
    {
        mainCamera = Camera.main;
        EnsureSceneReferences();
        _ = PauseMenuManager.Ins;
        _ = BattleResultUIManager.Ins;
        _ = BattleResourceUIManager.Ins;
        _ = SkillConfirmModalUI.Ins;
        _ = SplineTargetingArrow.Ins;

        // 첫 번째 Player 턴 시작
        StartCoroutine(CoStartPlayerTurn());
    }

    private void Update()
    {
        if (isBattleEnded) return;

        if (currunt_Turn == ETurn.PLAYER)
        {
            PTurn();
        }
        else if (currunt_Turn == ETurn.ENEMY)
        {
            EnTurn();
        }
    }

    private void PTurn()
    {
        // 플레이어 및 Enemy 오브젝트 클릭 선택 기능은 삭제되었으며,
        // 카드 드래그 타겟팅 및 스킬 아이콘 버튼으로 전투를 조작합니다.
    }

    private void EnTurn()
    {
        // 적 턴은 코루틴(CoExecuteEnemyTurn)으로 순차 진행됩니다.
    }

    public void OnUnitClicked(SelectableUnit clickedUnit)
    {
        // 플레이어, Enemy Object 선택 기능 삭제됨
    }

    public void SelectPlayerUnit(SelectableUnit targetPlayer)
    {
        // 플레이어 선택 및 크기 확대 기능 삭제됨
    }

    public void SelectEnemyUnit(SelectableUnit targetEnemy)
    {
        // Enemy 선택 및 크기 확대 기능 삭제됨
    }

    // =========================================================================
    // 플레이어 고유 스킬 지원 메서드 (Player_2 스타 +50 / 스타 2배 / 전체 방어력 +50%)
    // =========================================================================
    public void GainStarsImmediate(int amount)
    {
        if (amount <= 0) return;
        int finalAmount = isDoubleStarGainActiveThisTurn ? amount * 2 : amount;

        if (BattleResourceUIManager.Ins != null)
        {
            BattleResourceUIManager.Ins.AddStars(finalAmount);
            BattleResourceUIManager.Ins.PunchStarIcon(1.28f, 0.2f);
        }

        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayStarEffect();
    }

    public void ActivateDoubleStarGainForTurn()
    {
        isDoubleStarGainActiveThisTurn = true;
    }

    public void ApplyPartyDefenseBuffStacks(int stacks)
    {
        EnsureSceneReferences();
        for (int i = 0; i < playerUnits.Count; i++)
        {
            if (playerUnits[i] != null && !playerUnits[i].IsDead)
            {
                playerUnits[i].AddDefenseBuffStacks(stacks);
            }
        }
    }

    // =========================================================================
    // 슬더스2 + FGO 방식 실시간 카드 타겟팅 사용 (PDF 1~5페이지 명세)
    // =========================================================================
    public bool TryPlayCardOnEnemy(Card card, SelectableUnit targetEnemy)
    {
        if (!IsPlayerInputAllowed || card == null || targetEnemy == null || targetEnemy.IsDead)
            return false;

        int neededCost = card.EffectiveCost;
        if (BattleResourceUIManager.Ins != null)
        {
            if (!BattleResourceUIManager.Ins.TrySpendCost(neededCost))
            {
                if (SoundManager.Ins != null)
                    SoundManager.Ins.PlaySkillBlocked();
                return false;
            }
        }

        SelectableUnit attacker = card.OwnerPlayerUnit;
        if (attacker == null || attacker.IsDead)
        {
            attacker = GetFirstAlivePlayer();
        }

        if (attacker == null)
            return false;

        StartCoroutine(CoExecuteSingleCardAttack(card, attacker, targetEnemy));
        return true;
    }

    private IEnumerator CoExecuteSingleCardAttack(
        Card usedCard,
        SelectableUnit attackerPlayer,
        SelectableUnit targetEnemy)
    {
        isCardAttackAnimating = true;

        ECardColor cardColor = usedCard != null ? usedCard.CardColor : ECardColor.ARTS;
        float rawCardDamage = usedCard != null ? usedCard.CalculateCardRawDamage() : 5f;
        int assignedStars = usedCard != null ? usedCard.AssignedStars : 0;

        // 1) 스타가 사용될 때: 해당 카드에 치명타 확률(최대 10개 = 100%)만큼 화면 하단 스타 표기 UI에서 카드로 스타가 날아가도록 연출
        if (usedCard != null)
        {
            usedCard.IsAnimatingFlight = true;
            RectTransform cardRt = usedCard.GetComponent<RectTransform>();
            if (cardRt != null)
            {
                cardRt.anchoredPosition = new Vector2(0f, 62f);
                cardRt.localRotation = Quaternion.identity;
                usedCard.transform.localScale = Vector3.one * 1.15f;
            }

            int availableStars = BattleResourceUIManager.Ins != null ? BattleResourceUIManager.Ins.CurrentStars : 0;
            int starsToFly = Mathf.Clamp(10 - assignedStars, 0, availableStars);

            if (starsToFly > 0)
            {
                if (CombatEffectManager.Ins != null)
                {
                    yield return StartCoroutine(CombatEffectManager.Ins.CoFlyStarsFromCounterToSingleCard(usedCard, starsToFly));
                    assignedStars = usedCard != null ? usedCard.AssignedStars : (assignedStars + starsToFly);
                }
                else if (BattleResourceUIManager.Ins != null)
                {
                    BattleResourceUIManager.Ins.DeductStars(starsToFly);
                    assignedStars += starsToFly;
                    usedCard.SetAssignedStars(assignedStars);
                }
            }

            // 사용한 카드를 버린 카드 더미로 날려 보냄 (슬더스2 스타일 비행 연출)
            if (HandManager.Ins != null)
            {
                HandManager.Ins.ConsumeAndDiscardCard(usedCard);
            }
            else if (usedCard != null)
            {
                Destroy(usedCard.gameObject);
            }
        }

        if (targetEnemy == null || targetEnemy.IsDead)
        {
            targetEnemy = GetFirstAliveEnemy();
        }

        if (attackerPlayer != null && targetEnemy != null)
        {
            float damage = rawCardDamage;

            // 크리티컬 판정 (스타 1개당 10%, 최대 100%)
            float critChance = Mathf.Clamp(assignedStars * 10f, 0f, 100f);
            bool isCritical = (critChance > 0f) && (Random.Range(0f, 100f) < critChance);
            if (isCritical)
            {
                float critMultiplier = attackerPlayer.GetCriticalDamageMultiplier();
                damage *= critMultiplier;
            }

            float finalEnemyDamage = targetEnemy.CalculateFinalTakenDamage(damage);

            // 2) 플레이어 공격 오라 + 적 참격/흔들림/데미지 폰트 팝업 연출
            ECardColor auraColor = cardColor == ECardColor.SUPER_BUSTER ? ECardColor.RED : cardColor;
            bool spawnedPopupInEffect = false;
            if (CombatEffectManager.Ins != null)
            {
                spawnedPopupInEffect = true;
                yield return StartCoroutine(
                    CombatEffectManager.Ins.PlayCardAttackSequence(attackerPlayer, auraColor, targetEnemy, finalEnemyDamage, isCritical)
                );
            }
            else
            {
                yield return new WaitForSeconds(0.32f);
            }

            // 3) 적 체력 차감 및 사망 판정
            Vector3 enemyHitWorldPos = targetEnemy.WorldBasePosition;
            targetEnemy.TakeDamage(damage, isCritical, spawnPopup: !spawnedPopupInEffect);

            // 4) 카드 속성별 효과 처리 (PDF 1페이지 & 스타 연출 변경 지시서)
            switch (cardColor)
            {
                case ECardColor.ARTS:
                    // Arts 카드: 공격력만큼 데미지를 주고 카드를 1장 드로우하며 다음턴 획득할 코스트를 1개 추가한다
                    if (BattleResourceUIManager.Ins != null)
                    {
                        BattleResourceUIManager.Ins.AddNextTurnBonusCost(1);
                    }
                    if (HandManager.Ins != null)
                    {
                        HandManager.Ins.DrawCards(1);
                    }
                    break;

                case ECardColor.QUICK:
                    // Quick 카드: 공격력만큼 데미지를 주고 스타를 생성한다
                    // - 적 피격 시 피격받은 적 오브젝트 위치(바닥 기준: 피격 적 y값)에서 스타 생성
                    // - 스타 흡수는 화면 하단 스타 표기 UI(StarCounterUI)로 빨아들이도록 연출
                    int baseStars = Random.Range(4, 9);
                    int gainedStars = isDoubleStarGainActiveThisTurn ? baseStars * 2 : baseStars;

                    if (CombatEffectManager.Ins != null)
                    {
                        yield return StartCoroutine(CombatEffectManager.Ins.CoSpawnStarsOnGround(enemyHitWorldPos, gainedStars));
                        yield return new WaitForSeconds(0.18f);
                        yield return StartCoroutine(CombatEffectManager.Ins.CoCollectGroundStarsToCounter());
                    }
                    else if (BattleResourceUIManager.Ins != null)
                    {
                        BattleResourceUIManager.Ins.AddStars(gainedStars);
                    }
                    break;
            }

            // 5) 모든 적 사망 승리 판정
            if (AreAllEnemiesDead())
            {
                isBattleEnded = true;
                isCardAttackAnimating = false;

                if (CombatEffectManager.Ins != null && CombatEffectManager.Ins.HasGroundStars)
                {
                    yield return StartCoroutine(CombatEffectManager.Ins.CoCollectGroundStarsToCounter());
                }

                yield return new WaitForSeconds(0.65f);

                if (BattleResultUIManager.Ins != null)
                    BattleResultUIManager.Ins.ShowVictoryUI();

                yield break;
            }
        }

        if (attackStepInterval > 0f)
            yield return new WaitForSeconds(attackStepInterval);

        isCardAttackAnimating = false;
    }

    // =========================================================================
    // 턴 종료 버튼 클릭 처리 -> 남은 손패 버리기 -> Enemy 턴 진행
    // =========================================================================
    public void TurnEnd()
    {
        if (!IsPlayerInputAllowed) return;
        StartCoroutine(CoEndPlayerTurnAndStartEnemyTurn());
    }

    public void CheckAutoEndPlayerTurn()
    {
        // 레거시 호환 유지
    }

    private IEnumerator CoEndPlayerTurnAndStartEnemyTurn()
    {
        if (isAttackPhaseRunning || isPhaseTransitioning || isBattleEnded)
            yield break;

        isAttackPhaseRunning = true;
        isPhaseTransitioning = true;
        currunt_Turn = ETurn.ATTACK;

        OnPlayerTurnEndedEvent?.Invoke();

        if (SplineTargetingArrow.Ins != null)
            SplineTargetingArrow.Ins.HideArrow();

        if (SkillConfirmModalUI.Ins != null)
            SkillConfirmModalUI.Ins.HideModal();

        // 남은 손패를 버린 카드 더미로 날려 보냄
        if (HandManager.Ins != null)
        {
            yield return StartCoroutine(HandManager.Ins.CoDiscardRemainingHandAtTurnEnd());
        }

        if (CombatEffectManager.Ins != null && CombatEffectManager.Ins.HasGroundStars)
        {
            yield return StartCoroutine(CombatEffectManager.Ins.CoCollectGroundStarsToCounter());
        }

        isAttackPhaseRunning = false;

        if (!isBattleEnded)
        {
            yield return new WaitForSeconds(0.2f);
            yield return StartCoroutine(CoExecuteEnemyTurn());
        }
    }

    // =========================================================================
    // 1. Player 턴 시작 연출, 코스트 충전(기본 3 + Arts 보너스), 5장 드로우
    // =========================================================================
    private IEnumerator CoStartPlayerTurn()
    {
        if (isBattleEnded) yield break;

        isPhaseTransitioning = true;
        isAttackPhaseRunning = false;
        isCardAttackAnimating = false;
        isDoubleStarGainActiveThisTurn = false;
        currunt_Turn = ETurn.PLAYER;
        currentTurnNumber++;

        EnsureSceneReferences();

        if (currentTurnNumber > 1)
        {
            IncrementAllTurnBuffs();
        }

        // 코스트 충전 (기본 3 + 이전 턴 Arts 카드로 추가된 코스트)
        if (BattleResourceUIManager.Ins != null)
        {
            BattleResourceUIManager.Ins.RefillCostForNewTurn();
        }

        selectedPlayer = null;
        selectedEnemy = null;

        for (int i = 0; i < playerUnits.Count; i++)
        {
            if (playerUnits[i] != null)
                playerUnits[i].SetSelected(false);
        }

        for (int i = 0; i < enemyUnits.Count; i++)
        {
            if (enemyUnits[i] != null)
                enemyUnits[i].SetSelected(false);
        }

        if (turnUIController != null)
        {
            turnUIController.CenterHoldDuration = turnBannerCenterHoldTime;
            yield return StartCoroutine(turnUIController.PlayTurnTransitionRoutine(ETurn.PLAYER, turnBannerCenterHoldTime));
        }

        // 턴 시작 시 모든 플레이어의 카드를 합친 덱에서 5장을 랜덤으로 손패로 드로우
        if (HandManager.Ins != null)
        {
            HandManager.Ins.DealRandomHandsForAllPlayers(playerUnits);
        }

        isPhaseTransitioning = false;
    }

    // =========================================================================
    // 2. Enemy 턴: 생존한 적들이 행동, 데미지 계산 및 패배 판정
    // =========================================================================
    private IEnumerator CoExecuteEnemyTurn()
    {
        if (isBattleEnded) yield break;

        isPhaseTransitioning = true;
        currunt_Turn = ETurn.ENEMY;

        for (int i = 0; i < playerUnits.Count; i++)
        {
            if (playerUnits[i] != null)
                playerUnits[i].SetSelected(false, selectedScaleMultiplier);
        }
        for (int i = 0; i < enemyUnits.Count; i++)
        {
            if (enemyUnits[i] != null)
                enemyUnits[i].SetSelected(false, selectedScaleMultiplier);
        }

        if (turnUIController != null)
        {
            turnUIController.CenterHoldDuration = turnBannerCenterHoldTime;
            yield return StartCoroutine(turnUIController.PlayTurnTransitionRoutine(ETurn.ENEMY, turnBannerCenterHoldTime));
        }

        List<SelectableUnit> actionQueue = BuildEnemyActionQueue();

        if (EnemyTurnTutorialHook != null && actionQueue.Count > 0)
        {
            yield return StartCoroutine(EnemyTurnTutorialHook(actionQueue[0]));
        }

        for (int i = 0; i < actionQueue.Count; i++)
        {
            SelectableUnit attackerEnemy = actionQueue[i];
            if (attackerEnemy == null || attackerEnemy.IsDead)
                continue;

            SelectableUnit randomTargetPlayer = GetRandomAlivePlayer();
            if (randomTargetPlayer == null)
                break;

            float enemyDamage = attackerEnemy.CalculateAttackDamage();
            float finalPlayerDamage = randomTargetPlayer.CalculateFinalTakenDamage(enemyDamage);

            bool spawnedPopupInEffect = false;
            if (CombatEffectManager.Ins != null)
            {
                spawnedPopupInEffect = true;
                yield return StartCoroutine(
                    CombatEffectManager.Ins.PlayPlayerHitSequenceOnUnit(randomTargetPlayer, finalPlayerDamage, false)
                );
            }
            else
            {
                yield return new WaitForSeconds(0.35f);
            }

            randomTargetPlayer.TakeDamage(enemyDamage, isCritical: false, spawnPopup: !spawnedPopupInEffect);

            if (AreAllPlayersDead())
            {
                isBattleEnded = true;
                yield return new WaitForSeconds(0.7f);

                if (BattleResultUIManager.Ins != null)
                    BattleResultUIManager.Ins.ShowDefeatUI();

                yield break;
            }

            if (enemyAttackInterval > 0f)
                yield return new WaitForSeconds(enemyAttackInterval);
        }

        if (!isBattleEnded)
        {
            yield return new WaitForSeconds(0.3f);
            yield return StartCoroutine(CoStartPlayerTurn());
        }
    }

    private void IncrementAllTurnBuffs()
    {
        for (int i = 0; i < playerUnits.Count; i++)
        {
            if (playerUnits[i] != null && !playerUnits[i].IsDead)
            {
                playerUnits[i].IncrementTurnBuff(1);
                playerUnits[i].AdvanceSkillTurnState();
            }
        }

        for (int i = 0; i < enemyUnits.Count; i++)
        {
            if (enemyUnits[i] != null && !enemyUnits[i].IsDead)
                enemyUnits[i].IncrementTurnBuff(1);
        }
    }

    private SelectableUnit GetFirstAlivePlayer()
    {
        for (int i = 0; i < playerUnits.Count; i++)
        {
            if (playerUnits[i] != null && !playerUnits[i].IsDead)
                return playerUnits[i];
        }
        return null;
    }

    private SelectableUnit GetFirstAliveEnemy()
    {
        for (int i = 0; i < enemyUnits.Count; i++)
        {
            if (enemyUnits[i] != null && !enemyUnits[i].IsDead)
                return enemyUnits[i];
        }
        return null;
    }

    private SelectableUnit GetRandomAlivePlayer()
    {
        List<SelectableUnit> alivePlayers = new List<SelectableUnit>();
        for (int i = 0; i < playerUnits.Count; i++)
        {
            if (playerUnits[i] != null && !playerUnits[i].IsDead)
                alivePlayers.Add(playerUnits[i]);
        }

        if (alivePlayers.Count == 0)
            return null;

        int randIdx = Random.Range(0, alivePlayers.Count);
        return alivePlayers[randIdx];
    }

    private bool AreAllEnemiesDead()
    {
        if (enemyUnits.Count == 0) return false;

        for (int i = 0; i < enemyUnits.Count; i++)
        {
            if (enemyUnits[i] != null && !enemyUnits[i].IsDead)
                return false;
        }
        return true;
    }

    private bool AreAllPlayersDead()
    {
        if (playerUnits.Count == 0) return false;

        for (int i = 0; i < playerUnits.Count; i++)
        {
            if (playerUnits[i] != null && !playerUnits[i].IsDead)
                return false;
        }
        return true;
    }

    private List<SelectableUnit> BuildEnemyActionQueue()
    {
        List<SelectableUnit> pool = new List<SelectableUnit>();

        for (int e = 0; e < enemyUnits.Count; e++)
        {
            SelectableUnit enemy = enemyUnits[e];
            if (enemy == null || enemy.IsDead) continue;

            for (int count = 0; count < maxActionsPerEnemy; count++)
            {
                pool.Add(enemy);
            }
        }

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int swapIdx = Random.Range(0, i + 1);
            SelectableUnit temp = pool[i];
            pool[i] = pool[swapIdx];
            pool[swapIdx] = temp;
        }

        if (pool.Count > totalEnemyActions)
        {
            pool.RemoveRange(totalEnemyActions, pool.Count - totalEnemyActions);
        }

        return pool;
    }

    public void EnsureSceneReferences()
    {
        if (playerRoot == null)
        {
            GameObject pObj = GameObject.Find("Player");
            if (pObj != null) playerRoot = pObj.transform;
        }

        if (enemyRoot == null)
        {
            GameObject eObj = GameObject.Find("Enemy");
            if (eObj != null) enemyRoot = eObj.transform;
        }

        if (playerRoot != null && playerUnits.Count == 0)
        {
            for (int i = 0; i < playerRoot.childCount; i++)
            {
                Transform child = playerRoot.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                if (child.GetComponent<SpriteRenderer>() == null) continue;

                SelectableUnit unit = child.GetComponent<SelectableUnit>();
                if (unit == null)
                    unit = child.gameObject.AddComponent<SelectableUnit>();

                unit.InitializeUnit(true, playerUnits.Count, selectedScaleMultiplier);
                playerUnits.Add(unit);
            }
        }
        else
        {
            playerUnits.RemoveAll(u => u == null || !u.gameObject.activeSelf);
            for (int i = 0; i < playerUnits.Count; i++)
            {
                if (playerUnits[i] != null)
                    playerUnits[i].InitializeUnit(true, i, selectedScaleMultiplier);
            }
        }

        if (enemyRoot != null && enemyUnits.Count == 0)
        {
            for (int i = 0; i < enemyRoot.childCount; i++)
            {
                Transform child = enemyRoot.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                if (child.GetComponent<SpriteRenderer>() == null) continue;

                SelectableUnit unit = child.GetComponent<SelectableUnit>();
                if (unit == null)
                    unit = child.gameObject.AddComponent<SelectableUnit>();

                unit.InitializeUnit(false, enemyUnits.Count, selectedScaleMultiplier);
                enemyUnits.Add(unit);
            }
        }
        else
        {
            enemyUnits.RemoveAll(u => u == null || !u.gameObject.activeSelf);
            for (int i = 0; i < enemyUnits.Count; i++)
            {
                if (enemyUnits[i] != null)
                    enemyUnits[i].InitializeUnit(false, i, selectedScaleMultiplier);
            }
        }

        if (turnUIController == null)
        {
            turnUIController = Object.FindFirstObjectByType<TurnUIController>();
            if (turnUIController == null)
            {
                turnUIController = gameObject.GetComponent<TurnUIController>();
                if (turnUIController == null)
                    turnUIController = gameObject.AddComponent<TurnUIController>();
            }
        }
    }
}
