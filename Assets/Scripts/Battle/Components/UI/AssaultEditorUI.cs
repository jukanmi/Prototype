using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 총공격 슬롯 편집창. 위는 총공격 4슬롯, 아래는 보유 스킬(스킬이름:개수 → 합성 레벨) 목록이다.
    ///
    /// <b>마우스</b> — 아래 스킬을 슬롯으로 드래그, 슬롯끼리 드래그(맞교환), 스킬 클릭 = 가장 앞 빈 슬롯에 추가(push_back),
    /// 슬롯 클릭 = 비우기.
    /// <b>키</b> — 이동키(<see cref="PlayerInputController.NavigateStep"/> · 화살표)로 포커스를 옮기고,
    /// 맨 윗줄에서 위로 가면 슬롯 줄로 올라간다. 확정(Enter)은 스킬이면 추가, 슬롯이면 비우기.
    /// 비운 슬롯 뒤의 스킬은 앞으로 당겨지지 않는다. ESC · 닫기 버튼으로 닫는다.
    ///
    /// 게임 조작은 열려 있는 동안 잠근다(<see cref="PlayerInputController.SetSuspended"/>).
    /// </summary>
    public class AssaultEditorUI : MonoBehaviour
    {
        // ── 치수 ─────────────────────────────────────────

        private const float PanelWidth = 1480f;
        private const float PanelHeight = 900f;

        private const float SlotWidth = 180f;
        private const float SlotHeight = 190f;
        private const float SlotGap = 24f;
        private const float SlotTop = -150f;

        private const float CellWidth = 160f;
        private const float CellHeight = 190f;
        private const float CellGap = 10f;
        private const int Columns = 8;
        private const float CellFrame = 5f;

        private const float ViewportHeight = 420f;
        private const float ViewportTop = -370f;
        private const float ScrollbarWidth = 26f;

        // ── 색 ───────────────────────────────────────────

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color PanelColor = new Color(0.11f, 0.12f, 0.15f, 0.99f);
        private static readonly Color ViewportColor = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color CellColor = new Color(0.22f, 0.26f, 0.30f);
        private static readonly Color EmptyColor = new Color(0.14f, 0.16f, 0.19f);
        private static readonly Color FrameColor = new Color(0.08f, 0.09f, 0.11f);
        private static readonly Color FocusColor = new Color(1f, 0.82f, 0.4f);
        private static readonly Color TitleColor = new Color(1f, 0.86f, 0.45f);
        private static readonly Color SubColor = new Color(0.75f, 0.78f, 0.82f);
        private static readonly Color HintColor = new Color(1f, 0.82f, 0.4f);
        private static readonly Color CloseColor = new Color(0.24f, 0.42f, 0.30f);

        // ── 씬에 하나 ────────────────────────────────────

        private static AssaultEditorUI Instance { get; set; }

        public static bool IsOpen => Instance != null && Instance.isOpen;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterModal() => GameplayModal.Register(IsOpenNow);

        private static bool IsOpenNow() => IsOpen;

        /// <summary>편집창을 연다. 씬에 컴포넌트가 없으면 만든다.</summary>
        public static void RequestOpen()
        {
            if (Instance == null)
                Instance = new GameObject(nameof(AssaultEditorUI), typeof(RectTransform))
                    .AddComponent<AssaultEditorUI>();

            if (Instance.isOpen) return;
            Instance.Open();
        }

        // ── 한 칸 ────────────────────────────────────────

        private enum Area { Grid, Slots }

        /// <summary>보유 스킬 한 종류.</summary>
        private class Entry
        {
            public string name;
            public SkillData data;
            public int count;
        }

        /// <summary>칸 하나의 그림. 보유 칸과 슬롯 칸이 같이 쓴다.</summary>
        private class CellView
        {
            public Image frame;
            public Image body;
            public Image art;
            public Text name;
            public Text sub;
        }

        /// <summary>클릭 · 드래그 · 드롭을 한 곳에서 받는다. 칸의 테두리에 붙는다.</summary>
        private class CellHandler : MonoBehaviour,
            IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
        {
            public AssaultEditorUI Owner;
            public Area Kind;
            public int Index;

            public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Owner.Click(Kind, Index); }
            public void OnBeginDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Owner.BeginDrag(Kind, Index); }
            public void OnDrag(PointerEventData e) => Owner.Drag(e);
            public void OnEndDrag(PointerEventData e) => Owner.EndDrag();
            public void OnDrop(PointerEventData e) => Owner.Drop(Kind, Index);
        }

        // ── 상태 ─────────────────────────────────────────

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<CellView> gridCells = new List<CellView>();
        private readonly CellView[] slotCells = new CellView[RunProgression.AssaultSlotCount];

        private GameObject root;
        private RectTransform rootRect;
        private RectTransform content;
        private ScrollRect scroll;
        private Text emptyText;
        private Text ghost;

        private bool isOpen;
        private bool built;

        private Area focusArea = Area.Grid;
        private int gridFocus;
        private int slotFocus;

        private bool dragging;
        private Area dragKind;
        private int dragIndex;

        private static RunProgression Run => RunProgression.Current;

        // ── 수명 ─────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }

            Instance = this;

            UiKit.EnsureEventSystem();
            UiKit.BuildCanvas(gameObject, UiLayer.DeckBuilder);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            if (isOpen) PlayerInputController.SetSuspended(false);

            Instance = null;
        }

        private void Open()
        {
            if (!built) { BuildUI(); built = true; }

            BuildEntries();

            focusArea = Area.Grid;
            gridFocus = 0;
            slotFocus = 0;
            dragging = false;
            ghost.gameObject.SetActive(false);

            isOpen = true;
            root.SetActive(true);
            PlayerInputController.SetSuspended(true);
            PlayerInputController.Instance?.ResyncUiNavigate();

            Refresh();
        }

        private void Close()
        {
            isOpen = false;
            dragging = false;
            if (root != null) root.SetActive(false);
            PlayerInputController.SetSuspended(false);
        }

        // ── 키 입력 ──────────────────────────────────────

        private void Update()
        {
            if (!isOpen) return;

            PlayerInputController input = PlayerInputController.Instance;
            if (input == null) return;

            if (input.CancelPressed) { Close(); return; }

            // 조작 이동키(WASD)가 우선, 없으면 UI 맵(화살표 · 패드).
            // 두 래치를 <b>매 프레임 둘 다</b> 읽는다. 앞이 잡혔다고 뒤를 건너뛰면 뒤 래치의 기준값이
            // 낡아서, 같은 키가 다음 프레임에 "새로 눌림"으로 또 잡혀 두 칸씩 움직인다.
            Vector2Int cardStep = input.NavigateStep;
            Vector2Int uiStep = input.UiNavigateStep;
            Vector2Int step = cardStep != Vector2Int.zero ? cardStep : uiStep;

            if (step != Vector2Int.zero) Move(step);
            if (input.UiSubmitPressed) Confirm();
        }

        private void Move(Vector2Int step)
        {
            // 목록이 비면 슬롯 줄에 머문다(아래로 내려갈 곳이 없다).
            if (focusArea == Area.Slots)
            {
                if (step.x != 0)
                    slotFocus = Mathf.Clamp(slotFocus + step.x, 0, RunProgression.AssaultSlotCount - 1);
                else if (step.y < 0 && entries.Count > 0)
                {
                    focusArea = Area.Grid;
                    gridFocus = Mathf.Clamp(slotFocus, 0, entries.Count - 1);
                }
            }
            else if (entries.Count > 0)
            {
                if (step.x != 0)
                    gridFocus = Mathf.Clamp(gridFocus + step.x, 0, entries.Count - 1);
                else if (step.y < 0)
                    gridFocus = Mathf.Min(gridFocus + Columns, entries.Count - 1);
                else if (step.y > 0)
                {
                    // 맨 윗줄에서 위로 → 슬롯 줄.
                    if (gridFocus < Columns)
                    {
                        focusArea = Area.Slots;
                        slotFocus = Mathf.Clamp(gridFocus, 0, RunProgression.AssaultSlotCount - 1);
                    }
                    else gridFocus -= Columns;
                }
            }
            else if (step.y > 0)
            {
                focusArea = Area.Slots;
            }

            EnsureVisible();
            Refresh();
        }

        private void Confirm() => Click(focusArea, focusArea == Area.Grid ? gridFocus : slotFocus);

        // ── 조작(마우스 · 키 공용) ───────────────────────

        private void Click(Area kind, int index)
        {
            if (kind == Area.Grid)
            {
                if (index < 0 || index >= entries.Count) return;

                gridFocus = index;
                focusArea = Area.Grid;
                Run.PushAssaultSlot(entries[index].name);   // 가득 차면 무시
            }
            else
            {
                slotFocus = index;
                focusArea = Area.Slots;
                Run.SetAssaultSlot(index, null);            // 뒤 칸은 당기지 않는다
            }

            Rebuild();
        }

        private void BeginDrag(Area kind, int index)
        {
            string name = NameAt(kind, index);
            if (name == null) return;

            dragging = true;
            dragKind = kind;
            dragIndex = index;

            ghost.text = name;
            ghost.gameObject.SetActive(true);
        }

        private void Drag(PointerEventData e)
        {
            if (!dragging) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, e.position, null, out Vector2 local))
                ((RectTransform)ghost.transform).anchoredPosition = local;
        }

        private void EndDrag()
        {
            dragging = false;
            if (ghost != null) ghost.gameObject.SetActive(false);
        }

        /// <summary>슬롯 위에 놓았다. 보유 칸 위나 허공에 놓으면 아무 일도 없다.</summary>
        private void Drop(Area kind, int index)
        {
            if (!dragging || kind != Area.Slots) return;

            if (dragKind == Area.Grid)
                Run.SetAssaultSlot(index, NameAt(Area.Grid, dragIndex));
            else
                Run.SwapAssaultSlots(dragIndex, index);

            dragging = false;
            ghost.gameObject.SetActive(false);   // 끌던 칸이 다시 지어지면 EndDrag가 안 올 수 있다
            slotFocus = index;
            focusArea = Area.Slots;
            Rebuild();
        }

        private string NameAt(Area kind, int index)
        {
            if (kind == Area.Slots)
                return index >= 0 && index < RunProgression.AssaultSlotCount ? Run.AssaultSlots[index] : null;

            return index >= 0 && index < entries.Count ? entries[index].name : null;
        }

        // ── 그리기 ───────────────────────────────────────

        private void Refresh()
        {
            for (int i = 0; i < gridCells.Count; i++)
            {
                Entry e = entries[i];
                Paint(gridCells[i], e.data, true,
                    SkillFusion.Describe(e.count),
                    focusArea == Area.Grid && gridFocus == i);
            }

            for (int i = 0; i < slotCells.Length; i++)
            {
                string name = Run.AssaultSlots[i];
                SkillData d = name != null ? Run.Resolve(name) : null;
                Paint(slotCells[i], d, d != null,
                    d != null ? SkillFusion.Describe(Run.CountOf(name)) : $"{i + 1}번 — 비어 있음",
                    focusArea == Area.Slots && slotFocus == i);
            }

            emptyText.gameObject.SetActive(entries.Count == 0);
        }

        private static void Paint(CellView c, SkillData data, bool filled, string sub, bool focused)
        {
            c.frame.color = focused ? FocusColor : FrameColor;
            c.body.color = filled ? CellColor : EmptyColor;

            c.name.text = data != null ? data.skillName : "";
            c.sub.text = sub;

            bool icon = data != null && data.icon != null;
            c.art.gameObject.SetActive(icon);
            if (icon) c.art.sprite = data.icon;
        }

        /// <summary>키 포커스가 뷰포트 밖이면 그 줄이 보이게 내용을 민다.</summary>
        private void EnsureVisible()
        {
            if (focusArea != Area.Grid || content == null) return;

            int row = gridFocus / Columns;
            float top = row * (CellHeight + CellGap);
            float bottom = top + CellHeight + CellGap * 2f;

            float y = content.anchoredPosition.y;
            if (top < y) y = top;
            else if (bottom > y + ViewportHeight) y = bottom - ViewportHeight;

            float max = Mathf.Max(0f, content.sizeDelta.y - ViewportHeight);
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, max));
        }

        // ── 짓기 ─────────────────────────────────────────

        /// <summary>슬롯이 바뀌었다. 슬롯에 든 스킬은 아래 목록에서 빠져야 하므로 목록을 다시 짓는다.</summary>
        private void Rebuild()
        {
            float scrollY = content.anchoredPosition.y;
            BuildEntries();

            float max = Mathf.Max(0f, content.sizeDelta.y - ViewportHeight);
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(scrollY, 0f, max));

            gridFocus = Mathf.Clamp(gridFocus, 0, Mathf.Max(0, entries.Count - 1));
            if (entries.Count == 0 && focusArea == Area.Grid) focusArea = Area.Slots;

            Refresh();
        }

        private void BuildEntries()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject old = content.GetChild(i).gameObject;
                old.SetActive(false);   // Destroy는 프레임 끝이라, 그 전에 겹쳐 보이지 않게 먼저 끈다
                Destroy(old);
            }

            gridCells.Clear();
            entries.Clear();

            foreach (KeyValuePair<string, int> kv in Run.SkillCounts)
                if (!Run.IsInAssaultSlot(kv.Key))   // 슬롯에 넣은 스킬은 목록에서 사라진다
                    entries.Add(new Entry { name = kv.Key, data = Run.Resolve(kv.Key), count = kv.Value });

            entries.Sort((a, b) =>
            {
                int ra = a.data != null ? (int)a.data.role : 99;
                int rb = b.data != null ? (int)b.data.role : 99;
                return ra != rb ? ra.CompareTo(rb) : string.CompareOrdinal(a.name, b.name);
            });

            float span = Columns * CellWidth + (Columns - 1) * CellGap;
            for (int i = 0; i < entries.Count; i++)
            {
                int row = i / Columns;
                int col = i % Columns;
                float x = -span * 0.5f + CellWidth * 0.5f + col * (CellWidth + CellGap);
                float y = -CellGap - row * (CellHeight + CellGap);

                CellView view = BuildCell(content, $"Cell_{i}", new Vector2(x, y), CellWidth, CellHeight, Area.Grid, i);
                gridCells.Add(view);
            }

            int rows = Mathf.CeilToInt(entries.Count / (float)Columns);
            content.sizeDelta = new Vector2(content.sizeDelta.x, rows * (CellHeight + CellGap) + CellGap);
            content.anchoredPosition = Vector2.zero;
        }

        private void BuildUI()
        {
            root = UiKit.CreateImage(transform, "AssaultEditorRoot", BackdropColor);
            UiKit.Stretch((RectTransform)root.transform);
            root.GetComponent<Image>().raycastTarget = true;
            rootRect = (RectTransform)root.transform;

            GameObject panel = UiKit.CreateImage(root.transform, "Panel", PanelColor);
            UiKit.Place(panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(PanelWidth, PanelHeight));
            Transform p = panel.transform;

            Text title = UiKit.CreateText(p, "Title", "총공격 슬롯 편집", 38, TitleColor);
            UiKit.Place(title, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900f, 48f));

            Text hint = UiKit.CreateText(p, "Hint",
                "드래그 · 클릭 = 추가 / 슬롯 클릭 = 비우기   |   이동키로 선택 · 맨 윗줄에서 ↑ = 슬롯 줄 · Enter = 추가/비우기 · ESC = 닫기",
                19, HintColor);
            UiKit.Place(hint, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(1400f, 30f));

            Text slotTitle = UiKit.CreateText(p, "SlotTitle", "총공격 순서 (왼쪽부터 자동 발동)", 20, SubColor);
            UiKit.Place(slotTitle, new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(900f, 28f));

            // 슬롯 4칸 — 처음부터 4개를 가운데 정렬로 깐다.
            float spanSlots = RunProgression.AssaultSlotCount * SlotWidth + (RunProgression.AssaultSlotCount - 1) * SlotGap;
            for (int i = 0; i < slotCells.Length; i++)
            {
                float x = -spanSlots * 0.5f + SlotWidth * 0.5f + i * (SlotWidth + SlotGap);
                slotCells[i] = BuildCell(p, $"Slot_{i}", new Vector2(x, SlotTop), SlotWidth, SlotHeight, Area.Slots, i);
            }

            BuildScrollView(p);

            emptyText = UiKit.CreateText(p, "Empty", "보유한 스킬이 없다", 24, SubColor);
            UiKit.Place(emptyText, new Vector2(0.5f, 1f), new Vector2(0f, ViewportTop - 120f), new Vector2(700f, 40f));

            Button close = UiKit.CreateButton(p, "Close", "닫기", CloseColor, new Vector2(240f, 58f), 24);
            close.onClick.AddListener(Close);
            UiKit.Place(close, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(240f, 58f));

            // 드래그 중 포인터를 따라다니는 이름표. 이벤트를 막지 않는다.
            ghost = UiKit.CreateText(root.transform, "Ghost", "", 26, Color.white);
            ghost.raycastTarget = false;
            var gr = (RectTransform)ghost.transform;
            gr.anchorMin = gr.anchorMax = gr.pivot = new Vector2(0.5f, 0.5f);
            gr.sizeDelta = new Vector2(300f, 40f);
            ghost.gameObject.SetActive(false);

            root.SetActive(false);
        }

        private void BuildScrollView(Transform parent)
        {
            float viewWidth = PanelWidth - 80f - ScrollbarWidth;

            GameObject viewport = UiKit.CreateImage(parent, "Viewport", ViewportColor);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = viewportRect.anchorMax = viewportRect.pivot = new Vector2(0.5f, 1f);
            viewportRect.sizeDelta = new Vector2(viewWidth, ViewportHeight);
            viewportRect.anchoredPosition = new Vector2(-ScrollbarWidth * 0.5f, ViewportTop);
            viewport.AddComponent<Mask>().showMaskGraphic = true;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);
            content = (RectTransform)contentGo.transform;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(viewWidth, 0f);
            content.anchoredPosition = Vector2.zero;

            // 오른쪽 슬라이더 — 스킬이 많아도 끌어서 훑을 수 있다.
            GameObject barGo = UiKit.CreateImage(parent, "Scrollbar", new Color(0f, 0f, 0f, 0.4f));
            var barRect = (RectTransform)barGo.transform;
            barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0.5f, 1f);
            barRect.sizeDelta = new Vector2(ScrollbarWidth, ViewportHeight);
            barRect.anchoredPosition = new Vector2(viewWidth * 0.5f, ViewportTop);

            GameObject handleGo = UiKit.CreateImage(barRect, "Handle", new Color(0.75f, 0.78f, 0.82f, 0.9f));
            var handleRect = (RectTransform)handleGo.transform;
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;

            var bar = barGo.AddComponent<Scrollbar>();
            bar.handleRect = handleRect;
            bar.targetGraphic = handleGo.GetComponent<Image>();
            bar.direction = Scrollbar.Direction.BottomToTop;

            scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        /// <summary>
        /// 칸 하나. 테두리 판이 루트이고 <see cref="CellHandler"/>가 붙는다.
        /// <paramref name="pos"/>는 부모 위쪽 중앙 기준 칸 <b>위쪽 중앙</b>의 좌표다(그리드) —
        /// 슬롯은 같은 기준으로 한가운데 y를 쓰므로 호출자가 맞춰 준다.
        /// </summary>
        private CellView BuildCell(Transform parent, string objName, Vector2 pos, float w, float h, Area kind, int index)
        {
            GameObject frameGo = UiKit.CreateImage(parent, objName, FrameColor);
            var rect = (RectTransform)frameGo.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = pos;

            var view = new CellView { frame = frameGo.GetComponent<Image>() };
            view.frame.raycastTarget = true;

            GameObject bodyGo = UiKit.CreateImage(rect, "Body", CellColor);
            var body = (RectTransform)bodyGo.transform;
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(CellFrame, CellFrame);
            body.offsetMax = new Vector2(-CellFrame, -CellFrame);
            view.body = bodyGo.GetComponent<Image>();
            view.body.raycastTarget = true;

            GameObject artGo = UiKit.CreateImage(body, "Art", Color.white);
            var art = (RectTransform)artGo.transform;
            art.anchorMin = new Vector2(0f, 0.40f);
            art.anchorMax = new Vector2(1f, 1f);
            art.offsetMin = new Vector2(8f, 6f);
            art.offsetMax = new Vector2(-8f, -8f);
            view.art = artGo.GetComponent<Image>();
            view.art.preserveAspect = true;
            view.art.raycastTarget = false;
            artGo.SetActive(false);

            view.name = UiKit.CreateText(body, "Name", "", 16, Color.white);
            UiKit.Band(view.name, new Vector2(0f, 0.24f), new Vector2(1f, 0.40f), 4f);
            view.name.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.name.raycastTarget = false;

            view.sub = UiKit.CreateText(body, "Sub", "", 18, SubColor);
            UiKit.Band(view.sub, new Vector2(0f, 0f), new Vector2(1f, 0.22f), 4f);
            view.sub.raycastTarget = false;

            var handler = frameGo.AddComponent<CellHandler>();
            handler.Owner = this;
            handler.Kind = kind;
            handler.Index = index;

            return view;
        }
    }
}
