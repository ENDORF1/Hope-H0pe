Shader "XiWang_XiWang/MenuButtonLineGlow"
{
    Properties
    {
        _MainTex    ("Texture",    2D)    = "white" {}   // RawImage 必须
        _Color      ("发光颜色",   Color) = (0.78, 0.20, 0.20, 1)
        _Alpha      ("整体透明度", Float) = 1
        _GlowWidth  ("辉光宽度",   Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" }
        Blend One One         // 加法混合，完全还原 CSS box-shadow 叠加感
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _Color;
            float  _Alpha;
            float  _GlowWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // uv.y=0.5 是线条中心
                // 纵向高斯分布，模拟 CSS box-shadow 多层叠加
                float dy = abs(i.uv.y - 0.5) * 2.0; // 0=中心 1=边缘

                // 五层辉光，对应 CSS 里的五个 box-shadow 值
                // 10px / 20px / 30px / 50px / 100px
                float g1 = exp(-dy*dy * 200.0);  // 最内层，最亮最窄
                float g2 = exp(-dy*dy *  80.0);
                float g3 = exp(-dy*dy *  30.0);
                float g4 = exp(-dy*dy *  10.0);
                float g5 = exp(-dy*dy *   2.5); // 最外层，最暗最宽

                // 权重对应 CSS 里 box-shadow 从小到大递减的感觉
                float glow = g1 * 1.0
                           + g2 * 0.7
                           + g3 * 0.4
                           + g4 * 0.2
                           + g5 * 0.08;

                glow *= _GlowWidth; // 整体宽度缩放

                float3 col = _Color.rgb * glow * _Alpha;

                if(max(col.r, max(col.g, col.b)) < 0.002) discard;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
