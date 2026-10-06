using System.Collections.Generic;
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

        /// <summary>
        /// 기준 방. 방을 인자로 받게 바뀐 뒤에도 이 파일의 기대값은 하나도 안 바뀌었다 —
        /// 그게 "100% 방의 답은 예전과 같다"의 증거다. 다른 크기의 방은 아래 "방 크기" 절이 본다.
        /// </summary>
        private static readonly RoomRect Room = RoomRect.Default;

        private static float MaxDepth => WaveSpawnPlanner.MaxDepth(Room);

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
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Melee, SpawnSide.Both);

            SpawnPlacement right = WaveSpawnPlanner.PlanAuto(in group, 0, 0f, Room);
            SpawnPlacement left = WaveSpawnPlanner.PlanAuto(in group, 1, 0f, Room);

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
                    WaveSpawnEntry group = WaveSpawnEntry.Auto(role, SpawnSide.Both);
                    SpawnPlacement p = WaveSpawnPlanner.PlanAuto(in group, i, 2.9f, Room);

                    Assert.That(Mathf.Abs(p.spawnPoint.x), Is.LessThan(Room.HalfX), $"{role} {i}");
                    Assert.That(Mathf.Abs(p.entryPoint.x), Is.LessThan(Room.HalfX), $"{role} {i}");
                    Assert.That(Mathf.Abs(p.spawnPoint.z), Is.LessThanOrEqualTo(MaxDepth + Eps), $"{role} {i}");
                    Assert.That(Mathf.Abs(p.entryPoint.z), Is.LessThanOrEqualTo(MaxDepth + Eps), $"{role} {i}");
                }
        }

        /// <summary>걸어 들어오는 그림이 나오려면 시작점과 도착점이 달라야 한다.</summary>
        [Test]
        public void EntryPoint_IsInwardOfSpawnPoint()
        {
            foreach (EnemyRole role in new[] { EnemyRole.Melee, EnemyRole.Charger, EnemyRole.Ranged })
            {
                WaveSpawnEntry group = WaveSpawnEntry.Auto(role);
                SpawnPlacement p = WaveSpawnPlanner.PlanAuto(in group, 0, 0f, Room);

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
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Charger);
            SpawnPlacement p = WaveSpawnPlanner.PlanAuto(in group, 0, 0f, Room);

            Assert.That(p.holdSeconds, Is.InRange(1f, 1.5f));
        }

        /// <summary>전사·마법사는 걸어 들어오는 것 자체가 예고다. 세워 두면 늘어지기만 한다.</summary>
        [Test]
        public void OtherRoles_DoNotHold()
        {
            foreach (EnemyRole role in new[] { EnemyRole.Melee, EnemyRole.Ranged })
            {
                WaveSpawnEntry group = WaveSpawnEntry.Auto(role);
                Assert.That(WaveSpawnPlanner.PlanAuto(in group, 0, 0f, Room).holdSeconds, Is.Zero, $"{role}");
            }
        }

        /// <summary>
        /// 돌진은 X축 직선이다. 플레이어와 같은 깊이 줄에 서야 "깊이로 피한다"가 성립한다 —
        /// 엉뚱한 줄에 세우면 돌진이 그냥 빗나가고 회피를 배울 일이 없어진다.
        /// </summary>
        [Test]
        public void Charger_LinesUpWithThePlayerDepth()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Charger);

            foreach (float playerZ in new[] { -2f, 0f, 1.5f })
                Assert.That(WaveSpawnPlanner.PlanAuto(in group, 0, playerZ, Room).entryPoint.z,
                            Is.EqualTo(playerZ).Within(Eps), $"playerZ={playerZ}");
        }

        /// <summary>같은 줄에 여럿을 세우면 서로 밀려 돌진 각이 무너진다. 조금씩 벌린다.</summary>
        [Test]
        public void MultipleChargers_DoNotStackOnOneSpot()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Charger, SpawnSide.Right);

            float a = WaveSpawnPlanner.PlanAuto(in group, 0, 0f, Room).entryPoint.z;
            float b = WaveSpawnPlanner.PlanAuto(in group, 1, 0f, Room).entryPoint.z;
            float c = WaveSpawnPlanner.PlanAuto(in group, 2, 0f, Room).entryPoint.z;

            Assert.That(b, Is.Not.EqualTo(a).Within(Eps));
            Assert.That(c, Is.Not.EqualTo(a).Within(Eps));
            Assert.That(c, Is.Not.EqualTo(b).Within(Eps));
        }

        /// <summary>
        /// <b>플레이어가 깊이 끝에 붙어 있어도</b> 겹치면 안 된다.
        ///
        /// 예전에는 벗어난 줄을 벽으로 물려서 두 줄이 같은 좌표로 접혔고, 4-1(교차 돌진 3기)에서
        /// 돌진전사 둘이 같은 자리에 섰다. 화면으로는 한 기가 덜 나온 것처럼만 보인다.
        /// </summary>
        [Test]
        public void ChargersAtTheDepthEdge_StillSpreadOut()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Charger, SpawnSide.Right);

            foreach (float playerZ in new[] { MaxDepth, -MaxDepth, 2.9f })
            {
                var lanes = new List<float>();

                for (int i = 0; i < 3; i++)
                    lanes.Add(WaveSpawnPlanner.PlanAuto(in group, i, playerZ, Room).entryPoint.z);

                for (int i = 0; i < lanes.Count; i++)
                    for (int j = i + 1; j < lanes.Count; j++)
                        Assert.That(lanes[i], Is.Not.EqualTo(lanes[j]).Within(Eps),
                                    $"playerZ={playerZ}: {i}번과 {j}번이 같은 줄");
            }
        }

        /// <summary>방 안에 있을 때는 예전과 같은 답이어야 한다. 0, 한 칸 위, 한 칸 아래.</summary>
        [Test]
        public void ChargerLanes_AreUnchangedInsideTheRoom()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Charger, SpawnSide.Right);
            float step = WaveSpawnPlanner.ChargerLaneStep;

            Assert.That(WaveSpawnPlanner.PlanAuto(in group, 0, 0f, Room).entryPoint.z, Is.EqualTo(0f).Within(Eps));
            Assert.That(WaveSpawnPlanner.PlanAuto(in group, 1, 0f, Room).entryPoint.z, Is.EqualTo(step).Within(Eps));
            Assert.That(WaveSpawnPlanner.PlanAuto(in group, 2, 0f, Room).entryPoint.z, Is.EqualTo(-step).Within(Eps));
        }

        // ── 마법사 ──────────────────────────────────────

        /// <summary>맵 최상단·최하단 구석. 가운데 서면 전사와 뭉쳐 그냥 같이 맞는다.</summary>
        [Test]
        public void Ranged_StandsAtTheDepthEdges()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Ranged, SpawnSide.Both);

            for (int i = 0; i < 2; i++)
                Assert.That(Mathf.Abs(WaveSpawnPlanner.PlanAuto(in group, i, 0f, Room).entryPoint.z),
                            Is.EqualTo(MaxDepth).Within(Eps), $"{i}번");
        }

        [Test]
        public void Ranged_SplitsTopAndBottom_WhenPlayerIsInTheMiddle()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Ranged, SpawnSide.Both);

            float top = WaveSpawnPlanner.PlanAuto(in group, 0, 0f, Room).entryPoint.z;
            float bottom = WaveSpawnPlanner.PlanAuto(in group, 1, 0f, Room).entryPoint.z;

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
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Ranged, SpawnSide.Both);

            for (float playerZ = -3f; playerZ <= 3f; playerZ += 0.25f)
                for (int i = 0; i < 3; i++)
                {
                    float z = WaveSpawnPlanner.PlanAuto(in group, i, playerZ, Room).entryPoint.z;

                    Assert.That(Mathf.Abs(z - playerZ),
                                Is.GreaterThanOrEqualTo(WaveSpawnPlanner.RangedLaneGap - Eps),
                                $"playerZ={playerZ:0.##}, {i}번이 같은 줄에 섰다");
                }
        }

        /// <summary>
        /// 구석은 둘인데 마법사가 셋이면 <b>한 명은 안쪽으로 물러나야 한다.</b>
        ///
        /// 예전에는 구석 두 개만 번갈아 써서 0번과 2번이 같은 구석에 겹쳤고, 좌우 교대도 같은
        /// 주기라 둘이 같은 점에 섰다. 4-2(마법사 3기 탄막)가 그 조합이다.
        /// </summary>
        [Test]
        public void ThreeRanged_DoNotShareACorner()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Ranged, SpawnSide.Both);

            foreach (float playerZ in new[] { 0f, 0.25f, -1.2f, MaxDepth })
            {
                var lanes = new List<float>();

                for (int i = 0; i < 3; i++)
                    lanes.Add(WaveSpawnPlanner.PlanAuto(in group, i, playerZ, Room).entryPoint.z);

                for (int i = 0; i < lanes.Count; i++)
                    for (int j = i + 1; j < lanes.Count; j++)
                        Assert.That(lanes[i], Is.Not.EqualTo(lanes[j]).Within(Eps),
                                    $"playerZ={playerZ}: {i}번과 {j}번이 같은 줄");
            }
        }

        /// <summary>플레이어가 위쪽 구석에 박혀 있으면 전부 아래 구석으로 간다.</summary>
        [Test]
        public void Ranged_MovesToTheFarCorner_WhenPlayerCampsAnEdge()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Ranged, SpawnSide.Both);

            for (int i = 0; i < 2; i++)
                Assert.That(WaveSpawnPlanner.PlanAuto(in group, i, MaxDepth, Room).entryPoint.z,
                            Is.LessThan(0f), $"{i}번");
        }

        // ── 전사 ────────────────────────────────────────

        /// <summary>전사가 한 점에 겹치면 뒤쪽은 앞쪽에 막혀 영영 못 붙는다.</summary>
        [Test]
        public void Melee_SpreadsAcrossDepthLanes()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Melee, SpawnSide.Right);

            var seen = new System.Collections.Generic.HashSet<float>();
            for (int i = 0; i < 4; i++)
                Assert.That(seen.Add(WaveSpawnPlanner.PlanAuto(in group, i, 0f, Room).entryPoint.z), Is.True,
                            $"{i}번이 앞선 전사와 같은 줄이다");
        }

        /// <summary>전사의 자리는 플레이어 위치와 무관하다 — 전방을 채우는 것이 역할이다.</summary>
        [Test]
        public void Melee_IgnoresPlayerDepth()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Melee);

            float a = WaveSpawnPlanner.PlanAuto(in group, 0, -2f, Room).entryPoint.z;
            float b = WaveSpawnPlanner.PlanAuto(in group, 0, 2f, Room).entryPoint.z;

            Assert.That(a, Is.EqualTo(b).Within(Eps));
        }

        // ── 방 크기 ─────────────────────────────────────
        // 방이 바뀔 때 거리는 뜻에 따라 셋으로 갈린다. ① 벽에 붙는 자리는 벽을 따라가고,
        // ② 방 안 배치는 비율을 따라가고, ③ 몸 · 사거리는 고정이다.
        // 계획서: docs/Room_Size_Plan.md (3항 · 2단계)

        private static readonly EnemyRole[] Roles = { EnemyRole.Melee, EnemyRole.Charger, EnemyRole.Ranged };

        private static RoomRect Scaled(int percent) => RoomRules.Scale(RoomRect.Default, percent);

        /// <summary>기준 크기(12×6)지만 중심이 (20, 1)인 방. 원점 중심 방에서는 중심 기준 버그가 안 보인다.</summary>
        private static readonly RoomRect OffOrigin = new RoomRect(14f, 26f, -2f, 4f);

        /// <summary>줄어든 방 · 늘어난 방에서도 벽 밖에서 소환되거나 벽 밖에 정착하지 않는다.</summary>
        [TestCase(80)]
        [TestCase(125)]
        public void ResizedRoom_AllRolesSpawnAndSettleInside(int percent)
        {
            RoomRect room = Scaled(percent);
            float max = WaveSpawnPlanner.MaxDepth(room);

            foreach (EnemyRole role in Roles)
                foreach (float playerZ in new[] { -max, 0f, max })
                    for (int i = 0; i < 6; i++)
                    {
                        WaveSpawnEntry group = WaveSpawnEntry.Auto(role, SpawnSide.Both);
                        SpawnPlacement p = WaveSpawnPlanner.PlanAuto(in group, i, playerZ, room);

                        Assert.That(WaveSpawnPlanner.IsInsideRoom(p.spawnPoint, room), Is.True,
                                    $"{percent}% {role} {i} playerZ={playerZ}: 등장 지점 {p.spawnPoint}");
                        Assert.That(WaveSpawnPlanner.IsInsideRoom(p.entryPoint, room), Is.True,
                                    $"{percent}% {role} {i} playerZ={playerZ}: 정착 지점 {p.entryPoint}");
                    }
        }

        /// <summary>① 등장 지점은 벽을 따라간다. 벽까지 거리는 방 크기와 무관한 몸통 하나다.</summary>
        [TestCase(80)]
        [TestCase(100)]
        [TestCase(125)]
        public void SpawnPoint_KeepsFixedDistanceToWall(int percent)
        {
            RoomRect room = Scaled(percent);

            foreach (EnemyRole role in Roles)
            {
                WaveSpawnEntry right = WaveSpawnEntry.Auto(role, SpawnSide.Right);
                WaveSpawnEntry left = WaveSpawnEntry.Auto(role, SpawnSide.Left);

                Assert.That(room.MaxX - WaveSpawnPlanner.PlanAuto(in right, 0, 0f, room).spawnPoint.x,
                            Is.EqualTo(WaveSpawnPlanner.SpawnInset).Within(Eps), $"{percent}% {role} 오른쪽");
                Assert.That(WaveSpawnPlanner.PlanAuto(in left, 0, 0f, room).spawnPoint.x - room.MinX,
                            Is.EqualTo(WaveSpawnPlanner.SpawnInset).Within(Eps), $"{percent}% {role} 왼쪽");
            }
        }

        /// <summary>
        /// ② 정착 지점은 방 비율을 따라간다 — 반폭 대비 위치가 100% 방과 같다.
        /// 벽 기준 고정 거리로 두면 좁은 방에서 좌우 전사가 나오자마자 플레이어를 낀다.
        /// </summary>
        [TestCase(80)]
        [TestCase(125)]
        public void EntryPoint_KeepsRelativePositionInRoom(int percent)
        {
            RoomRect room = Scaled(percent);

            foreach (EnemyRole role in Roles)
            {
                WaveSpawnEntry group = WaveSpawnEntry.Auto(role, SpawnSide.Right);

                float full = WaveSpawnPlanner.PlanAuto(in group, 0, 0f, Room).entryPoint.x / Room.HalfX;
                float resized = WaveSpawnPlanner.PlanAuto(in group, 0, 0f, room).entryPoint.x / room.HalfX;

                Assert.That(resized, Is.EqualTo(full).Within(Eps), $"{percent}% {role}");
            }
        }

        /// <summary>비율로 옮기기 전의 벽 기준 거리(2.8 · 1.8 · 1.2)가 100% 방에서 그대로 나온다.</summary>
        [Test]
        public void EntryPoint_AtFullSize_MatchesOldWallDistances()
        {
            float Settle(EnemyRole role)
            {
                WaveSpawnEntry group = WaveSpawnEntry.Auto(role, SpawnSide.Right);
                return Room.MaxX - WaveSpawnPlanner.PlanAuto(in group, 0, 0f, Room).entryPoint.x;
            }

            Assert.That(Settle(EnemyRole.Melee), Is.EqualTo(2.8f).Within(Eps));
            Assert.That(Settle(EnemyRole.Charger), Is.EqualTo(1.8f).Within(Eps));
            Assert.That(Settle(EnemyRole.Ranged), Is.EqualTo(1.2f).Within(Eps));
        }

        /// <summary>③ 깊이 줄 간격은 몸 두 개 사이의 거리라 방에 비례하지 않는다.</summary>
        [TestCase(80)]
        [TestCase(125)]
        public void DepthLaneSpacing_DoesNotScale(int percent)
        {
            RoomRect room = Scaled(percent);

            WaveSpawnEntry melee = WaveSpawnEntry.Auto(EnemyRole.Melee, SpawnSide.Right);
            Assert.That(WaveSpawnPlanner.PlanAuto(in melee, 1, 0f, room).entryPoint.z
                        - WaveSpawnPlanner.PlanAuto(in melee, 0, 0f, room).entryPoint.z,
                        Is.EqualTo(1.6f).Within(Eps), $"{percent}% 전사");

            WaveSpawnEntry charger = WaveSpawnEntry.Auto(EnemyRole.Charger, SpawnSide.Right);
            Assert.That(WaveSpawnPlanner.PlanAuto(in charger, 1, 0f, room).entryPoint.z,
                        Is.EqualTo(WaveSpawnPlanner.ChargerLaneStep).Within(Eps), $"{percent}% 돌진전사");
        }

        /// <summary>
        /// 80% 방은 깊이 ±1.8이라 ±2.4 줄이 방 밖이다. 벽으로 물리기만 하면 ±1.8로 접혀
        /// ±1.6 줄과 0.2 차이로 겹친다. 방 밖 줄은 건너뛰고 방 안 줄만 돌려 써야 한다.
        /// </summary>
        [Test]
        public void SmallRoom_MeleeSkipsLanesOutsideTheRoom()
        {
            RoomRect room = Scaled(80);
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Melee, SpawnSide.Both);

            var spots = new HashSet<string>();

            for (int i = 0; i < 5; i++)
            {
                Vector3 p = WaveSpawnPlanner.PlanAuto(in group, i, 0f, room).entryPoint;

                bool onLane = Mathf.Abs(p.z) < Eps || Mathf.Abs(Mathf.Abs(p.z) - 1.6f) < Eps;
                Assert.That(onLane, Is.True, $"{i}번이 줄이 아닌 깊이 {p.z:0.###}에 섰다 — 벽으로 물린 줄이다");
                Assert.That(spots.Add($"{p.x:F3},{p.z:F3}"), Is.True, $"{i}번이 앞선 전사와 같은 자리다");
            }
        }

        /// <summary>
        /// 한쪽에서만 나오는 전사가 방 안 줄을 한 바퀴 다 쓰면, 다음 바퀴는 몸 하나만큼 안쪽에 서서 겹치지 않는다.
        /// 80% 방은 세 줄이라 네 번째부터, 100% 방은 다섯 줄이라 여섯 번째부터다.
        /// </summary>
        [TestCase(80, 6)]
        [TestCase(100, 8)]
        public void OneSidedMelee_WrappingLanes_DoNotStack(int percent, int count)
        {
            RoomRect room = Scaled(percent);
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Melee, SpawnSide.Right);

            var spots = new HashSet<string>();

            for (int i = 0; i < count; i++)
            {
                Vector3 p = WaveSpawnPlanner.PlanAuto(in group, i, 0f, room).entryPoint;

                Assert.That(spots.Add($"{p.x:F3},{p.z:F3}"), Is.True, $"{percent}% {i}번이 앞 바퀴 전사와 같은 자리다");
                Assert.That(p.x, Is.GreaterThan(room.CenterX), $"{percent}% {i}번이 방 중심을 넘어 반대편에 섰다");
            }
        }

        /// <summary>
        /// 양쪽 등장은 안쪽으로 당기지 않는다. 증원이 양쪽 등장으로 번호를 계속 올리므로,
        /// 당기면 늦게 온 증원일수록 플레이어 코앞에 선다.
        /// </summary>
        [Test]
        public void BothSidedMelee_IsNeverPulledInward()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Melee, SpawnSide.Both);

            for (int i = 0; i < 12; i++)
                Assert.That(Mathf.Abs(WaveSpawnPlanner.PlanAuto(in group, i, 0f, Room).entryPoint.x),
                            Is.EqualTo(Room.HalfX - 2.8f).Within(Eps), $"{i}번");
        }

        /// <summary>늘어난 방에서 마법사는 새 구석에 붙는다. 옛 구석에 서면 방 안쪽에 떠 있는 것으로 보인다.</summary>
        [Test]
        public void BigRoom_RangedMovesToTheNewCorner()
        {
            RoomRect room = Scaled(125);
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Ranged, SpawnSide.Both);

            for (int i = 0; i < 2; i++)
                Assert.That(Mathf.Abs(WaveSpawnPlanner.PlanAuto(in group, i, 0f, room).entryPoint.z),
                            Is.EqualTo(3.75f - WaveSpawnPlanner.DepthInset).Within(Eps), $"{i}번");
        }

        /// <summary>좌표는 원점이 아니라 방 중심 기준이다. 플레이어 깊이는 월드 좌표로 들어온다.</summary>
        [Test]
        public void OffOriginRoom_UsesRoomCenter()
        {
            RoomRect room = OffOrigin;

            WaveSpawnEntry charger = WaveSpawnEntry.Auto(EnemyRole.Charger, SpawnSide.Right);
            SpawnPlacement c = WaveSpawnPlanner.PlanAuto(in charger, 0, 1.5f, room);
            Assert.That(c.entryPoint.z, Is.EqualTo(1.5f).Within(Eps), "돌진전사가 플레이어 줄을 못 맞췄다");
            Assert.That(c.entryPoint.x, Is.EqualTo(20f + 6f - 1.8f).Within(Eps));
            Assert.That(c.spawnPoint.x, Is.EqualTo(26f - WaveSpawnPlanner.SpawnInset).Within(Eps));

            WaveSpawnEntry melee = WaveSpawnEntry.Auto(EnemyRole.Melee, SpawnSide.Right);
            Assert.That(WaveSpawnPlanner.PlanAuto(in melee, 0, 1f, room).entryPoint.z, Is.EqualTo(1f).Within(Eps),
                        "전사 0번 줄은 방 중심 깊이다");

            WaveSpawnEntry ranged = WaveSpawnEntry.Auto(EnemyRole.Ranged, SpawnSide.Both);
            Assert.That(Mathf.Abs(WaveSpawnPlanner.PlanAuto(in ranged, 0, 1f, room).entryPoint.z - 1f),
                        Is.EqualTo(3f - WaveSpawnPlanner.DepthInset).Within(Eps), "마법사가 방 구석이 아닌 곳에 섰다");

            Assert.That(WaveSpawnPlanner.IsInsideRoom(new Vector3(20f, 0f, 1f), room), Is.True);
            Assert.That(WaveSpawnPlanner.IsInsideRoom(new Vector3(2f, 0f, 0f), room), Is.False,
                        "기준 방 안인 점을 방 안으로 본다 — 원점 기준으로 검사하고 있다");

            Vector3 clamped = WaveSpawnPlanner.ClampIntoRoom(new Vector3(0f, 5f, 10f), room);
            Assert.That(clamped.x, Is.EqualTo(14f + WaveSpawnPlanner.SpawnInset).Within(Eps));
            Assert.That(clamped.y, Is.Zero);
            Assert.That(clamped.z, Is.EqualTo(4f - WaveSpawnPlanner.DepthInset).Within(Eps));
        }

        /// <summary>
        /// <b>방 크기 하한 80%는 마법사 후퇴 거리에서 나왔다</b>(계획서 2.5). 가장 작은 방에서도
        /// 마법사의 기본 배치가 중앙 플레이어로부터 후퇴 거리 밖이어야 한다 — 안이면 등장하자마자
        /// 물러나려는데 뒤가 벽이라 벽에 붙어 떤다.
        ///
        /// 값이 애셋에 있어서 여기서 읽는다. 누가 후퇴 거리를 올리면 이 테스트가 하한을 올리라고 알려 준다.
        /// </summary>
        [Test]
        public void RangedSettle_AtMinPercent_IsOutsideRetreatRange()
        {
            const string path = "Assets/Data/Enemy/Enemy_Ranged.asset";
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            Assert.That(data, Is.Not.Null, $"{path} 가 없다");

            RoomRect room = Scaled(RoomRules.MinPercent);
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Ranged, SpawnSide.Both);

            for (int i = 0; i < 2; i++)
            {
                Vector3 p = WaveSpawnPlanner.PlanAuto(in group, i, room.CenterZ, room).entryPoint;
                float distance = new Vector2(p.x - room.CenterX, p.z - room.CenterZ).magnitude;

                Assert.That(distance, Is.GreaterThanOrEqualTo(data.preferredMinRange),
                            $"{RoomRules.MinPercent}% 방에서 마법사 {i}번이 후퇴 거리({data.preferredMinRange}) 안에 선다 " +
                            $"({distance:0.##}) — RoomRules.MinPercent 를 올릴 것");
            }
        }

        // ── 등장 시각 ───────────────────────────────────

        /// <summary>저작한 시각이 그대로 배치에 실린다. 순번은 시각을 건드리지 않는다.</summary>
        [Test]
        public void AppearAt_ComesStraightFromTheRow()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Melee, SpawnSide.Right, 2.5f);

            Assert.That(WaveSpawnPlanner.PlanAuto(in group, 0, 0f, Room).appearAt, Is.EqualTo(2.5f).Within(Eps));
            Assert.That(WaveSpawnPlanner.PlanAuto(in group, 2, 0f, Room).appearAt, Is.EqualTo(2.5f).Within(Eps));
        }

        /// <summary>음수로 저작된 시각은 0으로 본다. 웨이브가 시작되기 전은 없다.</summary>
        [Test]
        public void NegativeAppearAt_FoldsToZero()
        {
            WaveSpawnEntry group = WaveSpawnEntry.Auto(EnemyRole.Melee, SpawnSide.Right, -3f);

            Assert.That(WaveSpawnPlanner.PlanAuto(in group, 0, 0f, Room).appearAt, Is.Zero);
        }
    }
}
