using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum EHitReactionStyle
{
    HorizontalShakeOnly = 0,        // 기본: 좌우로 흔들리는 애니메이션
    ShakeWithFlashAndKnockback = 1, // 좌우 흔들림 + 순간 점멸 + 살짝 밀렸다 복귀
    CyberGlitchSplit = 2            // 좌우 흔들림 + 네온 잔상 분리
}

/// <summary>
/// 전투 이펙트 매니저:
/// 1. 플레이어 공격: 공격 카드를 사용한 플레이어 캐릭터(Player_1~3 모두 가능) 뒤에서 해당 카드 색상(RED/BLUE/GREEN)의 오라가 재생됨
/// 2. 적 피격: 지정된 적(Enemy_1~3) 위에 단일 참격(Slash) 이펙트와 좌우 흔들리는 효과 재생
/// 3. 플레이어 피격: 적 턴에 랜덤 지정된 플레이어 캐릭터(Player_1~3)가 좌우로 흔들리는 피격 모션 재생
/// </summary>
public class CombatEffectManager : Singleton<CombatEffectManager>
{
    [Header("전투 대상 기본 참조 (단독 테스트용)")]
    [SerializeField] private SpriteRenderer playerRenderer;
    [SerializeField] private SpriteRenderer enemyRenderer;

    [Header("1. 플레이어 공격 오라 (빨강/파랑/초록 - 모든 플레이어 캐릭터 뒤에서 연출 가능)")]
    [SerializeField] private Sprite[] playerAuraSprites = new Sprite[3]; // 0: RED, 1: BLUE, 2: GREEN
    [SerializeField] private float auraDuration = 0.48f;                 // 오라가 서서히 커졌다 사라지는 시간
    [SerializeField] private float auraBaseScale = 4.2f;                 // 플레이어 뒤에 깔리는 오라 기본 크기
    [SerializeField] private float auraExpandMultiplier = 1.38f;         // 오라가 퍼지는 최대 배율

    [Header("2. 적 피격 참격 이펙트 (색상 구분 없는 단일 공통 참격)")]
    [SerializeField] private Sprite enemyHitSlashSprite;                 // 단일 참격 스프라이트
    [SerializeField] private float slashDuration = 0.20f;                // 짧고 간결한 참격 재생 시간 (초)

    [Header("3. 피격 흔들림 애니메이션 설정 (플레이어 피격 / 적 피격 공통)")]
    [SerializeField] private EHitReactionStyle hitReactionStyle = EHitReactionStyle.HorizontalShakeOnly;
    [SerializeField] private float shakeDuration = 0.32f;                // 흔들리는 총 시간 (초)
    [SerializeField] private float shakeMagnitude = 0.28f;               // 좌우로 흔들리는 폭 (로컬/월드 좌표 단위)
    [SerializeField] private float shakeFrequency = 45f;                 // 좌우 진동 속도
    [SerializeField] private float knockbackDistance = 0.35f;            // (옵션) 넉백 거리

    [Header("테스트 단축키 (Play 모드 확인용)")]
    [SerializeField] private bool enableTestHotkeys = false;

    private readonly Dictionary<SpriteRenderer, Coroutine> activeShakeCoroutines = new Dictionary<SpriteRenderer, Coroutine>();

    public float AuraDuration => auraDuration;
    public float ShakeDuration => shakeDuration;

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
        if (playerAuraSprites == null || playerAuraSprites.Length < 3)
            playerAuraSprites = new Sprite[3];

