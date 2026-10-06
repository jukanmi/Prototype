using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.Tests
{
    /// <summary>
    /// 아레나 씬이 <b>보드에 자리 붙은 조우를 들고 있는지</b> 본다.
    ///
    /// 아레나가 자기 디렉터를 들고 돌던 시절에는 이 검사가 그 컴포넌트를 셌다. 지금은
    /// 웨이브 방과 똑같이 <see cref="StageWaveBoard"/> 하나가 목록을 들고 <see cref="StageDirector"/>
    /// 하나가 돈다 — 아레나라는 사실은 조우에 <see cref="EncounterSite"/>가 붙어 있다는 것뿐이다.
    ///
    /// 배선이 빠지면 증상이 조용하다. 자리가 없으면 카메라 락이 한 점으로 접히고 문이 안 닫혀서
    /// <b>"그냥 지나가지는 방"</b>으로만 보이고, 내용이 없으면 문만 닫혔다 열린다.
    /// 둘 다 화면에서 원인이 안 읽히므로 <b>이 검사가 유일한 그물</b>이다.
    ///
    /// <b>씬을 열었다 닫는 테스트</b>라 느리다. <see cref="StageWaveWiringTests"/>와 같은 이유로 값은 한다.
    ///
    /// 계획서: docs/Stage_Encounter_Unification_Plan.md (5항 6 · 7단계)
    /// </summary>
    public class ArenaEncounterWiringTests
    {
        private static readonly string[] ArenaScenes =
        {
            "Assets/Scenes/Level/Stage_02.unity",
            "Assets/Scenes/Level/Stage_05.unity",
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

        /// <summary>씬 순서와 스테이지 번호가 짝이 맞아야 아래 검사가 엉뚱한 애셋과 대조하지 않는다.</summary>
        [Test]
        public void ArenaScenes_MatchTheArenaStages()
        {
            Assert.That(ArenaScenes.Length, Is.EqualTo(EncounterAssetPaths.ArenaStages.Length));
        }

        /// <summary>
        /// 방 하나에 보드 하나다. 둘이면 디렉터가 어느 쪽을 집을지가 <b>씬의 오브젝트 순서로</b>
        /// 갈린다 — 목록을 고쳤는데 아무것도 안 바뀌는 종류의 증상이 된다.
        /// </summary>
        [Test]
        public void EveryArenaScene_HasExactlyOneBoard()
        {
            foreach (string scene in ArenaScenes)
            {
                Open(scene);

                StageWaveBoard[] boards = Object.FindObjectsByType<StageWaveBoard>(
                    FindObjectsInactive.Include);

                Assert.That(boards.Length, Is.EqualTo(1), $"{scene}: 보드가 {boards.Length}개다");
            }
        }

        /// <summary>목록 길이가 곧 아레나 개수다. 개수를 정수로 따로 적는 칸은 없다(계획서 2.2).</summary>
        [Test]
        public void EveryArenaScene_CarriesItsRounds()
        {
            for (int s = 0; s < ArenaScenes.Length; s++)
            {
                int stage = EncounterAssetPaths.ArenaStages[s];
                StageWaveBoard board = OpenAndFind(ArenaScenes[s]);

                int expected = EncounterAssetPaths.LoadRounds(stage).Count;
                Assert.That(board.WaveCount, Is.EqualTo(expected),
                            $"{ArenaScenes[s]}: 조우가 {board.WaveCount}개 (라운드 애셋은 {expected}개)");

                for (int i = 0; i < expected; i++)
                {
                    string path = EncounterAssetPaths.RoundPathFor(stage, i + 1);
                    var wanted = AssetDatabase.LoadAssetAtPath<WaveAsset>(path);

                    Assert.That(wanted, Is.Not.Null, $"{path} 가 없다");
                    Assert.That(board.WaveAt(i), Is.SameAs(wanted),
                                $"{ArenaScenes[s]} {i}번 조우가 {path} 를 안 들고 있다");
                }
            }
        }

        /// <summary>
        /// 아레나 조우는 전부 자리를 들고 있어야 하고, 그 자리는 <b>폭이 있어야</b> 한다.
        ///
        /// 폭이 없으면 구간이 한 점이라 카메라가 그 점에 붙는다. 자리가 아예 없으면
        /// 벽이 어디인지를 못 풀어서 적이 방 규칙으로 나온다 — 아레나인데 벽에서 안 나온다.
        /// </summary>
        [Test]
        public void EveryArenaEncounter_HasASiteWithRealBounds()
        {
            foreach (string scene in ArenaScenes)
            {
                StageWaveBoard board = OpenAndFind(scene);

                for (int i = 0; i < board.WaveCount; i++)
                {
                    EncounterSite site = board.EncounterAt(i).site;

                    Assert.That(site, Is.Not.Null, $"{scene} {i}번 조우에 자리가 없다");
                    Assert.That(site.MaxX - site.MinX, Is.GreaterThan(1f),
                                $"{scene} {i}번 조우: 구간 폭이 없다");
                }
            }
        }

        /// <summary>
        /// 아레나는 <b>걸어 들어가야</b> 열린다. 시작 조건이 진입선이 아니면 앞 조우가 끝나는
        /// 순간 저 멀리 있는 방이 열려서, 플레이어가 도착하기도 전에 적이 벽에서 나온다.
        /// </summary>
        [Test]
        public void ArenaEncounters_OpenByCrossingTheLine()
        {
            foreach (string scene in ArenaScenes)
            {
                StageWaveBoard board = OpenAndFind(scene);

                for (int i = 0; i < board.WaveCount; i++)
                    Assert.That(board.EncounterAt(i).Trigger, Is.EqualTo(EncounterTrigger.CrossLine),
                                $"{scene} {i}번 조우가 진입선으로 안 열린다");
            }
        }

        /// <summary>
        /// 자리끼리 겹치면 안 된다. 겹치면 어느 자리에 있는지가 좌표로 안 갈리고,
        /// 진입선이 한 아레나에서 두 번 걸리거나 영영 안 걸린다.
        /// </summary>
        [Test]
        public void ArenaSites_DoNotOverlap()
        {
            foreach (string scene in ArenaScenes)
            {
                StageWaveBoard board = OpenAndFind(scene);

                for (int i = 0; i < board.WaveCount; i++)
                for (int j = i + 1; j < board.WaveCount; j++)
                {
                    EncounterSite a = board.EncounterAt(i).site, b = board.EncounterAt(j).site;
                    if (a == null || b == null) continue;

                    bool apart = a.MaxX <= b.MinX || b.MaxX <= a.MinX;
                    Assert.That(apart, Is.True, $"{scene}: {i}번과 {j}번 조우의 자리가 겹친다");
                }
            }
        }

        /// <summary>
        /// 자리가 목록 순서대로 오른쪽으로 가야 한다. 순서가 뒤집히면 플레이어가 <b>2번 자리를
        /// 먼저 밟는데</b> 1번이 아직 안 끝나서, 걸어 들어간 방이 조용히 아무 일도 안 한다.
        /// </summary>
        [Test]
        public void ArenaSites_RunLeftToRight()
        {
            foreach (string scene in ArenaScenes)
            {
                StageWaveBoard board = OpenAndFind(scene);

                for (int i = 1; i < board.WaveCount; i++)
                {
                    EncounterSite prev = board.EncounterAt(i - 1).site;
                    EncounterSite here = board.EncounterAt(i).site;
                    if (prev == null || here == null) continue;

                    Assert.That(here.MinX, Is.GreaterThanOrEqualTo(prev.MaxX),
                                $"{scene}: {i}번 조우가 {i - 1}번보다 왼쪽에 있다");
                }
            }
        }

        /// <summary>
        /// 보드가 스스로 잡아내는 저작 실수가 하나도 없어야 한다.
        /// 자리 없는 진입선 조건은 여기서만 잡힌다 — 런타임은 앞 조우 뒤로 접어 버린다.
        /// </summary>
        [Test]
        public void EveryBoard_IsClean()
        {
            foreach (string scene in ArenaScenes)
            {
                StageWaveBoard board = OpenAndFind(scene);

                foreach (BoardIssue issue in board.Issues())
                    Assert.Fail($"{scene}: {issue.Describe()}");
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
