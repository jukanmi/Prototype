using System.Collections.Generic;
using NUnit.Framework;
using Prototype;
using UnityEditor;

namespace Prototype.Tests
{
    /// <summary>
    /// 웨이브 애셋의 내용. <b>기획서를 그대로 옮겨 적은 것</b>이라 여기가 곧 기획서의 사본이다 —
    /// 숫자를 손대면 이 테스트가 먼저 깨진다.
    ///
    /// 예전에는 같은 역할을 <c>StageWaveCatalogTests</c>가 하드코딩 표를 읽어서 했다.
    /// 표가 사라지고 애셋이 원본이 되면서 <b>보는 대상만 바뀌었다.</b>
    /// 검사를 같이 버리면 인스펙터에서 숫자 하나 잘못 고친 것을 아무도 못 잡는다.
    ///
    /// 2 · 5스테이지는 아레나라 여기 없다. 그쪽은 <see cref="RoundAssetContentTests"/>가 본다.
    /// </summary>
    public class WaveAssetContentTests
    {
        // ── 있어야 할 것 ────────────────────────────────

        [Test]
        public void EveryWaveStage_HasThreeWaves()
        {
            foreach (int stage in EncounterAssetPaths.WaveStages)
                Assert.That(EncounterAssetPaths.Load(stage).Count, Is.EqualTo(3), $"스테이지 {stage}");
        }

        /// <summary>아레나 번호로는 애셋이 없어야 한다. 있으면 아레나에 웨이브가 겹쳐 나온다.</summary>
        [Test]
        public void ArenaStages_HaveNoWaveAssets()
        {
            Assert.That(EncounterAssetPaths.Load(StageWaveCatalog.ArenaStageNumber), Is.Empty);
            Assert.That(EncounterAssetPaths.Load(StageWaveCatalog.BossStageNumber), Is.Empty);
        }

        [Test]
        public void EveryWave_HasEnemiesAndALabel()
        {
            foreach (WaveAsset wave in All())
            {
                Assert.That(wave.TotalSpawnCount, Is.GreaterThan(0), $"{wave.name}: 빈 웨이브");
                Assert.That(wave.label, Is.Not.Empty, $"{wave.name}: 이름 없는 웨이브");
            }
        }

        // ── 역할군 도입 순서 = 학습 순서 ─────────────────

        /// <summary>1스테이지는 전사만. 변수를 빼야 기본 타격감에 집중시킬 수 있다.</summary>
        [Test]
        public void Stage1_IsMeleeOnly()
        {
            foreach (WaveAsset wave in EncounterAssetPaths.Load(1))
            {
                Assert.That(wave.CountOf(EnemyRole.Charger), Is.Zero, "1스테이지에 돌진전사가 있다");
                Assert.That(wave.CountOf(EnemyRole.Ranged), Is.Zero, "1스테이지에 마법사가 있다");
            }
        }

        /// <summary>
        /// 3스테이지부터는 세 역할군이 한 웨이브에 다 나온다.
        /// 돌진전사 · 마법사를 처음 만나는 자리는 2스테이지(아레나)다.
        /// </summary>
        [Test]
        public void Stage3_FinalWave_MixesAllThreeRoles()
        {
            WaveAsset last = EncounterAssetPaths.Load(3)[2];

            Assert.That(last.CountOf(EnemyRole.Melee), Is.EqualTo(2));
            Assert.That(last.CountOf(EnemyRole.Charger), Is.EqualTo(2));
            Assert.That(last.CountOf(EnemyRole.Ranged), Is.EqualTo(2));
        }

        /// <summary>보스는 웨이브에 절대 들어가면 안 된다 — 동시 등장 규칙이 그대로 먹힌다.</summary>
        [Test]
        public void Boss_NeverAppearsInWaves()
        {
            foreach (WaveAsset wave in All())
                Assert.That(wave.CountOf(EnemyRole.Boss), Is.Zero, wave.name);
        }

        // ── 표의 숫자 ───────────────────────────────────

        [Test]
        public void Stage1_MatchesDesign()
        {
            List<WaveAsset> w = EncounterAssetPaths.Load(1);

            Assert.That(w[0].CountOf(EnemyRole.Melee), Is.EqualTo(3), "1-1 전사 3기");
            Assert.That(w[1].CountOf(EnemyRole.Melee), Is.EqualTo(4), "1-2 전사 4기 포위");
            Assert.That(w[2].CountOf(EnemyRole.Melee), Is.EqualTo(3), "1-B 강화 1 + 전사 2");
        }

        [Test]
        public void Stage3_MatchesDesign()
        {
            List<WaveAsset> w = EncounterAssetPaths.Load(3);

            Assert.That(w[0].CountOf(EnemyRole.Charger), Is.EqualTo(2));
            Assert.That(w[0].CountOf(EnemyRole.Ranged), Is.EqualTo(1));

            Assert.That(w[1].CountOf(EnemyRole.Melee), Is.EqualTo(3));
            Assert.That(w[1].CountOf(EnemyRole.Charger), Is.EqualTo(1));
            Assert.That(w[1].CountOf(EnemyRole.Ranged), Is.EqualTo(2));
        }

        [Test]
        public void Stage4_MatchesDesign()
        {
            List<WaveAsset> w = EncounterAssetPaths.Load(4);

            Assert.That(w[0].CountOf(EnemyRole.Charger), Is.EqualTo(3), "4-1 교차 돌진 3기");
            Assert.That(w[0].TotalSpawnCount, Is.EqualTo(3),
                        "4-1에 다른 적이 섞이면 교차 돌진이 안 읽힌다");

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
            Assert.That(EliteCount(EncounterAssetPaths.Load(1)[2]), Is.EqualTo(1));
        }

