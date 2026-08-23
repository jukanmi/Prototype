using NUnit.Framework;
using Prototype;

namespace Prototype.Tests
{
    /// <summary>
    /// 웨이브 스테이지의 배치표. <b>레벨 디자인 문서를 그대로 옮겨 적은 것</b>이라
    /// 여기가 곧 기획서의 사본이다 — 숫자를 손대면 이 테스트가 먼저 깨진다.
    ///
    /// 2 · 5스테이지는 아레나라 여기 없다. 그쪽은 <see cref="ArenaRoundCatalogTests"/>가 본다.
    /// </summary>
    public class StageWaveCatalogTests
    {
        // ── 경로의 형태 ─────────────────────────────────

        [Test]
        public void FiveStages_WithArenasAtTwoAndFive()
        {
            Assert.That(StageWaveCatalog.StageCount, Is.EqualTo(5));
            Assert.That(StageWaveCatalog.ArenaStageNumber, Is.EqualTo(2));
            Assert.That(StageWaveCatalog.BossStageNumber, Is.EqualTo(5));
        }

        [Test]
        public void ArenaStages_AreNotWaveStages()
        {
            Assert.That(StageWaveCatalog.IsWaveStage(1), Is.True);
            Assert.That(StageWaveCatalog.IsWaveStage(2), Is.False, "아레나 방이 웨이브로 잡혔다");
            Assert.That(StageWaveCatalog.IsWaveStage(3), Is.True);
            Assert.That(StageWaveCatalog.IsWaveStage(4), Is.True);
            Assert.That(StageWaveCatalog.IsWaveStage(5), Is.False, "보스 방이 웨이브로 잡혔다");

            Assert.That(StageWaveCatalog.IsArenaStage(2), Is.True);
            Assert.That(StageWaveCatalog.IsArenaStage(5), Is.True);
        }

        /// <summary>
        /// 아레나 번호를 주면 <b>빈 배열</b>이어야 한다. 조용히 다른 스테이지의 표를 돌려주면
        /// 아레나에 웨이브가 겹쳐 나오고, 화면만 봐서는 어디서 온 적인지 알 수가 없다.
        /// </summary>
        [Test]
        public void ArenaStages_HaveNoWaves()
        {
            Assert.That(StageWaveCatalog.For(StageWaveCatalog.ArenaStageNumber), Is.Empty);
            Assert.That(StageWaveCatalog.For(StageWaveCatalog.BossStageNumber), Is.Empty);
        }

        [Test]
        public void EveryWaveStage_HasThreeWaves()
        {
            foreach (int stage in new[] { 1, 3, 4 })
                Assert.That(StageWaveCatalog.For(stage).Length, Is.EqualTo(3), $"스테이지 {stage}");
        }

        /// <summary>
        /// 표를 그대로 돌려주면 디렉터가 인스펙터에서 만진 값이 원본을 오염시킨다.
        /// <c>GameManager.DefaultStages</c>가 사본을 주는 것과 같은 이유다.
        /// </summary>
        [Test]
        public void For_ReturnsFreshCopies()
        {
            WaveDefinition[] a = StageWaveCatalog.For(1);
            WaveDefinition[] b = StageWaveCatalog.For(1);

            Assert.That(a, Is.Not.SameAs(b));
            Assert.That(a[0], Is.Not.SameAs(b[0]));

            a[0].attackTokens = 6;
            Assert.That(StageWaveCatalog.For(1)[0].attackTokens, Is.Not.EqualTo(6), "표 원본이 오염됐다");
        }

        [Test]
        public void OutOfRangeStage_ClampsToBothEnds()
        {
            Assert.That(StageWaveCatalog.For(0)[0].label, Is.EqualTo(StageWaveCatalog.For(1)[0].label));
            // 5는 아레나라 빈 배열이다. 웨이브 표의 마지막은 4번이다.
            Assert.That(StageWaveCatalog.For(99), Is.Empty);
        }

