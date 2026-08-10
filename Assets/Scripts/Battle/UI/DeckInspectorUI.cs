using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 덱 상황판. 화면 좌상단에 <b>사용됨 / 남은 덱</b> 두 숫자를 띄우고,
    /// 각 숫자를 누르면 해당 더미에 든 카드를 펼쳐 보여 주는 팝업이 뜬다.
    ///
    /// <see cref="ComboBoardUI"/>와 같은 방식으로 씬/프리팹 연결 없이 코드로만 만든다 —
    /// BulletTimeController가 붙은 GameObject에 이 컴포넌트만 추가하면 동작한다.
    /// 전투 로직은 건드리지 않고 Deck/Discard의 공개 API 위에서만 읽는다.
    ///
    /// 남은 덱은 <b>뽑히는 순서대로 보여 주지 않는다</b>. Deck.Cards는 맨 위부터가 곧 드로우 순서라
    /// 그대로 나열하면 다음에 뭐가 나올지가 전부 드러난다. 그래서 직업·유형·이름순으로
    /// 다시 정렬한 뒤 같은 스킬끼리 묶어 장수만 센다.
    /// </summary>
    [RequireComponent(typeof(BulletTimeController))]
    public class DeckInspectorUI : MonoBehaviour
    {
        // ── 치수 ─────────────────────────────────────────

        private const float BarButtonWidth = 132f;
        private const float BarButtonHeight = 40f;

        private const int GridColumns = 5;
        private const float EntryWidth = 104f;
        private const float EntryHeight = 148f;
        private const float EntrySpacing = 8f;
        private const float EntryStripHeight = 30f;
        private const float EntryArtInset = 3f;

        /// <summary>아트 영역이 시작하는 세로 비율. 그 아래는 이름 띠.</summary>
        private const float EntryArtBottom = EntryStripHeight / EntryHeight;

        /// <summary>4줄까지는 스크롤 없이 한눈에 들어온다. 그보다 많으면 스크롤.</summary>
        private const float ViewportHeight = EntryHeight * 4f + EntrySpacing * 3f;

        private const float ContentWidth = EntryWidth * GridColumns + EntrySpacing * (GridColumns - 1);

        // ── 색 ───────────────────────────────────────────

        private static readonly Color BarPanelColor = new Color(0.1f, 0.1f, 0.12f, 0.85f);
        private static readonly Color DiscardButtonColor = new Color(0.36f, 0.2f, 0.2f);
        private static readonly Color DeckButtonColor = new Color(0.18f, 0.28f, 0.34f);
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color PopupColor = new Color(0.12f, 0.13f, 0.16f, 0.97f);
        private static readonly Color EntryColor = new Color(0.22f, 0.26f, 0.3f);
        private static readonly Color EmptyEntryColor = new Color(0.4f, 0.18f, 0.18f);
        private static readonly Color HintColor = new Color(1f, 0.82f, 0.4f);
        private static readonly Color SubColor = new Color(0.72f, 0.76f, 0.8f);
        private static readonly Color CloseButtonColor = new Color(0.38f, 0.18f, 0.18f);

        /// <summary>어느 더미를 펼쳐 보고 있는지.</summary>
        private enum Pile
        {
            None,
            Discard,
            Deck,
        }

        private BulletTimeController _bulletTime;

        private Text _discardCountLabel;
        private Text _deckCountLabel;

        private GameObject _popupRoot;
        private Text _popupTitle;
        private Text _popupFooter;
        private RectTransform _popupContent;

        /// <summary>더미가 비었을 때 격자 대신 내보내는 안내. 격자와 서로 배타적으로 켠다.</summary>
        private GameObject _scrollRoot;
        private Text _emptyLabel;

        private Pile _openPile = Pile.None;

        /// <summary>같은 스킬끼리 묶은 한 칸.</summary>
        private struct Entry
        {
            public SkillData data;
            public int count;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        private void Awake()
        {
            _bulletTime = GetComponent<BulletTimeController>();
        }

        private void Start()
        {
            EnsureEventSystem();
            BuildUI();

            _bulletTime.Deck.OnChanged += HandlePileChanged;
            _bulletTime.Discard.OnChanged += HandlePileChanged;

            // 꼬리말이 손패 장수도 같이 보여 준다. 동료 사망으로 손패만 줄어드는 경우가 있어
            // 덱·버린 더미만 구독하면 그 숫자가 묵는다.
            _bulletTime.Hand.OnChanged += HandlePileChanged;

            HandlePileChanged();
        }

        private void OnDestroy()
        {
            if (_bulletTime == null) return;

            if (_bulletTime.Deck != null) _bulletTime.Deck.OnChanged -= HandlePileChanged;
            if (_bulletTime.Discard != null) _bulletTime.Discard.OnChanged -= HandlePileChanged;
            if (_bulletTime.Hand != null) _bulletTime.Hand.OnChanged -= HandlePileChanged;
        }

        private void Update()
        {
            if (_openPile == Pile.None) return;

            PlayerInputController input = PlayerInputController.Instance;
            if (input != null && input.CancelPressed)
                Close();
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ── UI 빌드 ──────────────────────────────────────

        private void BuildUI()
        {
            var canvasGo = new GameObject("DeckInspectorCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 손패 보드(0)·피격 HUD(2)보다 위. 팝업이 그 둘을 덮어야 한다.
            canvas.sortingOrder = 20;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            BuildCounterBar(canvasGo.transform);

            // 팝업을 나중에 붙여 카운터 바 위로 그린다(같은 캔버스에서는 형제 순서가 곧 그리는 순서).
            BuildPopup(canvasGo.transform);
        }

        /// <summary>항상 떠 있는 두 칸짜리 숫자 바. 왼쪽이 사용됨, 오른쪽이 남은 덱.</summary>
        private void BuildCounterBar(Transform parent)
        {
            var bar = CreatePanel(parent, "CounterBar", BarPanelColor);

            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.anchoredPosition = new Vector2(20f, -20f);

            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            var fitter = bar.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _discardCountLabel = BuildBarButton(bar.transform, "DiscardButton", DiscardButtonColor,
                () => Toggle(Pile.Discard));
            _deckCountLabel = BuildBarButton(bar.transform, "DeckButton", DeckButtonColor,
                () => Toggle(Pile.Deck));
        }

        private static Text BuildBarButton(Transform parent, string name, Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(BarButtonWidth, BarButtonHeight);

            var image = go.GetComponent<Image>();
            image.color = color;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            Text label = BuildStretchedText(go.transform, "Label", 15, Color.white, FontStyle.Bold);
            return label;
        }

        private void BuildPopup(Transform parent)
        {
            // 배경 막 — 화면 전체를 덮는다. 아무 데나 누르면 닫힌다.
            var root = new GameObject("Popup", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            _popupRoot = root;

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var backdrop = root.GetComponent<Image>();
            backdrop.color = BackdropColor;

            var backdropButton = root.GetComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.onClick.AddListener(Close);

            // 본체 — 화면 중앙. 배경 막의 자식이지만 자체 Image가 클릭을 먹으므로
            // 본체를 눌렀을 때는 닫히지 않는다.
            var panel = CreatePanel(root.transform, "Panel", PopupColor);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildPopupHeader(panel.transform);
            BuildPopupGrid(panel.transform);

            _emptyLabel = BuildSizedText(panel.transform, "Empty", ContentWidth, 120f, 16, SubColor, FontStyle.Normal);
            _emptyLabel.gameObject.SetActive(false);

            // 두 줄로 접힐 만큼 길다. 높이를 넉넉히 잡지 않으면 잘린다.
            _popupFooter = BuildSizedText(panel.transform, "Footer", ContentWidth, 44f, 13, SubColor, FontStyle.Normal);

            root.SetActive(false);
        }

        private void BuildPopupHeader(Transform parent)
        {
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(parent, false);
            header.GetComponent<RectTransform>().sizeDelta = new Vector2(ContentWidth, 32f);

            _popupTitle = BuildStretchedText(header.transform, "Title", 20, Color.white, FontStyle.Bold);
            _popupTitle.alignment = TextAnchor.MiddleLeft;

            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(header.transform, false);

            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 0.5f);
            closeRect.anchorMax = new Vector2(1f, 0.5f);
            closeRect.pivot = new Vector2(1f, 0.5f);
            closeRect.sizeDelta = new Vector2(32f, 28f);

            var closeImage = closeGo.GetComponent<Image>();
            closeImage.color = CloseButtonColor;

            var closeButton = closeGo.GetComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.onClick.AddListener(Close);

            BuildStretchedText(closeGo.transform, "X", 16, Color.white, FontStyle.Bold).text = "X";
        }

        private void BuildPopupGrid(Transform parent)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            _scrollRoot = scrollGo;
            scrollGo.GetComponent<RectTransform>().sizeDelta = new Vector2(ContentWidth, ViewportHeight);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);

            var viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);

            _popupContent = contentGo.GetComponent<RectTransform>();
            _popupContent.anchorMin = new Vector2(0f, 1f);
            _popupContent.anchorMax = new Vector2(1f, 1f);
            _popupContent.pivot = new Vector2(0.5f, 1f);
            _popupContent.anchoredPosition = Vector2.zero;

            var grid = contentGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(EntryWidth, EntryHeight);
            grid.spacing = new Vector2(EntrySpacing, EntrySpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = GridColumns;
            grid.childAlignment = TextAnchor.UpperLeft;

            var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = _popupContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
        }

        /// <summary>카드 한 칸. 아트가 있으면 아트, 없으면 이름·직업 표기로 대신한다.</summary>
        private static void BuildEntry(Transform parent, in Entry entry)
        {
            var go = new GameObject("Entry", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            SkillData data = entry.data;
            go.GetComponent<Image>().color = data != null ? EntryColor : EmptyEntryColor;

            var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(go.transform, false);

            var artRect = artGo.GetComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0f, EntryArtBottom);
            artRect.anchorMax = new Vector2(1f, 1f);
            artRect.offsetMin = new Vector2(EntryArtInset, EntryArtInset);
            artRect.offsetMax = new Vector2(-EntryArtInset, -EntryArtInset);

            var art = artGo.GetComponent<Image>();
            art.preserveAspect = true;
            art.raycastTarget = false;

            bool hasArt = data != null && data.icon != null;
            art.enabled = hasArt;
            if (hasArt) art.sprite = data.icon;

            // 아트가 없으면 이름·직업을 아트 자리에 대신 띄운다.
            if (!hasArt)
            {
                Text nameLabel = BuildAnchoredText(go.transform, "Name",
                    new Vector2(0f, 0.5f), new Vector2(1f, 0.78f), 13, Color.white, FontStyle.Bold);
                nameLabel.text = data != null ? data.skillName : "(빈 카드)";

                Text subLabel = BuildAnchoredText(go.transform, "Sub",
                    new Vector2(0f, 0.32f), new Vector2(1f, 0.5f), 11, SubColor, FontStyle.Normal);
                subLabel.text = data != null ? $"{data.role} · {data.attackType}" : "SkillData 미지정";
            }

            // 하단 띠 — 아트가 있는 카드는 여기서만 이름을 확인할 수 있다.
            Text strip = BuildAnchoredText(go.transform, "Strip",
                new Vector2(0f, 0f), new Vector2(1f, EntryArtBottom), 11, HintColor, FontStyle.Bold);
            string label = data != null ? data.skillName : "빈 카드";
            strip.text = entry.count > 1 ? $"{label}  ×{entry.count}" : label;
        }

        // ── 위젯 헬퍼 ────────────────────────────────────

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text BuildAnchoredText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(3f, 0f);
            rect.offsetMax = new Vector2(-3f, 0f);

            return Decorate(go.GetComponent<Text>(), fontSize, color, style);
        }

        private static Text BuildStretchedText(Transform parent, string name,
            int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return Decorate(go.GetComponent<Text>(), fontSize, color, style);
        }

        private static Text BuildSizedText(Transform parent, string name, float width, float height,
            int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);

            return Decorate(go.GetComponent<Text>(), fontSize, color, style);
        }

        private static Text Decorate(Text t, int fontSize, Color color, FontStyle style)
        {
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        // ── 조작 ─────────────────────────────────────────

        private void Toggle(Pile pile)
        {
            _openPile = _openPile == pile ? Pile.None : pile;
            RefreshPopup();
        }

        private void Close()
        {
            if (_openPile == Pile.None) return;

            _openPile = Pile.None;
            RefreshPopup();
        }

        // ── 갱신 ─────────────────────────────────────────

        /// <summary>더미가 바뀌면 숫자를 다시 쓰고, 열려 있는 팝업이 있으면 내용도 다시 채운다.</summary>
        private void HandlePileChanged()
        {
            _discardCountLabel.text = $"사용됨  {_bulletTime.Discard.Count}";
            _deckCountLabel.text = $"남은 덱  {_bulletTime.Deck.Count}";

            if (_openPile != Pile.None) RefreshPopup();
        }

        private void RefreshPopup()
        {
            bool open = _openPile != Pile.None;
            _popupRoot.SetActive(open);
            if (!open) return;

            bool isDeck = _openPile == Pile.Deck;
            IReadOnlyList<ComboCard> source = isDeck
                ? _bulletTime.Deck.Cards
                : _bulletTime.Discard.Cards;

            Group(source);

            _popupTitle.text = isDeck
                ? $"남은 덱 — {_bulletTime.Deck.Count}장 ({_entries.Count}종)"
                : $"사용된 카드 — {_bulletTime.Discard.Count}장 ({_entries.Count}종)";

            int deckCount = _bulletTime.Deck.Count;
            int handCount = _bulletTime.Hand.Count;
            int discardCount = _bulletTime.Discard.Count;

            string note = isDeck
                ? "뽑히는 순서는 가린다 — 직업·유형순으로 다시 정렬한 목록이다."
                : "사용한 순서와 무관하게 직업·유형순으로 묶었다.";

            _popupFooter.text =
                $"{note}   <color=#808080>덱 {deckCount} · 손패 {handCount} · 사용됨 {discardCount} = 총 {deckCount + handCount + discardCount}장</color>";

            // 빈 더미에 빈 격자만 띄우면 고장난 것처럼 보인다. 격자를 접고 안내로 바꾼다.
            bool empty = _entries.Count == 0;
            _scrollRoot.SetActive(!empty);
            _emptyLabel.gameObject.SetActive(empty);
            if (empty)
                _emptyLabel.text = isDeck
                    ? "남은 덱이 비었다.\n다음 드로우에 사용된 카드가 통째로 회수돼 다시 섞인다."
                    : "아직 사용한 카드가 없다.";

            // Destroy는 프레임 끝에 처리된다. 부모에서 먼저 떼지 않으면
            // 이번 프레임 레이아웃에 옛 칸과 새 칸이 함께 잡힌다.
            for (int i = _popupContent.childCount - 1; i >= 0; i--)
            {
                Transform child = _popupContent.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            for (int i = 0; i < _entries.Count; i++)
                BuildEntry(_popupContent, _entries[i]);
        }

        /// <summary>같은 SkillData끼리 묶어 장수를 세고, 직업 → 유형 → 이름순으로 정렬한다.</summary>
        private void Group(IReadOnlyList<ComboCard> cards)
        {
            _entries.Clear();
            if (cards == null) return;

            for (int i = 0; i < cards.Count; i++)
            {
                ComboCard card = cards[i];
                if (card == null) continue;

                SkillData data = card.Data;

                int found = -1;
                for (int j = 0; j < _entries.Count; j++)
                {
                    if (_entries[j].data == data) { found = j; break; }
                }

                if (found >= 0)
                {
                    Entry e = _entries[found];
                    e.count++;
                    _entries[found] = e;
                }
                else
                {
                    _entries.Add(new Entry { data = data, count = 1 });
                }
            }

            _entries.Sort(CompareEntry);
        }

        /// <summary>SkillData가 없는 카드는 항상 뒤로 보낸다.</summary>
        private static int CompareEntry(Entry a, Entry b)
        {
            if (a.data == null) return b.data == null ? 0 : 1;
            if (b.data == null) return -1;

            int byRole = a.data.role.CompareTo(b.data.role);
            if (byRole != 0) return byRole;

            int byType = a.data.attackType.CompareTo(b.data.attackType);
            if (byType != 0) return byType;

            return string.CompareOrdinal(a.data.skillName, b.data.skillName);
        }
    }
}
