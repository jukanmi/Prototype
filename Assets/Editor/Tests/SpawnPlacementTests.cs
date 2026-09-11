using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 방과 아레나가 <b>같은 배치 구조체</b>를 쓴다. 그 합침이 지켜지는지 본다.
    ///
    /// 예전에는 둘이 갈라져 있었고, 그래서 소환 창구도 두 갈래였다.
    /// 땅속 등장 같은 새 연출이 한쪽에만 붙는 것이 그 구조의 대가였다.
    ///
    /// 계획서: docs/Stage_Encounter_Unification_Plan.md (3.2)
    /// </summary>
    public class SpawnPlacementTests
    {
        private const float Eps = 0.0001f;

        // ── 직렬화 값이 곧 규약이다 ──────────────────────

        /// <summary>
        /// <see cref="SpawnWall.None"/>은 <b>맨 뒤</b>여야 한다. 이 열거형은 애셋에 정수로
        /// 저장되므로 중간에 끼우면 이미 저작된 라운드의 벽이 통째로 밀린다.
        /// 저작 기본값은 여전히 정면이어야 한다.
        /// </summary>
        [Test]
        public void NoneIsTheLastWall()
        {
            Assert.That((int)SpawnWall.Front, Is.EqualTo(0), "저작 기본값이 바뀌었다");
            Assert.That((int)SpawnWall.Left, Is.EqualTo(1));
            Assert.That((int)SpawnWall.Right, Is.EqualTo(2));
            Assert.That((int)SpawnWall.Back, Is.EqualTo(3));
            Assert.That((int)SpawnWall.None, Is.EqualTo(4), "None 이 앞으로 끼어들었다");
        }

        // ── 방 배치 ─────────────────────────────────────

        /// <summary>방 안에서 나오는 적은 벽에서 걸어 나오지 않는다.</summary>
        [Test]
        public void RoomPlacements_AreNotFromAWall()
        {
            var entry = WaveSpawnEntry.Auto(EnemyRole.Melee);
            var point = WaveSpawnEntry.At(EnemyRole.Melee, "굴");

            Assert.That(WaveSpawnPlanner.PlanAuto(in entry, 0, 0f).FromWall, Is.False);
            Assert.That(WaveSpawnPlanner.PlanAt(in point, Vector3.zero).FromWall, Is.False);
        }

        /// <summary>
        /// 날아 들어오거나 걸어 들어오는 적은 예고가 없다. 오는 모습 자체가 예고라,
        /// 표식을 더 얹으면 화면만 시끄러워진다.
        /// </summary>
        [Test]
        public void FlyInPlacements_CarryNoTelegraph()
        {
            var entry = WaveSpawnEntry.Auto(EnemyRole.Melee, SpawnSide.Right, 3f);

            Assert.That(WaveSpawnPlanner.PlanAuto(in entry, 0, 0f).HasTelegraph, Is.False);
        }

        /// <summary>
        /// 땅속에서 솟는 적은 <b>배치가 예고를 들고 온다.</b> 디렉터가 따로 계산하면
        /// 방과 아레나가 서로 다른 예고 규칙을 갖게 된다.
        /// </summary>
        [Test]
        public void BurrowPlacements_CarryATelegraph()
        {
            var entry = WaveSpawnEntry.At(EnemyRole.Melee, "굴", 3f, SpawnMotion.Burrow);

            SpawnPlacement place = WaveSpawnPlanner.PlanAt(in entry, new Vector3(2f, 0f, 1f));

            Assert.That(place.HasTelegraph, Is.True);
            Assert.That(place.telegraphAt,
                        Is.EqualTo(BurrowRules.TelegraphAt(3f)).Within(Eps));
            Assert.That(place.telegraphAt, Is.LessThan(place.appearAt), "표식이 몸보다 늦게 뜬다");
        }

        /// <summary>솟는 자리가 곧 표식 자리다. 발밑이 아니면 무엇을 예고하는지 안 읽힌다.</summary>
        [Test]
        public void BurrowTelegraph_SitsWhereTheBodyRises()
        {
            var entry = WaveSpawnEntry.At(EnemyRole.Melee, "굴", 3f, SpawnMotion.Burrow);

            SpawnPlacement place = WaveSpawnPlanner.PlanAt(in entry, new Vector3(2f, 0f, 1f));

            Assert.That(place.telegraphPoint, Is.EqualTo(place.spawnPoint));
        }

        // ── 아레나 배치 ─────────────────────────────────

        /// <summary>벽에서 나오는 배치는 벽과 진입선을 들고 온다. 소환 창구가 그 둘로 가드를 건다.</summary>
        [Test]
        public void ArenaPlacements_CarryTheWall()
        {
            foreach (SpawnWall wall in new[] { SpawnWall.Front, SpawnWall.Left,
                                               SpawnWall.Right, SpawnWall.Back })
            {
                WaveSpawnEntry entry = WaveSpawnEntry.AtWall(EnemyRole.Melee, wall, 0.5f);
                SpawnPlacement place = ArenaSpawnPlanner.PlanFromWall(in entry, -6f, 6f);

                Assert.That(place.FromWall, Is.True, $"{wall}");
                Assert.That(place.wall, Is.EqualTo(wall));
            }
        }

        /// <summary>
        /// 아레나 등장은 <b>언제나</b> 예고가 붙는다. 사방이 막힌 방이라 뒤를 잡히면
        /// 피할 데가 없고, 그건 실력 부족이 아니라 정보 부족이 된다.
        /// </summary>
        [Test]
        public void ArenaPlacements_AlwaysCarryATelegraph()
        {
            foreach (float delay in new[] { 0f, 1f, 4f })
            {
                WaveSpawnEntry entry = WaveSpawnEntry.AtWall(EnemyRole.Melee, SpawnWall.Front, 0.5f, delay);
                SpawnPlacement place = ArenaSpawnPlanner.PlanFromWall(in entry, -6f, 6f);

                Assert.That(place.HasTelegraph, Is.True, $"delay={delay}");
                Assert.That(place.telegraphAt, Is.LessThanOrEqualTo(place.appearAt));
            }
        }

        /// <summary>
        /// 방과 아레나가 같은 칸에 같은 뜻을 담아야 한다. 등장 시각이 대표적이다 —
        /// 예전에는 한쪽이 <c>appearAt</c>, 다른 쪽이 <c>spawnAt</c>이었다.
        /// </summary>
        [Test]
        public void BothPlanners_AgreeOnTheAppearTime()
        {
            var roomEntry = WaveSpawnEntry.Auto(EnemyRole.Melee, SpawnSide.Right, 2.5f);
            var wallEntry = WaveSpawnEntry.AtWall(EnemyRole.Melee, SpawnWall.Front, 0.5f, 2.5f);

            Assert.That(WaveSpawnPlanner.PlanAuto(in roomEntry, 0, 0f).appearAt,
                        Is.EqualTo(2.5f).Within(Eps));
            Assert.That(ArenaSpawnPlanner.PlanFromWall(in wallEntry, -6f, 6f).appearAt,
                        Is.EqualTo(2.5f).Within(Eps));
        }

        /// <summary>돌진전사의 대기 시간은 어느 쪽에서 나오든 붙는다. 그게 유일한 예고다.</summary>
        [Test]
        public void BothPlanners_HoldTheCharger()
        {
            var roomEntry = WaveSpawnEntry.Auto(EnemyRole.Charger);
            var wallEntry = WaveSpawnEntry.AtWall(EnemyRole.Charger, SpawnWall.Front, 0.5f);

            Assert.That(WaveSpawnPlanner.PlanAuto(in roomEntry, 0, 0f).holdSeconds,
                        Is.GreaterThan(0f));
            Assert.That(ArenaSpawnPlanner.PlanFromWall(in wallEntry, -6f, 6f).holdSeconds,
                        Is.GreaterThan(0f));
        }
    }
}
