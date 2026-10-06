// 마력화살 이펙트 (텍스처 불필요, 전부 수식으로 그림)
// Built-in 파이프라인 / URP(2D Renderer 포함) 양쪽에서 동작하는 Unlit 셰이더.
// MagicMissileVFX.cs 가 만드는 메시 전용입니다. 메시의 두 번째 UV(uv2.x)로 무엇을 그릴지 고릅니다.
//   uv2.x = 0 : 마력탄 / 잔상 (가시 돋친 빛 구슬)   uv2 = (0, 시간(초), 시드, 알파)   알파 < 1 이면 잔상
//   uv2.x = 1 : 꼬리 (부드러운 띠)                  uv = (0 = 마력탄 쪽 .. 1 = 꼬리 끝, 폭 방향 0..1),  uv2 = (1, 알파, -, -)
//   uv2.x = 2 : 섬광 (4갈래 별)                     uv2 = (2, 진행도 0..1, -, -)
//   uv2.x = 3 : 불티 (가늘고 긴 마름모)             uv2 = (3, 알파, -, -)
//   uv2.x = 4 : 탄착 고리 (퍼지는 원)               uv2 = (4, 진행도 0..1, -, -)
Shader "VFX/MagicMissile"
{
    Properties
    {
        [Header(Color)]
        [HDR] _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        [HDR] _MidColor ("Mid Color", Color) = (0.72, 0.45, 1, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.50, 0, 1, 1)
        _Glow ("Glow", Range(0, 2)) = 0.9
        _Alpha ("Alpha", Range(0, 1)) = 1
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

        Blend One OneMinusSrcAlpha   // premultiplied: 불투명한 본체 + 가산 글로우를 한 패스로
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
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
            };

            float4 _CoreColor, _MidColor, _EdgeColor;
            float _Glow, _Alpha;

            static const float TAU = 6.283185307;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uv2 = v.uv2;
                return o;
            }

            // 뾰족한 삼각파 (주기 1)
            float tri(float x)
            {
                return abs(frac(x) - 0.5) * 2.0;
            }

            // ---------------------------------------------------------------- 마력탄
            // 테두리가 들쭉날쭉한 빛 구슬. 가시는 시간에 따라 천천히 돌고, 잔상은 흐려지는 대신 파랗게 식음
            float4 orbFrag(float2 uv, float t, float seed, float alpha, float aa)
            {
                float2 p = uv * 2.0 - 1.0;
                float r = length(p);
                float a = atan2(p.y, p.x + 1e-6) / TAU;

                float aw = a + 0.035 * sin(a * TAU * 3.0 + t * 2.0 + seed);             // 가시 간격을 고르지 않게
                float s1 = tri(aw * 5.0 + t * 0.7 + seed);
                float e = 0.50 + 0.15 * s1 * s1 * (0.55 + 0.45 * tri(a * 2.0 + seed * 0.3 + t * 0.2))
                        + 0.05 * tri(aw * 13.0 - t * 1.3 + seed * 1.7);                  // 테두리 반지름

                float body = saturate((e - r) / aa + 0.5);
                float k = r / max(e, 1e-4);
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, smoothstep(0.45, 0.80, k));
                col = lerp(col, lerp(_MidColor.rgb, _EdgeColor.rgb, 0.6), smoothstep(0.80, 1.0, k));
                col = lerp(col, lerp(_EdgeColor.rgb, _MidColor.rgb, 0.35), (1.0 - alpha) * 0.85);

                float g = exp(-max(r - e * 0.6, 0.0) * 5.0) * _Glow * saturate((1.0 - r) / 0.25);
                float3 glowCol = lerp(_EdgeColor.rgb, _MidColor.rgb, 0.25);
                float3 rgb = (col * body + glowCol * (g * (1.0 - body))) * alpha;
                return float4(rgb, body * alpha * alpha);              // 잔상은 가리는 것보다 더하는 쪽이 커서 빛나 보임
            }

            // ---------------------------------------------------------------- 꼬리
            float4 ribbonFrag(float2 uv, float alpha)
            {
                float w = 1.0 - abs(uv.y * 2.0 - 1.0);                 // 가운데 1, 가장자리 0
                float tail = 1.0 - saturate(uv.x);
                float a = tail * sqrt(tail) * smoothstep(0.0, 0.9, w) * alpha * 0.75;
                float3 col = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.3, 1.0, w) * tail);
                col = lerp(col, _CoreColor.rgb, smoothstep(0.75, 1.0, w) * tail * tail);
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 섬광 (4갈래 별)
            float4 sparkleFrag(float2 uv, float t, float aa)
            {
                float2 p = abs(uv * 2.0 - 1.0);
                float st = sqrt(p.x) + sqrt(p.y);
                float c = 0.95 * sin(3.14159265 * saturate(t));
                float a = saturate((c - st) / (aa * 3.0) + 0.5) * saturate(c / (aa * 6.0));
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, smoothstep(0.45, 0.9, st / max(c, 1e-4)));
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 불티
            float4 needleFrag(float2 uv, float fade, float aa)
            {
                float2 p = abs(uv * 2.0 - 1.0);
                float d = 1.0 - (p.x + p.y);
                float a = saturate(d / aa + 0.5) * fade;
                float3 col = lerp(_MidColor.rgb, _CoreColor.rgb, saturate((d - 0.35) / aa + 0.5));
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 탄착 고리
            float4 ringFrag(float2 uv, float t, float aa)
            {
                float r = length(uv * 2.0 - 1.0);
                float it = 1.0 - t;
                float rr = lerp(0.2, 0.88, 1.0 - it * it * it);        // 빠르게 퍼지다 감속
                float w = 0.05 * it * sqrt(it) + 0.004;
                float d = abs(r - rr) - w;
                float a = saturate(-d / aa + 0.5) * (1.0 - smoothstep(0.6, 1.0, t));
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, saturate(abs(r - rr) / w - 0.3));
                return float4(col * a, a);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 미분은 분기 밖에서 미리 계산
                float aa = max(max(fwidth(i.uv.x), fwidth(i.uv.y)), 1e-5) * 2.0;

                float mode = i.uv2.x;
                float4 c;
                if (mode < 0.5)
                {
                    c = orbFrag(i.uv, i.uv2.y, i.uv2.z, saturate(i.uv2.w), aa);
                }
                else if (mode < 1.5)
                {
                    c = ribbonFrag(i.uv, i.uv2.y);
                }
                else if (mode < 2.5)
                {
                    c = sparkleFrag(i.uv, i.uv2.y, aa);
                }
                else if (mode < 3.5)
                {
                    c = needleFrag(i.uv, i.uv2.y, aa);
                }
                else
                {
                    c = ringFrag(i.uv, saturate(i.uv2.y), aa);
                }

                return fixed4(c.rgb, c.a) * _Alpha;
            }
            ENDCG
        }
    }

    Fallback Off
}
