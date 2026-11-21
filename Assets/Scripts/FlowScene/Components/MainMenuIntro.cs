using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 타이틀 화면 등장 연출.
    /// <list type="number">
    /// <item>"Prototype" 타이틀이 위에서 내려와 부드럽게 자리 잡는다.</item>
    /// <item>이어서 버튼들이 좌측 화면 밖에서 한꺼번에 미끄러져 들어온다.</item>
    /// </list>
    /// 배치는 씬에 놓인 자리(=최종 위치)를 그대로 쓰고, 시작 시 거기서 오프셋만큼 밀어 둔 뒤 되돌린다.
    /// 일시정지 · 슬로모와 무관하게 unscaled 시간을 쓴다.
    /// </summary>
    public class MainMenuIntro : MonoBehaviour
    {
        [Header("타이틀")]
        [SerializeField] private RectTransform title;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField, Min(0f)] private float titleDelay = 0.25f;
        [SerializeField, Min(0.01f)] private float titleDuration = 0.9f;
        [Tooltip("최종 위치보다 얼마나 위에서 출발하는지(캔버스 단위).")]
        [SerializeField] private float titleDropHeight = 650f;

        [Header("버튼")]
        [SerializeField] private RectTransform[] buttons;
        [SerializeField] private CanvasGroup[] buttonGroups;
        [Tooltip("타이틀이 출발한 뒤 버튼들이 출발하기까지(초).")]
        [SerializeField, Min(0f)] private float buttonsDelay = 0.85f;
        [SerializeField, Min(0.01f)] private float buttonDuration = 0.5f;
        [Tooltip("최종 위치보다 얼마나 왼쪽에서 출발하는지(캔버스 단위).")]
        [SerializeField] private float buttonSlideDistance = 900f;

        private Vector2 titleRest;
        private Vector2[] buttonRest;
        private float startTime;
        private bool done;

        private void Awake()
        {
            if (title != null) titleRest = title.anchoredPosition;

            buttonRest = new Vector2[buttons != null ? buttons.Length : 0];
            for (int i = 0; i < buttonRest.Length; i++)
                if (buttons[i] != null) buttonRest[i] = buttons[i].anchoredPosition;
        }

        private void OnEnable()
        {
            startTime = Time.unscaledTime;
            done = false;
            Apply(0f);
        }

        private void Update()
        {
            if (done) return;
            Apply(Time.unscaledTime - startTime);
        }

        private void Apply(float elapsed)
        {
            bool finished = true;

            if (title != null)
            {
                float p = Progress(elapsed - titleDelay, titleDuration, ref finished);
                title.anchoredPosition = titleRest + Vector2.up * (titleDropHeight * (1f - EaseOutCubic(p)));
                if (titleGroup != null) titleGroup.alpha = Mathf.Clamp01(p * 3f);
            }

            for (int i = 0; i < buttonRest.Length; i++)
            {
                if (buttons[i] == null) continue;
                float p = Progress(elapsed - titleDelay - buttonsDelay, buttonDuration, ref finished);
                buttons[i].anchoredPosition = buttonRest[i] + Vector2.left * (buttonSlideDistance * (1f - EaseOutCubic(p)));

                if (buttonGroups != null && i < buttonGroups.Length && buttonGroups[i] != null)
                {
                    buttonGroups[i].alpha = Mathf.Clamp01(p * 2f);
                    // 날아오는 도중에도 바로 누를 수 있다 (출발 전엔 화면 밖이라 눌릴 일이 없다)
                    buttonGroups[i].interactable = true;
                    buttonGroups[i].blocksRaycasts = true;
                }
            }

            done = finished;
        }

        private static float Progress(float t, float duration, ref bool finished)
        {
            float p = Mathf.Clamp01(t / duration);
            if (p < 1f) finished = false;
            return p;
        }

        /// <summary>빠르게 출발해 부드럽게 멈춘다. 지나치거나 튕기지 않는다.</summary>
        internal static float EaseOutCubic(float x)
        {
            float u = 1f - Mathf.Clamp01(x);
            return 1f - u * u * u;
        }
    }
}
