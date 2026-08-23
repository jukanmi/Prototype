using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Prototype.YG;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Prototype.Tests
{
    /// <summary>
    /// 원거리 스테이지 두 짝(Soft · Hard)의 구성.
    ///
    /// 씬은 빌더가 굽는 산출물이라 손으로 열어 보기 전에는 틀린 걸 알 수 없다.
    /// 실제로 여기서 잡히는 실수가 있었다 — 인스턴스의 EnemyData 오버라이드가 저장되지 않아
    /// Hard의 대마법사 넷이 전부 견습생 수치로 서 있었다. 화면상으로는 똑같이 생겼다.
    ///
    /// 씬은 <b>추가로 열고 닫는다.</b> 단독으로 열면 지금 작업 중인 씬이 날아간다.
    /// </summary>
    public class RangedStageCompositionTests
    {
        private const string SoftScene = "Assets/Scenes/Level/Stage_Soft.unity";
        private const string HardScene = "Assets/Scenes/Level/Stage_Hard.unity";

        private const string SoftDataPath = "Assets/Data/Enemy/Enemy_WizardSoft.asset";
        private const string HardDataPath = "Assets/Data/Enemy/Enemy_WizardHard.asset";

        /// <summary>방 오른쪽 벽. <see cref="Prototype.EditorTools.SceneLayoutBuilder"/>의 RoomHalfX와 같다.</summary>
        private const float RoomHalfX = 6f;
        private const float RoomHalfZ = 3f;

        private bool ignoredFailingMessages;

        /// <summary>
        /// 스테이지 씬을 추가로 열면 이미 열려 있던 씬과 Global Light 2D가 겹쳐
        /// URP가 "More than one global light..."를 <b>에러로</b> 찍는다. 씬 구성과는 무관한
        /// 소음인데, 테스트 프레임워크는 에러 로그 하나만 나와도 그 테스트를 실패로 본다.
        /// </summary>
        [SetUp]
        public void MuteAdditiveSceneLightNoise()
        {
            ignoredFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void RestoreLogAssert() => LogAssert.ignoreFailingMessages = ignoredFailingMessages;

        /// <summary>씬 하나의 적을 훑는다. 열었던 씬은 반드시 도로 닫는다.</summary>
        private static void WithEnemies(string path, System.Action<List<Enemy>> body)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null,
                $"씬이 없다: {path}. 'Prototype ▸ 스테이지 - 미니·원거리·보스 씬 만들기'를 돌릴 것");

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                var found = new List<Enemy>();
                foreach (GameObject root in scene.GetRootGameObjects())
                    found.AddRange(root.GetComponentsInChildren<Enemy>(true));

                body(found);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static EnemyData DataOf(Enemy enemy)
            => new SerializedObject(enemy).FindProperty("data").objectReferenceValue as EnemyData;

        // ── 구성 ────────────────────────────────────────

        /// <summary>
        /// Soft는 원거리만 있는 방이다. 근접이 섞이면 "거리를 좁히는 법"이라는 이 스테이지의
        /// 문제가 흐려진다 — 근접이 알아서 다가와 주기 때문이다.
        /// </summary>
        [Test]
        public void Soft_IsRangedOnly()
        {
            WithEnemies(SoftScene, enemies =>
            {
                Assert.That(enemies, Is.Not.Empty, "적이 하나도 없다");

                foreach (Enemy e in enemies)
                {
                    EnemyData data = DataOf(e);
                    Assert.That(data, Is.Not.Null, $"{e.name}: EnemyData가 비었다");
                    Assert.That(AssetDatabase.GetAssetPath(data), Is.EqualTo(SoftDataPath),
                        $"{e.name}: 견습생 데이터가 아니다");
                }
            });
        }

        /// <summary>
        /// Hard는 대마법사 + 근접이다. 마법사만 놓으면 전부 같은 방향으로 물러나 한 덩어리가 되고,
        /// 그러면 Soft를 숫자만 늘려 낸 것이 된다.
        /// </summary>
        [Test]
        public void Hard_MixesWizardsWithMelee()
        {
            WithEnemies(HardScene, enemies =>
            {
                int wizards = enemies.Count(e => AssetDatabase.GetAssetPath(DataOf(e)) == HardDataPath);
                int melee = enemies.Count(e => DataOf(e) != null && DataOf(e).basicProjectile == null);

                Assert.That(wizards, Is.GreaterThanOrEqualTo(2), "대마법사가 너무 적다");
                Assert.That(melee, Is.GreaterThanOrEqualTo(1), "붙는 몸이 없으면 그냥 사거리 긴 Soft다");
            });
        }

        /// <summary>난이도가 이름뿐이면 두 스테이지가 같은 방이다.</summary>
        [Test]
        public void Hard_HasMoreEnemiesThanSoft()
        {
            int soft = 0, hard = 0;

            WithEnemies(SoftScene, e => soft = e.Count);
            WithEnemies(HardScene, e => hard = e.Count);

            Assert.That(hard, Is.GreaterThan(soft), $"Soft {soft}명 / Hard {hard}명");
        }

        // ── 배치 ────────────────────────────────────────

        /// <summary>
        /// 벽 밖에 놓인 적은 밀려 들어오거나 낀 채로 시작한다.
        /// 플레이어는 x = -3에 서므로 적은 전부 오른쪽이어야 한다.
        /// </summary>
        [TestCase(SoftScene)]
        [TestCase(HardScene)]
        public void EveryEnemy_StandsInsideTheRoom_OnThePlayersRight(string path)
        {
            WithEnemies(path, enemies =>
            {
                foreach (Enemy e in enemies)
                {
                    Vector3 p = e.transform.position;

                    Assert.That(p.x, Is.GreaterThan(0f).And.LessThan(RoomHalfX), $"{e.name}: x = {p.x}");
                    Assert.That(p.z, Is.GreaterThan(-RoomHalfZ).And.LessThan(RoomHalfZ), $"{e.name}: z = {p.z}");
                    Assert.That(p.y, Is.EqualTo(0f).Within(0.0001f), $"{e.name}: 높이는 점프로만 생긴다");
                }
            });
        }

        /// <summary>같은 자리에 겹쳐 놓으면 물리가 서로 밀어내며 시작한다.</summary>
        [TestCase(SoftScene)]
        [TestCase(HardScene)]
        public void NoTwoEnemies_ShareASpot(string path)
        {
            WithEnemies(path, enemies =>
            {
                for (int i = 0; i < enemies.Count; i++)
                for (int j = i + 1; j < enemies.Count; j++)
                {
                    float gap = Vector3.Distance(enemies[i].transform.position, enemies[j].transform.position);
                    Assert.That(gap, Is.GreaterThan(1f),
                        $"{enemies[i].name}과 {enemies[j].name}이 {gap:0.##}만큼밖에 안 떨어져 있다");
                }
            });
        }

        /// <summary>
        /// 레이어가 Default로 남으면 충돌 매트릭스가 아군 · 적을 못 가른다 —
        /// 적의 화살이 적을 맞힌다. 씬에 새로 꽂은 인스턴스에서 특히 빠지기 쉽다.
        /// </summary>
        [TestCase(SoftScene)]
        [TestCase(HardScene)]
        public void EveryEnemy_IsOnTheEnemyLayers(string path)
        {
            WithEnemies(path, enemies =>
            {
                foreach (Enemy e in enemies)
                {
                    Assert.That(LayerMask.LayerToName(e.gameObject.layer), Is.EqualTo("EnemyHurtbox"), e.name);
                    Assert.That(e.BasicAttack, Is.Not.Null, $"{e.name}: 평타 히트박스가 안 물려 있다");
                    Assert.That(LayerMask.LayerToName(e.BasicAttack.gameObject.layer), Is.EqualTo("EnemyHitbox"), e.name);
                }
            });
        }

        // ── 진행 흐름 ────────────────────────────────────

        /// <summary>
        /// 등록하지 않으면 <see cref="BattleSceneController"/>의 씬 단독 실행 경로가 씬을 못 찾는다 —
        /// 재시작 버튼이 조용히 죽는다.
        /// </summary>
        [TestCase(SoftScene)]
        [TestCase(HardScene)]
        public void EveryStage_IsRegisteredInBuildSettings(string path)
        {
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path == path && s.enabled), Is.True,
                $"{path}가 Build Settings에 없거나 꺼져 있다");
        }

        /// <summary>구운 씬이 진행 순서에 없으면 게임에서 영영 안 나온다.</summary>
        [Test]
        public void BothStages_AreInTheDefaultRun()
        {
            Assert.That(GameManager.DefaultStages, Contains.Item(SceneNames.StageSoft));
            Assert.That(GameManager.DefaultStages, Contains.Item(SceneNames.StageHard));
        }

        /// <summary>쉬운 쪽이 먼저다. 순서가 뒤집히면 난이도 곡선이 거꾸로 간다.</summary>
        [Test]
        public void Soft_ComesBeforeHard()
        {
            var stages = new List<string>(GameManager.DefaultStages);

            Assert.That(stages.IndexOf(SceneNames.StageSoft),
                Is.LessThan(stages.IndexOf(SceneNames.StageHard)));
        }
    }
}
