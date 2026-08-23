using NUnit.Framework;
using Prototype;

namespace Prototype.Tests
{
    /// <summary>
    /// 아레나 스테이지의 라운드 배치표. 2스테이지(벽 학습)와 5스테이지(보스) 둘 다 본다.
    ///
    /// 2스테이지에서 가장 중요한 항목은 <b>스폰 방향이 라운드마다 늘어나는가</b>이다.
    /// 이게 무너지면 아레나를 둘로 나눈 이유가 통째로 사라진다 — 마릿수만 늘어난 같은 싸움을
    /// 두 번 하게 되고, 플레이해서는 "2라운드가 좀 더 길다"로만 느껴져 알아채기가 대단히 어렵다.
    /// </summary>
    public class ArenaRoundCatalogTests
    {
        private const int Arena = StageWaveCatalog.ArenaStageNumber;   // 2
        private const int Boss = StageWaveCatalog.BossStageNumber;     // 5

        // ── 형태 ────────────────────────────────────────

        [Test]
        public void BothArenaStages_HaveTwoArenas()
        {
            Assert.That(ArenaRoundCatalog.ArenaCountOf(Arena), Is.EqualTo(2));
            Assert.That(ArenaRoundCatalog.ArenaCountOf(Boss), Is.EqualTo(2));
        }

        /// <summary>웨이브 방에 아레나가 잡히면 빌더가 엉뚱한 씬을 아레나로 굽는다.</summary>
        [Test]
        public void WaveStages_HaveNoArenas()
        {
            foreach (int stage in new[] { 1, 3, 4 })
                Assert.That(ArenaRoundCatalog.ArenaCountOf(stage), Is.Zero, $"스테이지 {stage}");
        }

        [Test]
        public void EveryRound_HasEnemies_AndALabel()
        {
            foreach (int stage in new[] { Arena, Boss })
                for (int arena = 1; arena <= ArenaRoundCatalog.ArenaCountOf(stage); arena++)
                {
                    ArenaRound round = ArenaRoundCatalog.For(stage, arena);

                    Assert.That(round.TotalSpawnCount, Is.GreaterThan(0), $"{stage}-{arena}R이 비어 있다");
                    Assert.That(round.label, Is.Not.Empty, $"{stage}-{arena}R에 이름이 없다");
                }
        }

        [Test]
        public void OutOfRangeArena_ClampsToBothEnds()
        {
            Assert.That(ArenaRoundCatalog.For(Arena, 0).label, Is.EqualTo(ArenaRoundCatalog.For(Arena, 1).label));
            Assert.That(ArenaRoundCatalog.For(Arena, 99).label, Is.EqualTo(ArenaRoundCatalog.For(Arena, 2).label));
        }

        [Test]
        public void For_ReturnsFreshCopies()
        {
            ArenaRound a = ArenaRoundCatalog.For(Arena, 1);
            ArenaRound b = ArenaRoundCatalog.For(Arena, 1);

            Assert.That(a, Is.Not.SameAs(b));

            a.attackTokens = 6;
            Assert.That(ArenaRoundCatalog.For(Arena, 1).attackTokens, Is.Not.EqualTo(6), "표 원본이 오염됐다");
        }

        // ── 2스테이지: 방향이 곧 난이도 ──────────────────

        /// <summary>
        /// 1라운드는 정면 하나. 벽에서 나온다는 규칙 자체를 가르치는 자리라
        /// 방향이 둘 이상이면 무엇을 배워야 하는지가 흐려진다.
        /// </summary>
        [Test]
        public void Round1_UsesOnlyTheFrontWall()
        {
            ArenaRound round = ArenaRoundCatalog.For(Arena, 1);

            Assert.That(round.WallCount, Is.EqualTo(1));
            Assert.That(round.UsesWall(SpawnWall.Front), Is.True);
        }

        /// <summary>2라운드는 좌우 양쪽. 전방만 보고 있으면 반대쪽이 열린다.</summary>
        [Test]
        public void Round2_UsesBothSideWalls()
        {
            ArenaRound round = ArenaRoundCatalog.For(Arena, 2);

            Assert.That(round.UsesWall(SpawnWall.Left), Is.True);
            Assert.That(round.UsesWall(SpawnWall.Right), Is.True);
        }

        /// <summary>
        /// <b>마릿수가 아니라 방향으로 올린다.</b> 이 순서가 뒤집히면
        /// 재배치 개입을 시험하는 압박이 라운드가 지나도 안 늘어난다.
        /// </summary>
        [Test]
        public void WallCount_GrowsWithEachRound()
        {
            int previous = 0;

            for (int arena = 1; arena <= ArenaRoundCatalog.ArenaCountOf(Arena); arena++)
            {
                int walls = ArenaRoundCatalog.For(Arena, arena).WallCount;
                Assert.That(walls, Is.GreaterThan(previous), $"{arena}R의 방향이 안 늘었다");
                previous = walls;
            }
        }

        [Test]
        public void Round1_MatchesDesign()
        {
            ArenaRound round = ArenaRoundCatalog.For(Arena, 1);

            Assert.That(round.CountOf(EnemyRole.Melee), Is.EqualTo(3));
            Assert.That(round.CountOf(EnemyRole.Charger), Is.EqualTo(2));
            Assert.That(round.CountOf(EnemyRole.Ranged), Is.Zero, "1R에 마법사가 섞이면 정면 회피 학습이 흐려진다");
        }

