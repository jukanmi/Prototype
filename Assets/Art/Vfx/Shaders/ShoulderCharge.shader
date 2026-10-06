// 숄더차지 이펙트 (텍스처 불필요, 전부 수식으로 그림)
// Built-in 파이프라인 / URP(2D Renderer 포함) 양쪽에서 동작하는 Unlit 셰이더.
// ShoulderChargeVFX.cs 가 만드는 메시 전용입니다. 메시의 두 번째 UV(uv2.x)로 무엇을 그릴지 고릅니다.
//   uv2.x = 0 : 어깨 앞의 충격파 (뒤쪽으로 열린 초승달)   uv = (0 = 뒤 .. 1 = 앞, 0 = 아래 .. 1 = 위)
//               uv2 = (0, 시간(초), 알파, 시드),  uv3 = (열기 0..1, -, -, -)  ← 열기가 식으면 흰색 대신 파랗게
//   uv2.x = 1 : 바닥에서 튀는 기운의 날 (뾰족한 가시)   uv = (0 = 뿌리 .. 1 = 끝, 폭 방향 0..1)
//               uv2 = (1, 굵기 0..1, -, -)   ← 흐려지는 대신 가늘어지며 사라짐
//   uv2.x = 2 : 먼지   uv2 = (2, 시드, 나이(초), 알파)
//   uv2.x = 3 : 섬광 (4갈래 별)   uv2 = (3, 진행도 0..1, -, -)
//   uv2.x = 4 : 속도선 / 튀는 줄기 (가늘고 긴 마름모)   uv2 = (4, 알파, -, -)
//   uv2.x = 5 : 바닥 충격파 (퍼지는 고리)   uv = 바닥 좌표 (단위원 = 최대 반경),  uv2 = (5, 진행도 0..1, 시드, -)
//   uv2.x = 6 : 빛무리   uv2 = (6, 알파, -, -)
//   uv2.x = 7 : 바닥에 남는 돌진 자국   uv = (0 = 출발점 .. 1 = 발밑, 폭 방향 0..1)
//               uv2 = (7, 알파, 길이(월드), 시드)
Shader "VFX/ShoulderCharge"
{
    Properties
    {
        [Header(Color)]
        [HDR] _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        [HDR] _MidColor ("Mid Color", Color) = (0.66, 0.93, 1, 1)
        [HDR] _EdgeColor ("Edge Color (glow)", Color) = (0.13, 0.50, 1, 1)
        _SmokeColor ("Dust Color", Color) = (0.74, 0.80, 0.90, 1)
        _Glow ("Glow", Range(0, 2)) = 1
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
                float4 prm : TEXCOORD2;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
                float4 prm : TEXCOORD2;
            };

            float4 _CoreColor, _MidColor, _EdgeColor, _SmokeColor;
            float _Glow, _Alpha;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uv2 = v.uv2;
                o.prm = v.prm;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // ---------------------------------------------------------------- 어깨 앞의 충격파
            // 앞쪽(+x)으로 볼록한 초승달. 안쪽 가장자리가 바람에 뜯기듯 일렁이고, 그 뒤로 바람 줄기와 옅은 막이 끌려감
            float4 bowFrag(float2 uv, float time, float alpha, float seed, float heat, float aa)
            {
                float2 p = uv * 2.0 - 1.0;
                float2 q = p - float2(-0.05, 0.0);
                float r = length(q);
                float th = atan2(q.y, q.x + 1e-6);
                float tn = th / 1.25;
                float taper = saturate(1.0 - tn * tn);                  // 위아래 끝으로 갈수록 가늘어짐
                float side = step(0.0, th) * 5.3;                       // 위/아래가 거울상이 되지 않게

                // 가운데에서 위아래 끝으로 흘러가는 결
                float n1 = vnoise(float2(abs(th) * 7.0 - time * 9.0 + seed, seed * 3.1 + side));
                float n2 = vnoise(float2(abs(th) * 19.0 - time * 14.0 + seed * 2.0, seed + side));
                float outerR = 1.0 + 0.02 * (n2 - 0.5);
                float thick = 0.38 * pow(taper, 0.7) * (0.45 + 0.8 * n1 + 0.3 * n2);
                float dIn = outerR - r;                               // 0 = 앞 가장자리, 뒤로 갈수록 커짐
                float k = dIn / max(thick, 1e-4);
                float body = saturate(dIn / aa + 0.5) * saturate((thick - dIn) / aa + 0.5) * step(1e-4, taper);

                float aaK = aa / max(thick, 1e-3);
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, saturate((k - 0.50) / aaK + 0.5));
                col = lerp(col, _EdgeColor.rgb, saturate((k - 0.82) / aaK + 0.5));
                col = lerp(col, lerp(_MidColor.rgb, _EdgeColor.rgb, 0.55), 1.0 - heat);   // 놓아 보낼 때 회색이 아니라 파랗게 식음

                float back = dIn - thick;                             // 초승달 뒤쪽으로의 거리
                float behind = step(0.0, back);
                float border = saturate((1.0 - max(abs(p.x), abs(p.y))) / 0.08);

                // 안쪽 가장자리에서 뒤로 끌려가는 바람 줄기
                float ln = vnoise(float2(p.y * 9.0 + seed * 2.0, p.x * 1.3 + time * 8.0));
                float wind = smoothstep(0.60, 0.72, ln) * saturate(back / 0.04) * exp(-max(back, 0.0) * 3.2) * taper * 0.6 * border;

                // 초승달 뒤에 눌린 공기: 뒤로 갈수록 옅어지는 막
                float veil = exp(-max(back, 0.0) * 3.5) * taper * 0.42 * border * behind;

                // 앞 가장자리 바깥의 빛
                float g = exp(-max(-dIn, 0.0) * 10.0) * sqrt(taper) * 0.7 * _Glow * border * step(dIn, 0.0);

                float rest = 1.0 - body;
                float3 rgb = col * body
                           + (_MidColor.rgb * wind + lerp(_EdgeColor.rgb, _MidColor.rgb, 0.25) * veil + _EdgeColor.rgb * g) * rest;
                float a = body + (wind * 0.6 + veil * 0.55) * rest;
                return float4(rgb, a) * alpha;
            }

            // ---------------------------------------------------------------- 기운의 날 (가시)
            float4 spikeFrag(float2 uv, float thin, float aa)
            {
                float u = uv.x;
                float y = abs(uv.y * 2.0 - 1.0);
                float w = ((u < 0.18) ? u / 0.18 : max(1.0 - u, 0.0) / 0.82) * thin;
                float d = w - y;
                float a = saturate(d / aa + 0.5) * saturate(w / (aa * 1.5) - 0.3);     // 1픽셀보다 가늘어지면 깨끗이 사라짐
                float3 col = lerp(_EdgeColor.rgb, _MidColor.rgb, saturate((d - 0.16) / aa + 0.5));
                col = lerp(col, _CoreColor.rgb, saturate((w * 0.45 - y) / aa + 0.5));
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 먼지
            float4 dustFrag(float2 uv, float seed, float age, float alpha)
            {
                float2 p = uv * 2.0 - 1.0;
                float r = length(p);
                float n = vnoise(p * 1.8 + float2(seed * 13.1, -age * 0.8 + seed * 5.3));
                float n2 = vnoise(p * 4.0 + float2(seed * 7.7, seed * 3.1));
                float d = r + (n - 0.5) * 0.55 + (n2 - 0.5) * 0.2;
                float a = (1.0 - smoothstep(0.35, 0.98, d)) * alpha * 0.9 * (1.0 - smoothstep(0.8, 1.0, r));   // 쿼드 가장자리에서 잘리지 않게

                float shade = saturate(0.55 + p.y * 0.5 + (n - 0.5) * 0.7);
                float3 shadow = lerp(_SmokeColor.rgb * 0.5, _EdgeColor.rgb, 0.25);
                float3 col = lerp(shadow, _SmokeColor.rgb, smoothstep(0.10, 0.50, shade));
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

            // ---------------------------------------------------------------- 속도선 / 줄기
            float4 needleFrag(float2 uv, float fade, float aa)
            {
                float2 p = abs(uv * 2.0 - 1.0);
                float d = 1.0 - (p.x + p.y);
                float a = saturate(d / aa + 0.5) * fade;
                float3 col = lerp(_MidColor.rgb, _CoreColor.rgb, saturate((d - 0.35) / aa + 0.5));
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 바닥 충격파
            float4 shockFrag(float2 uv, float t, float seed, float aa)
            {
                float r = length(uv);
                float2 dir = uv / max(r, 1e-4);
                float it = 1.0 - t;
                float rr = lerp(0.20, 0.95, 1.0 - it * it * it);       // 빠르게 퍼지다 감속
                float w = 0.10 * it * sqrt(it) + 0.004;                // 퍼질수록 가늘어짐
                float n = vnoise(float2(dir.x * 4.0 + seed, dir.y * 4.0 + r * 6.0));
                float d = abs(r - rr) - w * (0.6 + 0.8 * n);
                float a = saturate(-d / aa + 0.5) * (1.0 - smoothstep(0.6, 1.0, t));
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, saturate(abs(r - rr) / max(w, 1e-4) - 0.3));
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 빛무리
            float4 glowFrag(float2 uv, float alpha)
            {
                float2 p = uv * 2.0 - 1.0;
                float r2 = dot(p, p);
                float a = exp(-r2 * 4.5) * alpha * saturate((1.0 - sqrt(r2)) / 0.3);
                float3 col = lerp(_EdgeColor.rgb, _CoreColor.rgb, exp(-r2 * 14.0));
                return float4(col * a, a * 0.15);                       // 거의 가산
            }

            // ---------------------------------------------------------------- 바닥에 남는 돌진 자국
            float4 skidFrag(float2 uv, float alpha, float len, float seed)
            {
                float u = saturate(uv.x);
                float y = uv.y * 2.0 - 1.0;
                float streaks = smoothstep(0.40, 0.72, vnoise(float2(y * 7.0 + seed, u * len * 0.45 + seed * 1.7)));
                float env = saturate(1.0 - y * y);
                float along = smoothstep(0.0, 0.5, u) * (1.0 - smoothstep(0.96, 1.0, u));      // 출발점 쪽은 옅게
                float head = u * u * u * u;                                                     // 발밑으로 갈수록 밝게
                float a = saturate(env * along * (0.22 + 0.55 * streaks + 0.5 * head * exp(-6.25 * y * y)) * alpha);
                float3 col = lerp(_EdgeColor.rgb, _MidColor.rgb, saturate(streaks * 0.8 + head));
                col = lerp(col, _CoreColor.rgb, saturate(head * exp(-16.0 * y * y)));
                return float4(col * a, a * 0.6);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 미분은 분기 밖에서 미리 계산
                float aaU = max(fwidth(i.uv.x), 1e-5);
                float aaV = max(fwidth(i.uv.y), 1e-5);
                float aa2 = max(aaU, aaV) * 2.0;
                float aaR = max(fwidth(length(i.uv)), 1e-5);

                float mode = i.uv2.x;
                float4 c;
                if (mode < 0.5)
                {
                    c = bowFrag(i.uv, i.uv2.y, saturate(i.uv2.z), i.uv2.w, saturate(i.prm.x), aa2);
                }
                else if (mode < 1.5)
                {
                    c = spikeFrag(i.uv, saturate(i.uv2.y), aaV * 2.0);
                }
                else if (mode < 2.5)
                {
                    c = dustFrag(i.uv, i.uv2.y, i.uv2.z, saturate(i.uv2.w));
                }
                else if (mode < 3.5)
                {
                    c = sparkleFrag(i.uv, i.uv2.y, aa2);
                }
                else if (mode < 4.5)
                {
                    c = needleFrag(i.uv, saturate(i.uv2.y), aa2);
                }
                else if (mode < 5.5)
                {
                    c = shockFrag(i.uv, saturate(i.uv2.y), i.uv2.z, aaR);
                }
                else if (mode < 6.5)
                {
                    c = glowFrag(i.uv, i.uv2.y);
                }
                else
                {
                    c = skidFrag(i.uv, saturate(i.uv2.y), i.uv2.z, i.uv2.w);
                }

                return fixed4(c.rgb, c.a) * _Alpha;
            }
            ENDCG
        }
    }

    Fallback Off
}
