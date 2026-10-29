using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AttackBox 안의 카드 순서를 표시하기 위한 금색(Gold) 네온 숫자 이미지(1~6)를 절차적으로 생성하는 유틸리티입니다.
/// </summary>
public static class GoldNumberTextureGenerator
{
    private struct Segment
    {
        public Vector2 a;
        public Vector2 b;
        public Segment(float x1, float y1, float x2, float y2)
        {
            a = new Vector2(x1, y1);
            b = new Vector2(x2, y2);
        }
    }

    public static Texture2D CreateGoldNumberTexture(int number, int width = 128, int height = 128)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        List<Segment> segments = GetDigitSegments(number);

        // 금색 색상 팔레트 (외곽 황금 글로우 -> 진한 금색 -> 밝은 황금색 -> 하이라이트 백금색)
        Color darkBronzeOutline = new Color(0.22f, 0.12f, 0.02f, 0.95f);
        Color deepGoldGlow = new Color(0.85f, 0.52f, 0.05f, 1f);
        Color richGoldMid = new Color(1.0f, 0.78f, 0.15f, 1f);
        Color brightGoldTop = new Color(1.0f, 0.92f, 0.45f, 1f);
        Color coreSpecular = new Color(1.0f, 0.99f, 0.85f, 1f);

        float strokeRadius = 0.068f;  // 숫자 획 두께
        float outlineRadius = 0.095f; // 진한 테두리 두께 (어두워진 카드 위에서 선명하게 보이도록)
        float glowRadius = 0.20f;     // 금빛 광원 반경

        Vector2 center = new Vector2(0.5f, 0.5f);
        float ringRadius = 0.42f;
        float ringThickness = 0.018f;

        for (int y = 0; y < height; y++)
        {
            float ny = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float nx = (x + 0.5f) / width;
                Vector2 p = new Vector2(nx, ny);

                // 1. 숫자 획까지의 최소 거리 계산
                float minDist = float.MaxValue;
                for (int i = 0; i < segments.Count; i++)
                {
                    float d = DistancePointToSegment(p, segments[i].a, segments[i].b);
                    if (d < minDist) minDist = d;
                }

                // 2. 숫자 주변을 감싸는 얇은 금색 기하학 다이아몬드/원형 엠블럼 링 거리
                float distToCenter = Vector2.Distance(p, center);
                float ringDist = Mathf.Abs(distToCenter - ringRadius);

                Color finalColor = Color.clear;

                // 배경 은은한 다크 골드 메달리온 음영 (숫자가 훨씬 잘 보이도록)
                if (distToCenter < ringRadius)
                {
                    float innerBgAlpha = Mathf.Clamp01((ringRadius - distToCenter) / ringRadius) * 0.35f;
                    finalColor = new Color(0.08f, 0.06f, 0.02f, innerBgAlpha);
                }

                // 외곽 금빛 링 렌더링
                if (ringDist <= ringThickness)
                {
                    float rt = 1f - (ringDist / ringThickness);
                    Color ringCol = Color.Lerp(deepGoldGlow, brightGoldTop, ny);
                    ringCol.a = rt * 0.85f;
                    finalColor = AlphaBlend(finalColor, ringCol);
                }
                else if (ringDist <= ringThickness + 0.04f)
                {
                    float rg = 1f - ((ringDist - ringThickness) / 0.04f);
                    Color ringGlow = deepGoldGlow;
                    ringGlow.a = rg * rg * 0.35f;
                    finalColor = AlphaBlend(finalColor, ringGlow);
                }

                // 숫자 본체 + 금빛 광원 렌더링
                if (minDist <= strokeRadius)
                {
                    // 상단에서 하단으로 흐르는 금속 골드 그라데이션
                    Color metalGrad = Color.Lerp(richGoldMid, brightGoldTop, Mathf.Clamp01((ny - 0.25f) / 0.5f));
                    // 획 중심부로 갈수록 밝게 빛나는 입체 베벨(Bevel) 효과
                    float coreFactor = 1f - (minDist / strokeRadius);
                    Color bodyCol = Color.Lerp(metalGrad, coreSpecular, Mathf.Pow(coreFactor, 2.2f));
                    bodyCol.a = 1f;
                    finalColor = AlphaBlend(finalColor, bodyCol);
                }
                else if (minDist <= outlineRadius)
                {
                    // 획 바깥쪽을 감싸는 어두운 브론즈/골드 외곽선 (가독성 극대화)
                    float ot = 1f - ((minDist - strokeRadius) / (outlineRadius - strokeRadius));
                    Color outCol = Color.Lerp(deepGoldGlow, darkBronzeOutline, ot);
                    outCol.a = Mathf.Clamp01(ot * 1.1f);
                    finalColor = AlphaBlend(finalColor, outCol);
                }
                else if (minDist <= glowRadius)
                {
                    // 황금빛 네온 글로우
                    float gt = 1f - ((minDist - outlineRadius) / (glowRadius - outlineRadius));
                    Color glowCol = deepGoldGlow;
                    glowCol.a = gt * gt * 0.65f;
                    finalColor = AlphaBlend(finalColor, glowCol);
                }

                pixels[y * width + x] = finalColor;
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

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.000001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        Vector2 projection = a + t * ab;
        return Vector2.Distance(p, projection);
    }

