using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 바닥의 스킬 두루마리(<see cref="LootPickup"/>)에 다가가면 그 위에 뜨는 설명 창 —
    /// 이름 · 직업 · 성격 · 설명, 그리고 이미 가진 장수에 하나 더 받으면 어떻게 되는지.
    ///
    /// 씬에 하나, 처음 부를 때 스스로 짓는다(활성 씬에 지어져 그 씬과 함께 내려간다).
    /// 두루마리마다 매 프레임 <see cref="Track"/>으로 "지금 내 차례인가"를 알린다 — 키 안내가 뜬 하나만 보인다.
    /// </summary>
    public class SkillTooltip : MonoBehaviour
    {
        private const float Width = 440f;
        private const float Inset = 18f;
        private const float IconSize = 64f;

        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.94f);
        private static readonly Color EdgeColor = new Color(1f, 0.86f, 0.45f, 0.9f);
        private static readonly Color NameColor = new Color(1f, 0.86f, 0.45f);
        private static readonly Color SubColor = new Color(0.66f, 0.70f, 0.75f);
        private static readonly Color BodyColor = new Color(0.88f, 0.90f, 0.93f);
        private static readonly Color OwnedColor = new Color(0.55f, 0.85f, 0.60f);

        private static SkillTooltip instance;

        private RectTransform canvasRect;
        private RectTransform panel;
        private Image icon;
        private Text title;
        private Text sub;
        private Text body;
        private Text owned;
        private Text hint;

        private LootPickup owner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        /// <summary>
        /// 두루마리가 매 프레임 부른다. <paramref name="focused"/>면 이 두루마리 설명을 띄우고,
        /// 아니면 — 지금 떠 있는 게 이 두루마리 것일 때만 — 걷는다.
        /// </summary>
        public static void Track(LootPickup who, SkillData skill, bool focused)
        {
            if (focused)
            {
                if (instance == null) instance = Create();
                instance.Show(who, skill);
            }
            else if (instance != null && instance.owner == who)
            {
                instance.Hide();
            }
        }

        /// <summary>두루마리가 사라진다. 그 두루마리 설명이 떠 있었으면 걷는다.</summary>
        public static void Release(LootPickup who)
        {
            if (instance != null && instance.owner == who) instance.Hide();
        }

        private static SkillTooltip Create()
        {
            var go = new GameObject(nameof(SkillTooltip), typeof(RectTransform));
            SkillTooltip tip = go.AddComponent<SkillTooltip>();
            tip.Build();
            return tip;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ── 짓기 ────────────────────────────────────────

        private void Build()
        {
            UiKit.BuildCanvas(gameObject, UiLayer.LootTooltip, match: 1f, raycaster: false);
            canvasRect = (RectTransform)transform;

            Image edge = UiKit.NewImage(transform, "Tooltip", EdgeColor);
            panel = edge.rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);

            Image fill = UiKit.NewImage(panel, "Fill", PanelColor);
            UiKit.Stretch(fill.rectTransform, 2f);

            icon = UiKit.NewImage(panel, "Icon", Color.white);
            icon.preserveAspect = true;
            UiKit.Place(icon, new Vector2(0f, 1f), new Vector2(Inset + IconSize * 0.5f, -Inset - IconSize * 0.5f),
                        new Vector2(IconSize, IconSize));

            title = Line("Name", 28, NameColor, FontStyle.Bold);
            sub = Line("Sub", 18, SubColor, FontStyle.Normal);
            body = Line("Body", 20, BodyColor, FontStyle.Normal);
            owned = Line("Owned", 18, OwnedColor, FontStyle.Normal);
            hint = Line("Hint", 18, SubColor, FontStyle.Normal);

            panel.gameObject.SetActive(false);
        }

        private Text Line(string name, int fontSize, Color color, FontStyle style)
        {
            Text t = UiKit.NewText(panel, name, fontSize, color, style);
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = t.rectTransform.pivot = new Vector2(0f, 1f);
            return t;
        }

        // ── 보이기 ──────────────────────────────────────

        private void Show(LootPickup who, SkillData skill)
        {
            bool changed = owner != who;
            owner = who;

            if (changed || !panel.gameObject.activeSelf) Fill(skill);

            panel.gameObject.SetActive(true);
        }

        private void Hide()
        {
            owner = null;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        /// <summary>글을 채우고 줄마다 높이를 재 아래로 쌓는다. 판 높이는 그 합이다.</summary>
        private void Fill(SkillData skill)
        {
            bool hasIcon = skill.icon != null;
            icon.gameObject.SetActive(hasIcon);
            if (hasIcon) icon.sprite = skill.icon;

            float textLeft = Inset + (hasIcon ? IconSize + 12f : 0f);

            title.text = skill.skillName;
            sub.text = $"{RoleNames.Of(skill.role)} · {AttackTypeNames.Of(skill.attackType)}";
            body.text = string.IsNullOrWhiteSpace(skill.description) ? "(설명 없음)" : skill.description;

            int count = RunProgression.Current.CountOf(skill.skillName);
            owned.text = count > 0
                ? $"이미 가진 스킬 — {SkillFusion.Describe(count)} → 받으면 {SkillFusion.Describe(count + 1)}"
                : "새 스킬";

            string key = InputHints.Key(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Interact);
            hint.text = $"[{key}] 이걸로 받는다 — 나머지 둘은 사라진다";

            float y = -Inset;
            y = Stack(title, textLeft, y, 0f);
            y = Stack(sub, textLeft, y, 2f);
            y = Mathf.Min(y, hasIcon ? -Inset - IconSize : y) - 10f;
            y = Stack(body, Inset, y, 0f) - 10f;
            y = Stack(owned, Inset, y, 0f) - 4f;
            y = Stack(hint, Inset, y, 0f);

            panel.sizeDelta = new Vector2(Width, -y + Inset);
        }

        /// <summary>글 한 줄(여러 줄일 수 있다)을 <paramref name="top"/>에 놓고 그 아래 y를 돌려준다.</summary>
        private static float Stack(Text t, float left, float top, float gap)
        {
            RectTransform r = t.rectTransform;
            r.sizeDelta = new Vector2(Width - left - Inset, 0f);
            r.sizeDelta = new Vector2(r.sizeDelta.x, Mathf.Ceil(t.preferredHeight));
            r.anchoredPosition = new Vector2(left, top - gap);
            return top - gap - r.sizeDelta.y;
        }

        /// <summary>두루마리 머리 위 화면 자리에 판을 붙인다. 화면 밖으로 삐져나가지 않게 가둔다.</summary>
        private void LateUpdate()
        {
            if (owner == null || !panel.gameObject.activeSelf) return;

            Camera cam = BeltScroll.Cam;
            if (cam == null) return;

            Vector3 world = owner.transform.position + BeltScroll.ScreenUp * 2.1f;
            Vector3 screen = cam.WorldToScreenPoint(world);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                return;

            // 캔버스 원점은 가운데다. 판의 앵커는 왼쪽 아래라 그쪽 기준으로 옮긴다.
            Vector2 size = canvasRect.rect.size;
            Vector2 at = local + size * 0.5f;

            float half = panel.sizeDelta.x * 0.5f;
            at.x = Mathf.Clamp(at.x, half + 12f, size.x - half - 12f);
            at.y = Mathf.Clamp(at.y, 12f, size.y - panel.sizeDelta.y - 12f);

            panel.anchoredPosition = at;
        }
    }
}
