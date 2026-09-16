// 런 지도 — 칸 · 지도 · 레시피 한 줄 · 검사 · 생성 · 진행.
//
// MonoBehaviour가 없어 한 파일에 둔다. 레시피 애셋만 RunMapRecipe.cs에 따로 있다
// (ScriptableObject는 자기 이름의 파일이 있어야 한다 — Refactor_Master_Plan 0항).
//
// 지도는 [시작]을 누를 때 한 번 굴리고 그 뒤로는 안 바뀐다. 진행은 값 두 개(current · pending)뿐이다.
// 계획서: docs/Run_Map_Plan.md (1.2 · 2.1 ~ 2.4 · 1단계)

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Prototype
{
    // ══ MapNodeKind ═══════════════════════════════════════════

    /// <summary>
    /// 칸의 <b>의미</b>. 아이콘과 보정값을 정한다.
    /// 씬을 여는지는 이 값이 아니라 <see cref="MapNode.Scene"/>이 비었는지로 갈린다(계획서 2.1).
    /// </summary>
    public enum MapNodeKind
    {
        Battle = 0,
        Elite = 1,
        Rest = 2,
        Shop = 3,
        Event = 4,

        /// <summary>마지막 층의 한 칸. 이걸 깨면 런이 끝난다.</summary>
        Boss = 5,
    }

    // ══ NodeCandidate · FloorRule ═══════════════════════════════════════════

    /// <summary>레시피 한 층에서 뽑힐 수 있는 칸 하나.</summary>
    [Serializable]
    public struct NodeCandidate
    {
        [Tooltip("열 씬 이름(파일명). 휴식 칸만 비워 둘 수 있다 — 비우면 씬 전환 없이 지도에서 바로 회복한다.")]
        public string scene;

        public MapNodeKind kind;

        [Tooltip("같은 층 후보끼리의 뽑힐 비중. 0이면 안 뽑힌다 — 검사가 경고한다.")]
        [Min(0)] public int weight;

        /// <summary>앞뒤 공백을 뺀 씬 이름. 비었으면 빈 문자열.</summary>
        public string Scene => scene != null ? scene.Trim() : "";

        public bool HasScene => Scene.Length > 0;

        public static NodeCandidate Of(string scene, MapNodeKind kind, int weight = 1)
            => new NodeCandidate { scene = scene, kind = kind, weight = weight };
    }

    /// <summary>
    /// 레시피의 층 하나. <b>층 수는 이 줄의 개수다</b> — 정수 칸을 따로 두지 않는다
    /// (Stage_Encounter_Unification_Plan 2.2와 같은 이유).
    /// </summary>
    [Serializable]
    public struct FloorRule
    {
        [Tooltip("이 층에 놓을 칸 수의 하한.")]
        [Min(1)] public int minNodes;

        [Tooltip("이 층에 놓을 칸 수의 상한. 굴릴 때 하한~상한에서 고른다.")]
        [Min(1)] public int maxNodes;

        public NodeCandidate[] candidates;

        [Tooltip("이 층 전투 · 정예 칸의 방 크기 하한(%). 80~125, 5 단위.\n\n" +
                 "하한 · 상한 둘 다 0이면 100% 고정이다. 보스 · 비전투 칸은 이 값과 무관하게 항상 100%다.\n\n" +
                 "방 크기 보정을 받는 씬(StageRoom 이 있는 웨이브 방)만 후보에 있어야 한다 — 아레나 씬이 섞인 층은 0으로 둔다.")]
        [Range(0, RoomRules.MaxPercent)] public int roomPercentMin;

        [Tooltip("방 크기 상한(%). 굴릴 때 하한~상한에서 5 단위로 고른다.")]
        [Range(0, RoomRules.MaxPercent)] public int roomPercentMax;

        public int CandidateCount => candidates != null ? candidates.Length : 0;

        /// <summary>방 크기 범위를 저작했는가. 둘 다 0이면 100% 고정이다(기존 레시피 애셋은 필드가 없어 0으로 읽힌다).</summary>
        public bool HasRoomRange => roomPercentMin != 0 || roomPercentMax != 0;

        public static FloorRule Of(int minNodes, int maxNodes, params NodeCandidate[] candidates)
            => new FloorRule { minNodes = minNodes, maxNodes = maxNodes, candidates = candidates };
    }

    // ══ MapNode ═══════════════════════════════════════════

    /// <summary>
    /// 지도의 칸 하나. 만들어진 뒤로 안 바뀐다.
    ///
    /// <b>씬 이름을 키로 쓰지 않는다</b> — 같은 씬이 여러 칸에 나온다(Stage_03 일반 · Stage_03 정예).
    /// </summary>
    public sealed class MapNode
    {
        /// <summary>지도 안에서 유일. 0층 왼쪽부터 층 순 · 열 순으로 매긴다.</summary>
        public int Id { get; }

        /// <summary>0부터 센다.</summary>
        public int Floor { get; }

        /// <summary>층 안에서 왼→오 순서. 간선 교차 판정이 이 값을 본다.</summary>
        public int Column { get; }

        public MapNodeKind Kind { get; }

        /// <summary>열 씬 이름. 비었으면 지도에서 바로 처리하는 칸이다.</summary>
        public string Scene { get; }

        /// <summary>다음 층에서 갈 수 있는 칸 Id. 열 순으로 정렬돼 있다.</summary>
        public IReadOnlyList<int> Next { get; }

        /// <summary>
        /// 이 칸의 방 크기 비율(%). 지도를 굴릴 때 정해져 <b>재시작해도 같다</b>(docs/Room_Size_Plan.md 2.1).
        /// 보스 · 비전투 칸은 100이다. 0 이하로 넘기면 100으로 읽는다.
        /// </summary>
        public int RoomPercent { get; }

        public bool HasScene => Scene.Length > 0;

        /// <param name="roomPercent">
        /// 기본값이 100인 이유 — 이 생성자를 부르는 실제 경로는 <see cref="RunMapGenerator"/> 하나이고 거기서는 항상 넘긴다.
        /// 나머지는 손으로 모양을 짜는 테스트라, 방 크기와 무관한 검사마다 100을 적게 하면 읽기만 나빠진다.
        /// 넘겼는지는 생성기 테스트가 본다.
        /// </param>
        public MapNode(int id, int floor, int column, MapNodeKind kind, string scene, IReadOnlyList<int> next,
                       int roomPercent = RoomRules.FullPercent)
        {
            Id = id;
            Floor = floor;
            Column = column;
            Kind = kind;
            Scene = scene != null ? scene.Trim() : "";
            Next = next != null ? new List<int>(next).ToArray() : new int[0];
            RoomPercent = RoomRules.Normalize(roomPercent);
        }

        public override string ToString() => HasScene ? $"#{Id} {Kind} {Scene}" : $"#{Id} {Kind}";
    }

    // ══ RunMap ═══════════════════════════════════════════

    /// <summary>
    /// 한 런의 지도. 순수 C# 객체라 <c>GameManager</c>가 씬을 넘어 들고 간다.
    /// 검사는 <see cref="RunMapRules.Issues(RunMap)"/>, 생성은 <see cref="RunMapGenerator"/>.
    /// </summary>
    public sealed class RunMap
    {
        private readonly MapNode[][] floors;
        private readonly Dictionary<int, MapNode> byId = new Dictionary<int, MapNode>();

        /// <summary>이 지도를 굴린 시드. 로그에 찍어 두면 같은 지도를 다시 만든다.</summary>
        public int Seed { get; }

        public RunMap(int seed, IReadOnlyList<MapNode[]> floors)
        {
            Seed = seed;

            int count = floors != null ? floors.Count : 0;
            this.floors = new MapNode[count][];

            for (int f = 0; f < count; f++)
            {
                this.floors[f] = floors[f] != null ? (MapNode[])floors[f].Clone() : new MapNode[0];

                foreach (MapNode n in this.floors[f])
                    if (n != null) byId[n.Id] = n;
            }
        }

        public int FloorCount => floors.Length;

        /// <summary>보스 층. 층이 없으면 -1.</summary>
        public int LastFloor => floors.Length - 1;

        public int NodeCount => byId.Count;

        /// <summary>그 층의 칸들, 열 순. 범위 밖이면 빈 목록.</summary>
        public IReadOnlyList<MapNode> NodesOn(int floor)
            => floor >= 0 && floor < floors.Length ? floors[floor] : (IReadOnlyList<MapNode>)new MapNode[0];

        public IEnumerable<MapNode> Nodes
        {
            get
            {
                foreach (MapNode[] floor in floors)
                    foreach (MapNode n in floor)
                        if (n != null) yield return n;
            }
        }

        /// <summary>Id로 칸을 찾는다. 없으면 null.</summary>
        public MapNode Get(int id) => byId.TryGetValue(id, out MapNode n) ? n : null;

        /// <summary>
        /// 지도 모양을 한 줄로. 로그 · 테스트가 "같은 지도인가"를 비교할 때 쓴다.
        /// <c>0[Battle:Stage_01>1,2] / 1[...]</c> 꼴이다.
        /// </summary>
        public string Describe()
        {
            var sb = new StringBuilder();

            for (int f = 0; f < floors.Length; f++)
            {
                if (f > 0) sb.Append(" / ");
                sb.Append(f).Append('[');

                for (int c = 0; c < floors[f].Length; c++)
                {
                    MapNode n = floors[f][c];
                    if (c > 0) sb.Append(' ');
                    if (n == null) { sb.Append("null"); continue; }

                    sb.Append(n.Kind).Append(':').Append(n.HasScene ? n.Scene : "-");
                    if (n.Next.Count > 0) sb.Append('>').Append(string.Join(",", n.Next));
                }

                sb.Append(']');
            }

            return sb.ToString();
        }

        /// <summary>
        /// 방 크기가 100이 아닌 칸만 한 줄로. <c>방: #3 90% · #4 105%</c> 꼴이고, 전부 100이면 빈 문자열.
        ///
        /// <see cref="Describe"/>에 섞지 않는다 — 그쪽은 "모양이 같은가"를 비교하는 문자열이라,
        /// 방 범위만 바꾼 레시피가 다른 지도로 보이면 안 된다.
        /// </summary>
        public string DescribeRooms()
        {
            var parts = new List<string>();

            foreach (MapNode n in Nodes)
                if (n.RoomPercent != RoomRules.FullPercent)
                    parts.Add($"#{n.Id} {n.RoomPercent}%");

            return parts.Count > 0 ? "방: " + string.Join(" · ", parts) : "";
        }
    }

    // ══ RunMapIssue ═══════════════════════════════════════════

    /// <summary>레시피 · 지도에서 잡히는 문제. 앞의 여섯은 레시피, 뒤는 굴린 지도다.</summary>
    public enum RunMapProblem
    {
        /// <summary>층이 하나도 없다. 런을 시작할 수 없다.</summary>
        NoFloors,

        /// <summary>후보가 없는 층. 칸을 놓을 수 없다.</summary>
        EmptyFloor,

        /// <summary>칸 수 하한이 1보다 작거나 상한이 하한보다 작다.</summary>
        BadNodeRange,

        /// <summary>비중이 0 이하인 후보. 영영 안 뽑힌다.</summary>
        ZeroWeight,

        /// <summary>씬이 비었는데 휴식 칸이 아니다. 지도에서 처리할 방법이 없어 들어가면 멈춘다.</summary>
        MissingScene,

        /// <summary>마지막 층이 아닌 곳에 보스가 있다. 그걸 깨도 런이 안 끝나 보스가 둘인 런이 된다.</summary>
        BossBeforeLastFloor,

        /// <summary>마지막 층이 보스 한 칸이 아니다. 끝이 둘이면 "전체 클리어"를 판정할 칸이 없다.</summary>
        LastFloorNotSingleBoss,

        /// <summary>마지막 층이 아닌데 나가는 간선이 없다. 들어가면 런이 멈춘다.</summary>
        NoOutgoing,

        /// <summary>0층이 아닌데 들어오는 간선이 없다. 영영 못 가는 칸이 그려진다.</summary>
        NoIncoming,

        /// <summary>간선이 다음 층이 아닌 곳이나 없는 칸을 가리킨다.</summary>
        BrokenEdge,

        /// <summary>간선 둘이 교차한다. 선이 엉켜 어느 칸으로 가는지 안 읽힌다.</summary>
        CrossingEdges,

        /// <summary>같은 씬이 연달아 이어진다. 갈림길이 선택처럼 안 느껴진다.</summary>
        SameSceneInARow,

        /// <summary>휴식 · 상점 · 이벤트가 같은 종류로 연달아 이어진다. 휴식 두 번이 선택지를 대신한다.</summary>
        SameNodeKindInARow,

        /// <summary>정해진 횟수만큼 굴려도 제약을 맞추는 지도가 안 나왔다. 레시피 후보가 너무 좁다.</summary>
        GenerationFailed,

        /// <summary>
        /// 레시피 층의 방 크기 범위가 잘못됐다 — 규칙 범위 밖, 5 단위가 아님, 하한 &gt; 상한, 한쪽만 0.
        /// 조용히 물리면 저작자는 자기가 적은 범위로 도는 줄 안다.
        /// </summary>
        BadRoomRange,

        /// <summary>굴린 지도의 칸 방 크기가 규칙에 안 맞는다 — 보스 · 비전투 칸이 100이 아니거나 전투 칸이 범위 밖. 생성기 버그다.</summary>
        BadRoomPercent,
    }

    /// <summary>잡힌 문제 하나. 레시피면 (층, 후보 번호), 지도면 (층, 칸 Id).</summary>
    public struct RunMapIssue
    {
        public int floor;
        public int index;
        public RunMapProblem problem;

        public RunMapIssue(int floor, int index, RunMapProblem problem)
        {
            this.floor = floor;
            this.index = index;
            this.problem = problem;
        }

        public string Describe()
        {
            switch (problem)
            {
                case RunMapProblem.NoFloors:               return "층이 하나도 없다.";
                case RunMapProblem.EmptyFloor:             return $"{floor}층: 후보가 없다.";
                case RunMapProblem.BadNodeRange:           return $"{floor}층: 칸 수 범위가 잘못됐다(하한 ≥ 1, 상한 ≥ 하한).";
                case RunMapProblem.ZeroWeight:             return $"{floor}층 {index}번 후보: 비중이 0 이하라 안 뽑힌다.";
                case RunMapProblem.MissingScene:           return $"{floor}층 {index}번: 씬이 비었는데 휴식 칸이 아니다.";
                case RunMapProblem.BossBeforeLastFloor:    return $"{floor}층 {index}번: 마지막 층이 아닌데 보스다.";
                case RunMapProblem.LastFloorNotSingleBoss: return $"{floor}층(마지막): 보스 한 칸이어야 한다.";
                case RunMapProblem.NoOutgoing:             return $"칸 #{index}({floor}층): 나가는 길이 없다.";
                case RunMapProblem.NoIncoming:             return $"칸 #{index}({floor}층): 들어오는 길이 없다.";
                case RunMapProblem.BrokenEdge:             return $"칸 #{index}({floor}층): 다음 층이 아닌 칸으로 이어진다.";
                case RunMapProblem.CrossingEdges:          return $"칸 #{index}({floor}층): 간선이 다른 간선과 교차한다.";
                case RunMapProblem.SameSceneInARow:        return $"칸 #{index}({floor}층): 다음 칸과 씬이 같다.";
                case RunMapProblem.SameNodeKindInARow:     return $"칸 #{index}({floor}층): 다음 칸과 같은 비전투 칸이다.";
                case RunMapProblem.GenerationFailed:       return "지도를 굴리지 못했다 — 레시피 후보가 제약에 비해 너무 좁다.";
                case RunMapProblem.BadRoomRange:           return $"{floor}층: 방 크기 범위가 잘못됐다({RoomRules.MinPercent}~{RoomRules.MaxPercent}, " +
                                                                  $"{RoomRules.Step} 단위, 하한 ≤ 상한. 둘 다 0이면 100% 고정).";
                case RunMapProblem.BadRoomPercent:         return $"칸 #{index}({floor}층): 방 크기가 규칙에 안 맞는다.";
                default:                                   return problem.ToString();
            }
        }
    }

    // ══ RunMapRules ═══════════════════════════════════════════

    /// <summary>
    /// 레시피와 지도의 <b>불변식 전부</b>. 생성기는 이 규칙으로 후보를 거르고,
    /// 다 만든 지도를 이 검사로 한 번 더 본다 — 거르는 쪽과 보는 쪽이 같은 함수라 어긋날 수 없다.
    /// </summary>
    public static class RunMapRules
    {
        /// <summary>싸우는 칸인가. 보정값 · 연속 금지가 이 갈래로 나뉜다.</summary>
        public static bool IsBattle(MapNodeKind kind)
            => kind == MapNodeKind.Battle || kind == MapNodeKind.Elite || kind == MapNodeKind.Boss;

        /// <summary>씬 없이 지도에서 처리할 수 있는 종류인가. 지금은 휴식뿐이다.</summary>
        public static bool CanSkipScene(MapNodeKind kind) => kind == MapNodeKind.Rest;

        /// <summary>
        /// 방 크기 보정을 받는 종류인가. 전투 · 정예뿐이다.
        /// <b>보스는 안 받는다</b> — 보스 패턴은 방 크기를 전제로 저작한다. 정예 강화를 보스에 안 거는 것(<see cref="EncounterModifierRules.IsElite"/>)과 같은 이유다.
        /// </summary>
        public static bool GetsRoomModifier(MapNodeKind kind) => kind == MapNodeKind.Battle || kind == MapNodeKind.Elite;

        /// <summary>레시피 층의 방 크기 범위가 저작 가능한가. 둘 다 0(고정 100%)이거나, 둘 다 쓸 수 있는 비율이고 하한 ≤ 상한.</summary>
        public static bool IsValidRoomRange(int min, int max)
        {
            if (min == 0 && max == 0) return true;
            if (min == 0 || max == 0) return false;

            return RoomRules.IsValidPercent(min) && RoomRules.IsValidPercent(max) && min <= max;
        }

        /// <summary>굴린 칸의 방 크기가 규칙에 맞는가. 보정 안 받는 칸은 100, 받는 칸은 쓸 수 있는 비율.</summary>
        public static bool IsValidRoomPercent(MapNodeKind kind, int percent)
            => GetsRoomModifier(kind)
                ? percent != 0 && RoomRules.IsValidPercent(percent)
                : percent == RoomRules.FullPercent;

        /// <summary>
        /// (a→b)와 (c→d)가 교차하는가. 넷 다 열 번호다. 같은 칸에서 나가거나 같은 칸으로 모이는 것은 교차가 아니다.
        /// </summary>
        public static bool Crosses(int a, int b, int c, int d) => (a < c && b > d) || (a > c && b < d);

        /// <summary>
        /// 이 칸 뒤에 저 칸이 올 수 있는가.
        /// 같은 씬이 연달아 오면 안 되고(빈 씬끼리는 같은 씬이 아니다), 비전투 칸은 같은 종류가 연달아 오면 안 된다.
        /// </summary>
        public static bool CanFollow(MapNodeKind fromKind, string fromScene, MapNodeKind toKind, string toScene)
        {
            string a = fromScene != null ? fromScene.Trim() : "";
            string b = toScene != null ? toScene.Trim() : "";

            if (a.Length > 0 && string.Equals(a, b, StringComparison.Ordinal)) return false;
            if (!IsBattle(fromKind) && fromKind == toKind) return false;

            return true;
        }

        // ── 레시피 ──────────────────────────────────────

        public static List<RunMapIssue> RecipeIssues(IReadOnlyList<FloorRule> floors)
        {
            var issues = new List<RunMapIssue>();

            if (floors == null || floors.Count == 0)
            {
                issues.Add(new RunMapIssue(-1, -1, RunMapProblem.NoFloors));
                return issues;
            }

            int last = floors.Count - 1;

            for (int f = 0; f < floors.Count; f++)
            {
                FloorRule floor = floors[f];

                if (floor.CandidateCount == 0)
                    issues.Add(new RunMapIssue(f, -1, RunMapProblem.EmptyFloor));

                if (floor.minNodes < 1 || floor.maxNodes < floor.minNodes)
                    issues.Add(new RunMapIssue(f, -1, RunMapProblem.BadNodeRange));

                if (!IsValidRoomRange(floor.roomPercentMin, floor.roomPercentMax))
                    issues.Add(new RunMapIssue(f, -1, RunMapProblem.BadRoomRange));

                bool lastIsBossOnly = f != last || (floor.minNodes == 1 && floor.maxNodes == 1 && floor.CandidateCount > 0);

                for (int i = 0; i < floor.CandidateCount; i++)
                {
                    NodeCandidate c = floor.candidates[i];

                    if (c.weight <= 0)
                        issues.Add(new RunMapIssue(f, i, RunMapProblem.ZeroWeight));

                    if (!c.HasScene && !CanSkipScene(c.kind))
                        issues.Add(new RunMapIssue(f, i, RunMapProblem.MissingScene));

                    if (f != last && c.kind == MapNodeKind.Boss)
                        issues.Add(new RunMapIssue(f, i, RunMapProblem.BossBeforeLastFloor));

                    if (f == last && c.kind != MapNodeKind.Boss)
                        lastIsBossOnly = false;
                }

                if (f == last && !lastIsBossOnly)
                    issues.Add(new RunMapIssue(f, -1, RunMapProblem.LastFloorNotSingleBoss));
            }

            return issues;
        }

        // ── 지도 ────────────────────────────────────────

        /// <summary>
        /// 굴린 지도가 불변식을 지키는가. 계획서 2.3의 표 그대로다.
        /// <b>순서가 곧 읽는 순서다</b> — 층 모양(보스 층) → 칸마다 길 · 이웃 → 교차.
        /// </summary>
        public static List<RunMapIssue> Issues(RunMap map)
        {
            var issues = new List<RunMapIssue>();

            if (map == null || map.FloorCount == 0)
            {
                issues.Add(new RunMapIssue(-1, -1, RunMapProblem.NoFloors));
                return issues;
            }

            int last = map.LastFloor;

            IReadOnlyList<MapNode> bossFloor = map.NodesOn(last);
            if (bossFloor.Count != 1 || bossFloor[0] == null || bossFloor[0].Kind != MapNodeKind.Boss)
                issues.Add(new RunMapIssue(last, -1, RunMapProblem.LastFloorNotSingleBoss));

            var incoming = new Dictionary<int, int>();

            for (int f = 0; f < map.FloorCount; f++)
            {
                foreach (MapNode n in map.NodesOn(f))
                {
                    if (n == null) continue;

                    if (!n.HasScene && !CanSkipScene(n.Kind))
                        issues.Add(new RunMapIssue(f, n.Id, RunMapProblem.MissingScene));

                    if (f != last && n.Kind == MapNodeKind.Boss)
                        issues.Add(new RunMapIssue(f, n.Id, RunMapProblem.BossBeforeLastFloor));

                    if (!IsValidRoomPercent(n.Kind, n.RoomPercent))
                        issues.Add(new RunMapIssue(f, n.Id, RunMapProblem.BadRoomPercent));

                    if (f == last) continue;

                    if (n.Next.Count == 0)
                        issues.Add(new RunMapIssue(f, n.Id, RunMapProblem.NoOutgoing));

                    bool broken = false, sameScene = false, sameKind = false;

                    foreach (int id in n.Next)
                    {
                        MapNode to = map.Get(id);
                        if (to == null || to.Floor != f + 1) { broken = true; continue; }

                        incoming[id] = incoming.TryGetValue(id, out int k) ? k + 1 : 1;

                        if (!CanFollow(n.Kind, n.Scene, to.Kind, to.Scene))
                        {
                            if (n.HasScene && n.Scene == to.Scene) sameScene = true;
                            else sameKind = true;
                        }
                    }

                    if (broken)    issues.Add(new RunMapIssue(f, n.Id, RunMapProblem.BrokenEdge));
                    if (sameScene) issues.Add(new RunMapIssue(f, n.Id, RunMapProblem.SameSceneInARow));
                    if (sameKind)  issues.Add(new RunMapIssue(f, n.Id, RunMapProblem.SameNodeKindInARow));
                }
            }

            for (int f = 1; f < map.FloorCount; f++)
                foreach (MapNode n in map.NodesOn(f))
                    if (n != null && !incoming.ContainsKey(n.Id))
                        issues.Add(new RunMapIssue(f, n.Id, RunMapProblem.NoIncoming));

            for (int f = 0; f < last; f++)
                CollectCrossings(map, f, issues);

            return issues;
        }

        /// <summary>한 층에서 나가는 간선끼리 교차를 본다. 교차 한 쌍마다 왼쪽 칸 하나로 적는다.</summary>
        private static void CollectCrossings(RunMap map, int floor, List<RunMapIssue> issues)
        {
            IReadOnlyList<MapNode> nodes = map.NodesOn(floor);

            for (int x = 0; x < nodes.Count; x++)
            {
                MapNode a = nodes[x];
                if (a == null) continue;

                bool crosses = false;

                for (int y = 0; y < nodes.Count && !crosses; y++)
                {
                    MapNode c = nodes[y];
                    if (c == null || c.Column <= a.Column) continue;

                    foreach (int ab in a.Next)
                    {
                        MapNode b = map.Get(ab);
                        if (b == null) continue;

                        foreach (int cd in c.Next)
                        {
                            MapNode d = map.Get(cd);
                            if (d != null && Crosses(a.Column, b.Column, c.Column, d.Column)) { crosses = true; break; }
                        }

                        if (crosses) break;
                    }
                }

                if (crosses) issues.Add(new RunMapIssue(floor, a.Id, RunMapProblem.CrossingEdges));
            }
        }
    }

    // ══ RunMapGenerator ═══════════════════════════════════════════

    /// <summary>
    /// 레시피와 시드로 지도를 굴린다. <b><see cref="System.Random"/>만 쓰는 순수 함수</b>라
    /// 같은 입력이면 같은 지도가 나온다 — <c>UnityEngine.Random</c>은 전역 상태라 테스트 순서에 따라 결과가 바뀐다.
    ///
    /// 순서는 <b>칸 수 → 간선 → 내용</b>이다. 내용(씬 · 종류)은 부모 칸을 보고 걸러야 해서 간선보다 나중이다.
    /// </summary>
    public static class RunMapGenerator
    {
        /// <summary>
        /// 제약을 못 맞췄을 때 다시 굴리는 횟수. 넘으면 조용히 물리지 않고 실패를 돌려준다(계획서 2.3).
        /// </summary>
        public const int MaxAttempts = 32;

        /// <summary>단조 연결 뒤, 칸마다 이웃 칸으로 간선을 하나 더 얹을 확률. 갈림길이 여기서 생긴다.</summary>
        public const double ExtraEdgeChance = 0.4;

        /// <summary>
        /// 지도를 굴린다. 레시피가 잘못됐거나 <see cref="MaxAttempts"/>번 안에 제약을 못 맞추면 거짓이고
        /// <paramref name="map"/>은 null, <paramref name="issues"/>에 이유가 든다.
        /// </summary>
        public static bool TryGenerate(IReadOnlyList<FloorRule> floors, int seed,
                                       out RunMap map, out List<RunMapIssue> issues)
        {
            map = null;

            issues = RunMapRules.RecipeIssues(floors);
            if (issues.Count > 0) return false;

            var rng = new System.Random(seed);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                RunMap rolled = Roll(floors, seed, rng);
                if (rolled == null) continue;

                // 거르면서 만들었으니 여기서 걸리면 생성기 버그다. 그래도 내보내지 않는다.
                issues = RunMapRules.Issues(rolled);
                if (issues.Count > 0) return false;

                map = rolled;
                return true;
            }

            issues = new List<RunMapIssue> { new RunMapIssue(-1, -1, RunMapProblem.GenerationFailed) };
            return false;
        }

        /// <summary>한 번 굴린다. 어떤 칸에도 놓을 후보가 없으면 null.</summary>
        private static RunMap Roll(IReadOnlyList<FloorRule> floors, int seed, System.Random rng)
        {
            int floorCount = floors.Count;

            var counts = new int[floorCount];
            for (int f = 0; f < floorCount; f++)
                counts[f] = rng.Next(floors[f].minNodes, floors[f].maxNodes + 1);

            // edges[f][i] = f층 i열에서 f+1층으로 가는 열 번호들
            var edges = new List<int>[floorCount][];
            for (int f = 0; f < floorCount - 1; f++)
                edges[f] = Connect(counts[f], counts[f + 1], rng);

            var picked = new NodeCandidate[floorCount][];

            for (int f = 0; f < floorCount; f++)
            {
                picked[f] = new NodeCandidate[counts[f]];
                var usedOnFloor = new HashSet<string>();

                for (int c = 0; c < counts[f]; c++)
                {
                    if (!TryPick(floors[f].candidates, ParentsOf(f, c, edges, picked), usedOnFloor, rng,
                                 out NodeCandidate pick))
                        return null;

                    picked[f][c] = pick;
                    usedOnFloor.Add(KeyOf(in pick));
                }
            }

            return Build(floors, seed, counts, edges, picked);
        }

        /// <summary>
        /// 위층 n1칸을 아래층 n2칸에 <b>단조로</b> 잇는다.
        ///
        /// ① i열 → round(i·(n2−1)/(n1−1))열. 위층 순서대로 아래층 번호가 줄지 않으므로 교차가 없다.
        /// ② 아무도 안 들어오는 아래층 칸은 "그 칸보다 왼쪽을 가리키는 마지막 위층 칸"에 잇는다.
        ///    그 위층 칸의 오른쪽 이웃은 이 칸보다 오른쪽을 가리키므로 역시 교차가 없다.
        /// ③ 칸마다 확률로 이웃 열에 하나 더. 이것만 교차를 직접 검사한다.
        /// </summary>
        private static List<int>[] Connect(int n1, int n2, System.Random rng)
        {
            var targets = new List<int>[n1];
            var baseTarget = new int[n1];
            var reached = new bool[n2];

            for (int i = 0; i < n1; i++)
            {
                baseTarget[i] = n1 == 1
                    ? (n2 - 1) / 2
                    : (int)Math.Round(i * (n2 - 1) / (double)(n1 - 1), MidpointRounding.AwayFromZero);

                targets[i] = new List<int> { baseTarget[i] };
                reached[baseTarget[i]] = true;
            }

            for (int j = 0; j < n2; j++)
            {
                if (reached[j]) continue;

                int from = 0;
                for (int i = 0; i < n1; i++)
                    if (baseTarget[i] < j) from = i;

                targets[from].Add(j);
            }

            for (int i = 0; i < n1; i++)
            {
                // 난수 소비 횟수를 칸마다 같게 둔다 — 조건에 따라 건너뛰면 뒤 칸의 결과가 밀린다.
                bool extra = rng.NextDouble() < ExtraEdgeChance;
                bool right = rng.Next(2) == 1;
                if (!extra) continue;

                int t = right ? Max(targets[i]) + 1 : Min(targets[i]) - 1;
                if (t < 0 || t >= n2) continue;

                if (!CrossesAny(targets, i, t)) targets[i].Add(t);
            }

            foreach (List<int> list in targets) list.Sort();
            return targets;
        }

        private static bool CrossesAny(List<int>[] targets, int from, int to)
        {
            for (int k = 0; k < targets.Length; k++)
            {
                if (k == from) continue;

                foreach (int d in targets[k])
                    if (RunMapRules.Crosses(from, to, k, d)) return true;
            }

            return false;
        }

        /// <summary>f층 c열로 들어오는 위층 칸들의 (종류, 씬). 0층이면 빈 목록.</summary>
        private static List<NodeCandidate> ParentsOf(int f, int c, List<int>[][] edges, NodeCandidate[][] picked)
        {
            var parents = new List<NodeCandidate>();
            if (f == 0) return parents;

            for (int i = 0; i < edges[f - 1].Length; i++)
                if (edges[f - 1][i].Contains(c)) parents.Add(picked[f - 1][i]);

            return parents;
        }

        /// <summary>
        /// 부모 전부를 따라올 수 있는 후보 중 비중대로 하나.
        /// 같은 층에 이미 나온 씬은 <b>될 수 있으면</b> 피한다 — 피할 후보가 없으면 겹쳐도 된다.
        /// 이건 취향이지 불변식이 아니라서, 막히면 물러서도 지도가 틀리지 않는다.
        /// </summary>
        private static bool TryPick(NodeCandidate[] candidates, List<NodeCandidate> parents,
                                    HashSet<string> usedOnFloor, System.Random rng, out NodeCandidate pick)
        {
            pick = default;

            var allowed = new List<NodeCandidate>();
            foreach (NodeCandidate c in candidates)
            {
                if (c.weight <= 0) continue;

                bool ok = true;
                foreach (NodeCandidate p in parents)
                    if (!RunMapRules.CanFollow(p.kind, p.Scene, c.kind, c.Scene)) { ok = false; break; }

                if (ok) allowed.Add(c);
            }

            if (allowed.Count == 0) return false;

            var fresh = allowed.FindAll(c => !usedOnFloor.Contains(KeyOf(in c)));
            List<NodeCandidate> bag = fresh.Count > 0 ? fresh : allowed;

            int total = 0;
            foreach (NodeCandidate c in bag) total += c.weight;

            int roll = rng.Next(total);
            foreach (NodeCandidate c in bag)
            {
                roll -= c.weight;
                if (roll < 0) { pick = c; return true; }
            }

            pick = bag[bag.Count - 1];
            return true;
        }

        /// <summary>같은 층 겹침을 볼 때의 이름. 씬 없는 칸은 종류로 센다.</summary>
        private static string KeyOf(in NodeCandidate c) => c.HasScene ? c.Scene : "kind:" + c.kind;

        /// <summary>
        /// 이 칸의 방 크기 비율. 보정 안 받는 칸이거나 층에 범위가 없으면 100.
        ///
        /// <b>지도 모양을 굴린 <c>rng</c>를 쓰지 않는다</b>(docs/Room_Size_Plan.md 2.2). 거기서 뽑으면 뒤의 난수가 전부 밀려
        /// ① 이 기능을 넣는 순간 로그에 남긴 시드가 다른 지도가 되고, ② 레시피에서 방 범위만 고쳐도 갈림길 모양이 흔들린다.
        /// 칸마다 (시드, 칸 Id)로 만든 전용 난수에서 한 번 뽑는다 — 시도(<see cref="MaxAttempts"/>)가 몇 번 돌았는지와도 무관하다.
        /// </summary>
        public static int RollRoomPercent(in FloorRule floor, MapNodeKind kind, int seed, int nodeId)
        {
            if (!RunMapRules.GetsRoomModifier(kind) || !floor.HasRoomRange) return RoomRules.FullPercent;

            int lo = floor.roomPercentMin / RoomRules.Step;
            int hi = floor.roomPercentMax / RoomRules.Step;

            var rng = new System.Random(RoomSeed(seed, nodeId));
            return rng.Next(lo, hi + 1) * RoomRules.Step;
        }

        /// <summary>
        /// 칸 전용 난수의 시드. 섞기 함수(murmur 끝단)를 한 번 거친다 —
        /// <see cref="System.Random"/>은 이웃한 시드의 첫 값이 서로 닮아서, 시드와 Id를 그냥 더하면 칸마다 비율이 줄지어 나온다.
        /// </summary>
        public static int RoomSeed(int seed, int nodeId)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)nodeId * 0x85EBCA77u;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        private static RunMap Build(IReadOnlyList<FloorRule> rules, int seed, int[] counts, List<int>[][] edges, NodeCandidate[][] picked)
        {
            var offset = new int[counts.Length];
            for (int f = 1; f < counts.Length; f++) offset[f] = offset[f - 1] + counts[f - 1];

            var floors = new MapNode[counts.Length][];

            for (int f = 0; f < counts.Length; f++)
            {
                floors[f] = new MapNode[counts[f]];

                for (int c = 0; c < counts[f]; c++)
                {
                    var next = new List<int>();
                    if (f < counts.Length - 1)
                        foreach (int t in edges[f][c]) next.Add(offset[f + 1] + t);

                    NodeCandidate p = picked[f][c];
                    int id = offset[f] + c;
                    int room = RollRoomPercent(rules[f], p.kind, seed, id);

                    floors[f][c] = new MapNode(id, f, c, p.kind, p.Scene, next, room);
                }
            }

            return new RunMap(seed, floors);
        }

        private static int Max(List<int> list)
        {
            int m = list[0];
            foreach (int v in list) if (v > m) m = v;
            return m;
        }

        private static int Min(List<int> list)
        {
            int m = list[0];
            foreach (int v in list) if (v < m) m = v;
            return m;
        }
    }

    // ══ RunMapProgress ═══════════════════════════════════════════

    /// <summary>
    /// 지도 위 어디까지 왔는가. <b>값 두 개뿐이다</b> — 마지막으로 끝낸 칸과 들어갔지만 아직 안 끝낸 칸.
    ///
    /// "지도 고르는 중 / 전투 중" 같은 상태 enum을 두지 않는다. 그건 지금 어느 씬이 떠 있는가와 같은 정보라,
    /// 따로 들면 씬 전환이 거절됐을 때 둘이 어긋난다(계획서 1.2).
    ///
    /// 패배 후 재시작은 <see cref="Pending"/>을 다시 열면 된다 — 끝내지 않았으니 되돌릴 것이 없다.
    /// </summary>
    public sealed class RunMapProgress
    {
        public const int None = -1;

        private readonly List<int> visited = new List<int>();

        public RunMap Map { get; }

        /// <summary>마지막으로 끝낸 칸. 시작 전이면 <see cref="None"/>.</summary>
        public int Current { get; private set; } = None;

        /// <summary>골라서 들어갔지만 아직 안 끝낸 칸. 없으면 <see cref="None"/>.</summary>
        public int Pending { get; private set; } = None;

        /// <summary>끝낸 칸들, 지나온 순서.</summary>
        public IReadOnlyList<int> Visited => visited;

        public RunMapProgress(RunMap map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public bool HasPending => Pending != None;

        public MapNode CurrentNode => Map.Get(Current);
        public MapNode PendingNode => Map.Get(Pending);

        /// <summary>보스 층을 끝냈는가.</summary>
        public bool IsFinished => CurrentNode != null && CurrentNode.Floor >= Map.LastFloor;

        /// <summary>
        /// 사람에게 보여 줄 층 번호, 1부터. 들어가 있으면 그 칸의 층, 아니면 다음에 고를 층이다.
        /// 다 끝났으면 마지막 층 번호에 머문다.
        /// </summary>
        public int FloorNumber
        {
            get
            {
                int floor = PendingNode != null ? PendingNode.Floor
                          : CurrentNode != null ? CurrentNode.Floor + 1
                          : 0;

                return Mathf.Clamp(floor, 0, Math.Max(0, Map.LastFloor)) + 1;
            }
        }

        /// <summary>
        /// 다음에 고를 수 있는 칸들. 들어가 있는 동안에도 같은 목록을 준다 — 지도 화면이 그리는 용도다.
        /// 고를 수 있는지는 <see cref="CanSelect"/>가 따로 본다.
        /// </summary>
        public List<MapNode> Choices()
        {
            var choices = new List<MapNode>();
            if (IsFinished) return choices;

            if (CurrentNode == null)
            {
                choices.AddRange(Map.NodesOn(0));
                return choices;
            }

            foreach (int id in CurrentNode.Next)
            {
                MapNode n = Map.Get(id);
                if (n != null) choices.Add(n);
            }

            return choices;
        }

        public bool HasVisited(int id) => visited.Contains(id);

        /// <summary>들어가 있는 칸이 없고, 그 칸이 지금 선택지에 있는가.</summary>
        public bool CanSelect(int id)
        {
            if (HasPending) return false;

            foreach (MapNode n in Choices())
                if (n.Id == id) return true;

            return false;
        }

        /// <summary>
        /// 칸에 들어간다. 선택지에 없거나 이미 들어가 있으면 거절하고 아무것도 안 바꾼다.
        /// <b>씬 전환이 접수된 뒤에 부른다</b> — 먼저 부르고 전환이 거절되면 들어가지도 못한 칸에 묶인다.
        /// </summary>
        public bool TrySelect(int id)
        {
            if (!CanSelect(id)) return false;

            Pending = id;
            return true;
        }

        /// <summary>들어가 있던 칸을 끝낸다. 들어가 있지 않으면 거짓.</summary>
        public bool CompletePending()
        {
            if (!HasPending) return false;

            Current = Pending;
            Pending = None;
            visited.Add(Current);
            return true;
        }
    }
}
