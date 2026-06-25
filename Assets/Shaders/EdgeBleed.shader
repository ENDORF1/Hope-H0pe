Shader "XiWang_XiWang/EdgeBleed"
{
    Properties
    {
        _Color        ("Line Color",       Color)       = (0.86, 0.20, 0.20, 1)
        _Time2        ("Time",             Float)       = 0
        _Intensity    ("Intensity",        Range(0,1))  = 0
        _Aspect       ("Aspect Ratio",     Float)       = 1.777

        [Header(Dash Lines)]
        _DashCount   ("Dash Count",       Float)  = 60
        _DashLenMin  ("Dash Len Min",     Float)  = 20
        _DashLenMax  ("Dash Len Max",     Float)  = 120
        _DashWMin    ("Dash Width Min",   Float)  = 0.002
        _DashWMax    ("Dash Width Max",   Float)  = 0.012

        [Header(Heartbeat)]
        _BeatInterval ("Beat Interval",   Float)  = 6
        _BeatDuration ("Beat Duration",   Float)  = 0.8
        _GlowStrength ("Glow Strength",   Float)  = 0.15
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 _Color;
            float  _Time2, _Intensity, _Aspect;
            float  _DashCount, _DashLenMin, _DashLenMax, _DashWMin, _DashWMax;
            float  _BeatInterval, _BeatDuration, _GlowStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            // ── 完全复制自 TitleBackground ─────────────────────────────

            float hashF(float n)
            {
                n = frac(n * 0.1031);
                n *= n + 33.33;
                n *= n + n;
                return frac(n);
            }

            float breathe(float t, float period, float phaseNorm)
            {
                float tNorm = frac(t / period + phaseNorm);
                float v = sin(tNorm * 3.14159265 * 2.0) * 0.5 + 0.5;
                return v * v;
            }

            float heartbeat(float t)
            {
                // 第一跳：瞬发，快速衰减
                float p1 = t < 0.30 ? 1.0 - t / 0.30 : 0.0;
                // 第二跳：0.30~0.50 之间的小峰
                float p2 = (t > 0.30 && t < 0.50)
                         ? sin((t - 0.30) / 0.20 * 3.14159265) * 0.35 : 0.0;
                // 余震
                float dc = t > 0.50 ? exp(-(t - 0.50) * 10.0) * 0.06 : 0.0;
                return max(0.0, p1 + p2 + dc);
            }

            // ──────────────────────────────────────────────────────────

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv  = i.uv;
                float  t   = _Time2;
                float  refH = 560.0; // 与 TitleBackground 一致

                // 心跳
                float cyclePos = fmod(t, _BeatInterval);
                float beat     = (cyclePos < _BeatDuration)
                               ? heartbeat(cyclePos / _BeatDuration) : 0.0;
                beat *= _Intensity;

                float result = 0.0;

                // ── 四边短线（原封不动复制自 TitleBackground DrawVoid）──
                int dashCount = (int)_DashCount;
                for (int j = 0; j < 200; j++)
                {
                    if (j >= dashCount) break;
                    float fj = (float)j;

                    float h1d = hashF(fj * 7.0 + 1.0);
                    float h2d = hashF(fj * 7.0 + 2.0);
                    float h3d = hashF(fj * 7.0 + 3.0);
                    float h4d = hashF(fj * 7.0 + 4.0);
                    float h5d = hashF(fj * 7.0 + 5.0);
                    float h6d = hashF(fj * 7.0 + 6.0);

                    int   edge    = (int)(h1d * 4.0);
                    float pos     = h2d;
                    float dperiod = 4.0 + h3d * 12.0;
                    float maxLen  = (_DashLenMin + h5d * (_DashLenMax - _DashLenMin)) / refH;
                    float dashW   = _DashWMin + h6d * (_DashWMax - _DashWMin);

                    // 只由心跳驱动，无持续呼吸
                    float len    = maxLen * beat;
                    float dAlpha = (0.15 + h5d * 0.5) * beat * _Intensity;

                    if (len < 0.001) continue;

                    float contrib  = 0.0;
                    float fadeEdge = 0.02;
                    if (edge == 0 && abs(uv.x - pos) < dashW && uv.y < len)
                        contrib = step(abs(uv.x - pos), dashW) * smoothstep(len, len - fadeEdge, uv.y);
                    else if (edge == 1 && abs(uv.x - pos) < dashW && (1.0 - uv.y) < len)
                        contrib = step(abs(uv.x - pos), dashW) * smoothstep(len, len - fadeEdge, 1.0 - uv.y);
                    else if (edge == 2 && abs(uv.y - pos) < dashW && uv.x < len)
                        contrib = step(abs(uv.y - pos), dashW) * smoothstep(len, len - fadeEdge, uv.x);
                    else if (edge == 3 && abs(uv.y - pos) < dashW && (1.0 - uv.x) < len)
                        contrib = step(abs(uv.y - pos), dashW) * smoothstep(len, len - fadeEdge, 1.0 - uv.x);

                    result += contrib * dAlpha;
                }

                // ── 心跳中心辉光（复制自 TitleBackground DrawVoid）──────
                float2 c2   = uv - float2(0.5, 0.5);
                float glowA = beat * _GlowStrength;
                float glowD = length(c2) / max(0.25 + beat * 0.5, 0.001);
                result += exp(-glowD * glowD * 4.0)  * glowA
                        + exp(-glowD * glowD * 12.0) * glowA * 0.4;

                result = saturate(result);
                return fixed4(_Color.rgb, result * _Color.a);
            }
            ENDCG
        }
    }
}
