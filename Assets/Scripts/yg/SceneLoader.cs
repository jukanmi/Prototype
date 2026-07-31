using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.YG
{
    /// <summary>
    /// 씬의 <b>존재 자체</b>만 관리한다. 씬 내부 로직에는 관여하지 않는다.
    /// Boot 씬에 상주하며 Additive 로 메뉴 / 배틀 씬을 갈아끼운다.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [SerializeField] private CanvasGroup fadeCanvas;
        [SerializeField] private float fadeDuration = 0.3f;

        /// <summary>전환 진행 중 여부. 버튼 연타 · ESC 연타를 막는 데 쓴다.</summary>
        public bool IsBusy { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (fadeCanvas != null)
            {
                fadeCanvas.alpha = 0f;
                fadeCanvas.blocksRaycasts = false;
            }
            else
            {
                Debug.LogWarning("[SceneLoader] fadeCanvas 미연결 — 페이드 없이 즉시 전환된다.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>현재 씬을 내리고 새 씬을 얹는다. 전환 중 재호출은 무시된다.</summary>
        public void SwapTo(string loadScene, string unloadScene, Action onComplete = null)
        {
            if (IsBusy)
            {
                Debug.LogWarning($"[SceneLoader] 전환 중 재호출 무시: {loadScene}");
                return;
            }

            StartCoroutine(SwapRoutine(loadScene, unloadScene, onComplete));
        }

        /// <summary>씬을 얹기만 한다 (최초 진입용).</summary>
        public void LoadOnly(string sceneName, Action onComplete = null)
        {
            if (IsBusy) return;

            StartCoroutine(SwapRoutine(sceneName, null, onComplete));
        }

        private IEnumerator SwapRoutine(string loadScene, string unloadScene, Action onComplete)
        {
            IsBusy = true;

            yield return Fade(1f);   // 화면 가리기

            // ── 1) 이전 씬 언로드 (메모리 피크를 낮추려고 로드보다 먼저 한다)
            if (!string.IsNullOrEmpty(unloadScene))
            {
                Scene old = SceneManager.GetSceneByName(unloadScene);
                if (old.IsValid() && old.isLoaded)
                {
                    AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(old);
                    while (unloadOp != null && !unloadOp.isDone) yield return null;
                }

                yield return Resources.UnloadUnusedAssets();
            }

            // ── 2) 새 씬 로드
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(loadScene, LoadSceneMode.Additive);
            while (loadOp != null && !loadOp.isDone) yield return null;

            // ── 3) 활성 씬 지정 (필수)
            //     지정하지 않으면 Instantiate 로 만든 오브젝트가 Boot 씬에 생성된다.
            //     그러면 배틀 씬을 언로드해도 몬스터 · 이펙트가 남아 다음 판에 유령처럼 나타난다.
            Scene loaded = SceneManager.GetSceneByName(loadScene);
            if (loaded.IsValid()) SceneManager.SetActiveScene(loaded);

            // ── 4) 씬 내부 Awake / Start 가 모두 돌 시간을 준다
            yield return null;

            onComplete?.Invoke();

            yield return Fade(0f);   // 화면 열기

            IsBusy = false;
        }

        private IEnumerator Fade(float target)
        {
            if (fadeCanvas == null) yield break;

            fadeCanvas.blocksRaycasts = true;

            float start = fadeCanvas.alpha;
            float t = 0f;

            while (t < fadeDuration)
            {
                // 불릿타임(TimeControl.Scale = 0) 중에 이탈해도 페이드는 돌아야 한다.
                t += Time.unscaledDeltaTime;
                fadeCanvas.alpha = Mathf.Lerp(start, target, t / fadeDuration);
                yield return null;
            }

            fadeCanvas.alpha = target;
            fadeCanvas.blocksRaycasts = target > 0.5f;
        }
    }
}
