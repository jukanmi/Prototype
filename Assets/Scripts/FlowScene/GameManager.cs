using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 런(run) 단위 데이터와 <b>스테이지 진행</b>을 소유한다. 씬 오브젝트는 절대 참조하지 않는다.
    /// 씬이 언로드되는 순간 그런 참조는 전부 무효가 되기 때문이다 —
    /// 스테이지 전환도 씬 <b>이름</b>만 주고받는다.
    /// Boot 씬에 상주하며 Boot 씬은 언로드되지 않으므로 DontDestroyOnLoad 는 쓰지 않는다.
    ///
    /// 승패를 <b>판정</b>하는 것은 여기가 아니다. 그건 전투 상황을 봐야 하는 일이라
    /// <see cref="BattleSceneController"/> 가 맡고, 결론이 나면 여기 있는 전환을 부른다.
    /// 그래서 GameManager 가 없는 상태(스테이지 씬 단독 실행)에서는 승패 기능이 아예 돌지 않는다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>
        /// 스테이지 진행 순서. 이 순서가 곧 게임의 흐름이다.
        ///
        /// 역할군(전사 → 돌진전사 → 마법사)이 스테이지마다 늘어나는 순서라
        /// 이 배열의 순서 자체가 학습 곡선이다.
        ///
        /// <b>2 · 5스테이지는 지형이 다르다</b> — 아레나 2개와 통로로 이뤄진 스크롤 스테이지다.
        /// 5스테이지가 보스방이다. 나머지는 방 하나짜리다. 다섯 다 같은 디렉터가 돌리고,
        /// 지형 규약은 <see cref="Prototype.StageWaveCatalog"/>에 있다.
        ///
        /// 예전 경로(미니 → SampleScene → 보스)는 <see cref="DebugStages"/>에 남겨 뒀다 —
        /// 씬 하나만 띄워 감각을 보는 용도라 게임의 흐름과는 다른 물건이다.
        /// </summary>
        public static readonly string[] DefaultStages =
        {
            SceneNames.Stage01,
            SceneNames.Stage02,
            SceneNames.Stage03,
            SceneNames.Stage04,
            SceneNames.Stage05,
        };

        /// <summary>
        /// 배치·전투 감각 확인용 짧은 경로. 웨이브 없이 씬에 놓인 적과 그대로 싸운다.
        /// 인스펙터의 <c>stageScenes</c>에 손으로 넣어 쓴다.
        /// </summary>
        public static readonly string[] DebugStages =
        {
            SceneNames.StageMini,
            SceneNames.Battle,
            SceneNames.StageBoss,
        };

        /// <summary>
        /// 인스펙터에서 바꿀 수 있는 진행 순서.
        ///
        /// <b>비어 있으면 <see cref="DefaultStages"/>로 채운다.</b> 이 필드가 생기기 전에 만들어진
        /// Boot 씬의 GameManager 는 빈 배열로 역직렬화되는데, 그대로 두면 스테이지가 0개라
        /// 게임이 시작조차 안 된다.
        /// </summary>
        [Tooltip("비우면 기본 순서(스테이지 1~5)로 채운다.")]
        [SerializeField] private string[] stageScenes;

        [Header("디버그 — 시작 덱")]
        [Tooltip("런의 첫 전투에서 시작 덱을 어떻게 정할지. 두 번째 스테이지부터는 런 덱이 이어진다.\n\n" +
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

        // ── 런 데이터 (프로토타입 단계 최소 구성)
        public int  CurrentStageIndex { get; private set; }
        public bool IsRunActive       { get; private set; }

        /// <summary>
        /// 경험치 · 레벨 · 런 덱. 씬 오브젝트가 아니라 여기가 들고 있어야
        /// 스테이지를 넘어가도 레벨업으로 얻은 카드가 살아남는다.
        /// </summary>
        public RunProgression Run { get; } = new RunProgression();

        /// <summary>런 누적 경험치. <see cref="Run"/>이 진짜 주인이고 이건 읽는 창구다.</summary>
        public int TotalExp => Run.Exp;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (stageScenes == null || stageScenes.Length == 0)
                stageScenes = (string[])DefaultStages.Clone();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 스테이지 목록 ────────────────────────────────

        public IReadOnlyList<string> Stages => stageScenes;
        public int StageCount => stageScenes != null ? stageScenes.Length : 0;

        /// <summary>지금 있어야 할 스테이지의 씬 이름. 목록이 비면 null.</summary>
        public string CurrentStageScene => StageAt(CurrentStageIndex);

        public bool HasNextStage => CurrentStageIndex + 1 < StageCount;

        /// <summary>다음 스테이지의 씬 이름. 마지막이면 null.</summary>
        public string NextStageScene => HasNextStage ? StageAt(CurrentStageIndex + 1) : null;

        /// <summary>사람에게 보여줄 번호. 1부터 센다.</summary>
        public int CurrentStageNumber => CurrentStageIndex + 1;

        /// <summary>범위를 벗어난 번호는 양끝으로 물린다. 목록이 비면 null.</summary>
        public string StageAt(int index)
        {
            if (StageCount == 0) return null;
            return stageScenes[Mathf.Clamp(index, 0, StageCount - 1)];
        }

        // ── 런 수명 ──────────────────────────────────────

        /// <summary>메인화면 [시작] 클릭 시 호출. 런 상태를 초기값으로 되돌린다.</summary>
        public void StartNewRun()
        {
            CurrentStageIndex = 0;
            IsRunActive       = true;

            // 메인화면에서 안 골랐으면 기본 조합으로 간다. 여기서 확정해 두면
            // 이후 스테이지의 PartyAssembler 는 매번 같은 답을 받는다.
            if (Loadout == null) Loadout = defaultLoadout;

            Run.Reset();

            RestoreTime();

            Debug.Log($"[GameManager] 새 런 시작 — 스테이지 1/{StageCount} ({CurrentStageScene})");
        }

        /// <summary>ESC 이탈 또는 패배 시 호출. 런 데이터를 폐기한다.</summary>
        public void EndRun()
        {
            IsRunActive = false;

            RestoreTime();

            Debug.Log("[GameManager] 런 종료");
        }

        public void AddExp(int amount) => Run.AddExp(amount);

        /// <summary>다음 칸으로 한 칸. 마지막에서는 더 가지 않는다.</summary>
        public void AdvanceStage()
        {
            if (!HasNextStage) return;
            CurrentStageIndex++;
        }

        // ── 스테이지 전환 ────────────────────────────────
        // 전부 "지금 떠 있는 전투 씬 이름"을 받아 그 씬을 내리고 새 씬을 얹는다.
        // 부르는 쪽이 자기 씬 이름을 아는 유일한 주체라, 여기서 상수로 짐작하지 않는다.

        /// <summary>다음 스테이지로 넘어간다. 마지막 스테이지였거나 전환 중이면 false.</summary>
        public bool GoToNextStage(string fromScene, Action onComplete = null)
        {
            if (!CanSwap()) return false;
            if (!HasNextStage)
            {
                Debug.Log("[GameManager] 마지막 스테이지다. 넘어갈 곳이 없다.");
                return false;
            }

            AdvanceStage();

            Debug.Log($"[GameManager] 스테이지 {CurrentStageNumber}/{StageCount} → {CurrentStageScene}");
            Swap(CurrentStageScene, fromScene, onComplete);
            return true;
        }

        /// <summary>같은 스테이지를 처음부터. 패배 후 [재시작]이 부른다.</summary>
        public bool RestartCurrentStage(string fromScene, Action onComplete = null)
        {
            if (!CanSwap()) return false;

            string target = CurrentStageScene;
            if (string.IsNullOrEmpty(target)) return false;

            // 스테이지 번호는 그대로 둔다 — "이 스테이지를 다시"이지 "처음부터"가 아니다.
            IsRunActive = true;
            RestoreTime();

            Debug.Log($"[GameManager] 스테이지 {CurrentStageNumber} 재시작 — {target}");
            Swap(target, fromScene, onComplete);
            return true;
        }

        /// <summary>런을 버리고 첫 스테이지부터. 전투 씬 우상단 [처음부터]가 부른다.</summary>
        public bool RestartRun(string fromScene, Action onComplete = null)
        {
            if (!CanSwap()) return false;

            StartNewRun();

            string target = CurrentStageScene;
            if (string.IsNullOrEmpty(target)) return false;

            Swap(target, fromScene, onComplete);
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
