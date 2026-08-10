using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Prototype.YG
{
    /// <summary>
    /// 배틀 씬의 지휘자. 지금 단계에서는 ESC 감지와 이탈 처리, 그리고 씬 초기화가 주 역할이다.
    /// 씬 자체가 통째로 언로드되므로 오브젝트 리셋 코드는 필요 없다.
    /// </summary>
    public class BattleSceneController : MonoBehaviour
    {
        private bool isExiting;
        private bool isRestarting;
        private bool isAdvancing;

        private void Start()
        {
            // 초기화 UI 는 씬 단독 실행에서도 필요하다. GameManager 체크보다 먼저 띄운다.
            BattleRestartUI.Create(this);

            // 씬 단독 실행 대응 — Boot 씬 없이 배틀 씬만 Play 했을 때
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[Battle] GameManager 없음. 씬 단독 실행 모드로 진행합니다.");
                return;
            }

            BeginStage();
        }

        private void BeginStage()
        {
            TimeControl.Reset();

            Debug.Log($"[Battle] 스테이지 시작 (index {GameManager.Instance.CurrentStageIndex})");

            // TODO: 턴 매니저 시작, 손패 드로우 등은 이후 단계에서 여기에 연결
        }

        private void Update()
        {
            if (isExiting || isRestarting || isAdvancing) return;
            if (WasEscapePressed()) ExitToMainMenu();
        }

        /// <summary>
        /// <see cref="StageExitTrigger"/>가 부른다. 다음 스테이지 씬으로 갈아끼운다.
        /// 마지막 스테이지를 넘어가면 런 클리어로 보고 메인화면으로 돌려보낸다.
        /// </summary>
        public void AdvanceToNextStage()
        {
            if (isExiting || isRestarting || isAdvancing) return;
            if (SceneLoader.Instance == null || SceneLoader.Instance.IsBusy) return;

            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[Battle] GameManager 없음 — 씬 단독 실행 모드에서는 스테이지 전환을 지원하지 않는다.");
                return;
            }

            int nextIndex = GameManager.Instance.CurrentStageIndex + 1;
            if (nextIndex >= SceneNames.Stages.Length)
            {
                Debug.Log("[Battle] 마지막 스테이지 클리어 — 메인화면으로 복귀");
                ExitToMainMenu();
                return;
            }

            isAdvancing = true;

            CleanupStage();
            GameManager.Instance.AdvanceStage();

            // RestartStage와 같은 이유로 로드 전에 비운다 — 새 스테이지의 Entity가
            // Start에서 스스로 등록하므로, 로드가 끝난 뒤에 비우면 갓 등록된 것까지 날아간다.
            TimeControl.Reset();
            BattleRegistry.Clear();

            string currentScene = gameObject.scene.name;
            string nextScene = SceneNames.Stages[nextIndex];

            Debug.Log($"[Battle] 다음 스테이지로 이동: {nextScene}");

            SceneLoader.Instance.SwapTo(
                loadScene:   nextScene,
                unloadScene: currentScene,
                onComplete:  OnBattleReloaded);
        }

        /// <summary>
        /// 인게임 씬을 처음부터 다시 시작한다. <see cref="BattleRestartUI"/> 의 확인 버튼이 부른다.
        ///
        /// 오브젝트를 하나씩 초기값으로 되돌리는 대신 씬을 통째로 내렸다 다시 올린다 —
        /// 리셋 누락이 원천적으로 생길 수 없는 방식이고, 이 프로젝트는 이미 ESC 이탈에서
        /// 같은 구조를 쓰고 있다.
        /// </summary>
        public void RestartStage()
        {
            if (isExiting || isRestarting) return;
            if (SceneLoader.Instance != null && SceneLoader.Instance.IsBusy) return;

            isRestarting = true;

            string currentScene = gameObject.scene.name;

            CleanupStage();

            // 씬 경계를 넘어 살아남는 static 은 반드시 <b>지금</b> 비운다.
            // 새 씬의 Entity 는 Start 에서 스스로 등록하므로, 로드가 끝난 뒤에 비우면
            // 갓 등록된 새 Entity 까지 날아가 타게팅이 통째로 죽는다.
            // 지금 비워도 곧 파괴될 옛 Entity 의 OnDestroy → Unregister 는 빈 목록에 대한
            // no-op 이라 안전하다.
            TimeControl.Reset();
            BattleRegistry.Clear();

            // ── 씬 단독 실행 모드: SceneLoader 가 없으니 현재 씬을 직접 다시 로드한다.
            if (SceneLoader.Instance == null)
            {
                Debug.Log("[Battle] 씬 단독 실행 모드 — 현재 씬을 다시 로드한다.");
                SceneManager.LoadScene(gameObject.scene.name);
                return;
            }

            Debug.Log("[Battle] 스테이지 초기화 — 씬을 다시 올린다.");

            // 런 데이터도 초기값으로 되돌린다. "처음부터"이므로 스테이지 · 경험치가 남으면 안 된다.
            GameManager.Instance?.StartNewRun();

            SceneLoader.Instance.SwapTo(
                loadScene:   SceneNames.Stages[0],
                unloadScene: currentScene,
                onComplete:  OnBattleReloaded);
        }

        /// <summary>
        /// 새 배틀 씬이 올라온 뒤 도는 콜백. 이 시점에 this 는 이미 파괴돼 있다.
        /// <see cref="BattleRegistry"/> 를 여기서 비우면 안 된다 — 새 씬의 Entity 가
        /// 이미 등록을 마친 뒤이기 때문이다.
        /// </summary>
        private static void OnBattleReloaded()
        {
            AudioManager.Instance?.PlayBattleBgm();

            // 새로 로드된 스테이지에서 카메라가 이전 위치부터 미끄러져 오지 않도록 즉시 붙인다.
            FindAnyObjectByType<CameraFollow>()?.SnapToTarget();
        }

        /// <summary>
        /// Active Input Handling 이 Input System Package 로 잡혀 있으면 레거시
        /// <c>Input.GetKeyDown</c> 은 예외를 던진다. 양쪽 모두 대응해 둔다.
        /// (현재 프로젝트 설정: activeInputHandler = 1, 즉 Input System 전용)
        /// </summary>
        private static bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private void ExitToMainMenu()
        {
            if (SceneLoader.Instance == null) return;
            if (SceneLoader.Instance.IsBusy) return;

            isExiting = true;

            string currentScene = gameObject.scene.name;

            CleanupStage();
            GameManager.Instance?.EndRun();

            SceneLoader.Instance.SwapTo(
                loadScene:   SceneNames.MainMenu,
                unloadScene: currentScene,
                onComplete:  OnReturnedToMenu);
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
