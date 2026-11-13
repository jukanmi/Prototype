using System;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 일시정지 창 — 음량 세 줄 · [조작키] · [계속] · [나가기]. ESC로 열고 닫는다.
    /// 전투 씬과 마을이 <see cref="Create"/>로 하나씩 짓고, 나가기 버튼의 이름과 할 일만 다르다
    /// (전투는 [마을로], 마을은 [타이틀로]).
    ///
    /// <b>정지는 <c>Time.timeScale = 0</c>이다.</b> 불릿타임은 손패 조작 · UI 애니메이션이 살아 있어야 해서
    /// <see cref="TimeControl"/>만 쓰지만(결정 로그 ⑥), 일시정지는 화면 전체가 멈추는 게 맞다.
    /// UI 클릭 · 입력은 timeScale 과 상관없이 돈다.
    ///
    /// 남의 창(레벨업 · 덱 편집 · 마을 창 · 조작키)이 떠 있거나 방금 닫힌 프레임에는 열리지 않는다 —
    /// 그 ESC는 그 창의 것이다.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private static readonly Color PanelColor  = new Color(0.10f, 0.11f, 0.14f, 0.97f);
        private static readonly Color DimColor    = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color LabelColor  = new Color(0.85f, 0.87f, 0.90f);
        private static readonly Color TrackColor  = new Color(0.20f, 0.22f, 0.26f);
        private static readonly Color FillColor   = new Color(1f, 0.86f, 0.45f);

        private static PauseMenu current;

        private string exitLabel;
        private Action onExit;

        private GameObject root;
        private Slider master, bgm, sfx;
        private Button rebindButton;
        private Button first;
        private RebindUI rebind;

        private bool isOpen;
        private bool wasSuspended;

        public bool IsOpen => isOpen;

        /// <summary>짓는다. 화면은 처음 열 때 그린다. 씬과 함께 언로드되도록 <paramref name="owner"/>의 자식이 된다.</summary>
        public static PauseMenu Create(MonoBehaviour owner, string exitLabel, Action onExit)
        {
            var go = new GameObject("PauseMenu", typeof(RectTransform));
            go.transform.SetParent(owner.transform, false);

            PauseMenu menu = go.AddComponent<PauseMenu>();
            menu.exitLabel = exitLabel;
            menu.onExit = onExit;
            return menu;
        }

        private void Awake()
        {
            current = this;
            GameplayModal.Register(AnyOpen);
        }

        /// <summary>정적 함수로 등록한다 — 인스턴스 람다를 넣으면 씬이 바뀔 때마다 목록에 쌓인다.</summary>
        private static bool AnyOpen() => current != null && current.isOpen;

        private void OnDestroy()
        {
            // 열린 채로 씬이 내려가면 다음 씬이 멈춘 채, 조작이 잠긴 채로 시작한다.
            if (isOpen)
            {
                Time.timeScale = 1f;
                PlayerInputController.SetSuspended(false);
            }

            if (current == this) current = null;
        }

        private void Update()
        {
            PlayerInputController input = PlayerInputController.Instance;
            bool cancel = input != null && input.CancelPressed && !GameplayModal.CancelConsumedThisFrame;

            if (isOpen)
            {
                // 조작키 창이 떠 있으면 ESC는 그쪽 것이다. 그 창이 닫히면서 잠금을 풀어 버리므로 다시 건다.
                if (rebind != null && rebind.IsOpen) return;
                PlayerInputController.SetSuspended(true);

                if (cancel)
                {
                    GameplayModal.ConsumeCancel();
                    Close();
                }
                return;
            }

            bool suspended = input == null || input.GameplaySuspended;
            bool blocked = suspended || wasSuspended || GameplayModal.IsOpen;
            wasSuspended = suspended;

            if (!blocked && cancel)
            {
                GameplayModal.ConsumeCancel();
                Open();
            }
        }

        // ── 열고 닫기 ────────────────────────────────────

        public void Open()
        {
            if (isOpen) return;
            if (root == null) Build();

            isOpen = true;
            wasSuspended = true;   // 닫히는 프레임의 ESC로 바로 다시 열리지 않게
            Time.timeScale = 0f;
            PlayerInputController.SetSuspended(true);

            // 조작키 창은 씬 배선(BattleInput 프리팹)이다. 열 때마다 찾는다 — 없는 씬이면 버튼을 숨긴다.
            rebind = FindAnyObjectByType<RebindUI>();
            rebindButton.gameObject.SetActive(rebind != null);

            Sync();
            root.SetActive(true);
            NodeWindows.Focus(first);
        }

        public void Close()
        {
            if (!isOpen) return;

            isOpen = false;
            if (rebind != null && rebind.IsOpen) rebind.Close();

            root.SetActive(false);
            Time.timeScale = 1f;
            PlayerInputController.SetSuspended(false);
        }

        private void Exit()
        {
            Close();
            onExit?.Invoke();
        }

        /// <summary>다른 곳(설정 패널 · 다른 씬)에서 바꿨을 수 있으므로 열 때마다 맞춘다.</summary>
        private void Sync()
        {
            AudioManager audio = AudioManager.Instance;
            bool has = audio != null;

            foreach (Slider s in new[] { master, bgm, sfx }) s.interactable = has;
            if (!has) return;

            SettingsPanel.Bind(master, audio.MasterVolume, audio.SetMasterVolume);
            SettingsPanel.Bind(bgm, audio.BgmVolume, audio.SetBgmVolume);
            SettingsPanel.Bind(sfx, audio.SfxVolume, audio.SetSfxVolume);
        }

        // ── 짓기 ────────────────────────────────────────

        private void Build()
        {
            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            UiKit.BuildCanvas(root, UiLayer.Pause);
            UiKit.EnsureEventSystem();

            Image dim = UiKit.NewImage(root.transform, "Dim", DimColor, raycast: true);
            UiKit.Stretch(dim.rectTransform);

            var size = new Vector2(620f, 560f);
            Image panel = UiKit.NewImage(dim.transform, "Panel", PanelColor);
            RectTransform p = UiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, size);

            Text title = UiKit.NewText(p, "Title", 40, NodeWindows.TitleColor, FontStyle.Bold);
            title.text = "일시정지";
            title.alignment = TextAnchor.MiddleCenter;
            UiKit.Place(title, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(size.x, 60f));

            master = VolumeRow(p, "전체 음량", -130f);
            bgm    = VolumeRow(p, "배경음", -195f);
            sfx    = VolumeRow(p, "효과음", -260f);

            Button resume = MenuButton(p, "계속", NodeWindows.ButtonColor, -340f, Close);
            rebindButton  = MenuButton(p, "조작키", NodeWindows.ButtonColor, -410f, OpenRebind);
            MenuButton(p, exitLabel, NodeWindows.ExitColor, -480f, Exit);

            first = resume;
            root.SetActive(false);
        }

        private void OpenRebind()
        {
            if (rebind != null) rebind.Open();
        }

        private static Button MenuButton(RectTransform panel, string label, Color color, float y, Action onClick)
        {
            var size = new Vector2(300f, 54f);
            Button b = UiKit.CreateButton(panel, "Btn_" + label, label, color, size, 24);
            UiKit.Place(b, new Vector2(0.5f, 1f), new Vector2(0f, y), size);
            b.onClick.AddListener(() => onClick());
            return b;
        }

        /// <summary>이름 + 슬라이더 한 줄. UiKit 에 슬라이더가 없어 여기서 짓는다 — 쓰는 곳이 여기뿐이다.</summary>
        private static Slider VolumeRow(RectTransform panel, string label, float y)
        {
            Text t = UiKit.NewText(panel, "Label_" + label, 24, LabelColor, FontStyle.Normal);
            t.text = label;
            t.alignment = TextAnchor.MiddleLeft;
            UiKit.Place(t, new Vector2(0.5f, 1f), new Vector2(-170f, y), new Vector2(180f, 44f));

            Image track = UiKit.NewImage(panel, "Slider_" + label, TrackColor, raycast: true);
            UiKit.Place(track, new Vector2(0.5f, 1f), new Vector2(90f, y), new Vector2(320f, 18f));

            RectTransform fillArea = UiKit.NewRect(track.transform, "FillArea");
            UiKit.Stretch(fillArea);
            Image fill = UiKit.NewImage(fillArea, "Fill", FillColor);
            fill.rectTransform.sizeDelta = Vector2.zero;

            RectTransform handleArea = UiKit.NewRect(track.transform, "HandleArea");
            UiKit.Stretch(handleArea);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            Image handle = UiKit.NewImage(handleArea, "Handle", Color.white, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(20f, 8f);   // 세로는 슬라이더가 늘린다 — 여기 값은 트랙 위아래로 삐져나오는 양

            Slider s = track.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight;
            return s;
        }
    }
}
