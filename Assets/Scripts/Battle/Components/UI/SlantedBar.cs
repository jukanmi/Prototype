using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 오른쪽 끝이 <b>비스듬히 꺾인</b> 막대 한 장. 좌측 하단 체력바 · 총공격 게이지가 쓴다.
    ///
    /// <code>
    /// ┌──────────────────┐
    /// │                 /   ← 윗변이 더 길다
    /// └────────────────┘
    /// </code>
    ///
    /// <see cref="Image"/>는 스프라이트 없이 사각형만 그린다. 꺾인 끝을 스프라이트로 만들면 채움이 줄 때
    /// 끝 모양이 같이 잘려 나가거나 늘어난다 — 그래서 모양을 직접 그리고, 채움은 그 모양을
    /// 세로선 <c>x = 채움 위치</c>로 잘라서 그린다. 꺾임 각은 높이에 비례(<see cref="SlantRatio"/>)라
    /// 굵기가 다른 막대끼리(체력 · 게이지 · 테두리 · 속)도 같은 각으로 나란히 선다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class SlantedBar : MaskableGraphic
    {
        [Tooltip("0~1. 왼쪽부터 이만큼만 그린다. 1이면 꺾인 끝까지 전부.")]
        [SerializeField, Range(0f, 1f)] private float fill = 1f;

        [Tooltip("꺾인 끝의 가로 길이 ÷ 높이. 0.5면 높이 30짜리 막대 끝이 15만큼 비스듬하다.")]
        [SerializeField, Min(0f)] private float slantRatio = 0.5f;

        public float Fill
        {
            get => fill;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(fill, value)) return;
                fill = value;
                SetVerticesDirty();
            }
        }

        public float SlantRatio
        {
            get => slantRatio;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(slantRatio, value)) return;
                slantRatio = value;
                SetVerticesDirty();
            }
        }

        private static readonly List<Vector2> Scratch = new List<Vector2>(8);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Shape(GetPixelAdjustedRect(), slantRatio, fill, Scratch);
            if (Scratch.Count < 3) return;

            Color32 c = color;
            for (int i = 0; i < Scratch.Count; i++)
                vh.AddVert(Scratch[i], c, Vector2.zero);

            // 볼록 다각형이라 첫 점에서 부채꼴로 쪼개면 된다.
            for (int i = 1; i < Scratch.Count - 1; i++)
                vh.AddTriangle(0, i, i + 1);
        }

        /// <summary>
        /// 그릴 다각형(반시계). 순수 함수 — 테스트가 씬 없이 모양을 본다.
        ///
        /// 모양은 <c>(좌하) → (우하 − 꺾임) → (우상) → (좌상)</c>의 사다리꼴이고,
        /// 채움은 그걸 <c>x ≤ xMin + fill·폭</c>으로 자른 것이다. 채움이 0이면 점이 3개 미만이다.
        /// </summary>
        public static void Shape(Rect r, float slantRatio, float fill, List<Vector2> into)
        {
            into.Clear();
            if (r.width <= 0f || r.height <= 0f || fill <= 0f) return;

            float slant = Mathf.Min(r.height * Mathf.Max(0f, slantRatio), r.width);
            float cut = r.xMin + Mathf.Clamp01(fill) * r.width;

            // 원 모양 네 점 — 반시계.
            var a = new Vector2(r.xMin, r.yMin);
            var b = new Vector2(r.xMax - slant, r.yMin);
            var c = new Vector2(r.xMax, r.yMax);
            var d = new Vector2(r.xMin, r.yMax);

            // 세로선 x = cut 으로 자른다(Sutherland–Hodgman, 반평면 하나).
            Clip(a, b, cut, into);
            Clip(b, c, cut, into);
            Clip(c, d, cut, into);
            Clip(d, a, cut, into);
        }

        /// <summary>변 p→q 하나를 x ≤ cut 쪽으로 잘라 남는 점을 붙인다.</summary>
        private static void Clip(Vector2 p, Vector2 q, float cut, List<Vector2> into)
        {
            bool pIn = p.x <= cut;
            bool qIn = q.x <= cut;

            if (pIn) into.Add(p);
            if (pIn == qIn) return;

            float t = (cut - p.x) / (q.x - p.x);
            into.Add(Vector2.Lerp(p, q, t));
        }
    }
}
