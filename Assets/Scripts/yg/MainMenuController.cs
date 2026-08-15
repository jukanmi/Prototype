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

            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[MainMenu] GameManager 없음. 스테이지를 알 수 없어 시작할 수 없다.");
                return;
            }

            gm.StartNewRun();

            // 어느 씬을 여는지는 GameManager 의 스테이지 목록이 정한다.
            // 여기서 상수로 박으면 첫 스테이지를 바꿀 때 고칠 곳이 둘이 된다.
            string first = gm.CurrentStageScene;
            if (string.IsNullOrEmpty(first))
            {
                Debug.LogError("[MainMenu] 스테이지 목록이 비어 있다.");
                return;
            }

            // onComplete 는 배틀 씬 로드 완료 시점에 불린다. 그때 이 컴포넌트는 이미 파괴된 뒤다.
            // 그래서 콜백 안에서 this 의 필드를 참조하면 안 된다 — 싱글톤만 만진다.
            SceneLoader.Instance.SwapTo(
                loadScene:   first,
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
