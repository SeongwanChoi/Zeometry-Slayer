using UnityEngine;

/// <summary>
/// 각 플레이어(Player_1, Player_2, Player_3) 전용 공격 차징 오라 및 전용 타격 임팩트 이펙트 텍스처 생성기:
/// - Player_1 (파란 삼각형 / Arts 특화):
///   * 오라: 이중 정삼각형 네온 프리즘 마법진 오라 (CreatePlayer1TriangleAuraTexture)
///   * 타격: 3방향 트라이앵글 프리즘 레이저 빔 & 삼각 쇼크웨이브 (CreatePlayer1PrismLaserHitTexture)
/// - Player_2 (초록 팔각형 / Quick 특화):
///   * 오라: 고속 회전 팔각형 소닉 볼텍스 링 오라 (CreatePlayer2OctagonAuraTexture)
///   * 타격: 에메랄드 쌍검 초승달 소닉 블레이드 & 팔각 섬광 (CreatePlayer2SonicFlurryHitTexture)
/// - Player_3 (빨간 오각형 / Buster 특화):
///   * 오라: 오각형 아머 & 펜타그램(5각 별) 플라즈마 리액터 오라 (CreatePlayer3PentagonAuraTexture)
///   * 타격: 5방향 크림슨 펜타그램 플라즈마 노바 폭발 & 오각 크래시 (CreatePlayer3BusterNovaHitTexture)
/// </summary>
public static class CombatEffectTextureGenerator
{
    public static void GetColorPalette(ECardColor colorType, out Color deepGlow, out Color midNeon, out Color brightCore)
    {
        switch (colorType)
        {
            case ECardColor.RED:
            case ECardColor.SUPER_BUSTER:
                deepGlow = new Color(0.88f, 0.04f, 0.14f, 1f);
                midNeon = new Color(1.0f, 0.26f, 0.18f, 1f);
                brightCore = new Color(1.0f, 0.92f, 0.78f, 1f);
                break;

            case ECardColor.GREEN:
                deepGlow = new Color(0.02f, 0.72f, 0.38f, 1f);
                midNeon = new Color(0.15f, 1.0f, 0.62f, 1f);
                brightCore = new Color(0.88f, 1.0f, 0.95f, 1f);
                break;

            case ECardColor.BLUE:
            default:
                deepGlow = new Color(0.04f, 0.38f, 0.98f, 1f);
                midNeon = new Color(0.12f, 0.82f, 1.0f, 1f);
                brightCore = new Color(0.90f, 0.99f, 1.0f, 1f);
                break;
        }
    }

    /// <summary>
    /// 플레이어 인덱스(0: Player_1 삼각형, 1: Player_2 팔각형, 2: Player_3 오각형)에 맞는 전용 공격 오라 텍스처를 생성합니다.
    /// </summary>
    public static Texture2D CreatePlayerSpecificAuraTexture(int playerIndex, int size = 256)
    {
        switch (Mathf.Clamp(playerIndex, 0, 2))
        {
            case 0:  return CreatePlayer1TriangleAuraTexture(size);
            case 1:  return CreatePlayer2OctagonAuraTexture(size);
            default: return CreatePlayer3PentagonAuraTexture(size);
        }
    }

    /// <summary>
    /// 플레이어 인덱스(0: Player_1, 1: Player_2, 2: Player_3)에 맞는 전용 타격 임팩트 텍스처를 생성합니다.
    /// </summary>
    public static Texture2D CreatePlayerSpecificHitTexture(int playerIndex, int size = 256)
    {
        switch (Mathf.Clamp(playerIndex, 0, 2))
        {
            case 0:  return CreatePlayer1PrismLaserHitTexture(size);
            case 1:  return CreatePlayer2SonicFlurryHitTexture(size);
            default: return CreatePlayer3BusterNovaHitTexture(size);
        }
    }

    // =========================================================================
    // [Player_1 전용] 파란 삼각형 프리즘 마법진 오라 & 트라이앵글 프리즘 레이저 타격
    // =========================================================================
    public static Texture2D CreatePlayer1TriangleAuraTexture(int size = 256)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        Color deepBlue = new Color(0.04f, 0.35f, 0.96f, 1f);
        Color cyanNeon = new Color(0.12f, 0.86f, 1.0f, 1f);
        Color whiteCore = new Color(0.92f, 0.99f, 1.0f, 1f);