        [Test]
        public void EveryWave_HasEnemies_AndALabel()
        {
            foreach (int stage in new[] { 1, 3, 4 })
                foreach (WaveDefinition wave in StageWaveCatalog.For(stage))
                {
                    Assert.That(wave.TotalSpawnCount, Is.GreaterThan(0), $"스테이지 {stage}에 빈 웨이브가 있다");
                    Assert.That(wave.label, Is.Not.Empty, $"스테이지 {stage}에 이름 없는 웨이브가 있다");
                }
        }

        // ── 역할군 도입 순서 = 학습 순서 ─────────────────

        /// <summary>1스테이지는 전사만. 변수를 빼야 기본 타격감에 집중시킬 수 있다.</summary>
        [Test]
        public void Stage1_IsMeleeOnly()
        {
            foreach (WaveDefinition wave in StageWaveCatalog.For(1))
            {
                Assert.That(wave.CountOf(EnemyRole.Charger), Is.Zero, "1스테이지에 돌진전사가 있다");
                Assert.That(wave.CountOf(EnemyRole.Ranged), Is.Zero, "1스테이지에 마법사가 있다");
            }
        }

        /// <summary>
        /// 3스테이지부터는 세 역할군이 한 웨이브에 다 나온다.
        /// 돌진전사·마법사를 처음 만나는 자리는 2스테이지(아레나)다.
        /// </summary>
        [Test]
        public void Stage3_FinalWave_MixesAllThreeRoles()
        {
            WaveDefinition last = StageWaveCatalog.For(3)[2];

            Assert.That(last.CountOf(EnemyRole.Melee), Is.EqualTo(2));
            Assert.That(last.CountOf(EnemyRole.Charger), Is.EqualTo(2));
            Assert.That(last.CountOf(EnemyRole.Ranged), Is.EqualTo(2));
        }

        // ── 표의 숫자 (기획서 사본) ──────────────────────

        [Test]
        public void Stage1_MatchesDesign()
        {
            WaveDefinition[] w = StageWaveCatalog.For(1);

            Assert.That(w[0].CountOf(EnemyRole.Melee), Is.EqualTo(3), "1-1 전사 3기");
            Assert.That(w[1].CountOf(EnemyRole.Melee), Is.EqualTo(4), "1-2 전사 4기 포위");
            Assert.That(w[2].CountOf(EnemyRole.Melee), Is.EqualTo(3), "1-B 강화 1 + 전사 2");
        }

        [Test]
        public void Stage3_MatchesDesign()
        {
            WaveDefinition[] w = StageWaveCatalog.For(3);

            Assert.That(w[0].CountOf(EnemyRole.Charger), Is.EqualTo(2));
            Assert.That(w[0].CountOf(EnemyRole.Ranged), Is.EqualTo(1));

            Assert.That(w[1].CountOf(EnemyRole.Melee), Is.EqualTo(3));
            Assert.That(w[1].CountOf(EnemyRole.Charger), Is.EqualTo(1));
            Assert.That(w[1].CountOf(EnemyRole.Ranged), Is.EqualTo(2));
        }

        [Test]
        public void Stage4_MatchesDesign()
        {
            WaveDefinition[] w = StageWaveCatalog.For(4);

            Assert.That(w[0].CountOf(EnemyRole.Charger), Is.EqualTo(3), "4-1 교차 돌진 3기");
            Assert.That(w[0].TotalSpawnCount, Is.EqualTo(3), "4-1에 다른 적이 섞이면 교차 돌진이 안 읽힌다");

            Assert.That(w[1].CountOf(EnemyRole.Ranged), Is.EqualTo(3));
            Assert.That(w[1].CountOf(EnemyRole.Melee), Is.EqualTo(3));

            Assert.That(w[2].CountOf(EnemyRole.Charger), Is.EqualTo(2));
            Assert.That(w[2].CountOf(EnemyRole.Ranged), Is.EqualTo(2));
        }

        // ── 강화 개체 · 지속 리젠 ────────────────────────

        /// <summary>1스테이지 보스 웨이브의 "강화 전사". 체력 벽 역할이라 표시가 붙어 있어야 한다.</summary>
        [Test]
        public void Stage1_BossWave_HasExactlyOneElite()
        {
            Assert.That(EliteCount(StageWaveCatalog.For(1)[2]), Is.EqualTo(1));
        }