    /// <summary>
    /// 1 ~ 6 숫자의 기하학적(Zeometry 스타일) 벡터 선분 좌표를 반환합니다 (0~1 정규화 좌표계, Y축 위쪽이 1).
    /// </summary>
    private static List<Segment> GetDigitSegments(int number)
    {
        List<Segment> list = new List<Segment>();
        switch (number)
        {
            case 1:
                list.Add(new Segment(0.50f, 0.26f, 0.50f, 0.74f)); // 수직 기둥
                list.Add(new Segment(0.38f, 0.63f, 0.50f, 0.74f)); // 상단 깃발
                list.Add(new Segment(0.36f, 0.26f, 0.64f, 0.26f)); // 하단 받침
                break;

            case 2:
                list.Add(new Segment(0.34f, 0.64f, 0.43f, 0.73f)); // 상단 왼쪽 대각
                list.Add(new Segment(0.43f, 0.73f, 0.59f, 0.73f)); // 상단 수평
                list.Add(new Segment(0.59f, 0.73f, 0.65f, 0.63f)); // 상단 오른쪽 코너
                list.Add(new Segment(0.65f, 0.63f, 0.65f, 0.55f)); // 우상단 수직
                list.Add(new Segment(0.65f, 0.55f, 0.34f, 0.26f)); // 중앙 대각선 하강
                list.Add(new Segment(0.34f, 0.26f, 0.66f, 0.26f)); // 하단 수평 받침
                break;

            case 3:
                list.Add(new Segment(0.35f, 0.73f, 0.63f, 0.73f)); // 상단 수평
                list.Add(new Segment(0.63f, 0.73f, 0.65f, 0.53f)); // 우상단 수직
                list.Add(new Segment(0.42f, 0.51f, 0.65f, 0.53f)); // 중앙 가로줄
                list.Add(new Segment(0.65f, 0.53f, 0.65f, 0.33f)); // 우하단 수직
                list.Add(new Segment(0.65f, 0.33f, 0.57f, 0.26f)); // 우하단 챔퍼
                list.Add(new Segment(0.35f, 0.26f, 0.57f, 0.26f)); // 하단 수평
                break;

            case 4:
                list.Add(new Segment(0.57f, 0.26f, 0.57f, 0.74f)); // 우측 수직 기둥
                list.Add(new Segment(0.57f, 0.74f, 0.32f, 0.42f)); // 좌측 사선
                list.Add(new Segment(0.32f, 0.42f, 0.68f, 0.42f)); // 가로 교차선
                break;

            case 5:
                list.Add(new Segment(0.64f, 0.73f, 0.36f, 0.73f)); // 상단 수평
                list.Add(new Segment(0.36f, 0.73f, 0.36f, 0.51f)); // 좌상단 수직
                list.Add(new Segment(0.36f, 0.51f, 0.59f, 0.51f)); // 중단 수평
                list.Add(new Segment(0.59f, 0.51f, 0.65f, 0.44f)); // 우중단 챔퍼
                list.Add(new Segment(0.65f, 0.44f, 0.65f, 0.33f)); // 우하단 수직
                list.Add(new Segment(0.65f, 0.33f, 0.58f, 0.26f)); // 우하단 챔퍼
                list.Add(new Segment(0.35f, 0.26f, 0.58f, 0.26f)); // 하단 수평
                break;

            case 6:
                list.Add(new Segment(0.60f, 0.73f, 0.43f, 0.73f)); // 상단 수평
                list.Add(new Segment(0.43f, 0.73f, 0.35f, 0.62f)); // 좌상단 챔퍼
                list.Add(new Segment(0.35f, 0.62f, 0.35f, 0.33f)); // 좌측 긴 수직
                list.Add(new Segment(0.35f, 0.33f, 0.42f, 0.26f)); // 좌하단 챔퍼
                list.Add(new Segment(0.42f, 0.26f, 0.58f, 0.26f)); // 하단 수평
                list.Add(new Segment(0.58f, 0.26f, 0.65f, 0.33f)); // 우하단 챔퍼
                list.Add(new Segment(0.65f, 0.33f, 0.65f, 0.45f)); // 우하단 수직
                list.Add(new Segment(0.65f, 0.45f, 0.58f, 0.51f)); // 우중단 챔퍼
                list.Add(new Segment(0.35f, 0.51f, 0.58f, 0.51f)); // 중단 가로 닫기
                break;
        }
        return list;
    }
}