        for (int i = 0; i < 3; i++)
        {
            if (playerAuraSprites[i] == null)
            {
                Texture2D tex = CombatEffectTextureGenerator.CreatePlayerCommonAuraTexture((ECardColor)i, 256);
                playerAuraSprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        if (enemyHitSlashSprite == null)
        {
            Texture2D slashTex = CombatEffectTextureGenerator.CreateSimpleHitSlashTexture(256);
            enemyHitSlashSprite = Sprite.Create(slashTex, new Rect(0, 0, slashTex.width, slashTex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    // =========================================================================
    // Attack 페이즈 전용: 특정 플레이어의 색상별 공격 오라 + 지정된 적의 참격 및 흔들림 연출
    // =========================================================================
    public IEnumerator PlayCardAttackSequence(SelectableUnit attackerPlayer, ECardColor cardColor, SelectableUnit targetEnemy)
    {
        EnsureRuntimeEffectSprites();

        SpriteRenderer attackerSr = attackerPlayer != null ? attackerPlayer.SpriteRenderer : playerRenderer;
        SpriteRenderer targetEnemySr = targetEnemy != null ? targetEnemy.SpriteRenderer : enemyRenderer;

        // 1) 공격하는 플레이어 캐릭터 뒤에서 카드 색상에 맞는 공격 오라 재생
        if (attackerSr != null)
        {
            StartCoroutine(CoPlayerBackAura(attackerSr, cardColor));
        }

        // 오라가 피어오르기 시작하는 짧은 타이밍(0.12초) 뒤 적 피격 연출 동시 실행
        yield return new WaitForSeconds(0.12f);

        // 2) 지정된 적 오브젝트에 피격 참격 이펙트 + 좌우 흔들리는 효과 재생
        if (targetEnemySr != null)
        {
            StartCoroutine(CoPlaySimpleSlash(targetEnemySr.transform.position, targetEnemySr.sortingLayerID, targetEnemySr.sortingOrder + 2));

            Vector3 basePos = targetEnemy != null ? targetEnemy.BaseLocalPosition : targetEnemySr.transform.localPosition;
            Vector3 baseScale = targetEnemy != null ? targetEnemy.CurrentTargetScale : targetEnemySr.transform.localScale;
            Color baseCol = targetEnemy != null ? targetEnemy.BaseColor : targetEnemySr.color;

            yield return StartCoroutine(CoHitShakeOnUnit(targetEnemySr, basePos, baseScale, baseCol, +1f));
        }
        else
        {
            yield return new WaitForSeconds(shakeDuration);
        }
    }

    // =========================================================================
    // Enemy 턴 전용: 공격 이펙트 없이 피격받는 플레이어만 좌우로 흔들리는 피격 모션 재생
    // =========================================================================
    public IEnumerator PlayPlayerHitSequenceOnUnit(SelectableUnit targetPlayer)
    {
        if (targetPlayer == null || targetPlayer.SpriteRenderer == null)
            yield break;

        SpriteRenderer targetSr = targetPlayer.SpriteRenderer;
        Vector3 basePos = targetPlayer.BaseLocalPosition;
        Vector3 baseScale = targetPlayer.CurrentTargetScale;
        Color baseCol = targetPlayer.BaseColor;

        yield return StartCoroutine(CoHitShakeOnUnit(targetSr, basePos, baseScale, baseCol, -1f));
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
        StartCoroutine(CoPlayerBackAura(targetPlayerSr, auraColor));
    }

    private IEnumerator CoPlayerBackAura(SpriteRenderer targetPlayerSr, ECardColor auraColor)
    {
        if (targetPlayerSr == null) yield break;

        int idx = Mathf.Clamp((int)auraColor, 0, 2);

        // 공격하는 플레이어 캐릭터 뒤(sortingOrder - 1)에 오라 오브젝트 생성 (모든 플레이어 캐릭터 공통 지원)
        GameObject auraObj = new GameObject($"PlayerBackAura_{targetPlayerSr.name}_{auraColor}", typeof(SpriteRenderer));
        auraObj.transform.SetParent(targetPlayerSr.transform, false);
        auraObj.transform.localPosition = Vector3.zero;
        auraObj.transform.localScale = Vector3.one * auraBaseScale;

        SpriteRenderer auraSr = auraObj.GetComponent<SpriteRenderer>();
        auraSr.sprite = playerAuraSprites[idx];
        auraSr.sortingLayerID = targetPlayerSr.sortingLayerID;
        auraSr.sortingOrder = targetPlayerSr.sortingOrder - 1; // 반드시 해당 플레이어 캐릭터 뒤에 그려지도록 설정

        float elapsed = 0f;
        while (elapsed < auraDuration)
        {
            if (auraObj == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / auraDuration);

            // 부드럽게 밝아졌다가 사라지는 사인 곡선 (0 -> 1 -> 0)
            float alphaCurve = Mathf.Sin(t * Mathf.PI);

            Color c = Color.white;
            c.a = alphaCurve;
            auraSr.color = c;

            float currentScale = Mathf.Lerp(auraBaseScale * 0.85f, auraBaseScale * auraExpandMultiplier, t);
            auraObj.transform.localScale = Vector3.one * currentScale;

            yield return null;
        }

        if (auraObj != null)
            Destroy(auraObj);
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

        StartCoroutine(CoPlaySimpleSlash(targetEnemySr.transform.position, targetEnemySr.sortingLayerID, targetEnemySr.sortingOrder + 2));
        yield return StartCoroutine(CoHitShakeOnUnit(targetEnemySr, basePos, baseScale, baseCol, +1f));
    }

    private IEnumerator CoPlaySimpleSlash(Vector3 worldPos, int sortingLayerID, int sortingOrder)
    {
        GameObject slashObj = new GameObject("EnemyHitSlash", typeof(SpriteRenderer));
        slashObj.transform.position = worldPos;

        float randomAngle = Random.Range(-18f, 18f);
        slashObj.transform.rotation = Quaternion.Euler(0f, 0f, randomAngle);

        SpriteRenderer sr = slashObj.GetComponent<SpriteRenderer>();
        sr.sprite = enemyHitSlashSprite;
        sr.sortingLayerID = sortingLayerID;
        sr.sortingOrder = sortingOrder;

        float elapsed = 0f;
        while (elapsed < slashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slashDuration);

            float lengthScale = Mathf.Lerp(0.55f, 1.55f, Mathf.Sin(t * Mathf.PI * 0.5f));
            float widthScale = Mathf.Lerp(1.25f, 0.25f, t);
            slashObj.transform.localScale = new Vector3(lengthScale, widthScale, 1f);

            Color c = Color.white;
            c.a = 1f - (t * t);
            sr.color = c;

            yield return null;
        }

        Destroy(slashObj);
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