        /// <summary>
        /// 시너지 붕괴 검증을 몰아 둔 라운드. 세 역할군이 다 나와야 "동료를 잃은 채로
        /// 이 구성을 상대할 수 있는가"를 물을 수 있다.
        /// </summary>
        [Test]
        public void Round2_MixesAllThreeRoles()
        {
            ArenaRound round = ArenaRoundCatalog.For(Arena, 2);

            Assert.That(round.CountOf(EnemyRole.Ranged), Is.EqualTo(2));
            Assert.That(round.CountOf(EnemyRole.Melee), Is.EqualTo(3));
            Assert.That(round.CountOf(EnemyRole.Charger), Is.EqualTo(1));
        }

        /// <summary>마법사는 좌우로 갈라 세운다. 한쪽에 몰면 반대쪽이 안전지대가 된다.</summary>
        [Test]
        public void Round2_SplitsRangedAcrossBothWalls()
        {
            int left = 0, right = 0;

            foreach (RoundSpawn spawn in ArenaRoundCatalog.For(Arena, 2).spawns)
            {
                if (spawn.role != EnemyRole.Ranged) continue;

                if (spawn.wall == SpawnWall.Left) left += spawn.Count;
                if (spawn.wall == SpawnWall.Right) right += spawn.Count;
            }

            Assert.That(left, Is.GreaterThan(0));
            Assert.That(right, Is.GreaterThan(0));
        }

        // ── 5스테이지: 보스 ──────────────────────────────

        /// <summary>
        /// 미니 룸은 <b>손을 푸는 자리</b>다. 기존 Stage_Mini 그대로 적 하나.
        /// 여기서 소모전을 시키면 보스전이 실력이 아니라 남은 체력으로 갈린다.
        /// </summary>
        [Test]
        public void BossStage_WarmupIsASingleEnemy()
        {
            ArenaRound warmup = ArenaRoundCatalog.For(Boss, 1);

            Assert.That(warmup.TotalSpawnCount, Is.EqualTo(1));
            Assert.That(warmup.CountOf(EnemyRole.Boss), Is.Zero, "몸풀기 방에 보스가 있다");
        }

        /// <summary>보스방은 보스 하나뿐이다. 기존 Stage_Boss 그대로.</summary>
        [Test]
        public void BossStage_FinalArenaIsTheBossAlone()
        {
            ArenaRound round = ArenaRoundCatalog.For(Boss, 2);

            Assert.That(round.CountOf(EnemyRole.Boss), Is.EqualTo(1));
            Assert.That(round.TotalSpawnCount, Is.EqualTo(1),
                        "보스방에 잡몹이 섞이면 어느 예고가 누구 것인지 안 읽힌다");
        }

        /// <summary>보스는 웨이브 표에 절대 들어가면 안 된다 — 동시 등장 규칙이 그대로 먹힌다.</summary>
        [Test]
        public void Boss_NeverAppearsInWaveStages()
        {
            foreach (int stage in new[] { 1, 3, 4 })
                foreach (WaveDefinition wave in StageWaveCatalog.For(stage))
                    Assert.That(wave.CountOf(EnemyRole.Boss), Is.Zero, $"스테이지 {stage}");
        }

        /// <summary>보스도 벽에서 걸어 나온다. 방 가운데에 뿅 하고 나타나면 등장이 아니다.</summary>
        [Test]
        public void Boss_EntersFromAWall()
        {
            foreach (RoundSpawn spawn in ArenaRoundCatalog.For(Boss, 2).spawns)
                if (spawn.role == EnemyRole.Boss)
                    Assert.That(spawn.wall, Is.EqualTo(SpawnWall.Front));
        }

        /// <summary>
        /// 등장 후 멈춰 서는 시간. 여기서 플레이어가 거리를 다시 잡는다 —
        /// 돌진전사보다 길어야 보스의 등장이 연출로 읽힌다.
        /// </summary>
        [Test]
        public void Boss_PausesLongerThanACharger()
        {
            Assert.That(ArenaSpawnPlanner.HoldFor(EnemyRole.Boss),
                        Is.GreaterThan(ArenaSpawnPlanner.HoldFor(EnemyRole.Charger)));
        }

        // ── 동시 공격 제한 ───────────────────────────────

        [Test]
        public void EveryRound_AllowsTwoOrThreeSimultaneousAttackers()
        {
            foreach (int stage in new[] { Arena, Boss })
                for (int arena = 1; arena <= ArenaRoundCatalog.ArenaCountOf(stage); arena++)
                    Assert.That(ArenaRoundCatalog.For(stage, arena).attackTokens,
                                Is.InRange(2, 3), $"{stage}-{arena}R");
        }

        /// <summary>돌진전사는 늦게 들어온다. 라운드 시작과 동시에 돌진하면 읽을 시간이 없다.</summary>
        [Test]
        public void Chargers_ArriveAfterTheRoundHasStarted()
        {
            foreach (int stage in new[] { Arena, Boss })
                for (int arena = 1; arena <= ArenaRoundCatalog.ArenaCountOf(stage); arena++)
                    foreach (RoundSpawn spawn in ArenaRoundCatalog.For(stage, arena).spawns)
                        if (spawn.role == EnemyRole.Charger)
                            Assert.That(spawn.delay, Is.GreaterThan(1f),
                                        $"{stage}-{arena}R의 돌진전사가 너무 이르다");
        }
    }
}
