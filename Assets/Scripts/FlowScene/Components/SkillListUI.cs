using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Prototype
{
    /// <summary>
    /// 스킬 목록 — 이번 런에서 가진 스킬을 한눈에 보는 창. 런 바 오른쪽 [스킬 목록]이 연다(<see cref="RunBarUI"/>).
    ///
    /// <code>
    /// ┌──────────────── 스킬 목록 ────────────────┐
    /// │ [아이콘] 회전베기             ×3  총공격 │
    /// │          Warrior · Launcher               │
    /// │          주변 적에게 광역 피해            │
    /// │ ...                                      │
    /// └───────────────[ 닫기 ]──────────────────┘
    /// </code>
    ///
    /// 보기 전용이다 — 총공격 슬롯을 바꾸는 곳은 <see cref="AssaultEditorUI"/>. 열려 있는 동안 게임 조작을 잠그고,
    /// ESC · [닫기]로 닫는다. 지도 · 칸 씬엔 플레이어 입력이 없어 ESC를 키보드에서 직접 읽는다.
    ///
    /// 골격은 <see cref="UiKit.Modal"/>(기획서 5.3), 줄 정렬은 <see cref="Order"/>가 정한다.
    /// </summary>
    public class SkillListUI : MonoBehaviour
    {
        public static readonly Vector2 PanelSize = new Vector2(860f, 760f);
        public const float RowHeight = 96f;
        public const float RowGap = UiTheme.Space.S8;
        public const float IconSize = 64f;

        // ── 씬에 하나 ────────────────────────────────────

        private static SkillListUI instance;

        public static bool IsOpen => instance != null && instance.isOpen;

        /// <summary>지금 떠 있는 창. 테스트가 읽는다.</summary>
        public static SkillListUI Current => instance;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterModal() => GameplayModal.Register(IsOpenNow);

        private static bool IsOpenNow() => IsOpen;

        /// <summary>연다. 씬에 없으면 만든다 — 씬이 바뀌면 같이 내려간다.</summary>
        public static void Open()
        {
            if (instance == null)
            {
                var go = new GameObject(nameof(SkillListUI), typeof(RectTransform));
                instance = go.AddComponent<SkillListUI>();
                instance.Build();
            }
            instance.Show();
        }

        public static void Toggle()
        {
            if (IsOpen) instance.Hide();
            else Open();
        }

        // ── 상태 ─────────────────────────────────────────

        private GameObject root;
        private RectTransform content;
        private Text empty;
        private bool isOpen;
        private readonly List<GameObject> rows = new List<GameObject>();

        /// <summary>지금 그려진 줄 수. 테스트가 읽는다.</summary>
        public int RowCount => rows.Count;

        /// <summary>줄의 스킬 이름(그려진 순서대로). 테스트가 읽는다.</summary>
        public IEnumerable<string> RowNames
        {
            get { foreach (GameObject r in rows) yield return r.name.Substring("Row_".Length); }
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            if (isOpen) PlayerInputController.SetSuspended(false);
            instance = null;
        }

        private void Update()
        {
            if (!isOpen) return;
            if (!CancelPressed()) return;

            GameplayModal.ConsumeCancel();
            Hide();
        }

        private static bool CancelPressed()
        {
            if (GameplayModal.CancelConsumedThisFrame) return false;

            PlayerInputController input = PlayerInputController.Instance;
            if (input != null) return input.CancelPressed;
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        // ── 열고 닫기 ────────────────────────────────────

        private void Show()
        {
            Fill(RunProgression.Current);
            isOpen = true;
            root.SetActive(true);
            PlayerInputController.SetSuspended(true);
        }

        public void Hide()
        {
            if (!isOpen) return;
            isOpen = false;
            root.SetActive(false);
            PlayerInputController.SetSuspended(false);
        }

        // ── 짓기 ─────────────────────────────────────────

        private void Build()
        {
            UiKit.EnsureEventSystem();
            UiKit.BuildCanvas(gameObject, UiLayer.SkillList);

            UiKit.ModalParts m = UiKit.Modal(transform, "스킬 목록", PanelSize);
            root = m.Backdrop.gameObject;

            // 목록 — 세로 스크롤.
            Image viewport = UiKit.Well(m.Body, "Viewport", raycast: true);
            UiKit.Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = UiKit.NewRect(viewport.transform, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            empty = UiKit.NewText(m.Body, "Empty", UiTheme.Size.Body, UiTheme.TextSub, FontStyle.Normal);
            empty.text = "아직 가진 스킬이 없다";
            UiKit.Stretch(empty.rectTransform);

            Button close = UiKit.NewButton(m.Buttons, "Btn_닫기", "닫기", ButtonKind.Ghost,
                                           new Vector2(200f, UiTheme.ButtonHeight.Medium));
            close.onClick.AddListener(Hide);

            root.SetActive(false);
        }

        /// <summary>런의 보유 스킬로 줄을 다시 깐다. 테스트가 직접 부른다.</summary>
        public void Fill(RunProgression run)
        {
            foreach (GameObject r in rows) Kill(r);
            rows.Clear();

            List<string> names = Order(run);
            float y = 0f;
            foreach (string name in names)
            {
                rows.Add(BuildRow(run, name, y));
                y -= RowHeight + RowGap;
            }

            content.sizeDelta = new Vector2(0f, Mathf.Max(0f, -y));
            content.anchoredPosition = Vector2.zero;
            empty.gameObject.SetActive(names.Count == 0);
        }

        /// <summary>
        /// 줄 순서 — 총공격 슬롯에 든 스킬이 먼저(슬롯 순서대로), 나머지는 개수 많은 순 · 이름 순.
        /// 지금 쓰고 있는 것부터 보이게.
        /// </summary>
        public static List<string> Order(RunProgression run)
        {
            var slotted = new List<string>();
            foreach (string s in run.AssaultSlots)
                if (!string.IsNullOrEmpty(s) && run.CountOf(s) > 0 && !slotted.Contains(s)) slotted.Add(s);

            var rest = new List<string>();
            foreach (KeyValuePair<string, int> kv in run.SkillCounts)
                if (kv.Value > 0 && !slotted.Contains(kv.Key)) rest.Add(kv.Key);

            rest.Sort((a, b) =>
            {
                int byCount = run.CountOf(b).CompareTo(run.CountOf(a));
                return byCount != 0 ? byCount : string.CompareOrdinal(a, b);
            });

            slotted.AddRange(rest);
            return slotted;
        }

        private GameObject BuildRow(RunProgression run, string name, float y)
        {
            SkillData data = run.Resolve(name);
            float pad = UiTheme.Space.S12;

            Image bg = UiKit.FramedPlate(content, "Row_" + name, UiTheme.Raised);
            RectTransform r = bg.rectTransform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(-2f * pad, RowHeight);
            r.anchoredPosition = new Vector2(0f, y - pad);

            // 아이콘 — 직업 색 홈 위에.
            Image well = UiKit.Well(r, "IconWell");
            if (data != null) well.color = SkillCutinUI.RoleColor(data.role).Shade(0.5f);
            UiKit.Place(well, new Vector2(0f, 0.5f), new Vector2(pad, 0f), new Vector2(IconSize, IconSize)).pivot = new Vector2(0f, 0.5f);
            Image icon = UiKit.NewImage(well.transform, "Icon", Color.white);
            UiKit.Stretch(icon.rectTransform, 4f);
            icon.preserveAspect = true;
            icon.sprite = data != null ? data.icon : null;
            icon.enabled = icon.sprite != null;

            float textX = pad + IconSize + UiTheme.Space.S16;

            Text title = UiKit.NewText(r, "Name", UiTheme.Size.Heading, UiTheme.Gold, FontStyle.Bold);
            title.text = name;
            title.alignment = TextAnchor.MiddleLeft;
            Line(title.rectTransform, textX, -8f, 34f, 220f);

            Text sub = UiKit.NewText(r, "Sub", UiTheme.Size.Caption, UiTheme.TextSub, FontStyle.Normal);
            sub.text = data != null ? $"{data.role} · {data.attackType}" : "";
            sub.alignment = TextAnchor.MiddleLeft;
            Line(sub.rectTransform, textX, -40f, 20f, 220f);

            Text desc = UiKit.NewText(r, "Desc", UiTheme.Size.Caption, UiTheme.TextMain, FontStyle.Normal);
            desc.text = data != null ? UiText.Format(data.description) : "";
            desc.alignment = TextAnchor.MiddleLeft;
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Truncate;
            Line(desc.rectTransform, textX, -62f, 26f, pad);

            // 오른쪽 위 — 개수, 총공격 표시.
            Text count = UiKit.NewText(r, "Count", UiTheme.Size.Heading, UiTheme.Gold, FontStyle.Bold);
            count.text = "×" + run.CountOf(name);
            count.alignment = TextAnchor.MiddleRight;
            Corner(count.rectTransform, -pad, -8f, 80f, 34f);

            if (run.IsInAssaultSlot(name))
            {
                Text tag = UiKit.NewText(r, "Assault", UiTheme.Size.Caption, UiTheme.Focus, FontStyle.Bold);
                tag.text = "총공격";
                tag.alignment = TextAnchor.MiddleRight;
                Corner(tag.rectTransform, -pad - 88f, -8f, 80f, 34f);
            }

            return bg.gameObject;
        }

        /// <summary>줄 안의 한 줄 글자 — 왼쪽 <paramref name="left"/>부터 오른쪽 <paramref name="rightPad"/> 전까지.</summary>
        private static void Line(RectTransform t, float left, float top, float height, float rightPad)
        {
            t.anchorMin = new Vector2(0f, 1f);
            t.anchorMax = new Vector2(1f, 1f);
            t.pivot = new Vector2(0f, 1f);
            t.offsetMin = new Vector2(left, top - height);
            t.offsetMax = new Vector2(-rightPad, top);
        }

        private static void Corner(RectTransform t, float x, float y, float w, float h)
        {
            t.anchorMin = t.anchorMax = t.pivot = new Vector2(1f, 1f);
            t.sizeDelta = new Vector2(w, h);
            t.anchoredPosition = new Vector2(x, y);
        }

        private static void Kill(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
