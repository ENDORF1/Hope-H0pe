Shader "XiWang/WaterSurface"
{
    Properties
    {
        _MainTex        ("Reflection RT", 2D)       = "black" {}

        [Header(Wave Distortion)]
        _WaveAmplitude  ("Wave Amplitude",  Range(0, 0.05))  = 0.008
        _WaveFrequency  ("Wave Frequency",  Range(0, 20))    = 6.0
        _WaveSpeed      ("Wave Speed",      Range(0, 5))     = 1.2
        _WaveScale      ("Wave Scale Y (depth stretch)", Range(0.5, 3)) = 1.4

        [Header(Appearance)]
        _Alpha          ("Overall Alpha",   Range(0, 1))     = 0.55
        _FadeStart      ("Fade Start (0=top)", Range(0, 1))  = 0.0
        _FadeEnd        ("Fade End   (1=bottom)", Range(0, 1)) = 0.85
        _TintColor      ("Tint Color",      Color)           = (0.18, 0.55, 0.9, 1)
        _TintStrength   ("Tint Strength",   Range(0, 1))     = 0.25

        [Header(Faction Preview Blend)]
        _BlendTex       ("Opposite Faction RT", 2D) = "black" {}
        _PreviewBlend   ("Preview Blend",   Range(0, 1))     = 0.0

        _Time2          ("Time (driven by script)", Float)   = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull   Off
        ZWrite Off
        Blend  SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BlendTex;

            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveSpeed;
            float _WaveScale;

            float _Alpha;
            float _FadeStart;
            float _FadeEnd;
            float4 _TintColor;
            float _TintStrength;
            float _PreviewBlend;
            float _Time2;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // ── UV 翻转（倒影朝上）──────────────────────
                uv.y = 1.0 - uv.y;

                // ── 波纹扰动 ────────────────────────────────
                // 越靠近底部（距水面越远）扰动越强
                float depthFactor = uv.y * _WaveScale;
                float offsetX = sin(uv.y * _WaveFrequency + _Time2 * _WaveSpeed)
                              * _WaveAmplitude * depthFactor;
                float offsetY = cos(uv.x * _WaveFrequency * 0.7 + _Time2 * _WaveSpeed * 0.8)
                              * _WaveAmplitude * 0.5 * depthFactor;
                float2 distortedUV = float2(uv.x + offsetX, uv.y + offsetY);
                distortedUV = clamp(distortedUV, 0.001, 0.999);

                // ── 采样两张RT ───────────────────────────────
                fixed4 colMain  = tex2D(_MainTex,  distortedUV);
                fixed4 colBlend = tex2D(_BlendTex, distortedUV);
                fixed4 col      = lerp(colMain, colBlend, _PreviewBlend);

                // ── 色调叠加 ─────────────────────────────────
                col.rgb = lerp(col.rgb, _TintColor.rgb, _TintStrength);

                // ── 垂直渐隐（从水面线向下淡出）──────────────
                // i.uv.y=0 是顶部(水面线), i.uv.y=1 是底部
                float fadeT = saturate((i.uv.y - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
                float fade  = 1.0 - fadeT;           // 顶部不透明，底部透明

                col.a = _Alpha * fade;

                return col;
            }
            ENDCG
        }
    }

    FallBack "Transparent/VertexLit"
}
