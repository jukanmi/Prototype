using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 타이틀 버튼 배선. MainMenu 씬에만 존재하며 씬과 함께 언로드된다.
    /// [시작]은 마을(Lobby)로 간다 — 런과 런 사이는 마을이 맡는다.
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

        /// <summary>
        /// 마을로 간다. 런은 여기서 열지 않는다 — 편성 · 출발은 마을 오브젝트가 맡는다
        /// (<see cref="LobbyController"/>, 계획서 docs/Lobby_Plan.md).
        /// </summary>
        private void OnStartClicked()
        {
            if (SceneLoader.Instance == null) return;
            if (SceneLoader.Instance.IsBusy) return;   // 연타 방지

            SceneLoader.Instance.SwapTo(
                loadScene:   SceneNames.Lobby,
                unloadScene: SceneNames.MainMenu,
                onComplete:  () => AudioManager.Instance?.PlayMenuBgm());
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
