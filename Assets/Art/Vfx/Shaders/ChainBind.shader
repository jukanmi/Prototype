// 사슬 속박 이펙트 (텍스처 불필요, 전부 수식으로 그림)
// Built-in 파이프라인 / URP(2D Renderer 포함) 양쪽에서 동작하는 Unlit 셰이더.
// ChainBindVFX.cs 가 만드는 메시 전용입니다. 메시의 두 번째 UV(uv2.x)로 무엇을 그릴지 고릅니다.
//   uv2.x = 0 : 사슬 (구를 감은 고리 모양의 띠)
//               uv = (고리 단위 위치 - 여기에 흐름이 더해져 고리가 사슬을 따라 돎, 폭 방향 0..1)
//               uv2 = (0, 한 바퀴 중 위치 0..1, 깊이 +1 = 카메라 쪽 .. -1 = 뒤쪽, 섬광 0..1)
//               uv3 = (한 바퀴의 고리 수, 끊어짐 0..1, 그려진 정도 0..1, -)
//   uv2.x = 1 : 중심의 빛무리    uv2 = (1, 알파, -, -)
//   uv2.x = 2 : 섬광 (4갈래 별)  uv2 = (2, 진행도 0..1, -, -)
//   uv2.x = 3 : 조여드는 고리    uv2 = (3, 진행도 0..1, -, -)
//   uv2.x = 4 : 끊어진 고리 조각 uv2 = (4, 알파, -, -)
//   uv2.x = 5 : 사슬이 감고 있는 구 (옅은 테두리빛)  uv2 = (5, 알파, -, -)
Shader "VFX/ChainBind"
{
    Properties
    {
        [Header(Color)]
        [HDR] _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        [HDR] _MidColor ("Mid Color", Color) = (0.72, 0.45, 1, 1)
        [HDR] _EdgeColor ("Edge Color (glow)", Color) = (0.50, 0, 1, 1)
        _Glow ("Glow", Range(0, 2)) = 1
        _Alpha ("Alpha", Range(0, 1)) = 1
        [HideInInspector] _Aspect ("Strip Width / Link Size", Float) = 1.6
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
                float4 prm : TEXCOORD2;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
                float4 prm : TEXCOORD2;
            };

            float4 _CoreColor, _MidColor, _EdgeColor;
            float _Glow, _Alpha, _Aspect;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uv2 = v.uv2;
                o.prm = v.prm;
                return o;
            }

            float hash11(float x)
            {
                return frac(sin(x * 127.1 + 311.7) * 43758.5453);
            }

            // 모서리가 둥근 사각형까지의 거리 (b = 반크기, r = 모서리 반지름)
            float sdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            // 고리 2개가 한 주기: 정면으로 보이는 네모난 고리 + 옆으로 선 고리(막대).
            // off = 이 픽셀이 속한 고리의 중심에서 얼마나 떨어져 있는지 (고리를 통째로 숨기기 위해)
            float linkSdf(float x, float y, out float off)
            {
                float fx = frac(x * 0.5) * 2.0;

                float xr = fx - 0.5;                                  // 네모 고리 중심 = 0.5
                if (xr > 1.0) xr -= 2.0;
                float dRing = abs(sdRoundBox(float2(xr, y), float2(0.60, 0.33), 0.12)) - 0.095;

                float xb = fx - 1.5;                                  // 막대 중심 = 1.5
                if (xb < -1.0) xb += 2.0;
                float dBar = sdRoundBox(float2(xb, y), float2(0.50, 0.095), 0.05);

                off = dRing < dBar ? xr : xb;
                return min(dRing, dBar);
            }

            // ---------------------------------------------------------------- 사슬
            // 구를 한 바퀴 감은 띠. 카메라 쪽 절반은 밝고 크게, 뒤쪽 절반은 어둡게 그려 입체감을 냄
            float4 chainFrag(float2 uv, float a01, float depth, float flash, float links, float dissolve, float wipe,
                             float aaU, float aaV)
            {
                float y = (uv.y - 0.5) * _Aspect;
                float off;
                float d = linkSdf(uv.x, y, off);

                float aa = max(aaU, aaV * _Aspect);
                float stroke = saturate(-d / aa + 0.5);
                float nearK = saturate(depth * 0.5 + 0.5);              // 1 = 앞, 0 = 뒤
                float3 col = lerp(lerp(_EdgeColor.rgb, _MidColor.rgb, 0.6), _CoreColor.rgb,
                                  smoothstep(0.15, 0.75, saturate(-d / 0.095)) * (0.25 + 0.75 * nearK));
                col = lerp(col, _CoreColor.rgb, flash);

                // 풀릴 때는 고리가 하나씩 통째로 떨어져 나감 (번호를 한 바퀴 단위로 돌려서 이음매가 없음)
                float linkId = round(uv.x - off - 0.5);
                linkId -= links * floor(linkId / links);
                float keep = step(dissolve * 1.05 - 0.02, hash11(linkId));

                // 등장할 때는 앞쪽 가운데에서부터 양쪽으로 돌아가며 그려짐
                float vis = step(abs(frac(a01 + 0.25) - 0.5) * 2.0, wipe);

                // 구의 옆구리를 돌아갈 때는 고리가 화면에 거의 수직이 되어 가늘게 사라짐
                float limb = saturate((0.45 - aaU) / 0.25);
                float shade = lerp(0.50, 1.0, nearK);

                float k = keep * vis * limb * shade;
                float a = stroke * k;
                float g = exp(-max(d, 0.0) * 7.0) * _Glow * k * (1.0 + 1.5 * flash)
                        * saturate((_Aspect * 0.5 - abs(y)) / 0.25);
                float3 glowCol = lerp(_EdgeColor.rgb, _MidColor.rgb, 0.2 + 0.6 * flash);
                return float4(col * a + glowCol * (g * (1.0 - a)), a);
            }

            // ---------------------------------------------------------------- 사슬이 감고 있는 구
            float4 sphereFrag(float2 uv, float alpha)
            {
                float2 p = uv * 2.0 - 1.0;
                float r = length(p);
                float inside = saturate((1.0 - r) / 0.03);
                float rim = smoothstep(0.55, 1.0, r);
                rim *= rim;                                            // 가장자리로 갈수록 밝은 테두리빛
                float2 h = p - float2(-0.35, 0.40);
                float hl = exp(-dot(h, h) * 7.0);                      // 왼쪽 위의 은은한 하이라이트
                float a = (0.15 + 0.85 * rim + 0.5 * hl) * alpha * inside;
                float3 col = lerp(_EdgeColor.rgb, _MidColor.rgb, saturate(rim * 0.6 + hl));
                return float4(col * a, a * 0.25);                      // 거의 가산
            }

            // ---------------------------------------------------------------- 중심의 빛무리
            float4 glowFrag(float2 uv, float alpha)
            {
                float2 p = uv * 2.0 - 1.0;
                float r2 = dot(p, p);
                float a = exp(-r2 * 4.5) * alpha * saturate((1.0 - sqrt(r2)) / 0.3);
                float3 col = lerp(_EdgeColor.rgb, _CoreColor.rgb, exp(-r2 * 14.0));
                return float4(col * a, a * 0.15);                      // 거의 가산
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

            // ---------------------------------------------------------------- 조여드는 고리
            float4 ringFrag(float2 uv, float t, float aa)
            {
                float r = length(uv * 2.0 - 1.0);
                float rr = lerp(0.92, 0.18, t * t);                    // 점점 빠르게 대상 쪽으로 좁혀짐
                float w = 0.022 + 0.02 * t;
                float d = abs(r - rr) - w;
                float vis = smoothstep(0.0, 0.2, t) * (1.0 - smoothstep(0.75, 1.0, t));
                float a = saturate(-d / aa + 0.5) * vis;
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, saturate(abs(r - rr) / w - 0.2));
                float g = exp(-max(d, 0.0) * 10.0) * 0.5 * vis * saturate((1.0 - r) / 0.1);
                return float4(col * a + _EdgeColor.rgb * (g * (1.0 - a)), a);
            }

            // ---------------------------------------------------------------- 끊어진 고리 조각
            float4 pieceFrag(float2 uv, float fade, float aa)
            {
                float2 q = uv * 2.0 - 1.0;
                float2 p = q * float2(0.80, 0.50);
                float d = abs(sdRoundBox(p, float2(0.60, 0.33), 0.12)) - 0.095;
                float a = saturate(-d / aa + 0.5) * fade;
                float3 col = lerp(_MidColor.rgb, _CoreColor.rgb, smoothstep(0.15, 0.75, saturate(-d / 0.095)));
                float g = exp(-max(d, 0.0) * 9.0) * 0.6 * fade * saturate((1.0 - max(abs(q.x), abs(q.y))) / 0.15);
                return float4(col * a + _EdgeColor.rgb * (g * (1.0 - a)), a);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 미분은 분기 밖에서 미리 계산
                float aaU = max(fwidth(i.uv.x), 1e-5);
                float aaV = max(fwidth(i.uv.y), 1e-5);
                float aa2 = max(aaU, aaV) * 2.0;

                float mode = i.uv2.x;
                float4 c;
                if (mode < 0.5)
                {
                    c = chainFrag(i.uv, i.uv2.y, i.uv2.z, saturate(i.uv2.w), max(i.prm.x, 2.0), i.prm.y, i.prm.z, aaU, aaV);
                }
                else if (mode < 1.5)
                {
                    c = glowFrag(i.uv, i.uv2.y);
                }
                else if (mode < 2.5)
                {
                    c = sparkleFrag(i.uv, i.uv2.y, aa2);
                }
                else if (mode < 3.5)
                {
                    c = ringFrag(i.uv, saturate(i.uv2.y), aa2);
                }
                else if (mode < 4.5)
                {
                    c = pieceFrag(i.uv, i.uv2.y, aa2 * 0.8);
                }
                else
                {
                    c = sphereFrag(i.uv, i.uv2.y);
                }

                return fixed4(c.rgb, c.a) * _Alpha;
            }
            ENDCG
        }
    }

    Fallback Off
}
