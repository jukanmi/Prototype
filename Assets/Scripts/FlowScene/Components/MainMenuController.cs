using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Prototype
{
    /// <summary>
    /// 타이틀 버튼 배선. MainMenu 씬에만 존재하며 씬과 함께 언로드된다.
    /// [시작]은 마을(Lobby)로 간다 — 런과 런 사이는 마을이 맡는다.
    /// <para>
    /// 키보드: 방향키(또는 W/S)로 버튼을 고른다 — 아무것도 안 골라져 있으면 첫 입력은 [시작]에 선다.
    /// 선택된 버튼엔 <see cref="HoverUnderline"/>이 밑줄을 긋고, Enter는 EventSystem의 Submit이 눌러 준다.
    /// 버튼 간 이동은 여기서만 한다 — 버튼 자체 Navigation은 꺼서 입력 모듈과 이중으로 움직이지 않게 한다.
    /// </para>
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

            DisableAutoNavigation(startButton);
            DisableAutoNavigation(quitButton);
        }

        private void Update()
        {
            int dir = ReadVerticalPress();
            if (dir == 0) return;

            var es = EventSystem.current;
            if (es == null) return;

            Button[] order = { startButton, quitButton };
            int current = System.Array.FindIndex(order,
                b => b != null && es.currentSelectedGameObject == b.gameObject);

            // 아무것도 안 골라져 있으면 방향과 상관없이 [시작]부터.
            int next = current < 0 ? 0 : (current + dir + order.Length) % order.Length;
            if (order[next] != null) es.SetSelectedGameObject(order[next].gameObject);
        }

        /// <summary>이번 프레임에 눌린 세로 방향. 위 = -1(앞 버튼), 아래 = +1(뒤 버튼), 없으면 0.</summary>
        private static int ReadVerticalPress()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return 0;
            if (kb.upArrowKey.wasPressedThisFrame   || kb.wKey.wasPressedThisFrame) return -1;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) return 1;
            return 0;
#else
            if (Input.GetKeyDown(KeyCode.UpArrow)   || Input.GetKeyDown(KeyCode.W)) return -1;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return 1;
            return 0;
#endif
        }

        private static void DisableAutoNavigation(Button button)
        {
            if (button == null) return;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
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

            // 타이틀에서 들어갈 때만 주인공이 발밑에서 솟아오른다. 런을 끝내고 돌아올 때는 그냥 서 있다.
            LobbyController.RequestRiseIntro();

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
