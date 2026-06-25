Shader "XiWang_XiWang/HopeSin"
{
    Properties
    {
        _Color     ("Hope Color", Color)      = (0.24, 0.91, 0.78, 1)
        _Time2     ("Time",       Float)      = 0
        _Progress  ("Progress",   Float)      = 0
        _Aspect    ("Aspect",     Float)      = 1.777
        _Alpha     ("Alpha",      Float)      = 1
        _DotCount  ("Dot Count",  Float)      = 60
        _ScreenH   ("Screen Height", Float)   = 1080
        _WaveCount ("Wave Count", Float)      = 8
        _RInner    ("R Inner",    Float)      = 0.15
        _AmpBase   ("Amp Base",   Float)      = 0.018
        _AmpStep   ("Amp Step",   Float)      = 0.007
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
            float  _Time2, _Progress, _Aspect, _Alpha, _DotCount, _ScreenH;
            float  _WaveCount, _RInner, _AmpBase, _AmpStep;

            v2f vert(appdata v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }

            float hashF(float n){ n=frac(n*0.1031); n*=n+33.33; n*=n+n; return frac(n); }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float  t  = _Time2;
                float  p  = _Progress; // 0~1

                // ── 极坐标 ──────────────────────────────────────
                // 以屏幕中心为原点，修正 aspect
                float2 c  = uv - 0.5;
                c.x      *= _Aspect;
                float r   = length(c);
                float theta = atan2(c.y, c.x); // -PI ~ PI，归一化到 0~1
                float thetaN = theta / (3.14159265 * 2.0) + 0.5;

                // 屏幕角落到中心最大半径（aspect修正后）
                // 最远角落约 sqrt((aspect*0.5)^2 + 0.5^2)
                float rMax = sqrt(_Aspect * _Aspect * 0.25 + 0.25);

                // ── Progress 三段驱动 ────────────────────────────
                // 段1: 0   ~ 0.45  从中心向外爆发（rFront 从 0 → rMax）
                // 段2: 0.45~ 0.60  呼吸脉冲
                // 段3: 0.60~ 1.0   从中心向外逐渐淡出

                float rInner = _RInner;
                float rFront;      // 已扩散到的最远半径
                float breatheMod = 1.0;
                float fadeMask   = 1.0;

                if (p < 0.45)
                {
                    float pp = smoothstep(0.0, 0.45, p);
                    rFront   = lerp(rInner, rMax, pp);
                }
                else if (p < 0.60)
                {
                    rFront    = rMax;
                    float bp  = (p - 0.45) / 0.15;
                    float bv  = sin(bp * 3.14159265);
                    breatheMod = 1.0 + bv * 0.6;
                }
                else
                {
                    // 从中心向外消退：内侧先消失
                    rFront      = rMax;
                    float ep    = (p - 0.60) / 0.40;
                    float rFade = lerp(rInner, rMax + 0.1, smoothstep(0.0, 1.0, ep));
                    fadeMask    = smoothstep(rFade - 0.06, rFade, r);
                }

                float result = 0.0;

                // ── 放射线：均匀分布角度，沿半径向中心延伸 ──────
                // 每条线固定在某个 θ 角，带波浪扰动
                // 线的"存在范围"：r 从 rMax 推进到 rFront
                float sY      = _ScreenH / 360.0;
                float rPx     = _ScreenH * sY; // 参考像素尺度

                int wc = (int)_WaveCount;
                for (int k = 0; k < 64; k++)
                {
                    if (k >= wc) break;
                    float fk = (float)k;

                    // 每条线的基准角度（均匀分布 0~2PI）
                    float baseAngle = (fk / _WaveCount) * 3.14159265 * 2.0;

                    // 波形扰动：沿 r 方向用三层 sin 叠加，让线边缘不规则
                    float freq  = 0.008 + fk * 0.003;
                    float amp   = _AmpBase + fk * _AmpStep;
                    float speed = 0.4 + fk * 0.15;
                    float ph    = t * speed + fk * 1.2;
                    float rNorm = r / max(rMax, 0.001); // 归一化半径 0~1

                    // 扰动量作用在角度方向
                    float noise = sin(rNorm * rPx * freq + ph)             * amp
                                + sin(rNorm * rPx * freq * 2.3 + ph * 1.7) * amp * 0.4
                                + sin(rNorm * rPx * freq * 0.5 + ph * 0.3) * amp * 0.6;

                    // 当前像素到这条线的角度距离
                    float lineAngle = baseAngle + noise;
                    // 角度差（wrap 处理）
                    float dTheta = theta - lineAngle;
                    // wrap 到 -PI~PI
                    dTheta = dTheta - round(dTheta / (3.14159265 * 2.0)) * 3.14159265 * 2.0;

                    // 线宽：固定像素宽度转角度，不受 aspect 和 r 拉伸影响
                    float lineHalfAngle = (1.5 / _ScreenH) / max(r, 0.01);
                    float lineA = smoothstep(lineHalfAngle, 0.0, abs(dTheta));

                    // 只在 rInner ~ rFront 范围内显示（从中心向外扩散）
                    float radialMask = step(rInner, r) * step(r, rFront);

                    // 前沿淡入（扩散前沿附近淡入）
                    float enterFade = smoothstep(rFront, rFront - 0.06, r);
                    // 中心淡出
                    float centerFade = smoothstep(rInner * 0.5, rInner, r);

                    float brightness = (1.0 - fk / max(_WaveCount, 1.0) * 0.5) * breatheMod;

                    result += lineA * radialMask * enterFade * centerFade * brightness;
                }

                // ── 漂浮小点（围绕中心随机分布）────────────────
                int dotCount = (int)_DotCount;
                for (int j = 0; j < 60; j++)
                {
                    if (j >= dotCount) break;
                    float fj = (float)j;
                    float pr = hashF(fj * 3.7 + 1.0) * rMax;
                    float pa = hashF(fj * 5.1 + 2.0) * 3.14159265 * 2.0;
                    pa += t * (0.1 + hashF(fj * 2.3) * 0.2);
                    pr += sin(t * 0.3 + fj * 1.1) * 0.04;
                    float2 pPos  = float2(cos(pa) * pr * (1.0 / _Aspect), sin(pa) * pr) + 0.5;
                    float dotR   = 3.0 / _ScreenH;
                    // 光点也只在已扩散区域内显示
                    float dotInRange = step(rInner, length(float2((pPos.x-0.5)*_Aspect, pPos.y-0.5)))
                                     * step(length(float2((pPos.x-0.5)*_Aspect, pPos.y-0.5)), rFront);
                    result += smoothstep(dotR, 0.0, length(uv - pPos)) * 0.18 * breatheMod * dotInRange;
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
