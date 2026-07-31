using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Prototype.YG
{
    /// <summary>
    /// 배틀 씬의 지휘자. 지금 단계에서는 ESC 감지와 이탈 처리가 주 역할이다.
    /// 씬 자체가 통째로 언로드되므로 오브젝트 리셋 코드는 필요 없다.
    /// </summary>
    public class BattleSceneController : MonoBehaviour
    {
        private bool isExiting;

        private void Start()
        {
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
            if (isExiting) return;
            if (WasEscapePressed()) ExitToMainMenu();
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

            CleanupStage();
            GameManager.Instance?.EndRun();

            SceneLoader.Instance.SwapTo(
                loadScene:   SceneNames.MainMenu,
                unloadScene: SceneNames.Battle,
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
