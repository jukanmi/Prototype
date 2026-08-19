using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 손패 4장을 <b>항상</b> 보여 주는 런타임 생성 UI.
    /// 씬/프리팹 연결 없이 코드로만 만든다 — BulletTimeController가 붙은 GameObject에 이 컴포넌트만 추가하면 동작한다.
    ///
    /// 손패가 곧 실행 순서다(왼쪽부터). 실시간에는 표시만 하고,
    /// 불릿타임 중에만 카드끼리 드래그해 순서를 바꾸거나 클릭해서 조준할 수 있다.
    ///
    /// 기존 전투 로직(BulletTimeController/ComboExecutor/TargetSelector)은 건드리지 않고
    /// 그 공개 API 위에서만 동작한다.
    /// </summary>
    [RequireComponent(typeof(BulletTimeController))]
    public class ComboBoardUI : MonoBehaviour
    {
        [Tooltip("비워두면 같은 GameObject 또는 자식에서 자동으로 찾는다.")]
        [SerializeField] private TargetSelector targetSelector;

        // 카드 아트가 세로형(약 3:4)이라 위젯도 세로로 잡는다.
        // 아래 StatusBarHeight만큼은 상태 띠로 남기고 그 위를 아트가 채운다.
        private const float CardWidth = 140f;
        private const float CardHeight = 208f;
        private const float StatusBarHeight = 22f;
        private const float ArtInset = 3f;
        private const float CardSpacing = 10f;

        /// <summary>조준 시작점을 카드 위쪽 모서리에서 얼마나 더 띄울지. 카드 높이 대비 비율.</summary>
        private const float AimStartGap = 0.5f;

        /// <summary>아트 영역이 시작하는 세로 비율. 그 아래는 상태 띠.</summary>
        private const float ArtBottom = StatusBarHeight / CardHeight;
        private const float TitleHeight = 26f;
        private const float HintHeight = 22f;

        private static readonly Color PanelColor = new Color(0.1f, 0.1f, 0.12f, 0.85f);
        private static readonly Color CardColor = new Color(0.22f, 0.26f, 0.3f);
        private static readonly Color NextCardColor = new Color(0.28f, 0.38f, 0.34f);
        private static readonly Color ChainedCardColor = new Color(0.24f, 0.42f, 0.26f);
        private static readonly Color AimingCardColor = new Color(0.45f, 0.35f, 0.15f);
        private static readonly Color CursorCardColor = new Color(0.30f, 0.44f, 0.58f);
        private static readonly Color GrabbedCardColor = new Color(0.52f, 0.44f, 0.20f);
        private static readonly Color EmptyCardColor = new Color(0.4f, 0.18f, 0.18f);
        private static readonly Color HintColor = new Color(1f, 0.82f, 0.4f);
        private static readonly Color SubColor = new Color(0.72f, 0.76f, 0.8f);
        private static readonly Color DownColor = new Color(1f, 0.5f, 0.5f);

        // ── 카드 확대 · 상세 패널 ─────────────────────────
        /// <summary>커서 · 집힘 · 조준 카드가 커지는 배율.</summary>
        private const float FocusScale = 1.18f;
        /// <summary>확대가 따라붙는 속도(1/s). 시간이 멈춰 있으므로 unscaled로 돈다.</summary>
        private const float ScaleLerpSpeed = 12f;
        private const float DetailWidth = 420f;
        /// <summary>줄 높이(28+18+46+18+18) + 간격 16 + 패딩 20.</summary>
        private const float DetailHeight = 164f;
        /// <summary>손패 판과 상세 패널 사이 여백.</summary>
        private const float DetailGap = 12f;

        private BulletTimeController _bulletTime;
        private GameObject _canvasRoot;
        private Text _titleText;
        private Text _hintText;
        private CardWidgets[] _cards;

        /// <summary>커서가 짚은 카드의 상세. 짚은 카드가 없으면 꺼진다.</summary>
        private GameObject _detailPanel;
        private RectTransform _detailRect;
        private Text _detailName;
        private Text[] _detailRows;

        /// <summary>손패 판. 카드를 이 밖으로 꺼냈는지 판정하는 기준이다.</summary>
        private RectTransform _handPanelRect;

        // 조준이 필요한 카드를 손패 밖으로 꺼낸 뒤, 월드 클릭으로 확정하기를 기다리는 동안의 대기 상태.
        private int _aimingIndex = -1;

        /// <summary>키보드 커서가 짚고 있는 카드. 빈 손패면 -1.</summary>
        private int _cursorIndex = -1;

        /// <summary>키보드로 집은 카드. -1이면 안 집은 상태.</summary>
        private int _grabbedIndex = -1;

        /// <summary>RectTransform.GetWorldCorners용 재사용 버퍼.</summary>
        private readonly Vector3[] _corners = new Vector3[4];

        private class CardWidgets
        {
            public GameObject root;
            /// <summary>확대에 쓴다. 매 프레임 GetComponent를 피하려고 들고 있는다.</summary>
            public RectTransform rect;
            /// <summary>목표 배율. RefreshUI가 정하고 Update가 여기로 따라간다.</summary>
            public float targetScale = 1f;
            /// <summary>
            /// 확대한 카드를 이웃 <b>위</b>로 올리는 데 쓴다.
            /// SetAsLastSibling은 못 쓴다 — HorizontalLayoutGroup이 형제 순서로 자리를 잡으므로
            /// 카드가 오른쪽 끝으로 튄다.
            /// </summary>
            public Canvas sorting;
            /// <summary>카드 전체를 덮는 판. 아트 바깥 테두리 · 하단 상태 띠가 이 색으로 보인다.</summary>
            public Image background;
            /// <summary>SkillData.icon. 없으면 꺼지고 이름 · 직업 텍스트가 대신 나온다.</summary>
            public Image art;
            public Text nameLabel;
            public Text subLabel;
            public Text statusLabel;
            public CanvasGroup group;
        }

        // 카드 드래그. 놓든 실패하든 항상 원래 자리로 스냅백한다 —
        // 실제 순서 반영은 Hand.OnChanged -> RefreshUI가 담당하므로 드래그 자체는 상태를 바꾸지 않는다.
        //
        // 드래그 하나로 두 가지를 가른다:
        //   · 손패 안에서 놓으면  → 그 자리 카드와 순서 교환 (OnDrop)
        //   · 손패 밖으로 꺼내면  → 조준 시작 (OnEndDrag)
        //
        // <b>끌고 다니는 동안 blocksRaycasts를 끄는 게 핵심이다.</b> 카드는 커서를 따라다니므로
        // 켜 둔 채로는 자기 자신이 커서 아래에 남아 레이캐스트를 먼저 먹는다. 그러면 uGUI가
        //     pointerPress == 놓은 자리의 핸들러  →  드롭이 아니라 클릭
        // 으로 판정해 OnDrop이 아예 오지 않는다. 게다가 어느 쪽이 먹느냐는 형제 순서가 정하므로
        // 뒤 형제(오른쪽 카드)를 왼쪽으로 끌 때만 교환이 조용히 실패했다.
        private class CardHandler : MonoBehaviour,
            IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
        {
            public int Index;
            public ComboBoardUI Owner;

            private RectTransform _rect;
            private CanvasGroup _group;
            private Vector2 _originalPos;
            private bool _dragging;

            private void Awake()
            {
                _rect = GetComponent<RectTransform>();
                _group = GetComponent<CanvasGroup>();
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (!Owner.CanEditNow) return;

                _dragging = true;
                _originalPos = _rect.anchoredPosition;

                // 커서 아래를 비워 준다 — 밑에 깔린 카드가 드롭 대상이 되도록.
                if (_group != null) _group.blocksRaycasts = false;
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (!_dragging) return;
                _rect.position += (Vector3)eventData.delta;
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (!_dragging) return;

                _dragging = false;
                if (_group != null) _group.blocksRaycasts = true;
                _rect.anchoredPosition = _originalPos;

                // 손패 밖에서 놓았으면 조준으로 넘어간다.
                // 안에서 놓았으면 이미 OnDrop이 순서 교환을 처리했다.
                if (!Owner.IsInsideHandPanel(eventData))
                    Owner.HandleCardPulledOut(Index);
            }

            public void OnDrop(PointerEventData eventData)
            {
                if (eventData.pointerDrag == null) return;

                var from = eventData.pointerDrag.GetComponent<CardHandler>();
                if (from != null && from.Index != Index)
                    Owner.HandleSwap(from.Index, Index);
            }
        }

        /// <summary>
        /// 마우스 드래그를 받아도 되는 때. 조준 중이거나 키보드로 카드를 집고 있으면 막는다 —
        /// 두 입력 경로가 같은 손패를 동시에 밀면 어느 쪽이 이겼는지 화면으로 알 수 없다.
        /// </summary>
        private bool CanEditNow => _bulletTime.AllowsCardEdit && _aimingIndex < 0 && _grabbedIndex < 0;

        private void Awake()
        {
            _bulletTime = GetComponent<BulletTimeController>();

            if (targetSelector == null) targetSelector = GetComponent<TargetSelector>();
            if (targetSelector == null) targetSelector = GetComponentInChildren<TargetSelector>();
        }

        private void Start()
        {
            EnsureEventSystem();

            _cards = new CardWidgets[Hand.Size];
            BuildUI();

            _bulletTime.Hand.OnChanged += RefreshUI;
            _bulletTime.OnEnter += HandleEnter;
            _bulletTime.OnExit += HandleExit;

            RefreshUI();
        }

        private void OnDestroy()
        {
            if (_bulletTime == null) return;

            if (_bulletTime.Hand != null) _bulletTime.Hand.OnChanged -= RefreshUI;
            _bulletTime.OnEnter -= HandleEnter;
            _bulletTime.OnExit -= HandleExit;
        }

        /// <summary>
        /// 키보드 카드 조작. 세 상태가 <b>배타적</b>으로 하나만 돈다 —
        /// 조준 확정(J)과 카드 놓기(J)가 기본값이 같아서, 한 프레임에 두 갈래가 돌면
        /// 한 번 누른 J가 조준을 확정하고 그 카드를 놓는 것까지 해 버린다.
        /// </summary>
        private void Update()
        {
            // 확대는 입력과 무관하게 매 프레임 따라간다 — 불릿타임이 풀리는 순간에도 부드럽게 돌아와야 한다.
            AnimateCardScale();

            // 불릿타임이 풀렸으면 조준도 집기도 같이 접는다.
            if (!_bulletTime.AllowsCardEdit)
            {
                if (_aimingIndex >= 0 || _grabbedIndex >= 0)
                {
                    CancelAiming();
                    ReleaseGrab();
                    RefreshUI();
                }
                return;
            }

            PlayerInputController input = PlayerInputController.Instance;
            if (input == null) return;

            if (_aimingIndex >= 0) HandleAimingInput(input);
            else if (_grabbedIndex >= 0) HandleGrabbedInput(input);
            else HandleBrowseInput(input);
        }

        // ── 상태 1. 커서 이동 ────────────────────────────

        private void HandleBrowseInput(PlayerInputController input)
        {
            Vector2Int step = input.NavigateStep;

            // 위를 먼저 본다. 대각선으로 눌리면 두 축이 함께 선다.
            if (step.y > 0)
            {
                Grab(_cursorIndex);
                return;
            }

            if (step.x == 0) return;

            int next = NextOccupied(_cursorIndex, step.x);
            if (next == _cursorIndex) return;

            _cursorIndex = next;
            RefreshUI();
        }

        // ── 상태 2. 카드를 집은 상태 ─────────────────────

        private void HandleGrabbedInput(PlayerInputController input)
        {
            Vector2Int step = input.NavigateStep;

            // 위 · 아래를 먼저 본다. 대각선으로 눌리면 두 축이 함께 서는데,
            // 상태를 옮기는 쪽이 순서 변경보다 우선이다.
            if (step.y > 0)
            {
                BeginAiming(_grabbedIndex);
                return;
            }

            if (step.y < 0)
            {
                ReleaseGrab();
                RefreshUI();
                return;
            }

            if (step.x != 0) MoveGrabbed(step.x);
        }

        /// <summary>집은 카드를 한 칸 민다. 손패 밖이나 빈자리로는 못 민다.</summary>
        private void MoveGrabbed(int dx)
        {
            int to = _grabbedIndex + dx;
            if (to < 0 || to >= Hand.Size) return;
            if (_bulletTime.Hand.Get(to).IsEmpty) return;

            // HandleSwap이 아니라 직접 부른다 — CanEditNow가 집은 상태를 막고 있다.
            if (!_bulletTime.SwapHand(_grabbedIndex, to)) return;

            _grabbedIndex = to;
            _cursorIndex = to;
            RefreshUI();
        }

        // ── 상태 3. 시전 위치 지정 ───────────────────────

        private void HandleAimingInput(PlayerInputController input)
        {
            if (input.AimCancelPressed)
            {
                CancelAiming();
                RefreshUI();
                return;
            }

            // "UI 위 클릭은 확정으로 안 친다"는 판정은 PlayerInputController가 한다 —
            // 여기서 커서 위치만 보면 마우스를 손패 위에 올려 둔 채 키보드로 확정하는 것까지 막힌다.
            if (!input.AimConfirmPressed) return;

            TargetInfo info = targetSelector != null ? targetSelector.Confirm() : TargetInfo.None;

            // 인덱스를 먼저 비운다 — SetHandTarget이 OnChanged로 RefreshUI를 부르므로
            // 그 시점에 이미 조준이 끝난 상태로 보여야 한다.
            int idx = _aimingIndex;
            _aimingIndex = -1;

            // 위치까지 찍었으면 그 카드는 볼일이 끝났다. 집은 채로 돌아가면
            // 방금 확정한 카드를 다시 놓아 줘야 다음 카드로 넘어갈 수 있다.
            // 반면 취소(K)는 집은 상태를 남긴다 — 조준만 무르고 다시 겨냥할 수 있어야 한다.
            _cursorIndex = idx;
            ReleaseGrab();

            _bulletTime.SetHandTarget(idx, in info);
            RefreshUI();
        }

        // ── 집기 · 놓기 ──────────────────────────────────

        private void Grab(int index)
        {
            if (index < 0 || index >= Hand.Size) return;
            if (_bulletTime.Hand.Get(index).IsEmpty) return;

            _grabbedIndex = index;
            RefreshUI();
        }

        /// <summary>
        /// 집기를 푼다. 바꾼 순서는 그대로 확정된다.
        /// 되돌리기는 없다 — 잘못 옮겼으면 다시 집어서 되밀면 된다.
        /// </summary>
        private void ReleaseGrab()
        {
            _grabbedIndex = -1;
        }

        /// <summary>커서에서 dx 방향으로 가장 가까운 카드 자리. 없으면 제자리를 돌려준다.</summary>
        private int NextOccupied(int from, int dx)
        {
            for (int i = from + dx; i >= 0 && i < Hand.Size; i += dx)
            {
                if (!_bulletTime.Hand.Get(i).IsEmpty) return i;
            }
            return from;
        }

        /// <summary>손패가 바뀌어 커서가 빈자리를 짚고 있으면 가장 왼쪽 카드로 되돌린다.</summary>
        private void EnsureCursorValid()
        {
            if (_cursorIndex >= 0 && _cursorIndex < Hand.Size &&
                !_bulletTime.Hand.Get(_cursorIndex).IsEmpty)
                return;

            _cursorIndex = -1;
            for (int i = 0; i < Hand.Size; i++)
            {
                if (_bulletTime.Hand.Get(i).IsEmpty) continue;
                _cursorIndex = i;
                return;
            }
        }

        private void HandleEnter()
        {
            // 불릿타임에 들어올 때마다 커서를 맨 왼쪽 카드에서 시작한다.
            _cursorIndex = -1;
            EnsureCursorValid();
            RefreshUI();
        }

        private void HandleExit()
        {
            CancelAiming();
            ReleaseGrab();
            RefreshUI();
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
            var canvasGo = new GameObject("ComboBoardCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvasRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            var panel = CreatePanel(canvasGo.transform, "HandPanel", PanelColor);
            var panelRect = panel.GetComponent<RectTransform>();
            _handPanelRect = panelRect;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0, 20);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 8;
            layout.padding = new RectOffset(16, 16, 12, 16);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            float rowWidth = Hand.Size * CardWidth + (Hand.Size - 1) * CardSpacing;

            _titleText = BuildText(panel.transform, "Title", rowWidth, TitleHeight, 18, Color.white, FontStyle.Bold);
            _hintText = BuildText(panel.transform, "Hint", rowWidth, HintHeight, 14, HintColor, FontStyle.Normal);

            BuildCardRow(panel.transform);
            BuildDetailPanel(canvasGo.transform);
        }

        /// <summary>
        /// 짚은 카드의 상세. 카드 아트(140×208)에 그려진 글자는 그 크기에서 읽히지 않는다 —
        /// 이름 · 설명 · 코스트는 <see cref="SkillData"/>에만 있고 화면에 한 번도 안 나왔다.
        /// 손패 판 <b>위</b>에 형제로 띄운다.
        /// </summary>
        private void BuildDetailPanel(Transform canvasRoot)
        {
            _detailPanel = CreatePanel(canvasRoot, "DetailPanel", PanelColor);
            _detailRect = _detailPanel.GetComponent<RectTransform>();
            _detailRect.anchorMin = new Vector2(0.5f, 0f);
            _detailRect.anchorMax = new Vector2(0.5f, 0f);
            _detailRect.pivot = new Vector2(0.5f, 0f);
            _detailRect.sizeDelta = new Vector2(DetailWidth, DetailHeight);

            // 이 판은 읽으라고 띄운 것이지 누르라고 띄운 게 아니다.
            // 레이캐스트를 남겨 두면 PlayerInputController.Pressed가 조준 클릭을
            // "UI 위 클릭"으로 판정해 삼킨다 — 판이 조준 영역을 덮고 있어 확정이 아예 안 먹혔다.
            _detailPanel.GetComponent<Image>().raycastTarget = false;

            var block = _detailPanel.AddComponent<CanvasGroup>();
            block.blocksRaycasts = false;
            block.interactable = false;

            var layout = _detailPanel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 4;
            layout.padding = new RectOffset(14, 14, 10, 10);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            _detailName = BuildDetailText(_detailPanel.transform, "Name", 28f, 22, Color.white, FontStyle.Bold);
            _detailRows = new[]
            {
                BuildDetailText(_detailPanel.transform, "Kind", 18f, 13, SubColor, FontStyle.Normal),
                BuildDetailText(_detailPanel.transform, "Desc", 46f, 15, Color.white, FontStyle.Normal),
                BuildDetailText(_detailPanel.transform, "Chain", 18f, 13, HintColor, FontStyle.Bold),
                BuildDetailText(_detailPanel.transform, "Cost", 18f, 13, SubColor, FontStyle.Normal),
            };

            // 설명만 여러 줄로 흐른다. 나머지는 한 줄이다.
            _detailRows[1].horizontalOverflow = HorizontalWrapMode.Wrap;

            _detailPanel.SetActive(false);
        }

        /// <summary>
        /// 상세 패널의 한 줄. 배경이 어두워도 밝아도 읽히도록 <see cref="Outline"/>을 붙인다 —
        /// 컷인(<see cref="SkillCutinUI"/>)과 같은 값이다. "글자가 안 보인다"의 실제 해결책이 이것이다.
        /// </summary>
        private static Text BuildDetailText(Transform parent, string name, float height,
            int fontSize, Color color, FontStyle style)
        {
            Text t = BuildText(parent, name, DetailWidth, height, fontSize, color, style);
            t.alignment = TextAnchor.UpperLeft;

            // 세로 배치가 높이를 정한다. 줄마다 자리를 못 박아 두면 설명 길이에 따라 판이 요동친다.
            var element = t.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleHeight = 0f;

            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            return t;
        }

        private void BuildCardRow(Transform parent)
        {
            var rowGo = new GameObject("HandRow", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);

            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = CardSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            for (int i = 0; i < Hand.Size; i++)
                _cards[i] = BuildCard(rowGo.transform, i);
        }

        private CardWidgets BuildCard(Transform parent, int index)
        {
            var go = new GameObject($"Card_{index}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var cardRect = go.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);

            // 아래를 축으로 커진다. 손패가 화면 맨 아래라 위로 자라야 잘리지 않는다.
            cardRect.pivot = new Vector2(0.5f, 0f);

            var w = new CardWidgets
            {
                root = go,
                rect = cardRect,
                background = go.GetComponent<Image>(),
                group = go.AddComponent<CanvasGroup>(),
            };
            w.background.color = CardColor;

            // 카드 아트 — 상태 띠 위를 채운다. 바깥으로 ArtInset만큼 배경이 테두리로 남는다.
            var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(go.transform, false);
            var artRect = artGo.GetComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0f, ArtBottom);
            artRect.anchorMax = new Vector2(1f, 1f);
            artRect.offsetMin = new Vector2(ArtInset, ArtInset);
            artRect.offsetMax = new Vector2(-ArtInset, -ArtInset);

            w.art = artGo.GetComponent<Image>();
            w.art.preserveAspect = true;
            w.art.raycastTarget = false;
            w.art.enabled = false;

            // 아트가 없는 카드용 대체 표기. 아트가 붙으면 둘 다 꺼진다.
            w.nameLabel = BuildAnchored(go.transform, "Name", new Vector2(0f, 0.52f), new Vector2(1f, 0.72f),
                14, Color.white, FontStyle.Bold);
            w.subLabel = BuildAnchored(go.transform, "Sub", new Vector2(0f, 0.38f), new Vector2(1f, 0.53f),
                11, SubColor, FontStyle.Normal);

            // 하단 상태 띠 — 순번 · U · 조준 여부 · 예측 상태.
            w.statusLabel = BuildAnchored(go.transform, "Status", new Vector2(0f, 0f), new Vector2(1f, ArtBottom),
                11, HintColor, FontStyle.Bold);

            // 그리는 순서만 바꾸는 중첩 캔버스. 자체 레이캐스터가 있어야 드래그가 계속 먹는다.
            w.sorting = go.AddComponent<Canvas>();
            w.sorting.overrideSorting = true;
            w.sorting.sortingOrder = 0;
            go.AddComponent<GraphicRaycaster>();

            var handler = go.AddComponent<CardHandler>();
            handler.Index = index;
            handler.Owner = this;

            return w;
        }

        private static Text BuildAnchored(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(4f, 0f);
            rect.offsetMax = new Vector2(-4f, 0f);

            var t = go.GetComponent<Text>();
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

        private static Text BuildText(Transform parent, string name, float width, float height,
            int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);

            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        // ── 조작 ─────────────────────────────────────────

        private void HandleSwap(int from, int to)
        {
            if (!CanEditNow) return;
            _bulletTime.SwapHand(from, to);
        }

        /// <summary>
        /// 뗀 자리가 손패 판 안인지. 화면좌표로 본다 —
        /// 끌려 나간 카드의 RectTransform이 아니라 <b>커서 위치</b>가 기준이다.
        /// </summary>
        private bool IsInsideHandPanel(PointerEventData eventData)
        {
            if (_handPanelRect == null) return true;

            return RectTransformUtility.RectangleContainsScreenPoint(
                _handPanelRect, eventData.position, eventData.pressEventCamera);
        }

        // 카드를 손패 밖으로 꺼냄 → 조준이 필요한 스킬이면 조준 모드로 들어간다.
        private void HandleCardPulledOut(int index)
        {
            if (!CanEditNow) return;
            BeginAiming(index);
        }

        /// <summary>
        /// 시전 위치 지정으로 들어간다. 드래그로 꺼냈을 때와 집은 카드에서 위를 눌렀을 때
        /// 같은 길을 타야 한다 — 두 경로가 갈리면 조준 상태가 반쪽만 서는 조합이 생긴다.
        /// </summary>
        private bool BeginAiming(int index)
        {
            if (index < 0 || index >= Hand.Size) return false;

            SkillData data = _bulletTime.Hand.GetData(index);
            if (data == null) return false;

            if (data.targeting == TargetingType.None)
            {
                BattleLog.Log(LogCategory.Predict, $"{data.skillName} — 조준이 필요 없는 스킬", this);
                return false;
            }

            if (targetSelector != null)
            {
                // 그 카드 바로 위에서 시작한다. 기본값(가장 가까운 적)은 벨트스크롤 투영 탓에
                // 방 안쪽 적이 화면 우측 상단으로 밀려 올라가 늘 같은 구석에서 시작하는 것처럼 보인다.
                if (TryGetAimStartScreen(index, out Vector2 screen))
                    targetSelector.Begin(data, targetSelector.ScreenToGround(screen));
                else
                    targetSelector.Begin(data);
            }

            _aimingIndex = index;
            RefreshUI();
            return true;
        }

        /// <summary>
        /// 조준 시작점의 화면 좌표 — 카드 위쪽 모서리 중앙에서 조금 더 위.
        /// 캔버스가 ScreenSpaceOverlay라 RectTransform의 월드 코너가 곧 화면 픽셀이다.
        /// </summary>
        private bool TryGetAimStartScreen(int index, out Vector2 screen)
        {
            screen = default;

            if (_cards == null || index < 0 || index >= _cards.Length) return false;

            CardWidgets w = _cards[index];
            if (w == null || w.root == null || !w.root.activeInHierarchy) return false;

            var rect = w.root.transform as RectTransform;
            if (rect == null) return false;

            rect.GetWorldCorners(_corners);

            Vector2 bottomLeft = _corners[0];
            Vector2 topLeft = _corners[1];
            Vector2 topRight = _corners[2];

            float height = topLeft.y - bottomLeft.y;
            screen = (topLeft + topRight) * 0.5f + Vector2.up * (height * AimStartGap);
            return true;
        }

        private void CancelAiming()
        {
            if (_aimingIndex < 0) return;

            targetSelector?.Cancel();
            _aimingIndex = -1;
        }

        // ── 갱신 ─────────────────────────────────────────

        /// <summary>
        /// 목표 배율로 부드럽게 따라간다. 시간이 멈춘 동안 돌아가므로 <see cref="TimeControl.UnscaledDeltaTime"/>를 쓴다.
        /// <see cref="HorizontalLayoutGroup"/>은 localScale을 보지 않으므로 레이아웃은 흔들리지 않는다.
        /// </summary>
        private void AnimateCardScale()
        {
            if (_cards == null) return;

            float t = 1f - Mathf.Exp(-ScaleLerpSpeed * TimeControl.UnscaledDeltaTime);

            for (int i = 0; i < _cards.Length; i++)
            {
                CardWidgets w = _cards[i];
                if (w == null || w.rect == null) continue;

                float cur = w.rect.localScale.x;
                float next = Mathf.Lerp(cur, w.targetScale, t);
                if (Mathf.Abs(next - w.targetScale) < 0.001f) next = w.targetScale;

                w.rect.localScale = new Vector3(next, next, 1f);
            }
        }

        /// <summary>지금 초점이 가 있는 카드. 조준 &gt; 집힘 &gt; 커서 순. 없으면 -1.</summary>
        private int FocusedIndex(bool editable)
        {
            if (_aimingIndex >= 0) return _aimingIndex;
            if (!editable) return -1;
            if (_grabbedIndex >= 0) return _grabbedIndex;
            return _cursorIndex;
        }

        /// <summary>짚은 카드의 상세를 채운다. 짚은 게 없으면 판을 접는다.</summary>
        private void RefreshDetail(Hand hand, ComboPredictor predictor, int focus, bool editable)
        {
            if (_detailPanel == null) return;

            SkillData data = focus >= 0 && focus < Hand.Size ? hand.Get(focus).Data : null;
            if (data == null)
            {
                _detailPanel.SetActive(false);
                return;
            }

            _detailPanel.SetActive(true);

            // 손패 판 바로 위. 판 높이는 카드 수 · 힌트 길이에 따라 변하므로 매번 다시 잰다.
            if (_handPanelRect != null)
                _detailRect.anchoredPosition = new Vector2(
                    _handPanelRect.anchoredPosition.x,
                    _handPanelRect.anchoredPosition.y + _handPanelRect.rect.height + DetailGap);

            bool chained = editable && predictor != null && predictor.IsChained(hand.Slots, focus);
            bool down = editable && predictor != null && predictor.IsBlockedByDown(focus);

            _detailName.text = data.skillName;

            string[] rows = DetailRows(data, chained, down);
            for (int i = 0; i < _detailRows.Length && i < rows.Length; i++)
                _detailRows[i].text = rows[i];

            _detailRows[2].color = down ? DownColor : HintColor;
        }

        /// <summary>
        /// 상세 패널 본문 네 줄 — 분류 · 설명 · 연계 · 코스트.
        /// 화면과 테스트가 같은 함수를 본다.
        /// </summary>
        public static string[] DetailRows(SkillData data, bool chained, bool willBeDown)
        {
            if (data == null) return new[] { string.Empty, string.Empty, string.Empty, string.Empty };

            string chain = willBeDown
                ? "다운 — 무효 (한 대도 안 들어간다)"
                : $"{data.requireState} → {data.resultState}   [{(chained ? "강화" : "기본")}]";

            return new[]
            {
                $"{data.role} · {data.attackType}",
                string.IsNullOrWhiteSpace(data.description) ? "(설명 없음)" : data.description,
                chain,
                $"마나 {data.manaCost:0} · 쿨 {data.cooldown:0.#}초",
            };
        }

        /// <summary>상세 패널에 실제로 뜨는 글자 전부. 검증용 단일 창구다.</summary>
        public static string DetailLines(SkillData data, bool chained, bool willBeDown)
        {
            if (data == null) return string.Empty;

            string[] rows = DetailRows(data, chained, willBeDown);
            return data.skillName + System.Environment.NewLine +
                   string.Join(System.Environment.NewLine, rows);
        }

        private void RefreshUI()
        {
            Hand hand = _bulletTime.Hand;
            ComboPredictor predictor = _bulletTime.Predictor;
            bool editable = _bulletTime.AllowsCardEdit;

            // 손패가 줄어 커서가 빈자리를 짚고 있을 수 있다. 그리기 전에 잡는다.
            if (editable) EnsureCursorValid();

            if (editable)
            {
                _titleText.text = "전술 배치 — 왼쪽부터 순서대로 발동";

                if (_aimingIndex >= 0)
                    _hintText.text = "조준: WASD 또는 마우스 이동 · J/좌클릭 확정 · K/우클릭 취소";
                else if (_grabbedIndex >= 0)
                    _hintText.text = "집은 상태: A/D 순서 변경 · W 조준 · S 놓기";
                else
                    _hintText.text = "A/D 카드 선택 · W 집기 · 드래그도 가능 · E 또는 Space로 실행";
            }
            else
            {
                _titleText.text = $"손패  <color=#808080>덱 {_bulletTime.Deck.Count} · 버린 더미 {_bulletTime.Discard.Count}</color>";
                _hintText.text = "U — 맨 왼쪽 카드 사용 / E — 불릿타임";
            }

            for (int i = 0; i < Hand.Size; i++)
            {
                CardWidgets w = _cards[i];
                ComboSlot slot = hand.Get(i);

                if (slot.IsEmpty)
                {
                    w.targetScale = 1f;
                    w.root.SetActive(false);
                    continue;
                }

                w.root.SetActive(true);

                SkillData data = slot.Data;
                if (data == null)
                {
                    // SkillData가 안 꽂힌 카드. 발동은 못 하지만 손패에는 자리를 차지하므로 그대로 보여 준다.
                    w.art.enabled = false;
                    w.nameLabel.enabled = true;
                    w.subLabel.enabled = true;
                    w.nameLabel.text = "(빈 카드)";
                    w.subLabel.text = "SkillData 미지정";
                    w.statusLabel.text = string.Empty;
                    w.background.color = EmptyCardColor;
                    w.group.alpha = 1f;
                    w.group.blocksRaycasts = true;
                    w.targetScale = 1f;
                    continue;
                }

                // 카드 아트에 이름 · 코스트 · 설명이 이미 그려져 있으므로 텍스트를 겹치지 않는다.
                bool hasArt = data.icon != null;
                w.art.enabled = hasArt;
                w.nameLabel.enabled = !hasArt;
                w.subLabel.enabled = !hasArt;

                if (hasArt)
                {
                    w.art.sprite = data.icon;
                }
                else
                {
                    w.nameLabel.text = data.skillName;
                    w.subLabel.text = $"{data.role} · {data.attackType}";
                }

                bool aiming = i == _aimingIndex;
                bool grabbed = editable && i == _grabbedIndex;
                bool cursor = editable && _grabbedIndex < 0 && _aimingIndex < 0 && i == _cursorIndex;
                bool chained = editable && predictor != null && predictor.IsChained(hand.Slots, i);

                w.statusLabel.text = BuildStatus(i, in slot, data, aiming, grabbed, cursor, editable, predictor);

                w.background.color = aiming ? AimingCardColor
                                   : grabbed ? GrabbedCardColor
                                   : cursor ? CursorCardColor
                                   : chained ? ChainedCardColor
                                   : i == 0 ? NextCardColor
                                   : CardColor;

                // 짚은 카드는 커진다. 아래를 축으로 자라므로 손패 줄이 밀리지 않는다.
                bool focused = aiming || grabbed || cursor;
                w.targetScale = focused ? FocusScale : 1f;
                if (w.sorting != null) w.sorting.sortingOrder = focused ? 1 : 0;

                // 조준 대기 중에는 다른 카드를 흐리게 해서 초점을 남긴다.
                bool dim = _aimingIndex >= 0 && !aiming;
                w.group.alpha = dim ? 0.45f : 1f;
                w.group.blocksRaycasts = !dim;
            }

            RefreshDetail(hand, predictor, FocusedIndex(editable), editable);
        }

        /// <summary>하단 상태 띠 한 줄. 순번 · 조준 여부 · 예측 상태를 합친다.</summary>
        private static string BuildStatus(int index, in ComboSlot slot, SkillData data,
            bool aiming, bool grabbed, bool cursor, bool editable, ComboPredictor predictor)
        {
            if (aiming) return "조준 중";
            if (grabbed) return "집음 — A/D 이동 · S 놓기";

            string head = index == 0 && !editable ? "U" : $"{index + 1}";
            if (cursor) head = $"▸{head}";
            if (slot.aimed) head += " ◉";

            if (editable && predictor != null && index < predictor.Predicted.Count)
            {
                // 다운 무적에 흘리는 슬롯은 상태만 보면 "AerialHit → Down"이라 멀쩡해 보인다.
                // 한 대도 안 들어간다는 걸 글자로 못 박는다.
                if (predictor.IsBlockedByDown(index))
                    return $"{head}  <color=#FF8080>다운 — 무효</color>";

                return $"{head}  → {predictor.Predicted[index]}";
            }

            // 실시간에는 예측 대신 조준 방식을 알려 준다.
            return data.targeting == TargetingType.None ? head : $"{head}  {data.targeting}";
        }
    }
}
