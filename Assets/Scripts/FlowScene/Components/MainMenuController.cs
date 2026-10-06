using UnityEngine;
using UnityEngine.UI;

namespace Prototype
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

        /// <summary>
        /// 파티 선택을 붙인다. 스스로 캔버스를 짓고, 고를 조합이 하나뿐이면 스스로 숨으므로
        /// 여기서 조건을 볼 것이 없다 — MainMenu 씬을 다시 굽지 않아도 붙는다.
        /// </summary>
        private void Start()
        {
            PartySelectUI.Create(this);
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

            // 파티를 골랐는데 아무도 안 데려가는 조합이면 막는다. 그대로 들어가면 덱이 0장이라
            // U키도 불릿타임도 아무것도 안 나가고, 그건 선택이 아니라 고장으로 보인다.
            //
            // Loadout 이 null 인 것은 <b>안 고른 것</b>이지 빈 것이 아니다 —
            // 표를 아직 안 구운 상태라 StartNewRun 이 기본 조합으로 채운다.
            if (gm.Loadout != null && !PartyAssembleRules.CanStart(gm.Loadout.members))
            {
                Debug.LogWarning("[MainMenu] 동료를 한 명 이상 골라야 시작할 수 있다.");
                return;
            }

            // 지도를 못 굴리면(레시피 미배선 · 검사 실패) GameManager 가 이유를 에러로 남기고 거짓을 준다.
            if (!gm.StartNewRun()) return;

            // 첫 전투가 아니라 지도로 간다. 무엇과 먼저 싸울지는 지도의 0층이 정한다.
            // onComplete 는 지도 씬 로드 완료 시점에 불린다. 그때 이 컴포넌트는 이미 파괴된 뒤다.
            // 그래서 콜백 안에서 this 의 필드를 참조하면 안 된다 — 싱글톤만 만진다.
            SceneLoader.Instance.SwapTo(
                loadScene:   SceneNames.RunMap,
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