        for (int y = 0; y < size; y++)
        {
            float py = ((y + 0.5f) / size - 0.5f) * 2f;
            for (int x = 0; x < size; x++)
            {
                float px = ((x + 0.5f) / size - 0.5f) * 2f;
                Vector2 p = new Vector2(px, py);
                float r = p.magnitude;
                Color col = Color.clear;

                if (r < 0.96f)
                {
                    // 배경 시안 글로우
                    float radial = Mathf.Clamp01(1f - r / 0.92f);
                    Color bg = Color.Lerp(deepBlue, cyanNeon, radial * 0.6f);
                    bg.a = Mathf.Pow(radial, 1.8f) * 0.65f;
                    col = AlphaBlend(col, bg);

                    // 정방향 정삼각형 네온 라인 (R = 0.56)
                    float triDist1 = Mathf.Abs(SignedDistRegularPolygon(p, 3, 0.52f, Mathf.PI * 0.5f));
                    AddNeonLine(ref col, triDist1, 0.022f, 0.095f, deepBlue, cyanNeon, whiteCore, 0.95f);

                    // 역방향 서브 정삼각형 (육망성 프리즘 구조, R = 0.44)
                    float triDist2 = Mathf.Abs(SignedDistRegularPolygon(p, 3, 0.42f, -Mathf.PI * 0.5f));
                    AddNeonLine(ref col, triDist2, 0.014f, 0.065f, deepBlue, cyanNeon, whiteCore, 0.78f);

                    // 외곽 사이버 테크 링 (R = 0.76)
                    float angle = Mathf.Atan2(py, px);
                    float dash = Mathf.Sin(angle * 6f);
                    if (dash > -0.2f)
                    {
                        float ringDist = Mathf.Abs(r - 0.76f);
                        AddNeonLine(ref col, ringDist, 0.012f, 0.05f, deepBlue, cyanNeon, whiteCore, 0.75f);
                    }
                }

                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    public static Texture2D CreatePlayer1PrismLaserHitTexture(int size = 256)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        Color deepBlue = new Color(0.05f, 0.40f, 1.0f, 1f);
        Color cyanNeon = new Color(0.20f, 0.90f, 1.0f, 1f);
        Color whiteCore = Color.white;

        for (int y = 0; y < size; y++)
        {
            float py = ((y + 0.5f) / size - 0.5f) * 2f;
            for (int x = 0; x < size; x++)
            {
                float px = ((x + 0.5f) / size - 0.5f) * 2f;
                Vector2 p = new Vector2(px, py);
                float r = p.magnitude;
                Color col = Color.clear;

                // 1) 중앙 정삼각형 프리즘 충격파 코어
                float triCoreDist = Mathf.Abs(SignedDistRegularPolygon(p, 3, 0.36f, Mathf.PI * 0.5f));
                AddNeonLine(ref col, triCoreDist, 0.026f, 0.11f, deepBlue, cyanNeon, whiteCore, 0.96f);

                // 2) 3방향(60도 간격)으로 날카롭게 교차 관통하는 프리즘 레이저 광선 3줄
                for (int i = 0; i < 3; i++)
                {
                    float beamAngle = (i * 60f + 30f) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(beamAngle), Mathf.Sin(beamAngle));
                    float along = Mathf.Abs(Vector2.Dot(p, dir));
                    float perp = Mathf.Abs(-p.x * dir.y + p.y * dir.x);

                    if (along < 0.94f)
                    {
                        float taper = Mathf.Clamp01(1f - (along / 0.94f));
                        float coreW = 0.032f * taper;
                        float glowW = 0.13f * taper;
                        if (perp < glowW)
                        {
                            AddNeonLine(ref col, perp, coreW, glowW, deepBlue, cyanNeon, whiteCore, taper * 0.98f);
                        }
                    }
                }

                // 3) 중심부 고밀도 프리즘 섬광
                if (r < 0.28f)
                {
                    float c = 1f - (r / 0.28f);
                    Color flash = Color.Lerp(cyanNeon, whiteCore, c);
                    flash.a = c * c * 0.95f;
                    col = AlphaBlend(col, flash);
                }

                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // =========================================================================
    // [Player_2 전용] 초록 팔각형 소닉 볼텍스 오라 & 에메랄드 질풍 쌍검 참격
    // =========================================================================
    public static Texture2D CreatePlayer2OctagonAuraTexture(int size = 256)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        Color deepEmerald = new Color(0.02f, 0.68f, 0.36f, 1f);
        Color mintNeon = new Color(0.18f, 1.0f, 0.64f, 1f);
        Color whiteCore = new Color(0.92f, 1.0f, 0.96f, 1f);

        for (int y = 0; y < size; y++)
        {
            float py = ((y + 0.5f) / size - 0.5f) * 2f;
            for (int x = 0; x < size; x++)
            {
                float px = ((x + 0.5f) / size - 0.5f) * 2f;
                Vector2 p = new Vector2(px, py);
                float r = p.magnitude;
                Color col = Color.clear;

                if (r < 0.96f)
                {
                    float radial = Mathf.Clamp01(1f - r / 0.92f);
                    Color bg = Color.Lerp(deepEmerald, mintNeon, radial * 0.55f);
                    bg.a = Mathf.Pow(radial, 1.7f) * 0.62f;
                    col = AlphaBlend(col, bg);

                    // 외곽 팔각형(Octagon) 네온 링 (R = 0.62)
                    float octDist1 = Mathf.Abs(SignedDistRegularPolygon(p, 8, 0.60f, Mathf.PI / 8f));
                    AddNeonLine(ref col, octDist1, 0.022f, 0.085f, deepEmerald, mintNeon, whiteCore, 0.95f);

                    // 내부 회전 팔각형 링 (R = 0.42)
                    float octDist2 = Mathf.Abs(SignedDistRegularPolygon(p, 8, 0.40f, 0f));
                    AddNeonLine(ref col, octDist2, 0.015f, 0.06f, deepEmerald, mintNeon, whiteCore, 0.82f);

                    // 8방향 소닉 윈드 핀(Wind Fins)
                    float angle = Mathf.Atan2(py, px);
                    float spiralAngle = angle + r * 2.2f;
                    float bladeWave = Mathf.Abs(Mathf.Sin(spiralAngle * 4f));
                    if (r > 0.38f && r < 0.84f && bladeWave > 0.92f)
                    {
                        float finFactor = (bladeWave - 0.92f) / 0.08f;
                        float rFade = Mathf.Sin(((r - 0.38f) / 0.46f) * Mathf.PI);
                        Color finCol = Color.Lerp(mintNeon, whiteCore, finFactor);
                        finCol.a = finFactor * rFade * 0.85f;
                        col = AlphaBlend(col, finCol);
                    }
                }

                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    public static Texture2D CreatePlayer2SonicFlurryHitTexture(int size = 256)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        Color deepEmerald = new Color(0.02f, 0.72f, 0.38f, 1f);
        Color mintNeon = new Color(0.22f, 1.0f, 0.66f, 1f);
        Color whiteCore = Color.white;

        for (int y = 0; y < size; y++)
        {
            float py = ((y + 0.5f) / size - 0.5f) * 2f;
            for (int x = 0; x < size; x++)
            {
                float px = ((x + 0.5f) / size - 0.5f) * 2f;
                Vector2 p = new Vector2(px, py);
                Color col = Color.clear;

                // 1) 예리하게 휘어진 초승달 소닉 윈드 커터 (대각선 아크 참격 2겹)
                DrawCurvedCrescentSlash(ref col, p, new Vector2(-0.78f, -0.68f), new Vector2(0.78f, 0.68f), 0.16f, 0.055f, deepEmerald, mintNeon, whiteCore);
                DrawCurvedCrescentSlash(ref col, p, new Vector2(-0.65f, -0.78f), new Vector2(0.82f, 0.48f), -0.14f, 0.038f, deepEmerald, mintNeon, whiteCore);

                // 2) 중앙 팔각형 소닉 링 파동
                float octWave = Mathf.Abs(SignedDistRegularPolygon(p, 8, 0.45f, Mathf.PI / 8f));
                AddNeonLine(ref col, octWave, 0.014f, 0.065f, deepEmerald, mintNeon, whiteCore, 0.85f);

                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // =========================================================================
    // [Player_3 전용] 빨간 오각형 펜타그램 리액터 오라 & 크림슨 플라즈마 노바 폭발
    // =========================================================================
    public static Texture2D CreatePlayer3PentagonAuraTexture(int size = 256)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        Color deepCrimson = new Color(0.88f, 0.03f, 0.14f, 1f);
        Color flameOrange = new Color(1.0f, 0.32f, 0.08f, 1f);
        Color goldWhiteCore = new Color(1.0f, 0.94f, 0.78f, 1f);

        for (int y = 0; y < size; y++)
        {
            float py = ((y + 0.5f) / size - 0.5f) * 2f;
            for (int x = 0; x < size; x++)
            {
                float px = ((x + 0.5f) / size - 0.5f) * 2f;
                Vector2 p = new Vector2(px, py);
                float r = p.magnitude;
                Color col = Color.clear;

                if (r < 0.96f)
                {
                    float radial = Mathf.Clamp01(1f - r / 0.94f);
                    Color bg = Color.Lerp(deepCrimson, flameOrange, radial * 0.65f);
                    bg.a = Mathf.Pow(radial, 1.5f) * 0.72f;
                    col = AlphaBlend(col, bg);

                    // 외곽 육중한 오각형(Pentagon) 네온 림 (R = 0.60)
                    float pentDist = Mathf.Abs(SignedDistRegularPolygon(p, 5, 0.58f, Mathf.PI * 0.5f));
                    AddNeonLine(ref col, pentDist, 0.028f, 0.11f, deepCrimson, flameOrange, goldWhiteCore, 0.98f);

                    // 내부 5각 별(Pentagram Star) 리액터 코어
                    float starDist = Mathf.Abs(SignedDistFivePointStar(p, 0.44f, 0.19f, Mathf.PI * 0.5f));
                    AddNeonLine(ref col, starDist, 0.018f, 0.075f, deepCrimson, flameOrange, goldWhiteCore, 0.92f);
                }

                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    public static Texture2D CreatePlayer3BusterNovaHitTexture(int size = 256)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        Color deepCrimson = new Color(0.92f, 0.02f, 0.14f, 1f);
        Color flameOrange = new Color(1.0f, 0.38f, 0.06f, 1f);
        Color goldWhiteCore = new Color(1.0f, 0.96f, 0.82f, 1f);

        for (int y = 0; y < size; y++)
        {
            float py = ((y + 0.5f) / size - 0.5f) * 2f;
            for (int x = 0; x < size; x++)
            {
                float px = ((x + 0.5f) / size - 0.5f) * 2f;
                Vector2 p = new Vector2(px, py);
                float r = p.magnitude;
                Color col = Color.clear;

                // 1) 거대한 5각 별(Pentagram) 플라즈마 노바 폭발
                float starDist = SignedDistFivePointStar(p, 0.82f, 0.30f, Mathf.PI * 0.5f);
                if (starDist <= 0.12f)
                {
                    if (starDist <= 0f)
                    {
                        float innerT = Mathf.Clamp01(-starDist / 0.25f);
                        Color fill = Color.Lerp(flameOrange, goldWhiteCore, innerT);
                        fill.a = 0.92f;
                        col = AlphaBlend(col, fill);
                    }
                    AddNeonLine(ref col, Mathf.Abs(starDist), 0.028f, 0.12f, deepCrimson, flameOrange, goldWhiteCore, 1.0f);
                }

                // 2) 외곽 오각형 쇼크웨이브 링
                float pentShock = Mathf.Abs(SignedDistRegularPolygon(p, 5, 0.62f, Mathf.PI * 0.5f));
                AddNeonLine(ref col, pentShock, 0.024f, 0.095f, deepCrimson, flameOrange, goldWhiteCore, 0.95f);

                // 3) 5개 꼭짓점 방향으로 뻗어나가는 폭발 플레어 광선
                for (int i = 0; i < 5; i++)
                {
                    float a = Mathf.PI * 0.5f + i * (Mathf.PI * 2f / 5f);
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float dot = Vector2.Dot(p, dir);
                    float perp = Mathf.Abs(-p.x * dir.y + p.y * dir.x);
                    if (dot > 0f && dot < 0.96f)
                    {
                        float taper = 1f - (dot / 0.96f);
                        float w = 0.045f * taper;
                        if (perp < w * 2.5f)
                        {
                            AddNeonLine(ref col, perp, w * 0.6f, w * 2.5f, deepCrimson, flameOrange, goldWhiteCore, taper * 0.95f);
                        }
                    }
                }

                // 4) 중심부 초고온 화이트-골드 폭발 코어
                if (r < 0.34f)
                {
                    float c = 1f - (r / 0.34f);
                    Color core = Color.Lerp(flameOrange, Color.white, c * c);
                    core.a = c * 0.98f;
                    col = AlphaBlend(col, core);
                }

                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // =========================================================================
    // 기하학 도형 SDF 및 네온 블렌딩 유틸리티
    // =========================================================================
    private static float SignedDistRegularPolygon(Vector2 p, int n, float radius, float rotationRad)
    {
        float angle = Mathf.Atan2(p.y, p.x) - rotationRad;
        float sector = Mathf.PI * 2f / n;
        float halfSector = sector * 0.5f;
        float a = Mathf.Repeat(angle + halfSector, sector) - halfSector;
        float r = p.magnitude;
        float apothem = radius * Mathf.Cos(halfSector);
        return r * Mathf.Cos(a) - apothem;
    }

    private static float SignedDistFivePointStar(Vector2 p, float outerR, float innerR, float rotationRad)
    {
        int n = 5;
        float angle = Mathf.Atan2(p.y, p.x) - rotationRad;
        float sector = Mathf.PI * 2f / n;
        float halfSector = sector * 0.5f;
        float a = Mathf.Abs(Mathf.Repeat(angle + halfSector, sector) - halfSector);

        Vector2 vOuter = new Vector2(outerR, 0f);
        Vector2 vInner = new Vector2(innerR * Mathf.Cos(halfSector), innerR * Mathf.Sin(halfSector));
        Vector2 pt = new Vector2(p.magnitude * Mathf.Cos(a), p.magnitude * Mathf.Sin(a));

        Vector2 edge = vInner - vOuter;
        float t = Mathf.Clamp01(Vector2.Dot(pt - vOuter, edge) / edge.sqrMagnitude);
        Vector2 closest = vOuter + edge * t;
        float dist = Vector2.Distance(pt, closest);

        float cross = edge.x * (pt.y - vOuter.y) - edge.y * (pt.x - vOuter.x);
        return cross < 0f ? dist : -dist;
    }

    private static void DrawCurvedCrescentSlash(
        ref Color col,
        Vector2 p,
        Vector2 start,
        Vector2 end,
        float arcBend,
        float thickness,
        Color deepCol,
        Color midCol,
        Color coreCol)
    {
        Vector2 dir = (end - start).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        float totalLen = Vector2.Distance(start, end);

        Vector2 toP = p - start;
        float along = Vector2.Dot(toP, dir);
        if (along < -0.04f || along > totalLen + 0.04f) return;

        float normAlong = Mathf.Clamp01(along / totalLen);
        float taper = Mathf.Sin(normAlong * Mathf.PI);
        float curveOffset = taper * arcBend;
        Vector2 curvePt = start + dir * Mathf.Clamp(along, 0f, totalLen) + normal * curveOffset;

        float dist = Vector2.Distance(p, curvePt);
        float coreW = thickness * taper + 0.002f;
        float glowW = coreW * 3.0f;

        if (dist < glowW)
        {
            AddNeonLine(ref col, dist, coreW, glowW, deepCol, midCol, coreCol, Mathf.Clamp01(taper * 1.35f));
        }
    }

    private static void AddNeonLine(
        ref Color dst,
        float dist,
        float coreHalfWidth,
        float glowHalfWidth,
        Color deepGlow,
        Color midNeon,
        Color whiteCore,
        float intensity)
    {
        if (dist >= glowHalfWidth) return;

        Color c;
        if (dist <= coreHalfWidth)
        {
            float t = 1f - (dist / Mathf.Max(0.0001f, coreHalfWidth));
            c = Color.Lerp(midNeon, whiteCore, Mathf.Pow(t, 1.3f));
            c.a = intensity;
        }
        else
        {
            float t = 1f - ((dist - coreHalfWidth) / Mathf.Max(0.0001f, glowHalfWidth - coreHalfWidth));
            c = Color.Lerp(deepGlow, midNeon, t * 0.65f);
            c.a = t * t * intensity * 0.85f;
        }

        dst = AlphaBlend(dst, c);
    }

    // =========================================================================
    // 기존 공통 메서드 (하위 호환 유지)
    // =========================================================================
    public static Texture2D CreatePlayerCommonAuraTexture(ECardColor colorType, int size = 256)
    {
        switch (colorType)
        {
            case ECardColor.BLUE:  return CreatePlayer1TriangleAuraTexture(size);
            case ECardColor.GREEN: return CreatePlayer2OctagonAuraTexture(size);
            case ECardColor.RED:
            case ECardColor.SUPER_BUSTER:
            default:               return CreatePlayer3PentagonAuraTexture(size);
        }
    }

    public static Texture2D CreateSimpleHitSlashTexture(int size = 256)
    {
        return CreatePlayer2SonicFlurryHitTexture(size);
    }

    private static Color AlphaBlend(Color bottom, Color top)
    {
        float outA = top.a + bottom.a * (1f - top.a);
        if (outA <= 0.0001f) return Color.clear;
        Color outRGB = (top * top.a + bottom * bottom.a * (1f - top.a)) / outA;
        outRGB.a = outA;
        return outRGB;
    }
}
