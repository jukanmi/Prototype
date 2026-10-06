// 일섬 이펙트 (텍스처 불필요, 전부 수식으로 그림)
// Built-in 파이프라인 / URP(2D Renderer 포함) 양쪽에서 동작하는 Unlit 셰이더.
// FlashSlashVFX.cs 가 만드는 메시 전용입니다. 메시의 두 번째 UV(uv2.x)로 무엇을 그릴지 고릅니다.
//   uv2.x = 0 : 참격선 (양끝이 뾰족한 바늘 모양)
//               uv = (길이 방향, 폭 방향) 0..1,  uv2 = (0, 그어진 정도, 글로우 세기, -)
//               uv3 = (가장 두꺼운 지점, 보조색 비율, 흰색 띠 비율, 굵기 배율)
//   uv2.x = 1 : 작은 파편 (삼각형)
//               uv = 무게중심 좌표,  uv2 = (1, -, 테두리 굵기, -)
//               uv3 = (면 불투명도, 보조색 비율, 테두리 불투명도, 전체 알파)
//   uv2.x = 2 : 섬광 (4갈래 별)   uv2.y = 진행도, uv3.w = 알파
//   uv2.x = 3 : 공간의 조각 (깨진 거울처럼 배경을 비추는 볼록 다각형, 중심에서 부채꼴로 나뉨)
//               uv.x = 중심의 무게 (바깥 변에서 0),  uv2 = (3, 비친 배경의 밝기, 테두리 선의 폭(픽셀), 반짝임 0..1)
//               uv3 = (배경을 못 찍을 때의 면 불투명도, -, 테두리 선의 진하기, 전체 알파)
//               uv4 = (그 꼭짓점이 비출 배경의 위치 xy, 화면 비율 단위로 더 어긋나는 양 uv)
//                     -> 조각이 날아가도 원래 자리의 배경을 싣고 가고, 위치를 일그러뜨리면 거울처럼 왜곡됨
//
// 배경 굴절: FlashSlashVFX 가 보조 카메라로 찍은 장면(_FlashSceneTex)을 화면 좌표로 샘플링합니다.
// GrabPass 나 URP 전용 텍스처를 쓰지 않으므로 렌더 파이프라인에 상관없이 동작합니다.
Shader "VFX/FlashSlash"
{
    Properties
    {
        [Header(Color)]
        [HDR] _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        [HDR] _MidColor ("Mid Color", Color) = (0.66, 0.93, 1, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.13, 0.50, 1, 1)
        [HDR] _AccentColor ("Accent Color (some slashes)", Color) = (0.55, 0.35, 1, 1)
        _Glow ("Glow", Range(0, 2)) = 0.7
        _Alpha ("Alpha", Range(0, 1)) = 1

        [Header(Glass)]
        _GlassTint ("Glass Tint", Range(0, 0.5)) = 0.03
        [HideInInspector] _FlashSceneTex ("Scene Capture", 2D) = "black" {}
        [HideInInspector] _Refract ("Refract (0/1)", Float) = 0
        [HideInInspector] _FlipY ("Flip Capture Y (0/1)", Float) = 0
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
                float4 src : TEXCOORD3;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
                float4 prm : TEXCOORD2;
                float4 grab : TEXCOORD3;
            };

            float4 _CoreColor, _MidColor, _EdgeColor, _AccentColor;
            float _Glow, _Alpha, _GlassTint, _Refract, _FlipY;
            sampler2D _FlashSceneTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uv2 = v.uv2;
                o.prm = v.prm;

                // 조각의 "원래 위치"가 화면의 어디였는지 -> 그 자리의 배경을 읽어 오기 위한 화면 좌표
                float4 srcClip = UnityObjectToClipPos(float4(v.src.xy, 0.0, 1.0));
                o.grab = ComputeScreenPos(srcClip);
                o.grab.xy += v.src.zw * o.grab.w;                     // 굴절: 조각마다 조금씩 어긋나게
                return o;
            }

            // ---------------------------------------------------------------- 참격선
            // draw : 한쪽 끝에서 반대쪽 끝으로 그어진 정도 (0..1)
            // c1   : 흰색 띠가 차지하는 비율 (1 이상이면 전부 흰색)
            // wk   : 굵기 배율 (0 이면 사라짐)
            float4 needleFrag(float2 uv, float draw, float glowK, float bias, float accent, float c1, float wk, float aaV)
            {
                float v = uv.y * 2.0 - 1.0;

                float ih = 1.0 - saturate(draw);
                float h = 1.0 - ih * ih * ih;                         // 그어지는 머리 위치 (ease-out)
                float uu = uv.x / max(h, 1e-3);
                float vis = step(uu, 1.0);

                bias = clamp(bias, 0.05, 0.95);
                float e = uu < bias ? (bias - uu) / bias : (uu - bias) / (1.0 - bias);
                float prof = saturate(1.0 - e * e);                   // 양끝이 뾰족한 렌즈 모양

                float w = 0.38 * prof * max(wk, 0.0);                 // 반폭 (v 단위)
                float av = abs(v);
                float a = saturate((w - av) / aaV + 0.5) * saturate(w / aaV) * vis;

                float k = av / max(w, 1e-4);                          // 0 = 중심, 1 = 가장자리
                float fk = max(aaV / max(w, 1e-4), 1e-4);
                float3 edgeCol = lerp(_EdgeColor.rgb, _AccentColor.rgb, accent);
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, saturate((k - c1) / fk + 0.5));
                col = lerp(col, edgeCol, saturate((k - c1 - 0.3) / fk + 0.5));

                float g = exp(-max(av - w, 0.0) * 6.0) * prof * _Glow * max(glowK, 0.0)
                        * vis * saturate((1.0 - av) / 0.3);
                return float4(col * a + edgeCol * (g * (1.0 - a)), a);
            }

            // ---------------------------------------------------------------- 작은 파편
            // 밝은 테두리 + 비스듬한 반사광 한 줄이 있는 반투명한 색유리
            float4 shardFrag(float2 uv, float fill, float accent, float rimA, float fade, float rimW, float aa)
            {
                float m = min(min(uv.x, uv.y), 1.0 - uv.x - uv.y);    // 가장 가까운 변까지 (무게중심 좌표)
                float cut = saturate(m / aa);
                float rim = (1.0 - saturate((m - rimW) / aa)) * rimA;

                float3 edgeCol = lerp(_EdgeColor.rgb, _AccentColor.rgb, accent);
                float streak = 1.0 - smoothstep(0.0, 0.09, abs(uv.x - uv.y - 0.1));
                float3 fillCol = lerp(_MidColor.rgb, edgeCol, saturate(uv.x * 1.4));
                fillCol = lerp(fillCol, _CoreColor.rgb, streak * 0.7);
                float aFill = saturate(fill * (1.0 + 2.0 * streak));

                float3 rgb = fillCol * aFill * (1.0 - rim) + _CoreColor.rgb * rim;   // premultiplied
                float a = aFill * (1.0 - rim) + rim;
                return float4(rgb, a) * (cut * fade);
            }

            // ---------------------------------------------------------------- 공간의 조각 (깨진 거울)
            // w    : 중심의 무게 (바깥 변에서 0),  aaW : 화면 1픽셀당 w 의 변화량  ->  w / aaW = 바깥 변까지의 픽셀 거리
            // bg   : 그 조각이 비추는 (어긋나고 일그러진) 배경
            // 테두리는 가늘고 흐린 선 한 줄뿐. 조각이 드러나는 것은 주로 비친 배경이 이웃 조각과 어긋나기 때문
            float4 mirrorFrag(float w, float aaW, float3 bg, float shade, float glint, float lineA, float linePx, float fill, float fade)
            {
                float px = w / aaW;
                float cut = saturate(px / 1.5);
                float rim = (1.0 - smoothstep(0.0, 1.5 * max(linePx, 0.01), px)) * lineA;

                // 배경을 찍고 있을 때: 불투명한 거울 조각
                float3 scene = lerp(bg, _MidColor.rgb, _GlassTint) * shade;
                scene = lerp(scene, _CoreColor.rgb, glint);
                float3 mirrorRgb = lerp(scene, _CoreColor.rgb, rim);

                // 배경을 찍지 못할 때: 반투명한 색유리 조각 (premultiplied)
                float aGlass = saturate(fill) * (1.0 - rim) + rim;
                float3 glassRgb = _MidColor.rgb * (saturate(fill) * (1.0 - rim)) + _CoreColor.rgb * rim;

                float3 rgb = lerp(glassRgb, mirrorRgb, _Refract);
                float a = lerp(aGlass, 1.0, _Refract);
                return float4(rgb, a) * (cut * fade);
            }

            // ---------------------------------------------------------------- 섬광 (4갈래 별)
            float4 flashFrag(float2 uv, float t, float aa)
            {
                float2 p = abs(uv * 2.0 - 1.0);
                float st = sqrt(p.x) + sqrt(p.y);
                float it = 1.0 - t;
                float grow = 1.0 - it * it * it;
                float c = 0.45 + 0.55 * grow;                         // 바깥 경계: 빠르게 커짐
                float hole = c * smoothstep(0.25, 1.0, t);            // 안쪽부터 비면서 사라짐
                float a = saturate((c - st) / (aa * 3.0) + 0.5) * saturate((st - hole) / (aa * 3.0) + 0.5)
                        * saturate((c - hole) / (aa * 6.0));
                float k = saturate((st - hole) / max(c - hole, 1e-4));
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, smoothstep(0.30, 0.65, k));
                col = lerp(col, _EdgeColor.rgb, smoothstep(0.70, 0.95, k));
                return float4(col * a, a);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 미분은 분기 밖에서 미리 계산
                float aaU = max(fwidth(i.uv.x), 1e-5);
                float aaV = max(fwidth(i.uv.y), 1e-5);

                // 보조 카메라가 찍은 배경 (분기 밖에서, 밉맵 없이 샘플링)
                float2 suv = i.grab.xy / max(i.grab.w, 1e-5);
                suv.y = lerp(suv.y, 1.0 - suv.y, _FlipY);
                float3 bg = tex2Dlod(_FlashSceneTex, float4(suv, 0.0, 0.0)).rgb;

                float mode = i.uv2.x;
                float4 c;
                if (mode < 0.5)
                {
                    c = needleFrag(i.uv, i.uv2.y, i.uv2.z, i.prm.x, i.prm.y, i.prm.z, i.prm.w, aaV * 2.0);
                }
                else if (mode < 1.5)
                {
                    c = shardFrag(i.uv, i.prm.x, i.prm.y, i.prm.z, i.prm.w, i.uv2.z, max(aaU, aaV) * 1.5);
                }
                else if (mode < 2.5)
                {
                    c = flashFrag(i.uv, saturate(i.uv2.y), max(aaU, aaV) * 2.0);
                    c *= i.prm.w;
                }
                else
                {
                    c = mirrorFrag(i.uv.x, aaU, bg, saturate(i.uv2.y), saturate(i.uv2.w), saturate(i.prm.z), i.uv2.z,
                                   i.prm.x, saturate(i.prm.w));
                }

                return fixed4(c.rgb, c.a) * _Alpha;
            }
            ENDCG
        }
    }

    Fallback Off
}
