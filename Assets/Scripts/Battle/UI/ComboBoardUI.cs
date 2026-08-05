using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 손패 카드를 콤보 슬롯에 드래그&드롭으로 배치하는 런타임 생성 UI.
    /// 씬/프리팹 연결 없이 코드로만 만든다 — BulletTimeController가 붙은 GameObject에 이 컴포넌트만 추가하면 동작한다.
    ///
    /// 기존 전투 로직(BulletTimeController/ComboSlotBoard/ComboExecutor/TargetSelector)은 건드리지 않고
    /// 그 공개 API 위에서만 동작한다. DebugComboHUD가 하던 "카드 편집 입력"을 대체하므로,
    /// 이 컴포넌트가 활성화되는 동안은 DebugComboHUD.SuppressCardInput을 켜서 입력만 겹치지 않게 한다
    /// (조작법 안내 등 OnGUI 정보 패널은 그대로 계속 보인다).
    /// </summary>
    [RequireComponent(typeof(BulletTimeController))]
    public class ComboBoardUI : MonoBehaviour
    {
        [Tooltip("비워두면 같은 GameObject 또는 자식에서 자동으로 찾는다.")]
        [SerializeField] private TargetSelector targetSelector;

        private const float CellSize = 110f;
        private const float CellSpacing = 10f;
        private const float TitleHeight = 28f;
        private const float HintHeight = 22f;
        private const float ExecuteButtonWidth = 120f;
        private const float ExecuteButtonHeight = 44f;
        private const float HandCardWidth = 140f;
        private const float HandCardHeight = 64f;

        private static readonly Color SlotColor = new Color(0.2f, 0.2f, 0.24f);
        private static readonly Color EmptySlotColor = new Color(0.15f, 0.15f, 0.17f);
        private static readonly Color ChainedSlotColor = new Color(0.3f, 0.5f, 0.3f);
        private static readonly Color HandCardColor = new Color(0.25f, 0.4f, 0.3f);
        private static readonly Color ExecuteButtonColor = new Color(0.7f, 0.2f, 0.2f);
        private static readonly Color HintColor = new Color(1f, 0.82f, 0.4f);

        private BulletTimeController _bulletTime;
        private DebugComboHUD _debugHud;
        private GameObject _canvasRoot;
        private Text _hintText;
        private Button _executeButton;

        private int _slotCount;
        private SlotWidgets[] _slotWidgets;
        private HandCardWidgets[] _handWidgets;

        // 조준이 필요한 스킬을 드롭한 뒤, 월드 클릭으로 확정하기를 기다리는 동안의 대기 상태.
        private int _pendingHandIndex = -1;
        private int _pendingSlotIndex = -1;

        private class SlotWidgets
        {
            public Image background;
            public Text label;
            public Text predictedLabel;
        }

        private class HandCardWidgets
        {
            public GameObject root;
            public Text label;
            public CanvasGroup group;
        }

        // 손패 카드 드래그. 놓든 실패하든 항상 원래 자리로 스냅백한다 —
        // 실제 손패 반영은 Hand.OnChanged -> RefreshUI가 담당하므로 드래그 자체는 상태를 바꾸지 않는다.
        private class HandCardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public int HandIndex;
            public CanvasGroup Group;

            private RectTransform _rect;
            private Vector2 _originalPos;
            private bool _dragging;

            private void Awake() => _rect = GetComponent<RectTransform>();

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (Group != null && !Group.interactable) return;

                _dragging = true;
                _originalPos = _rect.anchoredPosition;
                Group.blocksRaycasts = false;
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
                Group.blocksRaycasts = true;
                _rect.anchoredPosition = _originalPos;
            }
        }

        // 보드 슬롯 드래그: 슬롯끼리 드래그하면 재배치, 드래그 없이 클릭만 하면 손패로 회수한다.
        // uGUI는 드래그 임계값을 넘기면 같은 프레스에 대해 OnPointerClick을 호출하지 않으므로
        // 한 컴포넌트에서 드래그와 클릭을 함께 처리해도 서로 충돌하지 않는다.
        private class BoardSlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
        {
            public int SlotIndex;
            public ComboBoardUI Owner;

            private RectTransform _rect;
            private Vector2 _originalPos;
            private bool _dragging;

            private void Awake() => _rect = GetComponent<RectTransform>();

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (Owner._pendingHandIndex >= 0) return;
                if (Owner._bulletTime.Board.Get(SlotIndex).IsEmpty) return;

                _dragging = true;
                _originalPos = _rect.anchoredPosition;
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
                _rect.anchoredPosition = _originalPos;
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (Owner._pendingHandIndex >= 0) return;
                if (Owner._bulletTime.Board.Get(SlotIndex).IsEmpty) return;

                Owner._bulletTime.RecallToHand(SlotIndex);
            }
        }

        // 슬롯 위 드롭: 드래그 소스가 손패 카드인지 다른 보드 슬롯인지 구분해서 처리한다.
        private class SlotDropTarget : MonoBehaviour, IDropHandler
        {
            public int SlotIndex;
            public ComboBoardUI Owner;

            public void OnDrop(PointerEventData eventData)
            {
                if (eventData.pointerDrag == null) return;

                var handDrag = eventData.pointerDrag.GetComponent<HandCardDragHandler>();
                if (handDrag != null)
                {
                    Owner.HandleHandCardDropped(handDrag.HandIndex, SlotIndex);
                    return;
                }

                var slotDrag = eventData.pointerDrag.GetComponent<BoardSlotDragHandler>();
                if (slotDrag != null && slotDrag.SlotIndex != SlotIndex)
                    Owner.HandleSlotReordered(slotDrag.SlotIndex, SlotIndex);
            }
        }

        private void Awake()
        {
            _bulletTime = GetComponent<BulletTimeController>();

            if (targetSelector == null) targetSelector = GetComponent<TargetSelector>();
            if (targetSelector == null) targetSelector = GetComponentInChildren<TargetSelector>();

            _debugHud = FindAnyObjectByType<DebugComboHUD>();
        }

        private void Start()
        {
            EnsureEventSystem();

            _slotCount = _bulletTime.Board != null ? _bulletTime.Board.SlotCount : 0;
            _slotWidgets = new SlotWidgets[_slotCount];
            _handWidgets = new HandCardWidgets[Hand.Size];

            BuildUI();

            _bulletTime.Hand.OnChanged += RefreshUI;
            if (_bulletTime.Board != null) _bulletTime.Board.OnBoardChanged += RefreshUI;
            _bulletTime.OnEnter += HandleEnter;
            _bulletTime.OnExit += HandleExit;

            // DebugComboHUD는 같은 손패/슬롯을 키보드+마우스로도 조작하는 임시 컨트롤러라
            // 입력만 겹친다. 컴포넌트 자체는 켜둬서 조작법 안내 등 정보 패널은 계속 보이게 하고,
            // 손패/슬롯 입력 처리만 끈다.
            if (_debugHud != null) _debugHud.SuppressCardInput = true;

            SetVisible(_bulletTime.Tactic != null && _bulletTime.Tactic.AllowsCardEdit);
            RefreshUI();
        }

        private void OnDestroy()
        {
            if (_bulletTime == null) return;

            if (_bulletTime.Hand != null) _bulletTime.Hand.OnChanged -= RefreshUI;
            if (_bulletTime.Board != null) _bulletTime.Board.OnBoardChanged -= RefreshUI;
            _bulletTime.OnEnter -= HandleEnter;
            _bulletTime.OnExit -= HandleExit;

            if (_debugHud != null) _debugHud.SuppressCardInput = false;
        }

        private void Update()
        {
            if (_pendingHandIndex < 0) return;

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelPendingTarget();
                RefreshUI();
                return;
            }

            // UI 위 클릭은 배치 확정으로 치지 않는다 — 게임 화면(월드)을 클릭했을 때만 확정.
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                !EventSystem.current.IsPointerOverGameObject())
            {
                TargetInfo info = targetSelector != null ? targetSelector.Confirm() : TargetInfo.None;
                _bulletTime.PlaceFromHand(_pendingHandIndex, _pendingSlotIndex, in info);

                _pendingHandIndex = -1;
                _pendingSlotIndex = -1;
                RefreshUI();
            }
        }

        private void HandleEnter() => SetVisible(true);

        private void HandleExit()
        {
            CancelPendingTarget();
            SetVisible(false);
        }

        private void SetVisible(bool visible) => _canvasRoot.SetActive(visible);

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
            var canvasGo = new GameObject("ComboBoardCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvasRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            var panel = CreatePanel(canvasGo.transform, "ComboBoardPanel", new Color(0.1f, 0.1f, 0.12f, 0.9f));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0, 20);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 12;
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            float rowWidth = _slotCount * CellSize + Mathf.Max(0, _slotCount - 1) * CellSpacing;

            BuildTitle(panel.transform, rowWidth);
            BuildHint(panel.transform, rowWidth);
            BuildSlotRow(panel.transform);
            BuildExecuteButton(panel.transform);
            BuildHandRow(panel.transform);
        }

        private void BuildTitle(Transform parent, float width)
        {
            var go = new GameObject("Title", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, TitleHeight);

            var text = go.GetComponent<Text>();
            text.text = "전술 배치";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private void BuildHint(Transform parent, float width)
        {
            var go = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, HintHeight);

            _hintText = go.GetComponent<Text>();
            _hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hintText.fontSize = 15;
            _hintText.alignment = TextAnchor.MiddleCenter;
            _hintText.color = HintColor;
            _hintText.raycastTarget = false;
            _hintText.text = string.Empty;
        }

        private void BuildSlotRow(Transform parent)
        {
            var gridGo = new GameObject("SlotRow", typeof(RectTransform));
            gridGo.transform.SetParent(parent, false);

            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CellSize, CellSize);
            grid.spacing = new Vector2(CellSpacing, CellSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, _slotCount);

            for (int i = 0; i < _slotCount; i++)
                _slotWidgets[i] = BuildSlotCell(gridGo.transform, i);
        }

        private SlotWidgets BuildSlotCell(Transform parent, int index)
        {
            var cellGo = CreatePanel(parent, $"Slot_{index}", SlotColor);
            var widgets = new SlotWidgets { background = cellGo.GetComponent<Image>() };

            var drag = cellGo.AddComponent<BoardSlotDragHandler>();
            drag.SlotIndex = index;
            drag.Owner = this;

            var dropTarget = cellGo.AddComponent<SlotDropTarget>();
            dropTarget.SlotIndex = index;
            dropTarget.Owner = this;

            widgets.label = CreateLabel(cellGo.transform, string.Empty, 15, Color.white, FontStyle.Bold);
            widgets.label.raycastTarget = false;

            var predGo = new GameObject("Predicted", typeof(RectTransform), typeof(Text));
            predGo.transform.SetParent(cellGo.transform, false);
            var predRect = predGo.GetComponent<RectTransform>();
            predRect.anchorMin = new Vector2(0f, 0f);
            predRect.anchorMax = new Vector2(1f, 0f);
            predRect.pivot = new Vector2(0.5f, 0f);
            predRect.anchoredPosition = new Vector2(0, 4);
            predRect.sizeDelta = new Vector2(0, 20);

            widgets.predictedLabel = predGo.GetComponent<Text>();
            widgets.predictedLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            widgets.predictedLabel.fontSize = 11;
            widgets.predictedLabel.alignment = TextAnchor.LowerCenter;
            widgets.predictedLabel.color = new Color(0.6f, 1f, 0.6f);
            widgets.predictedLabel.raycastTarget = false;

            return widgets;
        }

        private void BuildExecuteButton(Transform parent)
        {
            _executeButton = CreateUIButton(parent, "실행", ExecuteButtonWidth, ExecuteButtonHeight, ExecuteButtonColor, OnExecuteClicked);
        }

        private void BuildHandRow(Transform parent)
        {
            var rowGo = new GameObject("HandRow", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);

            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            for (int i = 0; i < Hand.Size; i++)
                _handWidgets[i] = BuildHandCard(rowGo.transform, i);
        }

        private HandCardWidgets BuildHandCard(Transform parent, int index)
        {
            var go = new GameObject($"Hand_{index}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(HandCardWidth, HandCardHeight);
            go.GetComponent<Image>().color = HandCardColor;

            var widgets = new HandCardWidgets { root = go };
            widgets.label = CreateLabel(go.transform, string.Empty, 13, Color.white, FontStyle.Normal);
            widgets.label.raycastTarget = false;

            widgets.group = go.AddComponent<CanvasGroup>();

            var drag = go.AddComponent<HandCardDragHandler>();
            drag.HandIndex = index;
            drag.Group = widgets.group;

            return widgets;
        }

        private Button CreateUIButton(Transform parent, string label, float width, float height, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);

            var image = go.GetComponent<Image>();
            image.color = color;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            CreateLabel(go.transform, label, 16, Color.white, FontStyle.Bold);
            return button;
        }

        private Text CreateLabel(Transform parent, string text, int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            return t;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        // ── 배치 / 재배치 / 실행 ──────────────────────────

        // 손패 카드가 슬롯 위에 드롭됐을 때: 조준이 필요 없으면 즉시 배치, 필요하면 조준 대기로 들어간다.
        private void HandleHandCardDropped(int handIndex, int slotIndex)
        {
            if (_pendingHandIndex >= 0) return;
            if (_bulletTime.Tactic == null || !_bulletTime.Tactic.AllowsCardEdit) return;
            if (_bulletTime.Board == null || !_bulletTime.Board.Get(slotIndex).IsEmpty) return;

            ComboCard card = _bulletTime.Hand.Get(handIndex);
            if (card == null || card.Data == null) return;

            if (card.Data.targeting == TargetingType.None)
            {
                _bulletTime.PlaceFromHand(handIndex, slotIndex, TargetInfo.None);
                return;
            }

            targetSelector?.Begin(card.Data);
            _pendingHandIndex = handIndex;
            _pendingSlotIndex = slotIndex;
            RefreshUI();
        }

        // 보드 슬롯끼리 드래그&드롭 -> 순서 교환. ComboSlotBoard.Reorder를 그대로 호출한다.
        private void HandleSlotReordered(int fromIndex, int toIndex)
        {
            if (_pendingHandIndex >= 0) return;
            if (_bulletTime.Tactic == null || !_bulletTime.Tactic.AllowsCardEdit) return;
            if (_bulletTime.Board == null) return;

            _bulletTime.Board.Reorder(fromIndex, toIndex);
            _bulletTime.Predictor?.Simulate(_bulletTime.Board.Slots);
        }

        private void OnExecuteClicked()
        {
            if (_pendingHandIndex >= 0) return;
            _bulletTime.Exit();
        }

        private void CancelPendingTarget()
        {
            if (_pendingHandIndex < 0) return;

            targetSelector?.Cancel();
            _pendingHandIndex = -1;
            _pendingSlotIndex = -1;
        }

        // ── 갱신 ─────────────────────────────────────────

        private void RefreshUI()
        {
            bool editable = _bulletTime.Tactic != null && _bulletTime.Tactic.AllowsCardEdit && _pendingHandIndex < 0;

            _hintText.text = _pendingHandIndex >= 0 ? "조준: 좌클릭 확정 / 우클릭 취소" : string.Empty;

            ComboSlotBoard board = _bulletTime.Board;
            ComboPredictor predictor = _bulletTime.Predictor;
            bool hasAnyCard = false;

            for (int i = 0; i < _slotCount; i++)
            {
                SlotWidgets w = _slotWidgets[i];
                ComboSlot slot = board.Get(i);

                if (slot.IsEmpty)
                {
                    w.label.text = string.Empty;
                    w.predictedLabel.text = string.Empty;
                    w.background.color = EmptySlotColor;
                    continue;
                }

                hasAnyCard = true;
                w.label.text = slot.Data.skillName;

                bool chained = predictor != null && predictor.IsChained(board.Slots, i);
                w.predictedLabel.text = predictor != null && i < predictor.Predicted.Count
                    ? predictor.Predicted[i].ToString()
                    : "?";
                w.background.color = chained ? ChainedSlotColor : SlotColor;
            }

            _executeButton.interactable = editable && hasAnyCard;

            int handCount = _bulletTime.Hand.Count;
            for (int i = 0; i < Hand.Size; i++)
            {
                HandCardWidgets w = _handWidgets[i];
                bool has = i < handCount;
                w.root.SetActive(has);
                if (!has) continue;

                ComboCard card = _bulletTime.Hand.Get(i);
                w.label.text = card != null && card.Data != null
                    ? $"{card.Data.skillName}\n{card.Data.role}"
                    : "?";

                w.group.interactable = editable;
                w.group.alpha = editable ? 1f : 0.4f;
            }
        }
    }
}
