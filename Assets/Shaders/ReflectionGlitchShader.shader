Shader "XiWang/ReflectionGlitch"
{
    Properties
    {
        _MainTex      ("Hope RT",       2D)    = "white" {}
        _VoidTex      ("Void RT",       2D)    = "black" {}
        _Intensity    ("Intensity",     Float) = 0.0
        _Seed         ("Seed",          Float) = 0.0
        _BlockSize    ("Block Size",    Float) = 0.04
        _BlockDensity ("Block Density", Float) = 0.4
        _MaxShift     ("Max Shift",     Float) = 0.04
        _RGBOffset    ("RGB Offset",    Float) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert_img
            #pragma fragment frag
            #pragma target   3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _VoidTex;
            float4    _MainTex_TexelSize;
            float     _Intensity;
            float     _Seed;
            float     _BlockSize;
            float     _BlockDensity;
            float     _MaxShift;
            float     _RGBOffset;

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

            fixed4 frag(v2f_img i) : SV_Target
            {
                if (_Intensity <= 0.001)
                    return tex2D(_MainTex, i.uv);

                float2 uv      = i.uv;
                float  blockID = floor(uv.y / _BlockSize);
                float  timeSeg = floor(_Seed * 0.5);

                float trigger   = Hash21(float2(blockID, timeSeg));
                float corruptF  = (trigger < _BlockDensity * _Intensity) ? 1.0 : 0.0;

                // 水平位移
                float shift = (Hash21(float2(blockID + 0.5, timeSeg)) * 2.0 - 1.0)
                            * _MaxShift * _Intensity * corruptF;
                float tear  = Hash21(float2(blockID, timeSeg + 0.1));
                if (_Intensity > 0.7 && tear > 0.92)
                    shift += sign(shift) * 0.3 * corruptF;

                float2 finalUV = float2(frac(uv.x + shift), uv.y);

                // RGB 横向 jitter
                float jR = (Hash11(floor(_Seed * lerp(5.0, 22.0, _Intensity)))       * 2.0 - 1.0) * _RGBOffset * _Intensity * corruptF;
                float jB = (Hash11(floor(_Seed * lerp(5.0, 22.0, _Intensity)) + 0.7) * 2.0 - 1.0) * _RGBOffset * _Intensity * corruptF;

                float2 uvR = float2(frac(finalUV.x + jR), finalUV.y);
                float2 uvB = float2(frac(finalUV.x + jB), finalUV.y);

                // 分别采样两张 RT，用 corruptF 混合
                fixed4 hopeR = tex2D(_MainTex, uvR);
                fixed4 hopeG = tex2D(_MainTex, finalUV);
                fixed4 hopeB = tex2D(_MainTex, uvB);
                fixed4 voidR = tex2D(_VoidTex, uvR);
                fixed4 voidG = tex2D(_VoidTex, finalUV);
                fixed4 voidB = tex2D(_VoidTex, uvB);

                float r = lerp(hopeR.r, voidR.r, corruptF);
                float g = lerp(hopeG.g, voidG.g, corruptF);
                float b = lerp(hopeB.b, voidB.b, corruptF);
                float a = lerp(hopeG.a, voidG.a, corruptF);

                // 扫描线
                float scan = sin(uv.y * 180.0 * 3.14159) * 0.5 + 0.5;
                float3 col = float3(r, g, b) * lerp(1.0, scan * 0.5 + 0.5, 0.15 * _Intensity);

                return fixed4(saturate(col), a);
            }
            ENDCG
        }
    }
}
