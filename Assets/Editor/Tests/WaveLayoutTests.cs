using System.Collections.Generic;
using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 자동 배치의 <b>순번</b>. 한 줄이 한 기가 되면서 줄 자체는 자기가 몇 번째인지 모른다.
    ///
    /// 이 순번이 좌우 교대와 깊이 줄 로테이션의 입력이라, 틀리면 적이 겹쳐 선다.
    /// 그리고 그건 <b>눈으로 못 센다</b> — 적이 여섯이면 화면만 봐서는 둘이 같은 자리인지 알 수 없고,
    /// 증상은 "가끔 적이 하나 덜 나온 것 같다" 정도로만 보인다.
    ///
    /// 계획서: docs/Wave_Authoring_Refactor_Plan.md (3.1)
    /// </summary>
    public class WaveLayoutTests
    {
        /// <summary>돌진전사와 마법사의 규칙이 이 값을 기준으로 갈린다. 0만 보면 둘 다 안 타진다.</summary>
        private static readonly float[] PlayerDepths = { 0f, 1.1f, -2.4f };

        // ── 순번 매기기 ─────────────────────────────────

        /// <summary>순번은 역할별로 센다. 통짜로 세면 돌진전사의 0번이 앞 역할의 마릿수에 밀린다.</summary>
        [Test]
        public void AutoLaneIndices_CountPerRole()
        {
            var entries = new[]
            {
                WaveSpawnEntry.Auto(EnemyRole.Melee),
                WaveSpawnEntry.Auto(EnemyRole.Melee),
                WaveSpawnEntry.Auto(EnemyRole.Charger),
                WaveSpawnEntry.Auto(EnemyRole.Melee),
                WaveSpawnEntry.Auto(EnemyRole.Charger),
            };

            Assert.That(WaveLayout.AutoLaneIndices(entries), Is.EqualTo(new[] { 0, 1, 0, 2, 1 }));
        }

        /// <summary>
        /// 씬 지점을 쓰는 줄은 순번을 <b>안 먹는다</b>. 먹게 두면 지점 한 줄을 끼워 넣는 것만으로
        /// 나머지 자동 배치가 통째로 밀리고, 저작자는 건드리지도 않은 줄이 왜 움직였는지 모른다.
        /// </summary>
        [Test]
        public void AutoLaneIndices_PointRowsConsumeNothing()
        {
            var entries = new[]
            {
                WaveSpawnEntry.Auto(EnemyRole.Melee),
                WaveSpawnEntry.At(EnemyRole.Melee, "굴_좌", 1f, SpawnMotion.Burrow),
                WaveSpawnEntry.Auto(EnemyRole.Melee),
            };

            Assert.That(WaveLayout.AutoLaneIndices(entries), Is.EqualTo(new[] { 0, -1, 1 }));
        }

        /// <summary>지점을 쓰겠다면서 이름이 빈 줄은 자동 배치로 떨어진다. 그러면 순번도 받아야 한다.</summary>
        [Test]
        public void AutoLaneIndices_MalformedPointRowFallsBackToAuto()
        {
            var broken = WaveSpawnEntry.Auto(EnemyRole.Melee);
            broken.origin = SpawnOrigin.Point;

            var entries = new[] { WaveSpawnEntry.Auto(EnemyRole.Melee), broken };

            Assert.That(WaveLayout.AutoLaneIndices(entries), Is.EqualTo(new[] { 0, 1 }),
                        "자동으로 떨어진 줄이 순번을 못 받아 0번에 겹친다");
        }

        /// <summary>
        /// 이름이 표에 있는지는 애셋만 봐서 알 수 없다. 못 찾아 자동으로 떨어질 줄을
        /// <b>부르는 쪽이 알려 준다</b> — 안 그러면 그 줄이 0번으로 몰려 멀쩡한 0번과 겹친다.
        /// </summary>
        [Test]
        public void AutoLaneIndices_TakeTheCallersVerdict()
        {
            var entries = new[]
            {
                WaveSpawnEntry.Auto(EnemyRole.Melee),
                WaveSpawnEntry.At(EnemyRole.Melee, "굴_없음"),
                WaveSpawnEntry.At(EnemyRole.Melee, "굴_있음"),
            };

            // 두 번째 줄은 지점을 못 찾아 자동으로 떨어졌다.
            var atPoint = new[] { false, false, true };

            Assert.That(WaveLayout.AutoLaneIndices(entries, atPoint), Is.EqualTo(new[] { 0, 1, -1 }));
        }

        /// <summary>길이가 안 맞는 판정표는 무시하고 저작값을 그대로 믿는다.</summary>
        [Test]
        public void AutoLaneIndices_IgnoreMismatchedFlags()
        {
            var entries = new[]
            {
                WaveSpawnEntry.Auto(EnemyRole.Melee),
                WaveSpawnEntry.At(EnemyRole.Melee, "굴"),
            };

            Assert.That(WaveLayout.AutoLaneIndices(entries, new[] { true }),
                        Is.EqualTo(new[] { 0, -1 }));
        }

        [Test]
        public void AutoLaneIndices_HandlesEmptyAndNull()
        {
            Assert.That(WaveLayout.AutoLaneIndices(null), Is.Empty);
            Assert.That(WaveLayout.AutoLaneIndices(new WaveSpawnEntry[0]), Is.Empty);
        }

        // ── 겹치지 않는가 ───────────────────────────────

        /// <summary>
        /// 실제로 구운 웨이브를 전부 배치해 보고 <b>두 기가 같은 자리에 서지 않는지</b> 본다.
        ///
        /// 1-B가 이 검사의 이유다. 강화 전사 1기와 전사 2기가 예전에는 두 묶음이었고,
        /// 묶음마다 순번을 따로 세서 두 0번이 같은 정착 지점을 받았다. 등장 시각만 다를 뿐
        /// 걸어가는 목적지가 같아서, 먼저 온 쪽에 막힌 뒤쪽이 도착 판정을 못 받고
        /// 걷기 제한 시간까지 밀린다.
        /// </summary>
        [Test]
        public void EveryBakedWave_PlacesEnemiesApart()
        {
            int checkedWaves = 0;

            foreach (int stage in EncounterAssetPaths.WaveStages)
            foreach (WaveAsset wave in EncounterAssetPaths.Load(stage))
            {
                checkedWaves++;

                foreach (float playerZ in PlayerDepths)
                {
                    var seen = new HashSet<string>();

                    foreach (SpawnPlacement place in Place(wave, playerZ))
                        Assert.That(seen.Add($"{place.entryPoint.x:F3},{place.entryPoint.z:F3}"),
                                    Is.True, $"{wave.name}({wave.label}) playerZ={playerZ}: 두 기가 같은 자리에 선다");
                }
            }

            // 조건이 잘못 좁혀져 전부 건너뛰면 이 테스트는 아무것도 안 보면서 통과한다.
            Assert.That(checkedWaves, Is.EqualTo(9), "확인한 웨이브 수가 표와 다르다");
        }

        /// <summary>돌진전사는 플레이어와 같은 깊이 줄에 선다. 깊이 회피를 강제하는 규칙이다.</summary>
        [Test]
        public void FirstChargerOfAWave_SharesThePlayersLane()
        {
            var entries = new[] { WaveSpawnEntry.Auto(EnemyRole.Charger) };
            int[] lanes = WaveLayout.AutoLaneIndices(entries);

            SpawnPlacement place = WaveSpawnPlanner.PlanAuto(in entries[0], lanes[0], 1.4f);

            Assert.That(place.entryPoint.z, Is.EqualTo(1.4f).Within(0.0001f));
        }

        // ── 도우미 ──────────────────────────────────────

        private static List<SpawnPlacement> Place(WaveAsset wave, float playerZ)
        {
            WaveSpawnEntry[] spawns = wave.spawns;
            int[] lanes = WaveLayout.AutoLaneIndices(spawns);

            var placements = new List<SpawnPlacement>();

            for (int i = 0; i < spawns.Length; i++)
                placements.Add(WaveSpawnPlanner.PlanAuto(in spawns[i], lanes[i], playerZ));

            return placements;
        }
    }
}
