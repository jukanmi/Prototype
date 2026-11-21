using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype
{
    /// <summary>비전투 칸의 종류. 씬 하나에 하나를 고른다.</summary>
    public enum NodeSceneKind
    {
        /// <summary>카드 상점. 상인 NPC가 <see cref="NodeSceneController.OpenShop"/>을 부르면 창을 연다.</summary>
        Shop = 1,

        /// <summary>
        /// 이벤트. 내용물은 씬마다 다르다 — 선택지(<see cref="NodeSceneController.OpenEvent"/>) ·
        /// NPC와 싸움(선택지의 <see cref="RunEventChoice.fight"/>) · 모닥불(<see cref="NodeSceneController.Rest"/>).
        /// </summary>
        Event = 2,
    }

    /// <summary>
    /// 상점 · 이벤트 씬의 지휘자. <see cref="BattleSceneController"/>의 비전투판이다.
    ///
    /// 마을처럼 걸어다니는 방이다(BattleInput · 카메라 · 방 · 시작 지점). 씬에 놓은 상호작용 지점
    /// (<see cref="Interactable"/>)이 인스펙터에서 <see cref="OpenShop"/> · <see cref="OpenEvent"/> · <see cref="Rest"/>을 부른다.
    ///
    /// <list type="bullet">
    /// <item><b>상점</b> — 창이 뜬다. 닫아도 남아 있다가 다시 말을 걸면 그대로 뜬다(물건이 다시 굴려지지 않는다).</item>
    /// <item><b>이벤트 · 모닥불</b> — 화면 아래 대화창(<see cref="DialogueWindow"/>)에서 글과 선택지가 이어진다.
    /// 고른 뒤 다시 말을 걸면 결과만 다시 보인다 — 또 고를 수 없다.</item>
    /// </list>
    ///
    /// <b>칸은 전투처럼 오른쪽 벽으로 걸어가면 끝난다</b>(<see cref="Leave"/>). 싸움이 벌어지면 이길 때까지 출구가 닫히고,
    /// 지면 전투와 같은 패배 화면이 뜬다 — [재시작]은 이 칸을 처음부터 연다.
    ///
    /// 몸이 서 있는 방이라 체력의 원본은 몸이다. 기록(<see cref="PartyState"/>)을 바꾸는 일(모닥불 · 이벤트 · 싸움 보상)은
    /// 몸 → 기록으로 찍고, 바꾸고, 기록 → 몸으로 되돌린다. 나갈 때 한 번 더 찍는다(전투 씬과 같다).
    ///
    /// 계획서: docs/Run_Map_Plan.md (2.7 · 0단계)
    /// </summary>
    public class NodeSceneController : MonoBehaviour
    {
        [Tooltip("이 씬이 어떤 칸인가.")]
        [SerializeField] private NodeSceneKind kind = NodeSceneKind.Event;

        [Tooltip("OpenEvent가 뽑을 후보. 창을 처음 열 때 하나를 무작위로 고른다.")]
        [SerializeField] private RunEventAsset[] events = new RunEventAsset[0];

        [Tooltip("이 x를 넘으면 칸이 끝난다. 방 오른쪽 벽에서 1 안쪽(StageOutcomeRules.ExitLine) — 방을 바꾸면 같이 고친다.")]
        [SerializeField] private float exitX = 7f;

        [Header("싸움 (선택지의 fight)")]
        [Tooltip("적으로 돌아설 NPC. 싸움이 시작되면 사라지고 그 자리에 적이 선다.")]
        [SerializeField] private GameObject fightNpc;

        [Tooltip("NPC 대신 설 적.")]
        [SerializeField] private EnemyData fightEnemy;

        [Tooltip("적이 선 뒤 AI가 깨어나기까지 서 있는 시간.")]
        [SerializeField] private float fightHoldSeconds = 0.6f;

        private readonly Dictionary<string, GameObject> windows = new Dictionary<string, GameObject>();
        private GameObject openWindow;
        private bool rested;

        /// <summary>처음 말을 걸 때 하나 뽑아 둔다 — 다시 말을 걸어도 같은 이벤트다.</summary>
        private RunEventAsset eventAsset;

        /// <summary>고른 뒤의 결과 글. 있으면 이미 고른 것이다 — 다시 말을 걸면 이것만 보인다.</summary>
        private string[] eventResult;

        /// <summary>싸우는 선택지를 골랐다. 대화창이 닫히면(끝까지 읽든 ESC든) 싸움이 시작된다.</summary>
        private RunEventChoice? pendingFight;

        private Enemy foe;
        private RunEventChoice reward;
        private bool fighting;
        private bool defeated;
        private bool leaving;

        private StageResultUI resultUI;
        private PartyRoster party;

        public NodeSceneKind Kind => kind;
        public IReadOnlyList<RunEventAsset> Events => events;
        public GameObject FightNpc => fightNpc;
        public EnemyData FightEnemy => fightEnemy;

        private string SceneName => gameObject.scene.name;

        /// <summary>
        /// 이 런의 편성. 기록이 없는 동료까지 체력을 바꾸려면 필요하다.
        /// 단독 실행이면 null — 주인공 기록만 바뀐다.
        /// </summary>
        private static IReadOnlyList<PartyMemberData> Roster
            => GameManager.Instance != null && GameManager.Instance.Loadout != null
                ? GameManager.Instance.Loadout.Members
                : null;

        private void Start()
        {
            UiKit.EnsureEventSystem();

            if (GameManager.Instance == null)
                Debug.LogWarning("[Node] GameManager 없음. 씬 단독 실행 — 나가면 이 씬을 다시 연다.");

            // 런 바 — 지도 · 전투와 같은 상단 띠.
            RunBarUI.Create(this, RunBarUI.Mode.Overlay);

            party = FindAnyObjectByType<PartyRoster>();
            resultUI = StageResultUI.Create(this);
            ShowExit();

            Debug.Log($"[Node] {kind} 칸 진입 ({SceneName}) — 골드 {RunProgression.Current.Gold}");
        }

        private void OnDestroy()
        {
            // 창을 띄운 채 씬이 내려가면 조작이 잠긴 채로 다음 씬에 간다.
            if (openWindow != null) PlayerInputController.SetSuspended(false);
        }

        private void Update()
        {
            if (leaving || defeated) return;

            if (openWindow != null)
            {
                PlayerInputController input = PlayerInputController.Instance;
                if (input != null && input.CancelPressed && !GameplayModal.CancelConsumedThisFrame)
                {
                    GameplayModal.ConsumeCancel();
                    CloseWindow();
                }
                return;
            }

            // 싸우는 선택지를 골랐다 — 결과 대화가 닫히면(끝까지 읽든 ESC든) NPC가 적으로 돌아선다.
            if (pendingFight.HasValue && !DialogueWindow.IsOpen)
            {
                RunEventChoice choice = pendingFight.Value;
                pendingFight = null;
                StartFight(choice);
            }

            if (fighting) TickFight();
            else if (ExitReached()) Leave();
        }

        // ── 상호작용 지점이 부르는 것 (Interactable.onInteract) ──

        /// <summary>카드 상점 창. 처음 열 때 한 번 뽑고, 닫았다 다시 열어도 같은 물건이다.</summary>
        public void OpenShop() => Show("Shop", root => new ShopWindow(root, Roster, CloseWindow));

        /// <summary>
        /// 이벤트 — 말을 건 NPC가 대화창에서 글과 선택지를 내민다. 처음 한 번 이벤트를 뽑아 두고,
        /// 고른 뒤 다시 말을 걸면 결과만 다시 보인다. 싸우는 선택지를 고르면 결과 대화가 닫힐 때 싸움이 시작된다.
        /// </summary>
        public void OpenEvent()
        {
            if (!CanTalk()) return;

            Interactable who = Interactable.Current;
            string speaker = who != null ? who.Label : "이벤트";
            Npc face = who != null ? who.GetComponent<Npc>() : null;

            if (eventResult != null)
            {
                DialogueWindow.Open(speaker, eventResult, null, face);
                return;
            }

            if (eventAsset == null) eventAsset = PickEvent();
            if (eventAsset == null || eventAsset.ChoiceCount == 0)
            {
                DialogueWindow.Open(speaker, new[] { "(이벤트가 비어 있다)" }, null, face);
                return;
            }

            // 고르기 전에 몸 → 기록. 선택지가 바꾸는 체력이 지금 몸에서 출발해야 한다.
            CapturePartyState();

            int gold = RunProgression.Current.Gold;
            var options = new List<DialogueChoice>(eventAsset.choices.Length);
            foreach (RunEventChoice c in eventAsset.choices)
            {
                bool can = RunEventRules.CanChoose(in c, gold);
                options.Add(new DialogueChoice(can ? c.label : $"{c.label}  (골드 {-c.goldDelta} 필요)", can));
            }

            DialogueWindow.Ask(speaker, eventAsset.body, options, i => OnEventPicked(i, speaker, face), face, $"골드 {gold}");
        }

        /// <summary>
        /// 모닥불. 처음 한 번 파티 전원을 회복하고 대화창에 결과를 띄운다. 다시 누르면 이미 쉬었다고만 한다.
        /// </summary>
        public void Rest()
        {
            if (!CanTalk()) return;

            string line;
            if (!rested)
            {
                rested = true;
                CapturePartyState();
                RunProgression.Current.Party.ChangeHp(NodeRules.RestHealRatio, Roster);
                RestorePartyHealth();

                line = $"모닥불 앞에서 숨을 고른다.\n<color=#FFDB73>파티 전원의 체력이 {NodeRules.RestHealRatio:0%} 회복됐다.</color>";
            }
            else
            {
                line = "불씨가 아직 따뜻하다. 이미 충분히 쉬었다.";
            }

            Interactable who = Interactable.Current;
            DialogueWindow.Open(who != null ? who.Label : "모닥불", new[] { line });
        }

        // ── 이벤트 ──────────────────────────────────────

        private bool CanTalk() => !leaving && !defeated && !fighting && openWindow == null && !DialogueWindow.IsOpen;

        /// <summary>
        /// 선택지를 골랐다. 싸움이 아니면 지금 적용하고, 싸움이면 이겼을 때 준다(<see cref="Win"/>).
        /// 결과는 같은 대화창에 한 장으로 — 이야기 한 줄과 수치 한 줄.
        /// </summary>
        private void OnEventPicked(int index, string speaker, Npc face)
        {
            RunEventChoice choice = eventAsset.choices[index];

            ComboCard card = null;
            if (!choice.fight && !RunEventRules.Apply(in choice, RunProgression.Current, Roster, NodeWindows.CardPool(Roster),
                                                     () => UnityEngine.Random.value, out card))
            {
                // 고를 수 없는 줄은 잠겨 있어 여기 올 일이 없다 — 오면 골드가 그 사이 바뀐 것이다.
                Debug.LogWarning($"[Event] 고를 수 없는 선택지 — {choice.label}", this);
                return;
            }

            string summary = choice.fight ? $"이기면 {RunEventRules.Summary(in choice)}" : RunEventRules.Summary(in choice);
            if (card != null) summary += $"\n{card.Data.skillName} 카드를 얻었다.";

            string story = string.IsNullOrWhiteSpace(choice.outcome) ? "" : choice.outcome + "\n";
            eventResult = new[] { $"{story}<color=#FFDB73>{summary}</color>" };

            if (choice.fight) pendingFight = choice;
            else RestorePartyHealth();   // 선택지가 기록의 체력을 바꿨다 — 몸에도 보이게

            Debug.Log($"[Event] 선택 — {choice.label} · {summary} (골드 {RunProgression.Current.Gold})", this);
            DialogueWindow.Open(speaker, eventResult, null, face);
        }

        // ── 창 (상점) ───────────────────────────────────

        /// <summary>창을 띄운다. 처음이면 짓고, 지어 둔 것이면 그대로 다시 켠다. 떠 있는 동안 몸을 잠근다.</summary>
        private void Show(string key, Action<Transform> build)
        {
            if (openWindow != null || leaving || defeated || fighting) return;

            if (windows.TryGetValue(key, out GameObject root))
            {
                // 커서는 창에 매단 KeyMenuHost가 켜질 때 다시 잡는다.
                root.SetActive(true);
            }
            else
            {
                root = new GameObject(key + "Window", typeof(RectTransform));
                root.transform.SetParent(transform, false);
                UiKit.BuildCanvas(root, UiLayer.NodeWindow);
                windows[key] = root;
                build(root.transform);
            }

            openWindow = root;
            PlayerInputController.SetSuspended(true);
        }

        /// <summary>[닫기] · ESC. 창만 닫는다 — 칸은 오른쪽 벽으로 걸어가야 끝난다.</summary>
        private void CloseWindow()
        {
            if (openWindow == null) return;

            openWindow.SetActive(false);
            openWindow = null;
            PlayerInputController.SetSuspended(false);
        }

        /// <summary>빈 칸을 뺀 후보 중 하나. 후보가 없으면 null — 대화창이 그 사실을 적는다.</summary>
        private RunEventAsset PickEvent()
        {
            var pool = new List<RunEventAsset>();
            if (events != null)
                foreach (RunEventAsset e in events)
                    if (e != null) pool.Add(e);

            if (pool.Count == 0)
            {
                Debug.LogError($"[Node] {name}: 이벤트 후보가 비어 있다.", this);
                return null;
            }

            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        // ── 싸움 ────────────────────────────────────────

        /// <summary>NPC가 적으로 돌아선다. 이길 때까지 출구가 닫힌다.</summary>
        private void StartFight(RunEventChoice choice)
        {
            if (fightNpc == null || fightEnemy == null)
            {
                Debug.LogError($"[Node] {name}: 싸울 NPC · 적이 안 꽂혀 있다 — 싸움 없이 넘어간다.", this);
                return;
            }

            Vector3 at = fightNpc.transform.position;
            fightNpc.SetActive(false);

            var spawner = GetComponent<EnemySpawnService>();
            if (spawner == null) spawner = gameObject.AddComponent<EnemySpawnService>();

            foe = spawner.SpawnAt(fightEnemy, at, fightHoldSeconds);
            if (foe == null) return;

            reward = choice;
            fighting = true;
            resultUI?.HideExitArrow();

            Debug.Log($"[Node] 싸움 시작 — {fightEnemy.name}", this);
        }

        private void TickFight()
        {
            if (PartyWiped()) Lose();
            else if (foe == null || foe.Combat.IsDead) Win();
        }

        private void Win()
        {
            fighting = false;

            // 컷인 중에 끝나면 시간이 멈춘 채로 남아 출구까지 못 걷는다(BattleSceneController.HandleVictory와 같다).
            TimeControl.Reset();

            CapturePartyState();
            RunEventRules.Apply(in reward, RunProgression.Current, Roster, NodeWindows.CardPool(Roster),
                                () => UnityEngine.Random.value, out ComboCard card);
            RestorePartyHealth();

            string cardName = card != null ? $" · {card.Data.skillName}" : "";
            resultUI?.ShowExitArrow($"{RunEventRules.Summary(in reward)}{cardName} · 지도로", "승리");

            Debug.Log($"[Node] 싸움 승리 — {RunEventRules.Summary(in reward)} (골드 {RunProgression.Current.Gold})", this);
        }

        private void Lose()
        {
            fighting = false;
            defeated = true;

            CastDirector.RecallScene();
            TimeControl.Reset();
            if (foe != null) foe.StopAI();

            resultUI?.HideExitArrow();
            resultUI?.ShowDefeat(Retry, ExitToLobby);

            Debug.Log("[Node] 싸움 패배", this);
        }

        /// <summary>파티가 전멸했는가 — <see cref="BattleSceneController"/>와 같은 규칙.</summary>
        private bool PartyWiped()
            => StageOutcomeRules.PartyWiped(
                party != null ? party.Roster.Count : 0,
                party != null ? party.AliveCount : 0,
                BattleRegistry.AliveAllyCount());

        // ── 출구 ────────────────────────────────────────

        private void ShowExit() => resultUI?.ShowExitArrow("지도로", "");

        /// <summary>조작 중인 몸이 출구선을 넘었는가. 꺼지거나 쓰러진 몸은 안 본다.</summary>
        private bool ExitReached()
        {
            Entity body = BattleRegistry.Controlled;
            return body != null && body.isActiveAndEnabled && !body.Combat.IsDead
                   && StageOutcomeRules.ReachedExit(body.transform.position.x, exitX);
        }

        /// <summary>
        /// 이 칸을 끝내고 지도로 돌아간다. 몸이 아직 씬에 있을 때 체력을 찍는다(전투 씬의 ReturnToMap과 같다).
        /// 출구 오브젝트를 따로 두고 싶으면 그 Interactable에 이걸 꽂으면 된다.
        ///
        /// 비전투 칸은 보스 층에 올 수 없으므로(레시피 검사) "갈 곳이 없는" 경우가 없다.
        /// </summary>
        public void Leave()
        {
            if (leaving) return;

            CapturePartyState();
            Transition(gm =>
            {
                if (gm.CompleteNodeAndReturnToMap(SceneName, PlayMenuBgm)) return true;

                // 들어가 있는 칸이 없다 — 지도를 안 거치고 이 씬이 떴다. 갇히지 않게 마을로 보낸다.
                if (gm.CurrentNode != null || !gm.ReturnToLobby(SceneName, PlayMenuBgm)) return false;

                Debug.LogWarning($"[Node] 들어가 있는 지도 칸이 없다({SceneName}) — 마을로 돌아간다.");
                return true;
            });
        }

        /// <summary>패배 후 [재시작]. 이 칸을 처음부터 연다 — 기록은 들어올 때 그대로라 체력도 그때로 돌아간다.</summary>
        private void Retry() => Transition(gm => gm.RestartCurrentStage(SceneName, PlayMenuBgm));

        private void ExitToLobby() => Transition(gm => gm.ReturnToLobby(SceneName, PlayMenuBgm));

        /// <summary>
        /// 전환은 <b>접수가 먼저다</b> — 거절되면 다시 시도할 수 있게 남긴다. 접수되면 전투 씬 · 마을과 같은
        /// 정리(<see cref="BattleSceneController.ClearSceneStatics"/>)를 한다 — 이 방에도 몸이 서 있다.
        /// </summary>
        private void Transition(Func<GameManager, bool> go)
        {
            if (leaving) return;

            GameManager gm = GameManager.Instance;
            if (gm == null || SceneLoader.Instance == null)
            {
                leaving = true;
                BattleSceneController.ClearSceneStatics();
                SceneManager.LoadScene(SceneName);
                return;
            }

            if (!go(gm)) return;

            leaving = true;
            BattleSceneController.ClearSceneStatics();
        }

        private static void PlayMenuBgm() => AudioManager.Instance?.PlayMenuBgm();

        // ── 체력: 몸 ↔ 기록 ─────────────────────────────

        /// <summary>몸 → 기록. 이 방에서 다친 것까지 기록에 남긴다.</summary>
        private static void CapturePartyState()
        {
            Player hero = FindAnyObjectByType<Player>();
            if (hero != null) RunProgression.Current.Party.Capture(hero);
        }

        /// <summary>기록 → 몸. 기록을 바꾼 뒤(모닥불 · 이벤트) 화면의 체력바도 따라오게.</summary>
        private static void RestorePartyHealth()
        {
            PartyAssembler assembler = FindAnyObjectByType<PartyAssembler>();
            if (assembler != null) assembler.RestoreFromRecord();
        }
    }
}
