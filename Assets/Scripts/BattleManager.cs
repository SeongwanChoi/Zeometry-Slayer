using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BattleManager : Singleton<BattleManager>
{
    [Header("현재 턴 상태")]
    [SerializeField] private ETurn currunt_Turn = ETurn.PLAYER;
    [SerializeField] private int currentTurnNumber = 0;

    [Header("1. 턴 UI 및 배너 중앙 유지 시간 (Inspector 조정 가능)")]
    [SerializeField] private TurnUIController turnUIController;
    [Tooltip("턴 시작 시 화면 가운데에 배너가 머무르는 일정 시간 (초) - Inspector에서 조정 가능")]
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
    [Tooltip("Attack 페이즈에서 카드 1장 소모 후 다음 카드 공격까지의 간격 (초)")]
    [SerializeField] private float attackStepInterval = 0.25f;
    [Tooltip("Enemy 턴에서 적의 각 공격(총 6회) 사이의 간격 (초)")]
    [SerializeField] private float enemyAttackInterval = 0.22f;
    [Tooltip("적 전체 행동 횟수 (3명 합쳐서 6번)")]
    [SerializeField] private int totalEnemyActions = 6;
    [Tooltip("적 1명당 최대 행동 횟수 (최대 2번)")]
    [SerializeField] private int maxActionsPerEnemy = 2;

    private SelectableUnit selectedPlayer;
    private SelectableUnit selectedEnemy;
    private bool isPhaseTransitioning = false;
    private bool isAttackPhaseRunning = false;
    private bool isBattleEnded = false;
    private Camera mainCamera;

    /// <summary>
    /// 플레이어 턴이고 공격/적 턴 진행 중이 아니며, ESC 일시정지 메뉴나 승패 결과창이 열려있지 않을 때 조작을 허용합니다.
    /// </summary>
    public bool IsPlayerInputAllowed =>
        !isBattleEnded &&
        (currunt_Turn == ETurn.PLAYER) &&
        !isAttackPhaseRunning &&
        (PauseMenuManager.Ins == null || !PauseMenuManager.Ins.IsPaused) &&
        (BattleResultUIManager.Ins == null || !BattleResultUIManager.Ins.IsResultShowing);

    public SelectableUnit SelectedPlayer => selectedPlayer;
    public SelectableUnit SelectedEnemy => selectedEnemy;
    public ETurn CurrentTurn => currunt_Turn;

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
        if (!IsPlayerInputAllowed) return;

        // 마우스 좌클릭으로 생존한 Player 오브젝트 또는 Enemy 오브젝트의 Collider2D 선택 감지
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySelectUnitUnderMouse(Mouse.current.position.ReadValue());
        }

        // 플레이어 턴 중에 AttackBox에 카드 6장이 모두 들어오면 즉시 Attack 페이즈로 전환
        if (!isAttackPhaseRunning && AttackBoxManager.Ins != null && AttackBoxManager.Ins.IsReadyToExecute)
        {
            CheckAutoEndPlayerTurn();
        }
    }

    private void EnTurn()
    {
        // 적 턴은 코루틴(CoExecuteEnemyTurn)으로 순차 진행됩니다.
    }

    private void TrySelectUnitUnderMouse(Vector2 screenPos)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null) return;

        float distToPlane = Mathf.Abs(mainCamera.transform.position.z);
        Vector3 worldPoint3D = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distToPlane));
        Vector2 worldPoint2D = new Vector2(worldPoint3D.x, worldPoint3D.y);

        Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint2D);
        if (hits == null || hits.Length == 0) return;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;

            SelectableUnit unit = hits[i].GetComponent<SelectableUnit>();
            if (unit == null)
                unit = hits[i].GetComponentInParent<SelectableUnit>();

            if (unit != null && !unit.IsDead)
            {
                OnUnitClicked(unit);
                return;
            }
        }
    }

    public void OnUnitClicked(SelectableUnit clickedUnit)
    {
        if (!IsPlayerInputAllowed || clickedUnit == null || clickedUnit.IsDead) return;

        if (clickedUnit.IsPlayerUnit)
        {
            SelectPlayerUnit(clickedUnit);
        }
        else
        {
            SelectEnemyUnit(clickedUnit);
        }
    }

    public void SelectPlayerUnit(SelectableUnit targetPlayer)
    {
        if (targetPlayer == null || targetPlayer.IsDead) return;

        selectedPlayer = targetPlayer;

        for (int i = 0; i < playerUnits.Count; i++)
        {
            if (playerUnits[i] != null)
            {
                bool isThisSelected = (!playerUnits[i].IsDead && playerUnits[i] == selectedPlayer);
                playerUnits[i].SetSelected(isThisSelected, selectedScaleMultiplier);
            }
        }

        if (HandManager.Ins != null)
        {
            HandManager.Ins.ShowPlayerHand(selectedPlayer.UnitIndex);
        }
    }

    public void SelectEnemyUnit(SelectableUnit targetEnemy)
    {
        if (targetEnemy == null || targetEnemy.IsDead) return;

        selectedEnemy = targetEnemy;

        for (int i = 0; i < enemyUnits.Count; i++)
        {
            if (enemyUnits[i] != null)
            {
                bool isThisSelected = (!enemyUnits[i].IsDead && enemyUnits[i] == selectedEnemy);
                enemyUnits[i].SetSelected(isThisSelected, selectedScaleMultiplier);
            }
        }

        CheckAutoEndPlayerTurn();
    }

    public void CheckAutoEndPlayerTurn()
    {
        if (isBattleEnded || currunt_Turn != ETurn.PLAYER || isAttackPhaseRunning)
            return;

        if (AttackBoxManager.Ins == null || !AttackBoxManager.Ins.IsReadyToExecute)
            return;

        EnsureSceneReferences();

        if (selectedEnemy == null || selectedEnemy.IsDead)
        {
            selectedEnemy = GetFirstAliveEnemy();
            if (selectedEnemy != null)
                selectedEnemy.SetSelected(true, selectedScaleMultiplier);
        }

        if (selectedEnemy != null)
        {
            StartCoroutine(CoExecuteAttackPhase());
        }
    }

    public void TurnEnd()
    {
        CheckAutoEndPlayerTurn();
    }

    // =========================================================================
    // 1. Player 턴 시작 연출, 버프 증가(+1) 및 손패 배분
    // =========================================================================
    private IEnumerator CoStartPlayerTurn()
    {
        if (isBattleEnded) yield break;

        isPhaseTransitioning = true;
        isAttackPhaseRunning = false;
        currunt_Turn = ETurn.PLAYER;
        currentTurnNumber++;

        EnsureSceneReferences();

        // 2번째 턴부터 매 턴마다 버프를 가진 유닛들의 버프 값을 +1 증가시킵니다.
        if (currentTurnNumber > 1)
        {
            IncrementAllTurnBuffs();
        }

        selectedPlayer = null;
        selectedEnemy = null;

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

        // 생존한 모든 플레이어에게 새 랜덤 손패(RED, GREEN, BLUE 중 랜덤 6장) 배분
        if (HandManager.Ins != null)
        {
            HandManager.Ins.DealRandomHandsForAllPlayers(playerUnits);
        }

        // 첫 번째 생존 플레이어와 첫 번째 생존 적을 기본 선택
        SelectableUnit firstPlayer = GetFirstAlivePlayer();
        if (firstPlayer != null)
        {
            SelectPlayerUnit(firstPlayer);
        }

        SelectableUnit firstEnemy = GetFirstAliveEnemy();
        if (firstEnemy != null)
        {
            selectedEnemy = firstEnemy;
            selectedEnemy.SetSelected(true, selectedScaleMultiplier);
        }

        if (turnUIController != null)
        {
            turnUIController.CenterHoldDuration = turnBannerCenterHoldTime;
            yield return StartCoroutine(turnUIController.PlayTurnTransitionRoutine(ETurn.PLAYER, turnBannerCenterHoldTime));
        }

        isPhaseTransitioning = false;
    }

    // =========================================================================
    // 2. Attack 페이즈: 왼쪽부터 카드 1장씩 소모 -> 해당 색의 공격 및 데미지 계산 수행
    // =========================================================================
    private IEnumerator CoExecuteAttackPhase()
    {
        if (isAttackPhaseRunning || isBattleEnded) yield break;

        isAttackPhaseRunning = true;
        isPhaseTransitioning = true;
        currunt_Turn = ETurn.ATTACK;

        EnsureSceneReferences();

        if (HandManager.Ins != null)
        {
            HandManager.Ins.SetHandVisibility(false);
        }

        if (turnUIController != null)
        {
            turnUIController.UpdateTopLeftStatus(ETurn.ATTACK);
        }

        // Attack 페이즈로 넘어갔을 때, 이전 턴에 획득해 둔 모든 스타를 AttackBox 안의 카드들에 랜덤으로 부여합니다.
        // (부여된 스타 1개당 해당 카드의 크리티컬 확률이 +10% 증가합니다.)
        if (BattleResourceUIManager.Ins != null && AttackBoxManager.Ins != null)
        {
            int starsToDistribute = BattleResourceUIManager.Ins.ConsumeAllStarsForAttackPhase();
            if (starsToDistribute > 0)
            {
                AttackBoxManager.Ins.DistributeStarsRandomly(starsToDistribute);
                yield return new WaitForSeconds(0.45f);
            }
            else
            {
                yield return new WaitForSeconds(0.35f);
            }
        }
        else
        {
            yield return new WaitForSeconds(0.35f);
        }

        while (AttackBoxManager.Ins != null &&
               AttackBoxManager.Ins.ConsumeLeftmostCard(out ECardColor cardColor, out SelectableUnit attackerPlayer, out int assignedStars))
        {
            if (attackerPlayer == null || attackerPlayer.IsDead)
            {
                attackerPlayer = (selectedPlayer != null && !selectedPlayer.IsDead)
                    ? selectedPlayer
                    : GetFirstAlivePlayer();
            }

            if (attackerPlayer == null)
                break;

            SelectableUnit targetEnemy = (selectedEnemy != null && !selectedEnemy.IsDead)
                ? selectedEnemy
                : GetFirstAliveEnemy();

            if (targetEnemy == null)
                break;

            if (selectedEnemy != targetEnemy)
            {
                selectedEnemy = targetEnemy;
                selectedEnemy.SetSelected(true, selectedScaleMultiplier);
            }

            // 1) 플레이어 공격 오라 + 적 참격/흔들림 연출 실행
            if (CombatEffectManager.Ins != null)
            {
                yield return StartCoroutine(
                    CombatEffectManager.Ins.PlayCardAttackSequence(attackerPlayer, cardColor, targetEnemy)
                );
            }
            else
            {
                yield return new WaitForSeconds(0.4f);
            }

            // 2) 기본 공격 데미지 계산 (공격력 * 버프)
            float damage = attackerPlayer.CalculateAttackDamage();

            // 3) 카드 종류(Buster / Arts / Quick)에 따른 배율 및 자원(코스트/스타) 획득 처리
            switch (cardColor)
            {
                case ECardColor.BUSTER: // 빨간색 Buster 카드: 공격 시 데미지에 * 1.5 연산
                    damage *= 1.5f;
                    break;

                case ECardColor.ARTS:   // 파란색 Arts 카드: 공격력만큼 데미지를 주며 코스트 1 획득
                    if (BattleResourceUIManager.Ins != null)
                    {
                        BattleResourceUIManager.Ins.AddCost(1);
                    }
                    break;

                case ECardColor.QUICK:  // 초록색 Quick 카드: 공격력만큼 데미지를 주며 스타 랜덤(1~10) 획득 (다음 턴에 사용됨)
                    if (BattleResourceUIManager.Ins != null)
                    {
                        int gainedStars = Random.Range(1, 11); // 1 ~ 10 랜덤 획득
                        BattleResourceUIManager.Ins.AddStars(gainedStars);
                    }
                    break;
            }

            // 4) 크리티컬 판정: 부여된 스타 1개당 크리티컬 확률 +10%, 크리티컬 발생 시 데미지 * 2 연산
            float critChance = Mathf.Clamp(assignedStars * 10f, 0f, 100f);
            bool isCritical = (critChance > 0f) && (Random.Range(0f, 100f) < critChance);
            if (isCritical)
            {
                damage *= 2f;
                Debug.Log($"[Attack] {cardColor.GetCardName()} 카드 크리티컬 히트! (확률 {critChance:0}% -> 데미지 x2 = {damage})");
            }

            // 5) 최종 데미지 적용 및 사망 판정
            bool enemyDied = targetEnemy.TakeDamage(damage);

            // 3) 모든 Enemy가 사망했는지 승리 판정 체크
            if (AreAllEnemiesDead())
            {
                isBattleEnded = true;
                if (AttackBoxManager.Ins != null)
                    AttackBoxManager.Ins.ClearAllQueuedCards();

                // 적 오브젝트가 서서히 옅어지며 사라지는 연출이 보이도록 잠시 대기 후 승리 UI 표시
                yield return new WaitForSeconds(0.7f);

                if (BattleResultUIManager.Ins != null)
                    BattleResultUIManager.Ins.ShowVictoryUI();

                yield break;
            }
            else if (enemyDied)
            {
                // 타겟 적이 사망했으나 다른 적이 남아있다면 다음 생존 적을 자동으로 타겟 지정
                selectedEnemy = GetFirstAliveEnemy();
                if (selectedEnemy != null)
                    selectedEnemy.SetSelected(true, selectedScaleMultiplier);
            }

            if (attackStepInterval > 0f)
                yield return new WaitForSeconds(attackStepInterval);
        }

        if (!isBattleEnded)
        {
            yield return new WaitForSeconds(0.3f);
            yield return StartCoroutine(CoExecuteEnemyTurn());
        }
    }

    // =========================================================================
    // 3. Enemy 턴: 생존한 적들이 행동(최대 2번씩, 총 최대 6번), 데미지(공격력 * 버프) 계산 및 패배 판정
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

        for (int i = 0; i < actionQueue.Count; i++)
        {
            SelectableUnit attackerEnemy = actionQueue[i];
            if (attackerEnemy == null || attackerEnemy.IsDead)
                continue;

            SelectableUnit randomTargetPlayer = GetRandomAlivePlayer();
            if (randomTargetPlayer == null)
                break;

            // 1) 피격받는 플레이어 흔들림 모션 재생
            if (CombatEffectManager.Ins != null)
            {
                yield return StartCoroutine(
                    CombatEffectManager.Ins.PlayPlayerHitSequenceOnUnit(randomTargetPlayer)
                );
            }
            else
            {
                yield return new WaitForSeconds(0.35f);
            }

            // 2) 적 데미지 계산 (10f~20f * 버프 수치) 및 플레이어 체력 차감
            float enemyDamage = attackerEnemy.CalculateAttackDamage();
            randomTargetPlayer.TakeDamage(enemyDamage);

            // 3) 모든 Player가 사망했는지 패배 판정 체크
            if (AreAllPlayersDead())
            {
                isBattleEnded = true;

                // 마지막 플레이어가 서서히 옅어지며 사라지는 연출이 끝날 때까지 대기 후 패배 UI 표시
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
                playerUnits[i].IncrementTurnBuff(1);
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
