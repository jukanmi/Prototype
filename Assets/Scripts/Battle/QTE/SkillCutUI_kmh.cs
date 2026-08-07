using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// QTE 판정이 Good/Perfect로 성공했을 때, 방금 발동한 스킬 카드를 화면 중앙에 잠깐 띄우는 연출.
    /// BulletTimeQTEUI와 같은 방식으로 코드로만 짓는다 — 프리팹 · 씬 연결이 필요 없다.
    /// <see cref="ComboExecutor"/>가 QTE 판정 직후 <see cref="Show"/>만 호출하면 되고,
    /// 등장 → 유지 → 퇴장은 이 컴포넌트가 자체 코루틴으로 처리하므로 실행 흐름을 막지 않는다.
    /// </summary>
    public class SkillCutUI : MonoBehaviour
    {
        private const float CardWidth = 220f;
        private const float CardHeight = 328f;

        [Tooltip("카드가 다 나타난 뒤 화면에 머무는 시간(비배율 초).")]
        [SerializeField] private float holdTime = 0.5f;
        [Tooltip("확대되며 나타나는 데 걸리는 시간(비배율 초).")]
        [SerializeField] private float popInTime = 0.12f;
        [Tooltip("사라지는 데 걸리는 시간(비배율 초).")]
        [SerializeField] private float popOutTime = 0.18f;

        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.9f);
        private static readonly Color PerfectColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color GoodColor = new Color(0.6f, 0.85f, 1f);
        private static readonly Color PerfectPanelColor = new Color(0.4f, 0.32f, 0.08f, 0.9f);
        private static readonly Color GoodPanelColor = new Color(0.12f, 0.22f, 0.32f, 0.9f);

        private GameObject _root;
        private RectTransform _card;
        private Image _frame;
        private Image _art;
        private Text _nameLabel;
        private Text _tierLabel;
        private CanvasGroup _group;

        private Coroutine _running;

        private void Awake() => BuildUI();

        private void BuildUI()
        {
            var canvasGo = new GameObject("SkillCutCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11; // QTE 바(10)보다도 위에 뜬다.

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            _root = new GameObject("Card", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(canvasGo.transform, false);
            _frame = _root.GetComponent<Image>();
            _frame.color = PanelColor;

            _card = _root.GetComponent<RectTransform>();
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(CardWidth, CardHeight);
            _card.anchoredPosition = Vector2.zero;

            _group = _root.AddComponent<CanvasGroup>();

            var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(_root.transform, false);
            var artRect = artGo.GetComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0f, 0.16f);
            artRect.anchorMax = new Vector2(1f, 1f);
            artRect.offsetMin = new Vector2(6f, 6f);
            artRect.offsetMax = new Vector2(-6f, -6f);
            _art = artGo.GetComponent<Image>();
            _art.preserveAspect = true;
            _art.raycastTarget = false;

            _nameLabel = BuildText(_root.transform, "Name", new Vector2(0.5f, 0f), new Vector2(0f, 12f),
                CardWidth - 12f, 26f, 18, Color.white);

            _tierLabel = BuildText(_root.transform, "Tier", new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                CardWidth - 12f, 22f, 15, PerfectColor);

            _root.SetActive(false);
        }

        private static Text BuildText(Transform parent, string name, Vector2 anchor, Vector2 offset,
            float width, float height, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = offset;

            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        // ── ComboExecutor가 부르는 창구 ──────────────

        /// <summary>
        /// QTE가 Good/Perfect로 끝난 직후 호출. 이미 재생 중이면 새 카드로 갈아탄다 —
        /// 콤보가 빠르게 이어질 때 이전 컷이 끝나기를 기다리지 않는다.
        /// </summary>
        public void Show(SkillData data, BulletTimeQTETier tier)
        {
            if (data == null || tier == BulletTimeQTETier.Miss) return;

            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Play(data, tier));
        }

        private IEnumerator Play(SkillData data, BulletTimeQTETier tier)
        {
            bool hasArt = data.icon != null;
            _art.enabled = hasArt;
            if (hasArt) _art.sprite = data.icon;
            _nameLabel.enabled = !hasArt;
            _nameLabel.text = data.skillName;

            bool perfect = tier == BulletTimeQTETier.Perfect;
            _tierLabel.text = perfect ? "PERFECT!" : "GOOD";
            _tierLabel.color = perfect ? PerfectColor : GoodColor;
            _frame.color = perfect ? PerfectPanelColor : GoodPanelColor;

            _root.SetActive(true);

            yield return Lerp(popInTime, 0.6f, 1f, 0f, 1f);

            float hold = 0f;
            while (hold < holdTime)
            {
                hold += TimeControl.UnscaledDeltaTime;
                yield return null;
            }

            yield return Lerp(popOutTime, 1f, 0.85f, 1f, 0f);

            _root.SetActive(false);
            _running = null;
        }

        /// <summary>실시간 슬로우와 무관하게 항상 같은 속도로 재생 — UnscaledDeltaTime을 쓴다.</summary>
        private IEnumerator Lerp(float duration, float fromScale, float toScale, float fromAlpha, float toAlpha)
        {
            float t = 0f;
            duration = Mathf.Max(0.001f, duration);

            while (t < duration)
            {
                float u = t / duration;
                float s = Mathf.Lerp(fromScale, toScale, u);
                _card.localScale = new Vector3(s, s, 1f);
                _group.alpha = Mathf.Lerp(fromAlpha, toAlpha, u);

                t += TimeControl.UnscaledDeltaTime;
                yield return null;
            }

            _card.localScale = new Vector3(toScale, toScale, 1f);
            _group.alpha = toAlpha;
        }
    }
}
