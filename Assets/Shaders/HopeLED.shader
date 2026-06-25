Shader "XiWang_XiWang/HopeLED"
{
    Properties
    {
        _MainTex     ("Source",          2D)           = "white" {}
        _LedSize     ("LED Size (px)",   Float)        = 6.0
        _GapRatio    ("Gap Ratio",       Range(0,0.6)) = 0.25
        _Brightness  ("Brightness",      Range(0.5,2)) = 1.2
        _ColorShift  ("Color Shift",     Range(0,1))   = 0.35
        _Intensity   ("Intensity 0~1",   Range(0,1))   = 1.0
        _ScreenW     ("Screen Width",    Float)        = 1920
        _ScreenH     ("Screen Height",   Float)        = 1080
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            float     _LedSize, _GapRatio, _Brightness, _ColorShift, _Intensity;
            float     _ScreenW, _ScreenH;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // ── LED 格子计算 ──────────────────────────────
                // 用实际屏幕像素尺寸保证 LED 物理大小一致
                float2 pixelPos = float2(uv.x * _ScreenW, uv.y * _ScreenH);

                // 每格大小（像素）
                float cellSize  = max(_LedSize, 1.0);
                // 格子索引
                float2 cellIdx  = floor(pixelPos / cellSize);
                // 格子中心像素坐标
                float2 cellCenter = (cellIdx + 0.5) * cellSize;
                // 格子中心 UV
                float2 cellUV   = cellCenter / float2(_ScreenW, _ScreenH);
                // 当前像素在格子内的局部坐标（-0.5 ~ 0.5）
                float2 localPos = (pixelPos - cellCenter) / cellSize;

                // ── 采样格子中心颜色 ──────────────────────────
                fixed4 src = tex2D(_MainTex, cellUV);

                // ── 圆点 mask ─────────────────────────────────
                // 圆点半径 = (0.5 - gapRatio/2)，间隙由 gapRatio 控制
                float dotRadius = 0.5 - _GapRatio * 0.5;
                float dist      = length(localPos);
                // 抗锯齿边缘（1像素软边）
                float edgeSoft  = 1.0 / cellSize;
                float circle    = smoothstep(dotRadius + edgeSoft, dotRadius - edgeSoft, dist);

                // ── LED 发光：中心亮，边缘衰减 ────────────────
                float glow      = pow(max(0.0, 1.0 - dist / dotRadius), 1.5);
                float3 ledColor = src.rgb * _Brightness * (0.85 + glow * 0.3);

                // ── 色调偏移：偏向青蓝紫（对应图中效果）────────
                // 提升蓝色通道，轻压红色，模拟 LED 屏的冷色偏移
                float3 shifted;
                shifted.r = lerp(ledColor.r, ledColor.r * 0.75 + ledColor.b * 0.15, _ColorShift);
                shifted.g = lerp(ledColor.g, ledColor.g * 0.90 + ledColor.b * 0.10, _ColorShift);
                shifted.b = lerp(ledColor.b, ledColor.b * 1.20,                      _ColorShift);
                shifted   = saturate(shifted);

                // ── 最终合成：圆点内显示 LED 色，圆点外纯黑 ──
                float3 finalLED = shifted * circle;

                // ── 与原画面插值（_Intensity 控制过渡）─────────
                float3 result = lerp(src.rgb, finalLED, _Intensity);

                return fixed4(result, 1.0);
            }
            ENDCG
        }
    }
}
