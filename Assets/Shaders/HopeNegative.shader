Shader "XiWang_XiWang/HopeNegative"
{
    Properties
    {
        _MainTex   ("Screen Tex",  2D)          = "white" {}
        _Color     ("Hope Color",  Color)        = (0.24, 0.91, 0.78, 1)
        _Intensity ("Intensity",   Range(0,1))   = 0
        _TintStrength ("Tint Strength", Range(0,1)) = 0.25
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
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f     { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            sampler2D _MainTex;
            float4 _Color;
            float  _Intensity, _TintStrength;

            v2f vert(appdata v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }

            fixed4 frag(v2f i) : SV_Target
            {
                // 全白叠加 + 青色染色，模拟负片闪现
                float3 white = float3(1,1,1);
                float3 tint  = lerp(white, _Color.rgb, _TintStrength);
                return fixed4(tint, _Intensity);
            }
            ENDCG
        }
    }
}
