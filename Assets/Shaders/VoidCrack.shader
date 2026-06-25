Shader "XiWang_XiWang/VoidCrack"
{
    Properties
    {
        _Color     ("Void Color",    Color)  = (0.86, 0.20, 0.20, 1)
        _Time2     ("Time",          Float)  = 0
        _Progress  ("Progress",      Float)  = 0
        _Aspect    ("Aspect",        Float)  = 1.777
        _Alpha     ("Alpha",         Float)  = 1
        _ScreenH   ("Screen Height", Float)  = 1080
        _CrackCount("Crack Count",   Float)  = 12
        _RInner    ("R Inner",       Float)  = 0.12
        _AmpBase   ("Amp Base",      Float)  = 0.03
        _AmpStep   ("Amp Step",      Float)  = 0.005
        _JagAmp    ("Jag Amp",       Float)  = 0.06
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f     { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            float4 _Color;
            float  _Time2, _Progress, _Aspect, _Alpha, _ScreenH;
            float  _CrackCount, _RInner, _AmpBase, _AmpStep, _JagAmp;

            v2f vert(appdata v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }

            float hashF(float n){ n=frac(n*0.1031); n*=n+33.33; n*=n+n; return frac(n); }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv    = i.uv;
                float  t     = _Time2;
                float  p     = _Progress;

                // ── 极坐标 ──────────────────────────────────────
                float2 c  = uv - 0.5;
                c.x      *= _Aspect;
                float r      = length(c);
                float theta  = atan2(c.y, c.x);
                float rMax   = sqrt(_Aspect * _Aspect * 0.25 + 0.25);
                float rInner = _RInner;

                // ── Progress 三段驱动 ────────────────────────
                // 段1: 0   ~ 0.40  裂缝从 rMax 向内蔓延到 rInner
                // 段2: 0.40~ 0.70  两次电流脉冲（亮度闪烁）
                // 段3: 0.70~ 1.0   裂缝从外向内消退

                float rFront;
                float breatheMod = 1.0;
                float fadeMask   = 1.0;

                if (p < 0.40)
                {
                    float pp = smoothstep(0.0, 0.40, p);
                    rFront   = lerp(rMax, rInner, pp);
                }
                else if (p < 0.70)
                {
                    rFront       = rInner;
                    float bp     = (p - 0.40) / 0.30; // 0~1
                    // 4PI = 两个完整正弦周期 = 两次脉冲
                    float pulse  = sin(bp * 3.14159265 * 4.0);
                    breatheMod   = 1.0 + max(0.0, pulse) * 1.2;
                }
                else
                {
                    rFront       = rInner;
                    float ep     = (p - 0.70) / 0.30;
                    // 消退边界从 rMax 向 rInner 推进，外侧先消失
                    float rFade  = lerp(rMax, rInner - 0.05, smoothstep(0.0, 1.0, ep));
                    // r > rFade 的部分消失，r < rFade 的部分保留
                    fadeMask     = smoothstep(rFade + 0.06, rFade, r);
                    fadeMask    *= smoothstep(1.0, 0.85, ep);
                }

                float result = 0.0;
                float sY     = _ScreenH / 360.0;
                float rPx    = _ScreenH * sY;

                int cc = (int)_CrackCount;
                for (int k = 0; k < 64; k++)
                {
                    if (k >= cc) break;
                    float fk = (float)k;

                    // 基准角度：均匀分布 + 随机偏移，让裂缝分布不均匀
                    float baseAngle = (fk / _CrackCount) * 3.14159265 * 2.0
                                    + hashF(fk * 3.1 + 0.5) * 0.6;

                    float freq  = 0.012 + fk * 0.002;
                    float amp   = _AmpBase + fk * _AmpStep;
                    float speed = 0.5 + fk * 0.12;
                    float ph    = t * speed + fk * 2.1;
                    float rNorm = r / max(rMax, 0.001);

                    // 三层 sin + 高频锯齿（模拟裂缝的尖锐感）
                    float noise = sin(rNorm * rPx * freq + ph)              * amp
                                + sin(rNorm * rPx * freq * 2.3 + ph * 1.7)  * amp * 0.4
                                + sin(rNorm * rPx * freq * 0.5 + ph * 0.3)  * amp * 0.6
                                + sin(rNorm * rPx * freq * 11.3 + ph * 4.7) * _JagAmp * (1.0 - rNorm); // 越靠近中心锯齿越强

                    float lineAngle = baseAngle + noise;
                    float dTheta    = theta - lineAngle;
                    dTheta = dTheta - round(dTheta / (3.14159265 * 2.0)) * 3.14159265 * 2.0;

                    // 线宽：靠近中心时稍宽（裂缝蔓延感），外侧细
                    float widthScale    = 1.0 + (1.0 - rNorm) * 0.8;
                    float lineHalfAngle = (1.5 / _ScreenH) * widthScale / max(r, 0.01);
                    float lineA         = smoothstep(lineHalfAngle, 0.0, abs(dTheta));

                    // 只在 rFront ~ rMax 范围内显示（从外向内）
                    float radialMask = step(rFront, r) * step(r, rMax);

                    // 前沿淡入（裂缝蔓延前沿）
                    float enterFade  = smoothstep(rFront, rFront + 0.05, r);
                    // 中心发光增强（越靠近中心越亮，像能量汇聚）
                    float coreBright = 1.0 + smoothstep(rInner * 2.0, rInner, r) * 0.8 * breatheMod;

                    float brightness = (0.6 + fk / max(_CrackCount, 1.0) * 0.4) * breatheMod * coreBright;

                    result += lineA * radialMask * enterFade * brightness;
                }

                result  = saturate(result);
                result *= fadeMask * _Alpha * _Color.a;

                if (result < 0.005) discard;
                return fixed4(_Color.rgb, result);
            }
            ENDCG
        }
    }
}
