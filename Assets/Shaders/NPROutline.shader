Shader "Custom/NPROutline"
{
    Properties
    {
        _BaseColor    ("Base Color",    Color)            = (1,1,1,1)
        _BaseMap      ("Base Texture",  2D)               = "white" {}
        _OutlineColor ("Outline Color", Color)            = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0, 0.1))   = 0.005
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        // ── Pass 1：正常渲染（支持透明淡入）─────────────
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _BaseMap;
            float4    _BaseMap_ST;
            fixed4    _BaseColor;
            fixed4    _OutlineColor;
            float     _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos    = UnityObjectToClipPos(v.vertex);
                o.uv     = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex    = tex2D(_BaseMap, i.uv) * _BaseColor;
                float NdotL   = dot(normalize(i.normal), _WorldSpaceLightPos0.xyz);
                float lambert = NdotL * 0.5 + 0.5;
                tex.rgb *= lambert * _LightColor0.rgb;
                return tex; // alpha来自_BaseColor.a，支持淡入
            }
            ENDCG
        }

        // ── Pass 2：背面扩张描边（跟随主体alpha）────────
        Pass
        {
            Cull Front
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM
            #pragma vertex   vertOutline
            #pragma fragment fragOutline
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            fixed4 _BaseColor;
            float  _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vertOutline(appdata v)
            {
                v2f o;
                float3 expanded = v.vertex.xyz + v.normal * _OutlineWidth;
                o.pos = UnityObjectToClipPos(expanded);
                return o;
            }

            fixed4 fragOutline(v2f i) : SV_Target
            {
                fixed4 col = _OutlineColor;
                col.a = _BaseColor.a; // 描边alpha跟随主体淡入
                return col;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
