using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum EHitReactionStyle
{
    HorizontalShakeOnly = 0,        // 기본: 좌우로 흔들리는 애니메이션
    ShakeWithFlashAndKnockback = 1, // 좌우 흔들림 + 순간 점멸 + 살짝 밀렸다 복귀
    CyberGlitchSplit = 2            // 좌우 흔들림 + 네온 잔상 분리
}

/// <summary>
/// 전투 이펙트 매니저:
/// 1. 플레이어 공격: 공격 카드를 사용한 플레이어 캐릭터(Player_1~3 모두 가능) 뒤에서 해당 카드 색상(RED/BLUE/GREEN)의 오라가 재생됨
/// 2. 적 피격: 지정된 적(Enemy_1~3) 위에 단일 참격(Slash) 이펙트와 좌우 흔들리는 효과 및 슬레이 더 스파이어 2 스타일 데미지 폰트 팝업 재생
/// 3. 플레이어 피격: 적 턴에 랜덤 지정된 플레이어 캐릭터(Player_1~3)가 좌우로 흔들리는 피격 모션 및 데미지 폰트 팝업 재생
/// </summary>
public class CombatEffectManager : Singleton<CombatEffectManager>
{
    [Header("전투 대상 기본 참조 (단독 테스트용)")]
    [SerializeField] private SpriteRenderer playerRenderer;
    [SerializeField] private SpriteRenderer enemyRenderer;

    [Header("1. 플레이어별 전용 공격 오라 (0: Player_1 삼각형, 1: Player_2 팔각형, 2: Player_3 오각형)")]
    [SerializeField] private Sprite[] playerSpecificAuraSprites = new Sprite[3];
    [SerializeField] private Sprite[] playerAuraSprites = new Sprite[3]; // 하위 호환용
    [SerializeField] private float auraDuration = 0.48f;                 // 오라가 서서히 커졌다 사라지는 시간
    [SerializeField] private float auraBaseScale = 4.2f;                 // 플레이어 뒤에 깔리는 오라 기본 크기
    [SerializeField] private float auraExpandMultiplier = 1.38f;         // 오라가 퍼지는 최대 배율

    [Header("2. 플레이어별 전용 타격/공격 이펙트 (0: 프리즘 레이저, 1: 소닉 쌍검 연격, 2: 펜타그램 노바 폭발)")]
    [SerializeField] private Sprite[] playerSpecificHitSprites = new Sprite[3];
    [SerializeField] private Sprite enemyHitSlashSprite;                 // 하위 호환용
    [SerializeField] private float slashDuration = 0.28f;                // 타격 이펙트 재생 시간 (초)

    [Header("3. 피격 흔들림 애니메이션 설정 (플레이어 피격 / 적 피격 공통)")]
    [SerializeField] private EHitReactionStyle hitReactionStyle = EHitReactionStyle.HorizontalShakeOnly;
    [SerializeField] private float shakeDuration = 0.32f;                // 흔들리는 총 시간 (초)
    [SerializeField] private float shakeMagnitude = 0.28f;               // 좌우로 흔들리는 폭 (로컬/월드 좌표 단위)
    [SerializeField] private float shakeFrequency = 45f;                 // 좌우 진동 속도
    [SerializeField] private float knockbackDistance = 0.35f;            // (옵션) 넉백 거리

    [Header("4. 슬레이 더 스파이어 2 스타일 데미지 폰트 연출 설정")]
    [SerializeField] private Color normalDamageColor = Color.white;      // 기본 데미지 색상 (흰색)
    [SerializeField] private Color criticalDamageColor = new Color(1.0f, 0.84f, 0.16f, 1.0f); // 크리티컬 데미지 색상 (금색)
    [SerializeField] private int normalDamageFontSize = 44;              // 기본 데미지 폰트 크기
    [SerializeField] private int criticalDamageFontSize = 56;            // 크리티컬 데미지 폰트 크기
    [SerializeField] private float damagePopupDuration = 0.90f;          // 데미지 폰트가 유지되는 총 시간 (초)
    [SerializeField] private float damageLaunchSpeedY = 290f;            // 초기 위로 튀어오르는 속도 (px/s)
    [SerializeField] private float damageLaunchSpeedXMin = 65f;          // 초기 좌우로 퍼지는 최소 속도 (px/s)
    [SerializeField] private float damageLaunchSpeedXMax = 125f;         // 초기 좌우로 퍼지는 최대 속도 (px/s)
    [SerializeField] private float damageGravity = 680f;                 // 포물선 낙하 중력 가속도 (px/s^2)

    [Header("테스트 단축키 (Play 모드 확인용)")]
    [SerializeField] private bool enableTestHotkeys = false;

    private readonly Dictionary<SpriteRenderer, Coroutine> activeShakeCoroutines = new Dictionary<SpriteRenderer, Coroutine>();
    private RectTransform damagePopupRoot;
    private RectTransform flyingStarUIRoot;
    private Canvas cachedCanvas;

    private class GroundStarInstance
    {
        public GameObject gameObject;
        public RectTransform rectTransform;
        public Image image;
        public Vector2 currentPos;
    }

    private readonly List<GroundStarInstance> activeGroundStars = new List<GroundStarInstance>();

    public float AuraDuration => auraDuration;
    public float ShakeDuration => shakeDuration;
    public bool HasGroundStars => activeGroundStars.Count > 0;
    public int GroundStarCount => activeGroundStars.Count;

    private void Start()
    {
        FindTargetsIfNeeded();
        EnsureRuntimeEffectSprites();
    }