        [Test]
        public void Stage4_FinalWave_HasTwoEliteChargers()
        {
            WaveAsset last = EncounterAssetPaths.Load(4)[2];

            Assert.That(EliteCount(last), Is.EqualTo(2));

            foreach (WaveSpawnEntry entry in last.spawns)
                if (entry.elite)
                    Assert.That(entry.role, Is.EqualTo(EnemyRole.Charger));
        }

        /// <summary>
        /// 지속 리젠은 <b>마지막 웨이브 스테이지의 마지막 웨이브</b>에만 붙는다.
        /// 다른 데 붙으면 스테이지가 늘어지기만 하고, 5스테이지는 보스방이라 웨이브가 없다.
        /// </summary>
        [Test]
        public void Reinforcements_OnlyOnTheVeryLastWave()
        {
            const int lastWaveStage = 4;

            foreach (int stage in EncounterAssetPaths.WaveStages)
            {
                List<WaveAsset> waves = EncounterAssetPaths.Load(stage);

                for (int i = 0; i < waves.Count; i++)
                {
                    bool isTheOne = stage == lastWaveStage && i == waves.Count - 1;
                    Assert.That(waves[i].HasReinforcements, Is.EqualTo(isTheOne),
                                $"스테이지 {stage} 웨이브 {i + 1}");
                }
            }
        }

        /// <summary>상한이 없으면 <b>이길 수 없는 방</b>이 된다. 리젠이 붙은 웨이브에서 가장 먼저 볼 값이다.</summary>
        [Test]
        public void Reinforcements_AreCapped()
        {
            WaveAsset last = EncounterAssetPaths.Load(4)[2];

            Assert.That(last.reinforceCap, Is.GreaterThan(0));
            Assert.That(last.reinforceInterval, Is.GreaterThan(0f));
            Assert.That(last.reinforceRole, Is.EqualTo(EnemyRole.Melee),
                        "리젠은 전사다 — 돌진 · 마법사가 계속 나오면 회피가 안 된다");
        }

        // ── 동시 공격 제한 ───────────────────────────────

        /// <summary>
        /// 기획 기준 2~3기. 이 범위를 벗어나면 한쪽은 다구리가 되고, 다른 쪽은
        /// 여섯 마리가 서서 구경만 하는 방이 된다.
        /// </summary>
        [Test]
        public void EveryWave_AllowsTwoOrThreeSimultaneousAttackers()
        {
            foreach (WaveAsset wave in All())
                Assert.That(wave.AttackTokens, Is.InRange(2, 3), $"{wave.name} — {wave.label}");
        }

        // ── 등장 시각 ───────────────────────────────────

        /// <summary>
        /// 같은 역할끼리는 저작 순서대로 늦게 나와야 한다. 뒤집히면 순차 등장이 동시 등장이 된다.
        /// 역할이 다르면 순서가 섞여도 된다 — 그건 배치 의도다.
        /// </summary>
        [Test]
        public void AppearTimes_RiseWithinEachRole()
        {
            foreach (WaveAsset wave in All())
            {
                var last = new Dictionary<EnemyRole, float>();

                foreach (WaveSpawnEntry entry in wave.spawns)
                {
                    if (last.TryGetValue(entry.role, out float previous))
                        Assert.That(entry.AppearAt, Is.GreaterThanOrEqualTo(previous),
                                    $"{wave.name}: {entry.role} 등장 순서가 뒤집혔다");

                    last[entry.role] = entry.AppearAt;
                }
            }
        }

        /// <summary>1-1은 "순차 등장"이다. 간격이 좁으면 그냥 3기가 한 번에 쏟아진다.</summary>
        [Test]
        public void Stage1_FirstWave_ArrivesOneAtATime()
        {
            WaveSpawnEntry[] spawns = EncounterAssetPaths.Load(1)[0].spawns;

            Assert.That(spawns.Length, Is.EqualTo(3));

            for (int i = 1; i < spawns.Length; i++)
                Assert.That(spawns[i].AppearAt - spawns[i - 1].AppearAt, Is.GreaterThan(1f),
                            "한 명씩 상대할 틈이 없다");
        }

        // ── 저작 위생 ───────────────────────────────────

        /// <summary>
        /// 굽던 시절의 흔적이 남아 있으면 안 된다. 표가 만들지 않는 애셋이 폴더에 있으면
        /// 씬 보드에 안 꽂힌 채로 방치되고, 나중에 누가 그걸 진짜라고 믿는다.
        /// </summary>
        [Test]
        public void FolderHasNoStrayAssets()
        {
            var known = new HashSet<string>();

            foreach (int stage in EncounterAssetPaths.WaveStages)
            {
                int count = EncounterAssetPaths.Load(stage).Count;
                for (int i = 0; i < count; i++) known.Add(EncounterAssetPaths.PathFor(stage, i));
            }

            foreach (string guid in AssetDatabase.FindAssets("t:WaveAsset",
                                                             new[] { EncounterAssetPaths.Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.That(known, Does.Contain(path), $"{path} 가 이름 규칙 밖에 있다");
            }
        }

        // ── 도우미 ──────────────────────────────────────

        private static IEnumerable<WaveAsset> All()
        {
            foreach (int stage in EncounterAssetPaths.WaveStages)
                foreach (WaveAsset wave in EncounterAssetPaths.Load(stage))
                    yield return wave;
        }

        private static int EliteCount(WaveAsset wave)
        {
            int n = 0;
            foreach (WaveSpawnEntry entry in wave.spawns)
                if (entry.elite) n++;
            return n;
        }
    }
}
