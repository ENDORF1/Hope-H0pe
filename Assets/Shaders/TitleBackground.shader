Shader "XiWang_XiWang/TitleBackground"
{
    Properties
    {
        _Blend       ("Blend",            Float)   = 0
        _Time2       ("Time",             Float)        = 0
        _HopeColor   ("Hope Color",       Color)        = (0.29, 0.62, 1, 1)
        _VoidColor   ("Void Color",       Color)        = (0.86, 0.20, 0.20, 1)
        _Aspect      ("Aspect Ratio",     Float)        = 1.777
        _Darkness    ("Darkness",          Range(0,1))   = 0
        _RollOffset  ("Roll Offset",        Float)        = 0
        _ScrollOffsetX ("Scroll Offset X", Float)        = 0

        [Header(Void Layout)]
        _VoidYMax ("Void Y Max (0=top, 1=bottom)", Float) = 1.0

        [Header(Void Lines)]
        _LineCount      ("Line Count",       Float)   = 30
        _LinePeriodBase ("Breathe Period",   Float)   = 8
        _LinePeriodVar  ("Period Variance",  Float)    = 0.8

        [Header(Void Edge Dashes)]
        _DashCount   ("Dash Count",       Float)  = 60
        _DashLenMin  ("Dash Len Min",     Float)  = 20
        _DashLenMax  ("Dash Len Max",     Float) = 120
        _DashWMin    ("Dash Width Min",   Float) = 0.002
        _DashWMax    ("Dash Width Max",   Float) = 0.012

        [Header(Void Heartbeat)]
        _BeatInterval ("Beat Interval",   Float) = 6
        _BeatDuration ("Beat Duration",   Float)  = 0.8
        _GlowStrength ("Glow Strength",   Float)    = 0.15
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

            float  _Blend, _Time2, _Aspect;
            float4 _HopeColor, _VoidColor;
            float  _VoidYMax;
            float  _LineCount, _LinePeriodBase, _LinePeriodVar;
            float  _DashCount, _DashLenMin, _DashLenMax, _DashWMin, _DashWMax;
            float  _BeatInterval, _BeatDuration, _GlowStrength;
            float  _Darkness;
            float  _RollOffset;
            float  _ScrollOffsetX;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            // 稳定哈希：不用 sin，避免精度问题
            float hashF(float n)
            {
                n = frac(n * 0.1031);
                n *= n + 33.33;
                n *= n + n;
                return frac(n);
            }

            // 呼吸：sin²，在周期内取模避免大数精度丢失
            float breathe(float t, float period, float phaseNorm)
            {
                float tNorm = frac(t / period + phaseNorm);
                float v = sin(tNorm * 3.14159265 * 2.0) * 0.5 + 0.5;
                return v * v;
            }
            // 优化版：调用方预算 invPeriod = 1/period，省掉循环内除法
            float breatheOpt(float t, float invPeriod, float phaseNorm)
            {
                float tNorm = frac(t * invPeriod + phaseNorm);
                float v = sin(tNorm * 3.14159265 * 2.0) * 0.5 + 0.5;
                return v * v;
            }

            // 心跳波形
            float heartbeat(float t)
            {
                float p1 = t < 0.12 ? t / 0.12
                         : t < 0.30 ? 1.0 - (t - 0.12) / 0.18
                         : 0.0;
                float p2 = (t > 0.30 && t < 0.50)
                         ? sin((t - 0.30) / 0.20 * 3.14159265) * 0.30 : 0.0;
                float dc = t > 0.50 ? exp(-(t - 0.50) * 10.0) * 0.06 : 0.0;
                return max(0.0, p1 + p2 + dc);
            }

            // Hope
            float4 DrawHope(float2 uv, float3 col, float alpha)
            {
                float result = 0.0;
                float refH = 560.0;
                float refW = refH * _Aspect;
                float xPx  = uv.x * refW;
                float yPx  = uv.y * refH;

                for (int i = 0; i < 8; i++)
                {
                    float fi    = (float)i;
                    // 无缝接边：展开循环，每条线硬编码整数周期数
                    // sin(uv.x * N * 2π + ph)，N为整数，uv.x 0->1 相位差精确是N*2π
                    float twoPi = 6.28318530;
                    float amp   = 30.0  + fi * 12.0;
                    float speed = 0.4   + fi * 0.15;
                    float ph    = _Time2 * speed + fi * 1.2;
                    float yBase = refH * 0.5 + (fi - 3.5) * 22.0;
                    float nBase, n2, n3;
                    if      (i==0) { nBase=1; n2=3;  n3=1; }
                    else if (i==1) { nBase=2; n2=4;  n3=1; }
                    else if (i==2) { nBase=2; n2=5;  n3=1; }
                    else if (i==3) { nBase=3; n2=6;  n3=1; }
                    else if (i==4) { nBase=3; n2=7;  n3=2; }
                    else if (i==5) { nBase=4; n2=8;  n3=2; }
                    else if (i==6) { nBase=4; n2=9;  n3=2; }
                    else           { nBase=5; n2=11; n3=2; }
                    float noise = sin(uv.x * nBase * twoPi + ph) * amp
                                + sin(uv.x * n2    * twoPi + ph * 1.7) * amp * 0.4
                                + sin(uv.x * n3    * twoPi + ph * 0.3) * amp * 0.6;
                    float dist  = abs(yPx - (yBase + noise)) / refH;
                    result     += smoothstep(0.003, 0.0, dist) * (1.0 - fi * 0.08);
                }

                for (int k = 0; k < 60; k++)
                {
                    float fk = (float)k;
                    float px = frac(sin(_Time2 * 0.3 + fk * 2.1) * 0.5 + 0.5);
                    float py = frac(cos(_Time2 * 0.2 + fk * 1.7) * 0.5 + 0.5);
                    float2 diff = uv - float2(px, py);
                    diff.x = diff.x - round(diff.x); // 水平环绕
                    result  += smoothstep(0.005, 0.0, length(diff)) * 0.15;
                }

                result = saturate(result) * alpha;
                return float4(col * result, result);
            }

            // Void
            float4 DrawVoid(float2 uv, float3 col, float alpha)
            {
                // 只在指定范围内绘制，下方留给倒影
                if (uv.y < (1.0 - _VoidYMax)) return float4(0, 0, 0, 0);
                float result = 0.0;
                float refH   = 560.0;
                float yPx    = uv.y * refH;
                float t      = _Time2;

                // 心跳
                float cyclePos = fmod(t, _BeatInterval);
                float beat     = (cyclePos < _BeatDuration)
                               ? heartbeat(cyclePos / _BeatDuration) : 0.0;

                // 水平线：v4 逻辑，全局呼吸 + 每条线微小相位偏移
                float invPeriodBase = 1.0 / max(_LinePeriodBase, 0.001); // 预算，省掉循环内除法
                float lineHalfPx = 1.5 / refH;
                int   lineCount  = (int)_LineCount;
                for (int i = 0; i < 80; i++)
                {
                    if (i >= lineCount) break;
                    float fi    = (float)i;
                    float fracI = (fi + 1.0) / (_LineCount + 1.0);
                    float y     = refH * fracI;
                    float distC = abs(fracI - 0.5) * 2.0;

                    float phaseNorm = fi * 0.08 * invPeriodBase;
                    float b     = breatheOpt(t, invPeriodBase, phaseNorm);

                    float lineA = (1.0 - distC * 0.7) * 0.55 * (0.3 + b * 0.7);
                    float d     = abs(yPx - y) / refH;
                    result     += smoothstep(lineHalfPx, 0.0, d) * lineA;
                }

                // 四边短线：hashF 从 6 次减到 3 次，用数值派生替代
                int dashCount = (int)_DashCount;
                for (int j = 0; j < 200; j++)
                {
                    if (j >= dashCount) break;
                    float fj = (float)j;

                    // 3 次 hashF，每次派生 2 个独立随机值
                    float ha = hashF(fj * 7.0 + 1.0);
                    float hb = hashF(fj * 7.0 + 3.0);
                    float hc = hashF(fj * 7.0 + 5.0);
                    float ha2 = frac(ha * 17.3 + 0.1);  // 从 ha 派生第二个值（替代原 h2d）
                    float hb2 = frac(hb * 13.7 + 0.3);  // 从 hb 派生第二个值（替代原 h4d）
                    float hc2 = frac(hc * 11.1 + 0.5);  // 从 hc 派生第二个值（替代原 h6d）

                    int   edge    = (int)(ha * 4.0);
                    float pos     = ha2;
                    float dperiod = 4.0 + hb * 12.0;
                    float maxLen  = (_DashLenMin + hc * (_DashLenMax - _DashLenMin)) / refH;
                    float dashW   = _DashWMin + hc2 * (_DashWMax - _DashWMin);

                    float invDP   = 1.0 / max(dperiod, 0.001); // 预算，省掉 breathe 内的除法
                    float bd      = breatheOpt(t, invDP, hb2);
                    float len     = maxLen * (bd + beat * 0.5);
                    float dAlpha  = (0.15 + hc * 0.3) * bd + beat * 0.25;

                    if (len < 0.001) continue;

                    float contrib = 0.0;
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

                // 辉光
                float globalB = breatheOpt(t, invPeriodBase, 0.0);
                float2 c2     = uv - float2(0.5, 0.5);
                float glowA   = 0.04 + globalB * 0.06 + beat * _GlowStrength;
                float glowD   = length(c2) / max(0.25 + beat * 0.5, 0.001);
                result       += exp(-glowD * glowD * 4.0) * glowA
                              + exp(-glowD * glowD * 12.0) * glowA * 0.4;

                result = saturate(result);
                return float4(col * result, result);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv  = i.uv;
                // 失步：UV垂直偏移+wrap，模拟老式电视画面滚动
                uv.y = frac(uv.y + _RollOffset);
                // 转场：UV横向偏移，模拟镜头向左推移
                uv.x = frac(uv.x + _ScrollOffsetX);
                float3 col = lerp(_HopeColor.rgb, _VoidColor.rgb, _Blend);

                float4 hopeR = float4(0,0,0,0);
                float4 voidR = float4(0,0,0,0);

                float hA = saturate(1.0 - _Blend * 2.0) * 0.7;
                if (hA > 0.001) hopeR = DrawHope(uv, col, hA);

                float vA = saturate((_Blend - 0.5) * 2.0) * 0.7;
                if (vA > 0.001) voidR = DrawVoid(uv, col, vA);

                float4 final = hopeR + voidR;
                final.a = saturate(final.a);

                float2 c2      = uv - 0.5;
                float vignette = 1.0 - smoothstep(0.3, 0.8, length(c2) * 1.4);
                final.rgb     *= vignette;
                final.a       *= vignette * 0.9 + 0.1;

                // 渐黑压暗（voidGhost 序列驱动）
                final.rgb *= (1.0 - _Darkness);
                return final;
            }
            ENDCG
        }
    }
}
