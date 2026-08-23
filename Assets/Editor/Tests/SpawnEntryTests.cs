using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 진입 연출. 걸어 들어가 자리를 잡고, 잠시 서 있다가 AI에 몸을 넘긴다.
    ///
    /// 이게 <b>기획의 핵심 디테일</b>이다 — 돌진전사가 화면 가장자리에서 곧장 꿰뚫고 들어오면
    /// 반응할 시간이 아니라 반응할 <i>정보</i>가 없어서 불합리하게 느껴진다.
    ///
    /// 두 번째로 중요한 건 <b>걷기 제한 시간</b>이다. 정착 지점에 먼저 온 적이 서 있으면
    /// 뒤에 온 적은 영원히 도착하지 못하고, 그 적은 AI가 안 깨어난 채로 서 있는데
    /// 스테이지는 그 적이 안 죽어서 끝나지 않는다.
    /// </summary>
    public class SpawnEntryTests
    {
        private static readonly Vector3 Target = new Vector3(3f, 0f, 1f);

        // ── 걷기 ────────────────────────────────────────

        [Test]
        public void StartsWalking_TowardTheTarget()
        {
            var entry = new SpawnEntry(Target, 1f);

            Vector3 dir = entry.Tick(new Vector3(5.4f, 0f, 1f), 0.1f);

            Assert.That(entry.Current, Is.EqualTo(SpawnEntry.Phase.Walking));
            Assert.That(dir.x, Is.LessThan(0f), "안쪽(-x)으로 걸어야 한다");
            Assert.That(dir.magnitude, Is.EqualTo(1f).Within(0.001f), "정규화되지 않은 방향은 속도로 새어 나간다");
        }

        /// <summary>높이는 무시한다 — 맞고 떠 있는 동안에도 목표 방향은 바닥 기준이다.</summary>
        [Test]
        public void Height_DoesNotBendTheDirection()
        {
            var entry = new SpawnEntry(Target, 0f);

            Vector3 dir = entry.Tick(new Vector3(5.4f, 2.5f, 1f), 0.1f);

            Assert.That(dir.y, Is.Zero);
        }

        [Test]
        public void Arriving_EndsTheWalk()
        {
            var entry = new SpawnEntry(Target, 0f);

            Vector3 dir = entry.Tick(Target, 0.1f);

            Assert.That(entry.Current, Is.EqualTo(SpawnEntry.Phase.Done));
            Assert.That(dir, Is.EqualTo(Vector3.zero));
        }

        // ── 선딜레이 ────────────────────────────────────

        /// <summary>도착했다고 바로 덤비지 않는다. 이 구간이 "읽을 시간"이다.</summary>
        [Test]
        public void Arriving_WithHold_StandsStillFirst()
        {
            var entry = new SpawnEntry(Target, 1.2f);

            Vector3 dir = entry.Tick(Target, 0.1f);

            Assert.That(entry.Current, Is.EqualTo(SpawnEntry.Phase.Holding));
            Assert.That(dir, Is.EqualTo(Vector3.zero), "서 있어야 할 구간에 걸어간다");
            Assert.That(entry.IsActive, Is.True, "여기서 AI가 깨어나면 선딜레이가 없는 것과 같다");
        }

        [Test]
        public void Hold_RunsOutAndHandsOverTheBody()
        {
            var entry = new SpawnEntry(Target, 1f);
            entry.Tick(Target, 0.1f);

            for (int i = 0; i < 9; i++)
            {
                entry.Tick(Target, 0.1f);
                Assert.That(entry.Current, Is.EqualTo(SpawnEntry.Phase.Holding), $"{i}번째 프레임에 일찍 깨어났다");
            }

            entry.Tick(Target, 0.2f);

            Assert.That(entry.Current, Is.EqualTo(SpawnEntry.Phase.Done));
            Assert.That(entry.IsActive, Is.False);
        }

        [Test]
        public void ZeroHold_SkipsStraightToDone()
        {
            var entry = new SpawnEntry(Target, 0f);
            entry.Tick(Target, 0.016f);

            Assert.That(entry.Current, Is.EqualTo(SpawnEntry.Phase.Done));
        }

        // ── 막혔을 때 ───────────────────────────────────

        /// <summary>
        /// 자리에 먼저 온 적이 서 있으면 뒤에 온 적은 영영 도착하지 못한다.
        /// 제한 시간이 없으면 그 적은 <b>가만히 서 있는 불사신</b>이 되고 스테이지가 안 끝난다.
        /// </summary>
        [Test]
        public void BlockedWalk_GivesUpAfterTheTimeout()
        {
            var entry = new SpawnEntry(Target, 0f, walkTimeout: 1f);
            var stuck = new Vector3(5.4f, 0f, 1f);

            for (int i = 0; i < 9; i++)
            {
                entry.Tick(stuck, 0.1f);
                Assert.That(entry.Current, Is.EqualTo(SpawnEntry.Phase.Walking), $"{i}번째 프레임");
            }

            entry.Tick(stuck, 0.2f);

            Assert.That(entry.Current, Is.EqualTo(SpawnEntry.Phase.Done), "막힌 적이 영원히 걸어간다");
        }

        /// <summary>제한 시간에 걸려도 선딜레이는 지켜진다 — 나타나자마자 돌진하면 안 된다.</summary>
        [Test]
        public void Timeout_StillHonorsTheHold()
        {
            var entry = new SpawnEntry(Target, 1.2f, walkTimeout: 0.5f);
            var stuck = new Vector3(5.4f, 0f, 1f);

            for (int i = 0; i < 6; i++) entry.Tick(stuck, 0.1f);

            Assert.That(entry.Current, Is.EqualTo(SpawnEntry.Phase.Holding));
        }

        // ── 강제 종료 ───────────────────────────────────

        [Test]
        public void Finish_EndsImmediately_AndStaysDone()
        {
            var entry = new SpawnEntry(Target, 5f);
            entry.Finish();

            Assert.That(entry.IsActive, Is.False);
            Assert.That(entry.Tick(Vector3.zero, 1f), Is.EqualTo(Vector3.zero));
            Assert.That(entry.Current, Is.EqualTo(SpawnEntry.Phase.Done));
        }
    }
}
