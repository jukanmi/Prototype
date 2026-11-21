// 타이틀 화면 횃불. UI Image에 씌우는 절차적 불꽃 — 텍스처 없이 노이즈로 일렁인다.
// 가산 블렌드라 어두운 벽 위에서 빛처럼 보인다. 정점 색의 알파로 전체 세기를 조절한다(TorchFlicker가 흔든다).
Shader "Prototype/UI/TorchFlame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CoreColor ("Core", Color) = (1, 0.95, 0.75, 1)
        _MidColor ("Mid", Color) = (1, 0.55, 0.12, 1)
        _EdgeColor ("Edge", Color) = (0.75, 0.12, 0.02, 1)
        _Speed ("Rise Speed", Float) = 1.6
        _Turbulence ("Turbulence", Range(0, 1)) = 0.35
        _Seed ("Seed", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            fixed4 _Color, _CoreColor, _MidColor, _EdgeColor;
            float _Speed, _Turbulence, _Seed;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float hash (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            float fbm (float2 p)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += a * noise(p); p *= 2.03; a *= 0.5; }
                return v;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed + _Seed;
                float2 uv = i.uv;

                // 위로 갈수록 크게 휘청인다
                float sway = (fbm(float2(uv.y * 2.0 - t * 0.6, t * 0.3)) - 0.5) * _Turbulence * uv.y * 1.4;
                float x = (uv.x - 0.5) + sway;

                // 눈물방울 실루엣: 아래는 넓고 위로 좁아진다
                float width = lerp(0.42, 0.04, pow(saturate(uv.y), 0.85));
                float body = saturate(1.0 - abs(x) / width);

                // 위로 흘러가는 노이즈가 가장자리를 갉아먹어 혀 모양 불길을 만든다
                float n = fbm(float2(uv.x * 3.2, uv.y * 2.6 - t * 1.8));
                float flame = body * (0.55 + 0.9 * n) - uv.y * 0.55;
                flame *= smoothstep(0.0, 0.12, uv.y);          // 바닥은 횃대 속으로 사그라든다
                flame = saturate(flame * 2.2);

                float3 col = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.05, 0.45, flame));
                col = lerp(col, _CoreColor.rgb, smoothstep(0.55, 0.95, flame));

                float alpha = smoothstep(0.02, 0.25, flame) * i.color.a;
                return fixed4(col * i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