    private void Update()
    {
        if (!enableTestHotkeys || Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            PlayPlayerAttackAndEnemyHit(ECardColor.RED);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            PlayPlayerAttackAndEnemyHit(ECardColor.BLUE);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            PlayPlayerAttackAndEnemyHit(ECardColor.GREEN);
        else if (Keyboard.current.digit4Key.wasPressedThisFrame)
            PlayPlayerHit();
    }

    private void FindTargetsIfNeeded()
    {
        if (playerRenderer == null)
        {
            GameObject playerObj = GameObject.Find("Player_1") ?? GameObject.Find("Player");
            if (playerObj != null) playerRenderer = playerObj.GetComponentInChildren<SpriteRenderer>();
        }

        if (enemyRenderer == null)
        {
            GameObject enemyObj = GameObject.Find("Enemy_1") ?? GameObject.Find("Enemy");
            if (enemyObj != null) enemyRenderer = enemyObj.GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void EnsureRuntimeEffectSprites()
    {
        if (playerSpecificAuraSprites == null || playerSpecificAuraSprites.Length < 3)
            playerSpecificAuraSprites = new Sprite[3];

        if (playerSpecificHitSprites == null || playerSpecificHitSprites.Length < 3)
            playerSpecificHitSprites = new Sprite[3];

        string[] auraPaths =
        {
            "Assets/Resource/Effact/fx_player1_aura_triangle.png",
            "Assets/Resource/Effact/fx_player2_aura_octagon.png",
            "Assets/Resource/Effact/fx_player3_aura_pentagon.png"
        };

        string[] hitPaths =
        {
            "Assets/Resource/Effact/fx_player1_hit_prism_laser.png",
            "Assets/Resource/Effact/fx_player2_hit_sonic_flurry.png",
            "Assets/Resource/Effact/fx_player3_hit_buster_nova.png"
        };

        for (int i = 0; i < 3; i++)
        {
            if (playerSpecificAuraSprites[i] == null)
            {
                playerSpecificAuraSprites[i] = UIHelper.LoadSpriteFromAssetOrDisk(auraPaths[i]);
                if (playerSpecificAuraSprites[i] == null)
                {
                    Texture2D tex = CombatEffectTextureGenerator.CreatePlayerSpecificAuraTexture(i, 256);
                    playerSpecificAuraSprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }

            if (playerSpecificHitSprites[i] == null)
            {
                playerSpecificHitSprites[i] = UIHelper.LoadSpriteFromAssetOrDisk(hitPaths[i]);
                if (playerSpecificHitSprites[i] == null)
                {
                    Texture2D hitTex = CombatEffectTextureGenerator.CreatePlayerSpecificHitTexture(i, 256);
                    playerSpecificHitSprites[i] = Sprite.Create(hitTex, new Rect(0, 0, hitTex.width, hitTex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }

        if (playerAuraSprites == null || playerAuraSprites.Length < 3)
            playerAuraSprites = new Sprite[3];

        // 기존 색상 배열 호환 매핑 (0:RED -> Player_3, 1:BLUE -> Player_1, 2:GREEN -> Player_2)
        playerAuraSprites[0] = playerSpecificAuraSprites[2];
        playerAuraSprites[1] = playerSpecificAuraSprites[0];
        playerAuraSprites[2] = playerSpecificAuraSprites[1];

        if (enemyHitSlashSprite == null)
            enemyHitSlashSprite = playerSpecificHitSprites[1];
    }

    // =========================================================================
    // 각 플레이어(Player_1 / Player_2 / Player_3) 전용 공격 오라 + 전용 공격 모션 + 전용 타격 이펙트 시퀀스
    // =========================================================================
    public IEnumerator PlayCardAttackSequence(
        SelectableUnit attackerPlayer,
        ECardColor cardColor,
        SelectableUnit targetEnemy,
        float popupDamage = -1f,
        bool isCritical = false)
    {
        EnsureRuntimeEffectSprites();

        SpriteRenderer attackerSr = attackerPlayer != null ? attackerPlayer.SpriteRenderer : playerRenderer;
        SpriteRenderer targetEnemySr = targetEnemy != null ? targetEnemy.SpriteRenderer : enemyRenderer;
        int playerIdx = attackerPlayer != null ? attackerPlayer.ResolvePlayerTypeIndex() : 0;

        // 1) 공격하는 플레이어 캐릭터 뒤에서 각 플레이어 고유 도형 오라 + 전용 공격 모션 실행
        if (attackerSr != null)
        {
            if (SoundManager.Ins != null)
                SoundManager.Ins.PlayPlayerAura();

            StartCoroutine(CoPlayerSpecificBackAura(attackerSr, playerIdx));

            Vector3 attackerBaseLocalPos = attackerPlayer != null ? attackerPlayer.BaseLocalPosition : attackerSr.transform.localPosition;
            Vector3 targetWorldPos = targetEnemy != null ? targetEnemy.WorldBasePosition : (targetEnemySr != null ? targetEnemySr.transform.position : attackerSr.transform.position + Vector3.right * 4f);
            StartCoroutine(CoPlayPlayerSpecificAttackMotion(attackerSr, attackerBaseLocalPos, targetWorldPos, playerIdx));
        }

        // 오라와 공격 모션이 적에게 도달하는 타이밍 대기
        yield return new WaitForSeconds(0.14f);

        // 2) 지정된 적 오브젝트에 플레이어별 고유 타격 이펙트 + 데미지 폰트 팝업 + 좌우 흔들리는 효과 재생
        if (targetEnemySr != null)
        {
            if (SoundManager.Ins != null)
            {
                SoundManager.Ins.PlayCardAttackHit(cardColor);
                if (isCritical)
                    SoundManager.Ins.PlayCriticalHit();
            }

            if (popupDamage >= 0f && targetEnemy != null)
            {
                SpawnDamagePopup(targetEnemy, popupDamage, isCritical);
            }

            StartCoroutine(CoPlayPlayerSpecificHitEffect(
                playerIdx,
                cardColor,
                isCritical,
                targetEnemySr.transform.position,
                targetEnemySr.sortingLayerID,
                targetEnemySr.sortingOrder + 2
            ));

            Vector3 basePos = targetEnemy != null ? targetEnemy.BaseLocalPosition : targetEnemySr.transform.localPosition;
            Vector3 baseScale = targetEnemy != null ? targetEnemy.CurrentTargetScale : targetEnemySr.transform.localScale;
            Color baseCol = targetEnemy != null ? targetEnemy.BaseColor : targetEnemySr.color;

            yield return StartCoroutine(CoHitShakeOnUnit(targetEnemySr, basePos, baseScale, baseCol, +1f));
        }
        else
        {
            if (popupDamage >= 0f && targetEnemy != null)
            {
                SpawnDamagePopup(targetEnemy, popupDamage, isCritical);
            }
            yield return new WaitForSeconds(shakeDuration);
        }
    }

    // =========================================================================
    // Enemy 턴 전용: 공격 이펙트 없이 피격받는 플레이어만 좌우로 흔들리는 피격 모션 및 데미지 팝업 재생
    // =========================================================================
    public IEnumerator PlayPlayerHitSequenceOnUnit(
        SelectableUnit targetPlayer,
        float popupDamage = -1f,
        bool isCritical = false)
    {
        if (targetPlayer == null || targetPlayer.SpriteRenderer == null)
            yield break;

        if (SoundManager.Ins != null)
            SoundManager.Ins.PlayEnemyAttackHit();

        if (popupDamage >= 0f)
        {
            SpawnDamagePopup(targetPlayer, popupDamage, isCritical);
        }

        SpriteRenderer targetSr = targetPlayer.SpriteRenderer;
        Vector3 basePos = targetPlayer.BaseLocalPosition;
        Vector3 baseScale = targetPlayer.CurrentTargetScale;
        Color baseCol = targetPlayer.BaseColor;

        yield return StartCoroutine(CoHitShakeOnUnit(targetSr, basePos, baseScale, baseCol, -1f));
    }

    // =========================================================================
    // 슬레이 더 스파이어 2 스타일 데미지 폰트 팝업 (Scale Punch + 포물선 물리 궤적 + 페이드아웃)
    // - 기본 데미지: 흰색 글씨 (normalDamageColor)
    // - 크리티컬 데미지: 서브텍스트 없이 금색 글씨 (criticalDamageColor)
    // =========================================================================
    public void SpawnDamagePopup(SelectableUnit targetUnit, float damageAmount, bool isCritical = false)
    {
        if (targetUnit == null) return;
        SpawnDamagePopupAtWorldPosition(targetUnit.WorldBasePosition, damageAmount, isCritical);
    }

    public void SpawnDamagePopupAtWorldPosition(Vector3 worldPosition, float damageAmount, bool isCritical = false)
    {
        RectTransform root = EnsureDamagePopupRoot();
        if (root == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // 유닛 중심보다 살짝 위쪽에서 시작
        Vector3 spawnWorldPos = worldPosition + new Vector3(0f, 0.35f, 0f);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, spawnWorldPos);

        Camera uiCam = (cachedCanvas != null && cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? cachedCanvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, uiCam, out Vector2 startAnchoredPos))
        {
            return;
        }

        // 연속 타격 시 숫자가 완전히 겹치지 않도록 미세한 초기 위치 오프셋 부여
        startAnchoredPos += new Vector2(Random.Range(-16f, 16f), Random.Range(-6f, 10f));

        // 데미지 수치 문자열 포맷팅 (정수면 정수로, 소수점이 있으면 첫째 자리까지 표시)
        float cleanDamage = Mathf.Max(0f, damageAmount);
        string damageTextStr = UIHelper.FormatCompactNumber(cleanDamage);

        GameObject popupObj = new GameObject(
            isCritical ? $"DamagePopup_Crit_{damageTextStr}" : $"DamagePopup_{damageTextStr}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text),
            typeof(Outline),
            typeof(Shadow)
        );
        popupObj.layer = LayerMask.NameToLayer("UI");
        popupObj.transform.SetParent(root, false);

        RectTransform popupRt = popupObj.GetComponent<RectTransform>();
        popupRt.anchorMin = new Vector2(0.5f, 0.5f);
        popupRt.anchorMax = new Vector2(0.5f, 0.5f);
        popupRt.pivot = new Vector2(0.5f, 0.5f);
        popupRt.anchoredPosition = startAnchoredPos;
        popupRt.sizeDelta = new Vector2(260f, 90f);
        popupRt.localScale = Vector3.one * 0.35f;

        Text txt = popupObj.GetComponent<Text>();
        txt.font = GetOrCreateDamageFont();
        txt.text = damageTextStr;
        txt.fontSize = isCritical ? criticalDamageFontSize : normalDamageFontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;
        txt.color = isCritical ? criticalDamageColor : normalDamageColor;

        // 슬레이 더 스파이어 스타일의 두껍고 선명한 외곽선 + 그림자
        Outline outline = popupObj.GetComponent<Outline>();
        outline.effectColor = isCritical
            ? new Color(0.16f, 0.07f, 0.01f, 0.98f)
            : new Color(0.04f, 0.06f, 0.12f, 0.98f);
        outline.effectDistance = isCritical ? new Vector2(3.0f, -3.0f) : new Vector2(2.4f, -2.4f);
        outline.useGraphicAlpha = true;

        Shadow shadow = popupObj.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.88f);
        shadow.effectDistance = isCritical ? new Vector2(0f, -4.8f) : new Vector2(0f, -3.8f);
        shadow.useGraphicAlpha = true;

        StartCoroutine(CoAnimateDamagePopup(popupRt, txt, startAnchoredPos, isCritical));
    }

    private IEnumerator CoAnimateDamagePopup(
        RectTransform popupRt,
        Text popupText,
        Vector2 startPos,
        bool isCritical)
    {
        if (popupRt == null || popupText == null)
            yield break;

        Color baseColor = isCritical ? criticalDamageColor : normalDamageColor;
        float duration = isCritical ? damagePopupDuration * 1.08f : damagePopupDuration;

        // 좌/우 랜덤 방향으로 톡 튀어오르는 포물선 초기 속도 설정
        float dirX = Random.value < 0.5f ? -1f : 1f;
        float vx = dirX * Random.Range(damageLaunchSpeedXMin, damageLaunchSpeedXMax);
        float vy = (isCritical ? damageLaunchSpeedY * 1.12f : damageLaunchSpeedY) * Random.Range(0.92f, 1.08f);
        float gravity = damageGravity;

        // 이동 방향에 맞춘 자연스러운 기울기(Tilt)
        float targetTiltAngle = -dirX * Random.Range(5f, 11f);

        // 스케일 펀치 배율 (타격 순간 크게 튀어올랐다가 안착)
        float startScale = 0.35f;
        float peakScale = isCritical ? 1.62f : 1.36f;
        float settleScale = isCritical ? 1.10f : 1.00f;

        Vector2 currentPos = startPos;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (popupRt == null) yield break;

            float dt = Time.deltaTime;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));

            // 1) 포물선 물리 이동 (초기 상승 후 중력에 의해 완만하게 정점 통과 및 낙하)
            currentPos.x += vx * dt;
            currentPos.y += vy * dt;
            vy -= gravity * dt;
            // 수평 속도는 공기 저항처럼 서서히 감속
            vx *= Mathf.Pow(0.28f, dt);

            popupRt.anchoredPosition = currentPos;

            // 2) 회전 기울기 (초반에 살짝 기울어지며 날아감)
            float tiltT = Mathf.Clamp01(elapsed / 0.30f);
            float curAngle = Mathf.Lerp(0f, targetTiltAngle, 1f - Mathf.Pow(1f - tiltT, 2f));
            popupRt.localRotation = Quaternion.Euler(0f, 0f, curAngle);

            // 3) 탄성 스케일 펀치 (0~12%: start -> peak, 12~26%: peak -> settle, 26~70%: settle 유지, 70~100%: 살짝 작아지며 소멸)
            float curScale;
            if (t < 0.12f)
            {
                float p = t / 0.12f;
                float eased = 1f - Mathf.Pow(1f - p, 3f);
                curScale = Mathf.Lerp(startScale, peakScale, eased);
            }
            else if (t < 0.26f)
            {
                float p = (t - 0.12f) / 0.14f;
                float eased = p * p * (3f - 2f * p);
                curScale = Mathf.Lerp(peakScale, settleScale, eased);
            }
            else if (t < 0.70f)
            {
                curScale = settleScale;
            }
            else
            {
                float p = (t - 0.70f) / 0.30f;
                curScale = Mathf.Lerp(settleScale, settleScale * 0.82f, p);
            }
            popupRt.localScale = Vector3.one * curScale;

            // 4) 알파 페이드아웃 (70% 지점까지는 선명하게 유지 후 부드럽게 사라짐)
            float alpha = 1f;
            if (t >= 0.68f)
            {
                float fadeT = (t - 0.68f) / 0.32f;
                alpha = Mathf.Clamp01(1f - (fadeT * fadeT));
            }

            Color c = baseColor;
            c.a = alpha;
            popupText.color = c;

            yield return null;
        }

        if (popupRt != null)
        {
            Destroy(popupRt.gameObject);
        }
    }

    private RectTransform EnsureDamagePopupRoot()
    {
        if (cachedCanvas == null)
        {
            cachedCanvas = Object.FindFirstObjectByType<Canvas>();
        }
        if (cachedCanvas == null) return null;

        if (damagePopupRoot == null)
        {
            Transform existing = cachedCanvas.transform.Find("DamagePopupUIRoot");
            if (existing != null)
            {
                damagePopupRoot = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject rootObj = new GameObject("DamagePopupUIRoot", typeof(RectTransform));
                rootObj.layer = LayerMask.NameToLayer("UI");
                rootObj.transform.SetParent(cachedCanvas.transform, false);

                damagePopupRoot = rootObj.GetComponent<RectTransform>();
                damagePopupRoot.anchorMin = Vector2.zero;
                damagePopupRoot.anchorMax = Vector2.one;
                damagePopupRoot.offsetMin = Vector2.zero;
                damagePopupRoot.offsetMax = Vector2.zero;
                damagePopupRoot.pivot = new Vector2(0.5f, 0.5f);
                damagePopupRoot.localScale = Vector3.one;
                damagePopupRoot.localPosition = Vector3.zero;

                // 체력바(UnitStatusUIRoot)와 스킬 버튼(PlayerSkillUIRoot)보다 위, 결과창/일시정지창보다는 아래에 배치
                Transform skillRoot = cachedCanvas.transform.Find("PlayerSkillUIRoot");
                Transform statusRoot = cachedCanvas.transform.Find("UnitStatusUIRoot");
                if (skillRoot != null)
                    rootObj.transform.SetSiblingIndex(skillRoot.GetSiblingIndex() + 1);
                else if (statusRoot != null)
                    rootObj.transform.SetSiblingIndex(statusRoot.GetSiblingIndex() + 1);
            }
        }

        return damagePopupRoot;
    }

    private static Font GetOrCreateDamageFont() => UIHelper.GetOrCreateFont();

    // =========================================================================
    // FGO 스타일 크리티컬 스타(Star) 비주얼 연출 시스템:
    // 1. Quick 공격 피격 시: 적 위치에서 스타들이 톡 튀어올라 우측 하단 Star UI로 가속 흡수 비행
    // 2. Attack 페이즈 시작 시: 우측 하단 Star UI에서 AttackBox 카드들로 스타들이 솟구쳐 날아가 크리티컬 확률 상승
    // =========================================================================
    private RectTransform EnsureFlyingStarRoot()
    {
        if (cachedCanvas == null)
        {
            cachedCanvas = Object.FindFirstObjectByType<Canvas>();
        }
        if (cachedCanvas == null) return null;

        if (flyingStarUIRoot == null)
        {
            Transform existing = cachedCanvas.transform.Find("FlyingStarUIRoot");
            if (existing != null)
            {
                flyingStarUIRoot = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject rootObj = new GameObject("FlyingStarUIRoot", typeof(RectTransform));
                rootObj.layer = LayerMask.NameToLayer("UI");
                rootObj.transform.SetParent(cachedCanvas.transform, false);

                flyingStarUIRoot = rootObj.GetComponent<RectTransform>();
                flyingStarUIRoot.anchorMin = Vector2.zero;
                flyingStarUIRoot.anchorMax = Vector2.one;
                flyingStarUIRoot.offsetMin = Vector2.zero;
                flyingStarUIRoot.offsetMax = Vector2.zero;
                flyingStarUIRoot.pivot = new Vector2(0.5f, 0.5f);
                flyingStarUIRoot.localScale = Vector3.one;
                flyingStarUIRoot.localPosition = Vector3.zero;

                rootObj.transform.SetAsLastSibling();
            }
        }

        return flyingStarUIRoot;
    }

    private Vector2 WorldToLocalCanvasPosition(RectTransform root, Vector3 worldPos)
    {
        Camera cam = Camera.main;
        if (cam == null || root == null) return Vector2.zero;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        Camera uiCam = (cachedCanvas != null && cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? cachedCanvas.worldCamera
            : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, uiCam, out Vector2 localPos);
        return localPos;
    }

    private Vector2 RectTransformToLocalCanvasPosition(RectTransform root, RectTransform targetRt)
    {
        if (targetRt == null || root == null) return Vector2.zero;

        // 동일한 Canvas 하위 UI 요소(화면 하단 스타 표기 UI, 손패 카드 등)의 경우 InverseTransformPoint로 오차 없이 즉시 로컬 좌표를 구합니다.
        Vector3 localPt3D = root.InverseTransformPoint(targetRt.position);
        return new Vector2(localPt3D.x, localPt3D.y);
    }

    private GameObject CreateFlyingStarObject(RectTransform root, Vector2 startPos, float size = 36f)
    {
        GameObject starObj = new GameObject("FlyingStar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        starObj.layer = LayerMask.NameToLayer("UI");
        starObj.transform.SetParent(root, false);

        RectTransform rt = starObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = startPos;
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.zero;

        Image img = starObj.GetComponent<Image>();
        Sprite starSprite = BattleResourceUIManager.Ins != null
            ? BattleResourceUIManager.Ins.CriticalStarSprite
            : BattleResourceUIManager.GetOrCreateCriticalStarSprite();
        img.sprite = starSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        return starObj;
    }

    private static Vector2 EvalQuadBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    /// <summary>
    /// FGO 스타일 스타 생성 (Attack 페이즈 피격 시):
    /// 피격받은 적 오브젝트 위치를 기준으로 스타를 생성하여 바닥(피격 적 y좌표 기준)으로 튀겨 안착시킵니다.
    /// </summary>
    public void SpawnStarsOnGround(Vector3 enemyWorldPos, int starCount)
    {
        if (starCount <= 0) return;
        StartCoroutine(CoSpawnStarsOnGround(enemyWorldPos, starCount));
    }

    public IEnumerator CoSpawnStarsOnGround(Vector3 enemyWorldPos, int starCount)
    {
        RectTransform root = EnsureFlyingStarRoot();
        if (root == null) yield break;

        // 피격 위치 (적 중심 부근에서 팝업)
        Vector2 spawnCenterPos = WorldToLocalCanvasPosition(root, enemyWorldPos + new Vector3(0f, 0.35f, 0f));

        // 지시서: "바닥의 기준은 피격받은 적 오브젝트 위치에서 y값"
        float groundBaseY = WorldToLocalCanvasPosition(root, enemyWorldPos).y;

        for (int i = 0; i < starCount; i++)
        {
            Vector2 startPos = spawnCenterPos + new Vector2(Random.Range(-12f, 12f), Random.Range(-6f, 10f));
            float starGroundY = groundBaseY + Random.Range(-14f, 14f); // 적 발밑 부근 자연스러운 바닥선
            StartCoroutine(CoBounceAndSettleOnGround(startPos, starGroundY, root));
            yield return new WaitForSeconds(0.035f);
        }
    }

    private IEnumerator CoBounceAndSettleOnGround(Vector2 startPos, float groundY, RectTransform root)
    {
        if (root == null) yield break;

        GameObject starObj = CreateFlyingStarObject(root, startPos, 32f);
        if (starObj == null) yield break;

        RectTransform starRt = starObj.GetComponent<RectTransform>();
        Image starImg = starObj.GetComponent<Image>();

        GroundStarInstance starInst = new GroundStarInstance
        {
            gameObject = starObj,
            rectTransform = starRt,
            image = starImg,
            currentPos = startPos
        };
        activeGroundStars.Add(starInst);

        // 1단계: 적에게서 뿜어져 나와 바닥으로 떨어지며 톡- 톡- 2단 바운스
        float vx = Random.Range(-115f, 115f); // 좌우 분산 속도
        float vy = Random.Range(180f, 280f);  // 상향 튀어오름 속도
        float gravity = 760f;                 // 중력 가속도
        float rotSpeed = Random.Range(-360f, 360f);

        Vector2 pos = startPos;
        int bounceCount = 0;
        int maxBounces = 2;
        float settleTimer = 0f;
        bool isSettled = false;

        float spawnElapsed = 0f;
        float spawnDuration = 0.12f;

        while (!isSettled && starObj != null)
        {
            float dt = Time.deltaTime;
            settleTimer += dt;

            if (spawnElapsed < spawnDuration)
            {
                spawnElapsed += dt;
                float p = Mathf.Clamp01(spawnElapsed / spawnDuration);
                starRt.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.05f, p);
            }

            vy -= gravity * dt;
            pos += new Vector2(vx * dt, vy * dt);

            // 바닥 충돌 판정
            if (pos.y <= groundY)
            {
                pos.y = groundY;
                bounceCount++;

                if (bounceCount <= maxBounces && Mathf.Abs(vy) > 60f)
                {
                    // 바운스: 상향 반발력 + 감속
                    vy = -vy * 0.40f;
                    vx *= 0.65f;
                    rotSpeed *= 0.6f;
                }
                else
                {
                    // 바닥 안착
                    vy = 0f;
                    vx = 0f;
                    isSettled = true;
                }
            }

            starRt.anchoredPosition = pos;
            starRt.localEulerAngles = new Vector3(0f, 0f, starRt.localEulerAngles.z + rotSpeed * dt);
            starInst.currentPos = pos;

            if (settleTimer > 0.85f)
            {
                pos.y = groundY;
                starRt.anchoredPosition = pos;
                starInst.currentPos = pos;
                isSettled = true;
            }

            yield return null;
        }

        if (starObj == null) yield break;

        // 2단계: 안착 후 Attack 페이즈가 끝날 때까지 바닥에서 대기하며 은은하게 반짝임 (Sparkle/Bobbing Idle)
        float idlePhase = Random.Range(0f, Mathf.PI * 2f);
        Vector2 settledPos = pos;

        while (starObj != null && activeGroundStars.Contains(starInst))
        {
            float t = Time.time * 4f + idlePhase;
            float bobbing = Mathf.Sin(t) * 2.2f;
            float pulsing = 1f + Mathf.Sin(t * 1.25f) * 0.08f;
            starRt.anchoredPosition = settledPos + new Vector2(0f, bobbing);
            starRt.localScale = Vector3.one * pulsing;
            starRt.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 0.6f) * 10f);
            yield return null;
        }
    }

    /// <summary>
    /// FGO 스타일 스타 획득 (Attack 페이즈 종료 시):
    /// 바닥에 머무르던 모든 스타들이 일제히 우측 하단 Star UI로 빨려 들어가 스톡에 누적됩니다.
    /// </summary>
    public IEnumerator CoCollectGroundStarsToCounter()
    {
        if (activeGroundStars.Count == 0) yield break;

        RectTransform root = EnsureFlyingStarRoot();
        if (root == null) yield break;

        List<GroundStarInstance> starsToCollect = new List<GroundStarInstance>(activeGroundStars);
        activeGroundStars.Clear();

        int total = starsToCollect.Count;
        int arrivedCount = 0;

        for (int i = 0; i < total; i++)
        {
            GroundStarInstance star = starsToCollect[i];
            if (star != null && star.gameObject != null)
            {
                StartCoroutine(CoFlyGroundStarToCounter(star, root, () => arrivedCount++));
            }
            else
            {
                arrivedCount++;
            }

            // 바닥의 별들이 순차적으로 날아오르는 시차
            yield return new WaitForSeconds(0.04f);
        }

        float timeout = 1.8f;
        float elapsed = 0f;
        while (arrivedCount < total && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.12f);
    }

    private IEnumerator CoFlyGroundStarToCounter(GroundStarInstance starInst, RectTransform root, System.Action onArrived)
    {
        if (starInst == null || starInst.gameObject == null)
        {
            onArrived?.Invoke();
            yield break;
        }

        GameObject starObj = starInst.gameObject;
        RectTransform starRt = starInst.rectTransform;

        Vector2 startPos = starRt.anchoredPosition;
        RectTransform targetRt = BattleResourceUIManager.Ins != null ? BattleResourceUIManager.Ins.StarIconRect : null;
        Vector2 targetPos = RectTransformToLocalCanvasPosition(root, targetRt);

        // 높은 아크 궤적 제어점
        Vector2 mid = (startPos + targetPos) * 0.5f;
        float arcHeight = Mathf.Abs(targetPos.x - startPos.x) * 0.22f + 110f;
        Vector2 controlPoint = new Vector2(mid.x + 40f, Mathf.Max(startPos.y, targetPos.y) + arcHeight);

        float flightDuration = Random.Range(0.34f, 0.40f);
        float elapsed = 0f;
        float rotSpeed = Random.Range(-380f, 380f);

        while (elapsed < flightDuration)
        {
            if (starObj == null || starRt == null)
            {
                onArrived?.Invoke();
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightDuration);

            // 가속 비행: 스타 스톡으로 빠르게 쏙 빨려들어감
            float accelT = t * t;

            if (targetRt != null)
                targetPos = RectTransformToLocalCanvasPosition(root, targetRt);

            starRt.anchoredPosition = EvalQuadBezier(startPos, controlPoint, targetPos, accelT);
            starRt.localEulerAngles = new Vector3(0f, 0f, starRt.localEulerAngles.z + rotSpeed * Time.deltaTime);

            float scaleP = 1.05f - 0.25f * accelT;
            starRt.localScale = Vector3.one * scaleP;

            yield return null;
        }

        Destroy(starObj);

        // 스타 1개 누적 + 카운터 아이콘 펄스 + 효과음
        if (BattleResourceUIManager.Ins != null)
        {
            BattleResourceUIManager.Ins.AddStars(1);
            BattleResourceUIManager.Ins.PunchStarIcon(1.24f, 0.16f);
        }

        if (SoundManager.Ins != null)
        {
            SoundManager.Ins.PlayStarEffect();
        }

        onArrived?.Invoke();
    }

    /// <summary>
    /// 필드 바닥에 남아있는 모든 스타 오브젝트를 강제 정리합니다.
    /// </summary>
    public void ClearAllGroundStars()
    {
        for (int i = 0; i < activeGroundStars.Count; i++)
        {
            if (activeGroundStars[i] != null && activeGroundStars[i].gameObject != null)
            {
                Destroy(activeGroundStars[i].gameObject);
            }
        }
        activeGroundStars.Clear();
    }

    /// <summary>
    /// 호환성 유지 메서드: SpawnStarsOnGround로 포워딩합니다.
    /// </summary>
    public void SpawnStarsFromEnemyToCounter(Vector3 enemyWorldPos, int starCount)
    {
        SpawnStarsOnGround(enemyWorldPos, starCount);
    }

    /// <summary>
    /// 스타가 사용될 때 (카드로 공격 시):
    /// 화면 하단 스타 표기 UI(StarCounterUI)에서 사용하려는 해당 카드로 치명타 확률(starsToUse개, 1개당 10%)만큼 스타가 날아가도록 연출합니다.
    /// </summary>
    public IEnumerator CoFlyStarsFromCounterToSingleCard(Card targetCard, int starsToUse)
    {
        if (targetCard == null || starsToUse <= 0)
            yield break;

        RectTransform root = EnsureFlyingStarRoot();
        if (root == null) yield break;

        float stepDelay = Mathf.Clamp(0.28f / Mathf.Max(1, starsToUse), 0.022f, 0.055f);

        for (int i = 0; i < starsToUse; i++)
        {
            if (targetCard == null) break;

            if (BattleResourceUIManager.Ins != null)
            {
                BattleResourceUIManager.Ins.DeductStars(1);
                BattleResourceUIManager.Ins.PunchStarIcon(1.18f, 0.12f);
            }

            StartCoroutine(CoFlySingleStarToCard(targetCard, root));
            yield return new WaitForSeconds(stepDelay);
        }

        // 마지막 스타가 카드에 도달해 치명타 확률이 표시될 때까지 대기
        yield return new WaitForSeconds(0.36f);
    }

    /// <summary>
    /// 우측/좌측 하단 Star UI에서 지정된 카드들로 스타들을 순차 발사하여 크리티컬 확률을 할당합니다.
    /// </summary>
    public IEnumerator CoDistributeStarsToCards(IReadOnlyList<Card> cards, int[] distributionSequence)
    {
        if (cards == null || cards.Count == 0 || distributionSequence == null || distributionSequence.Length == 0)
            yield break;

        RectTransform root = EnsureFlyingStarRoot();
        if (root == null) yield break;

        float stepDelay = Mathf.Clamp(0.42f / Mathf.Max(1, distributionSequence.Length), 0.018f, 0.065f);

        for (int i = 0; i < distributionSequence.Length; i++)
        {
            int cardIdx = distributionSequence[i];
            if (cardIdx >= 0 && cardIdx < cards.Count && cards[cardIdx] != null)
            {
                Card targetCard = cards[cardIdx];

                // 우측 하단 스타 UI 카운트 1개 차감 및 미세 펄스
                if (BattleResourceUIManager.Ins != null)
                {
                    BattleResourceUIManager.Ins.DeductStars(1);
                    BattleResourceUIManager.Ins.PunchStarIcon(1.15f, 0.12f);
                }

                StartCoroutine(CoFlySingleStarToCard(targetCard, root));
            }

            yield return new WaitForSeconds(stepDelay);
        }

        // 마지막 스타가 카드에 도달하여 장착될 때까지 대기
        yield return new WaitForSeconds(0.38f);
    }

    private IEnumerator CoFlySingleStarToCard(Card targetCard, RectTransform root)
    {
        if (root == null || targetCard == null) yield break;

        RectTransform sourceRt = BattleResourceUIManager.Ins != null ? BattleResourceUIManager.Ins.StarIconRect : null;
        Vector2 startPos = RectTransformToLocalCanvasPosition(root, sourceRt);

        GameObject starObj = CreateFlyingStarObject(root, startPos, 38f);
        if (starObj == null) yield break;

        RectTransform starRt = starObj.GetComponent<RectTransform>();
        starRt.localScale = Vector3.one;

        Vector2 targetPos = RectTransformToLocalCanvasPosition(root, targetCard.CritBadgeRect);

        // 높은 아크형 궤적 (위로 시원하게 솟구쳤다가 카드로 꽂힘)
        Vector2 mid = (startPos + targetPos) * 0.5f;
        float arcHeight = 160f + Mathf.Abs(targetPos.x - startPos.x) * 0.15f;
        Vector2 controlPoint = new Vector2(mid.x, Mathf.Max(startPos.y, targetPos.y) + arcHeight);

        float duration = 0.34f;
        float elapsed = 0f;
        float rotSpeed = Random.Range(-320f, 320f);

        while (elapsed < duration)
        {
            if (starObj == null || starRt == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float curvedT = Mathf.Pow(t, 1.35f);

            if (targetCard != null && targetCard.CritBadgeRect != null)
                targetPos = RectTransformToLocalCanvasPosition(root, targetCard.CritBadgeRect);

            starRt.anchoredPosition = EvalQuadBezier(startPos, controlPoint, targetPos, curvedT);
            starRt.localEulerAngles = new Vector3(0f, 0f, starRt.localEulerAngles.z + rotSpeed * Time.deltaTime);
            yield return null;
        }

        Destroy(starObj);

        if (targetCard != null)
        {
            targetCard.AddOneAssignedStar();
        }

        if (SoundManager.Ins != null)
        {
            SoundManager.Ins.PlayStarEffect();
        }
    }

    // =========================================================================
    // 개별 호출 및 호환 메서드
    // =========================================================================
    public void PlayPlayerAttackAndEnemyHit(ECardColor auraColor)
    {
        FindTargetsIfNeeded();
        PlayPlayerAttackAura(playerRenderer, auraColor);
        PlayEnemyHit(enemyRenderer, 0.12f);
    }

    public void PlayPlayerAttackAura(ECardColor auraColor)
    {
        FindTargetsIfNeeded();
        PlayPlayerAttackAura(playerRenderer, auraColor);
    }

    public void PlayPlayerAttackAura(SpriteRenderer targetPlayerSr, ECardColor auraColor)
    {
        EnsureRuntimeEffectSprites();
        if (targetPlayerSr == null) return;
        SelectableUnit unit = targetPlayerSr.GetComponent<SelectableUnit>();
        int pIdx = unit != null ? unit.ResolvePlayerTypeIndex() : 0;
        StartCoroutine(CoPlayerSpecificBackAura(targetPlayerSr, pIdx));
    }

    private IEnumerator CoPlayerSpecificBackAura(SpriteRenderer targetPlayerSr, int playerIndex)
    {
        if (targetPlayerSr == null) yield break;

        int idx = Mathf.Clamp(playerIndex, 0, 2);
        Sprite auraSp = (playerSpecificAuraSprites != null && idx < playerSpecificAuraSprites.Length && playerSpecificAuraSprites[idx] != null)
            ? playerSpecificAuraSprites[idx]
            : playerAuraSprites[idx];

        GameObject auraObj = new GameObject($"PlayerSpecificAura_P{idx + 1}", typeof(SpriteRenderer));
        auraObj.transform.SetParent(targetPlayerSr.transform, false);
        auraObj.transform.localPosition = Vector3.zero;
        auraObj.transform.localScale = Vector3.one * auraBaseScale;

        SpriteRenderer auraSr = auraObj.GetComponent<SpriteRenderer>();
        auraSr.sprite = auraSp;
        auraSr.sortingLayerID = targetPlayerSr.sortingLayerID;
        auraSr.sortingOrder = targetPlayerSr.sortingOrder - 1;

        // 각 플레이어별 오라 회전 속도 (Player_1: 프리즘 회전, Player_2: 고속 소닉 볼텍스 회전, Player_3: 묵직한 리액터 회전)
        float rotSpeed = idx == 0 ? 110f : (idx == 1 ? -320f : 45f);
        float elapsed = 0f;

        while (elapsed < auraDuration)
        {
            if (auraObj == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / auraDuration);
            float alphaCurve = Mathf.Sin(t * Mathf.PI);

            Color c = Color.white;
            c.a = alphaCurve;
            auraSr.color = c;

            float currentScale = Mathf.Lerp(auraBaseScale * 0.82f, auraBaseScale * auraExpandMultiplier, t);
            auraObj.transform.localScale = Vector3.one * currentScale;
            auraObj.transform.localEulerAngles = new Vector3(0f, 0f, elapsed * rotSpeed);

            yield return null;
        }

        if (auraObj != null)
            Destroy(auraObj);
    }

    /// <summary>
    /// 플레이어별 고유 공격 모션 및 발사체(Player_1: 시안 프리즘 레이저 빔 발사, Player_2: 에메랄드 고속 잔상 대시, Player_3: 크림슨 헤비 스매시 돌진)
    /// </summary>
    private IEnumerator CoPlayPlayerSpecificAttackMotion(
        SpriteRenderer attackerSr,
        Vector3 originLocalPos,
        Vector3 targetWorldPos,
        int playerIndex)
    {
        if (attackerSr == null) yield break;

        Transform tr = attackerSr.transform;
        int idx = Mathf.Clamp(playerIndex, 0, 2);

        if (idx == 0)
        {
            // [Player_1 - 파란 삼각형]: 앞으로 살짝 전진하며 적에게 시안 프리즘 레이저 빔 스트림 발사
            StartCoroutine(CoFirePrismLaserBeam(tr.position, targetWorldPos, attackerSr.sortingLayerID, attackerSr.sortingOrder + 1));

            float dur = 0.24f;
            float el = 0f;
            while (el < dur)
            {
                if (tr == null) yield break;
                el += Time.deltaTime;
                float t = Mathf.Clamp01(el / dur);
                float lunge = Mathf.Sin(t * Mathf.PI) * 0.42f;
                tr.localPosition = originLocalPos + new Vector3(lunge, 0f, 0f);
                yield return null;
            }
            if (tr != null) tr.localPosition = originLocalPos;
        }
        else if (idx == 1)
        {
            // [Player_2 - 초록 팔각형]: 에메랄드 네온 잔상을 남기며 초고속 질풍 대시
            SpriteRenderer ghost = CreateGlitchGhost(attackerSr, new Color(0.15f, 1.0f, 0.62f, 0.72f));
            float dur = 0.26f;
            float el = 0f;
            while (el < dur)
            {
                if (tr == null) yield break;
                el += Time.deltaTime;
                float t = Mathf.Clamp01(el / dur);
                float dash = Mathf.Sin(t * Mathf.PI) * 0.75f;
                tr.localPosition = originLocalPos + new Vector3(dash, Mathf.Sin(t * Mathf.PI * 2f) * 0.12f, 0f);

                if (ghost != null)
                {
                    ghost.transform.localPosition = new Vector3(-dash * 1.4f, 0f, 0f);
                    ghost.color = new Color(0.15f, 1.0f, 0.62f, (1f - t) * 0.7f);
                }
                yield return null;
            }
            if (ghost != null) Destroy(ghost.gameObject);
            if (tr != null) tr.localPosition = originLocalPos;
        }
        else
        {
            // [Player_3 - 빨간 오각형]: 뒤로 살짝 기를 모았다가 묵직하게 앞으로 강타(Heavy Smash)
            float dur = Mathf.Max(0.18f, slashDuration * 1.05f);
            float el = 0f;
            while (el < dur)
            {
                if (tr == null) yield break;
                el += Time.deltaTime;
                float t = Mathf.Clamp01(el / dur);
                float offsetX;
                if (t < 0.28f)
                {
                    offsetX = Mathf.Lerp(0f, -0.25f, t / 0.28f);
                }
                else
                {
                    float p = (t - 0.28f) / 0.72f;
                    offsetX = Mathf.Sin(p * Mathf.PI) * 0.68f;
                }
                tr.localPosition = originLocalPos + new Vector3(offsetX, 0f, 0f);
                yield return null;
            }
            if (tr != null) tr.localPosition = originLocalPos;
        }
    }

    private IEnumerator CoFirePrismLaserBeam(Vector3 startWorldPos, Vector3 endWorldPos, int sortingLayerID, int sortingOrder)
    {
        GameObject beamObj = new GameObject("Player1_PrismLaserStream", typeof(SpriteRenderer));
        Vector3 mid = (startWorldPos + endWorldPos) * 0.5f;
        Vector3 diff = endWorldPos - startWorldPos;
        float dist = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

        beamObj.transform.position = mid;
        beamObj.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer sr = beamObj.GetComponent<SpriteRenderer>();
        sr.sprite = UIHelper.GetOrCreateSolidWhiteSprite();
        sr.sortingLayerID = sortingLayerID;
        sr.sortingOrder = sortingOrder;

        float dur = 0.18f;
        float el = 0f;
        while (el < dur)
        {
            if (beamObj == null) yield break;
            el += Time.deltaTime;
            float t = Mathf.Clamp01(el / dur);
            float thickness = Mathf.Sin(t * Mathf.PI) * 4.5f;
            beamObj.transform.localScale = new Vector3(dist * 25f, thickness, 1f);
            sr.color = Color.Lerp(new Color(0.20f, 0.92f, 1f, 0.95f), new Color(1f, 1f, 1f, 0f), t * t);
            yield return null;
        }

        if (beamObj != null)
            Destroy(beamObj);
    }

    /// <summary>
    /// 플레이어별 고유 적 타격 임팩트 연출:
    /// - Player_1 (0): 트라이앵글 프리즘 크로스 레이저 & 삼각 쇼크웨이브 회전 확장
    /// - Player_2 (1): 에메랄드 소닉 쌍검 초승달 3연속 고속 질풍 참격 (Multi-Slash Flurry)
    /// - Player_3 (2): 5방향 크림슨 펜타그램 플라즈마 노바 폭발 & 오각 크래시 쇼크웨이브
    /// </summary>
    private IEnumerator CoPlayPlayerSpecificHitEffect(
        int playerIndex,
        ECardColor cardColor,
        bool isCritical,
        Vector3 worldPos,
        int sortingLayerID,
        int sortingOrder)
    {
        int idx = Mathf.Clamp(playerIndex, 0, 2);
        Sprite hitSprite = (playerSpecificHitSprites != null && idx < playerSpecificHitSprites.Length && playerSpecificHitSprites[idx] != null)
            ? playerSpecificHitSprites[idx]
            : enemyHitSlashSprite;

        float critBoost = isCritical ? 1.28f : 1.0f;
        if (cardColor == ECardColor.SUPER_BUSTER)
            critBoost *= 1.35f;

        if (idx == 0)
        {
            // [Player_1 전용 타격]: 트라이앵글 프리즘 레이저 스타버스트 + 삼각 쇼크웨이브 링
            GameObject hitObj = new GameObject("Player1_PrismLaserHit", typeof(SpriteRenderer));
            hitObj.transform.position = worldPos;
            SpriteRenderer sr = hitObj.GetComponent<SpriteRenderer>();
            sr.sprite = hitSprite;
            sr.sortingLayerID = sortingLayerID;
            sr.sortingOrder = sortingOrder;

            // 서브 삼각 쇼크웨이브 링
            GameObject ringObj = new GameObject("Player1_TriangleShockRing", typeof(SpriteRenderer));
            ringObj.transform.position = worldPos;
            SpriteRenderer ringSr = ringObj.GetComponent<SpriteRenderer>();
            ringSr.sprite = playerSpecificAuraSprites[0];
            ringSr.sortingLayerID = sortingLayerID;
            ringSr.sortingOrder = sortingOrder - 1;

            float dur = Mathf.Max(0.18f, slashDuration * 1.05f);
            float el = 0f;
            while (el < dur)
            {
                el += Time.deltaTime;
                float t = Mathf.Clamp01(el / dur);
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);

                if (hitObj != null)
                {
                    float sc = Mathf.Lerp(0.45f, 1.35f, easeOut) * critBoost;
                    hitObj.transform.localScale = Vector3.one * sc;
                    hitObj.transform.rotation = Quaternion.Euler(0f, 0f, t * 60f);
                    sr.color = new Color(1f, 1f, 1f, 1f - t * t);
                }

                if (ringObj != null)
                {
                    float rSc = Mathf.Lerp(0.3f, 1.65f, easeOut) * critBoost;
                    ringObj.transform.localScale = Vector3.one * rSc;
                    ringObj.transform.rotation = Quaternion.Euler(0f, 0f, -t * 90f);
                    ringSr.color = new Color(0.55f, 0.96f, 1f, (1f - t) * 0.85f);
                }

                yield return null;
            }

            if (hitObj != null) Destroy(hitObj);
            if (ringObj != null) Destroy(ringObj);
        }
        else if (idx == 1)
        {
            // [Player_2 전용 타격]: 에메랄드 초승달 소닉 블레이드 3연속 고속 질풍 연격!
            float[] slashAngles = { -28f, 52f, -72f };
            Vector3[] slashOffsets =
            {
                new Vector3(-0.15f, 0.12f, 0f),
                new Vector3(0.16f, -0.10f, 0f),
                Vector3.zero
            };

            for (int hit = 0; hit < 3; hit++)
            {
                StartCoroutine(CoSingleSonicFlurrySlash(
                    hitSprite,
                    worldPos + slashOffsets[hit],
                    slashAngles[hit],
                    critBoost * (hit == 2 ? 1.18f : 0.95f),
                    sortingLayerID,
                    sortingOrder + hit
                ));
                yield return new WaitForSeconds(0.055f);
            }
        }
        else
        {
            // [Player_3 전용 타격]: 5방향 크림슨 펜타그램 플라즈마 노바 폭발 & 오각 크래시 쇼크웨이브
            GameObject novaObj = new GameObject("Player3_BusterNovaHit", typeof(SpriteRenderer));
            novaObj.transform.position = worldPos;
            SpriteRenderer novaSr = novaObj.GetComponent<SpriteRenderer>();
            novaSr.sprite = hitSprite;
            novaSr.sortingLayerID = sortingLayerID;
            novaSr.sortingOrder = sortingOrder + 1;

            GameObject shockObj = new GameObject("Player3_PentagonShockwave", typeof(SpriteRenderer));
            shockObj.transform.position = worldPos;
            SpriteRenderer shockSr = shockObj.GetComponent<SpriteRenderer>();
            shockSr.sprite = playerSpecificAuraSprites[2];
            shockSr.sortingLayerID = sortingLayerID;
            shockSr.sortingOrder = sortingOrder;

            float dur = Mathf.Max(0.22f, slashDuration * 1.28f);
            float el = 0f;
            while (el < dur)
            {
                el += Time.deltaTime;
                float t = Mathf.Clamp01(el / dur);
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);

                if (novaObj != null)
                {
                    float punch = t < 0.25f
                        ? Mathf.Lerp(0.35f, 1.48f, t / 0.25f)
                        : Mathf.Lerp(1.48f, 1.15f, (t - 0.25f) / 0.75f);
                    novaObj.transform.localScale = Vector3.one * (punch * critBoost);
                    novaObj.transform.rotation = Quaternion.Euler(0f, 0f, t * 28f);
                    novaSr.color = new Color(1f, 1f, 1f, 1f - Mathf.Pow(t, 1.6f));
                }

                if (shockObj != null)
                {
                    float shockScale = Mathf.Lerp(0.4f, 1.85f, easeOut) * critBoost;
                    shockObj.transform.localScale = Vector3.one * shockScale;
                    shockSr.color = new Color(1f, 0.72f, 0.35f, (1f - t) * 0.9f);
                }

                yield return null;
            }

            if (novaObj != null) Destroy(novaObj);
            if (shockObj != null) Destroy(shockObj);
        }
    }

