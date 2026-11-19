using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public struct PlayerSkillMetadata
{
    public string skillName;
    public int cooldownTurns;
    public int requiredCost;
    public int requiredStars;
    public string description;
}

/// <summary>
/// Player_1~3 및 Enemy_1~3 오브젝트에 부착되어:
/// 1) 클릭 선택 및 선택 시 크기 확대 효과
/// 2) 개별 체력(HP), 공격력(Attack), 버프(Buff) 및 플레이어별 고유 5장 카드 패턴 / 고유 3종 스킬 관리
/// 3) 유닛 하단 체력바 & 버프 UI(UnitStatusUI) 및 유닛 상단 스킬 버튼 UI(PlayerSkillUI) 연동
/// 4) 체력 0 도달 시 사망 판정 및 서서히 옅어지며 사라지는(Fade-out) 연출을 담당합니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SelectableUnit : MonoBehaviour, IPointerClickHandler
{
    [Header("유닛 구분 및 인덱스")]
    [SerializeField] private bool isPlayerUnit = true;
    [SerializeField] private int unitIndex = 0;

    [Header("전투 능력치 (각 유닛별로 개별 적용 및 Inspector에서 변경 가능)")]
    [SerializeField] private bool statsInitialized = false;
    [SerializeField] private float maxHp = 250f;
    [SerializeField] private float currentHp = 250f;
    [SerializeField] private float minAttack = 5f;
    [SerializeField] private float maxAttack = 5f;
    [SerializeField] private int buffCount = 0;
    [SerializeField] private bool incrementBuffEveryTurn = false;

    [Header("플레이어 스킬 버프 중첩 (1스택 = 10%) 및 각 스킬 남은 쿨타임")]
    [SerializeField] private int attackUpStacks = 0;
    [SerializeField] private int defenseUpStacks = 0;
    [SerializeField] private int critDamageUpStacks = 0;
    [SerializeField] private int attackUpCooldown = 0;
    [SerializeField] private int defenseUpCooldown = 0;
    [SerializeField] private int critDamageUpCooldown = 0;

    [Header("선택 상태 및 사망 연출 설정")]
    [Tooltip("선택된 상태일 때 오브젝트의 크기가 커지는 배율")]
    [SerializeField] private float selectedScaleMultiplier = 1.22f;
    [Tooltip("체력이 0이 되어 사망할 때 서서히 옅어지며 사라지는 시간 (초)")]
    [SerializeField] private float deathFadeDuration = 0.65f;

    [Header("유닛 하단 체력바 & 버프 UI / 상단 스킬 버튼 UI 참조")]
    [SerializeField] private UnitStatusUI statusUI;
    [SerializeField] private PlayerSkillUI skillUI;

    /// <summary>
    /// 플레이어 유닛이 스킬을 발동했을 때 호출되는 이벤트 (튜토리얼 및 사운드 연동용)
    /// </summary>
    public static event System.Action<SelectableUnit, ESkillType> OnPlayerSkillUsedEvent;

    private SpriteRenderer spriteRenderer;
    private Collider2D unitCollider;

    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private Color baseColor = Color.white;
    private bool isCached = false;
    private bool isSelected = false;
    private bool isDead = false;
    private bool isFadingOut = false;

    public bool IsPlayerUnit => isPlayerUnit;
    public int UnitIndex => unitIndex;
    public bool IsSelected => isSelected;
    public bool IsDead => isDead || currentHp <= 0f;

    public int AttackUpStacks => attackUpStacks;
    public int DefenseUpStacks => defenseUpStacks;
    public int CritDamageUpStacks => critDamageUpStacks;

    public int AttackUpCooldown => attackUpCooldown;
    public int DefenseUpCooldown => defenseUpCooldown;
    public int CritDamageUpCooldown => critDamageUpCooldown;

    public float MaxHp
    {
        get => maxHp;
        set
        {
            maxHp = Mathf.Max(1f, value);
            currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
            RefreshStatusUI();
        }
    }

    public float CurrentHp
    {
        get => currentHp;
        set
        {
            currentHp = Mathf.Clamp(value, 0f, maxHp);
            RefreshStatusUI();
        }
    }

    public float MinAttack
    {
        get => minAttack;
        set => minAttack = Mathf.Max(0f, value);
    }

    public float MaxAttack
    {
        get => maxAttack;
        set => maxAttack = Mathf.Max(minAttack, value);
    }

    public int BuffCount
    {
        get => buffCount;
        set
        {
            buffCount = Mathf.Max(0, value);
            RefreshStatusUI();
        }
    }

    public bool IncrementBuffEveryTurn
    {
        get => incrementBuffEveryTurn;
        set => incrementBuffEveryTurn = value;
    }

    public SpriteRenderer SpriteRenderer
    {
        get
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            return spriteRenderer;
        }
    }

    public Sprite UnitPortraitSprite => SpriteRenderer != null ? SpriteRenderer.sprite : null;

    public Vector3 BaseLocalPosition
    {
        get
        {
            CacheDefaultsIfNeeded();
            return baseLocalPosition;
        }
    }

    public Vector3 WorldBasePosition
    {
        get
        {
            if (!Application.isPlaying)
                return transform.position;

            CacheDefaultsIfNeeded();
            if (transform.parent != null)
                return transform.parent.TransformPoint(baseLocalPosition);
            return baseLocalPosition;
        }
    }

    public Vector3 CurrentTargetScale
    {
        get
        {
            CacheDefaultsIfNeeded();
            return baseLocalScale;
        }
    }

    public Color BaseColor
    {
        get
        {
            CacheDefaultsIfNeeded();
            return baseColor;
        }
    }

    public UnitStatusUI StatusUI => statusUI;
    public PlayerSkillUI SkillUI => skillUI;

    private void Awake()
    {
        CacheDefaultsIfNeeded();
        EnsureStatsConfigured();
    }

    private void Start()
    {
        EnsureStatusUI();
        EnsureSkillUI();
        RefreshStatusUI();
        RefreshSkillUI();
    }

    public void InitializeUnit(bool isPlayer, int index, float scaleMultiplier = 1.22f)
    {
        CacheDefaultsIfNeeded();
        isPlayerUnit = isPlayer;
        unitIndex = index;
        if (scaleMultiplier > 1f)
            selectedScaleMultiplier = scaleMultiplier;

        EnsureStatsConfigured();
        EnsureStatusUI();
        EnsureSkillUI();
        RefreshStatusUI();
        RefreshSkillUI();
    }

    public void EnsureStatsConfigured(bool forceResetToDefault = false)
    {
        if (statsInitialized && !forceResetToDefault && maxHp > 0f)
        {
            if (currentHp <= 0f && !isDead)
                currentHp = maxHp;
            return;
        }

        if (isPlayerUnit || gameObject.name.StartsWith("Player"))
        {
            isPlayerUnit = true;
            maxHp = 250f;
            currentHp = 250f;
            minAttack = 5f;
            maxAttack = 5f;
            buffCount = 0;
            incrementBuffEveryTurn = false;
        }
        else
        {
            isPlayerUnit = false;
            minAttack = 10f;
            maxAttack = 20f;
            incrementBuffEveryTurn = true;

            if (gameObject.name.Contains("1") || unitIndex == 0)
            {
                maxHp = 50f;
                currentHp = 50f;
                buffCount = 5;
            }
            else if (gameObject.name.Contains("2") || unitIndex == 1)
            {
                maxHp = 100f;
                currentHp = 100f;
                buffCount = 2;
            }
            else
            {
                maxHp = 500f;
                currentHp = 500f;
                buffCount = 1;
            }
        }

        attackUpStacks = 0;
        defenseUpStacks = 0;
        critDamageUpStacks = 0;
        attackUpCooldown = 0;
        defenseUpCooldown = 0;
        critDamageUpCooldown = 0;

        statsInitialized = true;
        isDead = false;
        isFadingOut = false;
    }

    public void CacheDefaultsIfNeeded()
    {
        if (isCached) return;

        spriteRenderer = GetComponent<SpriteRenderer>();
        unitCollider = GetComponent<Collider2D>();
        if (unitCollider == null)
            unitCollider = gameObject.AddComponent<BoxCollider2D>();

        if (unitCollider is BoxCollider2D boxCol && boxCol.size.x > 7.2f)
        {
            boxCol.size = new Vector2(6.6f, 6.6f);
        }

        baseLocalPosition = transform.localPosition;
        baseLocalScale = transform.localScale;
        if (spriteRenderer != null)
            baseColor = spriteRenderer.color;

        isCached = true;
    }

    // =========================================================================
    // 1. 각 플레이어 고유 5장 카드 패턴 반환 (PDF 1~4페이지 명세)
    // - Player_1 (파란 삼각형): Quick 1장, Buster 1장, Arts 3장
    // - Player_2 (초록 팔각형): Quick 3장, Buster 1장, Arts 1장
    // - Player_3 (빨간 오각형): Quick 1장, Buster 3장, Arts 1장
    // =========================================================================
    public List<ECardColor> GetOwnedCardPattern()
    {
        int resolvedIdx = ResolvePlayerTypeIndex();
        switch (resolvedIdx)
        {
            case 0: // Player_1: 퀵 1장, 버스터 1장, 아츠 3장
                return new List<ECardColor>
                {
                    ECardColor.QUICK,
                    ECardColor.BUSTER,
                    ECardColor.ARTS,
                    ECardColor.ARTS,
                    ECardColor.ARTS
                };

            case 1: // Player_2: 퀵 3장, 버스터 1장, 아츠 1장
                return new List<ECardColor>
                {
                    ECardColor.QUICK,
                    ECardColor.QUICK,
                    ECardColor.QUICK,
                    ECardColor.BUSTER,
                    ECardColor.ARTS
                };

            case 2: // Player_3: 퀵 1장, 버스터 3장, 아츠 1장
            default:
                return new List<ECardColor>
                {
                    ECardColor.QUICK,
                    ECardColor.BUSTER,
                    ECardColor.BUSTER,
                    ECardColor.BUSTER,
                    ECardColor.ARTS
                };
        }
    }

    public int ResolvePlayerTypeIndex()
    {
        if (gameObject.name.Contains("1")) return 0;
        if (gameObject.name.Contains("2")) return 1;
        if (gameObject.name.Contains("3")) return 2;
        return Mathf.Clamp(unitIndex, 0, 2);
    }

    // =========================================================================
    // 2. 각 플레이어 고유 스킬 3종 명세 및 발동 (PDF 3~4페이지 명세)
    // =========================================================================
    public PlayerSkillMetadata GetSkillMetadata(ESkillType skillType)
    {
        int pType = ResolvePlayerTypeIndex();

        if (pType == 0) // Player_1 (파란 삼각형)
        {
            switch (skillType)
            {
                case ESkillType.SKILL_1:
                    return new PlayerSkillMetadata
                    {
                        skillName = "전술 지휘 A+",
                        cooldownTurns = 7,
                        requiredCost = 0,
                        requiredStars = 0,
                        description = "이번 턴 동안 손패의 Quick && Buster 카드들의 필요 코스트를 0으로 만든다."
                    };
                case ESkillType.SKILL_2:
                    return new PlayerSkillMetadata
                    {
                        skillName = "마력 충전 A",
                        cooldownTurns = 6,
                        requiredCost = 0,
                        requiredStars = 0,
                        description = "즉시 코스트를 2 획득한다."
                    };
                default:
                    return new PlayerSkillMetadata
                    {
                        skillName = "전술 재편성 EX",
                        cooldownTurns = 6,
                        requiredCost = 0,
                        requiredStars = 0,
                        description = "손패를 모두 버리고 5장을 새로 드로우한다."
                    };
            }
        }
        else if (pType == 1) // Player_2 (초록 팔각형)
        {
            switch (skillType)
            {
                case ESkillType.SKILL_1:
                    return new PlayerSkillMetadata
                    {
                        skillName = "별의 개척자 EX",
                        cooldownTurns = 6,
                        requiredCost = 0,
                        requiredStars = 0,
                        description = "즉시 스타를 50 획득한다."
                    };
                case ESkillType.SKILL_2:
                    return new PlayerSkillMetadata
                    {
                        skillName = "스타 오버드라이브 A",
                        cooldownTurns = 6,
                        requiredCost = 0,
                        requiredStars = 0,
                        description = "이번 턴 동안 스타 획득량을 2배로 올린다."
                    };
                default:
                    return new PlayerSkillMetadata
                    {
                        skillName = "별빛의 수호장벽 B+",
                        cooldownTurns = 3,
                        requiredCost = 0,
                        requiredStars = 20,
                        description = "스타를 20개 삭제하여 아군 전체에 방어력을 50% 증가시킨다."
                    };
            }
        }
        else // Player_3 (빨간 오각형)
        {
            switch (skillType)
            {
                case ESkillType.SKILL_1:
                    return new PlayerSkillMetadata
                    {
                        skillName = "전투 속행 (공격력 UP)",
                        cooldownTurns = 5,
                        requiredCost = 0,
                        requiredStars = 0,
                        description = "이 캐릭터의 공격력을 10% 증가시킨다."
                    };
                case ESkillType.SKILL_2:
                    return new PlayerSkillMetadata
                    {
                        skillName = "크림슨 버스트 A+",
                        cooldownTurns = 1,
                        requiredCost = 1,
                        requiredStars = 0,
                        description = "코스트를 1 소비하여 이 캐릭터의 공격력을 30% 증가시킨다."
                    };
                default:
                    return new PlayerSkillMetadata
                    {
                        skillName = "슈퍼 Buster 연성 EX",
                        cooldownTurns = 3,
                        requiredCost = 0,
                        requiredStars = 0,
                        description = "이번 턴 슈퍼Buster카드를 생성한다.\n(손패에 잡힌 Buster카드를 흡수하여 그 공격력만큼 누적시킨다)"
                    };
            }
        }
    }

    public bool CanPaySkillResource(ESkillType skillType, out string lackReason)
    {
        lackReason = null;
        PlayerSkillMetadata meta = GetSkillMetadata(skillType);

        if (meta.requiredCost > 0)
        {
            int curCost = BattleResourceUIManager.Ins != null ? BattleResourceUIManager.Ins.CurrentCost : 0;
            if (curCost < meta.requiredCost)
            {
                lackReason = $"코스트가 부족합니다 (필요 코스트: {meta.requiredCost})";
                return false;
            }
        }

        if (meta.requiredStars > 0)
        {
            int curStars = BattleResourceUIManager.Ins != null ? BattleResourceUIManager.Ins.GetTotalAvailableStarsIncludingHand() : 0;
            if (curStars < meta.requiredStars)
            {
                lackReason = $"스타가 부족합니다 (필요 스타: {meta.requiredStars}개)";
                return false;
            }
        }

        return true;
    }

    public int GetSkillBuffStacks(ESkillType skillType)
    {
        switch (skillType)
        {
            case ESkillType.SKILL_1: return Mathf.Max(0, attackUpStacks);
            case ESkillType.SKILL_2: return Mathf.Max(0, defenseUpStacks);
            case ESkillType.SKILL_3: return Mathf.Max(0, critDamageUpStacks);
            default:                 return 0;
        }
    }

    public int GetSkillCooldown(ESkillType skillType)
    {
        switch (skillType)
        {
            case ESkillType.SKILL_1: return Mathf.Max(0, attackUpCooldown);
            case ESkillType.SKILL_2: return Mathf.Max(0, defenseUpCooldown);
            case ESkillType.SKILL_3: return Mathf.Max(0, critDamageUpCooldown);
            default:                 return 0;
        }
    }

    public void SetSkillCooldown(ESkillType skillType, int turns)
    {
        int clamped = Mathf.Max(0, turns);
        switch (skillType)
        {
            case ESkillType.SKILL_1: attackUpCooldown = clamped; break;
            case ESkillType.SKILL_2: defenseUpCooldown = clamped; break;
            case ESkillType.SKILL_3: critDamageUpCooldown = clamped; break;
        }
    }

    public bool IsSkillOnCooldown(ESkillType skillType)
    {
        return GetSkillCooldown(skillType) > 0;
    }

    public void AddDefenseBuffStacks(int stacks)
    {
        if (IsDead) return;
        defenseUpStacks = Mathf.Max(0, defenseUpStacks + stacks);
        RefreshStatusUI();
    }

    /// <summary>
    /// 스킬 확인 모달에서 [확인]을 눌렀을 때 실제 스킬 효과를 발동하고 쿨타임을 적용합니다.
    /// </summary>
    public bool TryUseSkill(ESkillType skillType)
    {
        if (!isPlayerUnit || IsDead) return false;
        if (BattleManager.Ins != null && !BattleManager.Ins.IsPlayerInputAllowed) return false;

        if (IsSkillOnCooldown(skillType))
        {
            if (SoundManager.Ins != null)
                SoundManager.Ins.PlaySkillBlocked();
            return false;
        }

        if (!CanPaySkillResource(skillType, out _))
        {
            if (SoundManager.Ins != null)
                SoundManager.Ins.PlaySkillBlocked();
            return false;
        }

        PlayerSkillMetadata meta = GetSkillMetadata(skillType);

        // 필요 코스트 차감
        if (meta.requiredCost > 0 && BattleResourceUIManager.Ins != null)
        {
            if (!BattleResourceUIManager.Ins.TrySpendCost(meta.requiredCost))
                return false;
        }

        // 필요 스타 차감
        if (meta.requiredStars > 0 && BattleResourceUIManager.Ins != null)
        {
            if (!BattleResourceUIManager.Ins.TrySpendStarsIncludingHand(meta.requiredStars))
                return false;
        }

        int pType = ResolvePlayerTypeIndex();

        if (pType == 0) // Player_1
        {
            switch (skillType)
            {
                case ESkillType.SKILL_1:
                    // 이번 턴 동안 손패의 Quick && Buster 카드들의 필요 코스트를 0으로 만든다
                    if (HandManager.Ins != null)
                        HandManager.Ins.ActivateZeroCostQuickBusterForTurn();
                    break;

                case ESkillType.SKILL_2:
                    // 코스트를 2 획득한다
                    if (BattleResourceUIManager.Ins != null)
                        BattleResourceUIManager.Ins.AddCost(2);
                    break;

                case ESkillType.SKILL_3:
                    // 손패를 모두 버리고 5장을 새로 드로우한다
                    if (HandManager.Ins != null)
                        HandManager.Ins.DiscardHandAndRedraw(5);
                    break;
            }
        }
        else if (pType == 1) // Player_2
        {
            switch (skillType)
            {
                case ESkillType.SKILL_1:
                    // 즉시 스타를 50 획득한다
                    if (BattleManager.Ins != null)
                        BattleManager.Ins.GainStarsImmediate(50);
                    else if (BattleResourceUIManager.Ins != null)
                        BattleResourceUIManager.Ins.AddStars(50);
                    break;

                case ESkillType.SKILL_2:
                    // 이번 턴 스타 획득량을 2배로 올린다
                    critDamageUpStacks++;
                    if (BattleManager.Ins != null)
                        BattleManager.Ins.ActivateDoubleStarGainForTurn();
                    break;

                case ESkillType.SKILL_3:
                    // 스타를 20개 삭제하여 아군 전체에 방어력을 50% 증가시킨다 (5스택 = 50%)
                    if (BattleManager.Ins != null)
                        BattleManager.Ins.ApplyPartyDefenseBuffStacks(5);
                    else
                        AddDefenseBuffStacks(5);
                    break;
            }
        }
        else // Player_3
        {
            switch (skillType)
            {
                case ESkillType.SKILL_1:
                    // 공격력을 10% 증가시킨다 (1스택 = 10%)
                    attackUpStacks += 1;
                    if (HandManager.Ins != null)
                        HandManager.Ins.RefreshSuperBusterCardDisplay();
                    break;

                case ESkillType.SKILL_2:
                    // 코스트를 1 소비하여 이 캐릭터의 공격력을 30% 증가시킨다 (3스택 = 30%)
                    attackUpStacks += 3;
                    if (HandManager.Ins != null)
                        HandManager.Ins.RefreshSuperBusterCardDisplay();
                    break;

                case ESkillType.SKILL_3:
                    // 이번 턴 슈퍼Buster카드를 생성한다
                    if (HandManager.Ins != null)
                        HandManager.Ins.CreateSuperBusterCard(this);
                    break;
            }
        }

        // 스킬 사용 직후 해당 스킬의 쿨타임 적용
        SetSkillCooldown(skillType, meta.cooldownTurns);

        RefreshStatusUI();
        RefreshSkillUI();
        OnPlayerSkillUsedEvent?.Invoke(this, skillType);
        return true;
    }

    /// <summary>
    /// 다음 플레이어 턴이 시작될 때 호출됩니다.
    /// - 진행 중인 스킬들의 쿨타임을 1턴씩 감소시킵니다.
    /// - 1턴 지속 버프들을 갱신합니다.
    /// </summary>
    public void AdvanceSkillTurnState()
    {
        if (!isPlayerUnit || IsDead) return;

        if (attackUpCooldown > 0) attackUpCooldown--;
        if (defenseUpCooldown > 0) defenseUpCooldown--;
        if (critDamageUpCooldown > 0) critDamageUpCooldown--;

        // 방어력 버프 및 스타 2배 버프는 1턴(내 턴 + 적 턴) 유지 후 해제
        if (defenseUpStacks > 0) defenseUpStacks = 0;
        if (critDamageUpStacks > 0) critDamageUpStacks = 0;
        if (attackUpStacks > 0) attackUpStacks = 0;

        RefreshStatusUI();
        RefreshSkillUI();
    }

    /// <summary>
    /// 기본 공격 데미지 계산 (공격력 * 버프 * 공격력 증가 스킬 배율)
    /// </summary>
    public float CalculateAttackDamage()
    {
        float baseAtk = Mathf.Approximately(minAttack, maxAttack)
            ? minAttack
            : Mathf.Round(Random.Range(minAttack, maxAttack));

        if (buffCount > 0)
        {
            baseAtk *= buffCount;
        }

        if (isPlayerUnit && attackUpStacks > 0)
        {
            baseAtk *= (1f + 0.10f * attackUpStacks);
        }

        return baseAtk;
    }

    public float GetCriticalDamageMultiplier()
    {
        return 2f;
    }

    public void IncrementTurnBuff(int amount = 1)
    {
        if (IsDead) return;

        if (incrementBuffEveryTurn || buffCount > 0)
        {
            buffCount += amount;
            RefreshStatusUI();
        }
    }

    /// <summary>
    /// 방어력 업 스킬 버프(1스택당 10%, 5스택 = 50%)를 반영하여 최종 피해량을 계산합니다.
    /// </summary>
    public float CalculateFinalTakenDamage(float rawDamage)
    {
        float finalDamage = Mathf.Max(0f, rawDamage);
        if (isPlayerUnit && defenseUpStacks > 0)
        {
            float defenseReduction = Mathf.Clamp(1f - 0.10f * defenseUpStacks, 0.1f, 1f);
            finalDamage *= defenseReduction;
        }
        return finalDamage;
    }

    public bool TakeDamage(float damage, bool isCritical = false, bool spawnPopup = true)
    {
        if (IsDead) return true;

        float finalDamage = CalculateFinalTakenDamage(damage);

        if (spawnPopup && CombatEffectManager.Ins != null)
        {
            CombatEffectManager.Ins.SpawnDamagePopup(this, finalDamage, isCritical);
        }

        currentHp = Mathf.Max(0f, currentHp - finalDamage);
        RefreshStatusUI();

        if (currentHp <= 0f)
        {
            Die();
            return true;
        }

        return false;
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        isSelected = false;

        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayUnitDeath();

        if (unitCollider == null)
            unitCollider = GetComponent<Collider2D>();
        if (unitCollider != null)
            unitCollider.enabled = false;

        if (gameObject.activeInHierarchy && !isFadingOut)
        {
            StartCoroutine(CoFadeOutAndDie());
        }
    }

    private IEnumerator CoFadeOutAndDie()
    {
        isFadingOut = true;

        SpriteRenderer sr = SpriteRenderer;
        Color startColor = sr != null ? sr.color : Color.white;
        float duration = Mathf.Max(0.15f, deathFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - t;

            if (sr != null)
            {
                Color c = startColor;
                c.a = alpha;
                sr.color = c;
            }

            if (statusUI != null)
                statusUI.SetAlpha(alpha);

            if (skillUI != null)
                skillUI.SetAlpha(alpha);

            yield return null;
        }

        if (sr != null)
        {
            Color c = startColor;
            c.a = 0f;
            sr.color = c;
        }

        if (statusUI != null)
        {
            statusUI.SetAlpha(0f);
            statusUI.gameObject.SetActive(false);
        }

        if (skillUI != null)
        {
            skillUI.SetAlpha(0f);
            skillUI.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);
    }

    public void EnsureStatusUI(
        Sprite customFeatherSprite = null,
        Sprite atkSkillSprite = null,
        Sprite defSkillSprite = null,
        Sprite critSkillSprite = null)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        Transform uiRoot = canvas.transform.Find("UnitStatusUIRoot");
        if (uiRoot == null)
        {
            GameObject rootObj = new GameObject("UnitStatusUIRoot", typeof(RectTransform));
            rootObj.layer = LayerMask.NameToLayer("UI");
            rootObj.transform.SetParent(canvas.transform, false);
            rootObj.transform.SetAsFirstSibling();

            RectTransform rt = rootObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localPosition = Vector3.zero;

            uiRoot = rootObj.transform;
        }

        string uiName = $"StatusUI_{gameObject.name}";
        if (statusUI == null)
        {
            Transform existing = uiRoot.Find(uiName);
            if (existing != null)
                statusUI = existing.GetComponent<UnitStatusUI>();
        }

        if (statusUI == null)
        {
            GameObject uiObj = new GameObject(uiName, typeof(RectTransform), typeof(CanvasGroup), typeof(UnitStatusUI));
            uiObj.layer = LayerMask.NameToLayer("UI");
            uiObj.transform.SetParent(uiRoot, false);
            statusUI = uiObj.GetComponent<UnitStatusUI>();
        }

        statusUI.Initialize(this, customFeatherSprite, atkSkillSprite, defSkillSprite, critSkillSprite);
    }

    public void EnsureSkillUI(
        Sprite atkSkillSprite = null,
        Sprite defSkillSprite = null,
        Sprite critSkillSprite = null)
    {
        if (!isPlayerUnit && !gameObject.name.StartsWith("Player"))
            return;

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        Transform skillRoot = canvas.transform.Find("PlayerSkillUIRoot");
        if (skillRoot == null)
        {
            GameObject rootObj = new GameObject("PlayerSkillUIRoot", typeof(RectTransform));
            rootObj.layer = LayerMask.NameToLayer("UI");
            rootObj.transform.SetParent(canvas.transform, false);

            Transform statusRoot = canvas.transform.Find("UnitStatusUIRoot");
            if (statusRoot != null)
                rootObj.transform.SetSiblingIndex(statusRoot.GetSiblingIndex() + 1);
            else
                rootObj.transform.SetAsFirstSibling();

            RectTransform rt = rootObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localPosition = Vector3.zero;

            skillRoot = rootObj.transform;
        }

        string skillUiName = $"SkillUI_{gameObject.name}";
        if (skillUI == null)
        {
            Transform existing = skillRoot.Find(skillUiName);
            if (existing != null)
                skillUI = existing.GetComponent<PlayerSkillUI>();
        }

        if (skillUI == null)
        {
            GameObject uiObj = new GameObject(skillUiName, typeof(RectTransform), typeof(CanvasGroup), typeof(PlayerSkillUI));
            uiObj.layer = LayerMask.NameToLayer("UI");
            uiObj.transform.SetParent(skillRoot, false);
            skillUI = uiObj.GetComponent<PlayerSkillUI>();
        }

        skillUI.Initialize(this, atkSkillSprite, defSkillSprite, critSkillSprite);
    }

    public void RefreshStatusUI()
    {
        if (statusUI != null)
        {
            statusUI.RefreshStatus(currentHp, maxHp, buffCount);
        }
    }

    public void RefreshSkillUI()
    {
        if (skillUI != null)
        {
            skillUI.RefreshSkillButtons();
        }
    }

    public void SetSelected(bool selected, float customScaleMultiplier = -1f)
    {
        CacheDefaultsIfNeeded();
        isSelected = false;
        transform.localScale = baseLocalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 플레이어 및 Enemy 오브젝트 클릭 선택 기능 삭제됨
    }

    private void OnValidate()
    {
        maxHp = Mathf.Max(1f, maxHp);
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
        minAttack = Mathf.Max(0f, minAttack);
        maxAttack = Mathf.Max(minAttack, maxAttack);
        buffCount = Mathf.Max(0, buffCount);

        attackUpStacks = Mathf.Max(0, attackUpStacks);
        defenseUpStacks = Mathf.Max(0, defenseUpStacks);
        critDamageUpStacks = Mathf.Max(0, critDamageUpStacks);
        attackUpCooldown = Mathf.Max(0, attackUpCooldown);
        defenseUpCooldown = Mathf.Max(0, defenseUpCooldown);
        critDamageUpCooldown = Mathf.Max(0, critDamageUpCooldown);

        if (Application.isPlaying)
        {
            RefreshStatusUI();
            RefreshSkillUI();
        }
    }
}
