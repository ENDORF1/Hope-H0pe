Shader "Custom/EdgeGlow"
{
    Properties
    {
        _MainTex            ("Screen Texture",          2D)     = "white" {}

        [Header(Vignette)]
        _VignetteColor      ("发光颜色（边缘）",         Color)  = (0.0, 0.78, 1.0, 1.0)
        _VignetteColorInner ("发光颜色（内侧）",         Color)  = (0.05, 0.0, 0.4, 1.0)
        _VignetteColorDeep  ("发光颜色（深处）",         Color)  = (0.0, 0.02, 0.12, 1.0)
        _VignetteIntensity  ("边缘强度",                Float)  = 1.5
        _VignettePower      ("边缘锐度",                Float)  = 4.0
        _VignetteAlpha      ("整体透明度（代码控制）",   Float)  = 0.0

        [Header(Voronoi)]
        _VoronoiSpeed       ("飞散速度",                Float)  = 0.4
        _VoronoiDensity     ("细胞密度",                Float)  = 9.0
        _VoronoiSharpness   ("边界锐度",                Float)  = 12.0
        _VoronoiContrast    ("明暗对比度",               Float)  = 2.5
        _VoronoiDistort     ("形状扰动",                Float)  = 0.06

        [Header(Shimmer)]
        _ShimmerSpeed       ("闪烁速度",                Float)  = 1.4
        _ShimmerStrength    ("闪烁强度",                Float)  = 0.3
        _ShimmerScale       ("闪烁缩放",                Float)  = 14.0
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
            float4    _VignetteColor;
            float4    _VignetteColorInner;
            float4    _VignetteColorDeep;
            float     _VignetteIntensity;
            float     _VignettePower;
            float     _VignetteAlpha;
            float     _VoronoiSpeed;
            float     _VoronoiDensity;
            float     _VoronoiSharpness;
            float     _VoronoiContrast;
            float     _VoronoiDistort;
            float     _ShimmerSpeed;
            float     _ShimmerStrength;
            float     _ShimmerScale;

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float Hash1(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash1(i);
                float b = Hash1(i + float2(1,0));
                float c = Hash1(i + float2(0,1));
                float d = Hash1(i + float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }

            // uvN：归一化等比空间（x*aspect，y），细胞形状均匀
            // 每个细胞有随机方向，在生命周期内从0飞到最大偏移再消失，循环
            float3 Voronoi(float2 uvN, float density, float time)
            {
                float2 scaled = uvN * density;
                float2 cell   = floor(scaled);
                float2 local  = frac(scaled);

                float f1 = 8.0, f2 = 8.0, cellID = 0.0;

                for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                {
                    float2 nb  = float2(x, y);
                    float2 hv  = Hash2(cell + nb);
                    float2 hv2 = Hash2(cell + nb + float2(7.3, 3.1));

                    // 静止基础位置
                    float2 bp = 0.5 + 0.4 * sin(6.2831 * hv);

                    // 随机飞散方向（单位圆上均匀分布）
                    float angle = hv2.x * 6.2831;
                    float2 dir  = float2(cos(angle), sin(angle));

                    // 每个细胞独立生命周期：phase 0→1 对应从内到外飞出，
                    // 用 frac 循环，speed 和 offset 都随机
                    float cycleSpeed = _VoronoiSpeed * (0.6 + hv2.y * 0.8);
                    float phase = frac(time * cycleSpeed + hv.x);

                    // 飞出距离：从0线性到最大，快速消失再重生（锯齿波）
                    float dist_travel = phase * 0.15;

                    float2 pt = bp + dir * dist_travel;

                    float2 diff = nb + pt - local;
                    float  d    = length(diff);

                    if (d < f1) { f2 = f1; f1 = d; cellID = Hash1(cell + nb); }
                    else if (d < f2) { f2 = d; }
                }
                return float3(f1, f2, cellID);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 scene = tex2D(_MainTex, i.uv);
                if (_VignetteAlpha <= 0.001) return scene;

                float2 uv     = i.uv;
                float  time   = _Time.y;
                float  aspect = _ScreenParams.x / _ScreenParams.y;

                // 归一化等比空间（细胞不拉伸）
                float2 uvN = float2(uv.x * aspect, uv.y);

                // 遮罩：Chebyshev，四边等强
                float2 uvC   = (uv - 0.5) * 2.0;
                float  distC = max(abs(uvC.x), abs(uvC.y));
                float  vignette = pow(saturate(distC * _VignetteIntensity), _VignettePower);

                // 轻微UV扰动（在归一化空间，各向同性）
                float2 distort;
                distort.x = sin(uv.y * 8.1 + time * 0.13) * _VoronoiDistort;
                distort.y = cos(uv.x * 8.1 + time * 0.11) * _VoronoiDistort;
                float2 duvN = uvN + distort;

                // 三层Voronoi，密度和速度各不同
                float3 v1 = Voronoi(duvN,                       _VoronoiDensity,        time);
                float3 v2 = Voronoi(duvN + float2(0.17, 0.11),  _VoronoiDensity * 1.3,  time + 1.3);
                float3 v3 = Voronoi(duvN + float2(0.33, 0.27),  _VoronoiDensity * 1.7,  time + 2.7);

                float f1a=v1.x, f2a=v1.y, idA=v1.z;
                float f1b=v2.x, f2b=v2.y, idB=v2.z;
                float f1c=v3.x, f2c=v3.y, idC=v3.z;

                // 三层硬边界
                float inv = 1.0 / _VoronoiSharpness;
                float e1  = 1.0 - smoothstep(0.0, 0.5  * inv, f2a - f1a);
                float e2  = 1.0 - smoothstep(0.0, 0.35 * inv, f2b - f1b);
                float e3  = 1.0 - smoothstep(0.0, 0.2  * inv, f2c - f1c);

                // 细胞明暗
                float thresh = 1.0 / _VoronoiContrast;
                float bri = step(thresh,        idA) * 0.55
                          + step(thresh * 0.85, idB) * 0.30
                          + step(thresh * 0.7,  idC) * 0.15;
                bri = saturate(bri + e1 * 0.75 + e2 * 0.35 + e3 * 0.2);

                // 闪烁高光
                float2 shimUV = duvN * _ShimmerScale + float2(time * 0.07, time * 0.05);
                float shimmer = ValueNoise(shimUV) * ValueNoise(shimUV * 1.7 + 3.1);
                shimmer = pow(shimmer, 2.5) * _ShimmerStrength;
                shimmer *= e1 + e2 * 0.5;
                shimmer *= saturate(0.5 + 0.5 * sin(time * _ShimmerSpeed + idA * 12.0 + idB * 7.3));
                bri = saturate(bri + shimmer);

                // 三层颜色渐变
                float distR  = length(uvC) * 0.5;
                float colorT = saturate(distR * 0.6 + (idA * 0.35 + idB * 0.4 + idC * 0.25) * 0.35);
                float3 colA  = lerp(_VignetteColorDeep.rgb,  _VignetteColorInner.rgb, saturate(colorT * 2.0));
                float3 colB  = lerp(_VignetteColorInner.rgb, _VignetteColor.rgb,      saturate(colorT * 2.0 - 1.0));
                float3 glowRGB = colorT < 0.5 ? colA : colB;
                glowRGB = lerp(glowRGB, _VignetteColor.rgb, e1 * 0.45 + shimmer * 0.8);

                float alpha = saturate(vignette * bri) * _VignetteAlpha;
                return fixed4(lerp(scene.rgb, glowRGB, alpha), 1.0);
            }
            ENDCG
        }
    }
}
