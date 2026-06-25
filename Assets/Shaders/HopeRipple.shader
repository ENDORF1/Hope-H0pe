Shader "XiWang_XiWang/HopeRipple"
{
    Properties
    {
        _Color      ("Hope Color",    Color)  = (0.24, 0.91, 0.78, 1)
        _Aspect     ("Aspect Ratio",  Float)  = 1.777
        _RippleWidth("Ring Width",    Float)  = 0.006
        _RippleCount("Active Count",  Int)    = 0
        _Drop       ("Water Drop",    Vector) = (0,0,0,-1)
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

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 _Color;
            float  _Aspect, _RippleWidth;
            int    _RippleCount;
            float4 _Drop;

            // 128个波纹，每个 float4 = (x, y, radius, alpha)，UV坐标
            float4 _Ripples[128];

            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }

            float rippleContrib(float2 uv, float4 r)
            {
                if(r.w < 0.001) return 0.0;
                float2 d = float2((uv.x - r.x) * _Aspect, uv.y - r.y);
                float dist = length(d);
                float ring = abs(dist - r.z);
                float main = smoothstep(_RippleWidth, 0.0, ring) * r.w;
                // 高光弧：点积代替 atan2
                float2 dn = (dist > 0.0001) ? d / dist : float2(0, 1);
                float dotVal = dot(dn, float2(-0.6, 0.8));
                float highlight = smoothstep(0.7, 1.0, dotVal) * 0.5;
                return main + main * highlight;
            }

            float dropContrib(float2 uv)
            {
                if(_Drop.w < 0.0) return 0.0;
                float2 d = float2((uv.x - _Drop.x) * _Aspect, uv.y - _Drop.y);
                float dist = length(d);
                float r = _Drop.z;
                float p = _Drop.w;
                if(p <= 1.0)
                {
                    if(dist > r) return 0.0;
                    float body    = (1.0 - smoothstep(0.85, 1.0, dist/r)) * 0.35;
                    float fresnel = smoothstep(0.7, 1.0, dist/r) * 0.45;
                    float2 dn = (dist > 0.0001) ? d/dist : float2(0,1);
                    float hl  = smoothstep(0.5, 1.0, dot(dn, float2(-0.7, 0.7))) * 0.85
                              * smoothstep(r, r*0.3, dist);
                    float hl2 = smoothstep(0.6, 1.0, dot(dn, float2(0.6, -0.8))) * 0.3
                              * smoothstep(r*0.6, 0.0, dist);
                    return body + fresnel + hl + hl2;
                }
                else
                {
                    float bp = p - 1.0;
                    return (1.0 - bp) * smoothstep(r*1.5, 0.0, dist) * 0.9;
                }
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float result = 0.0;
                int count = min(_RippleCount, 128);
                for(int k = 0; k < count; k++)
                    result += rippleContrib(uv, _Ripples[k]);
                result += dropContrib(uv);
                result = saturate(result);
                return fixed4(_Color.rgb, result * _Color.a);
            }
            ENDCG
        }
    }
}
