using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 지도 화면이 읽는 판단 — 칸 · 선의 상태, 방향키 순서, 배치.
    /// 색이 실제로 어떻게 보이는지는 지도 씬을 단독 실행해서 눈으로 본다.
    /// </summary>
    public class RunMapViewRulesTests
    {
        /// <summary>
        /// <code>
        ///            ┌ [1 B] ─ [3 D] ┐
        ///   [0 A] ───┤               ├─ [5 Boss]
        ///            └ [2 C] ─ [4 E] ┘
        /// </code>
        /// 1 → 3, 2 → 4 로만 이어진다. 1을 고르면 2와 4는 닫힌다.
        /// </summary>
        private static RunMap Map() => new RunMap(0, new[]
        {
            new[] { new MapNode(0, 0, 0, MapNodeKind.Battle, "A", new[] { 1, 2 }) },
            new[]
            {
                new MapNode(1, 1, 0, MapNodeKind.Battle, "B", new[] { 3 }),
                new MapNode(2, 1, 1, MapNodeKind.Shop, "C", new[] { 4 }),
            },
            new[]
            {
                new MapNode(3, 2, 0, MapNodeKind.Elite, "D", new[] { 5 }),
                new MapNode(4, 2, 1, MapNodeKind.Rest, "", new[] { 5 }),
            },
            new[] { new MapNode(5, 3, 0, MapNodeKind.Boss, "Z", null) },
        });

        private static MapNodeState State(RunMapProgress p, int id)
            => RunMapViewRules.StateOf(p, p.Map.Get(id), RunMapViewRules.Reachable(p));

        private static MapEdgeState Edge(RunMapProgress p, int from, int to)
            => RunMapViewRules.EdgeOf(p, p.Map.Get(from), p.Map.Get(to), RunMapViewRules.Reachable(p));

        private static RunMapProgress Walk(params int[] ids)
        {
            var p = new RunMapProgress(Map());
            foreach (int id in ids)
            {
                Assert.That(p.TrySelect(id), Is.True, $"칸 {id}");
                p.CompletePending();
            }
            return p;
        }

        // ── 칸 상태 ─────────────────────────────────────

        [Test]
        public void BeforeStart_FirstFloorSelectable_RestAhead()
        {
            RunMapProgress p = Walk();

            Assert.That(State(p, 0), Is.EqualTo(MapNodeState.Selectable));
            foreach (int id in new[] { 1, 2, 3, 4, 5 })
                Assert.That(State(p, id), Is.EqualTo(MapNodeState.Ahead), $"칸 {id}");
        }

        [Test]
        public void AfterPickingBranch_OtherBranchCloses()
        {
            RunMapProgress p = Walk(0, 1);

            Assert.That(State(p, 0), Is.EqualTo(MapNodeState.Visited));
            Assert.That(State(p, 1), Is.EqualTo(MapNodeState.Current));
            Assert.That(State(p, 2), Is.EqualTo(MapNodeState.Closed), "안 고른 같은 층 칸");
            Assert.That(State(p, 3), Is.EqualTo(MapNodeState.Selectable));
            Assert.That(State(p, 4), Is.EqualTo(MapNodeState.Closed), "고른 길에서 안 이어지는 칸");
            Assert.That(State(p, 5), Is.EqualTo(MapNodeState.Ahead));
        }

        /// <summary>들어가 있는 동안에는 아무것도 고를 수 없다. 옆 선택지는 잠긴 채 앞 칸으로 보인다.</summary>
        [Test]
        public void WhilePending_NothingSelectable()
        {
            RunMapProgress p = Walk(0);
            p.TrySelect(2);

            Assert.That(State(p, 2), Is.EqualTo(MapNodeState.Pending));
            Assert.That(State(p, 1), Is.EqualTo(MapNodeState.Closed));
            Assert.That(State(p, 4), Is.EqualTo(MapNodeState.Ahead));
            Assert.That(RunMapViewRules.NavigationOrder(p), Is.Empty);
        }

        [Test]
        public void Finished_OnlyVisitedAndClosedRemain()
        {
            RunMapProgress p = Walk(0, 2, 4, 5);

            Assert.That(State(p, 5), Is.EqualTo(MapNodeState.Current));
            Assert.That(State(p, 4), Is.EqualTo(MapNodeState.Visited));
            Assert.That(State(p, 3), Is.EqualTo(MapNodeState.Closed));
            Assert.That(RunMapViewRules.Reachable(p), Is.Empty);
        }

        // ── 선 상태 ─────────────────────────────────────

        [Test]
        public void Edges_TakenOpenAheadClosed()
        {
            RunMapProgress p = Walk(0, 1);

            Assert.That(Edge(p, 0, 1), Is.EqualTo(MapEdgeState.Taken));
            Assert.That(Edge(p, 0, 2), Is.EqualTo(MapEdgeState.Closed), "지나온 칸에서 안 고른 칸으로");
            Assert.That(Edge(p, 1, 3), Is.EqualTo(MapEdgeState.Open));
            Assert.That(Edge(p, 3, 5), Is.EqualTo(MapEdgeState.Ahead));
            Assert.That(Edge(p, 2, 4), Is.EqualTo(MapEdgeState.Closed));
            Assert.That(Edge(p, 4, 5), Is.EqualTo(MapEdgeState.Closed), "닫힌 칸에서 나가는 선");
        }

        [Test]
        public void Edge_IntoPendingNode_IsTaken()
        {
            RunMapProgress p = Walk(0);
            p.TrySelect(1);

            Assert.That(Edge(p, 0, 1), Is.EqualTo(MapEdgeState.Taken));
        }

        // ── 방향키 ──────────────────────────────────────

        [Test]
        public void NavigationOrder_IsSelectableChoicesByColumn()
        {
            RunMapProgress p = Walk(0);

            Assert.That(RunMapViewRules.NavigationOrder(p).Select(n => n.Id), Is.EqualTo(new[] { 1, 2 }));
        }

        // ── 배치 ────────────────────────────────────────

        [Test]
        public void Layout_FloorsGoLeftToRight_ColumnZeroOnTop()
        {
            RunMap map = Map();

            Vector2 start = RunMapLayout.PositionOf(map, map.Get(0));
            Vector2 top = RunMapLayout.PositionOf(map, map.Get(1));
            Vector2 bottom = RunMapLayout.PositionOf(map, map.Get(2));
            Vector2 boss = RunMapLayout.PositionOf(map, map.Get(5));

            Assert.That(start.x, Is.LessThan(top.x));
            Assert.That(top.x, Is.LessThan(boss.x));
            Assert.That(top.y, Is.GreaterThan(bottom.y));
            Assert.That(start.y, Is.EqualTo(0f), "한 칸짜리 층은 가운데");
            Assert.That(top.x, Is.EqualTo(bottom.x), "같은 층은 같은 X");
        }

        /// <summary>폭이 넓은 층 · 층이 많은 지도도 영역 안에 들고, 칸끼리 겹치지 않는다.</summary>
        [TestCase(5, 3)]
        [TestCase(8, 5)]
        [TestCase(RunMapLayout.MaxFloors, RunMapLayout.MaxColumns)]
        public void Layout_StaysInsideArea_WithoutOverlap(int floors, int columns)
        {
            var points = new List<Vector2>();
            for (int f = 0; f < floors; f++)
                for (int c = 0; c < columns; c++)
                    points.Add(RunMapLayout.PositionOf(f, c, columns, floors));

            foreach (Vector2 pt in points)
            {
                Assert.That(Mathf.Abs(pt.x), Is.LessThanOrEqualTo(RunMapLayout.Width * 0.5f + 0.01f));
                Assert.That(Mathf.Abs(pt.y), Is.LessThanOrEqualTo(RunMapLayout.Height * 0.5f + 0.01f));
            }

            Assert.That(RunMapLayout.FloorSpacing(floors), Is.GreaterThanOrEqualTo(RunMapLayout.NodeSize.x),
                        "층 간격이 칸 폭보다 좁으면 옆 층 칸과 겹친다");
            Assert.That(RunMapLayout.ColumnSpacing(columns), Is.GreaterThanOrEqualTo(RunMapLayout.NodeSize.y),
                        "열 간격이 칸 높이보다 좁으면 위아래 칸이 겹친다");
        }

        // ── 글 ─────────────────────────────────────────

        /// <summary>설명의 수치는 규칙에서 읽는다 — 회복량을 바꾸면 글도 따라 바뀌어야 한다.</summary>
        [Test]
        public void Describe_ReadsNumbersFromRules()
        {
            var rest = new MapNode(0, 0, 0, MapNodeKind.Rest, "", null);
            var elite = new MapNode(1, 0, 1, MapNodeKind.Elite, "S", null);

            Assert.That(RunMapViewRules.Describe(rest), Does.Contain($"{NodeRules.RestHealRatio:0%}"));
            Assert.That(RunMapViewRules.Describe(elite), Does.Contain(EncounterModifierRules.EliteNodeEvery.ToString()));
            Assert.That(RunMapViewRules.Describe(elite),
                        Does.Contain($"x{GoldRules.ClearRewardPercent(MapNodeKind.Elite) / 100f:0.##}"),
                        "정예를 고를 이유(골드 배율)가 설명에 보여야 한다");
        }

        /// <summary>
        /// 방 크기가 바뀐 칸은 고르기 전에 알 수 있어야 한다(docs/Room_Size_Plan.md 2.1 · 8단계).
        /// 100%인 칸에는 안 붙인다 — 모든 칸에 "방 크기 100%"가 붙으면 정보가 아니라 소음이다.
        /// </summary>
        [Test]
        public void Describe_ShowsRoomPercent_OnlyWhenResized()
        {
            var battle = new MapNode(0, 0, 0, MapNodeKind.Battle, "A", null, 90);
            var elite = new MapNode(1, 0, 1, MapNodeKind.Elite, "S", null, 110);
            var full = new MapNode(2, 0, 2, MapNodeKind.Battle, "B", null);

            Assert.That(RunMapViewRules.Describe(battle), Does.EndWith(" · 방 크기 90%"));
            Assert.That(RunMapViewRules.Describe(elite), Does.EndWith(" · 방 크기 110%"));
            Assert.That(RunMapViewRules.Describe(elite), Does.Contain($"x{GoldRules.ClearRewardPercent(MapNodeKind.Elite) / 100f:0.##}"),
                        "방 크기를 붙이면서 정예 설명이 잘렸다");
            Assert.That(RunMapViewRules.Describe(full), Does.Not.Contain("방 크기"));
        }

        /// <summary>보스 칸에 비율이 박혀 있어도 전투는 100%로 돈다. 지도도 그렇게 말해야 한다.</summary>
        [Test]
        public void Describe_BossNeverShowsRoomPercent()
        {
            var boss = new MapNode(0, 0, 0, MapNodeKind.Boss, "Z", null, 90);
            Assert.That(RunMapViewRules.Describe(boss), Does.Not.Contain("방 크기"));
        }

        [Test]
        public void KindLabel_EveryKindHasItsOwn()
        {
            var kinds = (MapNodeKind[])System.Enum.GetValues(typeof(MapNodeKind));
            var labels = kinds.Select(RunMapViewRules.KindLabel).ToList();

            Assert.That(labels.Distinct().Count(), Is.EqualTo(kinds.Length));
        }
    }
}