        /// <summary>최종 웨이브의 엘리트 돌진 2기.</summary>
        [Test]
        public void Stage4_FinalWave_HasTwoEliteChargers()
        {
            WaveDefinition last = StageWaveCatalog.For(4)[2];

            Assert.That(EliteCount(last), Is.EqualTo(2));

            foreach (WaveSpawn spawn in last.spawns)
                if (spawn.elite)
                    Assert.That(spawn.role, Is.EqualTo(EnemyRole.Charger));
        }

        /// <summary>
        /// 지속 리젠은 <b>마지막 웨이브 스테이지의 마지막 웨이브</b>에만 붙는다.
        /// 다른 데 붙으면 스테이지가 늘어지기만 하고, 5스테이지는 보스방이라 웨이브가 없다.
        /// </summary>
        [Test]
        public void Reinforcements_OnlyOnTheVeryLastWave()
        {
            const int lastWaveStage = 4;

            foreach (int stage in new[] { 1, 3, 4 })
            {
                WaveDefinition[] waves = StageWaveCatalog.For(stage);

                for (int i = 0; i < waves.Length; i++)
                {
                    bool isTheOne = stage == lastWaveStage && i == waves.Length - 1;
                    Assert.That(waves[i].HasReinforcements, Is.EqualTo(isTheOne),
                                $"스테이지 {stage} 웨이브 {i + 1}");
                }
            }
        }

        /// <summary>
        /// 상한이 없으면 <b>이길 수 없는 방</b>이 된다. 리젠이 붙은 웨이브에서 가장 먼저 볼 값이다.
        /// </summary>
        [Test]
        public void Reinforcements_AreCapped()
        {
            WaveDefinition last = StageWaveCatalog.For(4)[2];

            Assert.That(last.reinforceCap, Is.GreaterThan(0));
            Assert.That(last.reinforceInterval, Is.GreaterThan(0f));
            Assert.That(last.reinforceRole, Is.EqualTo(EnemyRole.Melee),
                        "리젠은 전사다 — 돌진·마법사가 계속 나오면 회피가 안 된다");
        }

        // ── 동시 공격 제한 ───────────────────────────────

        /// <summary>
        /// 기획 기준 2~3기. 이 범위를 벗어나면 한쪽은 다구리가 되고, 다른 쪽은
        /// 여섯 마리가 서서 구경만 하는 방이 된다.
        /// </summary>
        [Test]
        public void EveryWave_AllowsTwoOrThreeSimultaneousAttackers()
        {
            foreach (int stage in new[] { 1, 3, 4 })
                foreach (WaveDefinition wave in StageWaveCatalog.For(stage))
                    Assert.That(wave.attackTokens, Is.InRange(2, 3), $"스테이지 {stage} — {wave.label}");
        }

        // ── 등장 시각 ───────────────────────────────────

        /// <summary>시차 등장은 순번이 커질수록 늦어야 한다. 뒤집히면 순차 등장이 동시 등장이 된다.</summary>
        [Test]
        public void AppearTimes_AreMonotonic()
        {
            foreach (int stage in new[] { 1, 3, 4 })
                foreach (WaveDefinition wave in StageWaveCatalog.For(stage))
                    foreach (WaveSpawn spawn in wave.spawns)
                        for (int i = 1; i < spawn.Count; i++)
                            Assert.That(spawn.AppearAt(i), Is.GreaterThanOrEqualTo(spawn.AppearAt(i - 1)));
        }

        /// <summary>1-1은 "순차 등장"이다. 간격이 0이면 그냥 3기가 한 번에 쏟아진다.</summary>
        [Test]
        public void Stage1_FirstWave_ArrivesOneAtATime()
        {
            WaveSpawn spawn = StageWaveCatalog.For(1)[0].spawns[0];

            Assert.That(spawn.interval, Is.GreaterThan(1f), "한 명씩 상대할 틈이 없다");
            Assert.That(spawn.LastAppearAt, Is.GreaterThan(spawn.AppearAt(0)));
        }

        private static int EliteCount(WaveDefinition wave)
        {
            int n = 0;
            foreach (WaveSpawn spawn in wave.spawns)
                if (spawn.elite) n += spawn.Count;
            return n;
        }
    }
}
