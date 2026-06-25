Shader "XiWang/WaterReflection"
{
    Properties
    {
        _MainTex             ("Reflection RT",   2D)          = "white" {}
        _Time2               ("Time",            Float)       = 0
        _WaveStrength        ("Wave Strength",   Range(0,0.05)) = 0.008
        _WaveSpeed           ("Wave Speed",      Range(0,5))  = 1.2
        _WaveFreqX           ("Wave Freq X",     Range(0,20)) = 6.0
        _WaveFreqY           ("Wave Freq Y",     Range(0,20)) = 4.0
        _DistortionFalloff   ("Falloff",         Range(0,2))  = 1.0
        _Alpha               ("Alpha",           Range(0,1))  = 0.55
        _TintColor           ("Tint Color",      Color)       = (0.29, 0.62, 1.0, 0.12)
        _FadeStart           ("Fade Start",      Range(0,1))  = 0.3
        _FadeEnd             ("Fade End",        Range(0,1))  = 1.0
        _UseFade             ("Use Fade (0=off)",Float)       = 1.0
        _FlipY               ("Flip Y",          Float)       = 1.0
        _ChromaticAberration ("Chromatic",       Range(0,0.01)) = 0.002
        _ScanlineStrength    ("Scanlines",       Range(0,1))  = 0.15
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            float     _Time2;
            float     _WaveStrength;
            float     _WaveSpeed;
            float     _WaveFreqX;
            float     _WaveFreqY;
            float     _DistortionFalloff;
            float     _Alpha;
            fixed4    _TintColor;
            float     _FadeStart;
            float     _FadeEnd;
            float     _UseFade;
            float     _FlipY;
            float     _ChromaticAberration;
            float     _ScanlineStrength;
            float     _MaskAlpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                o.color  = v.color;
                return o;
            }

            float2 WaveOffset(float2 uv, float t)
            {
                float depthFactor = pow(uv.y, _DistortionFalloff);
                float wave1 = sin(uv.x * _WaveFreqX + t * _WaveSpeed) * 0.6
                            + sin(uv.x * _WaveFreqX * 0.5 + t * _WaveSpeed * 0.7 + 1.3) * 0.4;
                float wave2 = sin(uv.y * _WaveFreqY * 2.0 + t * _WaveSpeed * 1.4 + uv.x * 3.0) * 0.35
                            + cos(uv.y * _WaveFreqY + t * _WaveSpeed * 0.9 + 2.1) * 0.25;
                float wave3 = sin(uv.x * _WaveFreqX * 2.3 + uv.y * 4.0 + t * _WaveSpeed * 2.1) * 0.15;
                float dx = (wave1 + wave3) * _WaveStrength * depthFactor;
                float dy = wave2 * _WaveStrength * depthFactor * 0.5;
                return float2(dx, dy);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                if (_FlipY > 0.5)
                    uv.y = 1.0 - uv.y;

                float2 offset     = WaveOffset(float2(uv.x, 1.0 - uv.y), _Time2);
                float2 distortedUV = uv + offset;

                float  ca   = _ChromaticAberration * (1.0 + pow(1.0 - uv.y, 2.0));
                fixed4 colR = tex2D(_MainTex, distortedUV + float2( ca, 0));
                fixed4 colG = tex2D(_MainTex, distortedUV);
                fixed4 colB = tex2D(_MainTex, distortedUV + float2(-ca, 0));

                fixed4 col;
                col.r = colR.r;
                col.g = colG.g;
                col.b = colB.b;
                col.a = colG.a;

                if (_ScanlineStrength > 0.001)
                {
                    float scanline = sin(uv.y * _MainTex_TexelSize.w * 1.5 + _Time2 * 0.4) * 0.5 + 0.5;
                    col.rgb *= 1.0 - scanline * _ScanlineStrength * 0.15;
                }

                float fadeUV   = 1.0 - uv.y;
                float fadeMask = (_UseFade > 0.5)
                    ? 1.0 - smoothstep(_FadeStart, _FadeEnd, fadeUV)
                    : 1.0;

                col.a *= lerp(1.0, fadeMask, _MaskAlpha) * _Alpha;
                col   *= i.color;

                // 色调叠加
                col.rgb = lerp(col.rgb, _TintColor.rgb, _TintColor.a);

                col.a = saturate(col.a);

                return col;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
