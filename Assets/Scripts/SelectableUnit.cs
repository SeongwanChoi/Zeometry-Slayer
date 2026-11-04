using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Player_1~3 및 Enemy_1~3 오브젝트에 부착되어:
/// 1) 클릭 선택 및 선택 시 크기 확대 효과
/// 2) 개별 체력(HP), 공격력(Attack), 버프(Buff) 수치 관리 및 데미지 계산
/// 3) 유닛 하단 체력바 & 깃털 버프 UI 연동
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

    [Header("선택 상태 및 사망 연출 설정")]
    [Tooltip("선택된 상태일 때 오브젝트의 크기가 커지는 배율")]
    [SerializeField] private float selectedScaleMultiplier = 1.22f;
    [Tooltip("체력이 0이 되어 사망할 때 서서히 옅어지며 사라지는 시간 (초)")]
    [SerializeField] private float deathFadeDuration = 0.65f;

    [Header("유닛 하단 체력바 & 버프 UI 참조")]
    [SerializeField] private UnitStatusUI statusUI;

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

    public Vector3 BaseLocalPosition
    {
        get
        {
            CacheDefaultsIfNeeded();
            return baseLocalPosition;
        }
    }

    /// <summary>
    /// 흔들림(Shake) 중에도 체력바가 제자리에 고정되도록 기준 월드 좌표를 반환합니다.
    /// (에디터 비실행 상태에서는 현재 오브젝트의 월드 위치를 즉시 반환합니다.)
    /// </summary>
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
            return isSelected ? baseLocalScale * selectedScaleMultiplier : baseLocalScale;
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

    private void Awake()
    {
        CacheDefaultsIfNeeded();
        EnsureStatsConfigured();
    }

    private void Start()
    {
        EnsureStatusUI();
        RefreshStatusUI();
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
        RefreshStatusUI();
    }

    /// <summary>
    /// 아직 초기 능력치가 세팅되지 않은 경우 규칙에 맞는 기본 능력치를 설정합니다.
    /// (이미 Inspector에서 개별 수정한 경우에는 덮어쓰지 않고 유지합니다.)
    /// - Player_1~3 : 체력 250f, 공격력 5f, 버프 0
    /// - Enemy_1    : 체력 50f,  공격력 10f~20f, 버프 5
    /// - Enemy_2    : 체력 100f, 공격력 10f~20f, 버프 2
    /// - Enemy_3    : 체력 500f, 공격력 10f~20f, 버프 1
    /// </summary>
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

    /// <summary>
    /// 공격 데미지 계산:
    /// 기본 공격력(minAttack ~ maxAttack)을 구한 뒤, 버프가 있을 경우 공격력에 버프 값을 곱(*)하여 반환합니다.
    /// </summary>
    public float CalculateAttackDamage()
    {
        float baseAtk = Mathf.Approximately(minAttack, maxAttack)
            ? minAttack
            : Mathf.Round(Random.Range(minAttack, maxAttack));

        // 데미지를 계산하기 전에 공격력에 버프 * 연산을 실행한다
        if (buffCount > 0)
        {
            return baseAtk * buffCount;
        }

        return baseAtk;
    }

    /// <summary>
    /// 매 턴마다 버프 값을 +1 증가시킵니다.
    /// </summary>
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
    /// 데미지를 적용하고 체력바 UI를 갱신합니다.
    /// 체력이 0이 되면 사망 처리 및 서서히 옅어지며 사라지는 연출을 시작합니다.
    /// </summary>
    public bool TakeDamage(float damage)
    {
        if (IsDead) return true;

        currentHp = Mathf.Max(0f, currentHp - Mathf.Max(0f, damage));
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

        if (unitCollider == null)
            unitCollider = GetComponent<Collider2D>();
        if (unitCollider != null)
            unitCollider.enabled = false;

        if (gameObject.activeInHierarchy && !isFadingOut)
        {
            StartCoroutine(CoFadeOutAndDie());
        }
    }

    /// <summary>
    /// 사망 시 오브젝트와 하단 체력바가 서서히 옅어지며 사라지는 코루틴
    /// </summary>
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
            {
                statusUI.SetAlpha(alpha);
            }

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

        gameObject.SetActive(false);
    }

    public void EnsureStatusUI(Sprite customFeatherSprite = null)
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
            {
                statusUI = existing.GetComponent<UnitStatusUI>();
            }
        }

        if (statusUI == null)
        {
            GameObject uiObj = new GameObject(uiName, typeof(RectTransform), typeof(CanvasGroup), typeof(UnitStatusUI));
            uiObj.layer = LayerMask.NameToLayer("UI");
            uiObj.transform.SetParent(uiRoot, false);
            statusUI = uiObj.GetComponent<UnitStatusUI>();
        }

        statusUI.Initialize(this, customFeatherSprite);
    }

    public void RefreshStatusUI()
    {
        if (statusUI != null)
        {
            statusUI.RefreshStatus(currentHp, maxHp, buffCount);
        }
    }

    public void SetSelected(bool selected, float customScaleMultiplier = -1f)
    {
        CacheDefaultsIfNeeded();
        if (IsDead)
        {
            isSelected = false;
            transform.localScale = baseLocalScale;
            return;
        }

        isSelected = selected;

        if (customScaleMultiplier > 1f)
            selectedScaleMultiplier = customScaleMultiplier;

        transform.localScale = isSelected
            ? baseLocalScale * selectedScaleMultiplier
            : baseLocalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsDead) return;

        if (BattleManager.Ins != null)
        {
            BattleManager.Ins.OnUnitClicked(this);
        }
    }

    private void OnValidate()
    {
        maxHp = Mathf.Max(1f, maxHp);
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
        minAttack = Mathf.Max(0f, minAttack);
        maxAttack = Mathf.Max(minAttack, maxAttack);
        buffCount = Mathf.Max(0, buffCount);

        if (Application.isPlaying && statusUI != null)
        {
            RefreshStatusUI();
        }
    }
}
