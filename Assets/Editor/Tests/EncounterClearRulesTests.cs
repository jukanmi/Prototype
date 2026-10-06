using NUnit.Framework;
using Prototype;

namespace Prototype.Tests
{
    /// <summary>
    /// 조우 종료 판정. <b>이 구조에서 가장 조용하게 터지는 부분</b>이라 순수 함수로 떼어 뒀다.
    ///
    /// 틀리는 방향이 둘인데 증상이 정반대다. 너무 일찍 끝내면 예약분이 다음 조우 위로 쏟아지고,
    /// 너무 늦게 끝내면 화면에 적이 하나도 없는데 스테이지가 안 넘어간다.
    /// 둘 다 화면만 봐서는 원인이 안 보인다.
    ///
    /// 예전에는 방과 아레나가 규칙을 따로 들었고, 세는 항목이 교집합 하나뿐이었다.
    /// 여기 있는 검사 중 절반은 아레나 쪽에서, 절반은 방 쪽에서 왔다 —
    /// <b>합쳤으니 양쪽 이유가 전부 살아 있어야 한다.</b>
    ///
    /// 계획서: docs/Stage_Encounter_Unification_Plan.md (3.3)
    /// </summary>
    public class EncounterClearRulesTests
    {
        /// <summary>다 잡고 더 나올 것도 없는 상태. 다른 검사의 기준점이다.</summary>
        private static EncounterCensus Done() => new EncounterCensus { spawned = 3 };

        // ── 남은 위협 ───────────────────────────────────

        [Test]
        public void Threats_CountsWhatIsStillComing()
        {
            var census = new EncounterCensus
            {
                pending = 2, entering = 1, fighting = 3, reinforcementsLeft = 4,
            };

            Assert.That(EncounterClearRules.Threats(in census), Is.EqualTo(10));
        }

        /// <summary>
        /// 이미 지나간 것은 위협이 아니다. 사망 연출을 세면 시체가 사라질 때까지 문이 안 열리고,
        /// 누적 마릿수나 소환 실패를 세면 조우가 영영 안 끝난다.
        /// </summary>
        [Test]
        public void Threats_IgnoresWhatIsAlreadyPast()
        {
            var census = new EncounterCensus { dying = 5, spawned = 9, spawnFailures = 4 };

            Assert.That(EncounterClearRules.Threats(in census), Is.Zero);
        }

        /// <summary>음수로 들어온 칸은 0으로 본다. 빼기 실수 하나가 판정을 뒤집으면 안 된다.</summary>
        [Test]
        public void Threats_ClampsNegatives()
        {
            var census = new EncounterCensus
            {
                pending = -5, entering = -1, fighting = -2, reinforcementsLeft = -1,
            };

            Assert.That(EncounterClearRules.Threats(in census), Is.Zero);
        }

        // ── 전멸 조건 ───────────────────────────────────

        [Test]
        public void Cleared_WhenNothingIsLeft()
        {
            Assert.That(EncounterClearRules.IsCleared(Done(), WaveAdvance.AllCleared), Is.True);
        }

        /// <summary>사망 연출만 남았으면 끝난 것이다. 안 그러면 문이 몇 초씩 늦게 열린다.</summary>
        [Test]
        public void Cleared_WhileBodiesArePlayingTheirDeath()
        {
            EncounterCensus census = Done();
            census.dying = 5;

            Assert.That(EncounterClearRules.IsCleared(in census, WaveAdvance.AllCleared), Is.True);
        }

        /// <summary>
        /// 예약분이 남았으면 끝난 게 아니다. 여기서 일찍 끝내면 그 뒤에 나온 적이
        /// 다음 조우 위로 쏟아진다.
        /// </summary>
        [Test]
        public void NotCleared_WhilePendingRemains()
        {
            EncounterCensus census = Done();
            census.pending = 1;

            Assert.That(EncounterClearRules.IsCleared(in census, WaveAdvance.AllCleared), Is.False);
        }

