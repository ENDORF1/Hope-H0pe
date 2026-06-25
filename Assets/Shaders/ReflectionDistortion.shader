Shader "XiWang/ReflectionDistortion"
{
    Properties
    {
        _MainTex        ("Reflection RT", 2D) = "white" {}
        _Time2          ("Time", Float) = 0
        _WaveAmplitude  ("Wave Amplitude", Float) = 0.006
        _WaveFrequency  ("Wave Frequency", Float) = 12.0
        _WaveSpeed      ("Wave Speed", Float) = 1.0
        _WaveDepthScale ("Wave Depth Scale", Float) = 1.8
        _BlurRadius     ("Blur Radius", Float) = 0.003
        _BlurDepthScale ("Blur Depth Scale", Float) = 2.5
        _Brightness     ("Brightness", Float) = 0.45
        _TintColor      ("Tint Color", Color) = (0.85, 0.92, 1.0, 1.0)
        _FadeStart      ("Fade Start", Float) = 0.15
        _FadeEnd        ("Fade End", Float) = 0.85
        _FadePower      ("Fade Power", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Time2;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveSpeed;
            float _WaveDepthScale;
            float _BlurRadius;
            float _BlurDepthScale;
            float _Brightness;
            float4 _TintColor;
            float _FadeStart;
            float _FadeEnd;
            float _FadePower;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 col : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                o.col = v.color;
                return o;
            }

            float4 BlurSample(sampler2D tex, float2 uv, float radius)
            {
                float4 col = float4(0,0,0,0);
                float totalW = 0.0;

                float2 o[9];
                o[0]=float2(-1,-1); o[1]=float2(0,-1); o[2]=float2(1,-1);
                o[3]=float2(-1, 0); o[4]=float2(0, 0); o[5]=float2(1, 0);
                o[6]=float2(-1, 1); o[7]=float2(0, 1); o[8]=float2(1, 1);

                float w[9];
                w[0]=1; w[1]=2; w[2]=1;
                w[3]=2; w[4]=4; w[5]=2;
                w[6]=1; w[7]=2; w[8]=1;

                for (int i = 0; i < 9; i++)
                {
                    float2 suv = clamp(uv + o[i] * radius, 0.001, 0.999);
                    col    += tex2D(tex, suv) * w[i];
                    totalW += w[i];
                }
                return col / totalW;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                uv.y = 1.0 - uv.y;

                float depth = 1.0 - i.uv.y;

                float depthAmp = 1.0 + depth * _WaveDepthScale;
                float wave1 = sin(uv.y * _WaveFrequency       + _Time2 * _WaveSpeed)       * _WaveAmplitude * depthAmp;
                float wave2 = sin(uv.y * _WaveFrequency * 1.7 + _Time2 * _WaveSpeed * 0.6) * _WaveAmplitude * 0.5 * depthAmp;
                uv.x = clamp(uv.x + wave1 + wave2, 0.001, 0.999);

                float blurR = _BlurRadius * (1.0 + depth * _BlurDepthScale);
                float4 col  = BlurSample(_MainTex, uv, blurR);

                col.rgb *= _Brightness * _TintColor.rgb;

                float fadeT = saturate((i.uv.y - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
                float alpha  = saturate(1.0 - pow(fadeT, _FadePower));
                col.a = alpha * i.col.a;

                return col;
            }
            ENDCG
        }
    }

    FallBack "Transparent/VertexLit"
}
