using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Prototype.Tests
{
    /// <summary>
    /// GameManager 가 들고 있는 런 지도와 진행. 씬을 실제로 로드하지는 않고
    /// <b>어디로 가야 하는지</b>를 아는지만 본다. (옛 <c>GameManagerStageTests</c>의 자리)
    ///
    /// 전환 메서드는 SceneLoader 가 없으면 거절한다 — 테스트에는 SceneLoader 가 없다.
    /// 그 거절이 진행을 건드리지 않는지가 여기서 가장 중요한 항목이다. 들어가지도 못한 칸이
    /// "들어가 있음"으로 박히면 지도에서 아무 칸도 못 고르는 상태가 된다.
    /// </summary>
    public class GameManagerRunMapTests
    {
        private const int Seed = 4242;

        private GameObject host;
        private GameManager gm;
        private RunMapRecipe recipe;

        private static NodeCandidate N(string scene, MapNodeKind kind) => NodeCandidate.Of(scene, kind);

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("GameManager");
            gm = host.AddComponent<GameManager>();   // Awake 가 여기서 돈다

            recipe = Recipe(
                FloorRule.Of(1, 1, N("Stage_A", MapNodeKind.Battle)),
                FloorRule.Of(2, 2, N("Stage_B", MapNodeKind.Elite), N("Node_Shop", MapNodeKind.Shop)),
                FloorRule.Of(1, 1, N("Stage_Boss", MapNodeKind.Boss)));

            gm.Configure(recipe, Seed);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(recipe);
        }

        private static RunMapRecipe Recipe(params FloorRule[] floors)
        {
            var r = ScriptableObject.CreateInstance<RunMapRecipe>();
            r.floors = floors;
            return r;
        }

        private MapNode First => gm.Map.NodesOn(0)[0];

        // ── 런 열기 ─────────────────────────────────────

        [Test]
        public void StartNewRun_OpensRunAtFloorZero()
        {
            Assert.That(gm.StartNewRun(), Is.True);

            Assert.That(gm.IsRunActive, Is.True);
            Assert.That(gm.Map, Is.Not.Null);
            Assert.That(gm.Map.Seed, Is.EqualTo(Seed));
            Assert.That(gm.FloorNumber, Is.EqualTo(1));
            Assert.That(gm.FloorCount, Is.EqualTo(3));
            Assert.That(gm.CurrentNode, Is.Null, "아직 아무 칸에도 안 들어갔다");
            Assert.That(gm.Progress.CanSelect(First.Id), Is.True);
        }

        /// <summary>레시피가 없으면 기본값으로 몰래 채우지 않고 런을 안 연다.</summary>
        [Test]
        public void StartNewRun_WithoutRecipe_Refuses()
        {
            gm.Configure(null, Seed);
            LogAssert.Expect(LogType.Error, new Regex("레시피"));

            Assert.That(gm.StartNewRun(), Is.False);
            Assert.That(gm.IsRunActive, Is.False);
            Assert.That(gm.Progress, Is.Null);
        }

        [Test]
        public void StartNewRun_BrokenRecipe_Refuses()
        {
            RunMapRecipe broken = Recipe();
            gm.Configure(broken, Seed);
            LogAssert.Expect(LogType.Error, new Regex("지도를 굴리지 못했다"));

            Assert.That(gm.StartNewRun(), Is.False);
            Assert.That(gm.IsRunActive, Is.False);

            Object.DestroyImmediate(broken);
        }

        [Test]
        public void FixedSeed_SameMapEveryRun()
        {
            gm.StartNewRun();
            string first = gm.Map.Describe();

            gm.StartNewRun();
            Assert.That(gm.Map.Describe(), Is.EqualTo(first));
        }

        /// <summary>새 런은 지난 런의 진행 · 골드를 물고 시작하지 않는다.</summary>
        [Test]
        public void StartNewRun_ResetsProgressAndGold()
        {
            gm.StartNewRun();
            gm.Progress.TrySelect(First.Id);
            gm.Run.AddGold(90);

            gm.StartNewRun();

            Assert.That(gm.CurrentNode, Is.Null);
            Assert.That(gm.Progress.Visited, Is.Empty);
            Assert.That(gm.Run.Gold, Is.EqualTo(0));
        }

        // ── 칸 들어가기 ─────────────────────────────────

        [Test]
        public void EnterNode_WithoutSceneLoader_IsRefused_AndProgressUntouched()
        {
            gm.StartNewRun();

            Assert.That(gm.EnterNode(First.Id, SceneNames.RunMap), Is.False);
            Assert.That(gm.Progress.HasPending, Is.False, "전환은 거절됐는데 칸에 들어간 것으로 박혔다");
        }

        [Test]
        public void EnterNode_NotAChoice_IsRefused()
        {
            gm.StartNewRun();
            MapNode boss = gm.Map.NodesOn(gm.Map.LastFloor)[0];

            Assert.That(gm.EnterNode(boss.Id, SceneNames.RunMap), Is.False);
            Assert.That(gm.EnterNode(9999, SceneNames.RunMap), Is.False);
        }

        [Test]
        public void EnterNode_BeforeRun_IsRefused()
        {
            Assert.That(gm.EnterNode(0, SceneNames.RunMap), Is.False);
        }

        /// <summary>
        /// 씬 없는 휴식 칸은 전환 없이 그 자리에서 끝난다 — 그래서 SceneLoader 가 없어도 된다.
        /// </summary>
        [Test]
        public void EnterNode_SceneLessRest_HealsAndCompletesInPlace()
        {
            RunMapRecipe rest = Recipe(
                FloorRule.Of(1, 1, N("", MapNodeKind.Rest)),
                FloorRule.Of(1, 1, N("Stage_Boss", MapNodeKind.Boss)));
            gm.Configure(rest, Seed);
            gm.StartNewRun();
            gm.Run.Party.ChangeHp(-0.5f, null);

            Assert.That(gm.EnterNode(First.Id, SceneNames.RunMap), Is.True);

            Assert.That(gm.Run.Party.HeroHpRatio, Is.EqualTo(0.5f + NodeRules.RestHealRatio).Within(1e-4f));
            Assert.That(gm.Progress.Current, Is.EqualTo(First.Id));
            Assert.That(gm.Progress.HasPending, Is.False);

            Object.DestroyImmediate(rest);
        }

        // ── 들어가 있는 칸 ──────────────────────────────
        // 전환 없이 진행만 옮겨 "전투 씬 안"을 흉내 낸다.

        [Test]
        public void HasNextNode_TrueBeforeBoss_FalseOnBoss()
        {
            gm.StartNewRun();
            Assert.That(gm.HasNextNode, Is.False, "들어가 있는 칸이 없으면 다음도 없다");

            gm.Progress.TrySelect(First.Id);
            Assert.That(gm.HasNextNode, Is.True);
            gm.Progress.CompletePending();

            MapNode second = gm.Progress.Choices()[0];
            gm.Progress.TrySelect(second.Id);
            gm.Progress.CompletePending();

            MapNode boss = gm.Progress.Choices()[0];
            gm.Progress.TrySelect(boss.Id);

            Assert.That(gm.CurrentNode, Is.SameAs(boss));
            Assert.That(gm.HasNextNode, Is.False, "보스를 깨면 런이 끝난다");
            Assert.That(gm.FloorNumber, Is.EqualTo(3));
        }

        [Test]
        public void CurrentNodeModifier_FollowsNodeKind()
        {
            gm.StartNewRun();
            Assert.That(gm.CurrentNodeModifier.IsNone, Is.True, "칸 밖");

            gm.Progress.TrySelect(First.Id);
            gm.Progress.CompletePending();

            foreach (MapNode n in gm.Progress.Choices())
                if (n.Kind == MapNodeKind.Elite) gm.Progress.TrySelect(n.Id);

            Assert.That(gm.CurrentNode.Kind, Is.EqualTo(MapNodeKind.Elite));
            Assert.That(gm.CurrentNodeModifier.EliteEvery, Is.EqualTo(EncounterModifierRules.EliteNodeEvery));
        }

        [Test]
        public void CompleteNodeAndReturnToMap_WithoutSceneLoader_KeepsPending()
        {
            gm.StartNewRun();
            gm.Progress.TrySelect(First.Id);

            Assert.That(gm.CompleteNodeAndReturnToMap(First.Scene), Is.False);
            Assert.That(gm.Progress.Pending, Is.EqualTo(First.Id), "전환은 거절됐는데 칸이 끝났다");
            Assert.That(gm.Progress.Visited, Is.Empty);
        }

        [Test]
        public void CompleteNodeAndReturnToMap_WithoutPending_IsRefused()
        {
            gm.StartNewRun();
            Assert.That(gm.CompleteNodeAndReturnToMap(SceneNames.RunMap), Is.False);
        }

        [Test]
        public void RestartCurrentStage_WithoutSceneLoader_KeepsPending()
        {
            gm.StartNewRun();
            gm.Progress.TrySelect(First.Id);

            Assert.That(gm.RestartCurrentStage(First.Scene), Is.False);
            Assert.That(gm.CurrentNode, Is.SameAs(First));
        }

        [Test]
        public void RestartRun_WithoutSceneLoader_IsRefused_AndKeepsRun()
        {
            gm.StartNewRun();
            gm.Progress.TrySelect(First.Id);
            gm.Run.AddGold(40);

            Assert.That(gm.RestartRun(First.Scene), Is.False);
            Assert.That(gm.CurrentNode, Is.SameAs(First), "거절됐는데 새 지도로 바뀌었다");
            Assert.That(gm.Run.Gold, Is.EqualTo(40), "거절됐는데 런이 초기화됐다");
        }

        [Test]
        public void ReturnToMainMenu_WithoutSceneLoader_IsRefused_AndKeepsRun()
        {
            gm.StartNewRun();

            Assert.That(gm.ReturnToMainMenu(SceneNames.RunMap), Is.False);
            Assert.That(gm.IsRunActive, Is.True, "거절됐는데 런이 종료됐다");
            Assert.That(gm.Progress, Is.Not.Null);
        }

        // ── 런 수명 ─────────────────────────────────────

        [Test]
        public void EndRun_ClearsActiveFlagAndMap()
        {
            gm.StartNewRun();
            gm.EndRun();

            Assert.That(gm.IsRunActive, Is.False);
            Assert.That(gm.Progress, Is.Null);
            Assert.That(gm.Map, Is.Null);
            Assert.That(gm.CurrentNodeModifier.IsNone, Is.True);
        }

        [Test]
        public void AddExp_Accumulates()
        {
            gm.StartNewRun();
            gm.AddExp(30);
            gm.AddExp(12);

            Assert.That(gm.TotalExp, Is.EqualTo(42));
        }

        [Test]
        public void SelectLoadout_DuringRun_IsIgnored()
        {
            var picked = ScriptableObject.CreateInstance<PartyLoadout>();
            gm.StartNewRun();

            LogAssert.Expect(LogType.Warning, new Regex("파티 교체"));
            gm.SelectLoadout(picked);

            Assert.That(gm.Loadout, Is.Not.SameAs(picked));
            Object.DestroyImmediate(picked);
        }
    }
}
