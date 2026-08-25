using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.YG
{
    /// <summary>
    /// 배틀 씬의 지휘자. ESC 이탈, 씬 초기화, 그리고 <b>승패 판정</b>을 맡는다.
    /// 씬 자체가 통째로 언로드되므로 오브젝트 리셋 코드는 필요 없다.
    ///
    /// 판정은 여기서 하고 <b>전환은 <see cref="GameManager"/>가 한다.</b> 승패는 전투 상황을
    /// 봐야 알 수 있어서 씬 안에 있어야 하고, "다음이 어디인가"는 씬이 바뀌어도 남아야 해서
    /// Boot 씬에 있어야 한다.
    ///
    /// 그래서 <b>GameManager 없이 스테이지 씬만 단독으로 Play 하면 승패 기능이 아예 돌지 않는다.</b>
    /// 배치와 전투 감각만 확인하는 용도다.
    /// </summary>
    public class BattleSceneController : MonoBehaviour
    {
        [Tooltip("이 X좌표를 넘으면 다음 스테이지로 넘어간다. 방 오른쪽 벽이 x = 6이고 " +
                 "몸통 반지름 때문에 실제로는 5.5 근처에서 막힌다.")]
        [SerializeField] private float exitX = 5f;

        private bool isExiting;
        private bool isRestarting;

        /// <summary>승패 기능이 도는가. GameManager 가 있어야 켜진다.</summary>
        private bool flowEnabled;

        /// <summary>양쪽이 등록을 마쳐 판정을 시작했는가. 한 번 켜지면 내려가지 않는다.</summary>
        private bool judging;

        private StageOutcome outcome = StageOutcome.Undecided;

        private BattleRestartUI restartUI;
        private StageResultUI resultUI;

        /// <summary>
        /// 웨이브 · 라운드를 굴리는 쪽. <b>없어도 된다</b> — 씬에 적을 직접 놓은 방
        /// (SampleScene · Stage_Mini · 훈련장)은 지금까지처럼 그대로 돈다.
        /// 있으면 승리 판정이 이쪽에 "아직 나올 적이 남았는지"를 물어본다.
        /// </summary>
        private StageProgressSource progress;

        /// <summary>지금 떠 있는 이 씬의 이름. 전환할 때 "무엇을 내릴지"가 된다.</summary>
        private string SceneName => gameObject.scene.name;

        private void Start()
        {
            // 초기화 UI 는 씬 단독 실행에서도 필요하다. GameManager 체크보다 먼저 띄운다.
            restartUI = BattleRestartUI.Create(this);

            progress = FindAnyObjectByType<StageProgressSource>();

            // 씬 단독 실행 대응 — Boot 씬 없이 배틀 씬만 Play 했을 때
            if (GameManager.Instance == null)
            {
                Debug.LogWarning(
                    "[Battle] GameManager 없음. 씬 단독 실행 모드로 진행합니다 — 승리·패배 판정이 돌지 않습니다.");
                return;
            }

            flowEnabled = true;
            resultUI = StageResultUI.Create(this);

            BeginStage();
        }

        private void BeginStage()
        {
            TimeControl.Reset();

            GameManager gm = GameManager.Instance;
            Debug.Log($"[Battle] 스테이지 시작 {gm.CurrentStageNumber}/{gm.StageCount} ({SceneName})");
        }

        private void Update()
        {
            if (isExiting || isRestarting) return;

            if (WasEscapePressed())
            {
                ExitToMainMenu();
                return;
            }

            if (flowEnabled) TickOutcome();
        }

        // ── 승패 판정 ────────────────────────────────────

        private void TickOutcome()
        {
            // 전면 UI(레벨업 · 덱 편집)가 떠 있는 동안은 판정을 멈춘다. 마지막 라운드를
            // 정리하는 중이라 그대로 두면 카드를 고르는 위로 클리어 화면이 겹쳐 뜬다.
            if (GameplayModal.IsOpen) return;

            if (outcome == StageOutcome.Undecided)
            {
                Judge();
                return;
            }

            // 이긴 뒤에는 출구만 본다. 진 뒤에는 버튼이 다음 행동을 정한다.
            if (outcome == StageOutcome.Victory && GameManager.Instance.HasNextStage && ExitReached())
                GoToNextStage();
        }

        private void Judge()
        {
            // Entity 는 Start 에서 스스로 등록한다. 그 전에 판정하면 적 0명 = 승리이면서
            // 산 아군 0명 = 패배라, 씬이 뜨자마자 결과 화면이 나온다.
            if (!judging)
            {
                // 웨이브·라운드 방은 첫 적이 나오기 전까지 적이 0명이다. 등록 수만 보고 열면
                // 그 사이에 "적 0명 = 승리"가 나온다 — 한 기라도 나올 때까지 기다린다.
                if (progress != null && !progress.HasSpawnedAny) return;

                if (!StageOutcomeRules.CanJudge(BattleRegistry.Enemies.Count, BattleRegistry.Allies.Count))
                    return;

                judging = true;
            }

            StageOutcome next = StageOutcomeRules.Evaluate(
                BattleRegistry.AliveEnemyCount(),
                BattleRegistry.AllAlliesDead(),
                progress != null && progress.ThreatsRemaining);

            if (next == StageOutcome.Undecided) return;

            outcome = next;

            if (next == StageOutcome.Victory) HandleVictory();
            else HandleDefeat();
        }

        /// <summary>
        /// 조작 중인 몸이 출구선을 넘었는가.
        ///
        /// 교대로 내려간 동료는 <c>SetActive(false)</c> 상태라 좌표가 벤치에 있던 자리 그대로다.
        /// 활성 여부를 같이 보지 않으면 내려간 동료의 옛 좌표로 스테이지가 넘어간다.
        /// </summary>
        private bool ExitReached()
        {
            foreach (Entity e in BattleRegistry.Allies)
            {
                if (e == null || !e.isActiveAndEnabled || e.Combat.IsDead) continue;
                if (StageOutcomeRules.ReachedExit(e.transform.position.x, exitX)) return true;
            }

            return false;
        }

        private void HandleVictory()
        {
            // 불릿타임 중에 마지막 적이 죽으면 Scale 이 0 인 채로 남아 출구까지 걸어갈 수가 없다.
            TimeControl.Reset();

            GameManager gm = GameManager.Instance;
            Debug.Log($"[Battle] 스테이지 {gm.CurrentStageNumber} 클리어");

            if (gm.HasNextStage)
            {
                // 안내만 띄우고 조작은 막지 않는다. 걸어가야 넘어가는 방식이다.
                resultUI?.ShowExitArrow($"다음: {gm.NextStageScene}");
                return;
            }

            restartUI?.SetVisible(false);
            resultUI?.ShowAllClear(ExitToMainMenu);
        }

        private void HandleDefeat()
        {
            TimeControl.Reset();
            StopAllEnemies();

            Debug.Log($"[Battle] 스테이지 {GameManager.Instance.CurrentStageNumber} 패배");

            restartUI?.SetVisible(false);
            resultUI?.ShowDefeat(RetryStage, ExitToMainMenu);
        }

        /// <summary>진 뒤에도 적이 시체를 계속 두들기지 않게 한다.</summary>
        private static void StopAllEnemies()
        {
            foreach (Entity e in BattleRegistry.Enemies)
                if (e is Enemy enemy) enemy.StopAI();
        }

        // ── 전환 ────────────────────────────────────────

        /// <summary>
        /// 전환은 <b>먼저 접수시키고 그다음에 치운다.</b>
        ///
        /// 순서를 뒤집으면 안 된다 — SceneLoader 가 다른 전환 중이라 요청을 거절했을 때
        /// <see cref="BattleRegistry"/>만 비워진 채로 이 씬이 계속 돌아, 아무도 서로를 못 찾는
        /// 유령 전투가 된다. 접수된 뒤에 비우는 것은 안전하다: SwapTo 는 페이드부터 시작하므로
        /// 실제 언로드는 몇 프레임 뒤다.
        /// </summary>
        private void GoToNextStage()
        {
            if (isExiting || isRestarting) return;
            if (!GameManager.Instance.GoToNextStage(SceneName, OnBattleReloaded)) return;

            isExiting = true;   // 전환이 끝날 때까지 이 씬의 판정을 멈춘다
            CleanupStage();
            ClearStatics();
        }

        /// <summary>패배 후 [이 스테이지 재시작]. 스테이지 번호는 유지된다.</summary>
        private void RetryStage()
        {
            if (isExiting || isRestarting) return;
            if (!GameManager.Instance.RestartCurrentStage(SceneName, OnBattleReloaded)) return;

            isRestarting = true;
            CleanupStage();
            ClearStatics();
        }

        /// <summary>
        /// 런을 처음부터 다시 시작한다. <see cref="BattleRestartUI"/> 의 확인 버튼이 부른다.
        ///
        /// 오브젝트를 하나씩 초기값으로 되돌리는 대신 씬을 통째로 내렸다 다시 올린다 —
        /// 리셋 누락이 원천적으로 생길 수 없는 방식이고, 이 프로젝트는 이미 ESC 이탈에서
        /// 같은 구조를 쓰고 있다.
        /// </summary>
        public void RestartStage()
        {
            if (isExiting || isRestarting) return;
            if (SceneLoader.Instance != null && SceneLoader.Instance.IsBusy) return;

            // ── 씬 단독 실행 모드: SceneLoader 가 없으니 현재 씬을 직접 다시 로드한다.
            if (SceneLoader.Instance == null || GameManager.Instance == null)
            {
                Debug.Log("[Battle] 씬 단독 실행 모드 — 현재 씬을 다시 로드한다.");

                isRestarting = true;
                CleanupStage();
                ClearStatics();

                SceneManager.LoadScene(SceneName);
                return;
            }

            if (!GameManager.Instance.RestartRun(SceneName, OnBattleReloaded)) return;

            Debug.Log("[Battle] 런 초기화 — 첫 스테이지를 다시 올린다.");

            isRestarting = true;
            CleanupStage();
            ClearStatics();
        }

        private void ExitToMainMenu()
        {
            if (isExiting || isRestarting) return;
            if (GameManager.Instance == null) return;
            if (!GameManager.Instance.ReturnToMainMenu(SceneName, OnReturnedToMenu)) return;

            isExiting = true;
            CleanupStage();
            ClearStatics();
        }

        /// <summary>
        /// 씬 경계를 넘어 살아남는 static 은 반드시 <b>지금</b> 비운다.
        ///
        /// 새 씬의 Entity 는 Start 에서 스스로 등록하므로, 로드가 끝난 뒤에 비우면
        /// 갓 등록된 새 Entity 까지 날아가 타게팅이 통째로 죽는다. 지금 비워도 곧 파괴될
        /// 옛 Entity 의 OnDestroy → Unregister 는 빈 목록에 대한 no-op 이라 안전하다.
        /// </summary>
        private static void ClearStatics()
        {
            TimeControl.Reset();
            BattleRegistry.Clear();

            // 공격권도 씬을 넘어 살아남는다. 두고 가면 새 스테이지 첫 몇 초 동안
            // 지난 판의 임대가 정원을 차지해 아무도 공격하지 않는다.
            EnemyAttackTokens.Pool.ResetAll();
        }

        /// <summary>
        /// 새 배틀 씬이 올라온 뒤 도는 콜백. 이 시점에 this 는 이미 파괴돼 있다.
        /// <see cref="BattleRegistry"/> 를 여기서 비우면 안 된다 — 새 씬의 Entity 가
        /// 이미 등록을 마친 뒤이기 때문이다.
        /// </summary>
        private static void OnBattleReloaded()
        {
            AudioManager.Instance?.PlayBattleBgm();
        }

        /// <summary>
        /// UI/Cancel 액션을 본다. 기본 바인딩은 ESC 지만 리바인드로 바뀔 수 있다.
        ///
        /// 플레이어가 죽어도 계속 먹어야 한다 — <see cref="PlayerInputController"/> 는
        /// 비활성화될 때 UI 맵만은 끄지 않고, 사망은 오브젝트를 파괴하지 않으므로
        /// <c>Instance</c> 도 살아 있다.
        /// </summary>
        private static bool WasEscapePressed()
        {
            PlayerInputController input = PlayerInputController.Instance;
            return input != null && input.CancelPressed;
        }

        /// <summary>
        /// 씬이 통째로 언로드되므로 오브젝트 파괴는 불필요.
        /// 씬 경계를 넘어 살아남는 것들만 정리한다.
        /// </summary>
        private void CleanupStage()
        {
            StopAllCoroutines();

            // TODO: DOTween 등을 도입했다면 여기서 전역 트윈 Kill
        }

        /// <summary>
        /// 배틀 씬이 완전히 내려간 뒤 도는 콜백. 이 시점에 this 는 이미 파괴돼 있으므로
        /// 인스턴스 필드를 만지면 안 된다 — static 만 정리한다.
        /// </summary>
        private static void OnReturnedToMenu()
        {
            // BulletTimeController 가 Scale = 0 인 채로 파괴됐을 수 있다.
            // Domain Reload 가 꺼져 있으면 static 이 그대로 살아남아 다음 런이 멈춘 채 시작한다.
            TimeControl.Reset();

            // Entity 는 OnDestroy 에서 스스로 Unregister 하지만, 씬을 넘나드는 만큼
            // 잔여 항목이 남지 않도록 한 번 더 비운다.
            BattleRegistry.Clear();

            AudioManager.Instance?.PlayMenuBgm();
        }

        private void OnDestroy()
        {
            Debug.Log("[Battle] 씬 정리 완료");
        }
    }
}