    private IEnumerator CoSingleSonicFlurrySlash(
        Sprite slashSprite,
        Vector3 worldPos,
        float baseAngle,
        float scaleMult,
        int sortingLayerID,
        int sortingOrder)
    {
        GameObject slashObj = new GameObject("Player2_SonicFlurrySlash", typeof(SpriteRenderer));
        slashObj.transform.position = worldPos;
        slashObj.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle);

        SpriteRenderer sr = slashObj.GetComponent<SpriteRenderer>();
        sr.sprite = slashSprite;
        sr.sortingLayerID = sortingLayerID;
        sr.sortingOrder = sortingOrder;

        float dur = 0.19f;
        float el = 0f;
        while (el < dur)
        {
            if (slashObj == null) yield break;
            el += Time.deltaTime;
            float t = Mathf.Clamp01(el / dur);

            float s = Mathf.Lerp(0.55f, 1.32f, Mathf.Sin(t * Mathf.PI * 0.5f)) * scaleMult;
            slashObj.transform.localScale = new Vector3(s, s, 1f);
            slashObj.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle + t * 24f);

            Color c = Color.white;
            c.a = 1f - (t * t);
            sr.color = c;
            yield return null;
        }

        if (slashObj != null)
            Destroy(slashObj);
    }

    public void PlayEnemyHit(float delay = 0f)
    {
        FindTargetsIfNeeded();
        PlayEnemyHit(enemyRenderer, delay);
    }

    public void PlayEnemyHit(SpriteRenderer targetEnemySr, float delay = 0f)
    {
        EnsureRuntimeEffectSprites();
        if (targetEnemySr == null) return;

        SelectableUnit unit = targetEnemySr.GetComponent<SelectableUnit>();
        Vector3 basePos = unit != null ? unit.BaseLocalPosition : targetEnemySr.transform.localPosition;
        Vector3 baseScale = unit != null ? unit.CurrentTargetScale : targetEnemySr.transform.localScale;
        Color baseCol = unit != null ? unit.BaseColor : targetEnemySr.color;

        StartCoroutine(CoEnemyHitDelayed(targetEnemySr, basePos, baseScale, baseCol, delay));
    }

    private IEnumerator CoEnemyHitDelayed(SpriteRenderer targetEnemySr, Vector3 basePos, Vector3 baseScale, Color baseCol, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (targetEnemySr == null) yield break;

        StartCoroutine(CoPlayPlayerSpecificHitEffect(0, ECardColor.ARTS, false, targetEnemySr.transform.position, targetEnemySr.sortingLayerID, targetEnemySr.sortingOrder + 2));
        yield return StartCoroutine(CoHitShakeOnUnit(targetEnemySr, basePos, baseScale, baseCol, +1f));
    }

    public void PlayPlayerHit()
    {
        FindTargetsIfNeeded();
        if (playerRenderer == null) return;

        SelectableUnit unit = playerRenderer.GetComponent<SelectableUnit>();
        Vector3 basePos = unit != null ? unit.BaseLocalPosition : playerRenderer.transform.localPosition;
        Vector3 baseScale = unit != null ? unit.CurrentTargetScale : playerRenderer.transform.localScale;
        Color baseCol = unit != null ? unit.BaseColor : playerRenderer.color;

        StartCoroutine(CoHitShakeOnUnit(playerRenderer, basePos, baseScale, baseCol, -1f));
    }

    private IEnumerator CoHitShakeOnUnit(
        SpriteRenderer targetSr,
        Vector3 originPos,
        Vector3 originScale,
        Color originColor,
        float knockbackDirX)
    {
        if (targetSr == null) yield break;

        if (activeShakeCoroutines.TryGetValue(targetSr, out Coroutine prevCo) && prevCo != null)
        {
            StopCoroutine(prevCo);
            ResetTargetState(targetSr, originPos, originScale, originColor);
        }

        Coroutine currentCo = StartCoroutine(CoHitShakeAnimation(targetSr, originPos, originScale, originColor, knockbackDirX));
        activeShakeCoroutines[targetSr] = currentCo;
        yield return currentCo;
        activeShakeCoroutines.Remove(targetSr);
    }

    private IEnumerator CoHitShakeAnimation(
        SpriteRenderer targetSr,
        Vector3 originPos,
        Vector3 originScale,
        Color originColor,
        float knockbackDirX)
    {
        SpriteRenderer leftGhost = null;
        SpriteRenderer rightGhost = null;
        if (hitReactionStyle == EHitReactionStyle.CyberGlitchSplit)
        {
            leftGhost = CreateGlitchGhost(targetSr, new Color(1f, 0.15f, 0.35f, 0.65f));
            rightGhost = CreateGlitchGhost(targetSr, new Color(0.15f, 0.9f, 1f, 0.65f));
        }

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            if (targetSr == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shakeDuration);
            float damper = 1f - t;

            float shakeOffsetX = Mathf.Sin(elapsed * shakeFrequency) * shakeMagnitude * damper;

            if (hitReactionStyle == EHitReactionStyle.HorizontalShakeOnly)
            {
                targetSr.transform.localPosition = originPos + new Vector3(shakeOffsetX, 0f, 0f);
            }
            else if (hitReactionStyle == EHitReactionStyle.ShakeWithFlashAndKnockback)
            {
                float knockback = Mathf.Sin(t * Mathf.PI) * knockbackDistance * knockbackDirX * damper;
                targetSr.transform.localPosition = originPos + new Vector3(shakeOffsetX + knockback, 0f, 0f);

                float flashT = Mathf.Clamp01(1f - (t / 0.30f));
                targetSr.color = Color.Lerp(originColor, Color.white, flashT * 0.7f);
            }
            else if (hitReactionStyle == EHitReactionStyle.CyberGlitchSplit)
            {
                targetSr.transform.localPosition = originPos + new Vector3(shakeOffsetX, 0f, 0f);

                if (leftGhost != null && rightGhost != null)
                {
                    float splitDist = 1.5f * damper;
                    leftGhost.transform.localPosition = new Vector3(-splitDist, 0f, 0f);
                    rightGhost.transform.localPosition = new Vector3(splitDist, 0f, 0f);
                    leftGhost.color = new Color(1f, 0.15f, 0.35f, 0.65f * damper);
                    rightGhost.color = new Color(0.15f, 0.9f, 1f, 0.65f * damper);
                }
            }

            yield return null;
        }

        if (leftGhost != null) Destroy(leftGhost.gameObject);
        if (rightGhost != null) Destroy(rightGhost.gameObject);

        SelectableUnit unit = targetSr != null ? targetSr.GetComponent<SelectableUnit>() : null;
        Vector3 finalScale = unit != null ? unit.CurrentTargetScale : originScale;
        ResetTargetState(targetSr, originPos, finalScale, originColor);
    }

    private SpriteRenderer CreateGlitchGhost(SpriteRenderer source, Color ghostColor)
    {
        GameObject ghostObj = new GameObject("GlitchGhost", typeof(SpriteRenderer));
        ghostObj.transform.SetParent(source.transform, false);
        ghostObj.transform.localPosition = Vector3.zero;
        ghostObj.transform.localScale = Vector3.one;

        SpriteRenderer sr = ghostObj.GetComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.color = ghostColor;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder - 1;
        return sr;
    }

    private void ResetTargetState(SpriteRenderer sr, Vector3 pos, Vector3 scale, Color col)
    {
        if (sr == null) return;
        sr.transform.localPosition = pos;
        sr.transform.localScale = scale;
        sr.color = col;
    }
}
