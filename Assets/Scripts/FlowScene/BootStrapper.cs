using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// Boot 씬에서 최초 1회만 실행되어 메인화면을 띄운다.
    /// Awake 가 아니라 Start 인 이유 — 다른 매니저의 Awake 가 먼저 끝나야 Instance 가 유효하다.
    /// </summary>
    public class BootStrapper : MonoBehaviour
    {
        private void Start()
        {
            if (SceneLoader.Instance == null)
            {
                Debug.LogError("[BootStrapper] SceneLoader 가 없다. Boot 씬 구성을 확인할 것.");
                return;
            }

            SceneLoader.Instance.LoadOnly(
                SceneNames.MainMenu,
                () => AudioManager.Instance?.PlayMenuBgm());
        }
    }
}
