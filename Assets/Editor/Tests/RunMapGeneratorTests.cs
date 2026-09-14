using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Prototype.Tests
{
    /// <summary>
    /// 지도 생성기와 지도 검사. 계획서 2.3의 불변식 표가 여기 전부 있다.
    ///
    /// <b>검사기부터 본다.</b> "시드 1,000개가 검사를 통과한다"는 검사기가 틀린 지도를 잡는다는 전제에서만
    /// 의미가 있다 — 손으로 틀리게 만든 지도를 검사기가 정확히 잡는지를 먼저 확인한다.
    /// </summary>
    public class RunMapGeneratorTests
    {
        private const string MainRecipePath = "Assets/Data/Map/RunMap_Main.asset";
        private const string DebugRecipePath = "Assets/Data/Map/RunMap_Debug.asset";

        private const int SeedSweep = 1000;

        private static NodeCandidate N(string scene, MapNodeKind kind, int weight = 1)
            => NodeCandidate.Of(scene, kind, weight);

        /// <summary>폭이 넓은 레시피. 간선 · 교차 규칙을 실제 레시피보다 세게 흔든다.</summary>
        private static FloorRule[] Wide() => new[]
        {
            FloorRule.Of(1, 4, N("A", MapNodeKind.Battle), N("B", MapNodeKind.Battle), N("C", MapNodeKind.Battle)),
            FloorRule.Of(2, 5, N("A", MapNodeKind.Battle), N("B", MapNodeKind.Elite), N("", MapNodeKind.Rest),
                               N("S", MapNodeKind.Shop), N("E", MapNodeKind.Event)),
            FloorRule.Of(1, 5, N("C", MapNodeKind.Battle), N("D", MapNodeKind.Battle), N("", MapNodeKind.Rest),
                               N("S", MapNodeKind.Shop)),
            FloorRule.Of(3, 5, N("A", MapNodeKind.Elite), N("E", MapNodeKind.Event), N("", MapNodeKind.Rest),
                               N("D", MapNodeKind.Battle, 3)),
            FloorRule.Of(1, 1, N("Boss", MapNodeKind.Boss)),
        };

        private static MapNode Node(int id, int floor, int column, MapNodeKind kind, string scene, params int[] next)
            => new MapNode(id, floor, column, kind, scene, next);

        private static bool Has(List<RunMapIssue> issues, RunMapProblem problem)
            => issues.Any(i => i.problem == problem);

        private static RunMap Generate(IReadOnlyList<FloorRule> floors, int seed)
        {
            bool ok = RunMapGenerator.TryGenerate(floors, seed, out RunMap map, out List<RunMapIssue> issues);
            Assert.That(ok, Is.True, $"시드 {seed}: " + string.Join(" / ", issues.Select(i => i.Describe())));
            return map;
        }

        // ── 검사기가 틀린 지도를 잡는가 ─────────────────

        /// <summary>
        /// 멀쩡한 모양: 0층 한 칸 → 1층 두 칸 → 보스.
        /// <code>
        ///      [0 A]
        ///     /     \
        ///  [1 B]   [2 Rest]
        ///     \     /
        ///      [3 Boss]
        /// </code>
        /// </summary>
        private static MapNode[][] Diamond() => new[]
        {
            new[] { Node(0, 0, 0, MapNodeKind.Battle, "A", 1, 2) },
            new[] { Node(1, 1, 0, MapNodeKind.Battle, "B", 3), Node(2, 1, 1, MapNodeKind.Rest, "", 3) },
            new[] { Node(3, 2, 0, MapNodeKind.Boss, "Z") },
        };

        [Test]
        public void Issues_ValidMap_IsClean()
        {
            Assert.That(RunMapRules.Issues(new RunMap(0, Diamond())), Is.Empty);
        }

        [Test]
        public void Issues_DeadEnd_IsNoOutgoing()
        {
            MapNode[][] f = Diamond();
            f[1][1] = Node(2, 1, 1, MapNodeKind.Rest, "");

            Assert.That(Has(RunMapRules.Issues(new RunMap(0, f)), RunMapProblem.NoOutgoing), Is.True);
        }

        [Test]
        public void Issues_Unreachable_IsNoIncoming()
        {
            MapNode[][] f = Diamond();
            f[0][0] = Node(0, 0, 0, MapNodeKind.Battle, "A", 1);

            Assert.That(Has(RunMapRules.Issues(new RunMap(0, f)), RunMapProblem.NoIncoming), Is.True);
        }

        [Test]
        public void Issues_Crossing_IsCaught()
        {
            var f = new[]
            {
                new[] { Node(0, 0, 0, MapNodeKind.Battle, "A", 3), Node(1, 0, 1, MapNodeKind.Battle, "B", 2) },
                new[] { Node(2, 1, 0, MapNodeKind.Battle, "C", 4), Node(3, 1, 1, MapNodeKind.Battle, "D", 4) },
                new[] { Node(4, 2, 0, MapNodeKind.Boss, "Z") },
            };

            Assert.That(Has(RunMapRules.Issues(new RunMap(0, f)), RunMapProblem.CrossingEdges), Is.True);
        }

        /// <summary>같은 칸에서 갈라지거나 같은 칸으로 모이는 것은 교차가 아니다.</summary>
        [Test]
        public void Crosses_SharedEndpoint_IsNotCrossing()
        {
            Assert.That(RunMapRules.Crosses(0, 1, 1, 1), Is.False);
            Assert.That(RunMapRules.Crosses(0, 0, 0, 1), Is.False);
            Assert.That(RunMapRules.Crosses(0, 1, 1, 0), Is.True);
        }

        [Test]
        public void Issues_SameSceneTwice_IsCaught()
        {
            MapNode[][] f = Diamond();
            f[1][0] = Node(1, 1, 0, MapNodeKind.Elite, "A", 3);

            Assert.That(Has(RunMapRules.Issues(new RunMap(0, f)), RunMapProblem.SameSceneInARow), Is.True);
        }

        [Test]
        public void Issues_RestAfterRest_IsCaught_BattleAfterBattleIsFine()
        {
            var f = new[]
            {
                new[] { Node(0, 0, 0, MapNodeKind.Rest, "", 1) },
                new[] { Node(1, 1, 0, MapNodeKind.Rest, "R", 2) },
                new[] { Node(2, 2, 0, MapNodeKind.Boss, "Z") },
            };

            Assert.That(Has(RunMapRules.Issues(new RunMap(0, f)), RunMapProblem.SameNodeKindInARow), Is.True);
            Assert.That(RunMapRules.CanFollow(MapNodeKind.Battle, "A", MapNodeKind.Battle, "B"), Is.True);
        }

        /// <summary>씬 없는 휴식 칸끼리는 "같은 씬"이 아니다 — 빈 이름을 같은 씬으로 세면 오보가 난다.</summary>
        [Test]
        public void CanFollow_EmptyScenes_AreNotTheSameScene()
        {
            Assert.That(RunMapRules.CanFollow(MapNodeKind.Battle, "", MapNodeKind.Shop, ""), Is.True);
        }

        [Test]
        public void Issues_TwoBossesOrNoBoss_AreCaught()
        {
            MapNode[][] noBoss = Diamond();
            noBoss[2][0] = Node(3, 2, 0, MapNodeKind.Battle, "Z");
            Assert.That(Has(RunMapRules.Issues(new RunMap(0, noBoss)), RunMapProblem.LastFloorNotSingleBoss), Is.True);

            MapNode[][] early = Diamond();
            early[1][0] = Node(1, 1, 0, MapNodeKind.Boss, "B", 3);
            Assert.That(Has(RunMapRules.Issues(new RunMap(0, early)), RunMapProblem.BossBeforeLastFloor), Is.True);
        }

        [Test]
        public void Issues_EdgeSkippingAFloor_IsBroken()
        {
            MapNode[][] f = Diamond();
            f[0][0] = Node(0, 0, 0, MapNodeKind.Battle, "A", 1, 2, 3);

            Assert.That(Has(RunMapRules.Issues(new RunMap(0, f)), RunMapProblem.BrokenEdge), Is.True);
        }

        [Test]
        public void Issues_BattleWithoutScene_IsMissingScene()
        {
            MapNode[][] f = Diamond();
            f[1][0] = Node(1, 1, 0, MapNodeKind.Battle, "  ", 3);

            Assert.That(Has(RunMapRules.Issues(new RunMap(0, f)), RunMapProblem.MissingScene), Is.True);
        }

        // ── 생성기 ──────────────────────────────────────

        [Test]
        public void SameSeed_SameMap()
        {
            Assert.That(Generate(Wide(), 1234).Describe(), Is.EqualTo(Generate(Wide(), 1234).Describe()));
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentMaps()
        {
            var shapes = new HashSet<string>();
            for (int seed = 0; seed < 20; seed++) shapes.Add(Generate(Wide(), seed).Describe());

            Assert.That(shapes.Count, Is.GreaterThan(1));
        }

        [Test]
        public void GeneratedMap_RemembersSeed_AndIdsFollowFloorsAndColumns()
        {
            RunMap map = Generate(Wide(), 77);
            Assert.That(map.Seed, Is.EqualTo(77));

            int expected = 0;
            for (int f = 0; f < map.FloorCount; f++)
                for (int c = 0; c < map.NodesOn(f).Count; c++)
                {
                    MapNode n = map.NodesOn(f)[c];
                    Assert.That(n.Id, Is.EqualTo(expected++));
                    Assert.That(n.Floor, Is.EqualTo(f));
                    Assert.That(n.Column, Is.EqualTo(c));
                    Assert.That(map.Get(n.Id), Is.SameAs(n));
                }
        }

        [Test]
        public void WideRecipe_EverySeed_KeepsInvariantsAndNodeCounts()
        {
            FloorRule[] floors = Wide();

            for (int seed = 0; seed < SeedSweep; seed++)
            {
                RunMap map = Generate(floors, seed);

                for (int f = 0; f < floors.Length; f++)
                    Assert.That(map.NodesOn(f).Count, Is.InRange(floors[f].minNodes, floors[f].maxNodes),
                                $"시드 {seed} {f}층 칸 수");
            }
        }

        /// <summary>한 층 한 칸짜리 레시피는 일렬이 된다 — 디버그 경로가 기대는 성질이다.</summary>
        [Test]
        public void WidthOneRecipe_IsALine()
        {
            var floors = new[]
            {
                FloorRule.Of(1, 1, N("A", MapNodeKind.Battle)),
                FloorRule.Of(1, 1, N("B", MapNodeKind.Battle)),
                FloorRule.Of(1, 1, N("C", MapNodeKind.Boss)),
            };

            RunMap map = Generate(floors, 5);

            Assert.That(map.Describe(), Is.EqualTo("0[Battle:A>1] / 1[Battle:B>2] / 2[Boss:C]"));
        }

        /// <summary>제약을 못 맞추는 레시피는 조용히 물리지 않고 실패를 돌려준다.</summary>
        [Test]
        public void ImpossibleRecipe_FailsWithGenerationFailed()
        {
            var floors = new[]
            {
                FloorRule.Of(1, 1, N("A", MapNodeKind.Battle)),
                FloorRule.Of(1, 1, N("A", MapNodeKind.Elite)),
                FloorRule.Of(1, 1, N("Z", MapNodeKind.Boss)),
            };

            bool ok = RunMapGenerator.TryGenerate(floors, 0, out RunMap map, out List<RunMapIssue> issues);

            Assert.That(ok, Is.False);
            Assert.That(map, Is.Null);
            Assert.That(Has(issues, RunMapProblem.GenerationFailed), Is.True);
        }

        [Test]
        public void InvalidRecipe_RefusesWithRecipeIssues()
        {
            bool ok = RunMapGenerator.TryGenerate(new FloorRule[0], 0, out RunMap map, out List<RunMapIssue> issues);

            Assert.That(ok, Is.False);
            Assert.That(map, Is.Null);
            Assert.That(Has(issues, RunMapProblem.NoFloors), Is.True);
        }

        // ── 실제 레시피 ─────────────────────────────────

        /// <summary>
        /// 들어가 있는 레시피가 어떤 시드에서도 굴러가는가. 후보를 좁히는 수정이 제약에 걸리면 여기서 드러난다 —
        /// 안 그러면 "가끔 [시작]이 안 눌린다"로만 보인다.
        /// </summary>
        [TestCase(MainRecipePath)]
        [TestCase(DebugRecipePath)]
        public void ShippedRecipe_EverySeed_Generates(string path)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<RunMapRecipe>(path);
            Assert.That(recipe, Is.Not.Null, $"{path} 가 없다.");
            Assert.That(recipe.Issues(), Is.Empty, string.Join(" / ", recipe.Issues().Select(i => i.Describe())));

            for (int seed = 0; seed < SeedSweep; seed++)
            {
                bool ok = recipe.TryGenerate(seed, out RunMap map, out List<RunMapIssue> issues);
                Assert.That(ok, Is.True, $"{path} 시드 {seed}: " + string.Join(" / ", issues.Select(i => i.Describe())));

                for (int f = 0; f < recipe.FloorCount; f++)
                    Assert.That(map.NodesOn(f).Count, Is.InRange(recipe.floors[f].minNodes, recipe.floors[f].maxNodes));
            }
        }

        /// <summary>
        /// 입문은 고정이다(계획서 2.2) — 학습 곡선의 첫 칸이 굴림에 따라 바뀌면 안 된다.
        /// 레시피 값이 바뀌면 이 테스트도 같이 바꾼다. 조용히 바뀌는 것만 막는다.
        /// </summary>
        [Test]
        public void MainRecipe_StartsAtStage01_EndsAtStage05Boss()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<RunMapRecipe>(MainRecipePath);
            Assert.That(recipe, Is.Not.Null);

            for (int seed = 0; seed < 50; seed++)
            {
                Assert.That(recipe.TryGenerate(seed, out RunMap map, out _), Is.True);

                Assert.That(map.NodesOn(0).Select(n => n.Scene), Is.EqualTo(new[] { SceneNames.Stage01 }));

                MapNode boss = map.NodesOn(map.LastFloor).Single();
                Assert.That(boss.Kind, Is.EqualTo(MapNodeKind.Boss));
                Assert.That(boss.Scene, Is.EqualTo(SceneNames.Stage05));
            }
        }
    }
}
