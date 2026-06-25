Shader "XiWang_XiWang/MenuButtonWave"
{
    Properties
    {
        _Color      ("Color",          Color)  = (0.29, 0.62, 1, 1)
        _Time2      ("Time",           Float)  = 0
        _Progress   ("Progress 0~1",   Float)  = 0
        _BtnHeight  ("Button Height",  Float)  = 60
        _Aspect     ("Aspect W/H",     Float)  = 5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay+10" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
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
            float  _Time2, _Progress, _BtnHeight, _Aspect;

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
                float  t  = _Time2;
                float  p  = _Progress;

                // 像素空间，以按钮高度为参考
                float refH = _BtnHeight;
                float refW = refH * _Aspect;
                float xPx  = uv.x * refW;
                float yPx  = uv.y * refH;

                // 延伸遮罩：Progress 控制从两侧延伸出去的范围
                // uv.x=0 是左边缘，uv.x=1 是右边缘
                // 左侧线条：uv.x < 0.5，Progress决定能显示多远
                // 右侧线条：uv.x > 0.5，对称
                // 中心区域（文字区域）不画线，留出 centerGap
                float centerGap = 0.12; // 中心留空比例（文字两侧各留一点）
                float reach     = p * (0.5 - centerGap); // 最大延伸到边缘的 0~(0.5-gap)

                float distFromCenter = abs(uv.x - 0.5);
                // 只在 centerGap ~ (0.5) 范围内绘制
                // 线从 centerGap 开始，向外到 centerGap+reach
                float outerEdge = centerGap + reach;
                float drawMask  = step(centerGap, distFromCenter) * step(distFromCenter, outerEdge);

                // 沿延伸方向的淡出：越靠近外边缘越淡
                float depthInLine = (distFromCenter - centerGap) / max(reach, 0.001);
                float fadeTip  = smoothstep(1.0, 0.6, depthInLine); // 前端淡出
                float fadeRoot = smoothstep(0.0, 0.08, depthInLine); // 根部淡入

                float result = 0.0;

                // 8条波形线，公式与 TitleBackground DrawHope 完全一致
                for (int k = 0; k < 8; k++)
                {
                    float fk    = (float)k;
                    float freq  = 0.008 + fk * 0.003;
                    float amp   = refH * 0.05 + fk * refH * 0.02; // 幅度随按钮高度缩放
                    float speed = 0.4   + fk * 0.15;
                    float ph    = t * speed + fk * 1.2;
                    // yBase：从按钮垂直中心均匀分布
                    float yBase = refH * 0.5 + (fk - 3.5) * (refH * 0.12);
                    float noise = sin(xPx * freq + ph)               * amp
                                + sin(xPx * freq * 2.3 + ph * 1.7)   * amp * 0.4
                                + sin(xPx * freq * 0.5 + ph * 0.3)   * amp * 0.6;
                    float dist  = abs(yPx - (yBase + noise)) / refH;
                    float lineA = (1.0 - fk * 0.08) * 0.85;
                    result     += smoothstep(0.004, 0.0, dist) * lineA;
                }

                result *= drawMask * fadeTip * fadeRoot;
                result  = saturate(result);

                if (result < 0.005) discard;
                return fixed4(_Color.rgb, result * _Color.a);
            }
            ENDCG
        }
    }
}
