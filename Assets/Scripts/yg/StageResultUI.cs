using System;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.YG
{
    /// <summary>
    /// 스테이지가 끝난 뒤의 화면. 세 가지 얼굴을 가진다.
    ///
    /// <list type="bullet">
    /// <item><b>출구 화살표</b> — 이겼고 다음 스테이지가 있다. 오른쪽 벽으로 가라는 안내만 띄우고
    /// 조작을 막지 않는다.</item>
    /// <item><b>전부 클리어</b> — 마지막 스테이지를 이겼다. 갈 곳이 없으니 메인화면 버튼을 준다.</item>
    /// <item><b>패배</b> — 재시작과 메인화면 버튼.</item>
    /// </list>
    ///
    /// 캔버스를 코드로 만들므로 씬·프리팹 배선이 필요 없다.
    /// <see cref="BattleSceneController"/>의 자식으로 붙어 씬과 함께 언로드된다.
    /// </summary>
    public class StageResultUI : MonoBehaviour
    {
        /// <summary>BattleRestartUI(100)보다 위, SceneLoader 의 FadeCanvas(999)보다 아래.</summary>
        private const int SortingOrder = 200;

        private static readonly Color DimColor     = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color PanelColor   = new Color(0.10f, 0.10f, 0.12f, 0.97f);
        private static readonly Color RetryColor   = new Color(0.62f, 0.42f, 0.16f);
        private static readonly Color MenuColor    = new Color(0.25f, 0.27f, 0.32f);
        private static readonly Color ArrowColor   = new Color(1f, 0.85f, 0.35f);
        private static readonly Color DefeatColor  = new Color(0.85f, 0.35f, 0.35f);
        private static readonly Color VictoryColor = new Color(0.95f, 0.85f, 0.45f);
        private static readonly Color NoteColor    = new Color(0.75f, 0.75f, 0.78f);

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
        public static StageResultUI Create(BattleSceneController owner)
        {
            if (owner == null) return null;

            var go = new GameObject("StageResultUI", typeof(RectTransform));
            go.transform.SetParent(owner.transform, false);

            return go.AddComponent<StageResultUI>();
        }

        private void Awake()
        {
            SimpleUI.EnsureEventSystem();
            SimpleUI.BuildCanvas(gameObject, SortingOrder);
        }

        private void Update()
        {
            if (arrowRoot == null) return;

            // 불릿타임(TimeControl.Scale = 0)이 남아 있어도 안내는 움직여야 한다.
            float t = Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / BobPeriod));
            arrowRoot.anchoredPosition = new Vector2(arrowHomeX + t * BobDistance,
                                                     arrowRoot.anchoredPosition.y);
        }

        // ── 승리: 출구 안내 ──────────────────────────────

        /// <summary>
        /// 오른쪽 벽으로 가라는 안내. <b>화면을 막지 않는다</b> —
        /// 여기서 Dim 을 깔면 걸어갈 수가 없다.
        /// </summary>
        public void ShowExitArrow(string nextStageLabel)
        {
            var root = new GameObject("ExitGuide", typeof(RectTransform));
            root.transform.SetParent(transform, false);

            arrowRoot = (RectTransform)root.transform;
            arrowRoot.anchorMin = arrowRoot.anchorMax = arrowRoot.pivot = new Vector2(1f, 0.5f);
            arrowRoot.sizeDelta = new Vector2(260f, 220f);
            arrowHomeX = -130f;
            arrowRoot.anchoredPosition = new Vector2(arrowHomeX, 0f);

            BuildArrow(arrowRoot);

            Text title = SimpleUI.CreateText(arrowRoot, "Title", "스테이지 클리어", 30, VictoryColor);
            SimpleUI.Place(title, new Vector2(0.5f, 0.5f), new Vector2(0f, 78f), new Vector2(260f, 40f));

            Text guide = SimpleUI.CreateText(arrowRoot, "Guide", "오른쪽 벽으로 이동", 22, Color.white);
            SimpleUI.Place(guide, new Vector2(0.5f, 0.5f), new Vector2(0f, -72f), new Vector2(260f, 32f));

            if (!string.IsNullOrEmpty(nextStageLabel))
            {
                Text next = SimpleUI.CreateText(arrowRoot, "Next", nextStageLabel, 17, NoteColor);
                SimpleUI.Place(next, new Vector2(0.5f, 0.5f), new Vector2(0f, -102f), new Vector2(260f, 28f));
            }
        }

        /// <summary>
        /// 화살표를 글자로 찍지 않고 사각형 셋으로 짓는다.
        /// LegacyRuntime 폰트에 화살표 글리프가 있다는 보장이 없어서, 없으면 두부(□)가 뜬다.
        /// </summary>
        private static void BuildArrow(RectTransform parent)
        {
            GameObject shaft = SimpleUI.CreateImage(parent, "Shaft", ArrowColor);
            SimpleUI.Place(shaft.transform, new Vector2(0.5f, 0.5f), new Vector2(-14f, 0f),
                           new Vector2(ArrowShaftWidth, ArrowThickness));

            // 촉은 45도로 눕힌 막대 둘. 끝점이 축의 오른쪽 끝에 모이게 놓는다.
            float tipX = -14f + ArrowShaftWidth * 0.5f;
            float armOffset = ArrowHeadLength * 0.25f;

            BuildArrowArm(parent, "HeadUp", new Vector2(tipX - armOffset, armOffset), -45f);
            BuildArrowArm(parent, "HeadDown", new Vector2(tipX - armOffset, -armOffset), 45f);
        }

        private static void BuildArrowArm(RectTransform parent, string name, Vector2 position, float angle)
        {
            GameObject arm = SimpleUI.CreateImage(parent, name, ArrowColor);
            RectTransform rect = SimpleUI.Place(arm.transform, new Vector2(0.5f, 0.5f), position,
                                                new Vector2(ArrowHeadLength, ArrowThickness));
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ── 승리: 마지막 스테이지 ────────────────────────

        public void ShowAllClear(Action menu)
        {
            onMenu = menu;

            Transform panel = BuildPanel("모든 스테이지 클리어", VictoryColor,
                                         "보스를 쓰러뜨렸다.", 200f);

            Button menuButton = SimpleUI.CreateButton(panel, "Btn_Menu", "메인화면", MenuColor,
                                                      new Vector2(200f, 56f), 22);
            SimpleUI.Place(menuButton, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(200f, 56f));
            menuButton.onClick.AddListener(HandleMenu);
        }

        // ── 패배 ────────────────────────────────────────

        public void ShowDefeat(Action retry, Action menu)
        {
            onRetry = retry;
            onMenu = menu;

            Transform panel = BuildPanel("패배", DefeatColor,
                                         "아군이 모두 쓰러졌다.", 240f);

            Button retryButton = SimpleUI.CreateButton(panel, "Btn_Retry", "이 스테이지 재시작", RetryColor,
                                                       new Vector2(230f, 56f), 21);
            SimpleUI.Place(retryButton, new Vector2(0.5f, 0f), new Vector2(-124f, 44f), new Vector2(230f, 56f));
            retryButton.onClick.AddListener(HandleRetry);

            Button menuButton = SimpleUI.CreateButton(panel, "Btn_Menu", "메인화면", MenuColor,
                                                      new Vector2(200f, 56f), 22);
            SimpleUI.Place(menuButton, new Vector2(0.5f, 0f), new Vector2(124f, 44f), new Vector2(200f, 56f));
            menuButton.onClick.AddListener(HandleMenu);
        }

        // ── 공용 ────────────────────────────────────────

        /// <summary>
        /// 뒤를 덮는 Dim 이 레이캐스트를 먹으므로, 패널이 떠 있는 동안 아래 UI 클릭이 새지 않는다.
        /// </summary>
        private Transform BuildPanel(string title, Color titleColor, string note, float height)
        {
            GameObject dim = SimpleUI.CreateImage(transform, "Dim", DimColor);
            SimpleUI.Stretch((RectTransform)dim.transform);

            GameObject panel = SimpleUI.CreateImage(dim.transform, "Panel", PanelColor);
            SimpleUI.Place(panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, height));

            Text titleText = SimpleUI.CreateText(panel.transform, "Title", title, 34, titleColor);
            SimpleUI.Place(titleText, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(520f, 52f));

            Text noteText = SimpleUI.CreateText(panel.transform, "Note", note, 18, NoteColor);
            SimpleUI.Place(noteText, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(520f, 30f));

            return panel.transform;
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
