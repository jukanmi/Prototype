// 마력균열 이펙트 (텍스처 불필요, 전부 수식으로 그림)
// Built-in 파이프라인 / URP(2D Renderer 포함) 양쪽에서 동작하는 Unlit 셰이더.
// ManaRiftVFX.cs 가 만드는 메시 전용입니다. 메시의 두 번째 UV(uv2.x)로 무엇을 그릴지 고릅니다.
//   uv2.x = 0 : 바닥의 공간 폭풍 (소용돌이)   uv = 바닥 좌표 (단위원 = 폭풍의 가장자리)
//               uv2 = (0, 시간(초), 세기 0..1, 시드)
//   uv2.x = 1 : 균열의 핵 (가장자리가 찢긴 어두운 구)   uv2 = (1, 시간(초), 섬광 0..1, 시드)
//               uv3 = (가로 배율, 세로 배율, 쿼드 반너비, 쿼드 반높이)  ← 핵이 눌려도 테두리 두께와 결이 찌그러지지 않게
//   uv2.x = 2 : 균열 (핵에서 뻗어 나가는 들쭉날쭉한 금)   uv = (0 = 핵 .. 1 = 끝, 폭 방향 0..1)
//               uv2 = (2, 뻗은 정도 0..1, 알파, 어두운 정도 0..1),  uv3 = (시드, -, -, -)
//   uv2.x = 3 : 섬광 (4갈래 별)   uv2 = (3, 진행도 0..1, -, -)
//   uv2.x = 4 : 줄기 (가늘고 긴 마름모)   uv2 = (4, 알파, -, -)
//   uv2.x = 5 : 공간의 파편 (삼각형)   uv = 무게중심 좌표,  uv2 = (5, 알파, -, -)
//   uv2.x = 6 : 바닥 충격파 (퍼지는 고리)   uv = 바닥 좌표,  uv2 = (6, 진행도 0..1, 시드, -)
//   uv2.x = 7 : 빛무리   uv2 = (7, 알파, -, -)
Shader "VFX/ManaRift"
{
    Properties
    {
        [Header(Color)]
        [HDR] _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        [HDR] _MidColor ("Mid Color", Color) = (0.72, 0.45, 1, 1)
        [HDR] _EdgeColor ("Edge Color (glow)", Color) = (0.50, 0, 1, 1)
        _DarkColor ("Rift Color (dark)", Color) = (0.04, 0, 0.10, 1)
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

            float4 _CoreColor, _MidColor, _EdgeColor, _DarkColor;
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

            // 안쪽으로 끌려 들어가는 고리 하나 (phase 0 = 가장자리에서 시작, 1 = 중심에 도착)
            float pullRing(float r, float phase, float aa)
            {
                float rp = lerp(0.95, 0.15, phase * phase);
                return (1.0 - smoothstep(0.0, 0.018 + aa, abs(r - rp))) * sin(3.14159265 * phase);
            }

            // ---------------------------------------------------------------- 바닥의 공간 폭풍
            // 세 갈래의 나선 팔이 중심으로 감겨 들어가며 돌고, 가운데에는 어두운 눈(균열)이 있음
            float4 vortexFrag(float2 uv, float time, float intensity, float seed, float aa)
            {
                float r = length(uv);
                float th = atan2(uv.y, uv.x + 1e-6);
                float lr = log(max(r, 0.03));

                float armWave = 0.5 + 0.5 * sin(3.0 * th + 5.0 * lr + time * 5.0);

                // 노이즈도 같은 방향으로 휘감음 (좌표를 반지름에 따라 돌리는 것이라 이음매가 없음)
                float ang = (5.0 * lr + time * 5.0) / 3.0;
                float sn, cs;
                sincos(ang, sn, cs);
                float2 q = float2(uv.x * cs - uv.y * sn, uv.x * sn + uv.y * cs);
                float n = vnoise(q * 5.0 + float2(seed, -seed));
                float n2 = vnoise(q * 11.0 + float2(-seed, seed * 2.0));
                float vv = armWave * 0.6 + n * 0.45 + n2 * 0.15;
                float arm = smoothstep(0.55, 0.95, vv);

                float3 col = lerp(_DarkColor.rgb, _EdgeColor.rgb, 0.25 + 0.35 * n);
                col = lerp(col, _MidColor.rgb, arm * 0.85);
                col = lerp(col, _CoreColor.rgb, smoothstep(0.85, 1.05, vv) * arm);
                col += _MidColor.rgb * (exp(-r * 4.0) * 0.5);           // 가운데로 갈수록 뜨겁게

                // 끌어당기는 느낌: 가장자리에서 중심으로 좁혀 드는 고리 둘
                float pull = max(pullRing(r, frac(time * 0.6), aa), pullRing(r, frac(time * 0.6 + 0.5), aa));
                float rim = 1.0 - smoothstep(0.0, 0.03 + aa, abs(r - 0.93));
                col = lerp(col, _CoreColor.rgb, max(rim * 0.9, pull * 0.7));

                float hole = 1.0 - smoothstep(0.10, 0.20, r);           // 균열: 어두운 눈
                col = lerp(col, _DarkColor.rgb, hole);

                float env = 1.0 - smoothstep(0.86, 1.0, r);
                float a = env * saturate(0.50 + 0.45 * max(arm, max(rim, pull)) + hole) * intensity;
                return float4(saturate(col) * a, a);
            }

            // ---------------------------------------------------------------- 균열의 핵
            // shape = (가로 배율, 세로 배율, 쿼드 반너비, 쿼드 반높이). 반너비/반높이는 눌리지 않은 핵의 쿼드 반크기를 1 로 본 값.
            // 쿼드를 그냥 늘리면 테두리와 노이즈까지 찌그러지므로, 눌리지 않은 공간(q)에서 타원을 직접 계산한다.
            float4 orbFrag(float2 uv, float time, float flash, float seed, float4 shape, float aa)
            {
                float2 p = uv * 2.0 - 1.0;
                float2 sc = max(shape.xy, 1e-3);
                float2 q = p * shape.zw;                                // 눌리지 않은 핵의 반지름 = 0.58

                float n = vnoise(q * 3.0 + float2(time * 1.3, seed));
                float2 axes = 0.58 * sc * (1.0 + 0.1724 * (n - 0.5));   // 일렁이는 찢긴 가장자리
                float2 el = q / axes;
                float r = length(el);                                   // 1 = 가장자리
                float ang = atan2(el.y, el.x + 1e-6);

                // 타원까지의 거리 (1차 근사). 가장자리 근처에서 정확하면 충분함
                float gk = (r > 1e-4) ? r / max(length(el / axes), 1e-5) : min(axes.x, axes.y);
                float d = (r - 1.0) * gk;
                float inside = saturate(-d / aa + 0.5);
                float k = 1.0 + d / (0.58 * sqrt(sc.x * sc.y));

                float3 col = lerp(_DarkColor.rgb, _EdgeColor.rgb, smoothstep(0.55, 0.90, k) * 0.8);
                col = lerp(col, _MidColor.rgb, smoothstep(0.80, 0.97, k));
                col = lerp(col, _CoreColor.rgb, smoothstep(0.93, 1.0, k));

                // 안쪽에서 빨려 들어가는 소용돌이 결
                float swirl = sin(ang * 2.0 + log(max(r * 0.58, 0.02)) * 4.0 - time * 6.0);
                col += _EdgeColor.rgb * (0.30 * smoothstep(0.6, 1.0, swirl) * saturate(1.0 - k));

                // 섬광: 회색을 거치지 않도록 보라 → 흰색 순으로 달아오름
                col = lerp(saturate(col), _MidColor.rgb, saturate(flash * 2.0));
                col = lerp(col, _CoreColor.rgb, saturate(flash * 2.0 - 1.0));

                // 빛무리: 중심에서 뻗는 방향으로 잰 거리 (쿼드 밖으로 넘치지 않음)
                float m = max(sc.x, sc.y);
                float dg = length(q) * max(1.0 - 1.0 / max(r, 1e-4), 0.0) / m;
                float g = exp(-dg * 6.0) * _Glow * saturate((0.42 - dg) / 0.2)
                        * saturate((1.0 - max(abs(p.x), abs(p.y))) / 0.08);
                float3 glowCol = lerp(_EdgeColor.rgb, _MidColor.rgb, 0.3);
                return float4(col * inside + glowCol * (g * (1.0 - inside)), inside);
            }

            // ---------------------------------------------------------------- 균열
            // 핵에서 바깥으로 뻗는 들쭉날쭉한 금. dark = 1 이면 속이 어둡고 테두리만 빛나는 "공간의 틈"
            float4 crackFrag(float2 uv, float grow, float alpha, float dark, float seed, float aaV)
            {
                float u = uv.x;
                float y = uv.y * 2.0 - 1.0;
                float vis = step(u, grow);
                float taper = pow(max(1.0 - u / max(grow, 1e-3), 0.0), 0.8);

                float off = ((vnoise(float2(u * 7.0 + seed, seed * 1.7)) - 0.5) * 0.9
                           + (vnoise(float2(u * 19.0 + seed * 3.0, 2.3)) - 0.5) * 0.35) * smoothstep(0.0, 0.15, u);
                float w = 0.22 * taper;
                float d = abs(y - off * 0.6) - w;
                float a = saturate(-d / aaV + 0.5) * vis * saturate(w / aaV);
                float k = saturate(-d / max(w, 1e-4));

                float3 bright = lerp(_MidColor.rgb, _CoreColor.rgb, smoothstep(0.2, 0.7, k));
                float3 darkCol = lerp(_MidColor.rgb, _DarkColor.rgb, smoothstep(0.25, 0.60, k));
                float3 col = lerp(bright, darkCol, dark);

                float g = exp(-max(d, 0.0) * 7.0) * _Glow * 0.8 * vis * taper * saturate((1.0 - abs(y)) / 0.2);
                return float4(col * a + _EdgeColor.rgb * (g * (1.0 - a)), a) * alpha;
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

            // ---------------------------------------------------------------- 줄기
            float4 needleFrag(float2 uv, float fade, float aa)
            {
                float2 p = abs(uv * 2.0 - 1.0);
                float d = 1.0 - (p.x + p.y);
                float a = saturate(d / aa + 0.5) * fade;
                float3 col = lerp(_MidColor.rgb, _CoreColor.rgb, saturate((d - 0.35) / aa + 0.5));
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 공간의 파편
            // 유리 조각처럼 테두리가 밝고, 비스듬한 반사광 한 줄이 있음
            float4 shardFrag(float2 uv, float fade, float aa)
            {
                float m = min(min(uv.x, uv.y), 1.0 - uv.x - uv.y);
                float cut = saturate(m / aa);
                float rim = 1.0 - saturate((m - 0.06) / aa);
                float streak = 1.0 - smoothstep(0.0, 0.09, abs(uv.x - uv.y - 0.1));
                float3 fill = lerp(_EdgeColor.rgb, _MidColor.rgb, saturate(uv.x * 1.4));
                fill = lerp(fill, _CoreColor.rgb, streak * 0.7);
                float aFill = saturate(0.45 * (1.0 + 1.2 * streak));

                float3 rgb = fill * (aFill * (1.0 - rim)) + _CoreColor.rgb * rim;
                float a = aFill * (1.0 - rim) + rim;
                return float4(rgb, a) * (cut * fade);
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

            fixed4 frag(v2f i) : SV_Target
            {
                // 미분은 분기 밖에서 미리 계산
                float aaU = max(fwidth(i.uv.x), 1e-5);
                float aaV = max(fwidth(i.uv.y), 1e-5);
                float aa2 = max(aaU, aaV) * 2.0;
                float aaR = max(fwidth(length(i.uv)), 1e-5);
                float aaOrb = max(aaU * abs(i.prm.z), aaV * abs(i.prm.w)) * 2.0 + 1e-5;    // 핵 전용: 눌리지 않은 공간에서의 1픽셀

                float mode = i.uv2.x;
                float4 c;
                if (mode < 0.5)
                {
                    c = vortexFrag(i.uv, i.uv2.y, saturate(i.uv2.z), i.uv2.w, aaR);
                }
                else if (mode < 1.5)
                {
                    c = orbFrag(i.uv, i.uv2.y, saturate(i.uv2.z), i.uv2.w, i.prm, aaOrb);
                }
                else if (mode < 2.5)
                {
                    c = crackFrag(i.uv, saturate(i.uv2.y), i.uv2.z, saturate(i.uv2.w), i.prm.x, aaV * 2.0);
                }
                else if (mode < 3.5)
                {
                    c = sparkleFrag(i.uv, i.uv2.y, aa2);
                }
                else if (mode < 4.5)
                {
                    c = needleFrag(i.uv, i.uv2.y, aa2);
                }
                else if (mode < 5.5)
                {
                    c = shardFrag(i.uv, i.uv2.y, max(aaU, aaV) * 1.5);
                }
                else if (mode < 6.5)
                {
                    c = shockFrag(i.uv, saturate(i.uv2.y), i.uv2.z, aaR);
                }
                else
                {
                    c = glowFrag(i.uv, i.uv2.y);
                }

                return fixed4(c.rgb, c.a) * _Alpha;
            }
            ENDCG
        }
    }

    Fallback Off
}
