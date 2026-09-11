using NUnit.Framework;
using Prototype;

namespace Prototype.Tests
{
    /// <summary>
    /// 소환 창구가 고르는 갈래. <b>실제로 난 버그의 회귀 방지 자리다.</b>
    ///
    /// 통합 뒤 디렉터가 아레나도 방과 같은 창구로 보내게 됐는데, 창구는 벽 진입을
    /// "방에서는 성립 안 하는 것"으로만 알고 있어서 가장자리 갈래로 떨어뜨렸다.
    /// 몸이 <b>벽 뒤에 놓인 채 판정이 켜졌고</b>, 그대로 벽에 막혀 아레나에 못 들어왔다.
    /// 2 · 5스테이지에서 적이 벽에 낀 채로 서 있었다.
    ///
    /// 화면으로는 "저 적이 벽에 끼었다"로만 보인다 — 갈래를 잘못 골랐다는 것이 안 읽힌다.
    /// 그래서 갈래 고르기를 순수 함수로 떼어 여기서 본다.
    /// </summary>
    public class SpawnRouteRulesTests
    {
        // ── 벽 갈래 ─────────────────────────────────────

        /// <summary>
        /// <b>이 검사가 그 버그를 잡는다.</b> 벽을 든 배치는 벽 갈래로 가야 한다.
        /// 가장자리 갈래로 가면 시작 자리가 이미 벽 바깥이라 들어올 방법이 없다.
        /// </summary>
        [Test]
        public void WallPlacement_GoesThroughTheWall()
        {
            Assert.That(SpawnRouteRules.For(SpawnMotion.FromWall, placementHasWall: true),
                        Is.EqualTo(SpawnRoute.Wall));
        }

        /// <summary>
        /// 좌표가 벽을 들고 있으면 <b>모션이 무엇이든</b> 벽 갈래다.
        /// 저작이 어긋나도 몸이 벽 밖에서 시작한다는 사실은 안 바뀐다.
        /// </summary>
        [Test]
        public void WallPlacement_WinsOverEveryMotion()
        {
            foreach (SpawnMotion motion in new[] { SpawnMotion.FlyIn, SpawnMotion.FromWall,
                                                   SpawnMotion.Burrow })
                Assert.That(SpawnRouteRules.For(motion, placementHasWall: true),
                            Is.EqualTo(SpawnRoute.Wall), $"{motion}");
        }

        // ── 방 갈래 ─────────────────────────────────────

        [Test]
        public void BurrowInARoom_RisesFromTheGround()
        {
            Assert.That(SpawnRouteRules.For(SpawnMotion.Burrow, placementHasWall: false),
                        Is.EqualTo(SpawnRoute.Ground));
        }

        [Test]
        public void FlyInInARoom_ComesFromTheEdge()
        {
            Assert.That(SpawnRouteRules.For(SpawnMotion.FlyIn, placementHasWall: false),
                        Is.EqualTo(SpawnRoute.Edge));
        }

        /// <summary>
        /// 벽 없는 배치에 벽 진입이 박히면 갈 벽이 없다. 가장자리로 떨어뜨린다 —
        /// 소환을 건너뛰면 그 조우가 영영 전멸하지 않는다.
        /// </summary>
        [Test]
        public void WallMotionWithoutAWall_FallsBackToTheEdge()
        {
            Assert.That(SpawnRouteRules.For(SpawnMotion.FromWall, placementHasWall: false),
                        Is.EqualTo(SpawnRoute.Edge));
        }

        // ── 어긋남 알림 ─────────────────────────────────

        /// <summary>떨어뜨리되 조용히는 아니다. 저작자는 벽에서 나올 줄 알고 배치를 쌓는다.</summary>
        [Test]
        public void WallMotionWithoutAWall_IsReportedAsAMismatch()
        {
            Assert.That(SpawnRouteRules.IsMismatch(SpawnMotion.FromWall, placementHasWall: false),
                        Is.True);
        }

        [Test]
        public void EverythingElse_IsNotAMismatch()
        {
            Assert.That(SpawnRouteRules.IsMismatch(SpawnMotion.FromWall, true), Is.False);
            Assert.That(SpawnRouteRules.IsMismatch(SpawnMotion.FlyIn, false), Is.False);
            Assert.That(SpawnRouteRules.IsMismatch(SpawnMotion.Burrow, false), Is.False);

            // 벽을 든 땅속 등장은 어긋남이 아니라 벽 갈래로 흡수된다.
            Assert.That(SpawnRouteRules.IsMismatch(SpawnMotion.Burrow, true), Is.False);
        }

        // ── 실제 배치로 한 번 더 ────────────────────────

        /// <summary>
        /// 두 계획기가 내놓는 실제 배치가 각자 맞는 갈래로 가는지 본다.
        /// 위 검사들은 불리언을 직접 넘기므로, 그 불리언이 실제로 어떻게 채워지는지는 안 본다.
        /// </summary>
        [Test]
        public void RealPlacements_RouteToTheirOwnPath()
        {
            WaveSpawnEntry wallRow = WaveSpawnEntry.AtWall(EnemyRole.Melee, SpawnWall.Left, 0.5f);
            SpawnPlacement wall = ArenaSpawnPlanner.PlanFromWall(in wallRow, 12f, 24f);

            Assert.That(wall.FromWall, Is.True, "아레나 배치가 벽을 안 들고 있다");
            Assert.That(SpawnRouteRules.For(SpawnMotion.FromWall, wall.FromWall),
                        Is.EqualTo(SpawnRoute.Wall));

            WaveSpawnEntry roomRow = WaveSpawnEntry.Auto(EnemyRole.Melee);
            SpawnPlacement room = WaveSpawnPlanner.PlanAuto(in roomRow, 0, 0f);

            Assert.That(room.FromWall, Is.False, "방 배치가 벽을 들고 있다");
            Assert.That(SpawnRouteRules.For(SpawnMotion.FlyIn, room.FromWall),
                        Is.EqualTo(SpawnRoute.Edge));
        }
    }
}
