using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.Tests
{
    /// <summary>
    /// 웨이브 방이 <b>보드를 들고 있는지</b> 본다. 5단계의 회귀 방지 자리다.
    ///
    /// 디렉터가 하드코딩 표 대신 씬 보드를 읽게 되면서, 보드가 없는 씬은 웨이브가 하나도 안 돈다.
    /// 예전에는 번호로 조용히 폴백해서 <b>엉뚱한 스테이지의 적이 나왔고</b>(<c>c325639f</c>),
    /// 지금은 그 폴백을 없앤 대신 아무도 안 나온다. 어느 쪽이든 화면만 봐서는 원인을 못 짚는다.
    ///
    /// <b>씬을 열었다 닫는 테스트</b>라 느리다. <see cref="SceneCompositionTests"/>와 같은 이유로 값은 한다.
    ///
    /// 계획서: docs/Wave_Authoring_Refactor_Plan.md (5단계)
    /// </summary>
    public class StageWaveWiringTests
    {
        private static readonly string[] WaveScenes =
        {
            "Assets/Scenes/Level/Stage_01.unity",
            "Assets/Scenes/Level/Stage_03.unity",
            "Assets/Scenes/Level/Stage_04.unity",
        };

        private string reopen;

        [SetUp]
        public void SetUp() => reopen = SceneManager.GetActiveScene().path;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(reopen) && reopen != SceneManager.GetActiveScene().path)
                EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);
        }

        /// <summary>
        /// 씬 순서와 스테이지 번호가 짝이 맞아야 한다. 여기가 어긋나면 아래 검사 전부가
        /// 엉뚱한 표와 대조하게 된다.
        /// </summary>
        [Test]
        public void WaveScenes_MatchTheBakedStages()
        {
            Assert.That(WaveScenes.Length, Is.EqualTo(EncounterAssetPaths.WaveStages.Length));
        }

        [Test]
        public void EveryWaveScene_HasExactlyOneBoard()
        {
            for (int s = 0; s < WaveScenes.Length; s++)
            {
                Open(WaveScenes[s]);

                StageWaveBoard[] boards = Object.FindObjectsByType<StageWaveBoard>(
                    FindObjectsInactive.Include);

                Assert.That(boards.Length, Is.EqualTo(1),
                            $"{WaveScenes[s]}: 보드가 {boards.Length}개다");
            }
        }

        /// <summary>목록이 비면 그 스테이지는 시작하자마자 끝난다.</summary>
        [Test]
        public void EveryBoard_CarriesItsStagesWaves()
        {
            for (int s = 0; s < WaveScenes.Length; s++)
            {
                int stage = EncounterAssetPaths.WaveStages[s];
                StageWaveBoard board = OpenAndFind(WaveScenes[s]);

                int expected = EncounterAssetPaths.Load(stage).Count;
                Assert.That(board.WaveCount, Is.EqualTo(expected),
                            $"{WaveScenes[s]}: 웨이브가 {board.WaveCount}개 (표는 {expected}개)");

                for (int i = 0; i < expected; i++)
                {
                    string path = EncounterAssetPaths.PathFor(stage, i);
                    var wanted = AssetDatabase.LoadAssetAtPath<WaveAsset>(path);

                    Assert.That(board.WaveAt(i), Is.SameAs(wanted),
                                $"{WaveScenes[s]} {i}번 칸이 {path} 가 아니다");
                }
            }
        }

        /// <summary>
        /// 보드가 스스로 잡아내는 저작 실수가 하나도 없어야 한다.
        /// 지점 이름 오타는 컴파일에 안 걸리므로, 씬을 열어 훑는 이 자리가 유일한 그물이다.
        /// </summary>
        [Test]
        public void EveryBoard_IsClean()
        {
            for (int s = 0; s < WaveScenes.Length; s++)
            {
                StageWaveBoard board = OpenAndFind(WaveScenes[s]);

                foreach (BoardIssue issue in board.Issues())
                    Assert.Fail($"{WaveScenes[s]}: {issue.Describe()}");
            }
        }

        // ── 도우미 ──────────────────────────────────────

        private static void Open(string path)
            => EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        private static StageWaveBoard OpenAndFind(string path)
        {
            Open(path);

            StageWaveBoard[] boards = Object.FindObjectsByType<StageWaveBoard>(FindObjectsInactive.Include);
            Assert.That(boards, Is.Not.Empty, $"{path} 에 StageWaveBoard 가 없다");

            return boards[0];
        }
    }
}
