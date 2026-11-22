using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 런 바 — 런 동안 지도 · 전투 · 칸 씬 상단에 <b>같은 모양으로</b> 뜨는 띠(기획서 5.1 · 원칙 R1).
    ///
    /// <code>
    /// ┌───────────────────────────────────────────────────────────────────────────┐
    /// │ ■ 체력 72%  ■ 골드 288   [▣][▣][▣][▣] 총공격    층 3 / 12    [■ 스킬 목록]  [ESC] 설정 │
    /// └───────────────────────────────────────────────────────────────────────────┘
    /// </code>
    ///
    /// <list type="bullet">
    /// <item><b>체력</b>은 전투에서 숨긴다(결정 D2) — 전투 중 체력의 주인은 좌하단 HUD다.</item>
    /// <item><b>총공격 슬롯</b>은 편성할 수 있을 때만 눌린다 — 칸 안에서 싸움이 없을 때(<see cref="AssaultEditorUI.CanOpen"/>).
    /// 전투 칸은 이긴 뒤 · 방 사이 통로, 상점 · 이벤트 칸은 싸움이 안 벌어진 동안, 마을은 늘. 지도에서는 늘 읽기 전용이다.</item>
    /// <item><b>[ESC] 설정</b>은 씬에 일시정지 창이 있을 때만 — 우측 상단 [ESC 일시정지] 안내를 이걸로 흡수했다.</item>
    /// <item><b>[스킬 목록]</b>은 어느 씬에서나 눌린다 — 보유 스킬 창(<see cref="SkillListUI"/>)을 연다. 예전 "덱 N" 자리다.</item>
    /// </list>
    ///
    /// 값은 이벤트를 구독하지 않고 짧은 주기로 다시 읽는다 — 골드 · 층 · 슬롯이 바뀌는 길이 여러 군데라
    /// 구독을 빠뜨리면 조용히 낡은 숫자가 남는다. 읽는 값은 전부 싸다.
    /// </summary>
    public class RunBarUI : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>전투 — HUD 바닥층, 체력 숨김, 우하단 총공격 키 안내.</summary>
            Battle,
            /// <summary>지도 · 칸 — 전면 창 위에 얹힌다, 체력 보임.</summary>
            Overlay,
        }

        /// <summary>띠 높이(캔버스 단위). 지도 · 칸 화면이 이만큼 위를 비운다.</summary>
        public const float Height = 64f;

        public const float SlotSize = 40f;
        public const float RefreshInterval = 0.2f;

        private Mode mode;

        private Text hp, gold, floor;
        private Text skillListLabel;
        private Button skillList;
        private GameObject hpGroup;
        private readonly Image[] slotIcons = new Image[RunProgression.AssaultSlotCount];
        private readonly Text[] slotLetters = new Text[RunProgression.AssaultSlotCount];
        private Button[] slotButtons = new Button[RunProgression.AssaultSlotCount];
        private Button settings;

        /// <summary>전투 우하단 [키] 총공격 안내 — 총공격을 <b>내는</b> 키(기본 Space)다. 키 이름이 바뀌면(리바인드) 다시 짓는다.</summary>
        private RectTransform assaultHint;
        private string assaultKey;

        private float nextRefresh;

        // ── 테스트 · 배선이 읽는 것 ──────────────────────

        public Mode CurrentMode => mode;
        public Text Gold => gold;
        public Text Floor => floor;
        /// <summary>[스킬 목록] 버튼과 그 글자.</summary>
        public Button SkillList => skillList;
        public Text SkillListLabel => skillListLabel;

        public const string SkillListText = "스킬 목록";
        public Text Hp => hp;
        public bool HpVisible => hpGroup != null && hpGroup.activeSelf;
        public Button Settings => settings;
        public IReadOnlyList<Button> SlotButtons => slotButtons;
        public RectTransform AssaultHint => assaultHint;

        /// <summary>씬에 붙인다. 씬과 함께 언로드되도록 <paramref name="owner"/>의 자식이 된다.</summary>
        public static RunBarUI Create(Transform owner, Mode mode)
        {
            var go = new GameObject("RunBar", typeof(RectTransform));
            go.transform.SetParent(owner, false);

            RunBarUI bar = go.AddComponent<RunBarUI>();
            bar.mode = mode;
            bar.Build();
            bar.Refresh();
            return bar;
        }

        public static RunBarUI Create(Component owner, Mode mode) => Create(owner.transform, mode);

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshInterval;
            Refresh();
        }

        // ── 짓기 ─────────────────────────────────────────

        private void Build()
        {
            int order = mode == Mode.Battle ? UiLayer.RunBar : UiLayer.RunBarOverWindow;
            // 가로로 넓은 모니터에서 띠가 양 끝까지 늘어나되 높이는 안 커지게 세로 기준(1).
            UiKit.BuildCanvas(gameObject, order, match: 1f, raycaster: true);

            Image plate = UiKit.NewImage(transform, "Plate", UiTheme.BarPlate);
            RectTransform p = plate.rectTransform;
            p.anchorMin = new Vector2(0f, 1f);
            p.anchorMax = new Vector2(1f, 1f);
            p.pivot = new Vector2(0.5f, 1f);
            p.sizeDelta = new Vector2(0f, Height);
            p.anchoredPosition = Vector2.zero;

            // 아랫변 — 놋쇠 가는 줄 하나로 깔끔하게 끊는다(찢어진 가장자리는 쓰지 않기로 했다).
            Image edge = UiKit.NewImage(p, "Edge", UiTheme.Rim);
            RectTransform e = edge.rectTransform;
            e.anchorMin = new Vector2(0f, 0f);
            e.anchorMax = new Vector2(1f, 0f);
            e.pivot = new Vector2(0.5f, 1f);
            e.sizeDelta = new Vector2(0f, UiTheme.RimWidth);

            float x = UiTheme.Space.Edge;

            // ── 왼쪽: 체력 · 골드
            hp = Stat(p, "Hp", UiTheme.Art.IconHp, UiTheme.Hp, ref x);
            hpGroup = hp.transform.parent.gameObject;
            if (mode == Mode.Battle)
            {
                hpGroup.SetActive(false);
                x -= StatWidth + UiTheme.Space.S24;   // 숨긴 만큼 당긴다
            }
            gold = Stat(p, "Gold", UiTheme.Art.IconGold, UiTheme.Gold, ref x);

            // ── 총공격 슬롯
            x += UiTheme.Space.S16;
            for (int i = 0; i < slotIcons.Length; i++)
            {
                BuildSlot(p, i, x);
                x += SlotSize + UiTheme.Space.S8;
            }
            Text slotLabel = UiKit.NewText(p, "SlotLabel", UiTheme.Size.Caption, UiTheme.TextSub, FontStyle.Normal);
            slotLabel.text = "총공격";
            slotLabel.alignment = TextAnchor.MiddleLeft;
            LeftAt(slotLabel.rectTransform, x, 80f);

            // ── 가운데: 층
            floor = UiKit.IconStat(p, "Floor", UiTheme.Art.IconFloor, "", UiTheme.TextMain);
            UiKit.Place(floor.transform.parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, UiKit.StatIconSize));

            // ── 오른쪽: 스킬 목록 · 설정
            float rx = UiTheme.Space.Edge;
            if (PauseMenu.Current != null)
            {
                RectTransform hint = UiKit.KeyHint(p, "SettingsHint", "ESC", "설정");
                settings = hint.gameObject.AddComponent<Button>();
                Image hit = hint.gameObject.AddComponent<Image>();
                hit.color = Color.clear;   // 키캡 + 글자 전체가 눌리게 투명 판을 깐다
                settings.targetGraphic = hit;
                settings.navigation = new Navigation { mode = Navigation.Mode.None };
                settings.onClick.AddListener(OpenSettings);
                RightAt(hint, rx, hint.sizeDelta.x);
                rx += hint.sizeDelta.x + UiTheme.Space.S24;
            }

            // [스킬 목록] — 아이콘 + 글자 묶음 전체가 버튼이다. 투명 판을 깔아 어디를 눌러도 먹게 한다.
            skillListLabel = UiKit.IconStat(p, "SkillList", UiTheme.Art.IconDeck, SkillListText, UiTheme.TextMain);
            skillListLabel.fontSize = UiTheme.Size.Body;
            var listRoot = (RectTransform)skillListLabel.transform.parent;
            RightAt(listRoot, rx, SkillListWidth);
            Image listHit = listRoot.gameObject.AddComponent<Image>();
            listHit.color = Color.clear;
            skillList = listRoot.gameObject.AddComponent<Button>();
            skillList.targetGraphic = listHit;
            skillList.navigation = new Navigation { mode = Navigation.Mode.None };
            skillList.onClick.AddListener(SkillListUI.Toggle);
        }

        private const float StatWidth = 150f;
        private const float SkillListWidth = 160f;

        private Text Stat(RectTransform parent, string name, Sprite icon, Color color, ref float x)
        {
            Text t = UiKit.IconStat(parent, name, icon, "", color);
            var root = (RectTransform)t.transform.parent;
            root.sizeDelta = new Vector2(StatWidth, UiKit.StatIconSize);
            LeftAt(root, x, StatWidth);
            x += StatWidth + UiTheme.Space.S24;
            return t;
        }

        private void BuildSlot(RectTransform parent, int index, float x)
        {
            Image well = UiKit.Well(parent, $"Slot_{index}", raycast: true);
            LeftAt(well.rectTransform, x, SlotSize);
            well.rectTransform.sizeDelta = new Vector2(SlotSize, SlotSize);
            UiKit.AddRim(well, UiTheme.Rim.WithAlpha(0.35f));

            Image icon = UiKit.NewImage(well.transform, "Icon", Color.white);
            UiKit.Stretch(icon.rectTransform, 3f);
            icon.preserveAspect = true;
            icon.enabled = false;
            slotIcons[index] = icon;

            Text letter = UiKit.NewText(well.transform, "Letter", UiTheme.Size.Caption, UiTheme.TextMain, FontStyle.Bold);
            UiKit.Stretch(letter.rectTransform);
            slotLetters[index] = letter;

            Button b = well.gameObject.AddComponent<Button>();
            b.targetGraphic = well;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            UiKit.DressButton(b);
            b.interactable = AssaultEditorUI.CanOpen;   // Refresh가 계속 맞춘다
            b.onClick.AddListener(AssaultEditorUI.RequestOpen);
            slotButtons[index] = b;
        }

        private static void LeftAt(RectTransform r, float x, float width)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(width, r.sizeDelta.y > 0f ? r.sizeDelta.y : UiKit.StatIconSize);
            r.anchoredPosition = new Vector2(x, 0f);
        }

        private static void RightAt(RectTransform r, float fromRight, float width)
        {
            r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
            r.pivot = new Vector2(1f, 0.5f);
            r.sizeDelta = new Vector2(width, r.sizeDelta.y > 0f ? r.sizeDelta.y : UiKit.StatIconSize);
            r.anchoredPosition = new Vector2(-fromRight, 0f);
        }

        // ── 값 ───────────────────────────────────────────

        /// <summary>지금 값으로 다시 쓴다. 테스트가 직접 부른다.</summary>
        public void Refresh()
        {
            RunProgression run = RunProgression.Current;

            if (mode == Mode.Battle) RefreshAssaultHint();

            gold.text = run.Gold.ToString();
            hp.text = Mathf.RoundToInt(run.Party.HeroHpRatio * 100f) + "%";
            floor.text = FloorLabel();
            // 런 밖(씬 단독 실행)엔 층이 없다 — 계단 아이콘만 덩그러니 남지 않게 통째로 숨긴다.
            floor.transform.parent.gameObject.SetActive(floor.text.Length > 0);

            // 슬롯은 편성할 수 있을 때만 눌린다. 키(E)와 같은 문이라 둘이 다른 답을 하지 않는다.
            bool editable = AssaultEditorUI.CanOpen;
            foreach (Button b in slotButtons)
                if (b != null) b.interactable = editable;

            IReadOnlyList<string> slots = run.AssaultSlots;
            for (int i = 0; i < slotIcons.Length; i++)
            {
                string name = slots != null && i < slots.Count ? slots[i] : null;
                SkillData data = string.IsNullOrEmpty(name) ? null : run.Resolve(name);

                Sprite s = data != null ? data.icon : null;
                slotIcons[i].sprite = s;
                slotIcons[i].enabled = s != null;

                // 아이콘이 없는 스킬은 첫 글자로라도 칸이 찼음을 보인다.
                slotLetters[i].text = s == null && !string.IsNullOrEmpty(name) ? name.Substring(0, 1) : "";
            }
        }

        /// <summary>
        /// 우하단 조작 안내(기획서 5.2 "행동은 오른아래"). 플레이어 입력이 아직 없으면 미룬다 —
        /// 런 바가 플레이어보다 먼저 지어질 수 있다.
        /// </summary>
        private void RefreshAssaultHint()
        {
            if (PlayerInputController.Instance == null) return;

            // 편집창 키(BulletTime · E)가 아니라 총공격을 내는 키다 — 전투 중에 눌러야 하는 건 이쪽이다.
            string key = InputHints.Key(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Assault);
            SetAssaultHint(key);
        }

        /// <summary>안내를 이 키로 (다시) 짓는다. 같은 키면 아무것도 안 한다. 테스트가 직접 부른다.</summary>
        public void SetAssaultHint(string key)
        {
            if (string.IsNullOrEmpty(key) || key == assaultKey) return;
            assaultKey = key;

            if (assaultHint != null) Destroy(assaultHint.gameObject);

            assaultHint = UiKit.KeyHint(transform, "AssaultHint", key, "총공격");
            assaultHint.anchorMin = assaultHint.anchorMax = assaultHint.pivot = new Vector2(1f, 0f);
            assaultHint.anchoredPosition = new Vector2(-UiTheme.Space.Edge, UiTheme.Space.Edge);
        }

        private new static void Destroy(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        /// <summary>"층 3 / 12". 런 밖(씬 단독 실행)이면 비운다.</summary>
        public static string FloorLabel()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.FloorCount <= 0) return "";
            return $"층 {gm.FloorNumber} / {gm.FloorCount}";
        }

        private static void OpenSettings()
        {
            if (PauseMenu.Current != null) PauseMenu.Current.Open();
        }
    }
}
