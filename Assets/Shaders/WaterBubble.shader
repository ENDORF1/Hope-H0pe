Shader "XiWang_XiWang/WaterBubble"
{
    Properties
    {
        _BackgroundTex  ("Background RT",   2D)     = "black" {}
        _Color          ("Hope Color",      Color)  = (0.24, 0.91, 0.78, 1)
        _CenterUV       ("Center UV",       Vector) = (0.5, 0.5, 0, 0)
        _Radius         ("Radius (UV)",     Float)  = 0.1
        _GrowPhase      ("Grow Phase 0~1",  Float)  = 0.0
        _Tension        ("Tension 0~1",     Float)  = 0.0
        _RefractStrength("Refract Strength",Float)  = 0.08
        _FresnelPow     ("Fresnel Power",   Float)  = 2.5
        _Aspect         ("Aspect Ratio",    Float)  = 1.777
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay+100" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _BackgroundTex;
            float4    _Color;
            float4    _CenterUV;   // xy = center UV
            float     _Radius;
            float     _GrowPhase;
            float     _Tension;
            float     _RefractStrength;
            float     _FresnelPow;
            float     _Aspect;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            // smoothstep
            float ss(float e0, float e1, float x)
            {
                float t = saturate((x - e0) / (e1 - e0));
                return t * t * (3.0 - 2.0 * t);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv  = i.uv;
                float2 cen = _CenterUV.xy;

                // 考虑宽高比的椭圆距离 → 正圆判断
                float2 delta = float2((uv.x - cen.x) * _Aspect, uv.y - cen.y);
                float  dist  = length(delta);
                float  r     = _Radius;

                // 圆外完全透明
                if (dist > r + 0.002) return fixed4(0, 0, 0, 0);

                // 归一化距离 (0=圆心, 1=边缘)
                float nd = dist / r;

                // ── 折射：球面法线近似 ──────────────────────────
                // 把圆内的像素当作球面投影，法线 = (dx, dy, sqrt(1-nd²))
                float nz    = sqrt(max(0.0, 1.0 - nd * nd));
                float2 norm = delta / (dist + 0.0001); // 单位向量
                // 折射偏移：边缘强，中心弱，用 nd² 控制
                float refractMag = nd * nd * _RefractStrength * _GrowPhase;
                // 入射方向 (0,0,-1) 经球面法线折射后的切向分量
                float2 refractUV = uv - norm * refractMag;
                refractUV = saturate(refractUV);

                fixed4 bgColor = tex2D(_BackgroundTex, refractUV);

                // ── 菲涅尔边缘 ──────────────────────────────────
                // HTML: fresnel = smoothstep(0.7, 1.0, nd) * 0.45
                float fresnel   = ss(0.65, 1.0, nd) * 0.5;
                // body 内部极淡青色
                float body      = (1.0 - ss(0.80, 1.0, nd)) * 0.08;
                float edgeAlpha = saturate(fresnel + body);

                // 边缘抗锯齿
                float edgeMask  = 1.0 - ss(r - 0.003, r + 0.001, dist);

                // ── 折射暗环（轮廓内侧细暗边）──────────────────
                float darkRim = ss(0.88, 0.98, nd) * (1.0 - ss(0.98, 1.02, nd)) * 0.35;

                // ── 张力辉光（生长后期外围光晕）────────────────
                // HTML: og = radialGradient r*0.8~r*2.2, tension*0.25→0
                float outerDist = dist / r; // 相对于气泡半径
                float glowMask  = ss(2.2, 0.8, outerDist) * _Tension;
                float glowAlpha = glowMask * 0.22;

                // 辉光区域（圆外也要绘制）
                if (dist > r)
                {
                    // 圆外只画张力辉光
                    float outerFade = ss(r * 2.2, r * 0.8, dist) * _Tension * 0.18;
                    outerFade *= ss(r + 0.002, r, dist); // 紧贴边缘外
                    if (outerFade < 0.005) return fixed4(0,0,0,0);
                    return fixed4(_Color.rgb, outerFade);
                }

                // ── 高光1：左上小亮斑 ──────────────────────────
                // HTML: 左上椭圆 cx-r*0.28, cy-r*0.32, rw=r*0.22, rh=r*0.14
                float2 hl1Center = cen + float2(-0.28, 0.32) * r;  // y轴翻转
                float2 hl1d = float2((uv.x - hl1Center.x) * _Aspect, uv.y - hl1Center.y);
                // 椭圆：x轴压缩模拟
                float hl1dist = length(float2(hl1d.x / 0.22, hl1d.y / 0.14)) / r;
                float hl1     = ss(1.0, 0.0, hl1dist) * 0.82 * _GrowPhase;

                // ── 高光2：右下小亮斑 ──────────────────────────
                float2 hl2Center = cen + float2(0.30, -0.35) * r;
                float2 hl2d = float2((uv.x - hl2Center.x) * _Aspect, uv.y - hl2Center.y);
                float hl2dist = length(float2(hl2d.x / 0.12, hl2d.y / 0.08)) / r;
                float hl2     = ss(1.0, 0.0, hl2dist) * 0.30 * _GrowPhase;

                float totalHL = saturate(hl1 + hl2);

                // ── 描边 ────────────────────────────────────────
                float stroke = ss(0.96, 1.0, nd) * (1.0 - ss(1.0, 1.03, nd)) * 0.55;

                // ── 合成 ────────────────────────────────────────
                // 背景折射 + 青色菲涅尔叠加 + 白色高光 + 暗环
                fixed4 result;

                // 折射背景
                result = bgColor;

                // 叠加菲涅尔青色
                result.rgb = lerp(result.rgb, _Color.rgb, edgeAlpha * edgeMask * _GrowPhase);

                // 叠加暗环
                result.rgb = lerp(result.rgb, float3(0,0,0), darkRim * edgeMask);

                // 叠加高光（白色）
                result.rgb = lerp(result.rgb, float3(1,1,1), totalHL * edgeMask);

                // 叠加描边
                result.rgb = lerp(result.rgb, _Color.rgb, stroke * edgeMask);

                // 透明度：圆内有内容，抗锯齿过渡
                result.a = edgeMask * _GrowPhase;

                // 张力辉光叠加（圆内边缘）
                result.rgb += _Color.rgb * glowAlpha * ss(0.6, 1.0, nd);

                return result;
            }
            ENDCG
        }
    }
}
