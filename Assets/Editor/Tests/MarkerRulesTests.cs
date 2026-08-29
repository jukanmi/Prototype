using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 표식을 화면 가장자리에 물리는 규칙.
    ///
    /// <b>안 물리면 이 기능이 통째로 무의미하다</b> — 화면 밖 좌표에 그대로 그리면
    /// 표식도 같이 안 보이고, 풀려던 문제("다른 동료가 어디 있는지 모르겠다")가 그대로 남는다.
    /// </summary>
    public class MarkerRulesTests
    {
        private const float Eps = 0.0001f;

        private const float CamX = 0f;
        private const float Half = 8f;

        /// <summary>가장자리에서 안쪽으로 물린 실제 경계.</summary>
        private const float Edge = Half - MarkerRules.EdgeInset;

        // ── 물림 ────────────────────────────────────────

        /// <summary>화면 밖 좌표는 전부 화면 안으로 들어와야 한다.</summary>
        [Test]
        public void OffscreenPoints_ClampIntoView()
        {
            foreach (float x in new[] { -100f, -12f, 12f, 100f })
                Assert.That(EntranceRules.IsOnScreen(MarkerRules.ClampToEdge(x, CamX, Half), CamX, Half),
                            Is.True, $"x={x} 표식이 화면 밖이다");
        }

        [Test]
        public void ClampToEdge_LeavesOnscreenPointsAlone()
        {
            Assert.That(MarkerRules.ClampToEdge(0f, CamX, Half), Is.EqualTo(0f).Within(Eps));
            Assert.That(MarkerRules.ClampToEdge(3f, CamX, Half), Is.EqualTo(3f).Within(Eps));
            Assert.That(MarkerRules.ClampToEdge(-3f, CamX, Half), Is.EqualTo(-3f).Within(Eps));
        }

        [Test]
        public void ClampToEdge_PinsBothSides()
        {
            Assert.That(MarkerRules.ClampToEdge(100f, CamX, Half), Is.EqualTo(Edge).Within(Eps));
            Assert.That(MarkerRules.ClampToEdge(-100f, CamX, Half), Is.EqualTo(-Edge).Within(Eps));
        }

        /// <summary>카메라가 옮겨 가면 가장자리도 함께 간다.</summary>
        [Test]
        public void ClampToEdge_FollowsTheCamera()
        {
            Assert.That(MarkerRules.ClampToEdge(1000f, 40f, Half), Is.EqualTo(40f + Edge).Within(Eps));
        }

        /// <summary>깊이는 손대지 않는다 — 같은 쪽 표식 둘을 갈라 보이게 하는 것이 깊이뿐이다.</summary>
        [Test]
        public void Anchor_KeepsDepth()
        {
            var seat = new Vector3(100f, 0f, -1.2f);
            Vector3 a = MarkerRules.Anchor(seat, CamX, Half);

            Assert.That(a.z, Is.EqualTo(seat.z).Within(Eps));
            Assert.That(a.x, Is.EqualTo(Edge).Within(Eps));
        }

        // ── 방향 ────────────────────────────────────────

        [Test]
        public void OffscreenSide_ReadsTheDirection()
        {
            Assert.That(MarkerRules.OffscreenSide(0f, CamX, Half), Is.EqualTo(0));
            Assert.That(MarkerRules.OffscreenSide(100f, CamX, Half), Is.EqualTo(1));
            Assert.That(MarkerRules.OffscreenSide(-100f, CamX, Half), Is.EqualTo(-1));
        }

        [Test]
        public void PointerAngle_LiesFlatWhenOffscreen()
        {
            Assert.That(MarkerRules.PointerAngle(0), Is.EqualTo(0f).Within(Eps));
            Assert.That(MarkerRules.PointerAngle(1), Is.EqualTo(90f).Within(Eps));
            Assert.That(MarkerRules.PointerAngle(-1), Is.EqualTo(-90f).Within(Eps));
        }

        // ── 거리 ────────────────────────────────────────

        [Test]
        public void OffscreenDistance_IsZeroOnscreen()
        {
            Assert.That(MarkerRules.OffscreenDistance(0f, CamX, Half), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void OffscreenDistance_GrowsWithTheGap()
        {
            float near = MarkerRules.OffscreenDistance(Edge + 1f, CamX, Half);
            float far = MarkerRules.OffscreenDistance(Edge + 5f, CamX, Half);

            Assert.That(near, Is.EqualTo(1f).Within(Eps));
            Assert.That(far, Is.GreaterThan(near));
        }

        // ── 안전 ────────────────────────────────────────

        /// <summary>여백이 화면보다 넓어도 뒤집히지 않는다. 아주 좁은 게임 뷰에서 실제로 생긴다.</summary>
        [Test]
        public void ClampToEdge_SurvivesATinyScreen()
        {
            float x = MarkerRules.ClampToEdge(50f, CamX, 0.2f);

            Assert.That(x, Is.EqualTo(CamX).Within(Eps));
        }
    }
}
