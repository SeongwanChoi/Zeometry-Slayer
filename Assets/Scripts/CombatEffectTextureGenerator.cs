using UnityEngine;

/// <summary>
/// 1. 플레이어 공격 오라 (빨강/파랑/초록 3색) : 삼각형/오각형/팔각형 플레이어 캐릭터 3종류 모두에 공통으로 어울리는 배경 방사형 네온 오라
/// 2. 적 피격 참격 이펙트 (단일 공통) : 색깔 구분 없이 짧고 날카롭게 베어지는 심플한 참격(Slash) 이미지
/// </summary>
public static class CombatEffectTextureGenerator
{
    public static void GetColorPalette(ECardColor colorType, out Color deepGlow, out Color midNeon, out Color brightCore)
    {
        switch (colorType)
        {
            case ECardColor.RED:
                deepGlow = new Color(0.85f, 0.05f, 0.15f, 1f);
                midNeon = new Color(1.0f, 0.22f, 0.32f, 1f);
                brightCore = new Color(1.0f, 0.85f, 0.88f, 1f);
                break;

            case ECardColor.GREEN:
                deepGlow = new Color(0.05f, 0.78f, 0.25f, 1f);
                midNeon = new Color(0.20f, 1.0f, 0.45f, 1f);
                brightCore = new Color(0.88f, 1.0f, 0.92f, 1f);
                break;

            case ECardColor.BLUE:
            default:
                deepGlow = new Color(0.05f, 0.38f, 0.95f, 1f);
                midNeon = new Color(0.15f, 0.78f, 1.0f, 1f);
                brightCore = new Color(0.88f, 0.98f, 1.0f, 1f);
                break;
        }
    }

    /// <summary>
    /// 1. 플레이어 공격 오라 (빨강/파랑/초록)
    /// 삼각형, 오각형, 팔각형 등 어떤 도형의 플레이어 캐릭터 뒤에 놓여도 자연스럽게 어울리는
    /// 원형/방사형 네온 에너지 오라 텍스처를 생성합니다.
    /// </summary>
    public static Texture2D CreatePlayerCommonAuraTexture(ECardColor colorType, int size = 256)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        GetColorPalette(colorType, out Color deepGlow, out Color midNeon, out Color brightCore);
        Vector2 center = new Vector2(0.5f, 0.5f);

        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f) / size;
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size;
                Vector2 p = new Vector2(nx, ny);

                float dist = Vector2.Distance(p, center);
                Color col = Color.clear;

                if (dist < 0.48f)
                {
                    float normDist = dist / 0.48f;
                    float falloff = 1f - normDist;

                    // 부드러운 방사형 배경 광원 (캐릭터 뒤에서 은은하고 강렬하게 퍼짐)
                    Color baseAura = Color.Lerp(deepGlow, midNeon, Mathf.Pow(falloff, 0.8f));
                    baseAura.a = Mathf.Pow(falloff, 1.6f) * 0.88f;
                    col = AlphaBlend(col, baseAura);

                    // 도형 바깥쪽으로 퍼져나가는 2중 동심원 네온 파동 링 (특정 도형에 종속되지 않는 원형 에너지 링)
                    float ring1 = Mathf.Abs(dist - 0.33f);
                    if (ring1 < 0.045f)
                    {
                        float r1 = 1f - (ring1 / 0.045f);
                        Color ringCol = Color.Lerp(midNeon, brightCore, r1 * 0.7f);
                        ringCol.a = r1 * r1 * 0.75f;
                        col = AlphaBlend(col, ringCol);
                    }

                    float ring2 = Mathf.Abs(dist - 0.21f);
                    if (ring2 < 0.035f)
                    {
                        float r2 = 1f - (ring2 / 0.035f);
                        Color ringCol2 = Color.Lerp(midNeon, brightCore, r2);
                        ringCol2.a = r2 * r2 * 0.85f;
                        col = AlphaBlend(col, ringCol2);
                    }

                    // 중심부 밝은 코어 광원
                    if (dist < 0.20f)
                    {
                        float coreFactor = 1f - (dist / 0.20f);
                        Color coreCol = Color.Lerp(midNeon, brightCore, coreFactor);
                        coreCol.a = coreFactor * coreFactor * 0.95f;
                        col = AlphaBlend(col, coreCol);
                    }
                }

                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// 2. 적 피격 이펙트 (단일 공통 참격 이펙트)
    /// 색상 구분 없이 짧고 강렬하게 베어지는 초승달/대각선 참격(Slash) 텍스처를 생성합니다.
    /// </summary>
    public static Texture2D CreateSimpleHitSlashTexture(int size = 256)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        // 날카로운 백색 코어 + 시안/아이스블루 외곽 광선으로 이루어진 깔끔한 참격 컬러
        Color outerGlow = new Color(0.35f, 0.85f, 1.0f, 1f);
        Color midBlade = new Color(0.80f, 0.96f, 1.0f, 1f);
        Color whiteCore = new Color(1.0f, 1.0f, 1.0f, 1f);

        // 좌하단(0.12, 0.12)에서 우상단(0.88, 0.88)으로 살짝 휘어지며 뻗어나가는 날카로운 참격 궤적
        Vector2 start = new Vector2(0.12f, 0.12f);
        Vector2 end = new Vector2(0.88f, 0.88f);
        Vector2 dir = (end - start).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        float totalLen = Vector2.Distance(start, end);

        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f) / size;
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size;
                Vector2 p = new Vector2(nx, ny);

                Vector2 toP = p - start;
                float along = Vector2.Dot(toP, dir);
                float normAlong = Mathf.Clamp01(along / totalLen);

                // 양 끝(0과 1)은 바늘처럼 뾰족하고 중앙(0.5)은 도톰한 참격 두께 계수
                float taper = Mathf.Sin(normAlong * Mathf.PI);

                // 살짝 호(Arc)를 그리도록 법선 방향으로 곡률 오프셋 부여
                float curveOffset = taper * 0.045f;
                Vector2 curvePoint = start + dir * Mathf.Clamp(along, 0f, totalLen) + normal * curveOffset;

                float distToCurve = Vector2.Distance(p, curvePoint);
                float maxThickness = 0.042f * taper + 0.002f;
                float glowThickness = maxThickness * 2.8f;

                Color col = Color.clear;

                if (along >= -0.02f && along <= totalLen + 0.02f && distToCurve < glowThickness)
                {
                    if (distToCurve <= maxThickness)
                    {
                        float coreT = 1f - (distToCurve / maxThickness);
                        col = Color.Lerp(midBlade, whiteCore, Mathf.Pow(coreT, 1.5f));
                        col.a = Mathf.Clamp01(taper * 1.4f);
                    }
                    else
                    {
                        float glowT = 1f - ((distToCurve - maxThickness) / (glowThickness - maxThickness));
                        col = Color.Lerp(outerGlow, midBlade, glowT * 0.5f);
                        col.a = glowT * glowT * taper * 0.85f;
                    }
                }

                pixels[y * size + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
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
