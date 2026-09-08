using System.Collections.Generic;
using UnityEngine;
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
        // ── 칸 치수 ───────────────────────────────────────
        // 나머지 치수(바 · 팝업 · 격자 셀)는 프리팹이 쥔다. 여기 남은 건
        // 칸 <b>안쪽</b> 비율뿐이다 — 칸은 덱 장수만큼 코드로 찍으므로.

        private const float EntryHeight = 148f;
        private const float EntryStripHeight = 30f;
        private const float EntryArtInset = 3f;

        /// <summary>아트 영역이 시작하는 세로 비율. 그 아래는 이름 띠.</summary>
        private const float EntryArtBottom = EntryStripHeight / EntryHeight;

        // ── 색 ───────────────────────────────────────────
        // 칸에만 쓴다. 바 · 팝업 · 버튼 색은 프리팹으로 옮겼다.

        private static readonly Color EntryColor = new Color(0.22f, 0.26f, 0.3f);
        private static readonly Color EmptyEntryColor = new Color(0.4f, 0.18f, 0.18f);
        private static readonly Color HintColor = new Color(1f, 0.82f, 0.4f);
        private static readonly Color SubColor = new Color(0.72f, 0.76f, 0.8f);

        /// <summary>어느 더미를 펼쳐 보고 있는지.</summary>
        private enum Pile
        {
            None,
            Discard,
            Deck,
        }

        // ── 배선 ─────────────────────────────────────────
        // 화면은 프리팹이 쥔다. 자리·크기·색을 바꾸려면 코드가 아니라 CombatManager 프리팹을 연다.

        [Header("숫자 바")]
        [Tooltip("사용된 카드 장수. DiscardButton 의 Label.")]
        [SerializeField] private Text _discardCountLabel;

        [Tooltip("남은 덱 장수. DeckButton 의 Label.")]
        [SerializeField] private Text _deckCountLabel;

        [Tooltip("사용된 더미를 펼치는 버튼.")]
        [SerializeField] private Button _discardButton;

        [Tooltip("남은 덱을 펼치는 버튼.")]
        [SerializeField] private Button _deckButton;

        [Header("팝업")]
        [Tooltip("배경 막까지 포함한 팝업 전체. 프리팹에서는 꺼 둔다.")]
        [SerializeField] private GameObject _popupRoot;

        [Tooltip("배경 막 버튼. 바깥을 누르면 닫힌다.")]
        [SerializeField] private Button _backdropButton;

        [Tooltip("헤더의 X 버튼.")]
        [SerializeField] private Button _closeButton;

        [SerializeField] private Text _popupTitle;
        [SerializeField] private Text _popupFooter;

        [Tooltip("칸이 쌓이는 곳. GridLayoutGroup 이 붙은 Content.")]
        [SerializeField] private RectTransform _popupContent;

        [Tooltip("격자 스크롤 전체. 더미가 비면 끄고 안내로 바꾼다.")]
        [SerializeField] private GameObject _scrollRoot;

        [Tooltip("더미가 비었을 때 격자 대신 내보내는 안내. 격자와 서로 배타적으로 켠다.")]
        [SerializeField] private Text _emptyLabel;

        private BulletTimeController _bulletTime;

        private Pile _openPile = Pile.None;

        /// <summary>배선이 빈 채로 돌 때 경고를 한 번만 낸다.</summary>
        private bool _warned;

        private bool Wired =>
            _discardCountLabel != null && _deckCountLabel != null
            && _popupRoot != null && _popupTitle != null && _popupFooter != null
            && _popupContent != null && _scrollRoot != null && _emptyLabel != null;

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
            UiKit.EnsureEventSystem();

            if (!Wired)
            {
                Warn();
                return;
            }

            // 버튼은 인스펙터가 아니라 여기서 묶는다 — 대상 메서드를 private으로 두려면
            // UnityEvent 배선이 안 된다. 프리팹에는 참조만 꽂는다.
            if (_discardButton != null) _discardButton.onClick.AddListener(() => Toggle(Pile.Discard));
            if (_deckButton != null) _deckButton.onClick.AddListener(() => Toggle(Pile.Deck));
            if (_backdropButton != null) _backdropButton.onClick.AddListener(Close);
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);

            _popupRoot.SetActive(false);

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

        /// <summary>배선이 비면 조용히 아무것도 안 하는 대신 한 번 알린다.</summary>
        private void Warn()
        {
            if (_warned) return;

            _warned = true;
            Debug.LogWarning(
                "[DeckInspectorUI] 배선이 비어 있다 — 덱 상황판이 안 뜬다. " +
                "CombatManager 프리팹의 DeckInspectorCanvas 배선을 확인할 것.", this);
        }

        // ── 칸 짓기 ─────────────────────────────────────

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
        // 칸(Entry)만 코드로 짓는다 — 덱 장수만큼 찍히므로 프리팹으로 못 걷어낸다.

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
            if (!Wired) { Warn(); return; }

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
