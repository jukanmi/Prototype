using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// <b>디버그 모드</b>(<see cref="DeckStartupMode.Pick"/>)의 시작 덱 편집기.
    /// 모든 카드 종류를 <b>일반 · 황금 두 벌로</b> 늘어놓고, 좌클릭으로 한 장 늘리고
    /// 우클릭으로 한 장 줄인다. 칸 아래에 장수가, 화면 맨 아래에 지금까지 담은 목록이 뜬다.
    ///
    /// 카드 한 종류를 여러 장 담을 수 있는 것이 요점이다 — 합성(같은 카드 3장)과
    /// 황금 데미지를 확인하려면 원하는 카드를 원하는 장수만큼 손에 쥘 수 있어야 하는데,
    /// 정상 흐름으로는 레벨업 선택지가 그 카드를 뽑아 줄 때까지 기다리는 수밖에 없다.
    ///
    /// <c>RebindUI</c> · <see cref="LevelUpSession"/>과 같은 모달 패턴이다 —
    /// 씬 배선 없이 코드로 짓고, 열려 있는 동안 게임 조작을 잠근다.
    /// </summary>
    public class DeckBuilderUI : MonoBehaviour
    {
        /// <summary>레벨업 화면(210)보다 위. 시작 시점에만 뜨므로 겹칠 일은 없지만 순서는 못 박아 둔다.</summary>
        private const int SortingOrder = 220;

        // ── 치수 ─────────────────────────────────────────

        private const float PanelWidth = 1480f;
        private const float PanelHeight = 900f;

        private const float CellWidth = 160f;
        private const float CellHeight = 190f;
        private const float CellGap = 10f;
        private const int Columns = 8;

        /// <summary>칸 테두리 두께. 황금은 이 폭만큼 금색으로 남는다.</summary>
        private const float CellFrame = 5f;

        private const float ViewportHeight = 520f;
        private const float ViewportTop = -132f;

        // ── 색 ───────────────────────────────────────────

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color PanelColor = new Color(0.11f, 0.12f, 0.15f, 0.99f);
        private static readonly Color ViewportColor = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color CellColor = new Color(0.22f, 0.26f, 0.30f);
        private static readonly Color PickedColor = new Color(0.26f, 0.40f, 0.30f);
        private static readonly Color FrameColor = new Color(0.08f, 0.09f, 0.11f);
        private static readonly Color GoldColor = new Color(1f, 0.80f, 0.28f);
        private static readonly Color TitleColor = new Color(1f, 0.86f, 0.45f);
        private static readonly Color SubColor = new Color(0.75f, 0.78f, 0.82f);
        private static readonly Color HintColor = new Color(1f, 0.82f, 0.4f);
        private static readonly Color StartColor = new Color(0.24f, 0.42f, 0.30f);
        private static readonly Color ClearColor = new Color(0.42f, 0.26f, 0.20f);

        // ── 씬에 하나 ────────────────────────────────────

        public static DeckBuilderUI Instance { get; private set; }

        public static bool IsOpen => Instance != null && Instance.isOpen;

        /// <summary>
        /// 시작 덱을 짜러 들어간다. 다 짜면 <paramref name="owner"/>의 덱을 다시 짓는다.
        /// 씬에 컴포넌트가 없으면 여기서 만든다 — 디버그 모드를 켜자고 씬을 다시 굽게 할 수는 없다.
        /// </summary>
        public static void RequestOpen(BulletTimeController owner, IReadOnlyList<SkillData> pool)
        {
            if (Instance == null)
                Instance = new GameObject(nameof(DeckBuilderUI), typeof(RectTransform))
                    .AddComponent<DeckBuilderUI>();

            if (Instance.isOpen) return;
            Instance.Open(owner, pool);
        }

        // ── 한 칸 ────────────────────────────────────────

        /// <summary>고를 수 있는 카드 한 종류. 같은 스킬이 일반 · 황금 두 칸으로 나온다.</summary>
        private class Entry
        {
            public SkillData data;
            public bool golden;
            public int count;

            public Image frame;
            public Image body;
            public Text countLabel;
        }

        /// <summary>
        /// 좌 · 우클릭을 갈라 받는다. <see cref="Button"/>은 좌클릭만 알기 때문에 쓸 수 없다.
        /// </summary>
        private class CellHandler : MonoBehaviour, IPointerClickHandler
        {
            public int Index;
            public DeckBuilderUI Owner;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (Owner == null) return;

                if (eventData.button == PointerEventData.InputButton.Left) Owner.Add(Index, +1);
                else if (eventData.button == PointerEventData.InputButton.Right) Owner.Add(Index, -1);
            }
        }

        // ── 상태 ─────────────────────────────────────────

        private readonly List<Entry> entries = new List<Entry>();
        private readonly StringBuilder summary = new StringBuilder();

        private BulletTimeController owner;

        private GameObject root;
        private RectTransform content;
        private Text summaryText;
        private Text totalText;

        private bool isOpen;
        private bool built;

        // ── 수명 ─────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }

            Instance = this;

            SimpleUI.EnsureEventSystem();
            SimpleUI.BuildCanvas(gameObject, SortingOrder);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            // 화면이 사라지는데 잠금이 남아 있으면 조작이 영영 안 돌아온다.
            if (isOpen) SetSuspended(false);

            Instance = null;
        }

        // ── 열고 닫기 ────────────────────────────────────

        private void Open(BulletTimeController controller, IReadOnlyList<SkillData> pool)
        {
            owner = controller;

            if (!built) { BuildUI(); built = true; }

            BuildEntries(pool);

            if (entries.Count == 0)
            {
                // 고를 것이 없는 빈 화면을 띄우고 유저를 세워 두지 않는다.
                // 덱은 건드리지 않고 넘긴다 — 파티 장착 카드로 평소처럼 시작한다.
                BattleLog.Warn(LogCategory.Deck,
                    "디버그 모드 — 고를 스킬이 하나도 없다. 파티 장착 카드와 SkillCatalog을 확인할 것. " +
                    "이번 판은 파티 덱 그대로 시작한다.", this);

                if (owner != null) owner.RebuildDeckAndHand();
                return;
            }

            isOpen = true;
            root.SetActive(true);
            SetSuspended(true);

            Refresh();
        }

        /// <summary>짠 덱을 런에 밀어 넣고 화면을 닫는다.</summary>
        private void Apply()
        {
            var picked = new List<ComboCard>();

            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                for (int n = 0; n < e.count; n++)
                    picked.Add(new ComboCard(e.data, CardGrantRules.WeightOf(e.data), e.golden));
            }

            RunProgression.Current.SetDeck(picked);

            BattleLog.Log(LogCategory.Deck,
                $"<b>디버그 시작 덱</b> {picked.Count}장 확정 — {Summarize(plain: true)}", this);

            isOpen = false;
            if (root != null) root.SetActive(false);
            SetSuspended(false);

            // 덱을 다시 짓고 손패를 채운다. 여기까지 와야 유저가 카드를 쥔다.
            if (owner != null) owner.RebuildDeckAndHand();
        }

        private static void SetSuspended(bool value)
        {
            PlayerInputController input = PlayerInputController.Instance;
            if (input != null) input.GameplaySuspended = value;
        }

        // ── 고르기 ───────────────────────────────────────

        private void Add(int index, int delta)
        {
            if (index < 0 || index >= entries.Count) return;

            Entry e = entries[index];
            e.count = Mathf.Max(0, e.count + delta);

            Refresh();
        }

        private void ClearAll()
        {
            for (int i = 0; i < entries.Count; i++) entries[i].count = 0;
            Refresh();
        }

        private void Refresh()
        {
            int total = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                total += e.count;

                e.countLabel.text = e.count > 0 ? $"×{e.count}" : "0";
                e.countLabel.color = e.count > 0 ? HintColor : SubColor;

                // 담은 칸은 면색으로 표시한다. 테두리는 등급(황금) 전용이라 겹쳐 쓸 수 없다.
                e.body.color = e.count > 0 ? PickedColor : CellColor;
            }

            summaryText.text = Summarize(plain: false);
            totalText.text = $"합계 <b>{total}</b>장";
        }

        /// <summary>담은 카드 목록 한 줄. 아무것도 없으면 그 사실을 적는다.</summary>
        private string Summarize(bool plain)
        {
            summary.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e.count == 0) continue;

                if (summary.Length > 0) summary.Append("   ");

                string name = e.data != null ? e.data.skillName : "?";

                if (e.golden && !plain) summary.Append($"<color=#FFCC47>{name}(황금) ×{e.count}</color>");
                else summary.Append($"{name}{(e.golden ? "(황금)" : "")} ×{e.count}");
            }

            return summary.Length > 0 ? summary.ToString() : "(아직 담은 카드가 없다 — 이대로 시작하면 0장 덱이다)";
        }

        // ── 짓기 ─────────────────────────────────────────

        private void BuildUI()
        {
            root = SimpleUI.CreateImage(transform, "DeckBuilderRoot", BackdropColor);
            SimpleUI.Stretch((RectTransform)root.transform);
            root.GetComponent<Image>().raycastTarget = true;

            GameObject panel = SimpleUI.CreateImage(root.transform, "Panel", PanelColor);
            SimpleUI.Place(panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(PanelWidth, PanelHeight));

            Transform p = panel.transform;

            Text title = SimpleUI.CreateText(p, "Title", "디버그 — 시작 덱 짜기", 38, TitleColor);
            SimpleUI.Place(title, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900f, 48f));

            Text hint = SimpleUI.CreateText(p, "Hint",
                "좌클릭 +1장 · 우클릭 −1장 · 황금 카드는 테두리가 금색이다", 20, HintColor);
            SimpleUI.Place(hint, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(1200f, 30f));

            BuildScrollView(p);

            summaryText = SimpleUI.CreateText(p, "Summary", "", 19, Color.white);
            summaryText.supportRichText = true;
            summaryText.alignment = TextAnchor.UpperLeft;
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            summaryText.verticalOverflow = VerticalWrapMode.Truncate;
            SimpleUI.Place(summaryText, new Vector2(0.5f, 0f), new Vector2(0f, 172f),
                new Vector2(PanelWidth - 80f, 116f));

            totalText = SimpleUI.CreateText(p, "Total", "", 22, SubColor);
            totalText.supportRichText = true;
            SimpleUI.Place(totalText, new Vector2(0f, 0f), new Vector2(220f, 46f), new Vector2(300f, 34f));

            Button start = SimpleUI.CreateButton(p, "Start", "이 덱으로 시작", StartColor,
                new Vector2(240f, 58f), 24);
            start.onClick.AddListener(Apply);
            SimpleUI.Place(start, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(240f, 58f));

            Button clear = SimpleUI.CreateButton(p, "Clear", "전부 지우기", ClearColor,
                new Vector2(180f, 58f), 22);
            clear.onClick.AddListener(ClearAll);
            SimpleUI.Place(clear, new Vector2(1f, 0f), new Vector2(-140f, 46f), new Vector2(180f, 58f));

            root.SetActive(false);
        }

        private void BuildScrollView(Transform parent)
        {
            GameObject viewport = SimpleUI.CreateImage(parent, "Viewport", ViewportColor);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = viewportRect.anchorMax = viewportRect.pivot = new Vector2(0.5f, 1f);
            viewportRect.sizeDelta = new Vector2(PanelWidth - 60f, ViewportHeight);
            viewportRect.anchoredPosition = new Vector2(0f, ViewportTop);

            viewport.AddComponent<Mask>().showMaskGraphic = true;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);

            content = (RectTransform)contentGo.transform;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(PanelWidth - 60f, 0f);
            content.anchoredPosition = Vector2.zero;

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
        }

        /// <summary>
        /// 스킬 하나당 칸 둘 — 일반과 황금. 황금을 따로 놓지 않으면 "황금 카드도 모두 보여준다"가
        /// 성립하지 않고, 황금 데미지를 확인하려면 레벨업 확률을 기다려야 한다.
        /// </summary>
        private void BuildEntries(IReadOnlyList<SkillData> pool)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            entries.Clear();

            if (pool == null) return;

            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] == null) continue;

                entries.Add(new Entry { data = pool[i], golden = false });
                entries.Add(new Entry { data = pool[i], golden = true });
            }

            for (int i = 0; i < entries.Count; i++) BuildCell(entries[i], i);

            int rows = Mathf.CeilToInt(entries.Count / (float)Columns);
            content.sizeDelta = new Vector2(content.sizeDelta.x, rows * (CellHeight + CellGap) + CellGap);
        }

        private void BuildCell(Entry entry, int index)
        {
            int row = index / Columns;
            int column = index % Columns;

            float span = Columns * CellWidth + (Columns - 1) * CellGap;
            float x = -span * 0.5f + CellWidth * 0.5f + column * (CellWidth + CellGap);
            float y = -CellGap - CellHeight * 0.5f - row * (CellHeight + CellGap);

            // 테두리 판이 곧 칸의 루트다. 등급은 여기 색으로만 나타낸다.
            GameObject frameGo = SimpleUI.CreateImage(content, $"Cell_{index}",
                entry.golden ? GoldColor : FrameColor);

            var rect = (RectTransform)frameGo.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(CellWidth, CellHeight);
            rect.anchoredPosition = new Vector2(x, y + CellHeight * 0.5f);

            entry.frame = frameGo.GetComponent<Image>();
            entry.frame.raycastTarget = true;   // 테두리를 눌러도 먹어야 한다

            GameObject bodyGo = SimpleUI.CreateImage(rect, "Body", CellColor);
            var body = (RectTransform)bodyGo.transform;
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(CellFrame, CellFrame);
            body.offsetMax = new Vector2(-CellFrame, -CellFrame);

            entry.body = bodyGo.GetComponent<Image>();
            entry.body.raycastTarget = true;

            // 아이콘 — 칸 위쪽. 없으면 이름만 크게 보인다.
            if (entry.data != null && entry.data.icon != null)
            {
                GameObject artGo = SimpleUI.CreateImage(body, "Art", Color.white);
                var art = (RectTransform)artGo.transform;
                art.anchorMin = new Vector2(0f, 0.40f);
                art.anchorMax = new Vector2(1f, 1f);
                art.offsetMin = new Vector2(8f, 6f);
                art.offsetMax = new Vector2(-8f, -8f);

                Image image = artGo.GetComponent<Image>();
                image.sprite = entry.data.icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }

            Text name = SimpleUI.CreateText(body, "Name",
                entry.data != null ? entry.data.skillName : "?", 16, Color.white);
            Anchor(name, new Vector2(0f, 0.24f), new Vector2(1f, 0.40f));
            name.horizontalOverflow = HorizontalWrapMode.Wrap;

            Text grade = SimpleUI.CreateText(body, "Grade", entry.golden ? "황금" : "일반", 14,
                entry.golden ? GoldColor : SubColor);
            Anchor(grade, new Vector2(0f, 0.12f), new Vector2(1f, 0.24f));

            // 장수는 칸 <b>아래</b>에 둔다. 아이콘 위에 겹치면 어느 카드인지가 안 읽힌다.
            entry.countLabel = SimpleUI.CreateText(body, "Count", "0", 20, SubColor);
            Anchor(entry.countLabel, new Vector2(0f, 0f), new Vector2(1f, 0.13f));

            var handler = frameGo.AddComponent<CellHandler>();
            handler.Index = index;
            handler.Owner = this;
        }

        private static void Anchor(Component target, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)target.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(4f, 0f);
            rect.offsetMax = new Vector2(-4f, 0f);
        }
    }
}
