// 갈고리 사슬 이펙트 (텍스처 불필요, 전부 수식으로 그림)
// Built-in 파이프라인 / URP(2D Renderer 포함) 양쪽에서 동작하는 Unlit 셰이더.
// HookChainVFX.cs 가 만드는 메시 전용입니다. 메시의 두 번째 UV(uv2.x)로 무엇을 그릴지 고릅니다.
//   uv2.x = 0 : 사슬      uv.x = 갈고리 쪽에서부터 센 고리 개수, uv.y = 폭 방향 0..1
//   uv2.x = 1 : 갈고리 촉  uv.x = 0(뿌리) -> 1(끝)
//   uv2.x = 2 : 적중 섬광  uv2.y = 진행도 0..1
Shader "VFX/HookChain"
{
    Properties
    {
        [Header(Metal)]
        _BaseColor ("Base Color (shadow side)", Color) = (0.10, 0.17, 0.34, 1)
        _LightColor ("Light Color (lit side)", Color) = (0.50, 0.76, 0.98, 1)
        _OutlineColor ("Outline Color", Color) = (0.02, 0.05, 0.20, 1)
        _GlintColor ("Blade Edge Glint", Color) = (1, 1, 1, 1)

        [Header(Glow)]
        [HDR] _GlowColor ("Glow Color", Color) = (0.13, 0.50, 1, 1)
        _Glow ("Glow", Range(0, 2)) = 0.45

        _Alpha ("Alpha", Range(0, 1)) = 1
        [HideInInspector] _Aspect ("Strip Width / Link Size", Float) = 1.4
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

        Blend One OneMinusSrcAlpha   // premultiplied: 불투명한 금속 + 가산 글로우를 한 패스로
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
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            float4 _BaseColor, _LightColor, _OutlineColor, _GlintColor, _GlowColor;
            float _Glow, _Alpha, _Aspect;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.uv2 = v.uv2;
                return o;
            }

            // 직선 a->b 까지의 부호 있는 거리 (진행 방향의 왼쪽이 +)
            float edgeDist(float2 p, float2 a, float2 b)
            {
                float2 d = b - a;
                return (d.x * (p.y - a.y) - d.y * (p.x - a.x)) / length(d);
            }

            // 삼각형 SDF (Inigo Quilez)
            float sdTriangle(float2 p, float2 p0, float2 p1, float2 p2)
            {
                float2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
                float2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
                float2 pq0 = v0 - e0 * saturate(dot(v0, e0) / dot(e0, e0));
                float2 pq1 = v1 - e1 * saturate(dot(v1, e1) / dot(e1, e1));
                float2 pq2 = v2 - e2 * saturate(dot(v2, e2) / dot(e2, e2));
                float s = sign(e0.x * e2.y - e0.y * e2.x);
                float2 d = min(min(float2(dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                                   float2(dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                                   float2(dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
                return -sqrt(d.x) * sign(d.y);
            }

            // ---------------------------------------------------------------- 사슬
            // 정면으로 보이는 고리(타원 링)와 옆으로 선 고리(막대)가 번갈아 이어짐
            float4 chainFrag(float x, float y, float up, float aa)
            {
                float fx = frac(x * 0.5) * 2.0;                       // 고리 2개가 한 주기

                float xr = fx - 0.5;                                  // 링 중심 = 0.5
                if (xr > 1.0) xr -= 2.0;
                float dCap = length(float2(xr - clamp(xr, -0.38, 0.38), y)) - 0.26;
                float dRing = abs(dCap) - 0.085;

                float xb = fx - 1.5;                                  // 막대 중심 = 1.5
                if (xb < -1.0) xb += 2.0;
                float dBar = length(float2(xb - clamp(xb, -0.50, 0.50), y)) - 0.10;

                float inBar = step(dBar, 0.0);
                float d = min(dRing, dBar);
                float dLoc = lerp(dRing, dBar, inBar);

                // 위에서 빛을 받는 2톤: 링은 바깥벽 위쪽/안벽 아래쪽이 밝음
                float yl = y * up;
                float lit = lerp(step(0.0, dCap * yl), step(0.0, yl), inBar);
                float3 col = lerp(_BaseColor.rgb, _LightColor.rgb, lit);
                col = lerp(col, _OutlineColor.rgb, saturate((dLoc + 0.045) / aa + 0.5));

                float a = saturate(-d / aa + 0.5);
                float pulse = 0.75 + 0.25 * sin(x * 0.8 - _Time.y * 14.0);
                float g = exp(-max(d, 0.0) * 9.0) * _Glow * pulse * saturate((_Aspect * 0.5 - abs(y)) / 0.25);
                return float4(col * a + _GlowColor.rgb * (g * (1.0 - a)), a);
            }

            // ---------------------------------------------------------------- 갈고리 촉
            // 마름모 날 + 뒤로 젖혀진 미늘 한 쌍 + 사슬이 물리는 소켓
            float4 tipFrag(float2 uv, float up, float aa)
            {
                float px = uv.x * 1.1 - 0.05;
                float py = (uv.y - 0.5) * 0.88;
                float2 q = float2(px, abs(py));

                float dBlade = max(edgeDist(q, float2(0.20, 0.0), float2(0.46, 0.115)),
                                   edgeDist(q, float2(0.46, 0.115), float2(1.0, 0.0)));
                float dBarb = sdTriangle(q, float2(0.52, 0.07), float2(0.10, 0.30), float2(0.36, 0.06));
                float2 b = abs(q - float2(0.13, 0.0)) - float2(0.13, 0.05);
                float dSock = length(max(b, 0.0)) + min(max(b.x, b.y), 0.0);
                float d = min(min(dBlade, dBarb), dSock);

                float inBlade = step(dBlade, 0.0);
                float lit = step(0.0, py * up) * max(inBlade, step(dSock, 0.0));
                float3 col = lerp(_BaseColor.rgb, _LightColor.rgb, lit);

                // 날 끝쪽 가장자리를 따라 밝은 선
                float glint = inBlade * saturate((dBlade + 0.022) / aa + 0.5) * step(0.46, px);
                col = lerp(col, _GlintColor.rgb, glint);

                float dLoc = lerp(min(dBarb, dSock), dBlade, inBlade);
                col = lerp(col, _OutlineColor.rgb, saturate((dLoc + 0.016) / aa + 0.5) * (1.0 - glint));

                float a = saturate(-d / aa + 0.5);
                float g = exp(-max(d, 0.0) * 22.0) * _Glow * 1.2
                        * saturate((0.44 - q.y) / 0.1) * saturate((px + 0.05) / 0.05) * saturate((1.05 - px) / 0.05);
                return float4(col * a + _GlowColor.rgb * (g * (1.0 - a)), a);
            }

            // ---------------------------------------------------------------- 적중 섬광 (4갈래 별)
            float4 flashFrag(float2 uv, float t, float aa)
            {
                float2 p = abs(uv * 2.0 - 1.0);
                float st = sqrt(p.x) + sqrt(p.y);
                float grow = 1.0 - (1.0 - t) * (1.0 - t) * (1.0 - t);
                float c = 0.45 + 0.55 * grow;                         // 바깥 경계: 빠르게 커짐
                float hole = c * smoothstep(0.25, 1.0, t);            // 안쪽부터 비면서 사라짐
                float a = saturate((c - st) / (aa * 3.0) + 0.5) * saturate((st - hole) / (aa * 3.0) + 0.5)
                        * saturate((c - hole) / (aa * 6.0));
                float k = saturate((st - hole) / max(c - hole, 1e-4));
                float3 col = lerp(float3(1, 1, 1), lerp(_GlowColor.rgb, float3(1, 1, 1), 0.45), smoothstep(0.30, 0.65, k));
                col = lerp(col, _GlowColor.rgb, smoothstep(0.70, 0.95, k));
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
                    float y = (i.uv.y - 0.5) * _Aspect;
                    c = chainFrag(i.uv.x, y, i.uv2.y, max(aaU, aaV * _Aspect));
                }
                else if (mode < 1.5)
                {
                    c = tipFrag(i.uv, i.uv2.y, max(aaU * 1.1, aaV * 0.88));
                }
                else
                {
                    c = flashFrag(i.uv, saturate(i.uv2.y), max(aaU, aaV) * 2.0);
                }

                return fixed4(c.rgb, c.a) * (i.color.a * _Alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
