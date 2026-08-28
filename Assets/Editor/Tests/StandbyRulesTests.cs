using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 대기 좌표. 세 곳이 이 값 하나를 공유한다 — 퇴장 목적지 · 등장 출발지 · 가장자리 표식.
    ///
    /// <b>화면에서 검증할 수 없는 값이다.</b> 정의상 화면 밖이라, 틀려도
    /// "표식이 좀 이상하네"로만 보이고 숫자는 어디에도 안 뜬다.
    /// </summary>
    public class StandbyRulesTests
    {
        private const float Eps = 0.0001f;

        private const float CamX = 0f;
        private const float Half = 8f;

        /// <summary>플레이어 + 동료 4명. 이 게임의 로스터 크기다.</summary>
        private const int RosterSize = 5;

        // ── 화면 밖 ─────────────────────────────────────

        /// <summary>이 파일의 존재 이유. 한 칸이라도 화면 안이면 "대기"라는 말이 거짓이 된다.</summary>
        [Test]
        public void EverySlot_SitsOffscreen()
        {
            for (int i = 0; i < RosterSize; i++)
            {
                Vector3 p = StandbyRules.Slot(i, CamX, Half);

                Assert.That(EntranceRules.IsOnScreen(p.x, CamX, Half), Is.False,
                            $"{i}번 칸이 화면 안이다 (x={p.x})");
            }
        }

        /// <summary>카메라가 옮겨 가면 대기 자리도 함께 간다. 안 따라오면 통로에서 밖이 안이 된다.</summary>
        [Test]
        public void Slots_FollowTheCamera()
        {
            const float moved = 40f;

            for (int i = 0; i < RosterSize; i++)
            {
                Vector3 here = StandbyRules.Slot(i, CamX, Half);
                Vector3 there = StandbyRules.Slot(i, moved, Half);

                Assert.That(there.x - here.x, Is.EqualTo(moved).Within(Eps), $"{i}번 칸");
                Assert.That(there.z, Is.EqualTo(here.z).Within(Eps), $"{i}번 칸 깊이는 안 변한다");
            }
        }

        /// <summary>화면이 넓어지면 그만큼 더 밖으로 물러난다.</summary>
        [Test]
        public void Slots_RespectTheScreenWidth()
        {
            Vector3 narrow = StandbyRules.Slot(0, CamX, 6f);
            Vector3 wide = StandbyRules.Slot(0, CamX, 12f);

            Assert.That(wide.x, Is.GreaterThan(narrow.x));
            Assert.That(EntranceRules.IsOnScreen(wide.x, CamX, 12f), Is.False);
        }

        // ── 배치 ────────────────────────────────────────

        [Test]
        public void Sides_Alternate()
        {
            Assert.That(StandbyRules.SideOf(0), Is.EqualTo(1));
            Assert.That(StandbyRules.SideOf(1), Is.EqualTo(-1));
            Assert.That(StandbyRules.SideOf(2), Is.EqualTo(1));
            Assert.That(StandbyRules.SideOf(3), Is.EqualTo(-1));
        }

        [Test]
        public void Slot_SignMatchesItsSide()
        {
            for (int i = 0; i < RosterSize; i++)
            {
                Vector3 p = StandbyRules.Slot(i, CamX, Half);
                Assert.That(Mathf.Sign(p.x), Is.EqualTo(StandbyRules.SideOf(i)), $"{i}번 칸");
            }
        }

        /// <summary>
        /// <b>겹치면 표식이 하나로 뭉친다.</b> 좌우로는 어차피 가장자리에 물리므로
        /// 같은 쪽 두 칸을 갈라 보이게 하는 것은 깊이뿐이다.
        /// </summary>
        [Test]
        public void SameSideSlots_SeparateInDepth()
        {
            for (int a = 0; a < RosterSize; a++)
            {
                for (int b = a + 1; b < RosterSize; b++)
                {
                    if (StandbyRules.SideOf(a) != StandbyRules.SideOf(b)) continue;

                    Assert.That(Mathf.Abs(StandbyRules.LaneOf(a) - StandbyRules.LaneOf(b)),
                                Is.GreaterThan(0.5f), $"{a}번과 {b}번 칸이 같은 줄에 겹친다");
                }
            }
        }

        /// <summary>어느 두 칸도 같은 점에 있지 않다.</summary>
        [Test]
        public void NoTwoSlots_ShareAPoint()
        {
            for (int a = 0; a < RosterSize; a++)
            {
                for (int b = a + 1; b < RosterSize; b++)
                {
                    float d = Vector3.Distance(StandbyRules.Slot(a, CamX, Half),
                                               StandbyRules.Slot(b, CamX, Half));

                    Assert.That(d, Is.GreaterThan(0.5f), $"{a}번과 {b}번 칸이 겹친다");
                }
            }
        }

        /// <summary>
        /// 깊이는 방 안이어야 한다. 밖으로 나가면 몸이 실제로 그리 날아갈 때
        /// 벽 너머에서 들어오게 되고, 표식은 화면 위아래로 잘린다.
        /// </summary>
        [Test]
        public void Lanes_StayInsideTheRoom()
        {
            for (int i = 0; i < RosterSize; i++)
                Assert.That(Mathf.Abs(StandbyRules.LaneOf(i)),
                            Is.LessThanOrEqualTo(WaveSpawnPlanner.MaxDepth), $"{i}번 칸");
        }

        // ── 안정성 ──────────────────────────────────────

        /// <summary>
        /// <b>칸은 사람에게 못박혀 있다.</b> 살아 있는 사람만 모아 다시 매기면
        /// 한 명이 죽을 때마다 나머지 표식이 우르르 자리를 바꿔, 유저가
        /// "왼쪽이 마법사"를 학습할 수 없게 된다.
        /// </summary>
        [Test]
        public void Slot_DependsOnlyOnItsIndex()
        {
            Vector3 first = StandbyRules.Slot(3, CamX, Half);
            Vector3 again = StandbyRules.Slot(3, CamX, Half);

            Assert.That(Vector3.Distance(first, again), Is.LessThan(Eps));
        }

        /// <summary>로스터에 빈 칸이 있어도 번호만으로 답이 나온다 — 부르는 쪽에 분기가 안 생긴다.</summary>
        [Test]
        public void Slot_AnswersForAnyIndex()
        {
            Assert.DoesNotThrow(() => StandbyRules.Slot(0, CamX, Half));
            Assert.DoesNotThrow(() => StandbyRules.Slot(99, CamX, Half));
            Assert.DoesNotThrow(() => StandbyRules.Slot(-1, CamX, Half));
        }
    }
}
