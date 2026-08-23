using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 등장 자리 규칙. <b>좌표 규칙이 곧 레벨 디자인</b>이라 여기서 전부 덮는다 —
    /// 적이 여섯이면 화면만 봐서는 누가 어느 줄에 섰는지 셀 수 없고,
    /// "마법사가 왜 자꾸 걸어가는 김에 잡히지" 같은 증상은 좌표를 봐야만 원인이 보인다.
    /// </summary>
    public class WaveSpawnPlannerTests
    {
        private const float Eps = 0.001f;

        // ── 좌우 분산 ───────────────────────────────────

        [Test]
        public void Both_AlternatesSides()
        {
            Assert.That(WaveSpawnPlanner.SideSign(SpawnSide.Both, 0), Is.EqualTo(1));
            Assert.That(WaveSpawnPlanner.SideSign(SpawnSide.Both, 1), Is.EqualTo(-1));
            Assert.That(WaveSpawnPlanner.SideSign(SpawnSide.Both, 2), Is.EqualTo(1));
        }

        [Test]
        public void FixedSides_NeverAlternate()
        {
            for (int i = 0; i < 4; i++)
            {
                Assert.That(WaveSpawnPlanner.SideSign(SpawnSide.Right, i), Is.EqualTo(1));
                Assert.That(WaveSpawnPlanner.SideSign(SpawnSide.Left, i), Is.EqualTo(-1));
            }
        }

        /// <summary>양방향 포위는 실제로 반대편 벽에서 들어와야 성립한다.</summary>
        [Test]
        public void BothSides_SpawnFromOppositeWalls()
        {
            WaveSpawn group = WaveSpawn.Of(EnemyRole.Melee, 2, SpawnSide.Both);

            SpawnPlacement right = WaveSpawnPlanner.Plan(in group, 0, 0f);
            SpawnPlacement left = WaveSpawnPlanner.Plan(in group, 1, 0f);

            Assert.That(right.spawnPoint.x, Is.GreaterThan(0f));
            Assert.That(left.spawnPoint.x, Is.LessThan(0f));
        }

        // ── 방 안에서 나온다 ─────────────────────────────

        /// <summary>
        /// 방 밖에서 소환하면 벽 콜라이더에 걸려 <b>영영 못 들어온다</b>.
        /// 증상은 "적이 안 나온다"인데 실제로는 벽 뒤에 서 있는 것이라 씬 뷰를 열어야 보인다.
        /// </summary>
        [Test]
        public void SpawnAndEntryPoints_StayInsideTheRoom()
        {
            foreach (EnemyRole role in new[] { EnemyRole.Melee, EnemyRole.Charger, EnemyRole.Ranged })
                for (int i = 0; i < 5; i++)
                {
                    WaveSpawn group = WaveSpawn.Of(role, 5, SpawnSide.Both);
                    SpawnPlacement p = WaveSpawnPlanner.Plan(in group, i, 2.9f);

                    Assert.That(Mathf.Abs(p.spawnPoint.x), Is.LessThan(WaveSpawnPlanner.RoomHalfX), $"{role} {i}");
                    Assert.That(Mathf.Abs(p.entryPoint.x), Is.LessThan(WaveSpawnPlanner.RoomHalfX), $"{role} {i}");
                    Assert.That(Mathf.Abs(p.spawnPoint.z), Is.LessThanOrEqualTo(WaveSpawnPlanner.MaxDepth + Eps), $"{role} {i}");
                    Assert.That(Mathf.Abs(p.entryPoint.z), Is.LessThanOrEqualTo(WaveSpawnPlanner.MaxDepth + Eps), $"{role} {i}");
                }
        }

        /// <summary>걸어 들어오는 그림이 나오려면 시작점과 도착점이 달라야 한다.</summary>
        [Test]
        public void EntryPoint_IsInwardOfSpawnPoint()
        {
            foreach (EnemyRole role in new[] { EnemyRole.Melee, EnemyRole.Charger, EnemyRole.Ranged })
            {
                WaveSpawn group = WaveSpawn.Of(role, 1);
                SpawnPlacement p = WaveSpawnPlanner.Plan(in group, 0, 0f);

                Assert.That(Mathf.Abs(p.entryPoint.x), Is.LessThan(Mathf.Abs(p.spawnPoint.x)), $"{role}: 진입 모션이 없다");
                Assert.That(p.entryPoint.z, Is.EqualTo(p.spawnPoint.z).Within(Eps), $"{role}: 깊이가 바뀌면 비스듬히 들어온다");
            }
        }

        // ── 돌진전사 ────────────────────────────────────

        /// <summary>
        /// 기획의 핵심 디테일. 화면 밖에서 바로 꿰뚫고 들어오면 불합리하게 느껴지므로
        /// 자리를 잡고 <b>1~1.5초</b>를 서 있는다.
        /// </summary>
        [Test]
        public void Charger_HoldsBeforeItCanCharge()
        {
            WaveSpawn group = WaveSpawn.Of(EnemyRole.Charger, 1);
            SpawnPlacement p = WaveSpawnPlanner.Plan(in group, 0, 0f);

            Assert.That(p.holdSeconds, Is.InRange(1f, 1.5f));
        }

        /// <summary>전사·마법사는 걸어 들어오는 것 자체가 예고다. 세워 두면 늘어지기만 한다.</summary>
        [Test]
        public void OtherRoles_DoNotHold()
        {
            foreach (EnemyRole role in new[] { EnemyRole.Melee, EnemyRole.Ranged })
            {
                WaveSpawn group = WaveSpawn.Of(role, 1);
                Assert.That(WaveSpawnPlanner.Plan(in group, 0, 0f).holdSeconds, Is.Zero, $"{role}");
            }
        }

        /// <summary>
        /// 돌진은 X축 직선이다. 플레이어와 같은 깊이 줄에 서야 "깊이로 피한다"가 성립한다 —
        /// 엉뚱한 줄에 세우면 돌진이 그냥 빗나가고 회피를 배울 일이 없어진다.
        /// </summary>
        [Test]
        public void Charger_LinesUpWithThePlayerDepth()
        {
            WaveSpawn group = WaveSpawn.Of(EnemyRole.Charger, 1);

            foreach (float playerZ in new[] { -2f, 0f, 1.5f })
                Assert.That(WaveSpawnPlanner.Plan(in group, 0, playerZ).entryPoint.z,
                            Is.EqualTo(playerZ).Within(Eps), $"playerZ={playerZ}");
        }

        /// <summary>같은 줄에 여럿을 세우면 서로 밀려 돌진 각이 무너진다. 조금씩 벌린다.</summary>
        [Test]
        public void MultipleChargers_DoNotStackOnOneSpot()
        {
            WaveSpawn group = WaveSpawn.Of(EnemyRole.Charger, 3, SpawnSide.Right);

            float a = WaveSpawnPlanner.Plan(in group, 0, 0f).entryPoint.z;
            float b = WaveSpawnPlanner.Plan(in group, 1, 0f).entryPoint.z;
            float c = WaveSpawnPlanner.Plan(in group, 2, 0f).entryPoint.z;

            Assert.That(b, Is.Not.EqualTo(a).Within(Eps));
            Assert.That(c, Is.Not.EqualTo(a).Within(Eps));
            Assert.That(c, Is.Not.EqualTo(b).Within(Eps));
        }

        // ── 마법사 ──────────────────────────────────────

        /// <summary>맵 최상단·최하단 구석. 가운데 서면 전사와 뭉쳐 그냥 같이 맞는다.</summary>
        [Test]
        public void Ranged_StandsAtTheDepthEdges()
        {
            WaveSpawn group = WaveSpawn.Of(EnemyRole.Ranged, 2, SpawnSide.Both);

            for (int i = 0; i < 2; i++)
                Assert.That(Mathf.Abs(WaveSpawnPlanner.Plan(in group, i, 0f).entryPoint.z),
                            Is.EqualTo(WaveSpawnPlanner.MaxDepth).Within(Eps), $"{i}번");
        }

        [Test]
        public void Ranged_SplitsTopAndBottom_WhenPlayerIsInTheMiddle()
        {
            WaveSpawn group = WaveSpawn.Of(EnemyRole.Ranged, 2, SpawnSide.Both);

            float top = WaveSpawnPlanner.Plan(in group, 0, 0f).entryPoint.z;
            float bottom = WaveSpawnPlanner.Plan(in group, 1, 0f).entryPoint.z;

            Assert.That(top, Is.GreaterThan(0f));
            Assert.That(bottom, Is.LessThan(0f));
        }

        /// <summary>
        /// 기획의 두 번째 디테일. 플레이어와 같은 줄에 서면 <b>축을 옮겨 잡으러 가는 동선이
        /// 아예 생기지 않는다</b> — 전사와 싸우다 옆걸음질만 해도 닿기 때문이다.
        /// </summary>
        [Test]
        public void Ranged_NeverSharesThePlayerLane()
        {
            WaveSpawn group = WaveSpawn.Of(EnemyRole.Ranged, 3, SpawnSide.Both);

            for (float playerZ = -3f; playerZ <= 3f; playerZ += 0.25f)
                for (int i = 0; i < 3; i++)
                {
                    float z = WaveSpawnPlanner.Plan(in group, i, playerZ).entryPoint.z;

                    Assert.That(Mathf.Abs(z - playerZ),
                                Is.GreaterThanOrEqualTo(WaveSpawnPlanner.RangedLaneGap - Eps),
                                $"playerZ={playerZ:0.##}, {i}번이 같은 줄에 섰다");
                }
        }

        /// <summary>플레이어가 위쪽 구석에 박혀 있으면 전부 아래 구석으로 간다.</summary>
        [Test]
        public void Ranged_MovesToTheFarCorner_WhenPlayerCampsAnEdge()
        {
            WaveSpawn group = WaveSpawn.Of(EnemyRole.Ranged, 2, SpawnSide.Both);

            for (int i = 0; i < 2; i++)
                Assert.That(WaveSpawnPlanner.Plan(in group, i, WaveSpawnPlanner.MaxDepth).entryPoint.z,
                            Is.LessThan(0f), $"{i}번");
        }

        // ── 전사 ────────────────────────────────────────

        /// <summary>전사가 한 점에 겹치면 뒤쪽은 앞쪽에 막혀 영영 못 붙는다.</summary>
        [Test]
        public void Melee_SpreadsAcrossDepthLanes()
        {
            WaveSpawn group = WaveSpawn.Of(EnemyRole.Melee, 4, SpawnSide.Right);

            var seen = new System.Collections.Generic.HashSet<float>();
            for (int i = 0; i < 4; i++)
                Assert.That(seen.Add(WaveSpawnPlanner.Plan(in group, i, 0f).entryPoint.z), Is.True,
                            $"{i}번이 앞선 전사와 같은 줄이다");
        }

        /// <summary>전사의 자리는 플레이어 위치와 무관하다 — 전방을 채우는 것이 역할이다.</summary>
        [Test]
        public void Melee_IgnoresPlayerDepth()
        {
            WaveSpawn group = WaveSpawn.Of(EnemyRole.Melee, 1);

            float a = WaveSpawnPlanner.Plan(in group, 0, -2f).entryPoint.z;
            float b = WaveSpawnPlanner.Plan(in group, 0, 2f).entryPoint.z;

            Assert.That(a, Is.EqualTo(b).Within(Eps));
        }

        // ── 등장 시각 ───────────────────────────────────

        [Test]
        public void AppearAt_IsDelayPlusInterval()
        {
            WaveSpawn group = WaveSpawn.Of(EnemyRole.Melee, 3, SpawnSide.Right, delay: 2f, interval: 0.5f);

            Assert.That(WaveSpawnPlanner.Plan(in group, 0, 0f).appearAt, Is.EqualTo(2f).Within(Eps));
            Assert.That(WaveSpawnPlanner.Plan(in group, 2, 0f).appearAt, Is.EqualTo(3f).Within(Eps));
        }

        /// <summary>0이나 음수로 저작된 마릿수는 1로 본다. 아무도 안 나오는 묶음은 실수다.</summary>
        [Test]
        public void ZeroCount_CountsAsOne()
        {
            var group = new WaveSpawn { role = EnemyRole.Melee, count = 0 };
            Assert.That(group.Count, Is.EqualTo(1));
        }
    }
}
