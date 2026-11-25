Shader "Custom/URP_CyberCityHighway"
{
    Properties
    {
        _HorizonY ("Horizon Y", Range(0.2, 0.85)) = 0.63
        _Aspect ("Viewport Aspect", Float) = 1.7778
        _ScrollSpeed ("Scroll Speed", Float) = 1.0
        _ScrollTime ("Scroll Time", Float) = 0.0
        _UseCustomTime ("Use Custom Time", Float) = 1.0
        _EnemyBlend ("Enemy Turn Blend", Range(0, 1)) = 0.0
        _Brightness ("Brightness", Range(0.5, 2.0)) = 1.25
        _StreakCount ("Road Streak Count", Range(0, 12)) = 7.0

        [Header(Player Turn Colors Bright)]
        _PlayerSkyTop ("Player Sky Top", Color) = (0.05, 0.14, 0.34, 1)
        _PlayerSkyHorizon ("Player Sky Horizon", Color) = (0.13, 0.38, 0.70, 1)
        _PlayerFloorTop ("Player Floor Top", Color) = (0.07, 0.21, 0.46, 1)
        _PlayerFloorBot ("Player Floor Bot", Color) = (0.04, 0.12, 0.29, 1)
        _PlayerRoadFill ("Player Road Fill", Color) = (0.13, 0.44, 0.75, 1)
        _PlayerBuildingFill ("Player Building Fill", Color) = (0.05, 0.11, 0.25, 1)
        _PlayerPrimaryNeon ("Player Primary Neon", Color) = (0.40, 0.96, 1.00, 1)
        _PlayerSecondaryNeon ("Player Secondary Neon", Color) = (0.36, 0.70, 1.00, 1)

        [Header(Enemy Turn Colors Bright)]
        _EnemySkyTop ("Enemy Sky Top", Color) = (0.24, 0.05, 0.16, 1)
        _EnemySkyHorizon ("Enemy Sky Horizon", Color) = (0.54, 0.13, 0.30, 1)
        _EnemyFloorTop ("Enemy Floor Top", Color) = (0.30, 0.07, 0.20, 1)
        _EnemyFloorBot ("Enemy Floor Bot", Color) = (0.15, 0.03, 0.11, 1)
        _EnemyRoadFill ("Enemy Road Fill", Color) = (0.52, 0.13, 0.30, 1)
        _EnemyBuildingFill ("Enemy Building Fill", Color) = (0.16, 0.04, 0.12, 1)
        _EnemyPrimaryNeon ("Enemy Primary Neon", Color) = (1.00, 0.38, 0.52, 1)
        _EnemySecondaryNeon ("Enemy Secondary Neon", Color) = (0.95, 0.30, 0.66, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Background"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _HorizonY;
            float _Aspect;
            float _ScrollSpeed;
            float _ScrollTime;
            float _UseCustomTime;
            float _EnemyBlend;
            float _Brightness;
            float _StreakCount;

            half4 _PlayerSkyTop;
            half4 _PlayerSkyHorizon;
            half4 _PlayerFloorTop;
            half4 _PlayerFloorBot;
            half4 _PlayerRoadFill;
            half4 _PlayerBuildingFill;
            half4 _PlayerPrimaryNeon;
            half4 _PlayerSecondaryNeon;

            half4 _EnemySkyTop;
            half4 _EnemySkyHorizon;
            half4 _EnemyFloorTop;
            half4 _EnemyFloorBot;
            half4 _EnemyRoadFill;
            half4 _EnemyBuildingFill;
            half4 _EnemyPrimaryNeon;
            half4 _EnemySecondaryNeon;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv         : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv          : TEXCOORD0;
        };

        Varyings Vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
            OUT.uv = IN.uv;
            return OUT;
        }

        float Hash11(float p)
        {
            p = frac(p * 0.1031);
            p *= p + 33.33;
            p *= p + p;
            return frac(p);
        }

        float SdSegment(float2 p, float2 a, float2 b)
        {
            float2 pa = p - a;
            float2 ba = b - a;
            float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
            return length(pa - ba * h);
        }

        float SdEquilateralTriangle(float2 p, float r)
        {
            const float k = 1.7320508;
            p.x = abs(p.x) - r;
            p.y = p.y + r / k;
            if (p.x + k * p.y > 0.0)
                p = float2(p.x - k * p.y, -k * p.x - p.y) * 0.5;
            p.x -= clamp(p.x, -2.0 * r, 0.0);
            return -length(p) * sign(p.y);
        }

        float SdDiamond(float2 p, float r)
        {
            p = abs(p);
            return (p.x + p.y - r) * 0.70710678;
        }

        float SdHexagon(float2 p, float r)
        {
            const float3 k = float3(-0.8660254, 0.5, 0.57735027);
            p = abs(p);
            p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
            p -= float2(clamp(p.x, -k.z * r, k.z * r), r);
            return length(p) * sign(p.y);
        }

        float NeonOutline(float sdf, float halfThick, float glowSize)
        {
            float d = abs(sdf);
            float core = 1.0 - smoothstep(halfThick, halfThick + 0.0014, d);
            float glow = exp(-max(0.0, d - halfThick) / max(glowSize, 0.0005)) * 0.45;
            return saturate(core + glow);
        }

        float RectMask(float2 uv, float xMin, float xMax, float yMin, float yMax)
        {
            return step(xMin, uv.x) * step(uv.x, xMax) * step(yMin, uv.y) * step(uv.y, yMax);
        }

        float DrawCrane(float2 uv, float cx, float horizonY, float mastH, float jibL, float jibR, float aspect)
        {
            float2 p = float2((uv.x - cx) * aspect, uv.y - horizonY);
            float mast = SdSegment(p, float2(0.0, 0.0), float2(0.0, mastH));
            float jibY = mastH - 0.032;
            float jib = SdSegment(p, float2(-jibL * aspect, jibY), float2(jibR * aspect, jibY));
            float stayL = SdSegment(p, float2(0.0, mastH), float2(-jibL * aspect * 0.9, jibY));
            float stayR = SdSegment(p, float2(0.0, mastH), float2(jibR * aspect * 0.9, jibY));
            float d = min(min(mast, jib), min(stayL, stayR));
            return 1.0 - smoothstep(0.0022, 0.0042, d);
        }

        float DrawGridRect(float2 uv, float x0, float x1, float y0, float y1, float cols, float rows)
        {
            float mask = RectMask(uv, x0, x1, y0, y1);
            if (mask <= 0.0) return 0.0;

            float2 local = float2((uv.x - x0) / max(1e-4, x1 - x0), (uv.y - y0) / max(1e-4, y1 - y0));
            float cx = abs(frac(local.x * cols) - 0.5);
            float cy = abs(frac(local.y * rows) - 0.5);
            float borderX = min(local.x, 1.0 - local.x);
            float borderY = min(local.y, 1.0 - local.y);

            float lineV = 1.0 - smoothstep(0.035, 0.075, min(cx, borderX * cols));
            float lineH = 1.0 - smoothstep(0.035, 0.075, min(cy, borderY * rows));
            return saturate(max(lineV, lineH)) * mask;
        }

        half4 Frag(Varyings IN) : SV_Target
        {
            float2 uv = IN.uv;
            float aspect = max(1.0, _Aspect);
            float t = _UseCustomTime > 0.5 ? _ScrollTime : (_Time.y * _ScrollSpeed);
            float blend = saturate(_EnemyBlend);

            half3 skyTop = lerp(_PlayerSkyTop.rgb, _EnemySkyTop.rgb, blend);
            half3 skyHorizon = lerp(_PlayerSkyHorizon.rgb, _EnemySkyHorizon.rgb, blend);
            half3 floorTop = lerp(_PlayerFloorTop.rgb, _EnemyFloorTop.rgb, blend);
            half3 floorBot = lerp(_PlayerFloorBot.rgb, _EnemyFloorBot.rgb, blend);
            half3 roadFill = lerp(_PlayerRoadFill.rgb, _EnemyRoadFill.rgb, blend);
            half3 buildingFill = lerp(_PlayerBuildingFill.rgb, _EnemyBuildingFill.rgb, blend);
            half3 primaryNeon = lerp(_PlayerPrimaryNeon.rgb, _EnemyPrimaryNeon.rgb, blend);
            half3 secondaryNeon = lerp(_PlayerSecondaryNeon.rgb, _EnemySecondaryNeon.rgb, blend);
            half3 brightStreak = lerp(primaryNeon, half3(1.0, 1.0, 1.0), 0.48);

            half3 col = half3(0.0, 0.0, 0.0);

            if (uv.y >= _HorizonY)
            {
                // =============================================================
                // 상단: 밝은 사이버 밤하늘 + 크레인 + 전선 + 기하학 네온 스카이라인
                // =============================================================
                float skyH = max(0.001, 1.0 - _HorizonY);
                float hy = uv.y - _HorizonY;
                float skyT = saturate(hy / skyH);
                col = lerp(skyHorizon, skyTop, pow(skyT, 0.85));

                // 수평선 주변 화사한 안개 글로우
                float horizonGlow = exp(-hy * 11.5) * 0.42;
                col += primaryNeon * horizonGlow;

                // 뒤쪽 타워 크레인 실루엣 + 점멸 비콘
                float crane1 = DrawCrane(uv, 0.13, _HorizonY, 0.27, 0.045, 0.058, aspect);
                float crane2 = DrawCrane(uv, 0.59, _HorizonY, 0.27, 0.048, 0.062, aspect);
                col = lerp(col, buildingFill * 0.85, saturate(crane1 + crane2) * 0.85);

                float b1Blink = 0.45 + 0.55 * sin(t * 2.8);
                float b2Blink = 0.45 + 0.55 * sin(t * 2.8 + 1.8);
                float dBeacon1 = length(float2((uv.x - 0.13) * aspect, hy - 0.275));
                float dBeacon2 = length(float2((uv.x - 0.59) * aspect, hy - 0.275));
                col += primaryNeon * exp(-dBeacon1 * 180.0) * b1Blink;
                col += primaryNeon * exp(-dBeacon2 * 180.0) * b2Blink;

                // 우측 송전탑 & 처진 전선 실루엣
                float2 pPylon = float2((uv.x - 0.89) * aspect, hy);
                float pyL = SdSegment(pPylon, float2(-0.012, 0.0), float2(0.0, 0.23));
                float pyR = SdSegment(pPylon, float2(0.012, 0.0), float2(0.0, 0.23));
                float pyBar1 = SdSegment(pPylon, float2(-0.022, 0.18), float2(0.022, 0.18));
                float pyBar2 = SdSegment(pPylon, float2(-0.016, 0.13), float2(0.016, 0.13));
                float pylonMask = 1.0 - smoothstep(0.002, 0.004, min(min(pyL, pyR), min(pyBar1, pyBar2)));
                col = lerp(col, buildingFill * 0.85, pylonMask * 0.8);

                float wire1Y = _HorizonY + 0.205 - 0.028 * sin(saturate(uv.x / 0.45) * 3.14159);
                float wire2Y = _HorizonY + 0.215 - 0.025 * sin(saturate((uv.x - 0.58) / 0.42) * 3.14159);
                float wireMask = (uv.x < 0.45 ? (1.0 - smoothstep(0.0008, 0.0022, abs(uv.y - wire1Y))) : 0.0) +
                                 (uv.x > 0.58 ? (1.0 - smoothstep(0.0008, 0.0022, abs(uv.y - wire2Y))) : 0.0);
                col = lerp(col, buildingFill * 0.75, saturate(wireMask) * 0.65);

                // 건물 실루엣 채우기 (10개 빌딩)
                float bMask = 0.0;
                bMask = max(bMask, RectMask(uv, 0.000, 0.058, _HorizonY, _HorizonY + 0.210)); // 1
                bMask = max(bMask, RectMask(uv, 0.056, 0.151, _HorizonY, _HorizonY + 0.190)); // 2
                bMask = max(bMask, RectMask(uv, 0.152, 0.224, _HorizonY, _HorizonY + 0.160)); // 3
                bMask = max(bMask, RectMask(uv, 0.225, 0.330, _HorizonY, _HorizonY + 0.150)); // 4
                bMask = max(bMask, RectMask(uv, 0.330, 0.435, _HorizonY, _HorizonY + 0.195)); // 5
                bMask = max(bMask, RectMask(uv, 0.436, 0.568, _HorizonY, _HorizonY + 0.235)); // 6 (중앙 히어로)
                bMask = max(bMask, RectMask(uv, 0.568, 0.620, _HorizonY, _HorizonY + 0.210)); // 7
                bMask = max(bMask, RectMask(uv, 0.580, 0.608, _HorizonY + 0.210, _HorizonY + 0.235)); // 7 첨탑
                bMask = max(bMask, RectMask(uv, 0.648, 0.750, _HorizonY, _HorizonY + 0.155)); // 8
                bMask = max(bMask, RectMask(uv, 0.752, 0.850, _HorizonY, _HorizonY + 0.135)); // 9
                bMask = max(bMask, RectMask(uv, 0.850, 0.932, _HorizonY, _HorizonY + 0.170)); // 10a
                bMask = max(bMask, RectMask(uv, 0.932, 1.000, _HorizonY, _HorizonY + 0.200)); // 10b

                col = lerp(col, buildingFill, bMask);

                float pulseA = 0.82 + 0.18 * sin(t * 2.1);
                float pulseB = 0.82 + 0.18 * sin(t * 2.1 + 1.6);

                // 건물 1, 3, 10a, 10b 네온 격자창
                float grids = 0.0;
                grids += DrawGridRect(uv, 0.000, 0.058, _HorizonY, _HorizonY + 0.210, 3.0, 6.0) * 0.75;
                grids += DrawGridRect(uv, 0.152, 0.224, _HorizonY, _HorizonY + 0.160, 4.0, 6.0) * 0.68;
                grids += DrawGridRect(uv, 0.850, 0.932, _HorizonY, _HorizonY + 0.170, 4.0, 7.0) * 0.68;
                grids += DrawGridRect(uv, 0.932, 1.000, _HorizonY, _HorizonY + 0.200, 3.0, 7.0) * 0.75;
                col += secondaryNeon * grids;

                // 건물 2 상단/좌측 테두리 & 밝은 창 2개
                if (RectMask(uv, 0.054, 0.153, _HorizonY, _HorizonY + 0.193) > 0.0)
                {
                    float rim2 = (1.0 - smoothstep(0.001, 0.0028, abs(hy - 0.190))) * step(0.056, uv.x) * step(uv.x, 0.151);
                    rim2 = max(rim2, (1.0 - smoothstep(0.001, 0.0028, abs(uv.x - 0.056))) * step(hy, 0.190));
                    col += primaryNeon * rim2;
                    float w2 = RectMask(uv, 0.076, 0.084, _HorizonY + 0.148, _HorizonY + 0.156) +
                               RectMask(uv, 0.095, 0.103, _HorizonY + 0.148, _HorizonY + 0.156);
                    col += primaryNeon * saturate(w2) * 1.1;
                }

                // 건물 4: 3중 다이아몬드 심볼
                if (RectMask(uv, 0.225, 0.330, _HorizonY, _HorizonY + 0.150) > 0.0)
                {
                    float2 p4 = float2((uv.x - 0.2775) * aspect, hy - 0.072);
                    float d4 = NeonOutline(SdDiamond(p4, 0.044), 0.0020, 0.006) +
                               NeonOutline(SdDiamond(p4, 0.030), 0.0016, 0.005) +
                               NeonOutline(SdDiamond(p4, 0.016), 0.0015, 0.004);
                    col += primaryNeon * saturate(d4) * pulseA;
                }

                // 건물 5: 2x4 육각형 벌집 + 시안 외곽 프레임
                if (RectMask(uv, 0.328, 0.437, _HorizonY, _HorizonY + 0.197) > 0.0)
                {
                    float rim5 = max(1.0 - smoothstep(0.001, 0.0025, abs(hy - 0.195)),
                                     min(1.0 - smoothstep(0.001, 0.0025, abs(uv.x - 0.330)), 1.0)) *
                                 step(0.329, uv.x) * step(uv.x, 0.436) * step(hy, 0.196);
                    float side5 = max(1.0 - smoothstep(0.001, 0.0025, abs(uv.x - 0.330)),
                                      1.0 - smoothstep(0.001, 0.0025, abs(uv.x - 0.435))) * step(hy, 0.195);
                    col += primaryNeon * saturate(rim5 + side5) * 0.9;

                    for (int row = 0; row < 4; row++)
                    {
                        float hcy = _HorizonY + (0.163 - (float)row * 0.042);
                        half3 hexCol = row < 2 ? primaryNeon : secondaryNeon;
                        float pHex = row < 2 ? pulseB : pulseA;
                        for (int cIdx = 0; cIdx < 2; cIdx++)
                        {
                            float hcx = 0.362 + (float)cIdx * 0.041;
                            float2 ph = float2((uv.x - hcx) * aspect, uv.y - hcy);
                            col += hexCol * NeonOutline(SdHexagon(ph, 0.016), 0.0014, 0.004) * pHex;
                        }
                    }
                }

                // 건물 6: 중앙 최고층 히어로 타워 (상단 이중 삼각형 + 중단 이중 마름모 + 하단 이중 삼각형)
                if (RectMask(uv, 0.436, 0.568, _HorizonY, _HorizonY + 0.235) > 0.0)
                {
                    float cx6 = 0.502;
                    float2 ptTop = float2((uv.x - cx6) * aspect, hy - 0.175);
                    float triTop = NeonOutline(SdEquilateralTriangle(ptTop, 0.029), 0.0022, 0.006) +
                                   NeonOutline(SdEquilateralTriangle(ptTop, 0.017), 0.0016, 0.005);
                    col += primaryNeon * saturate(triTop) * pulseA;

                    float2 pDia = float2((uv.x - cx6) * aspect, hy - 0.102);
                    float diaMid = NeonOutline(SdDiamond(pDia, 0.030), 0.0018, 0.005) * pulseB +
                                   NeonOutline(SdDiamond(pDia, 0.018), 0.0015, 0.004) * pulseA;
                    col += lerp(secondaryNeon, primaryNeon, 0.45) * saturate(diaMid);

                    float2 ptBot = float2((uv.x - cx6) * aspect, hy - 0.025);
                    float triBot = NeonOutline(SdEquilateralTriangle(ptBot, 0.030), 0.0020, 0.006) +
                                   NeonOutline(SdEquilateralTriangle(ptBot, 0.017), 0.0015, 0.004);
                    col += primaryNeon * saturate(triBot);
                }

                // 건물 7: 하단 작은 창 3개
                float w7 = RectMask(uv, 0.578, 0.585, _HorizonY + 0.014, _HorizonY + 0.021) +
                           RectMask(uv, 0.591, 0.598, _HorizonY + 0.014, _HorizonY + 0.021) +
                           RectMask(uv, 0.604, 0.611, _HorizonY + 0.014, _HorizonY + 0.021);
                col += primaryNeon * saturate(w7) * 1.1;

                // 건물 8: 세로 2단 이중 다이아몬드
                if (RectMask(uv, 0.648, 0.750, _HorizonY, _HorizonY + 0.155) > 0.0)
                {
                    float2 p8a = float2((uv.x - 0.699) * aspect, hy - 0.106);
                    float2 p8b = float2((uv.x - 0.699) * aspect, hy - 0.044);
                    float d8a = NeonOutline(SdDiamond(p8a, 0.027), 0.0018, 0.005) +
                                NeonOutline(SdDiamond(p8a, 0.015), 0.0014, 0.004);
                    float d8b = NeonOutline(SdDiamond(p8b, 0.027), 0.0018, 0.005) +
                                NeonOutline(SdDiamond(p8b, 0.015), 0.0014, 0.004);
                    col += primaryNeon * saturate(d8a) * pulseA;
                    col += secondaryNeon * saturate(d8b) * pulseB;
                }

                // 건물 9: 창 2개
                float w9 = RectMask(uv, 0.778, 0.785, _HorizonY + 0.095, _HorizonY + 0.102) +
                           RectMask(uv, 0.796, 0.803, _HorizonY + 0.095, _HorizonY + 0.102);
                col += primaryNeon * saturate(w9) * 1.05;
            }
            else
            {
                // =============================================================
                // 하단: 밝은 원근 바닥 그리드 + 중앙 사이버 하이웨이 + 소수 정예 광선 스트릭
                // =============================================================
                float d = saturate((_HorizonY - uv.y) / max(0.001, _HorizonY));
                float vpX = 0.502;
                col = lerp(floorTop, floorBot, d);

                // 가로 원근 그리드 라인 (속도 1.0 기준 전진 스크롤)
                float gridCoord = pow(max(d, 0.0001), 0.4444) * 6.0 - t * 0.65;
                float gFrac = frac(gridCoord);
                float gDist = min(gFrac, 1.0 - gFrac);
                float horizLine = 1.0 - smoothstep(0.008, 0.022 + d * 0.018, gDist);
                horizLine *= smoothstep(0.0, 0.05, d);
                col += primaryNeon * horizLine * (0.28 + d * 0.48);

                // 바닥 방사형 원근 선
                float halfRoad = lerp(0.056, 0.375, d);
                float dx = uv.x - vpX;
                float absDx = abs(dx);

                float outerDenom = lerp(0.14, 0.48, d);
                float radialCoord = (dx / max(0.001, outerDenom)) * 2.3;
                float rFrac = frac(radialCoord);
                float rDist = min(rFrac, 1.0 - rFrac);
                float radialLine = 1.0 - smoothstep(0.012, 0.032, rDist);
                radialLine *= step(halfRoad, absDx) * smoothstep(0.0, 0.06, d);
                col += secondaryNeon * radialLine * 0.32;

                // 중앙 사이버 하이웨이 (사다리꼴 도로)
                float inRoad = 1.0 - smoothstep(halfRoad - 0.002, halfRoad + 0.002, absDx);
                col = lerp(col, roadFill, inRoad * (0.62 - d * 0.22));

                // 도로 내부 차선 2줄
                float laneU = dx / max(0.001, halfRoad);
                float laneDist = abs(abs(laneU) - 0.36);
                float laneLine = (1.0 - smoothstep(0.008, 0.020, laneDist)) * inRoad;
                col += secondaryNeon * laneLine * 0.38;

                // 도로 양쪽 네온 레일
                float railDist = abs(absDx - halfRoad);
                float railLine = NeonOutline(railDist, 0.0018, 0.007);
                col += brightStreak * railLine * 0.95;

                // 고속 광선 스트릭 (개수를 줄여 깔끔하게 _StreakCount = 7개)
                float streakAccum = 0.0;
                for (int i = 0; i < 12; i++)
                {
                    if ((float)i >= _StreakCount) break;

                    float fi = (float)i;
                    float laneSeed = (Hash11(fi * 17.31 + 1.7) - 0.5) * 1.48;
                    float speedSeed = 0.52 + Hash11(fi * 31.17 + 4.2) * 0.48;
                    float lenSeed = 0.045 + Hash11(fi * 47.53 + 9.1) * 0.055;
                    float phaseSeed = Hash11(fi * 61.97 + 2.3);

                    float progress = frac(phaseSeed + t * speedSeed);
                    float z0 = max(0.0, progress - lenSeed);
                    float z1 = min(1.0, progress);

                    float p0 = pow(z0, 1.85);
                    float p1 = pow(z1, 1.85);

                    if (d >= p0 && d <= p1)
                    {
                        float halfAtD = lerp(0.056, 0.375, d);
                        float streakX = vpX + laneSeed * halfAtD;
                        float distX = abs(uv.x - streakX);
                        float segT = saturate((d - p0) / max(0.0001, p1 - p0));
                        float alphaFade = smoothstep(0.0, 0.15, z1) * segT;
                        float lineMask = 1.0 - smoothstep(0.0010, 0.0026 + d * 0.0014, distX);
                        streakAccum += lineMask * alphaFade * 0.85;
                    }
                }
                col += brightStreak * saturate(streakAccum);
            }

            // 수평선 네온 글로우 라인 (전체 가로폭)
            float horizonDist = abs(uv.y - _HorizonY);
            float horizonBeam = NeonOutline(horizonDist, 0.0022, 0.011);
            col += brightStreak * horizonBeam * 0.95;

            return half4(col * _Brightness, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
