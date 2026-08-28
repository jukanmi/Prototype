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
    /// 카드는 실제 손에 쥔 것처럼 <b>부채꼴로 겹쳐</b> 놓인다. 겹쳐 있으니 선택된 장은
    /// 앞으로 나오면서 떠올라야 하고, 집으면 크게 솟아야 무엇을 들고 있는지 읽힌다.
    /// 그 자리 계산은 <see cref="HandFanLayout"/>이, 판의 세로 쌓기는 <see cref="HandBoardLayout"/>이,
    /// 미끄러지는 움직임은 <see cref="CardPoseAnimator"/>가 맡는다 —
    /// 전부 순수 함수라 씬을 켜지 않고 검증할 수 있다.
    /// 여기 남은 책임은 둘뿐이다: <b>RefreshUI</b>가 색과 글자를, <b>AnimateCards</b>가 자리를 바른다.
    ///
    /// 손패 바로 아래에 <see cref="BulletTimeGaugeWidget"/>이 붙는다. 카드를 쏟아내는 값이
    /// 카드와 같은 눈길 안에 있어야 E를 눌러도 되는 때를 안다.
    ///
    /// 기존 전투 로직(BulletTimeController/ComboExecutor/TargetSelector)은 건드리지 않고
    /// 그 공개 API 위에서만 동작한다.
    /// </summary>
    [RequireComponent(typeof(BulletTimeController))]
    public class ComboBoardUI : MonoBehaviour
    {
        [Tooltip("비워두면 같은 GameObject 또는 자식에서 자동으로 찾는다.")]
        [SerializeField] private TargetSelector targetSelector;

        // 카드 크기와 부채꼴 기하는 HandFanLayout이, 판의 세로 쌓기는 HandBoardLayout이 정한다.
        // 여기 남은 건 카드 한 장의 내부 구성뿐이다.
        // 아래 StatusBarHeight만큼은 상태 띠로 남기고 그 위를 아트가 채운다.
        private const float StatusBarHeight = 22f;
        private const float ArtInset = 3f;

        /// <summary>조준 시작점을 카드 위쪽 모서리에서 얼마나 더 띄울지. 카드 높이 대비 비율.</summary>
        private const float AimStartGap = 0.5f;

        /// <summary>쿨타임 중인 카드의 투명도. 지금 못 쓴다는 걸 글자 없이도 알아보게 한다.</summary>
        private const float CooldownAlpha = 0.45f;

        /// <summary>아트 영역이 시작하는 세로 비율. 그 아래는 상태 띠.</summary>
        private const float ArtBottom = StatusBarHeight / HandFanLayout.CardHeight;

        private static readonly Color PanelColor = new Color(0.1f, 0.1f, 0.12f, 0.85f);
        private static readonly Color CardColor = new Color(0.22f, 0.26f, 0.3f);
        private static readonly Color NextCardColor = new Color(0.28f, 0.38f, 0.34f);
        private static readonly Color AimingCardColor = new Color(0.45f, 0.35f, 0.15f);
        private static readonly Color CursorCardColor = new Color(0.30f, 0.44f, 0.58f);
        private static readonly Color GrabbedCardColor = new Color(0.52f, 0.44f, 0.20f);
        private static readonly Color EmptyCardColor = new Color(0.4f, 0.18f, 0.18f);
        private static readonly Color HintColor = new Color(1f, 0.82f, 0.4f);
        private static readonly Color SubColor = new Color(0.72f, 0.76f, 0.8f);
        private static readonly Color ArrowColor = new Color(1f, 0.86f, 0.35f);

        /// <summary>황금 카드의 테두리. 레벨업 화면(<see cref="CardOfferView"/>)과 같은 금색이다.</summary>
        private static readonly Color GoldFrameColor = new Color(1f, 0.80f, 0.28f);

        // ── 카드 상세 패널 ───────────────────────────────
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

        /// <summary>부채꼴의 중심. 카드는 전부 이 안에서 anchoredPosition으로 논다.</summary>
        private RectTransform _rowRect;

        /// <summary>선택 표시 화살표. 커서 카드를 같은 감쇠로 따라간다.</summary>
        private RectTransform _arrowRect;
        private Text _arrowText;
        private Vector2 _arrowPos;
        private bool _arrowPlaced;

        private readonly BulletTimeGaugeWidget _gauge = new BulletTimeGaugeWidget();

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
            /// <summary>포즈를 바르는 곳. 매 프레임 GetComponent를 피하려고 들고 있는다.</summary>
            public RectTransform rect;
            /// <summary>카드 전체를 덮는 판. 아트 바깥 테두리 · 하단 상태 띠가 이 색으로 보인다.</summary>
            public Image background;

            /// <summary>
            /// 황금 카드의 안쪽 판. 배경을 금색으로 칠한 뒤 이 판이 <see cref="ArtInset"/>만큼
            /// 물러난 자리를 상태색으로 덮어, 딱 그 두께의 금테만 남긴다.
            /// 일반 카드에서는 꺼져 있어 배경 하나로 그리던 예전 그림 그대로다.
            /// </summary>
            public Image inner;

            /// <summary>SkillData.icon. 없으면 꺼지고 이름 · 직업 텍스트가 대신 나온다.</summary>
            public Image art;
            public Text nameLabel;
            public Text subLabel;
            public Text statusLabel;
            public CanvasGroup group;

            /// <summary>지금 그려지고 있는 포즈. 매 프레임 target 쪽으로 미끄러진다.</summary>
            public CardPose pose;

            /// <summary>가야 할 자리. RefreshUI가 상태를 보고 정한다.</summary>
            public CardPose target;

            /// <summary>마우스가 끌고 있는 중. 그동안 애니메이터는 손을 뗀다.</summary>
            public bool dragging;

            /// <summary>한 번이라도 자리를 잡았는지. 처음 뽑힌 카드는 날아오지 않고 제자리에서 나타난다.</summary>
            public bool placed;

            /// <summary>쿨타임 표시로 덮어 둔 상태. 풀리는 순간을 잡아 원래 글자를 되돌리는 데 쓴다.</summary>
            public bool cooling;
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

                // 맨 앞으로 올리는 것도, 자세를 세우는 것도 Owner가 한다.
                Owner.SetDragging(Index, true);

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

                // 제자리로 순간이동시키지 않는다. 애니메이터가 놓인 자리에서부터
                // 부채꼴로 미끄러져 돌아간다 — 집기를 놓을 때와 같은 감이어야 한다.
                Owner.SetDragging(Index, false);

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

        private void Update()
        {
            HandleInput();

            // 카드와 게이지는 <b>실시간에도</b> 흐른다. 그리고 불릿타임 중에는
            // TimeControl.Scale이 0이므로 배율 시간으로 돌리면 화면이 얼어붙는다.
            float dt = TimeControl.UnscaledDeltaTime;

            AnimateCards(dt);
            AnimateArrow(dt);
            RefreshCooldowns();
            _gauge.Refresh(_bulletTime, dt);
        }

        /// <summary>
        /// 실시간 쿨타임 표시. 손패가 안 바뀌어도 숫자는 매 프레임 줄어야 하므로
        /// <see cref="RefreshUI"/>(이벤트 구동)가 아니라 여기서 덮어 쓴다.
        /// </summary>
        private void RefreshCooldowns()
        {
            if (!_bulletTime.RealtimeCooldownEnabled) return;

            // 전술 배치 중에는 실행 순서 · 체인 예측이 상태 띠를 쓴다. 쿨타임은 실시간에만 그린다.
            if (_bulletTime.AllowsCardEdit) return;

            bool restore = false;

            for (int i = 0; i < Hand.Size; i++)
            {
                CardWidgets w = _cards[i];
                if (w == null || !w.root.activeSelf) continue;

                float left = _bulletTime.SkillCooldownRemaining(_bulletTime.Hand.Get(i).Data);

                if (left > 0f)
                {
                    w.statusLabel.text = $"<color=#FF8080>쿨 {left:0.0}s</color>";
                    w.group.alpha = CooldownAlpha;
                    w.cooling = true;
                }
                else if (w.cooling)
                {
                    // 원래 글자를 여기서 다시 조립하지 않는다 — RefreshUI 한 번이면 전부 제자리로 온다.
                    w.cooling = false;
                    restore = true;
                }
            }

            if (restore) RefreshUI();
        }

        /// <summary>
        /// 키보드 카드 조작. 세 상태가 <b>배타적</b>으로 하나만 돈다 —
        /// 조준 확정(J)과 카드 놓기(J)가 기본값이 같아서, 한 프레임에 두 갈래가 돌면
        /// 한 번 누른 J가 조준을 확정하고 그 카드를 놓는 것까지 해 버린다.
        /// </summary>
        private void HandleInput()
        {
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
            panelRect.anchoredPosition = new Vector2(0f, HandBoardLayout.ScreenMargin);
            panelRect.sizeDelta = HandBoardLayout.PanelSize;

            // <b>배경 상자는 그리지 않는다.</b> 카드가 상자 안에 갇혀 보이던 원인이고,
            // 집은 카드는 어차피 판 위로 솟아오른다. 사각형은 드래그 판정 기준으로만 남는다.
            panel.GetComponent<Image>().enabled = false;

            // 레이아웃 그룹은 안 쓴다. 부채꼴은 회전과 겹침이 있어 어떤 그룹으로도 표현이 안 되고,
            // 자리는 HandBoardLayout · HandFanLayout이 계산해 준다.
            BuildCardRow(panelRect);

            _gauge.Build(panelRect);
            Place(_gauge.Root, HandBoardLayout.GaugeCenterY);

            _titleText = BuildText(panelRect, "Title", HandBoardLayout.TextWidth,
                HandBoardLayout.TitleHeight, 18, Color.white, FontStyle.Bold);
            Place(_titleText.rectTransform, HandBoardLayout.TitleCenterY);

            _hintText = BuildText(panelRect, "Hint", HandBoardLayout.TextWidth,
                HandBoardLayout.HintHeight, 14, HintColor, FontStyle.Normal);
            Place(_hintText.rectTransform, HandBoardLayout.HintCenterY);

            BuildDetailPanel(canvasGo.transform);
        }

        /// <summary>판 바닥 중앙을 원점 삼아 높이만 지정해 앉힌다. 가로는 항상 가운데.</summary>
        private static void Place(RectTransform rect, float centerY)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, centerY);
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

            _rowRect = (RectTransform)rowGo.transform;
            _rowRect.sizeDelta = HandBoardLayout.RowSize;
            Place(_rowRect, HandBoardLayout.RowCenterY);

            for (int i = 0; i < Hand.Size; i++)
                _cards[i] = BuildCard(_rowRect, i);

            BuildArrow(_rowRect);
        }

        /// <summary>
        /// 선택 표시 화살표. 커서가 짚은 카드 <b>바로 위</b>에 서서 아트를 가리지 않는다.
        /// 부채꼴은 카드끼리 겹쳐 있어 색만으로는 어느 장이 선택됐는지 읽기 어렵다.
        /// </summary>
        private void BuildArrow(Transform parent)
        {
            _arrowText = UiFactory.NewText(parent, "SelectArrow", 34, ArrowColor, FontStyle.Bold);
            _arrowText.text = "▼";
            _arrowText.enabled = false;

            _arrowRect = _arrowText.rectTransform;
            _arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            _arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            _arrowRect.pivot = new Vector2(0.5f, 0.5f);
            _arrowRect.sizeDelta = new Vector2(48f, 40f);
        }

        private CardWidgets BuildCard(Transform parent, int index)
        {
            var go = new GameObject($"Card_{index}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            // 부채꼴 중심을 기준으로 논다. 회전도 카드 한가운데를 축으로 돈다.
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(HandFanLayout.CardWidth, HandFanLayout.CardHeight);

            var w = new CardWidgets
            {
                root = go,
                rect = rect,
                background = go.GetComponent<Image>(),
                group = go.AddComponent<CanvasGroup>(),
                pose = CardPose.Identity,
                target = CardPose.Identity,
            };
            w.background.color = CardColor;

            // 황금 카드의 안쪽 판. 자식은 부모 위에 그려지므로 카드 <b>뒤에</b> 테두리를 깔 수가 없다 —
            // 대신 배경을 금색으로 칠하고 이 판이 안쪽을 도로 덮어 링만 남긴다.
            // 아트와 글자는 이 뒤에 만들어지므로 이 판 위에 온다.
            w.inner = UiFactory.NewImage(go.transform, "Inner", CardColor);
            UiFactory.Stretch(w.inner.rectTransform, ArtInset);
            w.inner.enabled = false;

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

            // 한 줄짜리 안내다. 줄바꿈 대신 넘치게 둔다 — 잘려서 사라지는 게 더 나쁘다.
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
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

        /// <summary>지금 초점이 가 있는 카드. 조준 &gt; 집힘 &gt; 커서 순. 없으면 -1.</summary>
        private int FocusedIndex(bool editable)
        {
            if (_aimingIndex >= 0) return _aimingIndex;
            if (!editable) return -1;
            if (_grabbedIndex >= 0) return _grabbedIndex;
            return _cursorIndex;
        }

        /// <summary>짚은 카드의 상세를 채운다. 짚은 게 없으면 판을 접는다.</summary>
        private void RefreshDetail(Hand hand, int focus)
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

            _detailName.text = data.skillName;

            string[] rows = DetailRows(data);
            for (int i = 0; i < _detailRows.Length && i < rows.Length; i++)
                _detailRows[i].text = rows[i];
        }

        /// <summary>
        /// 상세 패널 본문 세 줄 — 분류 · 설명 · 코스트.
        /// 화면과 테스트가 같은 함수를 본다.
        /// </summary>
        public static string[] DetailRows(SkillData data)
        {
            if (data == null) return new[] { string.Empty, string.Empty, string.Empty };

            return new[]
            {
                $"{data.role} · {data.attackType}",
                string.IsNullOrWhiteSpace(data.description) ? "(설명 없음)" : data.description,
                $"마나 {data.manaCost:0} · 쿨 {data.cooldown:0.#}초",
            };
        }

        /// <summary>상세 패널에 실제로 뜨는 글자 전부. 검증용 단일 창구다.</summary>
        public static string DetailLines(SkillData data)
        {
            if (data == null) return string.Empty;

            string[] rows = DetailRows(data);
            return data.skillName + System.Environment.NewLine +
                   string.Join(System.Environment.NewLine, rows);
        }

        private void RefreshUI()
        {
            Hand hand = _bulletTime.Hand;
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
                    w.root.SetActive(false);

                    // 꺼진 칸은 다음에 켜질 때 제자리에서 나타난다. 끌던 중이었다면 그 상태도 같이 접는다 —
                    // 안 그러면 없는 카드가 계속 "집힌 상태"로 남아 맨 앞자리를 차지한다.
                    w.placed = false;
                    w.dragging = false;
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
                    SetCardColor(w, EmptyCardColor, false);
                    w.group.alpha = 1f;
                    w.group.blocksRaycasts = true;
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

                w.statusLabel.text = BuildStatus(i, in slot, data, aiming, grabbed, cursor, editable);

                Color face = aiming ? AimingCardColor
                           : grabbed ? GrabbedCardColor
                           : cursor ? CursorCardColor
                           : i == 0 ? NextCardColor
                           : CardColor;

                // 등급은 테두리로, 상태는 면으로. 두 정보를 같은 채널에 실으면 둘 다 안 읽힌다.
                SetCardColor(w, face, slot.card != null && slot.card.Golden);

                // 조준 대기 중에는 다른 카드를 흐리게 해서 초점을 남긴다.
                bool dim = _aimingIndex >= 0 && !aiming;
                w.group.alpha = dim ? 0.45f : 1f;
                w.group.blocksRaycasts = !dim;
            }

            RefreshDetail(hand, FocusedIndex(editable));

            // 색과 글자를 정한 뒤에 자리를 정한다 — 어느 카드가 켜져 있는지 확정돼야
            // 부채꼴을 몇 장짜리로 펼지 정할 수 있다.
            UpdateTargets();
        }

        /// <summary>
        /// 카드 면색과 등급 테두리를 한 번에 바른다.
        ///
        /// 일반 카드면 두 겹을 꺼서 배경 하나로 그리던 예전 그림 그대로 두고,
        /// 황금이면 금색 판을 켠 뒤 안쪽만 면색으로 덮어 <see cref="ArtInset"/> 두께의 링을 남긴다.
        /// </summary>
        private static void SetCardColor(CardWidgets w, Color face, bool golden)
        {
            w.background.color = golden ? GoldFrameColor : face;

            if (w.inner == null) return;

            w.inner.enabled = golden;
            w.inner.color = face;
        }

        // ── 포즈 · 애니메이션 ────────────────────────────
        // 여기가 "어디에 있어야 하나"를, RefreshUI가 "어떻게 보여야 하나"를 맡는다.
        // 카드의 위치 · 회전 · 크기는 이 아래 코드만 건드린다.

        /// <summary>지금 이 카드가 받아야 할 대접.</summary>
        private CardVisualState StateOf(int index, bool editable)
        {
            // 마우스가 끌고 있는 카드는 손에 들린 것과 같이 취급한다.
            // (자리는 커서가 정하고 기울기 · 크기만 이 자세를 따른다)
            if (_cards[index] != null && _cards[index].dragging) return CardVisualState.Grabbed;

            // 실시간에는 다음에 나갈 맨 왼쪽 카드만 살짝 띄운다. U키가 무엇을 쓰는지 보여야 한다.
            if (!editable)
                return index == 0 ? CardVisualState.Next : CardVisualState.Idle;

            // 조준 중인 카드는 집은 자세를 유지한다 — 집기에서 곧장 넘어온 상태라
            // 여기서 자세가 내려앉으면 조준 시작점(카드 위)까지 같이 튄다.
            if (index == _aimingIndex || index == _grabbedIndex) return CardVisualState.Grabbed;

            if (_grabbedIndex < 0 && _aimingIndex < 0 && index == _cursorIndex)
                return CardVisualState.Cursor;

            return CardVisualState.Idle;
        }

        /// <summary>맨 앞으로 끌어올릴 카드. 겹친 부채꼴에서는 이게 곧 "선택됐다"는 신호다.</summary>
        private int FrontIndex()
        {
            // 끌고 다니는 카드가 최우선이다. 아래 깔린 카드에 반쯤 먹힌 채 커서를 따라다니면
            // 무엇을 집었는지 알 수 없다.
            for (int i = 0; i < _cards.Length; i++)
                if (_cards[i] != null && _cards[i].dragging) return i;

            if (_aimingIndex >= 0) return _aimingIndex;
            if (!_bulletTime.AllowsCardEdit) return -1;
            return _grabbedIndex >= 0 ? _grabbedIndex : _cursorIndex;
        }

        /// <summary>화살표가 가리킬 카드. 조준 중에는 화면의 조준선이 대신하므로 접는다.</summary>
        private int ArrowIndex()
        {
            if (!_bulletTime.AllowsCardEdit || _aimingIndex >= 0) return -1;
            return _grabbedIndex >= 0 ? _grabbedIndex : _cursorIndex;
        }

        /// <summary>목표 포즈와 겹침 순서를 다시 잡는다. 실제 이동은 <see cref="AnimateCards"/>가 한다.</summary>
        private void UpdateTargets()
        {
            int count = _bulletTime.Hand.Count;
            bool editable = _bulletTime.AllowsCardEdit;

            for (int i = 0; i < Hand.Size; i++)
            {
                CardWidgets w = _cards[i];
                if (w == null || w.rect == null || !w.root.activeSelf) continue;

                w.target = HandFanLayout.Pose(i, count, StateOf(i, editable));
                w.rect.SetSiblingIndex(HandFanLayout.SiblingIndex(i, count));

                // 방금 뽑혀 처음 켜진 카드는 화면 구석에서 날아오지 않고 제자리에서 나타난다.
                if (!w.placed)
                {
                    w.pose = w.target;
                    w.placed = true;
                    ApplyPose(w);
                }
            }

            int front = FrontIndex();
            if (front >= 0 && front < Hand.Size && _cards[front] != null && _cards[front].root.activeSelf)
                _cards[front].rect.SetAsLastSibling();

            // 화살표는 무조건 맨 위. 카드 뒤로 가면 반쯤 잘려 보인다.
            if (_arrowRect != null) _arrowRect.SetAsLastSibling();
        }

        private void AnimateCards(float dt)
        {
            if (_cards == null) return;

            for (int i = 0; i < _cards.Length; i++)
            {
                CardWidgets w = _cards[i];
                if (w == null || w.rect == null || !w.root.activeSelf) continue;

                if (w.dragging)
                {
                    // 자리는 마우스가 정한다. 기울기와 크기만 집은 자세로 따라 붙인다 —
                    // 손에 들린 카드가 비스듬히 누워 있으면 무엇을 끌고 있는지 잘 안 보인다.
                    CardPose cur = w.pose;
                    cur.pos = w.rect.anchoredPosition;

                    CardPose t = w.target;
                    t.pos = cur.pos;

                    w.pose = CardPoseAnimator.Step(in cur, in t, dt);
                }
                else if (CardPoseAnimator.IsSettled(in w.pose, in w.target))
                {
                    // 남은 미동을 끊는다. 안 그러면 영원히 소수점 자리가 떨린다.
                    w.pose = w.target;
                }
                else
                {
                    w.pose = CardPoseAnimator.Step(in w.pose, in w.target, dt);
                }

                ApplyPose(w);
            }
        }

        private static void ApplyPose(CardWidgets w)
        {
            w.rect.anchoredPosition = w.pose.pos;
            w.rect.localRotation = Quaternion.Euler(0f, 0f, w.pose.angle);
            w.rect.localScale = new Vector3(w.pose.scale, w.pose.scale, 1f);
        }

        private void AnimateArrow(float dt)
        {
            if (_arrowRect == null || _cards == null) return;

            int idx = ArrowIndex();
            bool show = idx >= 0 && idx < _cards.Length &&
                        _cards[idx] != null && _cards[idx].root.activeSelf;

            _arrowText.enabled = show;

            if (!show)
            {
                _arrowPlaced = false;
                return;
            }

            // 현재 포즈를 따라간다 — 카드가 떠오르는 동안 화살표도 같이 올라가야
            // 둘이 한 덩어리로 읽힌다.
            Vector2 target = HandFanLayout.ArrowPos(in _cards[idx].pose);

            // 처음 켜질 때는 미끄러져 들어오지 않는다. 화면 구석에서 날아오면 산만하다.
            _arrowPos = _arrowPlaced
                ? new Vector2(
                    CardPoseAnimator.Step(_arrowPos.x, target.x, dt, CardPoseAnimator.DefaultSpeed),
                    CardPoseAnimator.Step(_arrowPos.y, target.y, dt, CardPoseAnimator.DefaultSpeed))
                : target;

            _arrowPlaced = true;
            _arrowRect.anchoredPosition = _arrowPos;
        }

        /// <summary>
        /// 마우스 드래그의 시작과 끝. 끝날 때 <b>지금 놓인 자리를 포즈로 받아 적는다</b> —
        /// 그래야 애니메이터가 순간이동 없이 거기서부터 부채꼴로 돌아간다.
        /// </summary>
        private void SetDragging(int index, bool dragging)
        {
            if (_cards == null || index < 0 || index >= _cards.Length) return;

            CardWidgets w = _cards[index];
            if (w == null) return;

            w.dragging = dragging;
            if (!dragging && w.rect != null) w.pose.pos = w.rect.anchoredPosition;

            // 자세와 겹침 순서를 즉시 다시 잡는다. 손패 자체는 안 바뀌었으니 OnChanged가 안 온다.
            UpdateTargets();
        }

        /// <summary>하단 상태 띠 한 줄. 순번 · 조준 여부 · 조준 방식을 합친다.</summary>
        private static string BuildStatus(int index, in ComboSlot slot, SkillData data,
            bool aiming, bool grabbed, bool cursor, bool editable)
        {
            if (aiming) return "조준 중";
            if (grabbed) return "집음 — A/D 이동 · S 놓기";

            string head = index == 0 && !editable ? "U" : $"{index + 1}";
            if (cursor) head = $"▸{head}";
            if (slot.aimed) head += " ◉";

            return data.targeting == TargetingType.None ? head : $"{head}  {data.targeting}";
        }
    }
}
