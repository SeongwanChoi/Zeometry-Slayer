using UnityEngine;

/// <summary>
/// 밝은 톤의 사이버 시티 + 원근 하이웨이 동적 배경 Quad 셰이더와 기하학 네온 파티클(다이아몬드/삼각형/사각형)을 제어합니다.
/// BattleManager와 연동되어 Player 턴(밝은 시안 블루) ↔ Enemy 턴(밝은 크림슨 레드) 전환만 부드럽게 반영하며,
/// Attack 페이즈 전환·먼지 파티클·캐릭터 흔들림 연출은 포함하지 않습니다.
/// </summary>
public class CyberCityBackgroundController : MonoBehaviour
{
    private static readonly int HorizonYId = Shader.PropertyToID("_HorizonY");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
    private static readonly int ScrollTimeId = Shader.PropertyToID("_ScrollTime");
    private static readonly int UseCustomTimeId = Shader.PropertyToID("_UseCustomTime");
    private static readonly int EnemyBlendId = Shader.PropertyToID("_EnemyBlend");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int StreakCountId = Shader.PropertyToID("_StreakCount");

    [Header("배경 Quad 및 기하학 네온 파티클 참조")]
    [SerializeField] private MeshRenderer backgroundQuadRenderer;
    [SerializeField] private ParticleSystem[] geometricParticleSystems;

    [Header("스크롤 & 스트릭 & 밝기 설정")]
    [SerializeField] private float horizonY = 0.63f;
    [SerializeField] private float scrollSpeed = 1.0f;
    [SerializeField] private float streakCount = 7.0f;
    [SerializeField] private float brightness = 1.25f;
    [SerializeField] private float phaseBlendSpeed = 3.5f;

    [Header("턴 페이즈별 기하학 파티클 색상 (Player ↔ Enemy)")]
    [SerializeField] private Color playerParticleColor = new Color(0.58f, 0.97f, 1.0f, 0.34f);
    [SerializeField] private Color enemyParticleColor = new Color(1.0f, 0.56f, 0.68f, 0.36f);

    private MaterialPropertyBlock propertyBlock;
    private float currentEnemyBlend = 0f;
    private float targetEnemyBlend = 0f;
    private float scrollTimeAccum = 0f;

