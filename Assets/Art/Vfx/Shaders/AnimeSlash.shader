// 애니메풍 초승달 베기 이펙트 (텍스처 불필요, 전부 수식으로 그림)
// Built-in 파이프라인 / URP(2D Renderer 포함) 양쪽에서 동작하는 Unlit 셰이더.
// _Progress 를 0 -> 1 로 올리면 한 번 베는 애니메이션이 재생됩니다.
//
// 모양 잡는 법
//   - 쿼드 중심이 호의 중심. 쿼드를 납작하게(가로로 길게) 만들면 횡베기처럼 눕힌 타원이 됩니다.
//   - _ArcStart 에서 시작해 _ArcLength 만큼 쓸고 지나갑니다. 양수 = 반시계, 음수 = 시계 방향.
//     (오른쪽을 보는 캐릭터 기준: 0 = 정면, 90 = 머리 위, -90 = 발밑, 180 = 등 뒤)
//   - _LowerSquash 를 1 보다 작게 하면 호의 아래쪽 절반만 납작해집니다 (달걀 모양 궤적).
//     아래는 눕힌 타원으로 횡베기, 위는 크게 들어 올리는 궤적이 한 줄기로 끊김 없이 이어집니다.
//   - _StraightEnd 가 1 이면 베는 쪽 끝이 휘어지며 뾰족해지는 대신, 곧은 선으로 비스듬히 잘린 면이 됩니다.
//     이때는 사라질 때도 가늘어지지 않고, 꼬리부터 줄어들어 그 잘린 면이 마지막까지 남습니다.
//     _EndSlant 로 잘린 면의 기울기를 정합니다. 양수 = 바깥 날이 앞섬, 음수 = 안쪽 가장자리가 앞섬, 0 = 중심을 향하는 선.
Shader "VFX/AnimeSlash"
{
    Properties
    {
        _Progress ("Progress (0-1)", Range(0, 1)) = 0.3

        [Header(Color)]
        [HDR] _CoreColor ("Core Color (outer blade)", Color) = (1, 1, 1, 1)
        [HDR] _MidColor ("Mid Color", Color) = (0.66, 0.93, 1, 1)
        [HDR] _EdgeColor ("Edge Color (inner / tail)", Color) = (0.13, 0.50, 1, 1)
        _OutlineColor ("Outline Color", Color) = (0.03, 0.08, 0.30, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1

        [Header(Shape)]
        _ArcStart ("Arc Start Angle (deg)", Range(-180, 180)) = -75
        _ArcLength ("Arc Length (deg, negative = clockwise)", Range(-330, 330)) = 185
        _Radius ("Radius", Range(0.3, 0.92)) = 0.86
        _Thickness ("Thickness", Range(0.05, 0.8)) = 0.45
        _Spiral ("Spiral (start is closer)", Range(0, 0.4)) = 0.12
        _LowerSquash ("Lower Half Squash (1 = off)", Range(0.2, 1)) = 1
        _StraightEnd ("Straight End (0 = curved tip, 1 = straight cut)", Range(0, 1)) = 1
        _EndSlant ("End Slant (+ outer edge leads, - inner edge leads)", Range(-0.3, 0.3)) = 0.12
        _Outline ("Outline Width", Range(0, 0.06)) = 0

        [Header(Detail)]
        _WhiteRatio ("White Ratio", Range(0.1, 1)) = 0.42
        _Streak ("Streak Amount", Range(0, 1)) = 0.75
        _StreakFreq ("Streak Density", Range(4, 60)) = 22
        _Glow ("Glow", Range(0, 2)) = 0.6
        _Lines ("Accent Lines", Range(0, 1)) = 0
        _PixelGrid ("Pixel Grid XY (0 = off)", Vector) = (0, 0, 0, 0)
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
        Cull Off                     // 좌우 반전(scale.x = -1) 해도 보이도록
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
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _Progress;
            float4 _CoreColor, _MidColor, _EdgeColor, _OutlineColor;
            float _Alpha;
            float _ArcStart, _ArcLength, _Radius, _Thickness, _Spiral, _Outline, _LowerSquash, _StraightEnd, _EndSlant;
            float _WhiteRatio, _Streak, _StreakFreq, _Glow, _Lines;
            float4 _PixelGrid;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
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

            // s = a 부터 s = b 까지 이어지는, 양끝이 뾰족한 얇은 호
            float arcLine(float r, float s, float aa, float rad, float halfWidth, float a, float b)
            {
                float u = (s - a) / max(b - a, 1e-4);
                float taper = saturate(u * 4.0) * saturate((1.0 - u) * 4.0);
                float w = halfWidth * taper;
                return saturate((w - abs(r - rad)) / aa + 0.5) * step(1e-4, taper);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = saturate(_Progress);

                // 화면 픽셀 크기(안티에일리어싱 폭). 도트 모드에서는 계단 그대로 둠
                // 아래쪽 절반만 세로로 눌린 좌표계 (양옆에서 위쪽 절반과 매끄럽게 이어짐)
                float squash = clamp(_LowerSquash, 0.05, 1.0);
                float2 uv = i.uv;
                float2 p0 = uv * 2.0 - 1.0;
                p0.y = p0.y < 0.0 ? p0.y / squash : p0.y;
                float aa = max(fwidth(length(p0)), 1e-5);
                float soft = 0.04;
                if (_PixelGrid.x > 0.5)
                {
                    float2 grid = max(_PixelGrid.xy, float2(1.0, 1.0));
                    uv = (floor(uv * grid) + 0.5) / grid;
                    aa = 1e-4;
                    soft = 1e-3;
                }

                // 쿼드 중심 기준 -1..1 좌표, 극좌표
                float2 p = uv * 2.0 - 1.0;
                p.y = p.y < 0.0 ? p.y / squash : p.y;
                float r = length(p);

                // s: 호 위의 위치 (0 = 베기 시작점, 1 = 끝점). 이음매는 호의 반대편에 둠
                float arcLen = radians(_ArcLength);
                if (abs(arcLen) < 0.01) arcLen = 0.01;
                float midAng = radians(_ArcStart) + arcLen * 0.5;
                float sn, cs;
                sincos(-midAng, sn, cs);
                float2 q = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
                float s = atan2(q.y, q.x + 1e-6) / arcLen + 0.5;

                // ---- 타이밍: 머리가 빠르게 쓸고 지나가고, 꼬리가 뒤따라오며 사라짐
                float x1 = saturate(t / 0.40);
                float head = 1.0 - (1.0 - x1) * (1.0 - x1) * (1.0 - x1);
                float x2 = saturate((t - 0.12) / 0.88);
                float tail = x2 * x2;
                // 끝을 곧게 자를 때(hold = 1)는 사라질 때 두께를 거의 유지해서, 꼬리가 따라붙으며 짧아지는 동안 잘린 면이 끝까지 남게 함
                float hold = step(0.5, _StraightEnd);
                float wt = 1.0 - lerp(0.85, 0.35, hold) * smoothstep(0.45, 1.0, t);
                float wl0 = saturate((head - tail) / 0.55);
                float wl = lerp(pow(wl0, 1.2), pow(max(wl0, 1e-5), 0.25), hold * smoothstep(0.3, 0.6, t));

                float u = (s - tail) / max(head - tail, 1e-4);
                float inWin = step(0.0, u) * step(u, 1.0);
                float uc = saturate(u);

                // ---- 초승달 두께 프로파일 (양끝이 뾰족, 62% 지점이 가장 두꺼움)
                float prof = pow(max(uc, 1e-5), 1.5) * pow(max(1.0 - uc, 1e-5), 0.9) / 0.2044;
                float w = _Thickness * prof * wt * wl;
                float rout = _Radius * (1.0 - _Spiral * (1.0 - s));
                float d = rout - r;                  // 바깥 날에서 안쪽으로의 거리
                float v = d / max(w, 1e-4);          // 0 = 바깥 날, 1 = 안쪽 가장자리

                // ---- 호 방향으로 길게 늘어난 노이즈 = 붓질 같은 결
                float nz = vnoise(float2(s * 5.0 - t * 2.5, r * _StreakFreq));
                float nz2 = vnoise(float2(s * 11.0 - t * 4.0 + 7.3, r * _StreakFreq * 2.3));
                nz = nz * 0.65 + nz2 * 0.35;
                float ero = _Streak * smoothstep(0.25, 1.0, v) * (0.30 + 0.70 * (1.0 - uc)) * (0.3 + 0.7 * wl)
                          + 0.5 * smoothstep(0.55, 1.0, t) * smoothstep(0.1, 1.0, v) * (1.0 - 0.8 * hold * smoothstep(0.55, 0.8, uc));   // 잘린 면 쪽은 덜 깎임

                // ---- 곧게 잘린 끝: 베는 쪽 끝을 휘어진 뾰족한 모양으로 두지 않고,
                //      바깥 날 위의 한 점(u = uO)과 안쪽 가장자리 위의 한 점(u = uI)을 잇는 직선으로 잘라 냄.
                //      _EndSlant > 0 이면 바깥 날이, < 0 이면 안쪽 가장자리가 그만큼 앞서서 면이 비스듬해짐
                float uO = 0.90 - max(-_EndSlant, 0.0);
                float uI = 0.90 - max(_EndSlant, 0.0);
                float uKeep = min(uO, uI);
                float profI = pow(max(uI, 1e-5), 1.5) * pow(max(1.0 - uI, 1e-5), 0.9) / 0.2044;
                float sI = tail + uI * (head - tail);
                float sO = tail + uO * (head - tail);
                float thI = midAng + (sI - 0.5) * arcLen;
                float thO = midAng + (sO - 0.5) * arcLen;
                float rI = _Radius * (1.0 - _Spiral * (1.0 - sI)) - _Thickness * profI * wt * wl;
                float rO = _Radius * (1.0 - _Spiral * (1.0 - sO));
                float2 pI = rI * float2(cos(thI), sin(thI));
                float2 pO = rO * float2(cos(thO), sin(thO));
                float2 cutDir = pI - pO;
                float2 cutN = float2(-cutDir.y, cutDir.x) / max(length(cutDir), 1e-5);
                float thM = (thI + thO) * 0.5;
                float2 backTan = (arcLen >= 0.0 ? -1.0 : 1.0) * float2(-sin(thM), cos(thM));      // 꼬리 쪽을 향하는 접선
                cutN *= (dot(cutN, backTan) < 0.0) ? -1.0 : 1.0;                                   // 법선이 남길 쪽(꼬리 쪽)을 향하게
                float cutDist = dot(p - pO, cutN);
                float cutOn = hold;
                float cutK = lerp(1.0, (u <= uKeep) ? 1.0 : saturate(cutDist / aa + 0.5), cutOn);
                float cutG = lerp(1.0, (u <= uKeep) ? 1.0 : saturate(cutDist * 10.0 + 0.5), cutOn);

                float o = _Outline * saturate(prof * 6.0);
                float band = saturate((d + o) / aa + 0.5) * saturate((w + o - d) / aa + 0.5);
                float fillBand = saturate(d / aa + 0.5) * saturate((w - d) / aa + 0.5);
                float keep = saturate((nz - ero) / soft + 0.5);
                float alpha = band * keep * inWin * cutK;

                // ---- 툰 밴드: 처음엔 전부 흰색(섬광) -> 안쪽/꼬리부터 하늘색 -> 파랑 띠가 파고듦
                float vv = v + (nz - 0.5) * 0.18 + 0.25 * (1.0 - uc) * (1.0 - uc);
                float c1 = lerp(1.3, _WhiteRatio, smoothstep(0.0, 0.35, t)) * (1.0 - 0.6 * smoothstep(0.6, 1.0, t));
                float fv = max(aa / max(w, 1e-4), 1e-4);
                float k1 = saturate((vv - c1) / fv + 0.5);
                float k2 = saturate((vv - c1 - 0.20) / fv + 0.5);
                float k3 = saturate((vv - c1 - 0.40) / fv + 0.5);
                float3 mid2 = lerp(_MidColor.rgb, _EdgeColor.rgb, 0.55);
                float3 col = lerp(_CoreColor.rgb, _MidColor.rgb, k1);
                col = lerp(col, mid2, k2);
                col = lerp(col, _EdgeColor.rgb, k3);
                col = lerp(_OutlineColor.rgb, col, fillBand / max(band, 1e-4));

                // ---- 보조 스피드 라인 (바깥쪽 1개, 안쪽 1개) : _Lines 가 0 이면 없음
                float l1 = arcLine(r, s, aa, rout + 0.05, 0.010, lerp(tail, head, 0.25) - 0.02, head + 0.04);
                float l2 = arcLine(r, s, aa, rout - _Thickness * wt * wl * 0.95, 0.008, lerp(tail, head, 0.45), head - 0.05);
                float lineA = saturate(l1 + l2) * (1.0 - smoothstep(0.5, 0.9, t)) * _Lines;
                lineA *= step(-0.05, s) * step(s, 1.08);
                float3 lineCol = lerp(_MidColor.rgb, _CoreColor.rgb, 0.6);
                col = lerp(col, lineCol, lineA * (1.0 - alpha));
                alpha = saturate(alpha + lineA * (1.0 - alpha));

                // ---- 글로우 (가산)
                float gmask = smoothstep(0.0, 0.15, u) * (1.0 - smoothstep(0.9, 1.05, u));
                float g = exp(-abs(d - w * 0.35) * 9.0) * gmask * saturate(prof * 3.0)
                        * _Glow * (1.0 - smoothstep(0.3, 0.95, t)) * cutG;

                // 쿼드 가장자리에서 잘려 보이지 않도록 페이드
                float fade = 1.0 - smoothstep(0.93, 1.0, r);
                alpha *= fade;

                float3 rgb = col * alpha + _EdgeColor.rgb * (g * fade * 0.6);
                return fixed4(rgb * _Alpha, alpha * _Alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
