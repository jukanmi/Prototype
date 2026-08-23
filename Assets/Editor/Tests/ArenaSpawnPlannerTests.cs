using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 벽 뒤 몬스터의 등장 계획. 네 벽마다 방향과 부호가 반대라
    /// <b>넷 중 하나는 반드시 뒤집혀 들어간다</b> — 화면으로는 "저 적만 벽에 끼었네"로만 보인다.
    /// </summary>
    public class ArenaSpawnPlannerTests
    {
        private const float Eps = 0.001f;
        private const float MinX = -6f;
        private const float MaxX = 6f;

        private static readonly SpawnWall[] AllWalls =
        {
            SpawnWall.Front, SpawnWall.Left, SpawnWall.Right, SpawnWall.Back,
        };

        private static ArenaSpawnPlan Plan(SpawnWall wall, int count = 1, int index = 0,
                                           EnemyRole role = EnemyRole.Melee, float delay = 0f)
        {
            RoundSpawn spawn = RoundSpawn.Of(role, count, wall, delay);
            return ArenaSpawnPlanner.Plan(in spawn, index, MinX, MaxX);
        }

        // ── 예고 ────────────────────────────────────────

        /// <summary>
        /// 기획의 핵심. 플레이어가 뒤를 잡히는 건 <b>실력 부족이어야지 정보 부족이면 안 된다</b>.
        /// </summary>
        [Test]
        public void Telegraph_LeadsTheSpawn()
        {
            foreach (SpawnWall wall in AllWalls)
            {
                ArenaSpawnPlan plan = Plan(wall, delay: 3f);

                Assert.That(plan.spawnAt - plan.telegraphAt,
                            Is.EqualTo(ArenaSpawnPlanner.TelegraphLead).Within(Eps), $"{wall}");
            }
        }

        [Test]
        public void TelegraphLead_IsThePlannedEightTenths()
        {
            Assert.That(ArenaSpawnPlanner.TelegraphLead, Is.EqualTo(0.8f).Within(Eps));
        }

        /// <summary>라운드 시작과 동시에 나오는 적은 예고가 음수가 된다. 0으로 물린다.</summary>
        [Test]
        public void ImmediateSpawn_TelegraphsAtZero_NotNegative()
        {
            Assert.That(Plan(SpawnWall.Front).telegraphAt, Is.Zero);
        }

        [Test]
        public void Telegraph_SitsOnTheWallItself()
        {
            Assert.That(Plan(SpawnWall.Front).telegraphPoint.z,
                        Is.EqualTo(ArenaSpawnPlanner.ArenaHalfZ).Within(Eps));
            Assert.That(Plan(SpawnWall.Back).telegraphPoint.z,
                        Is.EqualTo(-ArenaSpawnPlanner.ArenaHalfZ).Within(Eps));
            Assert.That(Plan(SpawnWall.Left).telegraphPoint.x, Is.EqualTo(MinX).Within(Eps));
            Assert.That(Plan(SpawnWall.Right).telegraphPoint.x, Is.EqualTo(MaxX).Within(Eps));
        }

        // ── 시차 ────────────────────────────────────────

        /// <summary>
        /// 동시 스폰은 몹이 한 점에 겹쳐 그리드 점유가 꼬이고, 시각적으로도 갑자기 튀어나온 느낌이 된다.
        /// 기획 기준 0.2~0.3초.
        /// </summary>
        [Test]
        public void Spawns_AreStaggered()
        {
            Assert.That(ArenaSpawnPlanner.StaggerStep, Is.InRange(0.2f, 0.3f));

            float first = Plan(SpawnWall.Front, count: 3, index: 0).spawnAt;
            float second = Plan(SpawnWall.Front, count: 3, index: 1).spawnAt;
            float third = Plan(SpawnWall.Front, count: 3, index: 2).spawnAt;

            Assert.That(second - first, Is.EqualTo(ArenaSpawnPlanner.StaggerStep).Within(Eps));
            Assert.That(third - second, Is.EqualTo(ArenaSpawnPlanner.StaggerStep).Within(Eps));
        }

        [Test]
        public void Delay_ShiftsTheWholeGroup()
        {
            Assert.That(Plan(SpawnWall.Front, count: 2, index: 1, delay: 4f).spawnAt,
                        Is.EqualTo(4f + ArenaSpawnPlanner.StaggerStep).Within(Eps));
        }

        // ── 벽 뒤에서 시작한다 ───────────────────────────

        /// <summary>
        /// 시작점은 아레나 <b>바깥</b>이어야 한다. 안쪽에서 시작하면 벽에서 나오는 게 아니라
        /// 그냥 방 안에 뿅 하고 나타난 것이 된다.
        /// </summary>
        [Test]
        public void SpawnPoint_IsOutsideTheArena()
        {
            Assert.That(Plan(SpawnWall.Front).spawnPoint.z, Is.GreaterThan(ArenaSpawnPlanner.ArenaHalfZ));
            Assert.That(Plan(SpawnWall.Back).spawnPoint.z, Is.LessThan(-ArenaSpawnPlanner.ArenaHalfZ));
            Assert.That(Plan(SpawnWall.Left).spawnPoint.x, Is.LessThan(MinX));
            Assert.That(Plan(SpawnWall.Right).spawnPoint.x, Is.GreaterThan(MaxX));
        }

        /// <summary>목표 셀은 아레나 안이어야 한다. 벽에 붙어 서면 때릴 자리가 안 나온다.</summary>
        [Test]
        public void EntryPoint_IsInsideTheArena()
        {
            foreach (SpawnWall wall in AllWalls)
            {
                ArenaSpawnPlan plan = Plan(wall, count: 3, index: 1);

                Assert.That(plan.entryPoint.x, Is.InRange(MinX, MaxX), $"{wall}");
                Assert.That(Mathf.Abs(plan.entryPoint.z),
                            Is.LessThan(ArenaSpawnPlanner.ArenaHalfZ), $"{wall}");
            }
        }

        /// <summary>걸어 나오는 축은 벽에 수직이다. 비스듬히 들어오면 어느 벽에서 왔는지 안 읽힌다.</summary>
        [Test]
        public void EntryWalk_IsPerpendicularToTheWall()
        {
            foreach (SpawnWall wall in AllWalls)
            {
                ArenaSpawnPlan plan = Plan(wall, count: 3, index: 2);

                if (ArenaSpawnPlanner.IsHorizontal(wall))
                    Assert.That(plan.entryPoint.z, Is.EqualTo(plan.spawnPoint.z).Within(Eps), $"{wall}");
                else
                    Assert.That(plan.entryPoint.x, Is.EqualTo(plan.spawnPoint.x).Within(Eps), $"{wall}");
            }
        }

        // ── 진입선 ──────────────────────────────────────

        /// <summary>
        /// 진입선은 벽과 목표 셀 <b>사이</b>에 있어야 한다. 벽 밖이면 시작하자마자 넘은 것이 되어
        /// 벽 뒤에 숨는 그림이 아예 안 나오고, 목표 셀보다 안쪽이면 다 나올 때까지 벽에 가려 있다.
        /// </summary>
        [Test]
        public void EntryLine_SitsBetweenWallAndTarget()
        {
            foreach (SpawnWall wall in AllWalls)
            {
                ArenaSpawnPlan plan = Plan(wall);

                float wallCoord = ArenaSpawnPlanner.IsHorizontal(wall)
                    ? plan.telegraphPoint.x : plan.telegraphPoint.z;
                float targetCoord = ArenaSpawnPlanner.IsHorizontal(wall)
                    ? plan.entryPoint.x : plan.entryPoint.z;

                float lo = Mathf.Min(wallCoord, targetCoord);
                float hi = Mathf.Max(wallCoord, targetCoord);

                Assert.That(plan.entryLine, Is.InRange(lo, hi), $"{wall}");
            }
        }

        /// <summary>시작 지점에서는 아직 안 넘었고, 목표 셀에서는 넘어 있어야 한다.</summary>
        [Test]
        public void CrossingTest_FlipsBetweenSpawnAndTarget()
        {
            foreach (SpawnWall wall in AllWalls)
            {
                ArenaSpawnPlan plan = Plan(wall);

                Assert.That(ArenaSpawnPlanner.HasCrossedEntryLine(wall, plan.spawnPoint, plan.entryLine),
                            Is.False, $"{wall}: 나오기도 전에 벽 앞으로 나왔다");

                Assert.That(ArenaSpawnPlanner.HasCrossedEntryLine(wall, plan.entryPoint, plan.entryLine),
                            Is.True, $"{wall}: 자리를 잡았는데도 벽 뒤에 가려 있다");
            }
        }

        // ── 벽면을 따라 벌려 선다 ────────────────────────

        [Test]
        public void GroupMembers_SpreadAlongTheWall()
        {
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                {
                    ArenaSpawnPlan a = Plan(SpawnWall.Front, count: 3, index: i);
                    ArenaSpawnPlan b = Plan(SpawnWall.Front, count: 3, index: j);

                    Assert.That(a.entryPoint.x, Is.Not.EqualTo(b.entryPoint.x).Within(Eps), $"{i} vs {j}");
                }
        }

        /// <summary>양끝에 붙으면 몸통이 벽에 낀다. 여백만큼 물러나 있어야 한다.</summary>
        [Test]
        public void GroupMembers_KeepOffTheCorners()
        {
            for (int i = 0; i < 4; i++)
            {
                ArenaSpawnPlan plan = Plan(SpawnWall.Front, count: 4, index: i);

                Assert.That(plan.entryPoint.x,
                            Is.InRange(MinX + ArenaSpawnPlanner.EdgeMargin - Eps,
                                       MaxX - ArenaSpawnPlanner.EdgeMargin + Eps), $"{i}번");
            }
        }

        /// <summary>아레나가 어디에 있든 규칙은 같다 — 오른쪽 아레나도 자기 경계를 기준으로 잡는다.</summary>
        [Test]
        public void OffsetArena_PlansRelativeToItsOwnBounds()
        {
            RoundSpawn spawn = RoundSpawn.Of(EnemyRole.Melee, 1, SpawnWall.Left);
            ArenaSpawnPlan plan = ArenaSpawnPlanner.Plan(in spawn, 0, 33f, 45f);

            Assert.That(plan.spawnPoint.x, Is.LessThan(33f));
            Assert.That(plan.entryPoint.x, Is.InRange(33f, 45f));
        }

        // ── 돌진전사 ────────────────────────────────────

        /// <summary>나오자마자 꿰뚫고 들어오면 불합리하다. 자리를 잡고 1~1.5초를 서 있는다.</summary>
        [Test]
        public void Charger_HoldsBeforeItCanCharge()
        {
            Assert.That(Plan(SpawnWall.Front, role: EnemyRole.Charger).holdSeconds, Is.InRange(1f, 1.5f));
        }

        [Test]
        public void OtherRoles_DoNotHold()
        {
            Assert.That(Plan(SpawnWall.Front, role: EnemyRole.Melee).holdSeconds, Is.Zero);
            Assert.That(Plan(SpawnWall.Front, role: EnemyRole.Ranged).holdSeconds, Is.Zero);
        }

        // ── 방향 분류 ───────────────────────────────────

        [Test]
        public void SideWalls_AreHorizontal_DepthWallsAreNot()
        {
            Assert.That(ArenaSpawnPlanner.IsHorizontal(SpawnWall.Left), Is.True);
            Assert.That(ArenaSpawnPlanner.IsHorizontal(SpawnWall.Right), Is.True);
            Assert.That(ArenaSpawnPlanner.IsHorizontal(SpawnWall.Front), Is.False);
            Assert.That(ArenaSpawnPlanner.IsHorizontal(SpawnWall.Back), Is.False);
        }

        [Test]
        public void ZeroCount_CountsAsOne()
        {
            var spawn = new RoundSpawn { role = EnemyRole.Melee, count = 0, wall = SpawnWall.Front };
            Assert.That(spawn.Count, Is.EqualTo(1));
        }
    }
}
