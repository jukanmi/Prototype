// 런 지도 화면 — 칸 · 선 · 상태 색 · 키보드 이동.
//
// 그리기(RunMapScreen)와 판단(RunMapViewRules · RunMapLayout)을 나눴다. 칸이 어떤 상태인지,
// 선이 지나온 길인지, 방향키가 어디로 가는지는 씬 없이 테스트가 본다.
// 그림이 없어서 전부 코드로 짓는다(NodeWindows와 같은 방침). 아이콘이 생기면 칸 버튼만 바꾼다.
//
// 계획서: docs/Run_Map_Plan.md (2.6 · 2단계)

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

        /// <summary>칸을 골랐을 때 아래 설명줄에 뜨는 글. 수치는 규칙에서 읽는다 — 글과 실제가 어긋나지 않게.</summary>
        public static string Describe(MapNode node)
        {
            if (node == null) return "";

            switch (node.Kind)
            {
                case MapNodeKind.Elite:
                    return $"정예 — 강화된 적이 {EncounterModifierRules.EliteNodeEvery}기마다 1기 섞여 나온다. " +
                           $"클리어 골드 x{GoldRules.ClearRewardPercent(MapNodeKind.Elite) / 100f:0.##}";
                case MapNodeKind.Rest:
                    return $"휴식 — 파티 전원의 체력을 {NodeRules.RestHealRatio:0%} 회복한다.";
                case MapNodeKind.Shop:
                    return "상점 — 골드로 카드를 산다.";
                case MapNodeKind.Event:
                    return "이벤트 — 무슨 일이 일어날지 모른다.";
                case MapNodeKind.Boss:
                    return "보스 — 이 칸을 깨면 런이 끝난다.";
                default:
                    return "전투 — 적을 모두 쓰러뜨린다.";
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

    // ══ RunMapScreen ═══════════════════════════════════════════

    /// <summary>
    /// 지도 씬의 화면. 진행(<see cref="RunMapProgress"/>)을 받아 그리고, 칸을 누르면 넘겨받은 콜백을 부른다.
    /// <b>무엇을 할지는 부르는 쪽이 정한다</b> — 3단계에서 <c>GameManager</c>가 "그 칸으로 씬 전환"을 넘긴다.
    ///
    /// <c>GameManager</c>가 런을 들고 있으면 그 진행을 그리고, 칸을 누르면 그 칸의 씬으로 간다.
    /// 없으면(지도 씬 단독 실행) <b>미리보기</b>다 — 레시피를 고를 때 게임을 처음부터 안 켜도 되게,
    /// 누르면 그 칸을 끝낸 것으로 치고 다음 층으로 걸어간다. [다시 굴리기]가 새 시드로 바꾼다.
    ///
    /// 콜백 안에서 이 컴포넌트를 만지지 않는다 — 실제 흐름에서는 전환이 끝날 때 이미 파괴돼 있다
    /// (<c>MainMenuController</c> 주석과 같은 함정). 미리보기만 같은 씬에서 다시 그린다.
    /// </summary>
    public class RunMapScreen : MonoBehaviour
    {
        [Header("미리보기 (GameManager 가 지도를 안 줄 때)")]
        [Tooltip("미리보기로 굴릴 레시피.")]
        [SerializeField] private RunMapRecipe previewRecipe;

        [Tooltip("0이면 켤 때마다 새 시드. 숫자를 넣으면 그 지도를 다시 본다.")]
        [SerializeField] private int previewSeed;

        private static readonly Color BackdropColor = new Color(0.05f, 0.06f, 0.08f, 1f);
        private static readonly Color FocusColor = new Color(1f, 0.86f, 0.45f);
        private static readonly Color HeaderColor = new Color(1f, 0.86f, 0.45f);
        private static readonly Color InfoColor = new Color(0.85f, 0.87f, 0.90f);
        private static readonly Color FloorLabelColor = new Color(0.55f, 0.58f, 0.62f);

        private const float EdgeThickness = 6f;
        private const float FocusPad = 8f;

        private RectTransform frame;
        private RectTransform mapRoot;
        private RectTransform focus;
        private Text header;
        private Text info;

        private RunMapProgress progress;
        private Action<MapNode> onPick;
        private bool preview;

        private readonly Dictionary<GameObject, MapNode> nodeOf = new Dictionary<GameObject, MapNode>();
        private readonly Dictionary<int, Button> buttonOf = new Dictionary<int, Button>();
        private readonly List<Selectable> selectables = new List<Selectable>();
        private GameObject lastSelected;

        public RunMapProgress Progress => progress;
        public RunMapRecipe PreviewRecipe => previewRecipe;

        private void Start()
        {
            UiKit.EnsureEventSystem();
            BuildFrame();

            GameManager gm = GameManager.Instance;

            if (gm != null && gm.Progress != null)
            {
                preview = false;
                Show(gm.Progress, EnterNode);
                return;
            }

            if (gm != null)
                Debug.LogWarning("[RunMap] GameManager 는 있는데 런이 없다 — 미리보기로 띄운다. 칸을 골라도 전투로 안 간다.");

            StartPreview(previewSeed != 0 ? previewSeed : Environment.TickCount);
        }

        /// <summary>
        /// 실제 런에서 칸을 골랐다. 전환은 <see cref="GameManager.EnterNode"/>가 하고,
        /// 콜백은 전부 static 만 만진다 — 전환이 끝날 때 이 화면은 이미 파괴돼 있다.
        ///
        /// 씬 없는 칸(지도에서 바로 회복)만 전환이 없어서 같은 화면을 다시 그린다.
        /// 페이드 도중 다시 눌러도 진행이 이미 "들어가 있음"이라 <c>EnterNode</c>가 거절한다.
        /// </summary>
        private void EnterNode(MapNode node)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            Action bgm = RunMapRules.IsBattle(node.Kind)
                ? (Action)(() => AudioManager.Instance?.PlayBattleBgm())
                : () => AudioManager.Instance?.PlayMenuBgm();

            if (!gm.EnterNode(node.Id, gameObject.scene.name, bgm)) return;

            if (!node.HasScene) Redraw();
        }

        private void ReturnToMainMenu()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            gm.ReturnToMainMenu(gameObject.scene.name, () => AudioManager.Instance?.PlayMenuBgm());
        }

        /// <summary>진행을 그린다. <paramref name="pick"/>은 고를 수 있는 칸을 눌렀을 때만 불린다.</summary>
        public void Show(RunMapProgress runProgress, Action<MapNode> pick)
        {
            progress = runProgress;
            onPick = pick;
            Redraw();
        }

        // ── 미리보기 ────────────────────────────────────

        private void StartPreview(int seed)
        {
            preview = true;

            if (previewRecipe == null)
            {
                Debug.LogError("[RunMap] 미리보기 레시피가 비어 있다.", this);
                SetInfo("미리보기 레시피가 비어 있다.");
                return;
            }

            if (!previewRecipe.TryGenerate(seed, out RunMap map, out List<RunMapIssue> issues))
            {
                string why = issues.Count > 0 ? issues[0].Describe() : "";
                Debug.LogError($"[RunMap] 시드 {seed} 로 지도를 못 굴렸다 — {why}", this);
                SetInfo("지도를 못 굴렸다: " + why);
                return;
            }

            Debug.Log($"[RunMap] 미리보기 — 시드 {seed}\n{map.Describe()}");
            Show(new RunMapProgress(map), PreviewPick);
        }

        /// <summary>미리보기에서는 들어갔다 바로 끝낸 것으로 친다.</summary>
        private void PreviewPick(MapNode node)
        {
            progress.TrySelect(node.Id);
            progress.CompletePending();
            Redraw();

            if (progress.IsFinished) SetInfo("보스까지 왔다 — 미리보기 끝. [다시 굴리기]로 새 지도.");
        }

        // ── 틀 ─────────────────────────────────────────

        private void BuildFrame()
        {
            var canvasGo = new GameObject("RunMapCanvas");
            canvasGo.transform.SetParent(transform, false);
            UiKit.BuildCanvas(canvasGo, UiLayer.NodeWindow);

            Image backdrop = UiKit.NewImage(canvasGo.transform, "Backdrop", BackdropColor, raycast: true);
            UiKit.Stretch(backdrop.rectTransform);
            frame = backdrop.rectTransform;

            header = UiKit.NewText(frame, "Header", 34, HeaderColor, FontStyle.Bold);
            UiKit.Place(header, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(1200f, 50f));

            info = UiKit.NewText(frame, "Info", 26, InfoColor, FontStyle.Normal);
            UiKit.Place(info, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1500f, 44f));
        }

        private void SetInfo(string text)
        {
            if (info != null) info.text = text;
        }

        // ── 그리기 ──────────────────────────────────────

        private void Redraw()
        {
            if (progress == null || frame == null) return;

            if (mapRoot != null) Destroy(mapRoot.gameObject);
            nodeOf.Clear();
            buttonOf.Clear();
            selectables.Clear();
            lastSelected = null;

            mapRoot = UiKit.NewRect(frame, "Map");
            UiKit.Stretch(mapRoot);

            RunMap map = progress.Map;
            HashSet<int> reach = RunMapViewRules.Reachable(progress);

            DrawFloorLabels(map);
            DrawEdges(map, reach);

            // 칸보다 먼저 깔아야 테두리가 칸 뒤에 선다.
            Image f = UiKit.NewImage(mapRoot, "Focus", FocusColor);
            focus = UiKit.Place(f, new Vector2(0.5f, 0.5f), Vector2.zero, RunMapLayout.NodeSize + Vector2.one * FocusPad * 2f);
            focus.gameObject.SetActive(false);

            foreach (MapNode n in map.Nodes) DrawNode(map, n, RunMapViewRules.StateOf(progress, n, reach));

            WireNavigation();
            DrawHeader(map);
            DrawCornerButton();

            SetInfo(selectables.Count > 0 ? "칸을 고른다 — 방향키 · Enter 또는 클릭" : "");
            if (selectables.Count > 0) Select(selectables[0].gameObject);
        }

        private void DrawHeader(RunMap map)
        {
            string gold = $"골드 {RunProgression.Current.Gold}";
            string where = progress.IsFinished ? "완주" : $"{progress.FloorNumber}층 / {map.FloorCount}층";
            string tag = preview ? $"   (미리보기 · 시드 {map.Seed})" : "";

            header.text = $"{where}   ·   {gold}{tag}";
        }

        private void DrawFloorLabels(RunMap map)
        {
            for (int f = 0; f < map.FloorCount; f++)
            {
                float x = RunMapLayout.PositionOf(f, 0, 1, map.FloorCount).x;
                string label = f == map.LastFloor ? "보스" : $"{f + 1}층";

                Text t = UiKit.CreateText(mapRoot, $"Floor_{f}", label, 22, FloorLabelColor);
                UiKit.Place(t, new Vector2(0.5f, 0.5f), new Vector2(x, RunMapLayout.Height * 0.5f + 110f), new Vector2(200f, 32f));
            }
        }

        private void DrawEdges(RunMap map, HashSet<int> reach)
        {
            foreach (MapNode from in map.Nodes)
                foreach (int id in from.Next)
                {
                    MapNode to = map.Get(id);
                    if (to == null) continue;

                    MapEdgeState state = RunMapViewRules.EdgeOf(progress, from, to, reach);
                    Line(RunMapLayout.PositionOf(map, from), RunMapLayout.PositionOf(map, to), EdgeColor(state),
                         state == MapEdgeState.Open || state == MapEdgeState.Taken ? EdgeThickness : EdgeThickness * 0.6f);
                }
        }

        /// <summary>늘인 이미지 한 장으로 선을 긋는다. 중점에 놓고 길이만큼 늘여 기울인다.</summary>
        private void Line(Vector2 a, Vector2 b, Color color, float thickness)
        {
            Image img = UiKit.NewImage(mapRoot, "Edge", color);
            Vector2 d = b - a;

            RectTransform rect = UiKit.Place(img, new Vector2(0.5f, 0.5f), (a + b) * 0.5f, new Vector2(d.magnitude, thickness));
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        private void DrawNode(RunMap map, MapNode node, MapNodeState state)
        {
            string sub = node.HasScene ? $"\n<size=15>{node.Scene}</size>" : "";
            Button b = UiKit.CreateButton(mapRoot, $"Node_{node.Id}", RunMapViewRules.KindLabel(node.Kind) + sub,
                                          NodeColor(node.Kind, state), RunMapLayout.NodeSize, 28);
            UiKit.Place(b, new Vector2(0.5f, 0.5f), RunMapLayout.PositionOf(map, node), RunMapLayout.NodeSize);

            // 색은 상태가 이미 정했다. 버튼 틴트가 한 번 더 어둡게 하면 잠긴 칸과 먼 칸이 구분이 안 된다.
            ColorBlock colors = b.colors;
            colors.disabledColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.selectedColor = Color.white;
            b.colors = colors;

            Text label = b.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.supportRichText = true;
                label.color = state == MapNodeState.Closed ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            }

            bool selectable = state == MapNodeState.Selectable;
            b.interactable = selectable;
            b.navigation = new Navigation { mode = Navigation.Mode.None };

            if (selectable)
            {
                MapNode captured = node;
                b.onClick.AddListener(() => Pick(captured));
            }

            nodeOf[b.gameObject] = node;
            buttonOf[node.Id] = b;
        }

        /// <summary>
        /// 고를 수 있는 칸끼리만 위아래로 잇는다. 순서는 <see cref="RunMapViewRules.NavigationOrder"/>가 정한다 —
        /// 화면이 따로 정렬하면 테스트가 보는 순서와 실제 방향키 순서가 갈릴 수 있다.
        /// </summary>
        private void WireNavigation()
        {
            foreach (MapNode n in RunMapViewRules.NavigationOrder(progress))
                if (buttonOf.TryGetValue(n.Id, out Button b)) selectables.Add(b);

            for (int i = 0; i < selectables.Count; i++)
            {
                selectables[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? selectables[i - 1] : null,
                    selectOnDown = i + 1 < selectables.Count ? selectables[i + 1] : null,
                };
            }
        }

        /// <summary>
        /// 좌상단 버튼. 미리보기면 [다시 굴리기], 실제 런이면 [메인 메뉴] —
        /// 지도 씬에는 전투 씬의 ESC 이탈이 없어서, 이게 없으면 런을 접을 방법이 없다.
        /// 방향키가 칸 대신 이 버튼에 얹히지 않게 내비게이션에서 뺀다.
        /// </summary>
        private void DrawCornerButton()
        {
            string label = preview ? "다시 굴리기" : "메인 메뉴";
            Button b = UiKit.CreateButton(mapRoot, "Btn_Corner", label, new Color(0.30f, 0.32f, 0.36f),
                                          new Vector2(220f, 56f), 22);
            UiKit.Place(b, new Vector2(0f, 1f), new Vector2(150f, -56f), new Vector2(220f, 56f));
            b.navigation = new Navigation { mode = Navigation.Mode.None };

            if (preview) b.onClick.AddListener(() => StartPreview(Environment.TickCount));
            else b.onClick.AddListener(ReturnToMainMenu);
        }

        // ── 고르기 · 포커스 ─────────────────────────────

        private void Pick(MapNode node)
        {
            if (progress == null || !progress.CanSelect(node.Id)) return;

            Debug.Log($"[RunMap] 칸 선택 — {node}");
            onPick?.Invoke(node);
        }

        private void Select(GameObject go)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
        }

        /// <summary>
        /// 선택이 바뀌면 테두리와 설명줄을 옮긴다. 마우스로 빈 곳을 눌러 선택이 풀리면 첫 칸으로 되돌린다 —
        /// 안 그러면 키보드가 잡을 칸이 없어 방향키가 먹통이 된다.
        /// </summary>
        private void Update()
        {
            if (EventSystem.current == null || selectables.Count == 0) return;

            GameObject selected = EventSystem.current.currentSelectedGameObject;

            if (selected == null || !nodeOf.ContainsKey(selected))
            {
                if (selected == null) Select(selectables[0].gameObject);
                return;
            }

            if (selected == lastSelected) return;
            lastSelected = selected;

            MapNode node = nodeOf[selected];
            focus.gameObject.SetActive(true);
            focus.anchoredPosition = RunMapLayout.PositionOf(progress.Map, node);
            SetInfo(RunMapViewRules.Describe(node));
        }

        // ── 색 ─────────────────────────────────────────

        private static Color KindColor(MapNodeKind kind)
        {
            switch (kind)
            {
                case MapNodeKind.Elite: return new Color(0.62f, 0.20f, 0.34f);
                case MapNodeKind.Rest:  return new Color(0.22f, 0.48f, 0.32f);
                case MapNodeKind.Shop:  return new Color(0.62f, 0.50f, 0.18f);
                case MapNodeKind.Event: return new Color(0.30f, 0.34f, 0.62f);
                case MapNodeKind.Boss:  return new Color(0.52f, 0.10f, 0.10f);
                default:                return new Color(0.50f, 0.28f, 0.24f);
            }
        }

        private static Color NodeColor(MapNodeKind kind, MapNodeState state)
        {
            Color c = KindColor(kind);

            switch (state)
            {
                case MapNodeState.Selectable:
                case MapNodeState.Pending:
                    return c;
                case MapNodeState.Current:
                case MapNodeState.Visited:
                    return Color.Lerp(c, new Color(0.35f, 0.35f, 0.35f), 0.55f);
                case MapNodeState.Ahead:
                    return new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 1f);
                default:
                    return new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, 0.6f);
            }
        }

        private static Color EdgeColor(MapEdgeState state)
        {
            switch (state)
            {
                case MapEdgeState.Taken: return new Color(1f, 0.86f, 0.45f, 1f);
                case MapEdgeState.Open:  return new Color(0.92f, 0.94f, 0.96f, 0.95f);
                case MapEdgeState.Ahead: return new Color(0.55f, 0.58f, 0.62f, 0.55f);
                default:                 return new Color(0.35f, 0.37f, 0.40f, 0.25f);
            }
        }
    }
}
