// 회전베기 이펙트 (텍스처 불필요, 전부 수식으로 그림)
// Built-in 파이프라인 / URP(2D Renderer 포함) 양쪽에서 동작하는 Unlit 셰이더.
// SpinSlashVFX.cs 가 만드는 메시 전용입니다. 메시의 두 번째 UV(uv2.x)로 무엇을 그릴지 고릅니다.
//   uv2.x = 0 : 회전 참격   uv = 바닥 좌표 (단위원 = 베는 범위의 가장자리)
//               uv2 = (0, 칼끝이 돈 각도 rad, 꼬리가 돈 각도 rad, 시작 각도 rad)
//               uv3 = (반지름, 두께, 흰색 띠 비율, 굵기 배율),  uv4 = (글로우 세기, 꼬리 줄기 세기, 시드, 알파 배율)
//               알파 배율: 원의 먼 쪽 절반(캐릭터 뒤)과 가까운 쪽 절반(캐릭터 앞)이 만나는 띠에서 가까운 쪽을 서서히 드러내는 데 씀
//   uv2.x = 1 : 바람 먼지   uv2 = (1, 시드, 나이(초), 알파)
//   uv2.x = 2 : 반짝임      uv2 = (2, 진행도 0..1, -, -)
//   uv2.x = 3 : 불티 (가늘고 긴 마름모)   uv2 = (3, 알파, -, -)
//   uv2.x = 4 : 바닥 충격파 (퍼지는 고리)  uv = 바닥 좌표,  uv2 = (4, 진행도 0..1, 시드, -)
Shader "VFX/SpinSlash"
{
    Properties
    {
        [Header(Color)]
        [HDR] _CoreColor ("Core Color (outer blade)", Color) = (1, 1, 1, 1)
        [HDR] _MidColor ("Mid Color", Color) = (0.66, 0.93, 1, 1)
        [HDR] _EdgeColor ("Edge Color (inner / tail)", Color) = (0.13, 0.50, 1, 1)
        _Glow ("Glow", Range(0, 2)) = 0.6
        _Streak ("Streak Amount", Range(0, 1)) = 0.75
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
                float4 prm2 : TEXCOORD3;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
                float4 prm : TEXCOORD2;
                float4 prm2 : TEXCOORD3;
            };

            float4 _CoreColor, _MidColor, _EdgeColor;
            float _Glow, _Streak, _Alpha;

            static const float TAU = 6.283185307;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uv2 = v.uv2;
                o.prm = v.prm;
                o.prm2 = v.prm2;
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

            // ---------------------------------------------------------------- 회전 참격 (한 바퀴분)
            // s : 시작 각도에서부터 회전 방향으로 잰 각도 (rad). head/tail 사이가 지금 보이는 궤적
            float4 ringLayer(float s, float r, float aa, float head, float tail, float radius, float thickness,
                             float c1, float wk, float glowK, float lines, float seed)
            {
                float win = max(head - tail, 1e-3);
                float u = (s - tail) / win;
                float inWin = step(0.0, u) * step(u, 1.0);
                float uc = saturate(u);

                // 칼끝 쪽(70% 지점)이 가장 두껍고, 양끝은 뾰족
                float prof = pow(max(uc, 1e-5), 1.6) * pow(max(1.0 - uc, 1e-5), 0.7) / 0.2433;
                float w = thickness * prof * wk;
                float rout = radius * (1.0 - 0.06 * (1.0 - uc));       // 꼬리는 살짝 안쪽으로 말림
                float d = rout - r;                                    // 바깥 날에서 안쪽으로의 거리
                float v = d / max(w, 1e-4);

                // 회전 방향으로 길게 늘어난 노이즈 = 붓질 같은 결
                float nz = vnoise(float2(s * 2.2 + seed, r * 22.0));
                float nz2 = vnoise(float2(s * 5.0 + seed * 3.1, r * 50.0));
                nz = nz * 0.65 + nz2 * 0.35;
                float ero = _Streak * smoothstep(0.25, 1.0, v) * (0.30 + 0.70 * (1.0 - uc));

                float band = saturate(d / aa + 0.5) * saturate((w - d) / aa + 0.5);
                float keep = saturate((nz - ero) / 0.04 + 0.5);
                float alpha = band * keep * inWin;

                // 툰 밴드: 흰색 -> 하늘색 -> 파랑
                float vv = v + (nz - 0.5) * 0.18 + 0.25 * (1.0 - uc) * (1.0 - uc);
                float fv = max(aa / max(w, 1e-4), 1e-4);
                float k1 = saturate((vv - c1) / fv + 0.5);
                float k2 = saturate((vv - c1 - 0.20) / fv + 0.5);
                float k3 = saturate((vv - c1 - 0.40) / fv + 0.5);
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, k1);
                col = lerp(col, lerp(_MidColor.rgb, _EdgeColor.rgb, 0.55), k2);
                col = lerp(col, _EdgeColor.rgb, k3);

                // 꼬리 줄기: 칼날의 꼬리가 바깥 날을 따라 가는 선 하나로 길게 이어지다 사라짐 (따로 떨어진 선 조각 없이 칼날과 한 줄기)
                float lt = tail - 0.75 * win;
                float lu = (s - lt) / max(tail + 0.12 * win - lt, 1e-4);          // 0 = 줄기의 끝 .. 1 = 칼날 꼬리 안쪽
                float lw = 0.016 * pow(saturate(lu), 1.5) * step(0.0, lu) * step(lu, 1.0);
                float lineA = saturate((lw - abs(r - rout)) / aa + 0.5) * step(1e-4, lw)
                            * lines * saturate(wk * 1.5) * saturate(tail / 0.3);
                col = lerp(col, lerp(_MidColor.rgb, _CoreColor.rgb, 0.6), lineA * (1.0 - alpha));
                alpha = saturate(alpha + lineA * (1.0 - alpha));

                float gmask = smoothstep(0.0, 0.15, u) * (1.0 - smoothstep(0.9, 1.05, u));
                float g = exp(-abs(d - w * 0.35) * 9.0) * gmask * saturate(prof * 3.0) * _Glow * glowK;
                return float4(col * alpha + _EdgeColor.rgb * (g * 0.6), alpha);
            }

            // 한 바퀴를 넘게 도는 궤적: 같은 자리를 첫 바퀴째 / 두 바퀴째로 각각 그려서 겹침
            float4 ringFrag(float2 uv, float aa, float head, float tail, float startAngle, float dirSign,
                            float radius, float thickness, float c1, float wk, float glowK, float lines, float seed)
            {
                float r = length(uv);
                float ang = atan2(uv.y, uv.x + 1e-6) * dirSign;
                float s0 = frac((ang - startAngle) / TAU) * TAU;

                float4 a = ringLayer(s0, r, aa, head, tail, radius, thickness, c1, wk, glowK, lines, seed);
                float4 b = ringLayer(s0 + TAU, r, aa, head, tail, radius, thickness, c1, wk, glowK, lines, seed);
                float4 c = b + a * (1.0 - b.a);                       // 두 바퀴째가 위에
                return c * (1.0 - smoothstep(1.12, 1.22, r));          // 쿼드 가장자리에서 잘리지 않게
            }

            // ---------------------------------------------------------------- 바람 먼지
            float4 puffFrag(float2 uv, float seed, float age, float alpha)
            {
                float2 p = uv * 2.0 - 1.0;
                float r = length(p);
                float n = vnoise(p * 1.8 + float2(seed * 13.1, -age * 0.8 + seed * 5.3));
                float n2 = vnoise(p * 4.0 + float2(seed * 7.7, seed * 3.1));
                float d = r + (n - 0.5) * 0.55 + (n2 - 0.5) * 0.2;
                float a = (1.0 - smoothstep(0.35, 0.98, d)) * alpha * 0.9 * (1.0 - smoothstep(0.8, 1.0, r));   // 쿼드 가장자리에서 잘리지 않게

                float shade = saturate(0.55 + p.y * 0.5 + (n - 0.5) * 0.7);
                float3 shadow = lerp(_MidColor.rgb, _CoreColor.rgb, 0.25);
                float3 col = lerp(shadow, _CoreColor.rgb, smoothstep(0.10, 0.50, shade));
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 반짝임 (4갈래 별)
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
                float d = 1.0 - (p.x + p.y);                          // 마름모: 안쪽이 양수
                float a = saturate(d / aa + 0.5) * fade;
                float3 col = lerp(_MidColor.rgb, _CoreColor.rgb, saturate((d - 0.35) / aa + 0.5));
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 바닥 충격파
            float4 shockFrag(float2 uv, float t, float seed, float aa)
            {
                float r = length(uv);
                float th = atan2(uv.y, uv.x + 1e-6);
                float it = 1.0 - t;
                float rr = lerp(0.55, 1.18, 1.0 - it * it * it);       // 빠르게 퍼지다 감속
                float w = 0.10 * it * sqrt(it) + 0.004;                // 퍼질수록 가늘어짐
                float n = vnoise(float2(th * 7.0 + seed, r * 6.0));
                float d = abs(r - rr) - w * (0.6 + 0.8 * n);
                float a = saturate(-d / aa + 0.5) * (1.0 - smoothstep(0.6, 1.0, t));
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, saturate(abs(r - rr) / max(w, 1e-4) - 0.3));
                return float4(col * a, a);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 미분은 분기 밖에서 미리 계산
                float aaU = max(fwidth(i.uv.x), 1e-5);
                float aaV = max(fwidth(i.uv.y), 1e-5);
                float aaR = max(fwidth(length(i.uv)), 1e-5);

                float mode = i.uv2.x;
                float4 c;
                if (mode < 0.5)
                {
                    c = ringFrag(i.uv, aaR, i.uv2.y, i.uv2.z, i.uv2.w, 1.0,
                                 i.prm.x, i.prm.y, i.prm.z, i.prm.w, i.prm2.x, i.prm2.y, i.prm2.z);
                    c *= saturate(i.prm2.w);
                }
                else if (mode < 1.5)
                {
                    c = puffFrag(i.uv, i.uv2.y, i.uv2.z, i.uv2.w);
                }
                else if (mode < 2.5)
                {
                    c = sparkleFrag(i.uv, i.uv2.y, max(aaU, aaV) * 2.0);
                }
                else if (mode < 3.5)
                {
                    c = needleFrag(i.uv, i.uv2.y, max(aaU, aaV) * 2.0);
                }
                else
                {
                    c = shockFrag(i.uv, saturate(i.uv2.y), i.uv2.z, aaR);
                }

                return fixed4(c.rgb, c.a) * _Alpha;
            }
            ENDCG
        }
    }

    Fallback Off
}
