using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 인게임 씬 우상단의 [처음부터] 버튼과 확인 패널. 캔버스를 코드로 만들므로
    /// 씬 · 프리팹 배선이 필요 없다 — <see cref="BattleSceneController"/> 가 Start 에서 붙인다.
    ///
    /// 초기화 자체는 여기서 하지 않는다. 확인 버튼은
    /// <see cref="BattleSceneController.RestartStage"/> 로만 넘긴다.
    ///
    /// "처음부터"는 <b>런 전체</b>를 뜻한다 — 첫 스테이지로 돌아간다.
    /// 진 스테이지만 다시 하는 것은 <see cref="StageResultUI"/> 의 [이 스테이지 재시작] 쪽이다.
    /// </summary>
    public class BattleRestartUI : MonoBehaviour
    {

        private const float ButtonWidth = 150f;
        private const float ButtonHeight = 52f;
        private const float ScreenMargin = 24f;

        private static readonly Color RestartColor = new Color(0.62f, 0.42f, 0.16f);
        private static readonly Color ConfirmColor = new Color(0.70f, 0.20f, 0.20f);
        private static readonly Color CancelColor  = new Color(0.25f, 0.27f, 0.32f);
        private static readonly Color PanelColor   = new Color(0.10f, 0.10f, 0.12f, 0.97f);
        private static readonly Color DimColor     = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color NoteColor    = new Color(0.75f, 0.75f, 0.78f);

        private BattleSceneController controller;

        private GameObject confirmRoot;
        private Button restartButton;
        private Button confirmButton;
        private Button cancelButton;

        /// <summary>결과 패널이 떠 있는 동안에는 통째로 숨는다.</summary>
        private bool hidden;

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
            UiKit.EnsureEventSystem();
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
        /// 승패가 갈리면 결과 패널이 화면을 덮는다. 그 위에 [처음부터] 버튼이 겹쳐 있으면
        /// 같은 화면에 재시작 버튼이 둘이 되어 어느 쪽이 무엇인지 읽히지 않는다.
        /// </summary>
        public void SetVisible(bool value)
        {
            hidden = !value;

            if (confirmRoot != null && hidden) confirmRoot.SetActive(false);
            if (restartButton != null) restartButton.gameObject.SetActive(value);
        }

        // ── UI 빌드 ──────────────────────────────────────

        private void BuildUI()
        {
            UiKit.BuildCanvas(gameObject, UiLayer.BattleRestart);

            BuildRestartButton();
            BuildConfirmPanel();
        }

        private void BuildRestartButton()
        {
            restartButton = UiKit.CreateButton(transform, "Btn_Restart", "처음부터", RestartColor,
                                                  new Vector2(ButtonWidth, ButtonHeight), 22);

            UiKit.Place(restartButton, new Vector2(1f, 1f),
                           new Vector2(-ScreenMargin, -ScreenMargin),
                           new Vector2(ButtonWidth, ButtonHeight));

            restartButton.onClick.AddListener(OnRestartClicked);
        }

        /// <summary>
        /// 확인 패널. 뒤를 덮는 Dim 이미지가 레이캐스트를 먹으므로 패널이 떠 있는 동안
        /// 카드 배치 UI 클릭이 새어 들어가지 않는다.
        /// </summary>
        private void BuildConfirmPanel()
        {
            confirmRoot = UiKit.CreateImage(transform, "ConfirmDim", DimColor);
            UiKit.Stretch((RectTransform)confirmRoot.transform);

            GameObject panel = UiKit.CreateImage(confirmRoot.transform, "Panel", PanelColor);
            UiKit.Place(panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 240f));

            Text title = UiKit.CreateText(panel.transform, "Title", "런을 처음부터 다시 시작할까?", 26, Color.white);
            UiKit.Place(title, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(520f, 60f));

            Text note = UiKit.CreateText(panel.transform, "Note",
                                            "첫 스테이지로 돌아가며 진행 상황은 모두 사라진다.", 17, NoteColor);
            UiKit.Place(note, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(520f, 30f));

            confirmButton = UiKit.CreateButton(panel.transform, "Btn_Confirm", "재시작", ConfirmColor,
                                                  new Vector2(180f, 56f), 22);
            UiKit.Place(confirmButton, new Vector2(0.5f, 0f), new Vector2(-100f, 52f), new Vector2(180f, 56f));
            confirmButton.onClick.AddListener(OnConfirmClicked);

            cancelButton = UiKit.CreateButton(panel.transform, "Btn_Cancel", "취소", CancelColor,
                                                 new Vector2(180f, 56f), 22);
            UiKit.Place(cancelButton, new Vector2(0.5f, 0f), new Vector2(100f, 52f), new Vector2(180f, 56f));
            cancelButton.onClick.AddListener(OnCancelClicked);
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
            if (hidden) return;

            if (confirmRoot != null) confirmRoot.SetActive(visible);
            if (restartButton != null) restartButton.gameObject.SetActive(!visible);
        }

        private void SetInteractable(bool value)
        {
            if (restartButton != null) restartButton.interactable = value;
            if (confirmButton != null) confirmButton.interactable = value;
            if (cancelButton  != null) cancelButton.interactable  = value;
        }
    }
}
