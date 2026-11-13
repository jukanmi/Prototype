using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 마을(Lobby 씬)의 흐름. 런과 런 사이에 머무는 곳이다 — 주인공이 걸어다니고,
    /// <see cref="LobbyInteractable"/> 근처에서 상호작용 키를 누르면 그 기능이 열린다.
    ///
    /// 씬은 <c>Stage_Training</c>을 복제해 몸 · 카메라 · 허수아비를 물려받았고
    /// <see cref="BattleSceneController"/>만 뺐다 — 승패 판정이 없는 방이다.
    ///
    /// 창(편성 · 제단 · 기록판 · 설정)이 떠 있는 동안 조작을 잠그고, ESC는 창을 닫는다.
    /// 창이 없을 때 ESC는 타이틀로 간다.
    /// 계획서: docs/Lobby_Plan.md
    /// </summary>
    public class LobbyController : MonoBehaviour
    {
        private const float NoticeSeconds = 4f;

        private static readonly Color HudColor = new Color(1f, 0.86f, 0.45f);

        private PartySelectUI party;
        private SettingsPanel settings;
        private GameObject window;

        private Text prompt;
        private Text currency;
        private Text notice;
        private Coroutine noticeRoutine;

        private InputAction interactAction;
        private bool leaving;
        private bool suspendedByMe;
        private bool wasSuspended;

        private string SceneName => gameObject.scene.name;

        /// <summary>
        /// 지난 런의 흔적을 지운다. <b>Awake여야 한다</b> — <c>PartyAssembler.Start</c>(실행 순서 -200)가
        /// 파티 상태 기록을 읽어 체력을 되돌리는데, 그 전에 비워야 마을의 몸이 만피 · 전원 생존으로 선다.
        /// 런은 이미 끝났으므로(정산 완료) 버려도 잃는 것이 없다.
        /// </summary>
        private void Awake()
        {
            RunProgression.Current.Reset();
        }

        private void Start()
        {
            UiKit.EnsureEventSystem();

            if (GameManager.Instance == null)
                Debug.LogWarning("[Lobby] GameManager 없음 — 씬 단독 실행이다. 출발 · 저장이 안 된다. Boot 에서 시작할 것.");

            party = PartySelectUI.Create(this);
            party.Hide();

            settings = FindAnyObjectByType<SettingsPanel>();

            BuildHud();

            MetaProgression meta = MetaProgression.Current;
            if (meta.LastSettlement >= 0)
            {
                ShowNotice($"기억 조각 +{meta.LastSettlement}");
                meta.ClearLastSettlement();
            }
        }

        private void OnDestroy()
        {
            // 창을 띄운 채 씬이 내려가면 조작이 잠긴 채로 다음 씬에 간다.
            if (suspendedByMe) PlayerInputController.SetSuspended(false);
        }

        private void Update()
        {
            if (leaving) return;

            PlayerInputController input = PlayerInputController.Instance;
            MetaProgression meta = MetaProgression.Current;
            currency.text = $"기억 조각 {meta.Currency}";

            bool ownOpen = window != null || party.IsOpen || (settings != null && settings.IsOpen);
            SetSuspended(ownOpen);

            if (ownOpen)
            {
                wasSuspended = true;
                prompt.text = "";
                if (input != null && input.CancelPressed && !GameplayModal.CancelConsumedThisFrame)
                {
                    CloseWindows();
                    GameplayModal.ConsumeCancel();
                }
                return;
            }

            // 남의 창(조작키 설정 · 레벨업)이 떠 있거나 방금 닫혔다. 그 ESC는 내 것이 아니다 —
            // 지난 프레임까지 잠겨 있었는지도 봐야 같은 ESC로 창이 닫히면서 타이틀까지 가지 않는다.
            bool suspended = input == null || input.GameplaySuspended;
            bool blocked = suspended || wasSuspended || GameplayModal.IsOpen;
            wasSuspended = suspended;

            if (blocked) { prompt.text = ""; return; }

            Entity body = BattleRegistry.Controlled;
            LobbyInteractable near = body != null ? LobbyInteractable.Nearest(body.transform.position) : null;
            prompt.text = near != null ? $"{KeyName()}  {near.Label}" : "";

            if (near != null && input.InteractPressed)
            {
                Activate(near.Station);
                return;
            }

            if (input.CancelPressed && !GameplayModal.CancelConsumedThisFrame)
                ToTitle();
        }

        // ── 오브젝트 ────────────────────────────────────

        private void Activate(LobbyStation station)
        {
            switch (station)
            {
                case LobbyStation.Depart:   Depart(); break;
                case LobbyStation.Party:    party.Show(); break;
                case LobbyStation.Settings: OpenSettings(); break;
                case LobbyStation.Altar:    OpenAltar(); break;
                case LobbyStation.Records:  OpenRecords(); break;
            }
        }

        /// <summary>
        /// 미궁 입구. MainMenu 의 옛 [시작]을 그대로 옮겼다 — 파티 검사 → 지도 굴림 → 지도 씬.
        /// </summary>
        private void Depart()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) { ShowNotice("Boot 씬에서 시작해야 출발할 수 있다"); return; }
            if (SceneLoader.Instance == null || SceneLoader.Instance.IsBusy) return;

            // 아무도 안 데려가는 조합이면 막는다. 덱이 0장이라 아무것도 안 나가고, 그건 고장으로 보인다.
            // Loadout 이 null 인 것은 안 고른 것이다 — StartNewRun 이 기본 조합으로 채운다.
            if (gm.Loadout != null && !PartyAssembleRules.CanStart(gm.Loadout.members))
            {
                ShowNotice("동료를 한 명 이상 골라야 출발할 수 있다");
                return;
            }

            // 지도를 못 굴리면 GameManager 가 이유를 에러로 남긴다.
            if (!gm.StartNewRun()) return;

            Leave(SceneNames.RunMap);
        }

        private void ToTitle()
        {
            if (SceneLoader.Instance == null || SceneLoader.Instance.IsBusy) return;
            Leave(SceneNames.MainMenu);
        }

        /// <summary>
        /// 씬을 떠난다. 허수아비를 때리면 전투 정적 상태가 차므로 전투 씬과 같은 정리를 <b>떠나기 전에</b> 한다
        /// (로드가 끝난 뒤에 비우면 새 씬의 등록까지 날아간다).
        /// </summary>
        private void Leave(string scene)
        {
            leaving = true;
            prompt.text = "";

            BattleSceneController.ClearSceneStatics();
            SceneLoader.Instance.SwapTo(scene, SceneName, () => AudioManager.Instance?.PlayMenuBgm());
        }

        private void OpenSettings()
        {
            if (settings == null)
            {
                ShowNotice("설정 창이 없다 — Boot 씬에서 시작할 것");
                return;
            }

            settings.Open();
        }

        private void CloseWindows()
        {
            if (window != null) Destroy(window);
            window = null;

            party.Hide();
            if (settings != null) settings.Close();
        }

        private void SetSuspended(bool value)
        {
            if (suspendedByMe == value) return;

            suspendedByMe = value;
            PlayerInputController.SetSuspended(value);
        }

        // ── 유산 제단 ───────────────────────────────────

        private void OpenAltar()
        {
            RectTransform panel = OpenWindow("유산 제단", new Vector2(760f, 560f));
            MetaProgression meta = MetaProgression.Current;

            Line(panel, "Currency", $"기억 조각 {meta.Currency}", -120f, 24, NodeWindows.SubColor);

            Button first = null;
            MetaUpgrade[] all = (MetaUpgrade[])System.Enum.GetValues(typeof(MetaUpgrade));

            for (int i = 0; i < all.Length; i++)
            {
                MetaUpgrade u = all[i];
                int level = meta.LevelOf(u);
                int cost = MetaRules.UpgradeCost(level);
                float y = -190f - i * 80f;

                string now = level > 0 ? MetaRules.Effect(u, level) : "없음";
                string next = cost > 0 ? $" → {MetaRules.Effect(u, level + 1)}" : "";
                Text label = Line(panel, "Row_" + u, $"{MetaRules.Label(u)}  {level}/{MetaRules.MaxUpgradeLevel}   {now}{next}",
                                  y, 22, NodeWindows.BodyColor);
                label.alignment = TextAnchor.MiddleLeft;
                UiKit.Place(label, new Vector2(0.5f, 1f), new Vector2(-110f, y), new Vector2(480f, 50f));

                bool can = cost > 0 && meta.Currency >= cost;
                string text = cost > 0 ? $"강화 {cost}" : "최대";
                Button b = UiKit.CreateButton(panel, "Btn_" + u, text,
                                              can ? NodeWindows.ButtonColor : NodeWindows.ExitColor,
                                              new Vector2(180f, 50f), 20);
                UiKit.Place(b, new Vector2(0.5f, 1f), new Vector2(250f, y), new Vector2(180f, 50f));
                b.interactable = can;

                MetaUpgrade captured = u;
                b.onClick.AddListener(() =>
                {
                    if (MetaProgression.Current.TryUpgrade(captured)) OpenAltar();   // 다시 그린다
                });

                if (can && first == null) first = b;
            }

            Button close = NodeWindows.BottomButton(panel, "닫기", NodeWindows.ExitColor, CloseWindows);
            NodeWindows.Focus(first != null ? first : close);
        }

        // ── 기록판 ──────────────────────────────────────

        private void OpenRecords()
        {
            RectTransform panel = OpenWindow("기록판", new Vector2(560f, 420f));
            MetaProgression meta = MetaProgression.Current;

            Line(panel, "Runs", $"끝낸 런  {meta.Runs}", -140f, 26, NodeWindows.BodyColor);
            Line(panel, "Best", meta.BestFloor > 0 ? $"가장 깊이 간 층  {meta.BestFloor}층" : "가장 깊이 간 층  —",
                 -190f, 26, NodeWindows.BodyColor);
            Line(panel, "Currency", $"기억 조각  {meta.Currency}", -240f, 26, NodeWindows.SubColor);

            NodeWindows.Focus(NodeWindows.BottomButton(panel, "닫기", NodeWindows.ExitColor, CloseWindows));
        }

        // ── 화면 ────────────────────────────────────────

        /// <summary>창 하나를 연다. 이미 떠 있던 것은 버린다(제단은 살 때마다 다시 그린다).</summary>
        private RectTransform OpenWindow(string title, Vector2 size)
        {
            if (window != null) Destroy(window);

            window = new GameObject("LobbyWindow", typeof(RectTransform));
            window.transform.SetParent(transform, false);
            UiKit.BuildCanvas(window, UiLayer.NodeWindow);

            return NodeWindows.Panel(window.transform, title, size);
        }

        private static Text Line(RectTransform panel, string name, string content, float y, int size, Color color)
        {
            Text t = UiKit.NewText(panel, name, size, color, FontStyle.Normal);
            t.text = content;
            t.alignment = TextAnchor.MiddleCenter;
            UiKit.Place(t, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(panel.sizeDelta.x - 60f, 44f));
            return t;
        }

        /// <summary>키 안내 · 보유 재화 · 알림. 클릭을 안 받는 HUD다.</summary>
        private void BuildHud()
        {
            var hud = new GameObject("LobbyHud", typeof(RectTransform));
            hud.transform.SetParent(transform, false);
            UiKit.BuildCanvas(hud, UiLayer.PartyHealth, match: 1f, raycaster: false);

            prompt = UiKit.NewText(hud.transform, "Prompt", 30, Color.white, FontStyle.Bold);
            prompt.alignment = TextAnchor.MiddleCenter;
            UiKit.Place(prompt, new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(800f, 50f));

            currency = UiKit.NewText(hud.transform, "Currency", 26, HudColor, FontStyle.Bold);
            currency.alignment = TextAnchor.MiddleRight;
            UiKit.Place(currency, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(400f, 40f));

            notice = UiKit.NewText(hud.transform, "Notice", 34, HudColor, FontStyle.Bold);
            notice.alignment = TextAnchor.MiddleCenter;
            UiKit.Place(notice, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(900f, 60f));
            notice.text = "";
        }

        private void ShowNotice(string text)
        {
            if (noticeRoutine != null) StopCoroutine(noticeRoutine);
            noticeRoutine = StartCoroutine(NoticeRoutine(text));
        }

        private IEnumerator NoticeRoutine(string text)
        {
            notice.text = text;
            yield return new WaitForSecondsRealtime(NoticeSeconds);
            notice.text = "";
            noticeRoutine = null;
        }

        /// <summary>지금 상호작용 키 이름. 리바인드로 바뀔 수 있어 매번 묻는다.</summary>
        private string KeyName()
        {
            if (interactAction == null)
            {
                InputActionAsset actions = PlayerInputController.Instance != null ? PlayerInputController.Instance.Actions : null;
                interactAction = InputBindingUtility.FindAction(actions, InputActionNames.Gameplay.Map,
                                                                InputActionNames.Gameplay.Interact);
                if (interactAction == null) return "F";
            }

            int index = InputBindingUtility.FindBindingIndex(interactAction, null);
            return index >= 0 ? $"[{InputDisplayNames.Key(interactAction, index)}]" : "[F]";
        }
    }
}
