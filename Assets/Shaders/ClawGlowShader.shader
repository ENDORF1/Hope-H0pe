Shader "Custom/CoffinGlow"
{
    Properties
    {
        _MainTex        ("Screen Texture",    2D)    = "white" {}

        [Header(Coffin)]
        _StripColor     ("条纹发光色",        Color)  = (0.9, 0.05, 0.02, 1.0)
        _EdgeColor      ("边缘辉光色",        Color)  = (0.5, 0.0, 0.0,  1.0)
        _BinaryColor    ("二进制代码色",      Color)  = (0.9, 0.1, 0.05, 0.8)
        _Alpha          ("整体透明度",        Float)  = 0.0
        _Progress       ("推进进度 0~1",      Float)  = 0.0
        _StepCount      ("步进格数",          Float)  = 12.0
        _StripDensity   ("条纹密度",          Float)  = 14.0
        _StripRatio     ("条纹占比 0~1",      Float)  = 0.55
        _EdgeBleed      ("晕染宽度",          Float)  = 0.22
        _EdgeRough      ("晕染粗糙度",        Float)  = 6.0
        _Seed           ("随机种子",          Float)  = 0.0
        _BinaryDensity  ("二进制密度",        Float)  = 30.0
        _BinaryFade     ("二进制熄灭程度0~1", Float)  = 0.0

        [Header(Voronoi Glow)]
        _VoronoiSpeed     ("飞散速度",        Float)  = 0.35
        _VoronoiDensity   ("细胞密度",        Float)  = 6.0
        _VoronoiSharpness ("边界锐度",        Float)  = 3.0
        _VoronoiDistort   ("形状扰动",        Float)  = 0.05
        _GlowWidth        ("发光线宽",        Float)  = 0.4
        _GlowIntensity    ("发光强度",        Float)  = 2.5

        [Header(Tint)]
        _DarkenStrength   ("条纹内暗化强度",  Float)  = 0.5
        _TintStrength     ("条纹内染色强度",  Float)  = 0.15
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _StripColor;
            float4    _EdgeColor;
            float4    _BinaryColor;
            float     _Alpha;
            float     _Progress;
            float     _StepCount;
            float     _StripDensity;
            float     _StripRatio;
            float     _EdgeBleed;
            float     _EdgeRough;
            float     _Seed;
            float     _BinaryDensity;
            float     _BinaryFade;
            float     _VoronoiSpeed;
            float     _VoronoiDensity;
            float     _VoronoiSharpness;
            float     _VoronoiDistort;
            float     _GlowWidth;
            float     _GlowIntensity;
            float     _DarkenStrength;
            float     _TintStrength;

            // ── 噪声函数 ──────────────────────────────
            float Hash(float2 p)
            {
                return frac(sin(dot(p + _Seed * 0.01, float2(127.1, 311.7))) * 43758.5453);
            }

            float Hash1(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float2 Hash2v(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 ig = floor(p);
                float2 fg = frac(p);
                fg = fg * fg * (3.0 - 2.0 * fg);
                return lerp(
                    lerp(Hash(ig),             Hash(ig + float2(1,0)), fg.x),
                    lerp(Hash(ig+float2(0,1)), Hash(ig + float2(1,1)), fg.x),
                    fg.y);
            }

            float FBM(float2 p, int octaves)
            {
                float val = 0.0;
                float amp = 0.5;
                float freq = 1.0;
                for (int j = 0; j < octaves; j++)
                {
                    val += amp * ValueNoise(p * freq);
                    freq *= 2.17;
                    amp  *= 0.48;
                }
                return val;
            }

            // ── 飞散 Voronoi（移植自 EdgeGlowShader）────
            // 返回 float3(f1, f2, cellID)
            float3 VoronoiAnimated(float2 uvN, float density, float time)
            {
                float2 scaled = uvN * density;
                float2 cell   = floor(scaled);
                float2 local  = frac(scaled);

                float f1 = 8.0, f2 = 8.0, cellID = 0.0;

                for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                {
                    float2 nb  = float2(x, y);
                    float2 hv  = Hash2v(cell + nb);
                    float2 hv2 = Hash2v(cell + nb + float2(7.3, 3.1));

                    float2 bp = 0.5 + 0.4 * sin(6.2831 * hv);

                    float angle = hv2.x * 6.2831;
                    float2 dir  = float2(cos(angle), sin(angle));

                    float cycleSpeed = _VoronoiSpeed * (0.6 + hv2.y * 0.8);
                    float phase = frac(time * cycleSpeed + hv.x);
                    float dist_travel = phase * 0.15;

                    float2 pt   = bp + dir * dist_travel;
                    float2 diff = nb + pt - local;
                    float  d    = length(diff);

                    if (d < f1) { f2 = f1; f1 = d; cellID = Hash1(cell + nb); }
                    else if (d < f2) { f2 = d; }
                }
                return float3(f1, f2, cellID);
            }

            // ── 步进进度 ─────────────────────────────
            float SteppedProgress(float progress)
            {
                return floor(progress * _StepCount) / _StepCount;
            }

            // ── 单边侵蚀（形状蒙版）────────────────────
            // 返回值：> 0.5 为实心条纹区，0~0.5 为渗出边缘
            float EdgeStrip(float depth, float axis, float seedOff)
            {
                float stepped  = SteppedProgress(_Progress);
                float maxDepth = stepped * 0.5;

                if (depth > maxDepth + _EdgeBleed * 1.5) return 0.0;

                float strip   = axis * _StripDensity + seedOff;
                float stripID = floor(strip);
                float stripT  = frac(strip);

                float rndW = _StripRatio * (0.7 + Hash(float2(stripID, seedOff + 0.3)) * 0.6);
                rndW = clamp(rndW, 0.1, 0.95);
                float inStrip = step(stripT, rndW);

                float rndDepth = maxDepth * (0.65 + Hash(float2(stripID, seedOff + 1.7)) * 0.7);

                float noise1 = FBM(float2(
                    axis * _EdgeRough + seedOff,
                    depth * _EdgeRough * 0.5 + stripID * 0.3
                ), 4);
                float noise2 = ValueNoise(float2(
                    axis * _EdgeRough * 2.3 + seedOff + 99.0,
                    depth * _EdgeRough * 1.5
                ));
                float bleedNoise = noise1 * 0.7 + noise2 * 0.3;
                float bleedDepth = rndDepth + bleedNoise * _EdgeBleed * 1.5;

                if (depth > bleedDepth) return 0.0;

                float inSolid  = step(depth, rndDepth);
                float bleedT   = saturate((depth - rndDepth) / (_EdgeBleed * 1.5 + 0.001));
                float bleedVal = (1.0 - bleedT * bleedT) * (1.0 - inSolid);
                float showBleed = inStrip > 0.5 ? 1.0 : (bleedVal * 0.5);

                return inStrip * inSolid + showBleed * bleedVal;
            }

            // ── 二进制代码网格 ────────────────────────
            float BinaryGrid(float2 uv, float stripMask)
            {
                if (stripMask < 0.01) return 0.0;

                float2 cellUV = uv * _BinaryDensity;
                float2 cellID = floor(cellUV);
                float2 cellF  = frac(cellUV);

                float rnd = Hash(cellID + _Seed * 0.37);

                float2 charUV = (cellF - 0.5) * 2.5 + 0.5;
                if (charUV.x < 0.1 || charUV.x > 0.9 || charUV.y < 0.05 || charUV.y > 0.95)
                    return 0.0;

                float glyph = 0.0;
                if (rnd > 0.5)
                {
                    glyph = (charUV.x > 0.38 && charUV.x < 0.62) ? 1.0 : 0.0;
                    if (charUV.y > 0.8  && charUV.x > 0.25 && charUV.x < 0.62) glyph = 1.0;
                    if (charUV.y < 0.15 && charUV.x > 0.2  && charUV.x < 0.8)  glyph = 1.0;
                }
                else
                {
                    float2 ctr  = float2(0.5, 0.5);
                    float2 dd   = (charUV - ctr) * float2(1.0, 0.7);
                    float  dist = length(dd);
                    glyph = (dist > 0.2 && dist < 0.38) ? 1.0 : 0.0;
                }

                float flicker    = Hash(cellID + float2(_Seed, floor(_Time.y * 3.0)));
                float brightness = lerp(0.3, 1.0, flicker);

                float distToCenter = length(uv - 0.5) * 2.0;
                float fadeOrder    = 1.0 - distToCenter;
                float alive        = 1.0 - saturate((_BinaryFade - fadeOrder * 0.5) * 3.0);

                return glyph * brightness * alive * stripMask;
            }

            // ── 主片段 ──────────────────────────────
            fixed4 frag(v2f_img vi) : SV_Target
            {
                fixed4 scene = tex2D(_MainTex, vi.uv);
                if (_Alpha <= 0.001) return scene;

                float2 uv   = vi.uv;
                float  time = _Time.y;

                // 各边到边缘的深度
                float dTop    = uv.y;
                float dBottom = 1.0 - uv.y;
                float dLeft   = uv.x;
                float dRight  = 1.0 - uv.x;

                float sTop    = EdgeStrip(dTop,    uv.x, 0.00);
                float sBottom = EdgeStrip(dBottom, uv.x, 2.31);
                float sLeft   = EdgeStrip(dLeft,   uv.y, 4.17);
                float sRight  = EdgeStrip(dRight,  uv.y, 6.53);

                // 实心区（> 0.5）和渗出边缘区
                float solidMask = saturate(
                    step(0.5, sTop) + step(0.5, sBottom) +
                    step(0.5, sLeft) + step(0.5, sRight)
                );
                float rawBlend  = saturate(sTop + sBottom + sLeft + sRight);
                float bleedMask = saturate(rawBlend - solidMask);

                // ── 飞散 Voronoi 发光 ────────────────────
                // 等比空间，细胞形状不受屏幕比例影响
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 uvN   = float2(uv.x * aspect, uv.y);

                // 轻微扰动
                float2 distort;
                distort.x = sin(uv.y * 8.1 + time * 0.13) * _VoronoiDistort;
                distort.y = cos(uv.x * 8.1 + time * 0.11) * _VoronoiDistort;
                float2 duvN = uvN + distort;

                // 三层 Voronoi，各自密度和相位
                float3 v1 = VoronoiAnimated(duvN,                      _VoronoiDensity,       time);
                float3 v2 = VoronoiAnimated(duvN + float2(0.17, 0.11), _VoronoiDensity * 1.4, time + 1.3);
                float3 v3 = VoronoiAnimated(duvN + float2(0.33, 0.27), _VoronoiDensity * 1.8, time + 2.7);

                // ── Voronoi 边界线 ───────────────────────
                // f2 - f1 在边界处接近 0，在细胞中心较大（约 0.3~0.8）
                // threshold 必须和实际值域匹配，不能用 1/sharpness 这种极小值
                float threshold = _GlowWidth / _VoronoiSharpness;
                float e1 = 1.0 - smoothstep(0.0, threshold,        v1.y - v1.x);
                float e2 = 1.0 - smoothstep(0.0, threshold * 0.75, v2.y - v2.x);
                float e3 = 1.0 - smoothstep(0.0, threshold * 0.5,  v3.y - v3.x);

                float cellEdge = saturate(e1 * 0.55 + e2 * 0.3 + e3 * 0.15);

                float pulse = sin(time * 1.5 + v1.z * 12.0 + v2.z * 7.3) * 0.5 + 0.5;
                cellEdge *= lerp(0.6, 1.0, pulse);

                // ── 条纹区域合成 ─────────────────────────
                float3 col = scene.rgb;

                // 1. 渗出边缘：边缘辉光加法叠加
                col += _EdgeColor.rgb * bleedMask * 0.5 * _Alpha;

                // 2. 实心条纹区：暗化 + 染色（细胞中心），边界处保持原色
                float3 darkened = col * (1.0 - solidMask * _DarkenStrength * _Alpha);
                float3 tinted   = darkened + _StripColor.rgb * solidMask * _TintStrength * _Alpha * (1.0 - cellEdge);
                col = tinted;

                // 3. Voronoi 发光边界叠加（加法，边界线最亮）
                float glowZone = solidMask + bleedMask * 0.5;
                col += _StripColor.rgb * cellEdge * glowZone * _GlowIntensity * _Alpha;
                col  = saturate(col);

                // 4. 二进制字符
                float binaryMask = BinaryGrid(uv, solidMask);
                col += _BinaryColor.rgb * binaryMask * _BinaryColor.a * _Alpha * 0.6;
                col  = saturate(col);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
