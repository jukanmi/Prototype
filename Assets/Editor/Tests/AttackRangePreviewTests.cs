using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 바닥에 그리는 공격 범위의 <b>계산</b>만 검증한다. 그리기(LineRenderer · 벨트스크롤 접기)는
    /// 눈으로 보는 것 외에 확인할 방법이 없지만, 어느 자리를 얼마나 크게 그릴지는 순수 함수라
    /// 여기서 고정한다.
    ///
    /// 특히 전진 패턴(돌진베기)의 확장이 어긋나면 "표시 밖인데 맞았다"가 된다 —
    /// 화면에서 잡기 가장 어려운 종류의 거짓말이다.
    /// </summary>
    public class AttackRangePreviewTests
    {
        // 보스 광역 히트박스의 실제 수치(BossPrefabBuilder.WideHitboxSize/Pos)와 같은 모양.
        private static readonly Vector3 WideSize = new Vector3(3.6f, 2.4f, 3.2f);
        private static readonly Vector3 WideOffset = new Vector3(0f, 1.1f, 1.9f);

        private static readonly Vector3 Forward = Vector3.forward;

        private static AttackRangePreview Build(float advance, Vector3 facing = default)
        {
            return AttackRangePreview.FromBox(
                Vector3.zero,
                facing == default ? Forward : facing,
                WideOffset, WideSize, advance, 1f);
        }

        [Test]
        public void Stationary_UsesHitboxSizeAsIs()
        {
            AttackRangePreview r = Build(0f);

            Assert.That(r.halfWidth, Is.EqualTo(1.8f).Within(0.0001f));
            Assert.That(r.halfLength, Is.EqualTo(1.6f).Within(0.0001f));
        }

        [Test]
        public void Stationary_CenterSitsInFrontOfCaster()
        {
            AttackRangePreview r = Build(0f);

            Assert.That(r.center.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(r.center.z, Is.EqualTo(1.9f).Within(0.0001f));
        }

        /// <summary>바닥 표시다. 히트박스가 몸통 높이에 있어도 y는 0이어야 한다.</summary>
        [Test]
        public void Center_IsFlattenedToGround()
        {
            AttackRangePreview r = AttackRangePreview.FromBox(
                new Vector3(0f, 5f, 0f), Forward, WideOffset, WideSize, 0f, 1f);

            Assert.That(r.center.y, Is.EqualTo(0f));
        }

        [Test]
        public void Advancing_ExtendsByHalfTheTravel()
        {
            const float advance = 6.75f;   // 돌진베기: advanceSpeed 15 x active 0.45

            AttackRangePreview r = Build(advance);

            Assert.That(r.halfLength, Is.EqualTo(1.6f + advance * 0.5f).Within(0.0001f));
            Assert.That(r.halfWidth, Is.EqualTo(1.8f).Within(0.0001f), "폭은 안 변한다");
        }

        /// <summary>늘어난 만큼 중심도 밀려야 뒤쪽 경계가 제자리에 남는다.</summary>
        [Test]
        public void Advancing_KeepsBackEdgeInPlace()
        {
            float stationaryBack = Build(0f).center.z - Build(0f).halfLength;

            AttackRangePreview r = Build(6.75f);
            float advancingBack = r.center.z - r.halfLength;

            Assert.That(advancingBack, Is.EqualTo(stationaryBack).Within(0.0001f));
        }

        [Test]
        public void Advancing_FrontEdgeReachesTravelEnd()
        {
            const float advance = 6.75f;

            AttackRangePreview r = Build(advance);

            Assert.That(r.center.z + r.halfLength, Is.EqualTo(1.9f + 1.6f + advance).Within(0.0001f));
        }

        /// <summary>보스가 어디를 보든 상자는 그 앞에 놓여야 한다.</summary>
        [Test]
        public void Facing_RotatesTheFootprint()
        {
            AttackRangePreview r = Build(0f, Vector3.right);

            Assert.That(r.center.x, Is.EqualTo(1.9f).Within(0.0001f));
            Assert.That(r.center.z, Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>좌우 오프셋은 정면이 아니라 오른쪽 축을 따라간다.</summary>
        [Test]
        public void SideOffset_FollowsRightAxis()
        {
            AttackRangePreview r = AttackRangePreview.FromBox(
                Vector3.zero, Vector3.right,
                new Vector3(0.5f, 0f, 0f), WideSize, 0f, 1f);

            // facing이 +X면 오른쪽 축은 -Z다(LookRotation과 같은 규약).
            Assert.That(r.center.z, Is.EqualTo(-0.5f).Within(0.0001f));
        }

        [Test]
        public void Facing_IsNormalized()
        {
            AttackRangePreview r = Build(0f, Vector3.right * 9f);

            Assert.That(r.facing.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        /// <summary>정지 직후 등으로 방향이 0이어도 크기가 터지면 안 된다.</summary>
        [Test]
        public void ZeroFacing_FallsBackToForward()
        {
            AttackRangePreview r = AttackRangePreview.FromBox(
                Vector3.zero, Vector3.zero, WideOffset, WideSize, 0f, 1f);

            Assert.That(Vector3.Distance(r.facing, Vector3.forward), Is.LessThan(0.001f));
        }

        [Test]
        public void NegativeSize_ClampsToZero()
        {
            AttackRangePreview r = AttackRangePreview.FromBox(
                Vector3.zero, Forward, Vector3.zero, new Vector3(-2f, 0f, -2f), 0f, 1f);

            Assert.That(r.halfWidth, Is.EqualTo(0f));
            Assert.That(r.halfLength, Is.EqualTo(0f));
        }

        /// <summary>전진 거리가 음수인 패턴을 저작해도 상자가 뒤집히면 안 된다.</summary>
        [Test]
        public void NegativeAdvance_IsIgnored()
        {
            AttackRangePreview r = Build(-5f);

            Assert.That(r.halfLength, Is.EqualTo(1.6f).Within(0.0001f));
            Assert.That(r.center.z, Is.EqualTo(1.9f).Within(0.0001f));
        }

        [Test]
        public void Progress_IsClamped()
        {
            Assert.That(AttackRangePreview.FromBox(
                Vector3.zero, Forward, Vector3.zero, WideSize, 0f, 3f).progress, Is.EqualTo(1f));

            Assert.That(AttackRangePreview.FromBox(
                Vector3.zero, Forward, Vector3.zero, WideSize, 0f, -3f).progress, Is.EqualTo(0f));
        }

        // ── 히트박스 읽기 ───────────────────────────────

        /// <summary>
        /// 프리팹의 실제 배선을 흉내 낸다 — 히트박스는 시전자의 <b>자식</b>이고,
        /// 오프셋은 자식 transform과 BoxCollider.center에 나뉘어 들어 있다.
        /// 둘 중 하나만 읽으면 상자가 조용히 어긋난 자리에 그려진다.
        /// </summary>
        [Test]
        public void TryReadBox_CombinesChildOffsetAndColliderCenter()
        {
            var root = new GameObject("Boss");
            try
            {
                var child = new GameObject("WideHitbox");
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(0f, 1.1f, 1.5f);

                BoxCollider box = child.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0f, 0.4f);
                box.size = WideSize;

                var hitbox = child.AddComponent<Attack>();

                bool ok = AttackRangePreview.TryReadBox(
                    hitbox, root.transform, out Vector3 localOffset, out Vector3 size);

                Assert.That(ok, Is.True);
                Assert.That(localOffset.z, Is.EqualTo(1.9f).Within(0.0001f), "자식 위치 + 콜라이더 중심");
                Assert.That(size, Is.EqualTo(WideSize));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryReadBox_NullHitbox_ReturnsFalse()
        {
            var root = new GameObject("Boss");
            try
            {
                Assert.That(AttackRangePreview.TryReadBox(null, root.transform, out _, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ── 둘레 판정 (횡베기) ──────────────────────────
        // 원은 방향을 타지 않는다. 그 성질이 깨지면 "뒤로 돌아가면 안 맞는다"가 되어
        // 차징을 끝까지 기다릴 이유가 사라진다.

        [Test]
        public void Circle_IsFlaggedAsCircle()
        {
            AttackRangePreview r = AttackRangePreview.FromCircle(Vector3.zero, 3.2f, 1f);

            Assert.That(r.IsCircle, Is.True);
            Assert.That(r.radius, Is.EqualTo(3.2f).Within(0.0001f));
        }

        [Test]
        public void Box_IsNotFlaggedAsCircle()
        {
            Assert.That(Build(0f).IsCircle, Is.False);
        }

        [Test]
        public void Circle_CenterIsFlattenedToGround()
        {
            AttackRangePreview r = AttackRangePreview.FromCircle(new Vector3(2f, 5f, -3f), 3.2f, 1f);

            Assert.That(r.center.y, Is.EqualTo(0f));
            Assert.That(r.center.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(r.center.z, Is.EqualTo(-3f).Within(0.0001f));
        }

        [Test]
        public void Circle_NegativeRadius_ClampsToZero()
        {
            Assert.That(AttackRangePreview.FromCircle(Vector3.zero, -4f, 1f).radius, Is.EqualTo(0f));
        }

        [Test]
        public void Circle_ProgressIsClamped()
        {
            Assert.That(AttackRangePreview.FromCircle(Vector3.zero, 3.2f, 9f).progress, Is.EqualTo(1f));
            Assert.That(AttackRangePreview.FromCircle(Vector3.zero, 3.2f, -9f).progress, Is.EqualTo(0f));
        }

        /// <summary>
        /// 보스가 어디를 보든 같은 자리를 덮어야 한다. 정면을 받지 않는 것이 그 보장이다.
        /// </summary>
        [Test]
        public void TryReadSphere_IgnoresRotation()
        {
            var root = new GameObject("Boss");
            try
            {
                root.transform.position = new Vector3(4f, 0f, 1f);

                var child = new GameObject("RadialHitbox");
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(0f, 1.1f, 0f);

                SphereCollider sphere = child.AddComponent<SphereCollider>();
                sphere.radius = 3.2f;

                var hitbox = child.AddComponent<Attack>();

                root.transform.rotation = Quaternion.Euler(0f, 137f, 0f);

                bool ok = AttackRangePreview.TryReadSphere(hitbox, out Vector3 center, out float radius);

                Assert.That(ok, Is.True);
                Assert.That(radius, Is.EqualTo(3.2f).Within(0.0001f));
                Assert.That(center.x, Is.EqualTo(4f).Within(0.0001f), "돌아도 중심이 안 움직여야 한다");
                Assert.That(center.z, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>표시 반경과 판정 반경이 어긋나면 "표시 밖인데 맞았다"가 된다.</summary>
        [Test]
        public void TryReadSphere_FollowsLargestScaleAxis()
        {
            var root = new GameObject("Boss");
            try
            {
                root.transform.localScale = new Vector3(1f, 2f, 1f);

                var child = new GameObject("RadialHitbox");
                child.transform.SetParent(root.transform, false);

                SphereCollider sphere = child.AddComponent<SphereCollider>();
                sphere.radius = 3f;

                var hitbox = child.AddComponent<Attack>();

                AttackRangePreview.TryReadSphere(hitbox, out _, out float radius);

                Assert.That(radius, Is.EqualTo(6f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>상자 히트박스는 구 경로로 새면 안 된다 — 앞쪽 판정이 사방 판정이 된다.</summary>
        [Test]
        public void TryReadSphere_BoxHitbox_ReturnsFalse()
        {
            var root = new GameObject("Boss");
            try
            {
                var child = new GameObject("WideHitbox");
                child.transform.SetParent(root.transform, false);
                child.AddComponent<BoxCollider>();

                var hitbox = child.AddComponent<Attack>();

                Assert.That(AttackRangePreview.TryReadSphere(hitbox, out _, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
