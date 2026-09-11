using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 웨이브 애셋 <b>타입</b>의 규약. 값을 어떻게 읽어 주는가만 본다.
    ///
    /// 실제로 구워진 아홉 개의 <b>내용</b>은 <see cref="WaveAssetContentTests"/>가 본다.
    /// 둘을 한 파일에 두면 기획 숫자를 고칠 때마다 타입 검사까지 같이 흔들린다.
    ///
    /// 계획서: docs/Wave_Authoring_Refactor_Plan.md (2.1)
    /// </summary>
    public class WaveAssetTests
    {
        private WaveAsset asset;

        [SetUp]
        public void SetUp() => asset = ScriptableObject.CreateInstance<WaveAsset>();

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(asset);
            asset = null;
        }

        /// <summary>인스펙터에서 애셋을 새로 만든 상태. 그대로도 굴러가야 한다.</summary>
        [Test]
        public void FreshAsset_IsEmptyButUsable()
        {
            Assert.That(asset.TotalSpawnCount, Is.Zero);
            Assert.That(asset.HasReinforcements, Is.False);
            Assert.That(asset.Advance, Is.EqualTo(WaveAdvance.AllCleared));
            Assert.That(asset.PointIds(), Is.Empty);
        }

        [Test]
        public void TotalSpawnCount_IsOnePerRow()
        {
            asset.spawns = new[]
            {
                WaveSpawnEntry.Auto(EnemyRole.Melee),
                WaveSpawnEntry.Auto(EnemyRole.Melee),
                WaveSpawnEntry.Auto(EnemyRole.Ranged),
            };

            Assert.That(asset.TotalSpawnCount, Is.EqualTo(3));
            Assert.That(asset.CountOf(EnemyRole.Melee), Is.EqualTo(2));
            Assert.That(asset.CountOf(EnemyRole.Ranged), Is.EqualTo(1));
            Assert.That(asset.CountOf(EnemyRole.Charger), Is.Zero);
        }

        /// <summary>간격과 상한 둘 다 있어야 리젠이다. 하나만 채우면 안 도는 것이 맞다.</summary>
        [Test]
        public void Reinforcements_NeedBothIntervalAndCap()
        {
            asset.reinforceInterval = 7f;
            Assert.That(asset.HasReinforcements, Is.False, "상한 없는 리젠은 이길 수 없는 방이 된다");

            asset.reinforceCap = 4;
            Assert.That(asset.HasReinforcements, Is.True);

            asset.reinforceInterval = 0f;
            Assert.That(asset.HasReinforcements, Is.False);
        }

        /// <summary>미구현 조건이 박혀 있어도 전멸로 접는다. 안 접으면 스테이지가 영영 안 끝난다.</summary>
        [Test]
        public void Advance_FoldsUnsupportedToAllCleared()
        {
            asset.advance = WaveAdvance.TargetKilled;

            Assert.That(asset.advance, Is.EqualTo(WaveAdvance.TargetKilled), "저작한 값은 남아야 한다");
            Assert.That(asset.Advance, Is.EqualTo(WaveAdvance.AllCleared));
        }

        [Test]
        public void AttackTokens_AreClampedIntoRange()
        {
            asset.attackTokens = 0;
            Assert.That(asset.AttackTokens, Is.EqualTo(1), "아무도 못 때리는 웨이브가 됐다");

            asset.attackTokens = 99;
            Assert.That(asset.AttackTokens, Is.EqualTo(6));
        }

        /// <summary>보드가 훑을 목록이다. 중복이 섞이면 같은 경고가 여러 번 뜬다.</summary>
        [Test]
        public void PointIds_ListsPointRowsWithoutDuplicates()
        {
            asset.spawns = new[]
            {
                WaveSpawnEntry.Auto(EnemyRole.Melee),
                WaveSpawnEntry.At(EnemyRole.Melee, "굴_좌"),
                WaveSpawnEntry.At(EnemyRole.Charger, " 굴_좌 "),
                WaveSpawnEntry.At(EnemyRole.Ranged, "굴_우"),
            };

            Assert.That(asset.PointIds(), Is.EqualTo(new[] { "굴_좌", "굴_우" }));
        }

        /// <summary>이름이 빈 지점 줄은 부르는 이름이 없다. 보드에 물어볼 것도 없다.</summary>
        [Test]
        public void PointIds_SkipsMalformedRows()
        {
            var broken = WaveSpawnEntry.Auto(EnemyRole.Melee);
            broken.origin = SpawnOrigin.Point;

            asset.spawns = new[] { broken };

            Assert.That(asset.PointIds(), Is.Empty);
        }

        [Test]
        public void NullSpawns_DoNotThrow()
        {
            asset.spawns = null;

            Assert.That(asset.TotalSpawnCount, Is.Zero);
            Assert.That(asset.CountOf(EnemyRole.Melee), Is.Zero);
            Assert.That(asset.PointIds(), Is.Empty);
        }
    }
}
