// 스프라이트의 모양(알파)만 가져와 한 가지 색으로 칠하는 셰이더. 숄더차지의 잔상/오라용.
// ShoulderChargeVFX.cs 가 만드는 잔상 SpriteRenderer 전용이며, 색은 MaterialPropertyBlock 의 _GhostColor 로 넣습니다.
// Built-in 파이프라인 / URP(2D Renderer 포함) 양쪽에서 동작하는 Unlit 셰이더.
// (좌우 반전은 SpriteRenderer.flipX 대신 스케일로 처리하므로 _Flip 을 쓰지 않습니다.)
Shader "VFX/SpriteSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _GhostColor ("Color", Color) = (0.13, 0.50, 1, 0.6)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend One OneMinusSrcAlpha   // premultiplied
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _GhostColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float a = tex2D(_MainTex, i.uv).a * saturate(_GhostColor.a);
                return fixed4(_GhostColor.rgb * a, a);
            }
            ENDCG
        }
    }

    Fallback Off
}
