using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 런(run) 단위 데이터와 <b>런 지도 위의 진행</b>을 소유한다. 씬 오브젝트는 절대 참조하지 않는다.
    /// 씬이 언로드되는 순간 그런 참조는 전부 무효가 되기 때문이다 —
    /// 전환도 씬 <b>이름</b>만 주고받는다.
    /// Boot 씬에 상주하며 Boot 씬은 언로드되지 않으므로 DontDestroyOnLoad 는 쓰지 않는다.
    ///
    /// <b>흐름.</b> [시작] → 지도 굴림 → 지도 씬 → 칸 선택 → 그 칸의 씬 → 끝내면 지도 씬 → … → 보스.
    /// 지도는 [시작] 순간 한 번 굴리고, 진행은 <see cref="RunMapProgress"/>의 값 두 개뿐이다.
    /// 계획서: docs/Run_Map_Plan.md (3단계)
    ///
    /// 승패를 <b>판정</b>하는 것은 여기가 아니다. 그건 전투 상황을 봐야 하는 일이라
    /// <see cref="BattleSceneController"/> 가 맡고, 결론이 나면 여기 있는 전환을 부른다.
    /// 그래서 GameManager 가 없는 상태(스테이지 씬 단독 실행)에서는 승패 기능이 아예 돌지 않는다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("런 지도")]
        [Tooltip("[시작]을 누를 때 굴릴 지도 레시피. 비어 있거나 검사에 걸리면 런이 안 열린다.\n\n" +
                 "일렬 디버그 경로(미니 → SampleScene → 보스)는 RunMap_Debug 를 꽂는다.")]
        [SerializeField] private RunMapRecipe recipe;

        [Tooltip("0이면 [시작]마다 새 시드. 숫자를 넣으면 매번 같은 지도가 나온다 — 로그에 찍힌 시드로 재현할 때 쓴다.")]
        [SerializeField] private int debugSeed;

        [Header("디버그 — 시작 덱")]
        [Tooltip("런의 첫 전투에서 시작 덱을 어떻게 정할지. 두 번째 전투부터는 런 덱이 이어진다.\n\n" +
                 "· Party — 파티 장착 카드 16장. 게임의 실제 시작이다.\n" +
                 "· Empty — 테스트 모드. 0장으로 시작해 레벨업으로만 카드가 들어온다.\n" +
                 "· Pick  — 디버그 모드. 시작할 때 화면에서 카드를 직접 골라 짠다.\n\n" +
                 "런 단위 설정이라 여기가 스테이지 씬의 BulletTimeController 설정을 이긴다 — " +
                 "스테이지마다 시작 덱 규칙이 다르면 말이 안 되기 때문이다.")]
        [SerializeField] private DeckStartupMode deckStartupMode = DeckStartupMode.Party;

        /// <summary>이 런의 시작 덱 규칙. 전투 씬이 물어본다.</summary>
        public DeckStartupMode DeckStartupMode => deckStartupMode;

        [Header("파티")]
        [Tooltip("메인화면에서 아무것도 고르지 않았을 때 쓸 파티 조합.\n\n" +
                 "런 단위 결정이라 여기가 스테이지 씬의 PartyAssembler 설정을 이긴다 — " +
                 "시작 덱 규칙과 같은 이유다. 스테이지마다 파티가 다르면 말이 안 된다.")]
        [SerializeField] private PartyLoadout defaultLoadout;

        /// <summary>
        /// 이 런을 굴리는 파티. 스테이지 씬의 <see cref="Prototype.PartyAssembler"/>가 물어본다.
        ///
        /// <c>GameManager</c>는 Boot 씬에 상주하고 Boot 씬은 언로드되지 않으므로,
        /// 스테이지를 넘어가도 이 값은 그대로 살아남는다 — 따로 저장할 곳이 필요 없다.
        /// </summary>
        public PartyLoadout Loadout { get; private set; }

        /// <summary>
        /// 파티를 고른다. 메인화면의 파티 선택이 <see cref="StartNewRun"/> <b>전에</b> 부른다.
        ///
        /// <b>런 도중에는 부르지 않는다.</b> 덱은 첫 스테이지에서 파티 카드로 한 번 씨를 뿌린 뒤
        /// <see cref="Prototype.RunProgression.Seeded"/>가 서서 런 덱이 이긴다 —
        /// 도중에 파티만 갈아치우면 시전자가 없는 카드가 손패에 남아 U키가 먹통이 된다.
        /// </summary>
        public void SelectLoadout(PartyLoadout loadout)
        {
            if (IsRunActive)
            {
                Debug.LogWarning("[GameManager] 런 도중 파티 교체는 지원하지 않는다 — 무시한다.");
                return;
            }

            Loadout = loadout;
        }

        // ── 런 데이터 ────────────────────────────────────

        public bool IsRunActive { get; private set; }

        /// <summary>이 런의 지도. 런이 없으면 null.</summary>
        public RunMap Map => Progress != null ? Progress.Map : null;

        /// <summary>지도 위 진행. 런이 없으면 null.</summary>
        public RunMapProgress Progress { get; private set; }

        public RunMapRecipe Recipe => recipe;

        /// <summary>
        /// 레시피와 시드를 한 번에 꽂는다. <b>테스트가 들어오는 이음매</b>다 —
        /// <see cref="StageWaveBoard.Configure(StageEncounter[])"/>와 같은 이유로 <c>SerializedObject</c>를 안 쓴다.
        /// </summary>
        public void Configure(RunMapRecipe mapRecipe, int seed)
        {
            recipe = mapRecipe;
            debugSeed = seed;
        }

        /// <summary>
        /// 경험치 · 레벨 · 런 덱 · 골드. 씬 오브젝트가 아니라 여기가 들고 있어야
        /// 스테이지를 넘어가도 레벨업으로 얻은 카드가 살아남는다.
        /// </summary>
        public RunProgression Run { get; } = new RunProgression();

        /// <summary>런 누적 경험치. <see cref="Run"/>이 진짜 주인이고 이건 읽는 창구다.</summary>
        public int TotalExp => Run.Exp;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 지도 위 위치 ─────────────────────────────────

        /// <summary>지금 들어가 있는 칸(전투 중 · 패배 후 재시작 대기). 없으면 null.</summary>
        public MapNode CurrentNode => Progress != null ? Progress.PendingNode : null;

        /// <summary>사람에게 보여 줄 층 번호, 1부터. 런이 없으면 1.</summary>
        public int FloorNumber => Progress != null ? Progress.FloorNumber : 1;

        public int FloorCount => Map != null ? Map.FloorCount : 0;

        /// <summary>
        /// 지금 칸을 끝낸 뒤에 고를 칸이 남는가. 들어가 있는 칸이 보스 층이 아니면 참이다.
        /// 거짓이면 이 칸을 깨는 것이 런의 끝이다.
        /// </summary>
        public bool HasNextNode => CurrentNode != null && CurrentNode.Floor < Map.LastFloor;

        /// <summary>
        /// 지금 칸이 조우에 거는 보정 — 정예 강화와 방 크기. 칸이 없으면(씬 단독 실행 포함) 저작 그대로.
        /// 디렉터와 (7단계부터) 씬의 <see cref="StageRoom"/>이 Awake에서 읽는다.
        /// </summary>
        public EncounterModifier CurrentNodeModifier => EncounterModifierRules.For(CurrentNode);

        // ── 런 수명 ──────────────────────────────────────

        /// <summary>
        /// 메인화면 [시작] 클릭 시 호출. 런 상태를 초기값으로 되돌리고 지도를 굴린다.
        ///
        /// <b>레시피가 없거나 굴리지 못하면 런을 안 연다.</b> 빈 목록을 기본값으로 채우던 옛 폴백은
        /// 되살리지 않는다 — 조용한 폴백이 3 · 4스테이지를 1번 표로 돌렸다(<c>c325639f</c>).
        /// </summary>
        public bool StartNewRun()
        {
            if (recipe == null)
            {
                Debug.LogError("[GameManager] 런 지도 레시피가 비어 있다. 런을 열 수 없다.", this);
                return false;
            }

            int seed = debugSeed != 0 ? debugSeed : Environment.TickCount;

            if (!recipe.TryGenerate(seed, out RunMap map, out List<RunMapIssue> issues))
            {
                string why = issues.Count > 0 ? issues[0].Describe() : "";
                Debug.LogError($"[GameManager] 지도를 굴리지 못했다(시드 {seed}, {recipe.name}) — {why}", this);
                return false;
            }

            Progress = new RunMapProgress(map);
            IsRunActive = true;

            // 메인화면에서 안 골랐으면 기본 조합으로 간다. 여기서 확정해 두면
            // 이후 스테이지의 PartyAssembler 는 매번 같은 답을 받는다.
            if (Loadout == null) Loadout = defaultLoadout;

            Run.Reset();

            RestoreTime();

            // 방 크기는 모양 문자열과 따로 찍는다 — Describe 는 "같은 모양인가"를 비교하는 용도다.
            string rooms = map.DescribeRooms();
            Debug.Log($"[GameManager] 새 런 — 시드 {seed} · {map.FloorCount}층 ({recipe.name})\n{map.Describe()}" +
                      (rooms.Length > 0 ? "\n" + rooms : ""));
            return true;
        }

        /// <summary>ESC 이탈 또는 패배 시 호출. 런 데이터를 폐기한다.</summary>
        public void EndRun()
        {
            IsRunActive = false;
            Progress = null;

            RestoreTime();

            Debug.Log("[GameManager] 런 종료");
        }

        public void AddExp(int amount) => Run.AddExp(amount);

        // ── 전환 ────────────────────────────────────────
        // 전부 "지금 떠 있는 씬 이름"을 받아 그 씬을 내리고 새 씬을 얹는다.
        // 부르는 쪽이 자기 씬 이름을 아는 유일한 주체라, 여기서 상수로 짐작하지 않는다.
        //
        // <b>접수가 먼저, 상태 변경이 나중이다.</b> SceneLoader 가 거절했는데 진행만 바뀌면
        // 지도에 서 있는데 "들어가 있는 칸"이 박혀 아무 칸도 못 고르는 상태가 된다.

        /// <summary>
        /// 지도에서 칸을 고른다. 고를 수 없는 칸이거나 전환이 거절되면 거짓이고 진행은 그대로다.
        ///
        /// 씬이 빈 칸(휴식만 허용 — 계획서 2.1)은 씬을 안 열고 이 자리에서 회복한 뒤 끝낸다.
        /// 그때 <paramref name="onComplete"/>는 안 불린다 — 전환이 없으니 부르는 쪽이 바로 다시 그린다.
        /// </summary>
        public bool EnterNode(int nodeId, string fromScene, Action onComplete = null)
        {
            if (Progress == null || !Progress.CanSelect(nodeId)) return false;

            MapNode node = Map.Get(nodeId);

            if (!node.HasScene)
            {
                if (!RunMapRules.CanSkipScene(node.Kind))
                {
                    Debug.LogError($"[GameManager] 씬 없는 {node.Kind} 칸은 처리할 수 없다 — {node}", this);
                    return false;
                }

                Progress.TrySelect(nodeId);
                Run.Party.ChangeHp(NodeRules.RestHealRatio, Loadout != null ? Loadout.Members : null);
                Progress.CompletePending();

                Debug.Log($"[GameManager] {node.Floor + 1}층 {node} — 지도에서 바로 회복");
                return true;
            }

            if (!CanSwap()) return false;

            Progress.TrySelect(nodeId);
            IsRunActive = true;
            RestoreTime();

            Debug.Log($"[GameManager] {FloorNumber}층 / {FloorCount}층 → {node}");
            Swap(node.Scene, fromScene, onComplete);
            return true;
        }

        /// <summary>
        /// 들어가 있던 칸을 끝내고 지도로 돌아간다. 전투 씬의 출구와 비전투 씬의 [나가기]가 부른다.
        /// 들어가 있는 칸이 없거나 전환이 거절되면 거짓이다.
        /// </summary>
        public bool CompleteNodeAndReturnToMap(string fromScene, Action onComplete = null)
        {
            if (Progress == null || !Progress.HasPending) return false;
            if (!CanSwap()) return false;

            MapNode done = Progress.PendingNode;
            Progress.CompletePending();

            Debug.Log($"[GameManager] {done} 완료 → 지도");
            Swap(SceneNames.RunMap, fromScene, onComplete);
            return true;
        }

        /// <summary>
        /// 같은 칸을 처음부터. 패배 후 [재시작]이 부른다.
        /// 들어가 있는 칸은 끝나지 않았으므로 그대로 다시 열면 된다 — 되돌릴 진행이 없다.
        /// </summary>
        public bool RestartCurrentStage(string fromScene, Action onComplete = null)
        {
            if (!CanSwap()) return false;

            MapNode node = CurrentNode;
            if (node == null || !node.HasScene) return false;

            IsRunActive = true;
            RestoreTime();

            Debug.Log($"[GameManager] {node} 재시작");
            Swap(node.Scene, fromScene, onComplete);
            return true;
        }

        /// <summary>런을 버리고 새 지도부터. 전투 씬 우상단 [처음부터]가 부른다.</summary>
        public bool RestartRun(string fromScene, Action onComplete = null)
        {
            if (!CanSwap()) return false;
            if (!StartNewRun()) return false;

            Swap(SceneNames.RunMap, fromScene, onComplete);
            return true;
        }

        /// <summary>런을 접고 메인화면으로.</summary>
        public bool ReturnToMainMenu(string fromScene, Action onComplete = null)
        {
            if (!CanSwap()) return false;

            EndRun();
            Swap(SceneNames.MainMenu, fromScene, onComplete);
            return true;
        }

        private static bool CanSwap()
            => SceneLoader.Instance != null && !SceneLoader.Instance.IsBusy;

        private static void Swap(string load, string unload, Action onComplete)
            => SceneLoader.Instance.SwapTo(load, unload, onComplete);

        /// <summary>
        /// 이 프로젝트의 일시정지는 Time.timeScale 이 아니라 TimeControl.Scale 이다
        /// (불릿타임 중 UI · 조준은 계속 돌아야 하므로). 불릿타임 도중 이탈하면
        /// Scale 이 0 인 채로 남아 다음 런이 멈춘 상태로 시작하므로 여기서 되돌린다.
        /// Time.timeScale 은 외부 코드가 건드렸을 경우를 대비한 보험이다.
        /// </summary>
        private static void RestoreTime()
        {
            TimeControl.Reset();
            Time.timeScale = 1f;
        }
    }
}
