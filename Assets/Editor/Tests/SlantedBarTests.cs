using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 꺾인 막대의 모양(<see cref="SlantedBar.Shape"/>). 순수 함수라 씬 없이 본다.
    /// 폭 100 · 높이 20 · 꺾임 0.5 → 꺾인 끝 가로 길이 10. 아랫변은 90에서, 윗변은 100에서 끝난다.
    /// </summary>
    public class SlantedBarTests
    {
        private static readonly Rect R = new Rect(0f, 0f, 100f, 20f);
        private readonly List<Vector2> pts = new List<Vector2>();

        private float MaxX(float atY)
        {
            float best = float.MinValue;
            foreach (Vector2 p in pts)
                if (Mathf.Abs(p.y - atY) < 0.001f) best = Mathf.Max(best, p.x);
            return best;
        }

        [Test]
        public void Full_TopEdgeLongerThanBottom()
        {
            SlantedBar.Shape(R, 0.5f, 1f, pts);

            Assert.That(pts.Count, Is.EqualTo(4));
            Assert.That(MaxX(20f), Is.EqualTo(100f).Within(0.001f), "윗변은 끝까지 간다.");
            Assert.That(MaxX(0f), Is.EqualTo(90f).Within(0.001f), "아랫변은 꺾인 만큼 짧다.");
        }

        [Test]
        public void Empty_DrawsNothing()
        {
            SlantedBar.Shape(R, 0.5f, 0f, pts);
            Assert.That(pts.Count, Is.LessThan(3));
        }

        /// <summary>꺾인 끝에 못 미친 채움은 오른쪽이 곧게 잘린 사각형이다.</summary>
        [Test]
        public void Half_IsStraightCut()
        {
            SlantedBar.Shape(R, 0.5f, 0.5f, pts);

            Assert.That(pts.Count, Is.EqualTo(4));
            Assert.That(MaxX(0f), Is.EqualTo(50f).Within(0.001f));
            Assert.That(MaxX(20f), Is.EqualTo(50f).Within(0.001f));
        }

        /// <summary>꺾인 구간 안에서 잘리면 아랫변은 꺾인 끝에서 멈추고, 빗변 위 한 점이 생긴다.</summary>
        [Test]
        public void InsideSlant_FollowsTheSlope()
        {
            SlantedBar.Shape(R, 0.5f, 0.95f, pts);   // cut x = 95, 빗변 (90,0)→(100,20) 위 y = 10

            Assert.That(pts.Count, Is.EqualTo(5));
            Assert.That(MaxX(0f), Is.EqualTo(90f).Within(0.001f));
            Assert.That(MaxX(20f), Is.EqualTo(95f).Within(0.001f));
            Assert.That(MaxX(10f), Is.EqualTo(95f).Within(0.001f), "빗변 위 잘린 점이 없다.");
        }

        /// <summary>꺾임 각은 높이에 비례한다 — 굵기가 다른 막대도 같은 각으로 선다.</summary>
        [Test]
        public void SlantScalesWithHeight()
        {
            SlantedBar.Shape(new Rect(0f, 0f, 100f, 40f), 0.5f, 1f, pts);
            Assert.That(MaxX(0f), Is.EqualTo(80f).Within(0.001f));
        }
    }
}
