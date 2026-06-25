Shader "XiWang_XiWang/HopeWaves"
{
    Properties
    {
        _Color     ("Hope Color", Color)      = (0.24, 0.91, 0.78, 1)
        _Time2     ("Time",       Float)      = 0
        _Intensity ("Intensity",  Range(0,1)) = 0
        _Aspect    ("Aspect",     Float)      = 1.777
        _DotCount  ("Dot Count",  Float)      = 60
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
            float  _Time2, _Intensity, _Aspect, _DotCount;

            v2f vert(appdata v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }

            float hashF(float n){ n=frac(n*0.1031); n*=n+33.33; n*=n+n; return frac(n); }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float  t  = _Time2;
                float  intensity = _Intensity;
                float  refH = 560.0;
                float  result = 0.0;

                // 8条希望波浪线（复用 TitleBackground Hope 逻辑）
                float xPx = uv.x * refH * _Aspect;
                float yPx = uv.y * refH;
                for(int k = 0; k < 8; k++)
                {
                    float fk    = (float)k;
                    float freq  = 0.008 + fk * 0.003;
                    float amp   = 30.0  + fk * 12.0;
                    float speed = 0.4   + fk * 0.15;
                    float ph    = t * speed + fk * 1.2;
                    float yBase = refH * 0.5 + (fk - 3.5) * 22.0;
                    float noise = sin(xPx * freq + ph) * amp
                                + sin(xPx * freq * 2.3 + ph * 1.7) * amp * 0.4
                                + sin(xPx * freq * 0.5 + ph * 0.3) * amp * 0.6;
                    float dist  = abs(yPx - (yBase + noise)) / refH;
                    float lineA = (1.0 - fk * 0.08) * 0.6 * intensity;
                    result += smoothstep(0.003, 0.0, dist) * lineA;
                }

                // 漂浮小点
                int dotCount = (int)_DotCount;
                for(int j = 0; j < 60; j++)
                {
                    if(j >= dotCount) break;
                    float fj = (float)j;
                    float px = frac(sin(t * 0.3 + fj * 2.1) * 0.5 + 0.5);
                    float py = frac(cos(t * 0.2 + fj * 1.7) * 0.5 + 0.5);
                    result  += smoothstep(0.006, 0.0, length(uv - float2(px, py)))
                             * 0.18 * intensity;
                }

                result = saturate(result);
                return fixed4(_Color.rgb, result * _Color.a);
            }
            ENDCG
        }
    }
}
