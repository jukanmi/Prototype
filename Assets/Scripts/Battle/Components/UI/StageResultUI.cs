using System;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 스테이지가 끝난 뒤의 화면. 세 가지 얼굴을 가진다.
    ///
    /// <list type="bullet">
    /// <item><b>출구 화살표</b> — 이겼고 다음 스테이지가 있다. 오른쪽 벽으로 가라는 안내만 띄우고
    /// 조작을 막지 않는다.</item>
    /// <item><b>전부 클리어</b> — 마지막 스테이지를 이겼다. 갈 곳이 없으니 마을로 버튼을 준다.</item>
    /// <item><b>패배</b> — 재시작과 마을로 버튼.</item>
    /// </list>
    ///
    /// 캔버스를 코드로 만들므로 씬·프리팹 배선이 필요 없다.
    /// <see cref="BattleSceneController"/>의 자식으로 붙어 씬과 함께 언로드된다.
    ///
    /// 전부 클리어 · 패배 창의 골격은 <see cref="UiKit.Modal"/>이다(기획서 5.3) — 제목은 <see cref="UiTheme.Size.Display"/>.
    /// </summary>
    public class StageResultUI : MonoBehaviour
    {
        public static readonly Vector2 PanelSize = new Vector2(620f, 300f);
        public static readonly Vector2 ButtonSize = new Vector2(240f, UiTheme.ButtonHeight.Medium);

        private static readonly Color ArrowColor   = UiTheme.Gold;
        private static readonly Color DefeatColor  = UiTheme.Hp;
        private static readonly Color VictoryColor = UiTheme.Gold;
        private static readonly Color NoteColor    = UiTheme.TextSub;

        // ── 화살표 모양 ──
        private const float ArrowShaftWidth = 96f;
        private const float ArrowThickness = 16f;
        private const float ArrowHeadLength = 62f;

        /// <summary>화살표가 좌우로 흔들리는 폭. 가만히 있으면 배경 장식으로 읽힌다.</summary>
        private const float BobDistance = 18f;
        private const float BobPeriod = 1.1f;

        private RectTransform arrowRoot;
        private float arrowHomeX;

        private Action onRetry;
        private Action onMenu;

        /// <summary>배틀 씬에 결과 UI를 붙인다. 씬과 함께 언로드되도록 컨트롤러의 자식으로 둔다.</summary>
        public static StageResultUI Create(Component owner)
        {
            if (owner == null) return null;

            var go = new GameObject("StageResultUI", typeof(RectTransform));
            go.transform.SetParent(owner.transform, false);

            return go.AddComponent<StageResultUI>();
        }

        private void Awake()
        {
            UiKit.EnsureEventSystem();
            UiKit.BuildCanvas(gameObject, UiLayer.StageResult);
        }

        private void Update()
        {
            if (arrowRoot == null) return;

            // 시간 정지(TimeControl.Scale = 0)가 남아 있어도 안내는 움직여야 한다.
            float t = Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / BobPeriod));
            arrowRoot.anchoredPosition = new Vector2(arrowHomeX + t * BobDistance,
                                                     arrowRoot.anchoredPosition.y);
        }

        // ── 승리: 출구 안내 ──────────────────────────────

        /// <summary>
        /// 오른쪽 벽으로 가라는 안내. <b>화면을 막지 않는다</b> —
        /// 여기서 Dim 을 깔면 걸어갈 수가 없다.
        /// </summary>
        public void ShowExitArrow(string nextStageLabel, string title = "스테이지 클리어")
        {
            HideExitArrow();   // 다시 띄우면 둘이 겹친다

            var root = new GameObject("ExitGuide", typeof(RectTransform));
            root.transform.SetParent(transform, false);

            arrowRoot = (RectTransform)root.transform;
            arrowRoot.anchorMin = arrowRoot.anchorMax = arrowRoot.pivot = new Vector2(1f, 0.5f);
            arrowRoot.sizeDelta = new Vector2(260f, 220f);
            arrowHomeX = -130f;
            arrowRoot.anchoredPosition = new Vector2(arrowHomeX, 0f);

            BuildArrow(arrowRoot);

            if (!string.IsNullOrEmpty(title))
            {
                Text titleText = UiKit.CreateText(arrowRoot, "Title", title, UiTheme.Size.Heading, VictoryColor);
                UiKit.Place(titleText, new Vector2(0.5f, 0.5f), new Vector2(0f, 78f), new Vector2(260f, 40f));
            }

            Text guide = UiKit.CreateText(arrowRoot, "Guide", "오른쪽 벽으로 이동", UiTheme.Size.Body, UiTheme.TextMain);
            UiKit.Place(guide, new Vector2(0.5f, 0.5f), new Vector2(0f, -72f), new Vector2(260f, 32f));

            if (!string.IsNullOrEmpty(nextStageLabel))
            {
                Text next = UiKit.CreateText(arrowRoot, "Next", nextStageLabel, UiTheme.Size.Caption, NoteColor);
                UiKit.Place(next, new Vector2(0.5f, 0.5f), new Vector2(0f, -102f), new Vector2(260f, 28f));
            }
        }

        /// <summary>출구 안내를 걷는다. 비전투 칸에서 싸움이 벌어지면 이길 때까지 출구가 닫힌다.</summary>
        public void HideExitArrow()
        {
            if (arrowRoot != null) Destroy(arrowRoot.gameObject);
            arrowRoot = null;
        }

        /// <summary>
        /// 화살표를 글자로 찍지 않고 사각형 셋으로 짓는다.
        /// 글꼴에 화살표 글리프가 있다는 보장이 없어서, 없으면 두부(□)가 뜬다.
        /// </summary>
        private static void BuildArrow(RectTransform parent)
        {
            GameObject shaft = UiKit.CreateImage(parent, "Shaft", ArrowColor);
            UiKit.Place(shaft.transform, new Vector2(0.5f, 0.5f), new Vector2(-14f, 0f),
                           new Vector2(ArrowShaftWidth, ArrowThickness));

            // 촉은 45도로 눕힌 막대 둘. 끝점이 축의 오른쪽 끝에 모이게 놓는다.
            float tipX = -14f + ArrowShaftWidth * 0.5f;
            float armOffset = ArrowHeadLength * 0.25f;

            BuildArrowArm(parent, "HeadUp", new Vector2(tipX - armOffset, armOffset), -45f);
            BuildArrowArm(parent, "HeadDown", new Vector2(tipX - armOffset, -armOffset), 45f);
        }

        private static void BuildArrowArm(RectTransform parent, string name, Vector2 position, float angle)
        {
            GameObject arm = UiKit.CreateImage(parent, name, ArrowColor);
            RectTransform rect = UiKit.Place(arm.transform, new Vector2(0.5f, 0.5f), position,
                                                new Vector2(ArrowHeadLength, ArrowThickness));
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ── 승리: 마지막 스테이지 ────────────────────────

        public void ShowAllClear(Action menu)
        {
            onMenu = menu;

            UiKit.ModalParts m = BuildPanel("모든 스테이지 클리어", VictoryColor, "보스를 쓰러뜨렸다.");

            Button menuButton = UiKit.NewButton(m.Buttons, "Btn_Menu", "마을로", ButtonKind.Primary, ButtonSize);
            menuButton.onClick.AddListener(HandleMenu);
            NodeWindows.Focus(menuButton);
        }

        // ── 패배 ────────────────────────────────────────

        public void ShowDefeat(Action retry, Action menu)
        {
            onRetry = retry;
            onMenu = menu;

            UiKit.ModalParts m = BuildPanel("패배", DefeatColor, "아군이 모두 쓰러졌다.");

            Button retryButton = UiKit.NewButton(m.Buttons, "Btn_Retry", "이 스테이지 재시작", ButtonKind.Danger, ButtonSize);
            retryButton.onClick.AddListener(HandleRetry);

            Button menuButton = UiKit.NewButton(m.Buttons, "Btn_Menu", "마을로", ButtonKind.Primary, ButtonSize);
            menuButton.onClick.AddListener(HandleMenu);
            NodeWindows.Focus(menuButton);
        }

        // ── 공용 ────────────────────────────────────────

        /// <summary>
        /// 공용 창 골격. 바탕막이 레이캐스트를 먹으므로, 창이 떠 있는 동안 아래 UI 클릭이 새지 않는다.
        /// 제목은 결과의 색(승리 금 · 패배 빨강)을 띠고 한 단계 크게 — 이 화면만의 큰 글씨다.
        /// </summary>
        private UiKit.ModalParts BuildPanel(string title, Color titleColor, string note)
        {
            UiKit.ModalParts m = UiKit.Modal(transform, title, PanelSize);
            m.Title.color = titleColor;
            m.Title.fontSize = UiTheme.Size.Display;

            Text noteText = UiKit.NewText(m.Body, "Note", UiTheme.Size.Body, NoteColor, FontStyle.Normal);
            noteText.text = note;
            UiKit.Stretch(noteText.rectTransform);

            return m;
        }

        /// <summary>
        /// 전환이 시작되면 버튼을 다시 누를 수 없어야 한다. 씬은 곧 통째로 내려가지만
        /// 페이드아웃이 도는 동안에도 클릭은 계속 들어온다.
        /// </summary>
        private void LockButtons()
        {
            foreach (Button b in GetComponentsInChildren<Button>(true))
                b.interactable = false;
        }

        private void HandleRetry()
        {
            LockButtons();
            onRetry?.Invoke();
        }

        private void HandleMenu()
        {
            LockButtons();
            onMenu?.Invoke();
        }
    }
}
