using System.Collections.Generic;
using NUnit.Framework;
using Prototype;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 아레나 라운드 애셋. <b>기획서의 사본</b>이다.
    ///
    /// 방 쪽 <see cref="WaveAssetContentTests"/>와 같은 역할인데, 아레나에는 검사가 하나 더 있다.
    /// <b>두 줄이 한 자리에 겹치지 않는지</b>를 실제 배치를 풀어서 본다 — 벽면 위 자리가
    /// 런타임 계산에서 저작 데이터로 내려오면서, 이제 겹치는 것이 저작 실수로 가능해졌다.
    ///
    /// 계획서: docs/Stage_Encounter_Unification_Plan.md (5항 3 · 7단계)
    /// </summary>
    public class RoundAssetContentTests
    {
        private const float Eps = 0.0001f;

        /// <summary>아레나 구간 하나. 배치를 풀어 보는 용도라 값 자체는 아무 숫자여도 된다.</summary>
        private const float MinX = 12f;
        private const float MaxX = 24f;

        // ── 두 줄이 한 자리에 서지 않는가 ───────────────

        /// <summary>
        /// 같은 벽에서 <b>같은 시각에</b> 나오는 두 줄이 같은 자리에 서면 안 된다.
        ///
        /// 겹치면 둘이 서로 밀어내며 튀어나오고, 한쪽이 벽 밖으로 밀려 콜라이더에 낀다.
        /// 화면으로는 "적 하나가 안 나왔다"로만 보인다 — 원인에서 가장 먼 종류의 증상이다.
        ///
        /// 벽면 위 자리를 런타임이 순번으로 계산하던 시절에는 겹칠 수가 없었다.
        /// 그 셈이 저작 데이터로 내려오면서 이 실수가 <b>처음으로 가능해졌다.</b>
        /// </summary>
        [Test]
        public void NoTwoRows_ShareASpot()
        {
            foreach (WaveAsset round in All())
            {
                List<SpawnPlacement> placed = PlanBaked(round);

                for (int i = 0; i < placed.Count; i++)
                for (int j = i + 1; j < placed.Count; j++)
                {
                    if (placed[i].wall != placed[j].wall) continue;
                    if (Mathf.Abs(placed[i].appearAt - placed[j].appearAt) > Eps) continue;

                    float gap = Vector3.Distance(placed[i].entryPoint, placed[j].entryPoint);

                    Assert.That(gap, Is.GreaterThan(0.5f),
                                $"{round.name}: {i}번과 {j}번 줄이 같은 자리에 선다");
                }
            }
        }

        /// <summary>벽면 위 자리가 양 끝에 붙으면 안 된다. 모서리에 끼면 걸어 나오다 막힌다.</summary>
        [Test]
        public void EveryRow_SitsInsideItsWall()
        {
            foreach (WaveAsset round in All())
                foreach (WaveSpawnEntry entry in round.spawns)
                {
                    Assert.That(entry.AlongWall, Is.GreaterThan(0f), $"{round.name}: 벽 끝에 붙었다");
                    Assert.That(entry.AlongWall, Is.LessThan(1f), $"{round.name}: 벽 끝에 붙었다");
                }
        }

        // ── 있어야 할 것 ────────────────────────────────

        [Test]
        public void EveryArenaStage_HasTwoRounds()
        {
            foreach (int stage in EncounterAssetPaths.ArenaStages)
                Assert.That(EncounterAssetPaths.LoadRounds(stage).Count, Is.EqualTo(2), $"스테이지 {stage}");
        }

        [Test]
        public void EveryRound_HasEnemiesAndALabel()
        {
            foreach (WaveAsset round in All())
            {
                Assert.That(round.TotalSpawnCount, Is.GreaterThan(0), $"{round.name}: 빈 라운드");
                Assert.That(round.label, Is.Not.Empty, $"{round.name}: 이름 없는 라운드");
            }
        }

        /// <summary>라운드의 모든 줄은 벽에서 나온다. 아레나는 사방이 막힌 방이다.</summary>
        [Test]
        public void EveryRow_ComesFromAWall()
        {
            foreach (WaveAsset round in All())
                foreach (WaveSpawnEntry entry in round.spawns)
                {
                    Assert.That(entry.motion, Is.EqualTo(SpawnMotion.FromWall), round.name);
                    Assert.That(entry.wall, Is.Not.EqualTo(SpawnWall.None), round.name);
                }
        }

        // ── 기획의 숫자 ─────────────────────────────────

        /// <summary>
        /// <b>난이도는 마릿수가 아니라 방향으로 올린다.</b> 몹을 늘리면 그냥 오래 걸리지만,
        /// 방향을 늘리면 "재배치 개입이 의미가 있는가"라는 질문이 라운드마다 강해진다.
        /// </summary>
        [Test]
        public void Stage2_AddsAWallEachRound()
        {
            int arena = StageWaveCatalog.ArenaStageNumber;

            Assert.That(Load(arena, 1).WallCount, Is.EqualTo(1), "1R은 정면 한 방향");
            Assert.That(Load(arena, 2).WallCount, Is.GreaterThan(1), "2R에서 방향이 안 늘었다");
        }

        [Test]
        public void Stage2_FirstRound_UsesTheFrontWallOnly()
        {
            WaveAsset first = Load(StageWaveCatalog.ArenaStageNumber, 1);

            Assert.That(first.UsesWall(SpawnWall.Front), Is.True);
            Assert.That(first.CountOf(EnemyRole.Melee), Is.EqualTo(3));
            Assert.That(first.CountOf(EnemyRole.Charger), Is.EqualTo(2));
        }

        [Test]
        public void Stage2_SecondRound_SqueezesFromBothSides()
        {
            WaveAsset second = Load(StageWaveCatalog.ArenaStageNumber, 2);

            Assert.That(second.UsesWall(SpawnWall.Left), Is.True);
            Assert.That(second.UsesWall(SpawnWall.Right), Is.True);
            Assert.That(second.CountOf(EnemyRole.Ranged), Is.EqualTo(2),
                        "좌우 마법사가 시너지 붕괴 검증의 핵심이다");
        }

        /// <summary>미니 룸은 손을 푸는 자리다. 여기서 체력을 깎아 두면 보스전이 운이 된다.</summary>
        [Test]
        public void BossStage_WarmupIsOneEnemy()
        {
            Assert.That(Load(StageWaveCatalog.BossStageNumber, 1).TotalSpawnCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 보스방에 잡몹이 섞이면 어느 예고가 누구 것인지 안 읽힌다.
        /// 보스는 패턴을 여럿 들고 있어서 더 그렇다.
        /// </summary>
        [Test]
        public void BossStage_BossRoomHoldsOnlyTheBoss()
        {
            WaveAsset boss = Load(StageWaveCatalog.BossStageNumber, 2);

            Assert.That(boss.TotalSpawnCount, Is.EqualTo(1));
            Assert.That(boss.CountOf(EnemyRole.Boss), Is.EqualTo(1));
        }

        /// <summary>보스도 벽에서 걸어 나온다. 방 가운데에 뿅 하고 나타나면 등장이 아니다.</summary>
        [Test]
        public void Boss_EntersFromAWall()
        {
            foreach (WaveSpawnEntry entry in Load(StageWaveCatalog.BossStageNumber, 2).spawns)
                if (entry.role == EnemyRole.Boss)
                    Assert.That(entry.wall, Is.EqualTo(SpawnWall.Front));
        }

        [Test]
        public void EveryRound_AllowsTwoOrThreeSimultaneousAttackers()
        {
            foreach (WaveAsset round in All())
                Assert.That(round.AttackTokens, Is.InRange(2, 3), $"{round.name} — {round.label}");
        }

        // ── 도우미 ──────────────────────────────────────

        private static WaveAsset Load(int stage, int arena)
            => AssetDatabase.LoadAssetAtPath<WaveAsset>(EncounterAssetPaths.RoundPathFor(stage, arena));

        private static IEnumerable<WaveAsset> All()
        {
            foreach (int stage in EncounterAssetPaths.ArenaStages)
                foreach (WaveAsset round in EncounterAssetPaths.LoadRounds(stage))
                    yield return round;
        }

        /// <summary>저작한 줄들을 실제 좌표로 푼다. 벽면 자리와 시각을 줄이 직접 들고 있다.</summary>
        private static List<SpawnPlacement> PlanBaked(WaveAsset round)
        {
            var placements = new List<SpawnPlacement>();

            foreach (WaveSpawnEntry entry in round.spawns)
                placements.Add(ArenaSpawnPlanner.PlanFromWall(in entry, MinX, MaxX));

            return placements;
        }
    }
}
