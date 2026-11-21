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
    /// <b>정지는 <c>Time.timeScale = 0</c>이다.</b> 전투의 시간 정지(컷인 등)는 UI 애니메이션이 살아 있어야 해서
    /// <see cref="TimeControl"/>만 쓰지만(결정 로그 ⑥), 일시정지는 화면 전체가 멈추는 게 맞다.
    /// UI 클릭 · 입력은 timeScale 과 상관없이 돈다.
    ///
    /// 남의 창(레벨업 · 덱 편집 · 마을 창 · 조작키)이 떠 있거나 방금 닫힌 프레임에는 열리지 않는다 —
    /// 그 ESC는 그 창의 것이다.
    ///
    /// <b>[처음부터]</b>(런 전체 재시작)는 이 창 안에만 있다 — 실수로 누를 자리(화면 모서리)에
    /// 런을 지우는 버튼을 두지 않는다. 누르면 확인 창이 한 번 더 묻는다. 전투만 넘긴다(<c>onRestart</c>).
    /// 골격은 <see cref="UiKit.Modal"/>이다(기획서 5.3).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private static readonly Color LabelColor  = UiTheme.TextMain;
        private static readonly Color TrackColor  = UiTheme.InkDeep;
        private static readonly Color FillColor   = UiTheme.Gold;

        /// <summary>창 크기. 본문(음량 세 줄 + 옵션 두 줄)과 버튼 줄 셋이 들어간다.</summary>
        public static readonly Vector2 PanelSize = new Vector2(680f, 560f);
        public static readonly Vector2 ConfirmSize = new Vector2(600f, 300f);

        /// <summary>버튼 줄 · 옵션 버튼 하나의 크기.</summary>
        public static readonly Vector2 ActionSize = new Vector2(180f, UiTheme.ButtonHeight.Medium);
        public static readonly Vector2 OptionSize = new Vector2(320f, UiTheme.ButtonHeight.Small + 8f);

        public const string RestartLabel = "처음부터";

        private static PauseMenu current;

        /// <summary>이 씬의 일시정지 창. 없으면 null. 런 바의 [ESC] 설정이 연다.</summary>
        public static PauseMenu Current => current;

        private string exitLabel;
        private Action onExit;
        private Action onRestart;

        private GameObject root;
        private Slider master, bgm, sfx;
        private Button rebindButton, shakeButton, restartButton;
        private Button first;
        private RebindUI rebind;
        private Button hint;

        /// <summary>[처음부터] 확인 창. 이 창의 바탕막 위에 한 겹 더 뜬다.</summary>
        private GameObject confirm;
        private Button confirmCancel;

        private bool isOpen;
        private bool wasSuspended;

        public bool IsOpen => isOpen;

        /// <summary>[처음부터] 확인 창이 떠 있나. 테스트가 읽는다.</summary>
        public bool IsConfirming => confirm != null && confirm.activeSelf;

        /// <summary>우측 상단 [ESC 일시정지] 안내 버튼. 안 지었으면 null. 테스트가 읽는다.</summary>
        public Button Hint => hint;

        /// <summary>
        /// 짓는다. 창은 처음 열 때 그린다. 씬과 함께 언로드되도록 <paramref name="owner"/>의 자식이 된다.
        /// </summary>
        /// <param name="showHint">
        /// 우측 상단에 [ESC 일시정지] 버튼을 띄울지. <b>지금은 아무 씬도 안 켠다</b> — 전투는 런 바(<see cref="RunBarUI"/>)의
        /// [ESC] 설정이 이 자리를 흡수했고, 마을은 그 자리에 재화가 있다. 런 바 없는 씬을 위해 남겨 둔다.
        /// </param>
        /// <param name="onRestart">
        /// 주면 [처음부터] 버튼이 생기고, 확인을 거쳐 이걸 부른다. 전투만 넘긴다 — 마을엔 지울 런이 없다.
        /// </param>
        public static PauseMenu Create(MonoBehaviour owner, string exitLabel, Action onExit,
                                       bool showHint = false, Action onRestart = null)
            => Create(owner.transform, exitLabel, onExit, showHint, onRestart);

        /// <inheritdoc cref="Create(MonoBehaviour, string, Action, bool, Action)"/>
        public static PauseMenu Create(Transform owner, string exitLabel, Action onExit,
                                       bool showHint = false, Action onRestart = null)
        {
            var go = new GameObject("PauseMenu", typeof(RectTransform));
            go.transform.SetParent(owner, false);

            PauseMenu menu = go.AddComponent<PauseMenu>();
            menu.exitLabel = exitLabel;
            menu.onExit = onExit;
            menu.onRestart = onRestart;
            current = menu;   // Awake 도 같은 일을 하지만, 에디트 모드(테스트)에선 Awake 가 안 돈다

            // 안내 버튼은 창과 달리 처음부터 보여야 하므로 지금 짓는다.
            if (showHint) menu.BuildHint();
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
                    // 확인 창이 떠 있으면 ESC는 "취소"다 — 일시정지 창으로 한 겹만 물러난다.
                    if (IsConfirming) HideConfirm();
                    else Close();
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
            SetHintVisible(false);
            NodeWindows.Focus(first);
        }

        public void Close()
        {
            if (!isOpen) return;

            isOpen = false;
            if (rebind != null && rebind.IsOpen) rebind.Close();
            if (confirm != null) confirm.SetActive(false);

            root.SetActive(false);
            SetHintVisible(true);
            Time.timeScale = 1f;
            PlayerInputController.SetSuspended(false);
        }

        /// <summary>창이 떠 있는 동안은 안내를 숨긴다 — 어두운 막 너머로 비쳐 보이면 군더더기다.</summary>
        private void SetHintVisible(bool visible)
        {
            if (hint != null) hint.transform.parent.gameObject.SetActive(visible);
        }

        private void Exit()
        {
            Close();
            onExit?.Invoke();
        }

        // ── [처음부터] 확인 ──────────────────────────────

        private void AskRestart()
        {
            if (confirm == null) return;
            confirm.SetActive(true);
            NodeWindows.Focus(confirmCancel);   // 기본 포커스는 물러나는 쪽 — Enter 연타로 런이 날아가지 않게
        }

        private void HideConfirm()
        {
            if (confirm == null) return;
            confirm.SetActive(false);
            NodeWindows.Focus(restartButton);
        }

        private void ConfirmRestart()
        {
            // 창을 닫아 timeScale · 조작 잠금을 먼저 풀고 넘긴다 — 재시작은 씬 전환이라 멈춘 채면 페이드가 안 돈다.
            Close();
            onRestart?.Invoke();
        }

        /// <summary>다른 씬의 일시정지 창에서 바꿨을 수 있으므로 열 때마다 맞춘다.</summary>
        private void Sync()
        {
            AudioManager audio = AudioManager.Instance;
            bool has = audio != null;

            foreach (Slider s in new[] { master, bgm, sfx }) s.interactable = has;
            if (!has) return;

            Bind(master, audio.MasterVolume, audio.SetMasterVolume);
            Bind(bgm, audio.BgmVolume, audio.SetBgmVolume);
            Bind(sfx, audio.SfxVolume, audio.SetSfxVolume);
        }

        /// <summary>슬라이더를 현재 값에 맞추고 움직이면 그대로 밀어 넣는다.</summary>
        private static void Bind(Slider slider, float value, UnityEngine.Events.UnityAction<float> apply)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(apply);
        }

        // ── 짓기 ────────────────────────────────────────

        private void Build()
        {
            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            UiKit.BuildCanvas(root, UiLayer.Pause);
            UiKit.EnsureEventSystem();

            UiKit.ModalParts m = UiKit.Modal(root.transform, "일시정지", PanelSize);
            RectTransform body = m.Body;

            // 본문 — 음량 세 줄, 그 밑에 옵션 두 개(화면 흔들림 · 조작키)를 나란히.
            master = VolumeRow(body, "전체 음량", -28f);
            bgm    = VolumeRow(body, "배경음", -84f);
            sfx    = VolumeRow(body, "효과음", -140f);

            float optY = -224f, optX = OptionSize.x / 2f + UiTheme.Space.S8;
            shakeButton  = OptionButton(body, ShakeLabel(), new Vector2(-optX, optY), ToggleShake);
            rebindButton = OptionButton(body, "조작키", new Vector2(optX, optY), OpenRebind);

            // 버튼 줄 — 권하는 쪽(계속)이 왼쪽, 되돌릴 수 없는 쪽이 가운데, 나가기가 오른쪽.
            Button resume = ActionButton(m.Buttons, "계속", ButtonKind.Primary, Close);
            if (onRestart != null)
                restartButton = ActionButton(m.Buttons, RestartLabel, ButtonKind.Danger, AskRestart);
            ActionButton(m.Buttons, exitLabel, ButtonKind.Normal, Exit);

            if (onRestart != null) BuildConfirm();

            first = resume;
            root.SetActive(false);
        }

        /// <summary>
        /// [처음부터] 확인 창. 일시정지 창 위에 바탕막째 한 겹 더 얹는다 — 뒤의 버튼이 눌리지 않게.
        /// </summary>
        private void BuildConfirm()
        {
            UiKit.ModalParts c = UiKit.Modal(root.transform, "런을 처음부터 다시 시작할까?", ConfirmSize);
            confirm = c.Backdrop.gameObject;
            c.Title.fontSize = UiTheme.Size.Heading;

            Text note = UiKit.NewText(c.Body, "Note", UiTheme.Size.Body, UiTheme.TextSub, FontStyle.Normal);
            note.text = "새 지도로 다시 시작하며 " + UiText.Kw("진행 상황은 모두 사라진다") + ".";
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Stretch(note.rectTransform);

            ActionButton(c.Buttons, "재시작", ButtonKind.Danger, ConfirmRestart);
            confirmCancel = ActionButton(c.Buttons, "취소", ButtonKind.Normal, HideConfirm);

            confirm.SetActive(false);
        }

        // ── 우측 상단 안내 ───────────────────────────────

        private static readonly Color HintPlateColor = UiTheme.Ink.WithAlpha(0.78f);
        private static readonly Color KeycapColor    = UiTheme.TextMain;
        private static readonly Color KeycapText     = UiTheme.TextOnLight;

        /// <summary>안내 버튼 크기와 화면 모서리에서의 거리(캔버스 단위).</summary>
        public static readonly Vector2 HintSize = new Vector2(176f, 46f);
        public static readonly Vector2 HintMargin = new Vector2(28f, 24f);

        /// <summary>
        /// 우측 상단 [ESC | 일시정지] 버튼. 키를 알려 주는 안내이자, 마우스로 눌러도 창이 열린다.
        ///
        /// 자기 캔버스를 따로 쓴다 — 창 캔버스(<see cref="UiLayer.Pause"/>)는 닫힐 때 통째로 꺼지고,
        /// 이건 반대로 닫혀 있을 때만 보여야 한다. 레이캐스터를 붙이는 이유는 클릭을 받아야 해서고,
        /// 그 덕에 이 버튼 위 클릭이 평타로 새지 않는다(평타는 UI 위 클릭을 거른다).
        /// </summary>
        private void BuildHint()
        {
            var canvasGo = new GameObject("PauseHintCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            // 이벤트 시스템은 여기서 만들지 않는다 — 전투 씬은 BulletTimeGaugeUI.Start 가 보장하고,
            // 여기서 만들면 에디트모드 테스트가 씬에 이벤트 시스템을 흘리고 간다.
            UiKit.BuildCanvas(canvasGo, UiLayer.PauseHint, match: 1f, raycaster: true);

            hint = UiKit.CreateButton(canvasGo.transform, "PauseHint", "", HintPlateColor, HintSize, 20);
            UiKit.Place(hint, new Vector2(1f, 1f), new Vector2(-HintMargin.x, -HintMargin.y), HintSize);

            // 키보드 · 패드 포커스가 이 버튼으로 새지 않게 한다. 마우스 전용이다.
            hint.navigation = new Navigation { mode = Navigation.Mode.None };
            hint.onClick.AddListener(Open);

            // CreateButton 이 가운데에 라벨을 깔아 준다. 키캡 + 글자로 다시 나눈다.
            Text label = hint.GetComponentInChildren<Text>();
            label.text = "일시정지";
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            var lr = label.rectTransform;
            lr.offsetMin = new Vector2(74f, 0f);
            lr.offsetMax = new Vector2(-10f, 0f);

            Image keycap = UiKit.NewImage(hint.transform, "Keycap", KeycapColor);
            UiKit.Place(keycap, new Vector2(0f, 0.5f), new Vector2(9f, 0f), new Vector2(56f, 30f));

            Text key = UiKit.NewText(keycap.transform, "Key", 17, KeycapText, FontStyle.Bold);
            key.text = "ESC";
            UiKit.Stretch(key.rectTransform);
        }

        private static string ShakeLabel() => CameraFollow.ShakeEnabled ? "화면 흔들림: 켜짐" : "화면 흔들림: 꺼짐";

        private void ToggleShake()
        {
            CameraFollow.ShakeEnabled = !CameraFollow.ShakeEnabled;
            shakeButton.GetComponentInChildren<Text>().text = ShakeLabel();
        }

        private void OpenRebind()
        {
            if (rebind != null) rebind.Open();
        }

        /// <summary>버튼 줄의 버튼 하나. 줄(가로 레이아웃)이 자리를 잡는다.</summary>
        private static Button ActionButton(RectTransform row, string label, ButtonKind kind, Action onClick)
        {
            Button b = UiKit.NewButton(row, "Btn_" + label, label, kind, ActionSize);
            b.onClick.AddListener(() => onClick());
            return b;
        }

        /// <summary>본문 안의 옵션 버튼(토글 · 하위 창 열기). 본문 위쪽 가운데 기준.</summary>
        private static Button OptionButton(RectTransform body, string label, Vector2 pos, Action onClick)
        {
            Button b = UiKit.NewButton(body, "Btn_" + label, label, ButtonKind.Ghost, OptionSize);
            UiKit.Place(b, new Vector2(0.5f, 1f), pos, OptionSize);
            b.onClick.AddListener(() => onClick());
            return b;
        }

        /// <summary>이름 + 슬라이더 한 줄. UiKit 에 슬라이더가 없어 여기서 짓는다 — 쓰는 곳이 여기뿐이다.</summary>
        private static Slider VolumeRow(RectTransform panel, string label, float y)
        {
            Text t = UiKit.NewText(panel, "Label_" + label, UiTheme.Size.Body, LabelColor, FontStyle.Normal);
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
            Image handle = UiKit.NewImage(handleArea, "Handle", UiTheme.TextMain, raycast: true);
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
