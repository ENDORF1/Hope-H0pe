Shader "Custom/CoffinGlow"
{
    Properties
    {
        _MainTex        ("Screen Texture", 2D) = "white" {}

        [Header(Glitch)]
        _Intensity      ("故障强度 0~1",       Float) = 0.0
        _Seed           ("随机种子",            Float) = 0.0

        [Header(RGB Shift)]
        _RGBMaxOffset   ("RGB最大偏移(UV单位)", Float) = 0.03

        [Header(Block Glitch)]
        _BlockSize      ("像素块高度(UV单位)",  Float) = 0.05
        _BlockMaxShift  ("块最大位移(UV单位)",  Float) = 0.08
        _BlockDensity   ("同时错位的块比例",    Float) = 0.4

        [Header(Scanline)]
        _ScanlineStr    ("扫描线强度",          Float) = 0.15
        _ScanlineCount  ("扫描线数量",          Float) = 180.0

        [Header(Noise)]
        _NoiseStr       ("噪点强度",            Float) = 0.08
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
            float4    _MainTex_TexelSize;
            float     _Intensity;
            float     _Seed;
            float     _RGBMaxOffset;
            float     _BlockSize;
            float     _BlockMaxShift;
            float     _BlockDensity;
            float     _ScanlineStr;
            float     _ScanlineCount;
            float     _NoiseStr;

            // ── Hash ────────────────────────────────
            float Hash11(float p)
            {
                p = frac(p * 0.1031 + _Seed * 0.003);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973) + _Seed * 0.007);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // ── 像素块错位 ───────────────────────────
            // 把 uv.y 按 _BlockSize 分格，每格随机水平位移
            // intensity 越大：错位的块越多，位移越大
            float2 BlockGlitch(float2 uv, float intensity)
            {
                if (intensity < 0.001) return uv;

                // 时间分段：每隔一小段换一批随机块（制造跳帧感）
                float timeSeg = floor(_Time.y * lerp(4.0, 18.0, intensity));

                // 当前像素所在的块 ID
                float blockID = floor(uv.y / _BlockSize);

                // 该块是否错位：由 Hash 决定，intensity 越高触发概率越大
                float trigger = Hash21(float2(blockID, timeSeg));
                if (trigger > _BlockDensity * intensity) return uv;

                // 错位量：随机方向，intensity 越高越大
                float shift = (Hash21(float2(blockID + 0.5, timeSeg)) * 2.0 - 1.0)
                            * _BlockMaxShift * intensity;

                // 极端时偶发整块跳到屏幕另一侧（撕裂感）
                float tear = Hash21(float2(blockID, timeSeg + 0.1));
                if (intensity > 0.7 && tear > 0.92)
                    shift += sign(shift) * 0.5;

                return float2(frac(uv.x + shift), uv.y);
            }

            // ── RGB 通道错位 ─────────────────────────
            // R 向左偏、B 向右偏，模拟色差
            // 同时加入轻微的垂直错位（更真实）
            float3 RGBShift(float2 uv, float intensity)
            {
                float maxOff = _RGBMaxOffset * intensity;

                // 时间驱动的随机方向抖动
                float t = _Time.y;
                float jitterR = (Hash11(floor(t * lerp(6.0, 20.0, intensity)))       * 2.0 - 1.0) * maxOff;
                float jitterB = (Hash11(floor(t * lerp(6.0, 20.0, intensity)) + 0.3) * 2.0 - 1.0) * maxOff;

                float2 uvR = float2(frac(uv.x + jitterR), uv.y);
                float2 uvB = float2(frac(uv.x + jitterB), uv.y);

                float r = tex2D(_MainTex, uvR).r;
                float g = tex2D(_MainTex, uv ).g;
                float b = tex2D(_MainTex, uvB).b;

                return float3(r, g, b);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                if (_Intensity <= 0.001)
                    return tex2D(_MainTex, i.uv);

                float intensity = _Intensity;
                float2 uv = i.uv;

                // ── 1. 像素块错位 UV ─────────────────
                float2 glitchedUV = BlockGlitch(uv, intensity);

                // ── 2. RGB 通道分离（在错位后的 UV 上采样）
                // 先在 glitchedUV 上取 G，R/B 在基础 uv 上另行偏移
                float2 uvR = BlockGlitch(uv, intensity * 0.8);
                float2 uvB = BlockGlitch(uv, intensity * 1.1);

                float maxOff = _RGBMaxOffset * intensity;
                float t = _Time.y;
                float jR = (Hash11(floor(t * lerp(5.0, 22.0, intensity)))       * 2.0 - 1.0) * maxOff;
                float jB = (Hash11(floor(t * lerp(5.0, 22.0, intensity)) + 0.7) * 2.0 - 1.0) * maxOff;

                float r = tex2D(_MainTex, float2(frac(uvR.x + jR), uvR.y)).r;
                float g = tex2D(_MainTex, glitchedUV).g;
                float b = tex2D(_MainTex, float2(frac(uvB.x + jB), uvB.y)).b;

                float3 col = float3(r, g, b);

                // ── 3. 扫描线（强度高时更明显）──────────
                float scanline = sin(uv.y * _ScanlineCount * 3.14159) * 0.5 + 0.5;
                float scanStr  = _ScanlineStr * intensity;
                col *= lerp(1.0, scanline * 0.5 + 0.5, scanStr);

                // ── 4. 数字噪点 ──────────────────────
                float noise = Hash21(uv * float2(800.0, 450.0) + floor(t * 30.0));
                col = lerp(col, float3(noise, noise * 0.1, noise * 0.05),
                           _NoiseStr * intensity * intensity);

                // ── 5. 极端时整帧闪白/闪黑 ───────────
                if (intensity > 0.85)
                {
                    float flashT = Hash11(floor(t * 25.0));
                    if (flashT > 0.88)
                        col = lerp(col, float3(1,1,1), (flashT - 0.88) / 0.12 * 0.6);
                    else if (flashT < 0.06)
                        col = lerp(col, float3(0,0,0), (0.06 - flashT) / 0.06 * 0.8);
                }

                return fixed4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
}
