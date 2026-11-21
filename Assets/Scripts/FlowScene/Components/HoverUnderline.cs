using UnityEngine;
using UnityEngine.EventSystems;

namespace Prototype
{
    /// <summary>
    /// 버튼에 커서를 대면(또는 키보드/패드로 선택되면) 밑줄이 가운데에서 양옆으로 펼쳐진다.
    /// 커서가 빠지면 다시 가운데로 접힌다. 타이틀 화면 버튼용.
    /// <para>
    /// <see cref="underline"/>는 피벗이 가운데인 RectTransform이어야 한다 — 가로 스케일로 폭을 조절한다.
    /// 일시정지 · 슬로모와 무관하게 움직이도록 unscaled 시간을 쓴다.
    /// </para>
    /// </summary>
    public class HoverUnderline : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private RectTransform underline;
        [SerializeField] private CanvasGroup underlineGroup;

        [Tooltip("펼쳐지는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.01f)] private float expandDuration = 0.18f;

        [Tooltip("접히는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.01f)] private float collapseDuration = 0.12f;

        private bool hovered;
        private bool selected;
        private float progress;   // 0 = 접힘, 1 = 다 펼쳐짐 (이징 전 선형값)

        private bool Active => hovered || selected;

        private void OnEnable()
        {
            hovered = selected = false;
            progress = 0f;
            Apply();
        }

        private void Update()
        {
            float target = Active ? 1f : 0f;
            if (Mathf.Approximately(progress, target)) return;

            float duration = Active ? expandDuration : collapseDuration;
            progress = Mathf.MoveTowards(progress, target, Time.unscaledDeltaTime / duration);
            Apply();
        }

        private void Apply()
        {
            float t = EaseOutCubic(progress);
            if (underline != null)
                underline.localScale = new Vector3(t, 1f, 1f);
            if (underlineGroup != null)
                underlineGroup.alpha = Mathf.Clamp01(t * 1.5f);
        }

        private static float EaseOutCubic(float x)
        {
            float inv = 1f - x;
            return 1f - inv * inv * inv;
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            // 마우스로 클릭하면 버튼이 '선택' 상태로 남아 밑줄이 안 접힌다 — 커서가 떠나면 선택도 푼다.
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == gameObject)
                es.SetSelectedGameObject(null);
        }
        public void OnSelect(BaseEventData eventData)          => selected = true;
        public void OnDeselect(BaseEventData eventData)        => selected = false;
    }
}
