using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Prototype.YG
{
    /// <summary>
    /// 인게임 씬 우상단의 [처음부터] 버튼과 확인 패널. 캔버스를 코드로 만들므로
    /// 씬 · 프리팹 배선이 필요 없다 — <see cref="BattleSceneController"/> 가 Start 에서 붙인다.
    ///
    /// 초기화 자체는 여기서 하지 않는다. 확인 버튼은
    /// <see cref="BattleSceneController.RestartStage"/> 로만 넘긴다.
    ///
    /// TMP 대신 레거시 Text 를 쓰는 이유 — TMP Essential Resources 가 임포트돼 있지 않으면
    /// 런타임에 글자가 아예 안 나온다. 배틀 씬의 다른 런타임 UI(ComboBoardUI)와도 같은 방식이다.
    /// </summary>
    public class BattleRestartUI : MonoBehaviour
    {
        /// <summary>ComboBoardUI 캔버스(0)보다 위, SceneLoader 의 FadeCanvas(999)보다 아래.</summary>
        private const int SortingOrder = 100;

        private const float ButtonWidth = 150f;
        private const float ButtonHeight = 52f;
        private const float ScreenMargin = 24f;

        private static readonly Color RestartColor = new Color(0.62f, 0.42f, 0.16f);
        private static readonly Color ConfirmColor = new Color(0.70f, 0.20f, 0.20f);
        private static readonly Color CancelColor  = new Color(0.25f, 0.27f, 0.32f);
        private static readonly Color PanelColor   = new Color(0.10f, 0.10f, 0.12f, 0.97f);
        private static readonly Color DimColor     = new Color(0f, 0f, 0f, 0.65f);

        private BattleSceneController controller;

        private GameObject confirmRoot;
        private Button restartButton;
        private Button confirmButton;
        private Button cancelButton;

        /// <summary>
        /// 배틀 씬에 UI 를 띄운다. 씬과 함께 언로드되도록 컨트롤러의 자식으로 붙인다.
        /// </summary>
        public static BattleRestartUI Create(BattleSceneController owner)
        {
            if (owner == null) return null;

            var go = new GameObject("BattleRestartUI", typeof(RectTransform));
            go.transform.SetParent(owner.transform, false);

            BattleRestartUI ui = go.AddComponent<BattleRestartUI>();
            ui.controller = owner;

            return ui;
        }

        // controller 는 AddComponent 직후에 채워지므로 Awake 에서는 아직 비어 있다. Start 에서 짓는다.
        private void Start()
        {
            EnsureEventSystem();
            BuildUI();
            SetConfirmVisible(false);
        }

        private void OnDestroy()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
            if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (cancelButton  != null) cancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        /// <summary>
        /// Boot 씬을 거쳐 들어오면 EventSystem 이 이미 있다. 배틀 씬만 단독으로 Play 했을 때를 위한 보험.
        /// 중복 생성되면 UI 입력이 불안정해지므로 없을 때만 만든다.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ── UI 빌드 ──────────────────────────────────────

        private void BuildUI()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            BuildRestartButton();
            BuildConfirmPanel();
        }

        private void BuildRestartButton()
        {
            restartButton = CreateButton(transform, "Btn_Restart", "처음부터", RestartColor,
                                         new Vector2(ButtonWidth, ButtonHeight), 22);

            var rect = (RectTransform)restartButton.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-ScreenMargin, -ScreenMargin);

            restartButton.onClick.AddListener(OnRestartClicked);
        }

        /// <summary>
        /// 확인 패널. 뒤를 덮는 Dim 이미지가 레이캐스트를 먹으므로 패널이 떠 있는 동안
        /// 카드 배치 UI 클릭이 새어 들어가지 않는다.
        /// </summary>
        private void BuildConfirmPanel()
        {
            confirmRoot = CreateImage(transform, "ConfirmDim", DimColor);
            Stretch((RectTransform)confirmRoot.transform);

            GameObject panel = CreateImage(confirmRoot.transform, "Panel", PanelColor);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 240f);
            panelRect.anchoredPosition = Vector2.zero;

            Text title = CreateText(panel.transform, "Title", "전투를 처음부터 다시 시작할까?", 26, Color.white);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = titleRect.anchorMax = titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(520f, 60f);
            titleRect.anchoredPosition = new Vector2(0f, -46f);

            Text note = CreateText(panel.transform, "Note", "진행 중인 전투 상황은 모두 사라진다.", 17,
                                   new Color(0.75f, 0.75f, 0.78f));
            var noteRect = (RectTransform)note.transform;
            noteRect.anchorMin = noteRect.anchorMax = noteRect.pivot = new Vector2(0.5f, 1f);
            noteRect.sizeDelta = new Vector2(520f, 30f);
            noteRect.anchoredPosition = new Vector2(0f, -96f);

            confirmButton = CreateButton(panel.transform, "Btn_Confirm", "재시작", ConfirmColor,
                                         new Vector2(180f, 56f), 22);
            PlaceInPanel(confirmButton, new Vector2(-100f, 52f));
            confirmButton.onClick.AddListener(OnConfirmClicked);

            cancelButton = CreateButton(panel.transform, "Btn_Cancel", "취소", CancelColor,
                                        new Vector2(180f, 56f), 22);
            PlaceInPanel(cancelButton, new Vector2(100f, 52f));
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        private static void PlaceInPanel(Button button, Vector2 offsetFromBottom)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = offsetFromBottom;
        }

        // ── 입력 ─────────────────────────────────────────

        private void OnRestartClicked() => SetConfirmVisible(true);

        private void OnCancelClicked() => SetConfirmVisible(false);

        private void OnConfirmClicked()
        {
            // 전환이 시작되면 버튼을 다시 누를 수 없어야 한다. 씬은 곧 통째로 내려가지만
            // 페이드아웃이 도는 동안에도 클릭은 계속 들어온다.
            SetInteractable(false);

            if (controller != null) controller.RestartStage();
        }

        private void SetConfirmVisible(bool visible)
        {
            if (confirmRoot != null) confirmRoot.SetActive(visible);
            if (restartButton != null) restartButton.gameObject.SetActive(!visible);
        }

        private void SetInteractable(bool value)
        {
            if (restartButton != null) restartButton.interactable = value;
            if (confirmButton != null) confirmButton.interactable = value;
            if (cancelButton  != null) cancelButton.interactable  = value;
        }

        // ── 공용 ─────────────────────────────────────────

        private static Button CreateButton(Transform parent, string name, string label, Color color,
                                           Vector2 size, int fontSize)
        {
            GameObject go = CreateImage(parent, name, color);
            ((RectTransform)go.transform).sizeDelta = size;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();

            Text text = CreateText(go.transform, "Label", label, fontSize, Color.white);
            Stretch((RectTransform)text.transform);

            return button;
        }

        private static GameObject CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;

            return go;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;   // 버튼 클릭을 라벨이 가로채지 않도록

            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
