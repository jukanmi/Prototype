using UnityEngine;
using UnityEngine.UI;

namespace Prototype.YG
{
    /// <summary>
    /// 메인화면 버튼 배선. MainMenu 씬에만 존재하며 씬과 함께 언로드된다.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            else Debug.LogError("[MainMenu] startButton 미연결.");

            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
            else Debug.LogError("[MainMenu] quitButton 미연결.");
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
            if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        private void OnStartClicked()
        {
            if (SceneLoader.Instance == null) return;
            if (SceneLoader.Instance.IsBusy) return;   // 연타 방지

            GameManager.Instance?.StartNewRun();

            // onComplete 는 배틀 씬 로드 완료 시점에 불린다. 그때 이 컴포넌트는 이미 파괴된 뒤다.
            // 그래서 콜백 안에서 this 의 필드를 참조하면 안 된다 — 싱글톤만 만진다.
            SceneLoader.Instance.SwapTo(
                loadScene:   SceneNames.Battle,
                unloadScene: SceneNames.MainMenu,
                onComplete:  () => AudioManager.Instance?.PlayBattleBgm());
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
