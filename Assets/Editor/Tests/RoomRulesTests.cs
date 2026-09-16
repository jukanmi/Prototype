using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 방 크기와 그 비율. 지도 칸이 굴린 비율이 벽 · 스폰 지점 · 스폰 계산에 똑같이 걸려야 해서
    /// 셋이 공유하는 계산을 여기서 먼저 못 박는다. 원점이 아닌 방을 일부러 섞는다 —
    /// 원점 중심 방에서는 "중심 기준"과 "원점 기준"이 같은 답을 내서 틀려도 안 보인다.
    ///
    /// 계획서: docs/Room_Size_Plan.md (1단계)
    /// </summary>
    public class RoomRulesTests
    {
        private const float Eps = 0.001f;

        /// <summary>중심 (15, 2) · 반폭 5 · 반깊이 3. 원점에서 떨어뜨려 둔다.</summary>
        private static readonly RoomRect OffOrigin = new RoomRect(10f, 20f, -1f, 5f);

        private static void AssertRoom(in RoomRect r, float minX, float maxX, float minZ, float maxZ)
        {
            Assert.That(r.MinX, Is.EqualTo(minX).Within(Eps), "MinX");
            Assert.That(r.MaxX, Is.EqualTo(maxX).Within(Eps), "MaxX");
            Assert.That(r.MinZ, Is.EqualTo(minZ).Within(Eps), "MinZ");
            Assert.That(r.MaxZ, Is.EqualTo(maxZ).Within(Eps), "MaxZ");
        }

        // ── RoomRect ────────────────────────────────────

        [Test]
        public void RoomRect_SortsReversedEdges()
        {
            AssertRoom(new RoomRect(20f, 10f, 5f, -1f), 10f, 20f, -1f, 5f);
        }

        [Test]
        public void RoomRect_DerivesCenterAndHalves()
        {
            Assert.That(OffOrigin.CenterX, Is.EqualTo(15f).Within(Eps));
            Assert.That(OffOrigin.CenterZ, Is.EqualTo(2f).Within(Eps));
            Assert.That(OffOrigin.HalfX, Is.EqualTo(5f).Within(Eps));
            Assert.That(OffOrigin.HalfZ, Is.EqualTo(3f).Within(Eps));
        }

        /// <summary>기준 방은 지금 씬의 벽 규격 그대로다. 바뀌면 씬 다섯 개의 벽이 다 틀린 자리에 선다.</summary>
        [Test]
        public void Default_IsSixByThree_AtOrigin()
        {
            AssertRoom(RoomRect.Default, -6f, 6f, -3f, 3f);
        }

        // ── 비율 값 ─────────────────────────────────────

        [Test]
        public void Normalize_ZeroOrLess_MeansFull()
        {
            Assert.That(RoomRules.Normalize(0), Is.EqualTo(100));
            Assert.That(RoomRules.Normalize(-5), Is.EqualTo(100));
            Assert.That(RoomRules.Normalize(85), Is.EqualTo(85));
        }

        /// <summary>0은 "보정 없음"이라 저작 가능하다 — 필드가 없는 기존 레시피 애셋이 0으로 읽힌다.</summary>
        [TestCase(0, true)]
        [TestCase(80, true)]
        [TestCase(100, true)]
        [TestCase(125, true)]
        [TestCase(75, false)]
        [TestCase(130, false)]
        [TestCase(83, false)]
        [TestCase(-5, false)]
        public void IsValidPercent(int percent, bool expected)
        {
            Assert.That(RoomRules.IsValidPercent(percent), Is.EqualTo(expected));
        }

        [TestCase(0, 100)]
        [TestCase(50, 80)]
        [TestCase(200, 125)]
        [TestCase(92, 90)]
        [TestCase(93, 95)]
        [TestCase(110, 110)]
        public void ClampPercent_RoundsToStep_ThenClamps(int percent, int expected)
        {
            Assert.That(RoomRules.ClampPercent(percent), Is.EqualTo(expected));
        }

        /// <summary>규칙 상수끼리 어긋나면 레시피 검사와 굴림이 서로 다른 값을 허용한다.</summary>
        [Test]
        public void PercentBounds_AreOnStep_AndBracketFull()
        {
            Assert.That(RoomRules.MinPercent % RoomRules.Step, Is.Zero);
            Assert.That(RoomRules.MaxPercent % RoomRules.Step, Is.Zero);
            Assert.That(RoomRules.MinPercent, Is.LessThanOrEqualTo(RoomRules.FullPercent));
            Assert.That(RoomRules.MaxPercent, Is.GreaterThanOrEqualTo(RoomRules.FullPercent));
        }

        // ── Scale ───────────────────────────────────────

        [Test]
        public void Scale_Full_IsIdentity()
        {
            AssertRoom(RoomRules.Scale(OffOrigin, 100), 10f, 20f, -1f, 5f);
        }

        [Test]
        public void Scale_Zero_IsFull()
        {
            AssertRoom(RoomRules.Scale(OffOrigin, 0), 10f, 20f, -1f, 5f);
        }

        /// <summary>원점이 아니라 방 중심을 기준으로 줄어든다.</summary>
        [Test]
        public void Scale_KeepsCenter_OffOrigin()
        {
            RoomRect small = RoomRules.Scale(OffOrigin, 80);

            AssertRoom(small, 11f, 19f, -0.4f, 4.4f);
            Assert.That(small.CenterX, Is.EqualTo(OffOrigin.CenterX).Within(Eps));
            Assert.That(small.CenterZ, Is.EqualTo(OffOrigin.CenterZ).Within(Eps));
        }

        [Test]
        public void Scale_Default_AtBounds()
        {
            AssertRoom(RoomRules.Scale(RoomRect.Default, 80), -4.8f, 4.8f, -2.4f, 2.4f);
            AssertRoom(RoomRules.Scale(RoomRect.Default, 125), -7.5f, 7.5f, -3.75f, 3.75f);
        }

        // ── MovePoint ───────────────────────────────────

        /// <summary>방 안 상대 위치를 그대로 옮긴다. 높이는 손대지 않는다.</summary>
        [Test]
        public void MovePoint_KeepsRelativePosition_AndHeight()
        {
            RoomRect small = RoomRules.Scale(OffOrigin, 80);

            // 상대 위치 x = (19−15)/5 = 0.8, z = (4−2)/3 = 2/3
            Vector3 moved = RoomRules.MovePoint(new Vector3(19f, 2f, 4f), OffOrigin, small);

            Assert.That(moved.x, Is.EqualTo(15f + 0.8f * 4f).Within(Eps));
            Assert.That(moved.y, Is.EqualTo(2f).Within(Eps));
            Assert.That(moved.z, Is.EqualTo(2f + 2f / 3f * 2.4f).Within(Eps));
        }

        [Test]
        public void MovePoint_Center_StaysCenter()
        {
            RoomRect big = RoomRules.Scale(OffOrigin, 125);
            Vector3 moved = RoomRules.MovePoint(new Vector3(15f, 0f, 2f), OffOrigin, big);

            Assert.That(moved.x, Is.EqualTo(15f).Within(Eps));
            Assert.That(moved.z, Is.EqualTo(2f).Within(Eps));
        }

        /// <summary>벽 위 점은 벽 위에 남는다 — 스폰 지점이 벽 밖으로 새지 않는다.</summary>
        [Test]
        public void MovePoint_OnEdge_StaysOnEdge()
        {
            RoomRect small = RoomRules.Scale(OffOrigin, 80);
            Vector3 moved = RoomRules.MovePoint(new Vector3(OffOrigin.MaxX, 0f, OffOrigin.MinZ), OffOrigin, small);

            Assert.That(moved.x, Is.EqualTo(small.MaxX).Within(Eps));
            Assert.That(moved.z, Is.EqualTo(small.MinZ).Within(Eps));
        }

        [Test]
        public void MovePoint_FromEmptyRoom_GoesToCenter()
        {
            var empty = new RoomRect(3f, 3f, 1f, 1f);
            Vector3 moved = RoomRules.MovePoint(new Vector3(9f, 1f, 9f), empty, OffOrigin);

            Assert.That(moved.x, Is.EqualTo(OffOrigin.CenterX).Within(Eps));
            Assert.That(moved.y, Is.EqualTo(1f).Within(Eps));
            Assert.That(moved.z, Is.EqualTo(OffOrigin.CenterZ).Within(Eps));
        }

        // ── WallFor ─────────────────────────────────────

        /// <summary>
        /// 기준 방에서 지금 씬의 벽 규격이 그대로 나온다(Stage_01: 옆벽 x ±6.25 · size (0.5, 4, 6),
        /// 앞뒤 벽 z ±3.25 · size (13, 4, 0.5)). 이게 맞아야 100% 방에서 벽이 한 치도 안 움직인다.
        /// </summary>
        [Test]
        public void WallFor_Default_MatchesStage01Walls()
        {
            WallPose left = RoomRules.WallFor(RoomSide.Left, RoomRect.Default, 0.5f);
            WallPose right = RoomRules.WallFor(RoomSide.Right, RoomRect.Default, 0.5f);
            WallPose near = RoomRules.WallFor(RoomSide.Near, RoomRect.Default, 0.5f);
            WallPose far = RoomRules.WallFor(RoomSide.Far, RoomRect.Default, 0.5f);

            Assert.That(left.Center.x, Is.EqualTo(-6.25f).Within(Eps));
            Assert.That(right.Center.x, Is.EqualTo(6.25f).Within(Eps));
            Assert.That(near.Center.z, Is.EqualTo(-3.25f).Within(Eps));
            Assert.That(far.Center.z, Is.EqualTo(3.25f).Within(Eps));

            Assert.That(left.ColliderSize(4f), Is.EqualTo(new Vector3(0.5f, 4f, 6f)));
            Assert.That(near.ColliderSize(4f), Is.EqualTo(new Vector3(13f, 4f, 0.5f)));
        }

        /// <summary>어느 크기 · 어느 자리의 방이든 벽의 <b>안쪽 면</b>이 방 변에 온다.</summary>
        [TestCase(80)]
        [TestCase(100)]
        [TestCase(125)]
        public void WallFor_InnerFaceOnRoomEdge_OffOrigin(int percent)
        {
            RoomRect room = RoomRules.Scale(OffOrigin, percent);
            const float t = 0.5f;

            WallPose left = RoomRules.WallFor(RoomSide.Left, room, t);
            WallPose right = RoomRules.WallFor(RoomSide.Right, room, t);
            WallPose near = RoomRules.WallFor(RoomSide.Near, room, t);
            WallPose far = RoomRules.WallFor(RoomSide.Far, room, t);

            Assert.That(left.Center.x + t * 0.5f, Is.EqualTo(room.MinX).Within(Eps), "Left");
            Assert.That(right.Center.x - t * 0.5f, Is.EqualTo(room.MaxX).Within(Eps), "Right");
            Assert.That(near.Center.z + t * 0.5f, Is.EqualTo(room.MinZ).Within(Eps), "Near");
            Assert.That(far.Center.z - t * 0.5f, Is.EqualTo(room.MaxZ).Within(Eps), "Far");

            // 옆벽은 방 깊이의 가운데, 앞뒤 벽은 방 폭의 가운데에 선다
            Assert.That(left.Center.z, Is.EqualTo(room.CenterZ).Within(Eps));
            Assert.That(near.Center.x, Is.EqualTo(room.CenterX).Within(Eps));

            Assert.That(left.AlongX, Is.False);
            Assert.That(near.AlongX, Is.True);
            Assert.That(left.Length, Is.EqualTo(room.HalfZ * 2f).Within(Eps));
            Assert.That(near.Length, Is.EqualTo(room.HalfX * 2f + t * 2f).Within(Eps), "모서리를 덮는다");
        }

        [Test]
        public void WallFor_NegativeThickness_IsZero()
        {
            WallPose left = RoomRules.WallFor(RoomSide.Left, RoomRect.Default, -1f);

            Assert.That(left.Thickness, Is.Zero);
            Assert.That(left.Center.x, Is.EqualTo(-6f).Within(Eps));
        }

        // ── StretchScale ────────────────────────────────

        /// <summary>눕힌 바닥(로컬 x · y가 월드 x · z)은 두 축이 다 줄고, 두께 축은 그대로다.</summary>
        [Test]
        public void StretchScale_LyingFloor_ShrinksBothFloorAxes()
        {
            Vector3 s = RoomRules.StretchScale(new Vector3(12f, 6f, 1f), Quaternion.Euler(90f, 0f, 0f), 80);

            Assert.That(s.x, Is.EqualTo(9.6f).Within(Eps));
            Assert.That(s.y, Is.EqualTo(4.8f).Within(Eps));
            Assert.That(s.z, Is.EqualTo(1f).Within(Eps));
        }

        /// <summary>세운 그림(로컬 y가 월드 높이)은 높이가 그대로다. 방이 좁아진다고 벽이 낮아지면 안 된다.</summary>
        [Test]
        public void StretchScale_StandingBackdrop_KeepsHeight()
        {
            Vector3 s = RoomRules.StretchScale(new Vector3(12f, 4f, 1f), Quaternion.identity, 80);

            Assert.That(s.x, Is.EqualTo(9.6f).Within(Eps));
            Assert.That(s.y, Is.EqualTo(4f).Within(Eps));
            Assert.That(s.z, Is.EqualTo(0.8f).Within(Eps));
        }

        /// <summary>Y축으로 돌려도 수평 축은 수평이다. X와 Z에 같은 비율이라 어느 방향이든 같다.</summary>
        [Test]
        public void StretchScale_YawDoesNotMatter()
        {
            Vector3 s = RoomRules.StretchScale(new Vector3(12f, 4f, 1f), Quaternion.Euler(0f, 37f, 0f), 125);

            Assert.That(s.x, Is.EqualTo(15f).Within(Eps));
            Assert.That(s.y, Is.EqualTo(4f).Within(Eps));
            Assert.That(s.z, Is.EqualTo(1.25f).Within(Eps));
        }

        /// <summary>기울어진 축은 바닥에 비친 길이만큼만 비율을 받는다. 45°면 약 0.707만큼.</summary>
        [Test]
        public void StretchScale_TiltedAxis_TakesPartialFactor()
        {
            Vector3 s = RoomRules.StretchScale(Vector3.one, Quaternion.Euler(45f, 0f, 0f), 80);

            float partial = Mathf.Lerp(1f, 0.8f, Mathf.Sqrt(0.5f));
            Assert.That(s.x, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(s.y, Is.EqualTo(partial).Within(Eps));
            Assert.That(s.z, Is.EqualTo(partial).Within(Eps));
        }

        [TestCase(100)]
        [TestCase(0)]
        public void StretchScale_Full_IsIdentity(int percent)
        {
            Vector3 s = RoomRules.StretchScale(new Vector3(12f, 6f, 1f), Quaternion.Euler(90f, 0f, 0f), percent);

            Assert.That(s.x, Is.EqualTo(12f).Within(Eps));
            Assert.That(s.y, Is.EqualTo(6f).Within(Eps));
            Assert.That(s.z, Is.EqualTo(1f).Within(Eps));
        }

        // ── Fits ────────────────────────────────────────

        /// <summary>규칙이 허용하는 양끝 비율의 기준 방은 반드시 쓸 수 있어야 한다 — 상수끼리의 결속.</summary>
        [Test]
        public void Fits_DefaultRoom_AcrossWholePercentRange()
        {
            for (int p = RoomRules.MinPercent; p <= RoomRules.MaxPercent; p += RoomRules.Step)
                Assert.That(RoomRules.Fits(RoomRules.Scale(RoomRect.Default, p)), Is.True, $"{p}%");
        }

        /// <summary>130%면 반깊이 3.9 — 뒷벽 윗부분이 화면 밖이다.</summary>
        [Test]
        public void Fits_TooDeep_FailsCamera()
        {
            RoomRect deep = RoomRules.Scale(RoomRect.Default, 130);

            Assert.That(RoomRules.IsLargeEnough(deep), Is.True);
            Assert.That(RoomRules.FitsCamera(deep), Is.False);
            Assert.That(RoomRules.Fits(deep), Is.False);
        }

        [Test]
        public void Fits_TooWide_FailsCamera()
        {
            var wide = new RoomRect(-9f, 9f, -3f, 3f);
            Assert.That(RoomRules.FitsCamera(wide), Is.False);
        }

        /// <summary>반깊이 2면 소환 가능 깊이 ±1.4 — 마법사가 플레이어 줄을 1.5만큼 피할 자리가 없다.</summary>
        [Test]
        public void IsLargeEnough_TooShallow_Fails()
        {
            var shallow = new RoomRect(-6f, 6f, -2f, 2f);

            Assert.That(RoomRules.IsLargeEnough(shallow), Is.False);
            Assert.That(RoomRules.Fits(shallow), Is.False);
        }

        [Test]
        public void IsLargeEnough_ExactBoundary_Passes()
        {
            float half = WaveSpawnPlanner.DepthInset + WaveSpawnPlanner.RangedLaneGap;
            var edge = new RoomRect(-6f, 6f, -half, half);

            Assert.That(RoomRules.IsLargeEnough(edge), Is.True);
        }
    }
}
