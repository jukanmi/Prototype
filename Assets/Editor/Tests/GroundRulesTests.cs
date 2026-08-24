using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 발판 판정. 예전에는 바닥이 float 하나(무한 평면)라 그림을 벗어나도 그대로 서 있었다.
    /// 낙차를 넣으려면 "여기 발판이 있나"가 먼저 맞아야 한다.
    /// </summary>
    public class GroundRulesTests
    {
        /// <summary>방 바닥. x ±6, z ±3, 높이 0. 씬 빌더가 까는 것과 같은 크기다.</summary>
        private static GroundRect Room => new GroundRect(-6f, 6f, -3f, 3f, 0f);

        private static List<GroundRect> Plates(params GroundRect[] r) => new List<GroundRect>(r);

        // ── 기본 ────────────────────────────────────────

        [Test]
        public void NoPlates_HasNoGround()
        {
            Assert.That(GroundRules.TryHeightAt(Plates(), Vector3.zero, 0f, out _), Is.False);
        }

        [Test]
        public void NullList_DoesNotThrow()
        {
            Assert.That(GroundRules.TryHeightAt(null, Vector3.zero, 0f, out _), Is.False);
        }

        [Test]
        public void InsideThePlate_HasGround()
        {
            bool ok = GroundRules.TryHeightAt(Plates(Room), new Vector3(2f, 0f, 1f), 0f, out float y);

            Assert.That(ok, Is.True);
            Assert.That(y, Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary><b>이 테스트가 낙차의 전제다.</b> 판 밖이면 딛을 것이 없어야 한다.</summary>
        [Test]
        public void OutsideThePlate_HasNoGround()
        {
            Assert.That(GroundRules.Supports(Plates(Room), new Vector3(7f, 0f, 0f), 0f), Is.False,
                        "x가 판 밖");
            Assert.That(GroundRules.Supports(Plates(Room), new Vector3(0f, 0f, 4f), 0f), Is.False,
                        "z가 판 밖");
        }

        /// <summary>가장자리는 발판에 포함한다. 벽 안쪽 면과 판 끝이 같은 좌표라 여기서 새면 안 된다.</summary>
        [Test]
        public void ExactlyOnTheEdge_IsStillSupported()
        {
            Assert.That(GroundRules.Supports(Plates(Room), new Vector3(6f, 0f, 3f), 0f), Is.True);
            Assert.That(GroundRules.Supports(Plates(Room), new Vector3(-6f, 0f, -3f), 0f), Is.True);
        }

        [Test]
        public void Margin_WidensTheEdge()
        {
            var p = new Vector3(6.4f, 0f, 0f);

            Assert.That(GroundRules.Supports(Plates(Room), p, 0f), Is.False);
            Assert.That(GroundRules.Supports(Plates(Room), p, 0.5f), Is.True);
        }

        [Test]
        public void NegativeMargin_ShrinksTheEdge()
        {
            var p = new Vector3(5.8f, 0f, 0f);

            Assert.That(GroundRules.Supports(Plates(Room), p, 0f), Is.True);
            Assert.That(GroundRules.Supports(Plates(Room), p, -0.5f), Is.False);
        }

        // ── 여러 장 ─────────────────────────────────────

        /// <summary>통로 타일은 이음매에서 겹친다. 겹치면 위쪽 판이 이겨야 계단이 성립한다.</summary>
        [Test]
        public void OverlappingPlates_TakeTheHigherOne()
        {
            var low = new GroundRect(-2f, 2f, -2f, 2f, 0f);
            var high = new GroundRect(-1f, 1f, -1f, 1f, 1.5f);

            GroundRules.TryHeightAt(Plates(low, high), new Vector3(0f, 3f, 0f), 0f, out float y);

            Assert.That(y, Is.EqualTo(1.5f).Within(0.0001f));
        }

        /// <summary>순서에 기대면 안 된다 — 등록 순서는 씬 로드 순서라 우리가 못 정한다.</summary>
        [Test]
        public void OverlappingPlates_OrderDoesNotMatter()
        {
            var low = new GroundRect(-2f, 2f, -2f, 2f, 0f);
            var high = new GroundRect(-1f, 1f, -1f, 1f, 1.5f);
            var p = new Vector3(0f, 3f, 0f);

            GroundRules.TryHeightAt(Plates(low, high), p, 0f, out float a);
            GroundRules.TryHeightAt(Plates(high, low), p, 0f, out float b);

            Assert.That(a, Is.EqualTo(b).Within(0.0001f));
        }

        /// <summary>머리 위 판으로 순간이동하면 안 된다.</summary>
        [Test]
        public void PlateAboveTheHead_IsIgnored()
        {
            var floor = new GroundRect(-2f, 2f, -2f, 2f, 0f);
            var ceiling = new GroundRect(-2f, 2f, -2f, 2f, 5f);

            GroundRules.TryHeightAt(Plates(floor, ceiling), new Vector3(0f, 0f, 0f), 0f, out float y);

            Assert.That(y, Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>서 있는 판과 발 높이가 부동소수점으로 어긋나도 그 판을 잃으면 안 된다.</summary>
        [Test]
        public void StandingOnAPlate_SurvivesFloatingPointDrift()
        {
            var plate = new GroundRect(-2f, 2f, -2f, 2f, 0f);
            var p = new Vector3(0f, -0.0001f, 0f);

            Assert.That(GroundRules.TryHeightAt(Plates(plate), p, 0f, out float y), Is.True);
            Assert.That(y, Is.EqualTo(0f).Within(0.0001f));
        }

        // ── 만들기 ──────────────────────────────────────

        /// <summary>발판 크기를 그림에서 얻는다 — 그림과 판정이 갈라지지 않게.</summary>
        [Test]
        public void FromBounds_UsesTheTopSurface()
        {
            var b = new Bounds(new Vector3(1f, 0.5f, -2f), new Vector3(12f, 0.2f, 6f));

            GroundRect r = GroundRect.FromBounds(b);

            Assert.That(r.MinX, Is.EqualTo(-5f).Within(0.0001f));
            Assert.That(r.MaxX, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(r.MinZ, Is.EqualTo(-5f).Within(0.0001f));
            Assert.That(r.MaxZ, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(r.Y, Is.EqualTo(0.6f).Within(0.0001f), "윗면이 밟히는 높이다");
        }

        [Test]
        public void Constructor_NormalizesSwappedBounds()
        {
            var r = new GroundRect(6f, -6f, 3f, -3f, 0f);

            Assert.That(r.MinX, Is.EqualTo(-6f).Within(0.0001f));
            Assert.That(r.MaxX, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(r.Contains(0f, 0f), Is.True);
        }
    }
}