        /// <summary>
        /// <b>진입 중인 적도 위협이다.</b> 판정이 꺼져 있어 때릴 수도 맞을 수도 없지만
        /// 곧 싸울 몸이고, 안 세면 벽에서 걸어 나오는 도중에 문이 열린다.
        /// </summary>
        [Test]
        public void NotCleared_WhileSomeoneIsStillWalkingIn()
        {
            EncounterCensus census = Done();
            census.entering = 1;

            Assert.That(EncounterClearRules.IsCleared(in census, WaveAdvance.AllCleared), Is.False);
        }

        [Test]
        public void NotCleared_WhileAnyEnemyFights()
        {
            EncounterCensus census = Done();
            census.fighting = 1;

            Assert.That(EncounterClearRules.IsCleared(in census, WaveAdvance.AllCleared), Is.False);
        }

        /// <summary>지속 리젠이 남았으면 끝난 게 아니다. 잠깐 비는 순간마다 조우가 넘어간다.</summary>
        [Test]
        public void NotCleared_WhileReinforcementsRemain()
        {
            EncounterCensus census = Done();
            census.reinforcementsLeft = 2;

            Assert.That(EncounterClearRules.IsCleared(in census, WaveAdvance.AllCleared), Is.False);
        }

        // ── 아무도 안 나온 조우 ─────────────────────────

        /// <summary>
        /// 한 기도 안 나온 조우를 클리어로 치면 <b>배선이 어긋났을 때 그냥 지나간다.</b>
        /// 화면에는 아무 일도 안 일어나고 문이 열려서, 무엇이 빠졌는지 알 길이 없다.
        /// </summary>
        [Test]
        public void NotCleared_WhenNothingEverSpawned()
        {
            Assert.That(EncounterClearRules.IsCleared(new EncounterCensus(), WaveAdvance.AllCleared),
                        Is.False);
        }

        /// <summary>
        /// 다만 소환 자체가 실패했다면 기다려도 나올 것이 없다. 여기서 안 넘어가면
        /// 적이 하나도 없는 화면에서 스테이지가 영원히 멈춘다.
        /// </summary>
        [Test]
        public void Cleared_WhenEverySpawnFailed()
        {
            var census = new EncounterCensus { spawnFailures = 3 };

            Assert.That(EncounterClearRules.IsCleared(in census, WaveAdvance.AllCleared), Is.True);
        }

        /// <summary>소환이 일부만 실패해도 나온 적은 다 잡아야 한다.</summary>
        [Test]
        public void PartialFailure_StillWaitsForTheLiveOnes()
        {
            var census = new EncounterCensus { spawned = 2, spawnFailures = 1, fighting = 1 };

            Assert.That(EncounterClearRules.IsCleared(in census, WaveAdvance.AllCleared), Is.False);
        }

        // ── 조건 접기 ───────────────────────────────────

        /// <summary>
        /// 미구현 조건이 애셋에 박혀 있어도 전멸로 접는다.
        /// 안 접으면 그 조우에서 <b>스테이지가 영영 안 끝난다</b>.
        /// </summary>
        [Test]
        public void UnsupportedAdvance_BehavesLikeAllCleared()
        {
            Assert.That(EncounterClearRules.IsCleared(Done(), WaveAdvance.TargetKilled), Is.True);

            EncounterCensus busy = Done();
            busy.fighting = 1;
            Assert.That(EncounterClearRules.IsCleared(in busy, WaveAdvance.TargetKilled), Is.False);
        }

        /// <summary>정의되지 않은 값이 박혀 있어도 멈추지 않는다.</summary>
        [Test]
        public void GarbageAdvance_BehavesLikeAllCleared()
        {
            Assert.That(EncounterClearRules.IsCleared(Done(), (WaveAdvance)77), Is.True);
        }
    }
}
