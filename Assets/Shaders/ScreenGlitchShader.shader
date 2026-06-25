Shader "XiWang/ScreenGlitch"
{
    Properties
    {
        _MainTex        ("Screen Texture",  2D)    = "white" {}

        [Header(Block Tear)]
        _TearIntensity  ("Tear Intensity",  Float) = 0.0
        _TearSeed       ("Tear Seed",       Float) = 0.0
        _BlockSize      ("Block Size",      Float) = 0.04
        _TearMaxShift   ("Max Shift",       Float) = 0.14
        _TearBigChance  ("Big Tear Chance", Float) = 0.9
        _TearBigShift   ("Big Tear Shift",  Float) = 0.4

        [Header(RGB Split)]
        _RGBIntensity   ("RGB Intensity",   Float) = 0.0
        _RGBHOffset     ("H Offset",        Float) = 0.016
        _RGBVOffset     ("V Offset",        Float) = 0.022
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert_img
            #pragma fragment frag
            #pragma target   3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _TearIntensity;
            float _TearSeed;
            float _BlockSize;
            float _TearMaxShift;
            float _TearBigChance;
            float _TearBigShift;
            float _RGBIntensity;
            float _RGBHOffset;
            float _RGBVOffset;

            float Hash21(float2 p, float seed)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031,0.1030,0.0973) + seed * 0.007);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // 块撕裂
                if (_TearIntensity > 0.001)
                {
                    float blockID = floor(uv.y / _BlockSize);
                    float trig    = Hash21(float2(blockID, _TearSeed), _TearSeed);
                    float shift   = 0.0;

                    if (trig < 0.7)
                        shift = (Hash21(float2(blockID + 0.5, _TearSeed), _TearSeed) * 2.0 - 1.0)
                                * _TearMaxShift * _TearIntensity;

                    float bigTrig = Hash21(float2(blockID * 1.3, _TearSeed + 0.1), _TearSeed);
                    if (bigTrig > _TearBigChance)
                        shift += (Hash21(float2(blockID * 0.7, _TearSeed), _TearSeed) * 2.0 - 1.0)
                                 * _TearBigShift * _TearIntensity;

                    uv.x = frac(uv.x + shift);
                }

                // RGB 分离
                if (_RGBIntensity > 0.001)
                {
                    float2 uvR = float2(frac(uv.x + _RGBHOffset * _RGBIntensity), saturate(uv.y - _RGBVOffset * _RGBIntensity));
                    float2 uvB = float2(frac(uv.x - _RGBHOffset * _RGBIntensity), saturate(uv.y + _RGBVOffset * _RGBIntensity));
                    float r = tex2D(_MainTex, uvR).r;
                    float g = tex2D(_MainTex, uv ).g;
                    float b = tex2D(_MainTex, uvB).b;
                    return fixed4(r, g, b, 1.0);
                }

                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
