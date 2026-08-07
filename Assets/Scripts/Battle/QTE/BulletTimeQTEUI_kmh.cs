using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// <see cref="BulletTimeQTEController"/>가 그리는 타이밍 바.
    /// ComboBoardUI와 같은 방식으로 <b>코드로만</b> 짓는다 — 프리팹 · 씬 연결이 필요 없다.
    /// </summary>
    public class BulletTimeQTEUI : MonoBehaviour
    {
        private const float BarWidth = 420f;
        private const float BarHeight = 22f;
        private const float CursorWidth = 5f;

        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.85f);
        private static readonly Color BarColor = new Color(0.2f, 0.2f, 0.24f);
        private static readonly Color GoodZoneColor = new Color(0.85f, 0.7f, 0.25f, 0.55f);
        private static readonly Color PerfectZoneColor = new Color(1f, 0.85f, 0.3f, 0.9f);
        private static readonly Color CursorColor = Color.white;
        private static readonly Color MissColor = new Color(0.75f, 0.4f, 0.4f);
        private static readonly Color GoodResultColor = new Color(0.95f, 0.85f, 0.4f);
        private static readonly Color PerfectResultColor = new Color(0.4f, 0.95f, 0.6f);

        private GameObject _root;
        private RectTransform _bar;
        private RectTransform _goodZone;
        private RectTransform _perfectZone;
        private RectTransform _cursor;
        private Text _nameLabel;
        private Text _resultLabel;

        private void Awake() => BuildUI();

        private void BuildUI()
        {
            var canvasGo = new GameObject("BulletTimeQTECanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // 손패 UI(ComboBoardUI)보다 위에 뜬다.

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            _root = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(canvasGo.transform, false);
            _root.GetComponent<Image>().color = PanelColor;

            var panelRect = _root.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.68f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(BarWidth + 32f, 70f);
            panelRect.anchoredPosition = Vector2.zero;

            _nameLabel = BuildText(_root.transform, "Name", new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                BarWidth, 20f, 15, Color.white);

            var barGo = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(_root.transform, false);
            barGo.GetComponent<Image>().color = BarColor;
            _bar = barGo.GetComponent<RectTransform>();
            _bar.anchorMin = _bar.anchorMax = new Vector2(0.5f, 0.5f);
            _bar.pivot = new Vector2(0.5f, 0.5f);
            _bar.sizeDelta = new Vector2(BarWidth, BarHeight);
            _bar.anchoredPosition = new Vector2(0f, -4f);

            _goodZone = BuildZone(_bar, "GoodZone", GoodZoneColor);
            _perfectZone = BuildZone(_bar, "PerfectZone", PerfectZoneColor);

            var cursorGo = new GameObject("Cursor", typeof(RectTransform), typeof(Image));
            cursorGo.transform.SetParent(_bar, false);
            cursorGo.GetComponent<Image>().color = CursorColor;
            _cursor = cursorGo.GetComponent<RectTransform>();
            _cursor.anchorMin = new Vector2(0f, 0f);
            _cursor.anchorMax = new Vector2(0f, 1f);
            _cursor.pivot = new Vector2(0.5f, 0.5f);
            _cursor.sizeDelta = new Vector2(CursorWidth, 0f);
            _cursor.anchoredPosition = Vector2.zero;

            _resultLabel = BuildText(_root.transform, "Result", new Vector2(0.5f, 0f), new Vector2(0f, 14f),
                BarWidth, 22f, 16, Color.white);

            _root.SetActive(false);
        }

        private static RectTransform BuildZone(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            return rect;
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

        // ── BulletTimeQTEController가 부르는 창구 ──────────────

        public void Show(SkillData data, Ally caster, float zoneCenter, float perfectWidth, float goodWidth)
        {
            _root.SetActive(true);

            _nameLabel.text = data != null
                ? $"{(caster != null ? BattleLog.Name(caster) : "")} — {data.skillName}"
                : "?";
            _resultLabel.text = string.Empty;

            SetZone(_goodZone, zoneCenter, goodWidth);
            SetZone(_perfectZone, zoneCenter, perfectWidth);
            SetCursor(0f);
        }

        private static void SetZone(RectTransform zone, float center, float width)
        {
            float left = Mathf.Clamp01(center - width * 0.5f);
            zone.anchoredPosition = new Vector2(left * BarWidth, 0f);
            zone.sizeDelta = new Vector2(width * BarWidth, 0f);
        }

        public void SetCursor(float t)
        {
            _cursor.anchoredPosition = new Vector2(Mathf.Clamp01(t) * BarWidth, 0f);
        }

        public void ShowResult(BulletTimeQTETier tier)
        {
            switch (tier)
            {
                case BulletTimeQTETier.Perfect:
                    _resultLabel.text = "PERFECT!";
                    _resultLabel.color = PerfectResultColor;
                    break;
                case BulletTimeQTETier.Good:
                    _resultLabel.text = "GOOD";
                    _resultLabel.color = GoodResultColor;
                    break;
                default:
                    _resultLabel.text = "MISS";
                    _resultLabel.color = MissColor;
                    break;
            }
        }

        public void Hide() => _root.SetActive(false);
    }
}
