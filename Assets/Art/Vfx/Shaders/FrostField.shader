// 서릿발 이펙트 (텍스처 불필요, 전부 수식으로 그림)
// Built-in 파이프라인 / URP(2D Renderer 포함) 양쪽에서 동작하는 Unlit 셰이더.
// FrostFieldVFX.cs 가 만드는 메시 전용입니다. 메시의 두 번째 UV(uv2.x)로 무엇을 그릴지 고릅니다.
//   uv2.x = 0 : 얼음 장판 (부채꼴)   uv = 바닥 좌표 / 반지름 (x = 앞, y = 옆)
//               uv2 = (0, 퍼진 정도 0..1, 충전 0..1, 알파),  uv3 = (부채꼴 반각 rad, 결정 무늬 밀도, 시드, 깨지는 파동의 위치)
//   uv2.x = 1 : 냉기 (뭉게구름)      uv2 = (1, 시드, 나이(초), 알파)
//   uv2.x = 2 : 반짝임 (4갈래 별)    uv2 = (2, 진행도 0..1, -, -)
//   uv2.x = 3 : 얼음 파편 (삼각형)   uv = 무게중심 좌표,  uv2 = (3, 알파, -, -)
Shader "VFX/FrostField"
{
    Properties
    {
        [Header(Color)]
        [HDR] _CoreColor ("Core Color (frost)", Color) = (1, 1, 1, 1)
        [HDR] _MidColor ("Mid Color (ice)", Color) = (0.66, 0.93, 1, 1)
        [HDR] _EdgeColor ("Edge Color (deep ice)", Color) = (0.13, 0.50, 1, 1)
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

        Blend One OneMinusSrcAlpha   // premultiplied
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
            float _Alpha;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uv2 = v.uv2;
                o.prm = v.prm;
                return o;
            }

            // ---------------------------------------------------------------- noise
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

            float2 hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            // 보로노이: (가장 가까운 점까지 거리, 두 번째로 가까운 점까지 거리, 칸마다 다른 난수), center = 그 칸의 중심
            float3 voronoi(float2 x, out float2 center)
            {
                float2 n = floor(x);
                float2 f = frac(x);
                float f1 = 8.0;
                float f2 = 8.0;
                float id = 0.0;
                center = float2(0.0, 0.0);
                for (int j = -1; j <= 1; j++)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(i, j);
                        float2 o = hash22(n + g);
                        float2 r = g + o - f;
                        float d = dot(r, r);
                        if (d < f1) { f2 = f1; f1 = d; id = o.x; center = n + g + o; }
                        else if (d < f2) { f2 = d; }
                    }
                }
                return float3(sqrt(f1), sqrt(f2), id);
            }

            // ---------------------------------------------------------------- 얼음 장판
            // 부채꼴로 퍼지는 서리. 가장자리는 들쭉날쭉하고, 안쪽은 얼음 결정 무늬(보로노이)의 금이 보임.
            // brk : 깨지는 파동의 위치 (반지름 대비). 파동이 지나간 결정 칸은 하얗게 번쩍인 뒤 떨어져 나감
            float4 floorFrag(float2 uv, float front, float charge, float fade, float halfAngle, float density, float seed, float brk, float aa)
            {
                float r = length(uv);
                float th = atan2(uv.y, uv.x + 1e-6);

                float n = vnoise(uv * 5.0 + float2(seed, 0.0));
                float n2 = vnoise(float2(th * 6.0 + seed * 1.7, r * 3.0));
                float n3 = vnoise(uv * 19.0 + float2(-seed, seed));
                float rr = r + ((n - 0.5) * 0.16 + (n3 - 0.5) * 0.05) * smoothstep(0.05, 0.3, r);
                float ta = abs(th) + (n2 - 0.5) * 0.22 + (n3 - 0.5) * 0.07;
                float e = min(front - rr, (halfAngle - ta) * r);      // 가장 가까운 가장자리까지 거리
                float mask = saturate(e / (aa + 0.012) + 0.5);

                float2 cellCenter;
                float3 vor = voronoi(uv * density, cellCenter);
                float crack = 1.0 - smoothstep(0.02, 0.09, vor.y - vor.x);
                float shade = floor(vor.z * 3.0) * 0.5;               // 칸마다 0 / 0.5 / 1
                float frost = max(smoothstep(front - 0.30, front, rr), 1.0 - smoothstep(0.0, 0.07, e));

                float3 col = lerp(_MidColor.rgb, _CoreColor.rgb, 0.30 + 0.30 * shade);
                col = lerp(col, _EdgeColor.rgb, 0.22 * (1.0 - shade));
                col = lerp(col, _CoreColor.rgb, max(frost * 0.85, crack * 0.9));     // 갓 얼어붙은 쪽과 금은 하얗게
                col = lerp(col, _CoreColor.rgb, saturate(charge * (0.35 + 0.65 * crack)));

                // 가까운 칸부터, 칸마다 조금씩 어긋난 타이밍으로 깨짐
                float k = brk - length(cellCenter) / density - vor.z * 0.18;
                float flare = smoothstep(-0.14, 0.0, k);
                float gone = step(0.0, k);
                col = lerp(col, _CoreColor.rgb, flare);

                float a = mask * saturate(0.55 + 0.40 * max(crack, frost) + 0.3 * charge + 0.5 * flare) * fade * (1.0 - gone);
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 냉기
            float4 puffFrag(float2 uv, float seed, float age, float alpha)
            {
                float2 p = uv * 2.0 - 1.0;
                float r = length(p);
                float n = vnoise(p * 1.8 + float2(seed * 13.1, -age * 0.8 + seed * 5.3));
                float n2 = vnoise(p * 4.0 + float2(seed * 7.7, seed * 3.1));
                float d = r + (n - 0.5) * 0.55 + (n2 - 0.5) * 0.2;
                float a = (1.0 - smoothstep(0.35, 0.98, d)) * alpha * 0.9 * (1.0 - smoothstep(0.8, 1.0, r));   // 쿼드 가장자리에서 잘리지 않게

                float shade = saturate(0.55 + p.y * 0.5 + (n - 0.5) * 0.7);       // 아래쪽이 살짝 푸름
                float3 shadow = lerp(_MidColor.rgb, _CoreColor.rgb, 0.25);
                float3 col = lerp(shadow, _CoreColor.rgb, smoothstep(0.10, 0.50, shade));
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 반짝임
            float4 sparkleFrag(float2 uv, float t, float aa)
            {
                float2 p = abs(uv * 2.0 - 1.0);
                float st = sqrt(p.x) + sqrt(p.y);
                float c = 0.95 * sin(3.14159265 * saturate(t));        // 커졌다 작아짐
                float a = saturate((c - st) / (aa * 3.0) + 0.5) * saturate(c / (aa * 6.0));
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, smoothstep(0.45, 0.9, st / max(c, 1e-4)));
                return float4(col * a, a);
            }

            // ---------------------------------------------------------------- 얼음 파편
            float4 shardFrag(float2 uv, float fade, float aa)
            {
                float m = min(min(uv.x, uv.y), 1.0 - uv.x - uv.y);
                float cut = saturate(m / aa);
                float rim = 1.0 - saturate((m - 0.05) / aa);
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, saturate(uv.x * 1.6));
                col = lerp(col, _EdgeColor.rgb, saturate(uv.y * 1.5 - 0.9));
                col = lerp(col, _CoreColor.rgb, rim);
                float a = lerp(0.85, 1.0, rim) * cut * fade;
                return float4(col * a, a);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 미분은 분기 밖에서 미리 계산
                float aaU = max(fwidth(i.uv.x), 1e-5);
                float aaV = max(fwidth(i.uv.y), 1e-5);

                float mode = i.uv2.x;
                float4 c;
                if (mode < 0.5)
                {
                    c = floorFrag(i.uv, i.uv2.y, i.uv2.z, i.uv2.w, i.prm.x, i.prm.y, i.prm.z, i.prm.w, max(aaU, aaV));
                }
                else if (mode < 1.5)
                {
                    c = puffFrag(i.uv, i.uv2.y, i.uv2.z, i.uv2.w);
                }
                else if (mode < 2.5)
                {
                    c = sparkleFrag(i.uv, i.uv2.y, max(aaU, aaV) * 2.0);
                }
                else
                {
                    c = shardFrag(i.uv, i.uv2.y, max(aaU, aaV) * 1.5);
                }

                return fixed4(c.rgb, c.a) * _Alpha;
            }
            ENDCG
        }
    }

    Fallback Off
}
