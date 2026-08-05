using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>아군이 가장 최근에 유효 타격한 적 한 명의 체력을 하단에 표시한다.</summary>
    public class RecentHitEnemyHUD : MonoBehaviour
    {
        private const float VisibleDuration = 3f;

        private GameObject panel;
        private Text nameLabel;
        private RectTransform healthFill;
        private Combat currentTarget;
        private float expiresAt;

        public Combat CurrentTarget => currentTarget;
        public bool IsVisible => panel != null && panel.activeSelf;

        /// <summary>체력 바가 실제로 그려지는 비율. 0~1.</summary>
        public float HealthFillRatio => healthFill != null ? healthFill.anchorMax.x : 0f;

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
        }

        private void OnEnable() => Combat.OnAnyHitLanded += Track;

        private void OnDisable()
        {
            Combat.OnAnyHitLanded -= Track;
            UnsubscribeTarget();
        }

        private void Update() => TickExpiry(Time.unscaledTime);

        /// <summary>
        /// 표시 시간 만료 검사. 현재 시각을 인자로 받는다 —
        /// 에디트모드 테스트가 3초를 실제로 기다리지 않고 앞당길 수 있어야 한다.
        /// </summary>
        public void TickExpiry(float unscaledNow)
        {
            if (currentTarget != null && unscaledNow >= expiresAt)
                Clear();
        }

        public void Track(Combat attacker, Combat target)
        {
            if (attacker?.Owner?.Faction != Faction.Ally ||
                target?.Owner?.Faction != Faction.Enemy || target.IsDead) return;

            if (currentTarget != target)
            {
                UnsubscribeTarget();
                currentTarget = target;
                currentTarget.OnDead += Clear;
                currentTarget.Health.OnChanged += HandleHealthChanged;
            }

            expiresAt = Time.unscaledTime + VisibleDuration;
            Refresh();
            SetVisible(true);
        }

        private void HandleHealthChanged(Energy _) => Refresh();

        private void Refresh()
        {
            if (currentTarget == null || currentTarget.IsDead)
            {
                Clear();
                return;
            }

            nameLabel.text = currentTarget.name;
            SetFillRatio(currentTarget.Health.Ratio);
        }

        /// <summary>
        /// 오른쪽 앵커를 움직여 폭을 줄인다.
        /// Image.fillAmount는 스프라이트가 있을 때만 동작한다 — 이 HUD는 스프라이트 없이
        /// 코드로만 만들기 때문에 sprite가 null이고, 그 경우 Image는 type/fillAmount를
        /// 무시하고 RectTransform 전체를 채운다. 그래서 폭을 직접 그린다.
        /// </summary>
        private void SetFillRatio(float ratio)
        {
            if (healthFill == null) return;
            healthFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }

        private void Clear()
        {
            UnsubscribeTarget();
            currentTarget = null;
            SetVisible(false);
        }

        private void UnsubscribeTarget()
        {
            if (currentTarget == null) return;
            currentTarget.OnDead -= Clear;
            currentTarget.Health.OnChanged -= HandleHealthChanged;
        }

        private void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("RecentHitEnemyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            panel = new GameObject("RecentHitEnemyPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0.06f, 0.06f, 0.08f, 0.88f);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 210f);
            panelRect.sizeDelta = new Vector2(280f, 54f);

            nameLabel = CreateText(panel.transform, "Name", 16, TextAnchor.UpperLeft);
            RectTransform nameRect = nameLabel.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.5f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(14f, 0f);
            nameRect.offsetMax = new Vector2(-14f, -4f);

            var bar = new GameObject("HealthBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(panel.transform, false);
            bar.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.21f, 1f);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.offsetMin = new Vector2(14f, 10f);
            barRect.offsetMax = new Vector2(-14f, 20f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bar.transform, false);
            fill.GetComponent<Image>().color = new Color(0.91f, 0.25f, 0.22f, 1f);

            // 왼쪽 고정, 오른쪽 앵커만 움직여 폭을 만든다. SetFillRatio가 anchorMax.x를 쓴다.
            healthFill = fill.GetComponent<RectTransform>();
            healthFill.anchorMin = Vector2.zero;
            healthFill.anchorMax = Vector2.one;
            healthFill.pivot = new Vector2(0f, 0.5f);
            healthFill.offsetMin = Vector2.zero;
            healthFill.offsetMax = Vector2.zero;
        }

        private static Text CreateText(Transform parent, string name, int size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
