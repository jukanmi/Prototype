using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Prototype
{
    // ══ 상태 ═══════════════════════════════════════════

    /// <summary>화면에서 칸이 어떻게 보이는가. 뒤로 갈수록 강조가 세다.</summary>
    public enum MapNodeState
    {
        /// <summary>이제 못 가는 칸. 지나친 층의 안 고른 칸, 고른 길에서 안 이어지는 칸.</summary>
        Closed,

        /// <summary>앞으로 갈 수도 있는 칸. 아직 고를 차례는 아니다.</summary>
        Ahead,

        /// <summary>지금 고를 수 있는 칸.</summary>
        Selectable,

        /// <summary>들어가 있는 칸(전투 중 · 패배 후 재시작 대기).</summary>
        Pending,

        /// <summary>끝낸 칸.</summary>
        Visited,

        /// <summary>마지막으로 끝낸 칸 — 지금 서 있는 자리.</summary>
        Current,
    }

    /// <summary>선이 어떻게 보이는가.</summary>
    public enum MapEdgeState
    {
        Closed,
        Ahead,

        /// <summary>지금 서 있는 칸에서 고를 수 있는 칸으로.</summary>
        Open,

        /// <summary>지나온 길.</summary>
        Taken,
    }

    // ══ RunMapViewRules ═══════════════════════════════════════════

    /// <summary>화면이 읽는 판단 전부. 순수 함수다.</summary>
    public static class RunMapViewRules
    {
        /// <summary>
        /// 앞으로 닿을 수 있는 칸 전부. 들어가 있으면 그 칸에서, 아니면 지금 선택지에서 뻗어 나간다.
        /// 선택지 자체도 포함한다.
        /// </summary>
        public static HashSet<int> Reachable(RunMapProgress progress)
        {
            var reach = new HashSet<int>();
            if (progress == null) return reach;

            var frontier = new Queue<MapNode>();

            if (progress.PendingNode != null) frontier.Enqueue(progress.PendingNode);
            else foreach (MapNode n in progress.Choices()) frontier.Enqueue(n);

            while (frontier.Count > 0)
            {
                MapNode n = frontier.Dequeue();
                if (n == null || !reach.Add(n.Id)) continue;

                foreach (int id in n.Next) frontier.Enqueue(progress.Map.Get(id));
            }

            return reach;
        }

        public static MapNodeState StateOf(RunMapProgress progress, MapNode node, HashSet<int> reachable)
        {
            if (progress == null || node == null) return MapNodeState.Closed;

            if (node.Id == progress.Current) return MapNodeState.Current;
            if (progress.HasVisited(node.Id)) return MapNodeState.Visited;
            if (node.Id == progress.Pending) return MapNodeState.Pending;
            if (progress.CanSelect(node.Id)) return MapNodeState.Selectable;
            if (reachable != null && reachable.Contains(node.Id)) return MapNodeState.Ahead;

            return MapNodeState.Closed;
        }

        /// <summary>
        /// 선의 상태. 층마다 끝낸 칸은 하나뿐이라, 양끝이 다 끝낸 칸이면 곧 지나온 길이다.
        /// </summary>
        public static MapEdgeState EdgeOf(RunMapProgress progress, MapNode from, MapNode to, HashSet<int> reachable)
        {
            if (progress == null || from == null || to == null) return MapEdgeState.Closed;

            bool fromDone = progress.HasVisited(from.Id);

            if (fromDone && (progress.HasVisited(to.Id) || to.Id == progress.Pending)) return MapEdgeState.Taken;
            if (from.Id == progress.Current && progress.CanSelect(to.Id)) return MapEdgeState.Open;

            bool fromLive = from.Id == progress.Current || (reachable != null && reachable.Contains(from.Id));
            if (fromLive && reachable != null && reachable.Contains(to.Id)) return MapEdgeState.Ahead;

            return MapEdgeState.Closed;
        }

        /// <summary>
        /// 방향키가 오갈 칸들 — 지금 고를 수 있는 칸을 열 순으로. <b>간선에서 나온 목록이다</b>:
        /// 화면 좌표로 이웃을 고르는 자동 내비게이션은 옆 층의 못 가는 칸으로 튄다.
        /// </summary>
        public static List<MapNode> NavigationOrder(RunMapProgress progress)
        {
            var list = new List<MapNode>();
            if (progress == null) return list;

            foreach (MapNode n in progress.Choices())
                if (progress.CanSelect(n.Id)) list.Add(n);

            list.Sort((a, b) => a.Column.CompareTo(b.Column));
            return list;
        }

        public static string KindLabel(MapNodeKind kind)
        {
            switch (kind)
            {
                case MapNodeKind.Elite: return "정예";
                case MapNodeKind.Rest:  return "휴식";
                case MapNodeKind.Shop:  return "상점";
                case MapNodeKind.Event: return "이벤트";
                case MapNodeKind.Boss:  return "보스";
                default:                return "전투";
            }
        }
    }

    // ══ RunMapLayout ═══════════════════════════════════════════

    /// <summary>
    /// 칸의 화면 좌표(1920×1080 기준, 가운데가 원점). <b>왼쪽이 0층, 오른쪽이 보스</b>다 —
    /// 전투 씬도 오른쪽 벽으로 걸어 나가서 다음으로 가므로 방향을 맞췄다. 한 층 안에서는 0열이 위다.
    /// </summary>
    public static class RunMapLayout
    {
        /// <summary>층 사이 최대 간격. 층이 많으면 <see cref="Width"/> 안에 들도록 좁힌다.</summary>
        public const float MaxFloorSpacing = 360f;

        public const float MaxColumnSpacing = 220f;

        /// <summary>칸이 차지할 수 있는 가로 · 세로 폭(칸 중심 기준).</summary>
        public const float Width = 1500f;
        public const float Height = 640f;

        public static readonly Vector2 NodeSize = new Vector2(170f, 110f);

        /// <summary>
        /// 칸이 겹치지 않는 한계. 간격을 영역에 맞춰 좁히다 보면 이보다 많을 때 칸 폭 · 높이보다 좁아진다.
        /// 레시피가 이걸 넘으면 칸 크기나 영역부터 손본다(<c>RunMapViewRulesTests</c>가 이 값으로 겹침을 본다).
        /// </summary>
        public const int MaxFloors = 9;
        public const int MaxColumns = 6;

        public static float FloorSpacing(int floorCount)
            => floorCount <= 1 ? 0f : Mathf.Min(MaxFloorSpacing, Width / (floorCount - 1));

        public static float ColumnSpacing(int countOnFloor)
            => countOnFloor <= 1 ? 0f : Mathf.Min(MaxColumnSpacing, Height / (countOnFloor - 1));

        public static Vector2 PositionOf(int floor, int column, int countOnFloor, int floorCount)
        {
            float x = (floor - (floorCount - 1) * 0.5f) * FloorSpacing(floorCount);
            float y = ((countOnFloor - 1) * 0.5f - column) * ColumnSpacing(countOnFloor);
            return new Vector2(x, y);
        }

        public static Vector2 PositionOf(RunMap map, MapNode node)
            => PositionOf(node.Floor, node.Column, map.NodesOn(node.Floor).Count, map.FloorCount);
    }
}
