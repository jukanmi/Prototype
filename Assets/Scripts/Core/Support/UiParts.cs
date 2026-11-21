using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>버튼 종류. 바탕색 · 테두리가 여기서 갈린다.</summary>
    public enum ButtonKind
    {
        /// <summary>권하는 행동 — 금 테두리.</summary>
        Primary,
        Normal,
        /// <summary>되돌릴 수 없는 행동.</summary>
        Danger,
        /// <summary>테두리만 — [닫기] · [받지 않고 나가기]처럼 물러나는 행동.</summary>
        Ghost,
    }

    /// <summary>
    /// 규격 부품 — <c>docs/UI_Unification_Plan.md</c> 4항. 모든 부품은 <see cref="UiTheme"/>만 읽는다.
    /// 9-slice 판 스프라이트가 생기기 전까지 판은 단색 <see cref="Image"/> + <see cref="Outline"/> 테두리로 짓는다.
    /// </summary>
    public static partial class UiKit
    {
        // ── 판 ───────────────────────────────────────────

        /// <summary>테두리가 있는 판 — 창 · 선택지 줄 · 툴팁 · 버튼의 공통 재질.</summary>
        public static Image FramedPlate(Transform parent, string name, Color? fill = null, bool raycast = false)
        {
            Image img = NewImage(parent, name, fill ?? UiTheme.Ink, raycast);
            UiTheme.Art.Apply(img, UiTheme.Art.Plate);
            AddRim(img, UiTheme.Rim);
            return img;
        }

        /// <summary>오목판 — 빈 슬롯 · 홈.</summary>
        public static Image Well(Transform parent, string name, bool raycast = false)
        {
            Image img = NewImage(parent, name, UiTheme.InkDeep, raycast);
            UiTheme.Art.Apply(img, UiTheme.Art.Well);
            return img;
        }

        /// <summary>판 테두리. 그래픽 하나에 한 벌만 — 이미 있으면 색만 바꾼다.</summary>
        public static Outline AddRim(Graphic g, Color color)
        {
            Outline o = g.GetComponent<Outline>();
            if (o == null) o = g.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(UiTheme.RimWidth, -UiTheme.RimWidth);
            o.useGraphicAlpha = false;
            return o;
        }

        // ── 버튼 ─────────────────────────────────────────

        public static Color ButtonFill(ButtonKind kind)
        {
            switch (kind)
            {
                case ButtonKind.Primary: return UiTheme.ButtonPrimary;
                case ButtonKind.Danger:  return UiTheme.ButtonDanger;
                case ButtonKind.Ghost:   return UiTheme.Ink.WithAlpha(0.35f);
                default:                 return UiTheme.ButtonNormal;
            }
        }

        /// <summary>종류로 고르는 버튼. 크기를 안 주면 <see cref="UiTheme.ButtonHeight.Medium"/> × 최소 폭.</summary>
        public static Button NewButton(Transform parent, string name, string label, ButtonKind kind, Vector2 size = default)
        {
            if (size == default) size = new Vector2(Mathf.Max(UiTheme.ButtonHeight.MinWidth, 200f), UiTheme.ButtonHeight.Medium);

            Button b = CreateButton(parent, name, label, ButtonFill(kind), size, UiTheme.Size.Body);
            b.GetComponent<UiFocusRim>().Rest = kind == ButtonKind.Primary ? UiTheme.Gold.WithAlpha(0.8f) : UiTheme.Rim;
            return b;
        }

        // ── 창 ───────────────────────────────────────────

        /// <summary><see cref="Modal"/>이 돌려주는 조각들.</summary>
        public struct ModalParts
        {
            /// <summary>화면 막. 클릭을 막는다. 창 전체를 끄려면 이걸 끈다.</summary>
            public Image Backdrop;
            public Image Panel;
            public Text Title;
            /// <summary>창마다 다른 부분이 여기 들어간다. 제목 띠와 버튼 줄 사이.</summary>
            public RectTransform Body;
            /// <summary>가로로 버튼을 늘어놓는 줄. 가운데 정렬.</summary>
            public RectTransform Buttons;
        }

        /// <summary>제목 띠 높이 · 버튼 줄 높이 · 안쪽 여백. 전 창 동일.</summary>
        public const float ModalTitleBand = 72f;
        public const float ModalButtonBand = 88f;
        public const float ModalPadding = UiTheme.Space.S32;

        /// <summary>
        /// 창의 골격 하나 — 바탕막 → 판 → 제목 띠 → 본문 → 버튼 줄. 창이 다른 것은 본문뿐이다.
        /// 열릴 때 <see cref="UiTheme.Motion.Normal"/> 동안 아래에서 올라오며 나타난다.
        /// </summary>
        public static ModalParts Modal(Transform root, string title, Vector2 size)
        {
            var parts = new ModalParts();

            parts.Backdrop = NewImage(root, "Backdrop", UiTheme.Backdrop, raycast: true);
            Stretch(parts.Backdrop.rectTransform);

            parts.Panel = FramedPlate(parts.Backdrop.transform, "Panel", UiTheme.Ink.WithAlpha(0.97f), raycast: true);
            Place(parts.Panel, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            parts.Panel.gameObject.AddComponent<UiOpenAnim>();

            parts.Title = NewText(parts.Panel.transform, "Title", UiTheme.Size.Title, UiTheme.Gold, FontStyle.Bold);
            parts.Title.text = title;
            var tr = parts.Title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-2f * ModalPadding, ModalTitleBand);
            tr.anchoredPosition = new Vector2(0f, -UiTheme.Space.S8);

            // 제목 밑 가는 줄 — 레퍼런스의 띠 구분선.
            Image rule = NewImage(parts.Panel.transform, "TitleRule", UiTheme.Rim.WithAlpha(0.35f));
            var rr = rule.rectTransform;
            rr.anchorMin = new Vector2(0f, 1f);
            rr.anchorMax = new Vector2(1f, 1f);
            rr.pivot = new Vector2(0.5f, 1f);
            rr.sizeDelta = new Vector2(-2f * ModalPadding, 1f);
            rr.anchoredPosition = new Vector2(0f, -ModalTitleBand - UiTheme.Space.S8);

            parts.Body = NewRect(parts.Panel.transform, "Body");
            parts.Body.anchorMin = Vector2.zero;
            parts.Body.anchorMax = Vector2.one;
            parts.Body.offsetMin = new Vector2(ModalPadding, ModalButtonBand);
            parts.Body.offsetMax = new Vector2(-ModalPadding, -(ModalTitleBand + UiTheme.Space.S24));

            parts.Buttons = NewRect(parts.Panel.transform, "Buttons");
            parts.Buttons.anchorMin = new Vector2(0f, 0f);
            parts.Buttons.anchorMax = new Vector2(1f, 0f);
            parts.Buttons.pivot = new Vector2(0.5f, 0f);
            parts.Buttons.sizeDelta = new Vector2(-2f * ModalPadding, UiTheme.ButtonHeight.Medium);
            parts.Buttons.anchoredPosition = new Vector2(0f, UiTheme.Space.S24);
            var row = parts.Buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = UiTheme.Space.S16;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            return parts;
        }

        /// <summary>
        /// 프리팹에 박혀 있는 창을 <see cref="Modal"/>과 같은 모양으로 입힌다 — 구조는 그대로 두고 재질만.
        /// 프리팹 배치 테스트가 지키는 자리 · 크기는 건드리지 않는다.
        /// <list type="bullet">
        /// <item>루트 이미지 → 바탕막 색</item>
        /// <item>루트 바로 밑 <c>Panel</c> → 판 색 + 테두리</item>
        /// <item>판 바로 밑 <c>Title</c> 글자 → 금색 · 제목 크기 · 굵게</item>
        /// <item>모든 버튼 → 공통 호버 테두리(<see cref="UiFocusRim"/>), 라벨은 본문 색</item>
        /// <item>모든 글자 → 글꼴 · 외곽선(<see cref="Dress"/>)</item>
        /// </list>
        /// </summary>
        public static void DressModal(GameObject backdrop)
        {
            if (backdrop == null) return;

            Image dim = backdrop.GetComponent<Image>();
            if (dim != null) dim.color = UiTheme.Backdrop;

            Transform panel = backdrop.transform.Find("Panel");
            if (panel != null)
            {
                Image plate = panel.GetComponent<Image>();
                if (plate != null)
                {
                    plate.color = UiTheme.Ink.WithAlpha(0.97f);
                    AddRim(plate, UiTheme.Rim);
                }

                Transform title = panel.Find("Title");
                Text titleText = title != null ? title.GetComponent<Text>() : null;
                if (titleText != null)
                {
                    titleText.color = UiTheme.Gold;
                    titleText.fontSize = UiTheme.Size.Title;
                    titleText.fontStyle = FontStyle.Bold;
                }
            }

            Dress(backdrop.transform);

            foreach (Button b in backdrop.GetComponentsInChildren<Button>(true))
                DressButton(b);
        }

        /// <summary>코드 밖(프리팹)에서 온 버튼에 공통 호버 테두리와 라벨 색을 입힌다.</summary>
        public static void DressButton(Button b)
        {
            if (b == null) return;
            if (b.GetComponent<Graphic>() != null && b.GetComponent<UiFocusRim>() == null)
                b.gameObject.AddComponent<UiFocusRim>();

            ColorBlock colors = b.colors;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = UiTheme.PressedTint;
            colors.disabledColor = UiTheme.DisabledTint;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = UiTheme.Motion.Quick;
            b.colors = colors;

            foreach (Text t in b.GetComponentsInChildren<Text>(true))
                t.color = UiTheme.TextMain;
        }

        // ── 선택지 줄 ────────────────────────────────────

        public struct OptionRowParts
        {
            public Button Button;
            public Image Icon;
            public Text Title;
            public Text Body;
        }

        public const float OptionIconSize = 48f;

        /// <summary>
        /// 선택지 한 줄 — 왼쪽 아이콘, 금색 제목, 흰 설명(<see cref="UiText.Format"/> 표식 강조).
        /// 휴식 · 상점 · 이벤트 · 레벨업 선택지가 모두 이 모양이다.
        /// </summary>
        public static OptionRowParts OptionRow(Transform parent, string name, Sprite icon,
                                               string title, string body, Vector2 size)
        {
            var parts = new OptionRowParts();
            parts.Button = CreateButton(parent, name, "", UiTheme.Ink.WithAlpha(0.92f), size, UiTheme.Size.Body);
            Transform t = parts.Button.transform;

            // CreateButton 의 가운데 라벨은 이 줄에서 안 쓴다.
            Transform label = t.Find("Label");
            if (label != null) Kill(label.gameObject);

            float textX = UiTheme.Space.S16;
            if (icon != null)
            {
                parts.Icon = NewImage(t, "Icon", Color.white);
                parts.Icon.sprite = icon;
                parts.Icon.preserveAspect = true;
                Place(parts.Icon, new Vector2(0f, 0.5f), new Vector2(UiTheme.Space.S16, 0f),
                      new Vector2(OptionIconSize, OptionIconSize)).pivot = new Vector2(0f, 0.5f);
                textX += OptionIconSize + UiTheme.Space.S16;
            }

            parts.Title = NewText(t, "Title", UiTheme.Size.Heading, UiTheme.Gold, FontStyle.Bold);
            parts.Title.text = title;
            parts.Title.alignment = TextAnchor.LowerLeft;
            Band(parts.Title, new Vector2(0f, 0.52f), new Vector2(1f, 1f), 0f);
            parts.Title.rectTransform.offsetMin = new Vector2(textX, 0f);
            parts.Title.rectTransform.offsetMax = new Vector2(-UiTheme.Space.S16, -UiTheme.Space.S8);

            parts.Body = NewText(t, "Body", UiTheme.Size.Body, UiTheme.TextMain, FontStyle.Normal);
            parts.Body.text = UiText.Format(body);
            parts.Body.alignment = TextAnchor.UpperLeft;
            parts.Body.horizontalOverflow = HorizontalWrapMode.Wrap;
            Band(parts.Body, new Vector2(0f, 0f), new Vector2(1f, 0.48f), 0f);
            parts.Body.rectTransform.offsetMin = new Vector2(textX, UiTheme.Space.S8);
            parts.Body.rectTransform.offsetMax = new Vector2(-UiTheme.Space.S16, 0f);

            return parts;
        }

        /// <summary>
        /// 선택지를 쓸 수 없게 한다 — 글자는 <see cref="UiTheme.TextOff"/>, 아이콘은 흐리게.
        /// <paramref name="reason"/>을 주면 호버 때 툴팁으로 이유를 보인다.
        /// </summary>
        public static void SetUnavailable(OptionRowParts row, string reason)
        {
            row.Button.interactable = false;
            if (row.Title != null) row.Title.color = UiTheme.TextOff;
            if (row.Body != null) row.Body.color = UiTheme.TextOff;
            if (row.Icon != null) row.Icon.color = UiTheme.TextOff;
            if (!string.IsNullOrEmpty(reason)) UiTooltip.Attach(row.Button.gameObject, "사용 불가", reason);
        }

        /// <summary>재생 중이면 Destroy, 에디트 모드(테스트)면 DestroyImmediate.</summary>
        private static void Kill(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        // ── 아이콘 + 숫자 · 배지 · 키 안내 ───────────────

        public const float StatIconSize = 32f;

        /// <summary>아이콘 + 굵은 숫자 한 묶음. 숫자 글자를 돌려준다 — 값은 부르는 쪽이 갱신한다.</summary>
        public static Text IconStat(Transform parent, string name, Sprite icon, string value, Color valueColor)
        {
            RectTransform root = NewRect(parent, name);
            root.sizeDelta = new Vector2(120f, StatIconSize);

            Image img = NewImage(root, "Icon", icon != null ? Color.white : valueColor);
            img.sprite = icon;
            img.preserveAspect = true;
            Place(img, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(StatIconSize, StatIconSize)).pivot = new Vector2(0f, 0.5f);

            Text t = NewText(root, "Value", UiTheme.Size.Heading, valueColor, FontStyle.Bold);
            t.text = value;
            t.alignment = TextAnchor.MiddleLeft;
            var tr = t.rectTransform;
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(StatIconSize + UiTheme.Space.S8, 0f);
            tr.offsetMax = Vector2.zero;
            return t;
        }

        public const float BadgeSize = 24f;

        /// <summary>
        /// 아이콘 우하단의 개수 배지. 0이면 숨는다 — <see cref="SetBadge"/>로 갱신한다.
        /// </summary>
        public static Text Badge(Transform target, int count)
        {
            Image bg = NewImage(target, "Badge", UiTheme.Hp);
            if (UiTheme.Art.Badge != null) UiTheme.Art.Apply(bg, UiTheme.Art.Badge);   // 테두리가 구워져 있다
            else AddRim(bg, UiTheme.OutlineColor);
            Place(bg, new Vector2(1f, 0f), new Vector2(4f, -4f), new Vector2(BadgeSize, BadgeSize));

            Text t = NewText(bg.transform, "Count", UiTheme.Size.Micro, UiTheme.TextMain, FontStyle.Bold);
            Stretch(t.rectTransform);
            SetBadge(t, count);
            return t;
        }

        public static void SetBadge(Text badge, int count)
        {
            if (badge == null) return;
            badge.text = count.ToString();
            badge.transform.parent.gameObject.SetActive(count > 0);
        }

        public const float KeycapHeight = 28f;

        /// <summary>
        /// 키캡 + 동작 이름. <paramref name="key"/>는 리바인드 결과의 표시 문자열을 그대로 넘긴다.
        /// 동작 글자를 돌려준다.
        /// </summary>
        public static RectTransform KeyHint(Transform parent, string name, string key, string action)
        {
            RectTransform root = NewRect(parent, name);
            float capW = Mathf.Max(KeycapHeight, 14f + 11f * key.Length);
            root.sizeDelta = new Vector2(capW + UiTheme.Space.S8 + 14f * action.Length, KeycapHeight);

            Image cap = NewImage(root, "Keycap", UiTheme.TextMain);
            UiTheme.Art.Apply(cap, UiTheme.Art.Keycap);
            AddRim(cap, UiTheme.OutlineColor);
            Place(cap, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(capW, KeycapHeight)).pivot = new Vector2(0f, 0.5f);

            Text k = NewText(cap.transform, "Key", UiTheme.Size.Micro, UiTheme.TextOnLight, FontStyle.Bold);
            k.text = key;
            NoOutline(k);
            Stretch(k.rectTransform);

            Text a = NewText(root, "Action", UiTheme.Size.Caption, UiTheme.TextMain, FontStyle.Normal);
            a.text = action;
            a.alignment = TextAnchor.MiddleLeft;
            var ar = a.rectTransform;
            ar.anchorMin = Vector2.zero;
            ar.anchorMax = Vector2.one;
            ar.offsetMin = new Vector2(capW + UiTheme.Space.S8, 0f);
            ar.offsetMax = Vector2.zero;
            return root;
        }
    }

    /// <summary>
    /// 호버 · 키보드 포커스 · 선택의 공통 표시 — 테두리가 <see cref="UiTheme.Focus"/>로 바뀌고 살짝 커진다.
    /// <b>마우스와 키보드가 같은 모양이다.</b> 둘이 다른 표시를 쓰면 동시에 있을 때 무엇이 눌릴지 모른다.
    ///
    /// 화면이 자기 커서(<see cref="MenuCursor"/>)를 따로 그리는 곳은 <see cref="Forced"/>로 켠다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiFocusRim : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        /// <summary>평소 테두리 색.</summary>
        public Color Rest = UiTheme.Rim;

        public const float FocusScale = 1.02f;

        private Outline rim;
        private Image glow;
        private Selectable selectable;
        private bool hovered, selected, forced;
        private float t;          // 0 = 평소, 1 = 포커스

        /// <summary>화면 쪽 커서가 이 칸에 있다.</summary>
        public bool Forced
        {
            get => forced;
            set { forced = value; Apply(true); }
        }

        public bool IsFocused => (hovered || selected || forced) && (selectable == null || selectable.interactable);

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            Graphic g = GetComponent<Graphic>();
            if (g != null) rim = UiKit.AddRim(g, Rest);
            BuildGlow();
        }

        /// <summary>
        /// 바깥 발광 — 판보다 <see cref="UiTheme.Art.GlowSpread"/>만큼 크게 깔린 고리. 가운데가 비어 있어
        /// 자식으로 위에 그려도 판을 가리지 않는다. 스프라이트가 없으면 테두리 색만 바뀐다.
        /// </summary>
        private void BuildGlow()
        {
            Sprite s = UiTheme.Art.FocusGlow;
            if (s == null || glow != null) return;

            glow = UiKit.NewImage(transform, "FocusGlow", UiTheme.Focus.WithAlpha(0f));
            UiTheme.Art.Apply(glow, s);
            UiKit.Stretch(glow.rectTransform, -UiTheme.Art.GlowSpread);
            glow.transform.SetAsFirstSibling();
        }

        /// <summary>발광의 최대 진하기.</summary>
        public const float GlowAlpha = 0.75f;

        private void OnEnable() { hovered = false; selected = false; Apply(true); }

        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = false; }
        public void OnSelect(BaseEventData e) { selected = true; }
        public void OnDeselect(BaseEventData e) { selected = false; }

        private void Update() => Apply(false);

        private void Apply(bool snap)
        {
            float target = IsFocused ? 1f : 0f;
            t = snap ? target : Mathf.MoveTowards(t, target, Time.unscaledDeltaTime / UiTheme.Motion.Quick);

            if (rim != null) rim.effectColor = Color.Lerp(Rest, UiTheme.Focus, t);
            if (glow != null) glow.color = UiTheme.Focus.WithAlpha(GlowAlpha * t);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, FocusScale, t);
        }
    }

    /// <summary>
    /// 창 열림 — 아래 16px에서 올라오며 나타난다. unscaled 라 일시정지 · 불릿타임 중에도 돈다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiOpenAnim : MonoBehaviour
    {
        public const float Rise = UiTheme.Space.S16;

        private CanvasGroup group;
        private RectTransform rect;
        private float t = 1f;
        private float applied;    // 지금 위치에 더해 둔 오프셋 — 부르는 쪽이 자리를 옮겨도 어긋나지 않게 차이만 민다

        private void Awake()
        {
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (rect == null) return;
            t = 0f;
            Step();
        }

        private void OnDisable()
        {
            // 중간에 꺼지면 오프셋을 되돌려 둔다 — 다음에 켤 때 제자리에서 다시 시작하게.
            if (rect == null) return;
            rect.anchoredPosition -= new Vector2(0f, applied);
            applied = 0f;
            group.alpha = 1f;
            t = 1f;
        }

        private void Update()
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / UiTheme.Motion.Normal);
            Step();
        }

        private void Step()
        {
            float e = 1f - (1f - t) * (1f - t);   // ease-out
            group.alpha = e;
            float offset = -Rise * (1f - e);
            rect.anchoredPosition += new Vector2(0f, offset - applied);
            applied = offset;
        }
    }

    /// <summary>
    /// 툴팁 — 호버 <see cref="UiTheme.Motion.TooltipDelay"/> 뒤 대상 옆에 뜬다. 화면 오른쪽을 넘으면 왼쪽에 뜬다.
    /// 툴팁 캔버스는 처음 쓸 때 하나 짓고 계속 재사용한다.
    /// </summary>
    public class UiTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public const float Width = 360f;

        public string Title;
        [TextArea] public string Body;

        private float hoverAt = -1f;

        /// <summary>대상에 툴팁을 단다. 이미 있으면 내용만 바꾼다.</summary>
        public static UiTooltip Attach(GameObject target, string title, string body)
        {
            UiTooltip tip = target.GetComponent<UiTooltip>();
            if (tip == null) tip = target.AddComponent<UiTooltip>();
            tip.Title = title;
            tip.Body = body;
            return tip;
        }

        public void OnPointerEnter(PointerEventData e) => hoverAt = Time.unscaledTime;

        public void OnPointerExit(PointerEventData e)
        {
            hoverAt = -1f;
            Hide();
        }

        private void OnDisable()
        {
            if (hoverAt >= 0f) Hide();
            hoverAt = -1f;
        }

        private void Update()
        {
            if (hoverAt < 0f || Time.unscaledTime - hoverAt < UiTheme.Motion.TooltipDelay) return;
            hoverAt = float.MaxValue;   // 한 번만 띄운다
            Show((RectTransform)transform, Title, Body);
        }

        // ── 공용 판 하나 ─────────────────────────────────

        private static GameObject view;
        private static Text titleText, bodyText;
        private static RectTransform panel;

        public static void Show(RectTransform anchor, string title, string body)
        {
            Ensure();

            titleText.text = title;
            bodyText.text = UiText.Format(body);

            float bodyH = bodyText.preferredHeight;
            panel.sizeDelta = new Vector2(Width, UiTheme.Space.S16 * 2f + UiTheme.Size.Heading + UiTheme.Space.S8 + bodyH);

            // 대상의 오른쪽 위 모서리에 붙인다. 화면을 넘으면 왼쪽으로.
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Canvas canvas = view.GetComponent<Canvas>();
            RectTransform cr = (RectTransform)canvas.transform;
            Camera cam = null;
            Vector2 right = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            Vector2 left = RectTransformUtility.WorldToScreenPoint(cam, corners[1]);

            bool flip = right.x + Width * cr.localScale.x > Screen.width;
            panel.pivot = flip ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            Vector2 screen = flip ? left - new Vector2(UiTheme.Space.S12, 0f) : right + new Vector2(UiTheme.Space.S12, 0f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(cr, screen, cam, out Vector2 local);
            panel.anchoredPosition = local;

            view.SetActive(true);
        }

        public static void Hide()
        {
            if (view != null) view.SetActive(false);
        }

        private static void Ensure()
        {
            if (view != null) return;

            view = new GameObject("UiTooltip");
            DontDestroyOnLoad(view);
            UiKit.BuildCanvas(view, UiLayer.Tooltip, match: 0.5f, raycaster: false);

            Image plate = UiKit.FramedPlate(view.transform, "Plate", UiTheme.Ink.WithAlpha(0.97f));
            panel = plate.rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);

            titleText = UiKit.NewText(panel, "Title", UiTheme.Size.Heading, UiTheme.Gold, FontStyle.Bold);
            titleText.alignment = TextAnchor.UpperLeft;
            var tr = titleText.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-2f * UiTheme.Space.S16, UiTheme.Size.Heading + 4f);
            tr.anchoredPosition = new Vector2(0f, -UiTheme.Space.S16);

            bodyText = UiKit.NewText(panel, "Body", UiTheme.Size.Caption, UiTheme.TextMain, FontStyle.Normal);
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var br = bodyText.rectTransform;
            br.anchorMin = Vector2.zero;
            br.anchorMax = Vector2.one;
            br.offsetMin = new Vector2(UiTheme.Space.S16, UiTheme.Space.S16);
            br.offsetMax = new Vector2(-UiTheme.Space.S16, -(UiTheme.Space.S16 + UiTheme.Size.Heading + UiTheme.Space.S8));

            view.SetActive(false);
        }
    }
}