    private void Awake()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        FitQuadToCamera();
        SyncTargetBlendFromBattleManager();
        currentEnemyBlend = targetEnemyBlend;
        ApplyShaderAndParticles();
    }

    private void OnEnable()
    {
        BattleManager.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        SyncTargetBlendFromBattleManager();
    }

    private void OnDisable()
    {
        BattleManager.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
    }

    private void Start()
    {
        FitQuadToCamera();
        SyncTargetBlendFromBattleManager();
        currentEnemyBlend = targetEnemyBlend;
        ApplyShaderAndParticles();
    }

    private void Update()
    {
        if (BattleManager.Ins != null)
        {
            if (BattleManager.Ins.CurrentTurn == ETurn.ENEMY)
                targetEnemyBlend = 1f;
            else if (BattleManager.Ins.CurrentTurn == ETurn.PLAYER)
                targetEnemyBlend = 0f;
            // Attack 페이즈는 배경 색상을 변경하지 않습니다.
        }

        currentEnemyBlend = Mathf.MoveTowards(currentEnemyBlend, targetEnemyBlend, Time.deltaTime * phaseBlendSpeed);
        scrollTimeAccum += Time.deltaTime * scrollSpeed;

        FitQuadToCamera();
        ApplyShaderAndParticles();
    }

    private void HandleTurnPhaseChanged(ETurn turn)
    {
        if (turn == ETurn.ENEMY)
            targetEnemyBlend = 1f;
        else if (turn == ETurn.PLAYER)
            targetEnemyBlend = 0f;
    }

    private void SyncTargetBlendFromBattleManager()
    {
        if (BattleManager.Ins != null && BattleManager.Ins.CurrentTurn == ETurn.ENEMY)
            targetEnemyBlend = 1f;
        else
            targetEnemyBlend = 0f;
    }

    private void FitQuadToCamera()
    {
        if (backgroundQuadRenderer == null) return;

        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float worldH = cam.orthographicSize * 2f;
        float worldW = worldH * cam.aspect;

        Transform quadTr = backgroundQuadRenderer.transform;
        quadTr.localPosition = new Vector3(cam.transform.position.x - transform.position.x, cam.transform.position.y - transform.position.y, 0f);
        quadTr.localScale = new Vector3(worldW * 1.04f, worldH * 1.04f, 1f);
    }

    private void ApplyShaderAndParticles()
    {
        if (backgroundQuadRenderer != null)
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            Camera cam = Camera.main;
            float aspect = (cam != null && cam.aspect > 0.1f) ? cam.aspect : 1.7778f;

            backgroundQuadRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(HorizonYId, horizonY);
            propertyBlock.SetFloat(AspectId, aspect);
            propertyBlock.SetFloat(ScrollSpeedId, scrollSpeed);
            propertyBlock.SetFloat(ScrollTimeId, scrollTimeAccum);
            propertyBlock.SetFloat(UseCustomTimeId, 1f);
            propertyBlock.SetFloat(EnemyBlendId, currentEnemyBlend);
            propertyBlock.SetFloat(BrightnessId, brightness);
            propertyBlock.SetFloat(StreakCountId, streakCount);
            backgroundQuadRenderer.SetPropertyBlock(propertyBlock);
        }

        if (geometricParticleSystems != null)
        {
            Color activeParticleCol = Color.Lerp(playerParticleColor, enemyParticleColor, currentEnemyBlend);
            for (int i = 0; i < geometricParticleSystems.Length; i++)
            {
                ParticleSystem ps = geometricParticleSystems[i];
                if (ps == null) continue;

                var main = ps.main;
                main.startColor = activeParticleCol;
            }
        }
    }

    /// <summary>
    /// 에디터 자동 세팅 시 디스크(.png)로 영구 저장할 Player/Enemy 스타일 네온 도형 파티클 텍스처를 생성합니다.
    /// shapeType: 0 = Triangle (Player_1), 1 = Octagon (Player_2), 2 = Pentagon (Player_3), 3 = Square (Enemy)
    /// </summary>
    public static Texture2D CreateGeometricParticleTexture(int shapeType, int size = 128)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        int sides;
        float radius;
        float baseAngle;
        Vector2 centerOffset = Vector2.zero;

        switch (shapeType)
        {
            case 0: // Triangle (Player_1)
                sides = 3;
                radius = 0.72f;
                baseAngle = Mathf.PI * 0.5f;
                centerOffset = new Vector2(0f, -0.11f);
                break;
            case 1: // Octagon (Player_2)
                sides = 8;
                radius = 0.72f;
                baseAngle = Mathf.PI * 0.5f;
                break;
            case 2: // Pentagon (Player_3)
                sides = 5;
                radius = 0.70f;
                baseAngle = Mathf.PI * 0.5f;
                centerOffset = new Vector2(0f, -0.04f);
                break;
            default: // Square (Enemy)
                sides = 4;
                radius = 0.78f;
                baseAngle = Mathf.PI * 0.25f;
                break;
        }

        Vector2[] verts = new Vector2[sides];
        float angleStep = (Mathf.PI * 2f) / sides;
        for (int i = 0; i < sides; i++)
        {
            float a = baseAngle + i * angleStep;
            verts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius) + centerOffset;
        }

        Color[] pixels = new Color[size * size];
        float inv = 1f / (size - 1);

        for (int y = 0; y < size; y++)
        {
            float py = (y * inv - 0.5f) * 2f;
            for (int x = 0; x < size; x++)
            {
                float px = (x * inv - 0.5f) * 2f;
                Vector2 p = new Vector2(px, py);

                float minDist = float.MaxValue;
                bool isInside = true;

                for (int i = 0; i < sides; i++)
                {
                    Vector2 a = verts[i];
                    Vector2 b = verts[(i + 1) % sides];
                    Vector2 ab = b - a;
                    Vector2 ap = p - a;

                    float cross = ab.x * ap.y - ab.y * ap.x;
                    if (cross < 0f)
                        isInside = false;

                    float edgeLenSq = Vector2.Dot(ab, ab);
                    float h = edgeLenSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / edgeLenSq) : 0f;
                    float dist = Vector2.Distance(ap, ab * h);
                    if (dist < minDist)
                        minDist = dist;
                }

                // Player/Enemy 스타일: 얇은 백색 네온 튜브 코어 + 바깥/안쪽 네온 오라 + 완전히 뚫린 중앙 빈 공간
                float core = 1f - SmoothStep01(0.032f, 0.062f, minDist);
                float glow;
                if (isInside)
                {
                    glow = Mathf.Exp(-Mathf.Max(0f, minDist - 0.032f) / 0.055f) * 0.58f * Mathf.Clamp01(1f - minDist / 0.18f);
                }
                else
                {
                    glow = Mathf.Exp(-Mathf.Max(0f, minDist - 0.032f) / 0.095f) * 0.68f * Mathf.Clamp01(1f - minDist / 0.34f);
                }

                float alpha = Mathf.Clamp01(Mathf.Max(core, glow));
                float edgeFade = Mathf.Clamp01((0.97f - Mathf.Max(Mathf.Abs(px), Mathf.Abs(py))) * 8f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * edgeFade);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static float SmoothStep01(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / Mathf.Max(1e-6f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
