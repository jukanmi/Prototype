// 마나 스피어 이펙트 (텍스처 불필요, 전부 수식으로 그림)
// Built-in 파이프라인 / URP(2D Renderer 포함) 양쪽에서 동작하는 Unlit 셰이더.
// ManaSpearVFX.cs 가 만드는 메시 전용입니다. 메시의 두 번째 UV(uv2.x)로 무엇을 그릴지 고릅니다.
//   uv2.x = 0 : 마력창 (끝이 창날 모양인 긴 빛줄기)
//               uv = (길이 방향 0 = 시전자 .. 1 = 끝, 폭 방향 0..1)
//               uv2 = (0, 사라지는 정도 0..1, 굵기 배율, 섬광 0..1),  uv3 = (길이 / 폭, 시드, 시간(초), 창날이 차지하는 비율)
//   uv2.x = 1 : 후폭풍의 반구 (맨 앞 꼭짓점이 발사 지점인 입체적인 돔. 앞뒤로 눌린 쿼드에 그려 위아래가 긴 완만한 곡면이 됨. 크기는 그대로 두고 서서히 사라짐)
//               쿼드의 +x = 발사 방향, +y = 위
//               uv2 = (1, 진행도 0..1, 반각 rad, 시드),  uv3 = (바닥 높이(쿼드 좌표), 바닥 기울기, -, -)
//   uv2.x = 2 : 섬광 (4갈래 별)   uv2 = (2, 진행도 0..1, -, -)
//   uv2.x = 3 : 불티 (가늘고 긴 마름모)   uv2 = (3, 알파, -, -)
//   uv2.x = 4 : 발사 지점의 빛무리   uv2 = (4, 알파, -, -)
//   uv2.x = 5 : 바닥의 반쪽 고리 (반구가 바닥에 닿는 자리. 납작한 쿼드에 그려 바닥에 누운 것처럼 보임)
//               uv2 = (5, 진행도 0..1, 반각 rad, 시드)
//   uv2.x = 6 : 바닥을 쓸고 지나가는 바람 (꼭짓점이 시전자 앞, 뒤로 벌어지는 세모꼴)
//               uv = 바닥 좌표 (꼭짓점에서 뒤쪽으로의 거리 / 길이, 옆으로의 거리 / 길이)
//               uv2 = (6, 진행도 0..1, 벌어진 반각 rad, 시드)
//   uv2.x = 7 : 연기 한 덩이   uv2 = (7, 시드, 나이(초), 알파)
Shader "VFX/ManaSpear"
{
    Properties
    {
        [Header(Color)]
        [HDR] _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        [HDR] _MidColor ("Mid Color", Color) = (0.72, 0.45, 1, 1)
        [HDR] _EdgeColor ("Edge Color (glow)", Color) = (0.50, 0, 1, 1)
        _Glow ("Glow", Range(0, 2)) = 1
        _Lines ("Side Lines", Range(0, 1)) = 1
        _SmokeColor ("Smoke Color", Color) = (0.74, 0.70, 0.86, 1)
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
            float _Glow, _Lines, _Alpha;

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

            // ---------------------------------------------------------------- 마력창
            // 처음부터 전체 길이가 한 번에 나타나고, 가늘어지면서 결대로 찢어지듯 사라짐
            float4 beamFrag(float2 uv, float fadeT, float wk, float flash, float aspect, float seed, float time, float headFrac, float aa)
            {
                float u = uv.x;
                float v = uv.y * 2.0 - 1.0;
                float av = abs(v);
                float sv = v < 0.0 ? -1.0 : 1.0;

                // 길이 방향의 폭: 자루는 일정하고, 끝에서 한 번 넓어졌다가 뾰족하게 모이는 창날
                float hs = 1.0 - headFrac;                              // 창날이 시작되는 곳
                float sh = 1.0 - headFrac * 0.72;                       // 창날의 가장 넓은 곳
                float prof;
                if (u < hs) prof = 0.58 * smoothstep(0.0, 0.04, u) * (0.85 + 0.15 * saturate(u / hs));
                else if (u < sh) prof = lerp(0.58, 1.0, (u - hs) / (sh - hs));
                else prof = saturate((1.0 - u) / (1.0 - sh));

                float ud = u * aspect;                                  // 폭 단위로 잰 길이 방향 거리
                float ne = vnoise(float2(ud * 0.8 - time * 10.0 + seed, sv * 3.7 + seed));       // 가장자리의 일렁임
                float n = vnoise(float2(ud * 0.6 - time * 9.0 + seed, v * 4.0 + seed));
                float n2 = vnoise(float2(ud * 1.7 - time * 14.0 + seed * 2.3, v * 9.0));
                n = n * 0.65 + n2 * 0.35;

                float w = 0.62 * prof * wk * (0.86 + 0.28 * ne);
                float a = saturate((w - av) / aa + 0.5) * saturate(w / aa);
                float k = av / max(w, 1e-4);                            // 0 = 중심, 1 = 가장자리
                float ero = fadeT * 1.15 * smoothstep(0.10, 1.0, k);
                a *= saturate((n - ero) / 0.05 + 0.5);

                float c1 = lerp(0.46, 1.3, flash);                      // 섬광일 때는 전부 흰색
                float fk = max(aa / max(w, 1e-4), 1e-4);
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, saturate((k - c1) / fk + 0.5));
                col = lerp(col, _EdgeColor.rgb, saturate((k - c1 - 0.28) / fk + 0.5));

                // 자루 양옆을 따라 깜빡이는 가는 선
                float w0 = 0.62 * prof * wk;
                float seg1 = step(0.45, vnoise(float2(ud * 0.30 - time * 5.0 + 13.1 + seed, sv * 2.3)));
                float seg2 = step(0.45, vnoise(float2(ud * 0.30 - time * 5.0 + 26.2 + seed, sv * 4.6)));
                float la = max(saturate((0.014 - abs(av - (w0 + 0.13))) / aa + 0.5) * seg1,
                               saturate((0.014 - abs(av - (w0 + 0.25))) / aa + 0.5) * seg2);
                la *= _Lines * (1.0 - fadeT) * step(u, hs) * smoothstep(0.0, 0.06, u);
                col = lerp(col, lerp(_MidColor.rgb, _CoreColor.rgb, 0.5), la * (1.0 - a));
                a = saturate(a + la * (1.0 - a));

                float g = exp(-max(av - w, 0.0) * 5.0) * saturate(prof * 3.0) * _Glow
                        * (1.0 - smoothstep(0.3, 1.0, fadeT)) * (0.7 + 0.8 * flash) * saturate((1.0 - av) / 0.25);
                return float4(col * a + _EdgeColor.rgb * (g * (1.0 - a)), a);
            }

            // ---------------------------------------------------------------- 바닥의 반쪽 고리
            // 발사 순간 나타나 그 크기 그대로 서서히 사라지는 반쪽 고리. 정면이 가장 두껍고 양끝은 뾰족함
            float4 ringFrag(float2 uv, float t, float halfAngle, float seed, float aa)
            {
                float2 p = uv * 2.0 - 1.0;
                float r = length(p);
                float ang = atan2(p.y, p.x + 1e-6);

                float x = saturate(abs(ang) / halfAngle);
                float taper = pow(max(1.0 - x * x, 0.0), 0.8) * step(abs(ang), halfAngle);
                float it = 1.0 - t;
                float rr = 0.92;                                        // 첫 프레임부터 이 크기. 줄어들지 않고 옅어지기만 함
                float w = 0.24 * (1.0 - 0.45 * t) * taper;
                float d = rr - r;                                       // 바깥 가장자리에서 안쪽으로의 거리
                float vv = d / max(w, 1e-4);

                float n = vnoise(float2(ang * 5.0 + seed, r * 14.0 - t * 6.0));
                float ero = smoothstep(0.3, 1.0, vv) * (0.4 + 0.6 * t);
                float band = saturate(d / aa + 0.5) * saturate((w - d) / aa + 0.5) * saturate(w / aa);
                float a = band * saturate((n - ero) / 0.05 + 0.5) * (1.0 - smoothstep(0.10, 1.0, t));

                // 섬광(흰색) -> 색 띠 -> 사라지면서 보라색으로
                float c1 = lerp(1.2, 0.40, smoothstep(0.0, 0.3, t)) * (1.0 - 0.9 * smoothstep(0.3, 0.8, t));
                float fv = max(aa / max(w, 1e-4), 1e-4);
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, saturate((vv - c1) / fv + 0.5));
                col = lerp(col, _EdgeColor.rgb, saturate((vv - c1 - 0.30) / fv + 0.5));

                float g = exp(-abs(d - w * 0.3) * 8.0) * taper * _Glow * 0.6 * it * it * saturate((1.0 - r) / 0.08);
                return float4(col * a + _EdgeColor.rgb * (g * (1.0 - a)), a);
            }

            // 반구의 곡면을 따라 도는 큰 원 하나. 옆에서 보면 세로로 눌린 타원이 됨 (s = 눌린 비율)
            float domeArc(float2 p, float rr, float s, float aa)
            {
                float e = sqrt(p.x * p.x + (p.y / s) * (p.y / s)) / rr;
                float nearSide = p.y < 0.0 ? 1.0 : 0.4;                 // 카메라 쪽(아래) 절반이 더 밝음
                return saturate((0.030 - abs(e - 1.0)) / (aa / (rr * s)) + 0.5) * nearSide;
            }

            // ---------------------------------------------------------------- 후폭풍의 반구
            // 윤곽에는 또렷한 초승달, 그 안쪽은 유리 같은 얇은 막, 그리고 곡면을 드러내는 눌린 호들
            float4 domeFrag(float2 uv, float t, float halfAngle, float seed, float groundP, float tilt, float aa)
            {
                float2 p = uv * 2.0 - 1.0;
                float r = length(p);
                float ang = atan2(p.y, p.x + 1e-6);
                float it = 1.0 - t;
                float rr = 0.92;                                        // 첫 프레임부터 이 크기. 줄어들지 않고 옅어지기만 함

                float x = saturate(abs(ang) / halfAngle);
                float inAng = saturate((halfAngle - abs(ang)) * r / aa + 0.5);
                float taper = pow(max(1.0 - x * x, 0.0), 0.8) * step(abs(ang), halfAngle);

                // 윤곽의 초승달
                float w = 0.15 * (1.0 - 0.45 * t) * taper + 0.012 * inAng;
                float d = rr - r;
                float vv = d / max(w, 1e-4);
                float n = vnoise(float2(ang * 5.0 + seed, r * 14.0 - t * 6.0));
                float ero = smoothstep(0.3, 1.0, vv) * (0.4 + 0.6 * t);
                float band = saturate(d / aa + 0.5) * saturate((w - d) / aa + 0.5) * saturate(w / aa);
                float aEdge = band * saturate((n - ero) / 0.05 + 0.5);
                float c1 = lerp(1.2, 0.40, smoothstep(0.0, 0.3, t)) * (1.0 - 0.9 * smoothstep(0.3, 0.8, t));
                float fv = max(aa / max(w, 1e-4), 1e-4);
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, saturate((vv - c1) / fv + 0.5));
                col = lerp(col, _EdgeColor.rgb, saturate((vv - c1 - 0.30) / fv + 0.5));

                // 얇은 막: 가운데는 거의 투명하고 윤곽 쪽으로 갈수록 밝음. 열린 뒤쪽으로는 옅어짐
                float inside = saturate(d / aa + 0.5) * inAng;
                float q = saturate(r / rr);
                float fres = q * q * q;
                float2 h = (p - float2(0.45, 0.50) * rr) / rr;
                float hl = exp(-dot(h, h) * 9.0);                       // 위쪽 앞의 은은한 하이라이트
                float openFade = smoothstep(0.0, 0.55, p.x / rr);
                float aFill = inside * (0.10 + 0.55 * fres + 0.35 * hl) * sqrt(it) * openFade;

                // 곡면을 드러내는 호: 바닥과 나란한 큰 원(바닥 기울기만큼 눌림)과 그 사이의 하나
                tilt = clamp(tilt, 0.05, 1.0);
                float mer = max(domeArc(p, rr, tilt, aa), domeArc(p, rr, 0.5 + 0.5 * tilt, aa) * 0.45)
                          * inside * it * openFade;

                float fade = (1.0 - smoothstep(0.10, 1.0, t)) * smoothstep(0.0, 0.05, p.y - groundP);   // 바닥 아래는 가림

                float3 rgb = lerp(_EdgeColor.rgb, _MidColor.rgb, fres) * (aFill * 0.6)
                           + lerp(_MidColor.rgb, _CoreColor.rgb, 0.4) * (mer * 0.7);
                float al = aFill * 0.25 + mer * 0.3;
                rgb = col * aEdge + rgb * (1.0 - aEdge);
                al = aEdge + al * (1.0 - aEdge);

                float g = exp(-abs(d - w * 0.3) * 8.0) * taper * _Glow * 0.6 * it * saturate((1.0 - r) / 0.08);
                rgb += _EdgeColor.rgb * (g * (1.0 - aEdge));
                return float4(rgb, al) * fade;
            }

            // ---------------------------------------------------------------- 바닥 바람
            // 바람 한 줄기: 꼭짓점에서 뒤쪽으로 뻗어 나가는 가늘고 긴 줄. x = 덮는 정도, y = 중심에 가까운 정도
            float2 windStreak(float rho, float ath, float sgn, float t, float phi, float seed, float aaTh,
                              float angFrac, float widthScale, float delay, float len, float idx)
            {
                float tt = saturate((t - delay) / (1.0 - delay));
                float i1 = 1.0 - saturate(tt / 0.55);
                float head = len * (1.0 - i1 * i1 * i1);                // 머리: 빠르게 뻗다가 감속
                float x2 = saturate((tt - 0.15) / 0.85);
                float tail = len * x2 * x2;                             // 꼬리가 뒤따라오며 사라짐
                float u = (rho - tail) / max(head - tail, 1e-3);
                float inWin = step(0.0, u) * step(u, 1.0);
                float uc = saturate(u);
                float prof = pow(max(uc, 1e-5), 1.3) * pow(max(1.0 - uc, 1e-5), 0.6) / 0.306;

                float n = vnoise(float2(rho * 9.0 - t * 3.0 + seed + idx * 7.0, sgn * 3.1 + idx));
                float centre = phi * angFrac + (n - 0.5) * 0.05;        // 살짝 일렁이는 줄기의 방향
                float wth = 0.055 * widthScale * prof * (1.0 - 0.6 * tt);
                float d = abs(ath - centre) - wth;
                return float2(saturate(-d / aaTh + 0.5) * inWin * saturate(wth / aaTh), saturate(-d / max(wth, 1e-4)));
            }

            // 바닥을 따라 뒤로 쓸려 가는 바람. 시전자 양옆을 지나며 세모꼴로 벌어짐
            float4 windFrag(float2 uv, float th, float t, float phi, float seed, float aaTh)
            {
                float rho = length(uv);
                float ath = abs(th);
                float sgn = uv.y < 0.0 ? -1.0 : 1.0;

                float2 s1 = windStreak(rho, ath, sgn, t, phi, seed, aaTh, 1.00, 1.00, 0.00, 1.00, 0.0);   // 세모꼴의 변
                float2 s2 = windStreak(rho, ath, sgn, t, phi, seed, aaTh, 0.74, 0.60, 0.08, 0.80, 1.0);   // 안쪽의 가는 줄기들
                float2 s3 = windStreak(rho, ath, sgn, t, phi, seed, aaTh, 0.50, 0.45, 0.16, 0.62, 2.0);

                float a = max(s1.x, max(s2.x * 0.8, s3.x * 0.6));
                float k = max(s1.y, max(s2.y, s3.y));
                a *= (1.0 - smoothstep(0.75, 1.0, t)) * smoothstep(0.02, 0.10, rho);
                float3 col = lerp(_MidColor.rgb, _CoreColor.rgb, smoothstep(0.2, 0.8, k));
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 연기
            // 울퉁불퉁한 구름 한 덩이. 위에서 빛을 받아 아래쪽이 어두움
            float4 smokeFrag(float2 uv, float seed, float age, float alpha)
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

            // ---------------------------------------------------------------- 불티
            float4 needleFrag(float2 uv, float fade, float aa)
            {
                float2 p = abs(uv * 2.0 - 1.0);
                float d = 1.0 - (p.x + p.y);
                float a = saturate(d / aa + 0.5) * fade;
                float3 col = lerp(_MidColor.rgb, _CoreColor.rgb, saturate((d - 0.35) / aa + 0.5));
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 발사 지점의 빛무리
            float4 glowFrag(float2 uv, float alpha)
            {
                float2 p = uv * 2.0 - 1.0;
                float r2 = dot(p, p);
                float a = exp(-r2 * 4.5) * alpha * saturate((1.0 - sqrt(r2)) / 0.3);
                float3 col = lerp(_EdgeColor.rgb, _CoreColor.rgb, exp(-r2 * 14.0));
                return float4(col * a, a * 0.15);                       // 거의 가산
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 미분은 분기 밖에서 미리 계산
                float aaU = max(fwidth(i.uv.x), 1e-5);
                float aaV = max(fwidth(i.uv.y), 1e-5);
                float aa2 = max(aaU, aaV) * 2.0;
                float th = atan2(i.uv.y, max(i.uv.x, 1e-6));            // 바닥 바람용 (꼭짓점 기준 각도)
                float aaTh = max(fwidth(th), 1e-5);

                float mode = i.uv2.x;
                float4 c;
                if (mode < 0.5)
                {
                    c = beamFrag(i.uv, saturate(i.uv2.y), i.uv2.z, saturate(i.uv2.w),
                                 i.prm.x, i.prm.y, i.prm.z, clamp(i.prm.w, 0.01, 0.5), aaV * 2.0);
                }
                else if (mode < 1.5)
                {
                    c = domeFrag(i.uv, saturate(i.uv2.y), max(i.uv2.z, 0.05), i.uv2.w, i.prm.x, i.prm.y, aa2);
                }
                else if (mode < 2.5)
                {
                    c = sparkleFrag(i.uv, i.uv2.y, aa2);
                }
                else if (mode < 3.5)
                {
                    c = needleFrag(i.uv, i.uv2.y, aa2);
                }
                else if (mode < 4.5)
                {
                    c = glowFrag(i.uv, i.uv2.y);
                }
                else if (mode < 5.5)
                {
                    c = ringFrag(i.uv, saturate(i.uv2.y), max(i.uv2.z, 0.05), i.uv2.w, aa2);
                }
                else if (mode < 6.5)
                {
                    c = windFrag(i.uv, th, saturate(i.uv2.y), max(i.uv2.z, 0.02), i.uv2.w, aaTh);
                }
                else
                {
                    c = smokeFrag(i.uv, i.uv2.y, i.uv2.z, i.uv2.w);
                }

                return fixed4(c.rgb, c.a) * _Alpha;
            }
            ENDCG
        }
    }

    Fallback Off
}
