// 베기 이펙트용 불꽃 파티클 셰이더. 텍스처 없이 마름모를 그리며,
// Stretched Billboard 로 늘어나면 날카로운 바늘 모양이 됩니다.
Shader "VFX/SlashSpark"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float4 _Tint;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = abs(i.uv * 2.0 - 1.0);
                float dmd = 1.0 - (p.x + p.y);               // 마름모: 안쪽이 양수
                float aa = max(fwidth(dmd), 1e-5);
                float a = saturate(dmd / aa + 0.5);
                float core = saturate((dmd - 0.45) / aa + 0.5); // 중심은 흰색

                float3 col = lerp(i.color.rgb * _Tint.rgb, float3(1, 1, 1), core);
                a *= i.color.a * _Tint.a;
                return fixed4(col * a, a);
            }
            ENDCG
        }
    }

    Fallback Off
}
