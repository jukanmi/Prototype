using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.YG
{
    /// <summary>
    /// 메인화면의 <b>파티 편성</b>. 데려갈 동료를 명단에서 골라 4칸을 채운다.
    ///
    /// <see cref="BattleRestartUI"/> · <see cref="StageResultUI"/>와 같이 캔버스를 코드로 짓는다 —
    /// 씬 · 프리팹 배선이 필요 없고, MainMenu 씬을 다시 굽지 않아도 붙는다.
    ///
    /// <b>고른 순서가 곧 슬롯 순서이고, 그게 F키 교대 순환 순서다.</b> 그래서 슬롯을 누르면
    /// 빼기만 하고 자리를 비워 두지 않는다 — 뒤가 당겨진다.
    ///
    /// 고를 때마다 <see cref="GameManager.SelectLoadout"/>에 즉시 넘긴다. [시작]을 누르는
    /// 시점에 한 번에 확정하지 않는 이유는, 그러면 이 화면이 파괴된 뒤에 값을 읽어야 해서
    /// 메인화면 컨트롤러가 이 화면을 알아야 하기 때문이다.
    ///
    /// 명단이 비어 있으면 <b>스스로 숨는다</b> — 표(<see cref="PartyMemberData"/>)를 아직
    /// 굽지 않은 상태이고, 그때는 <c>GameManager</c>의 기본 조합이 알아서 간다.
    /// </summary>
    public class PartySelectUI : MonoBehaviour
    {
        /// <summary>메인화면 기본 UI 위. 다른 캔버스와 다투지 않을 만큼만.</summary>
        private const int SortingOrder = 50;

        private const float PanelWidth = 420f;
        private const float ScreenMargin = 24f;
        private const float Pad = 14f;
        private const float Gap = 8f;

        private const float SlotSize = 88f;
        private const float RowHeight = 46f;

        private static readonly Color PanelColor  = new Color(0.10f, 0.10f, 0.12f, 0.94f);
        private static readonly Color SlotEmpty   = new Color(0.16f, 0.17f, 0.20f);
        private static readonly Color RowPlain    = new Color(0.19f, 0.20f, 0.24f);
        private static readonly Color RowPicked   = new Color(0.24f, 0.48f, 0.72f);
        private static readonly Color RowBlocked  = new Color(0.15f, 0.15f, 0.17f);
        private static readonly Color TitleColor  = new Color(0.88f, 0.90f, 0.94f);
        private static readonly Color NoteColor   = new Color(0.66f, 0.68f, 0.72f);
        private static readonly Color WarnColor   = new Color(0.90f, 0.68f, 0.32f);
        private static readonly Color DimText     = new Color(0.45f, 0.46f, 0.50f);

        /// <summary>지금 고른 동료. <b>구멍이 없다</b> — 순서가 곧 교대 순서다.</summary>
        private readonly List<PartyMemberData> picked = new List<PartyMemberData>(PartyLoadout.MaxMembers);

        /// <summary>슬롯 위젯. 항상 <see cref="PartyLoadout.MaxMembers"/>칸.</summary>
        private readonly List<(Image bg, Text name, Text role)> slots =
            new List<(Image, Text, Text)>(PartyLoadout.MaxMembers);

        /// <summary>명단 줄. 표 하나당 하나.</summary>
        private readonly List<(PartyMemberData member, Image bg, Text mark, Text cards)> rows =
            new List<(PartyMemberData, Image, Text, Text)>();

        /// <summary>
        /// 주인공 줄. 주인공이 하나뿐이면 짓지 않는다 — 고를 것이 없는 라디오 버튼이다.
        /// </summary>
        private readonly List<(PlayerData hero, Image bg)> heroRows = new List<(PlayerData, Image)>();

        /// <summary>고른 주인공. 태그 로스터 0번이고, 카드는 안 낸다.</summary>
        private PlayerData hero;

        private Text summary;
        private Text warning;

        /// <summary>
        /// <c>GameManager</c>에 넘기는 조합. 에셋이 아니라 이 화면이 만든 인스턴스다.
        /// 고를 때마다 <see cref="PartyLoadout.Fill"/>로 내용만 갈아 끼운다.
        /// </summary>
        private PartyLoadout runtime;

        /// <summary>메인화면에 띄운다. 씬과 함께 언로드되도록 컨트롤러의 자식으로 붙인다.</summary>
        public static PartySelectUI Create(MonoBehaviour owner)
        {
            if (owner == null) return null;

            var go = new GameObject("PartySelectUI", typeof(RectTransform));
            go.transform.SetParent(owner.transform, false);

            return go.AddComponent<PartySelectUI>();
        }

        /// <summary>
        /// 지금 편성. 읽기 전용 창구다 — 확정된 값의 주인은 <c>GameManager.Loadout</c>이고,
        /// <see cref="MainMenuController"/>도 이 화면이 아니라 그쪽에 물어본다.
        /// 이 화면이 없는 경로(표를 아직 안 구운 상태)에서도 같은 답이 나와야 하기 때문이다.
        /// </summary>
        public IReadOnlyList<PartyMemberData> Picked => picked;

        private void Start()
        {
            IReadOnlyList<PartyMemberData> roster = PartyCatalog.AllMembers();

            // 표가 아직 없다. 'Prototype ▸ 파티 - 1단계: 씬에서 표 추출'을 안 돌린 상태다.
            if (roster.Count == 0) { gameObject.SetActive(false); return; }

            SeedFromCurrent(roster);
            Build(roster);
            Refresh();
        }

        /// <summary>
        /// 화면을 열 때의 초기값. 이미 고른 조합이 있으면 그걸, 없으면 기본 조합을 편다.
        ///
        /// <b>명단에 없는 표는 버린다.</b> 기본 조합이 Resources 밖의 에셋을 물고 있을 수 있는데,
        /// 그대로 두면 화면에서 뺄 수도 없는 동료가 파티에 끼어 있게 된다.
        /// </summary>
        private void SeedFromCurrent(IReadOnlyList<PartyMemberData> roster)
        {
            GameManager gm = GameManager.Instance;

            PartyLoadout seed = gm != null && gm.Loadout != null ? gm.Loadout : PartyCatalog.Default();

            // 주인공은 항상 하나 정해져 있어야 한다 — 표가 없으면 null이고,
            // 그때는 Player 프리팹의 고정 스펙으로 돈다(지금까지와 같다).
            IReadOnlyList<PlayerData> allHeroes = PartyCatalog.AllHeroes();
            hero = seed != null && seed.hero != null
                ? seed.hero
                : (allHeroes.Count > 0 ? allHeroes[0] : null);

            if (seed == null || seed.members == null) return;

            foreach (PartyMemberData m in seed.members)
            {
                if (m == null || picked.Count >= PartyLoadout.MaxMembers) continue;
                if (!Contains(roster, m) || picked.Contains(m)) continue;

                picked.Add(m);
            }
        }

        private static bool Contains(IReadOnlyList<PartyMemberData> list, PartyMemberData m)
        {
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], m)) return true;

            return false;
        }

        // ── 짓기 ────────────────────────────────────────

        private void Build(IReadOnlyList<PartyMemberData> roster)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // 화면 오른쪽에 세로로 붙인다. 메인화면 버튼은 가운데라 겹치지 않는다.
            Image panel = UiFactory.NewImage(transform, "Panel", PanelColor, raycast: true);
            RectTransform p = panel.rectTransform;
            p.anchorMin = new Vector2(1f, 0f);
            p.anchorMax = new Vector2(1f, 1f);
            p.pivot = new Vector2(1f, 0.5f);
            p.offsetMin = new Vector2(-PanelWidth - ScreenMargin, ScreenMargin);
            p.offsetMax = new Vector2(-ScreenMargin, -ScreenMargin);

            float y = Pad;
            float inner = PanelWidth - Pad * 2f;

            Text title = UiFactory.NewText(p, "Title", 26, TitleColor, FontStyle.Bold);
            Row(title.rectTransform, ref y, 34f, inner);
            title.text = "파티 편성";
            title.alignment = TextAnchor.MiddleLeft;

            Text hint = UiFactory.NewText(p, "Hint", 14, DimText, FontStyle.Normal);
            Row(hint.rectTransform, ref y, 22f, inner);
            hint.text = "고른 순서가 교대(F) 순서가 된다";
            hint.alignment = TextAnchor.MiddleLeft;

            y += Gap;
            BuildHeroes(p, ref y, inner);
            BuildSlots(p, ref y, inner);

            y += Gap;
            summary = UiFactory.NewText(p, "Summary", 17, NoteColor, FontStyle.Bold);
            Row(summary.rectTransform, ref y, 24f, inner);
            summary.alignment = TextAnchor.MiddleLeft;

            warning = UiFactory.NewText(p, "Warning", 14, WarnColor, FontStyle.Normal);
            Row(warning.rectTransform, ref y, 22f, inner);
            warning.alignment = TextAnchor.MiddleLeft;

            y += Gap;
            Text header = UiFactory.NewText(p, "RosterHeader", 15, DimText, FontStyle.Bold);
            Row(header.rectTransform, ref y, 22f, inner);
            header.text = $"동료 {roster.Count}명";
            header.alignment = TextAnchor.MiddleLeft;

            BuildRoster(p, roster, y);
        }

        /// <summary>
        /// 주인공 선택. <b>둘 이상일 때만 짓는다</b> — 하나뿐이면 고를 것이 없는 라디오 버튼이라
        /// 화면만 차지한다. 주인공 표를 하나 더 만들면 저절로 나타난다.
        ///
        /// 여기서 갈리는 것은 카드가 아니라 <b>평타와 조작감</b>(대시 쿨 · 선입력 창)이라,
        /// 덱 장수 요약에는 영향을 주지 않는다.
        /// </summary>
        private void BuildHeroes(RectTransform panel, ref float y, float inner)
        {
            IReadOnlyList<PlayerData> all = PartyCatalog.AllHeroes();
            if (all.Count <= 1) return;

            Text header = UiFactory.NewText(panel, "HeroHeader", 15, DimText, FontStyle.Bold);
            Row(header.rectTransform, ref y, 22f, inner);
            header.text = "주인공";
            header.alignment = TextAnchor.MiddleLeft;

            RectTransform row = UiFactory.NewRect(panel, "Heroes");
            Row(row, ref y, RowHeight, inner);

            float w = (inner - Gap * (all.Count - 1)) / all.Count;

            for (int i = 0; i < all.Count; i++)
            {
                PlayerData h = all[i];
                if (h == null) continue;

                Image bg = UiFactory.NewImage(row, h.heroId, RowPlain, raycast: true);
                RectTransform r = bg.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(i * (w + Gap), 0f);
                r.sizeDelta = new Vector2(w, RowHeight);

                Text label = UiFactory.NewText(r, "Label", 17, Color.white, FontStyle.Normal);
                UiFactory.Stretch(label.rectTransform, 6f);
                label.alignment = TextAnchor.MiddleCenter;
                label.text = h.Label;

                PlayerData captured = h;
                bg.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectHero(captured));

                heroRows.Add((h, bg));
            }

            y += Gap;
        }

        private void BuildSlots(RectTransform panel, ref float y, float inner)
        {
            RectTransform row = UiFactory.NewRect(panel, "Slots");
            Row(row, ref y, SlotSize, inner);

            float w = (inner - Gap * (PartyLoadout.MaxMembers - 1)) / PartyLoadout.MaxMembers;

            for (int i = 0; i < PartyLoadout.MaxMembers; i++)
            {
                Image bg = UiFactory.NewImage(row, "Slot" + i, SlotEmpty, raycast: true);
                RectTransform r = bg.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(i * (w + Gap), 0f);
                r.sizeDelta = new Vector2(w, SlotSize);

                Text order = UiFactory.NewText(r, "Order", 13, DimText, FontStyle.Normal);
                order.rectTransform.anchorMin = new Vector2(0f, 1f);
                order.rectTransform.anchorMax = new Vector2(1f, 1f);
                order.rectTransform.pivot = new Vector2(0.5f, 1f);
                order.rectTransform.anchoredPosition = new Vector2(0f, -6f);
                order.rectTransform.sizeDelta = new Vector2(0f, 16f);
                order.text = (i + 1).ToString();

                Text name = UiFactory.NewText(r, "Name", 17, Color.white, FontStyle.Bold);
                UiFactory.Stretch(name.rectTransform, 4f);
                name.alignment = TextAnchor.MiddleCenter;

                Text role = UiFactory.NewText(r, "Role", 13, NoteColor, FontStyle.Normal);
                role.rectTransform.anchorMin = new Vector2(0f, 0f);
                role.rectTransform.anchorMax = new Vector2(1f, 0f);
                role.rectTransform.pivot = new Vector2(0.5f, 0f);
                role.rectTransform.anchoredPosition = new Vector2(0f, 6f);
                role.rectTransform.sizeDelta = new Vector2(0f, 16f);

                int slot = i;
                bg.gameObject.AddComponent<Button>().onClick.AddListener(() => RemoveAt(slot));

                slots.Add((bg, name, role));
            }
        }

        /// <summary>
        /// 명단. <see cref="ScrollRect"/>로 감싼다 — 동료가 늘어나면 화면을 넘치는데,
        /// 그때 조용히 잘리면 "그 동료는 못 데려간다"로 보인다.
        /// </summary>
        private void BuildRoster(RectTransform panel, IReadOnlyList<PartyMemberData> roster, float top)
        {
            Image viewport = UiFactory.NewImage(panel, "Viewport", new Color(0f, 0f, 0f, 0.25f), raycast: true);
            RectTransform v = viewport.rectTransform;
            v.anchorMin = new Vector2(0f, 0f);
            v.anchorMax = new Vector2(1f, 1f);
            v.pivot = new Vector2(0.5f, 1f);
            v.offsetMin = new Vector2(Pad, Pad);
            v.offsetMax = new Vector2(-Pad, -top);

            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = UiFactory.NewRect(v, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, roster.Count * (RowHeight + 4f));

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = v;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 24f;

            // Elastic(기본)은 명단이 화면보다 짧을 때도 끌려 늘어난다 — 지금은 넷뿐이라
            // 그 상태가 기본값이고, 늘어났다 돌아오는 게 고장처럼 보인다.
            scroll.movementType = ScrollRect.MovementType.Clamped;

            for (int i = 0; i < roster.Count; i++)
            {
                PartyMemberData m = roster[i];
                if (m == null) continue;

                Image bg = UiFactory.NewImage(content, m.memberId, RowPlain, raycast: true);
                RectTransform r = bg.rectTransform;
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = new Vector2(1f, 1f);
                r.pivot = new Vector2(0.5f, 1f);
                r.anchoredPosition = new Vector2(0f, -i * (RowHeight + 4f));
                r.sizeDelta = new Vector2(0f, RowHeight);

                // 직업 색 띠. 초상화가 없어도 넷을 구분할 수 있어야 한다.
                Image band = UiFactory.NewImage(r, "Band", SkillCutinUI.RoleColor(m.role));
                RectTransform b = band.rectTransform;
                b.anchorMin = new Vector2(0f, 0f);
                b.anchorMax = new Vector2(0f, 1f);
                b.pivot = new Vector2(0f, 0.5f);
                b.sizeDelta = new Vector2(6f, 0f);
                b.anchoredPosition = Vector2.zero;

                Text mark = UiFactory.NewText(r, "Mark", 18, Color.white, FontStyle.Bold);
                mark.rectTransform.anchorMin = new Vector2(0f, 0f);
                mark.rectTransform.anchorMax = new Vector2(0f, 1f);
                mark.rectTransform.pivot = new Vector2(0f, 0.5f);
                mark.rectTransform.anchoredPosition = new Vector2(14f, 0f);
                mark.rectTransform.sizeDelta = new Vector2(26f, 0f);
                mark.alignment = TextAnchor.MiddleLeft;

                Text label = UiFactory.NewText(r, "Label", 18, Color.white, FontStyle.Normal);
                UiFactory.Stretch(label.rectTransform, 10f);
                label.rectTransform.offsetMin = new Vector2(44f, 0f);
                label.alignment = TextAnchor.MiddleLeft;
                label.text = $"{m.Label}  <color=#9AA0A8>{RoleNames.Of(m.role)}</color>";
                label.supportRichText = true;

                Text cards = UiFactory.NewText(r, "Cards", 14, NoteColor, FontStyle.Normal);
                cards.rectTransform.anchorMin = new Vector2(1f, 0f);
                cards.rectTransform.anchorMax = new Vector2(1f, 1f);
                cards.rectTransform.pivot = new Vector2(1f, 0.5f);
                cards.rectTransform.anchoredPosition = new Vector2(-10f, 0f);
                cards.rectTransform.sizeDelta = new Vector2(120f, 0f);
                cards.alignment = TextAnchor.MiddleRight;

                PartyMemberData captured = m;
                bg.gameObject.AddComponent<Button>().onClick.AddListener(() => Toggle(captured));

                rows.Add((m, bg, mark, cards));
            }
        }

        private static void Row(RectTransform rect, ref float y, float height, float width)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(Pad, -y);
            rect.sizeDelta = new Vector2(width, height);

            y += height + Gap;
        }

        // ── 고르기 ──────────────────────────────────────

        private void Toggle(PartyMemberData member)
        {
            if (!PartyAssembleRules.Toggle(picked, member, PartyLoadout.MaxMembers)) return;
            Refresh();
        }

        /// <summary>주인공은 라디오다 — 언제나 정확히 하나. 같은 걸 다시 눌러도 안 비워진다.</summary>
        private void SelectHero(PlayerData value)
        {
            if (value == null || ReferenceEquals(hero, value)) return;

            hero = value;
            Refresh();
        }

        private void RemoveAt(int slot)
        {
            if (slot < 0 || slot >= picked.Count) return;

            picked.RemoveAt(slot);
            Refresh();
        }

        /// <summary>
        /// 화면을 다시 그리고 <b>고른 결과를 즉시 넘긴다</b>.
        ///
        /// 런타임 조합은 <b>하나만 만들어 계속 고쳐 쓴다</b>. 누를 때마다 새로 만들면
        /// 버려진 ScriptableObject 인스턴스가 쌓이고, <c>GameManager</c>가 들고 있는 것이
        /// 매번 다른 물건이 된다.
        /// </summary>
        private void Refresh()
        {
            if (runtime == null) runtime = PartyLoadout.CreateRuntime(picked);
            else runtime.Fill(picked);

            runtime.SetHero(hero);
            GameManager.Instance?.SelectLoadout(runtime);

            foreach ((PlayerData h, Image bg) in heroRows)
                bg.color = ReferenceEquals(h, hero) ? RowPicked : RowPlain;

            for (int i = 0; i < slots.Count; i++)
            {
                (Image bg, Text name, Text role) = slots[i];
                PartyMemberData m = i < picked.Count ? picked[i] : null;

                bg.color = m != null ? SkillCutinUI.RoleColor(m.role) : SlotEmpty;
                name.text = m != null ? m.Label : "";
                role.text = m != null ? RoleNames.Of(m.role) : "비어 있음";
                role.color = m != null ? new Color(1f, 1f, 1f, 0.75f) : DimText;
            }

            bool full = picked.Count >= PartyLoadout.MaxMembers;

            foreach ((PartyMemberData m, Image bg, Text mark, Text cards) in rows)
            {
                int at = picked.IndexOf(m);
                bool inParty = at >= 0;

                bg.color = inParty ? RowPicked : (full ? RowBlocked : RowPlain);
                mark.text = inParty ? (at + 1).ToString() : "";

                int n = CountCards(m);
                cards.text = n == Ally.EquipSlots ? $"{n}장" : $"{n}장 (목표 {Ally.EquipSlots})";
                cards.color = n == Ally.EquipSlots ? NoteColor : WarnColor;
            }

            RefreshSummary();
        }

        private void RefreshSummary()
        {
            int cards = PartyAssembleRules.CardCount(picked);
            int target = DeckRules.TargetSize(picked.Count);

            // 목표는 상수 16이 아니라 인원 × 4다. 3인 파티의 12장은 정상이고,
            // 그 사실을 "부족"으로 칠하면 정상 편성이 오류처럼 보인다.
            summary.text = $"{picked.Count}인 파티 · 시작 덱 {cards}장" +
                           (cards > 0 ? $" · 한 바퀴 {DeckRules.CycleHands(cards)}핸드" : "");
            summary.color = DeckRules.Matches(cards, target) ? NoteColor : WarnColor;

            warning.text = FirstProblem(cards);
        }

        /// <summary>
        /// 문제를 <b>하나만</b> 보여 준다. 세 줄을 한꺼번에 띄우면 무엇부터 고쳐야 하는지
        /// 알 수 없고, 어차피 하나를 고치면 다음 것이 뜬다.
        /// </summary>
        private string FirstProblem(int cards)
        {
            if (!PartyAssembleRules.CanStart(picked))
                return "동료를 한 명 이상 골라야 시작할 수 있다";

            foreach (PartyMemberData m in picked)
                if (!PartyAssembleRules.IsValid(m, out string reason))
                    return reason;

            // 인원 × 4와 안 맞는다 = 장착 칸이 빈 동료가 있다. 인원이 적어서 작은 덱은
            // 여기 안 걸린다 — 그건 목표 자체가 줄어드는 경우다.
            string authoring = DeckRules.Explain(cards, picked.Count);
            if (authoring != null) return authoring;

            List<Role> dup = PartyAssembleRules.DuplicateRoles(picked);
            if (dup.Count > 0)
                return $"{RoleNames.Of(dup[0])}가 둘이다 — 덱이 그쪽으로 기운다";

            // 오류가 아니라 트레이드오프다. 그래서 마지막에, 그리고 "모자라다"가 아니라
            // 무엇이 달라지는지로 적는다.
            if (picked.Count < PartyLoadout.MaxMembers)
                return $"{PartyLoadout.MaxMembers - picked.Count}자리 비었다 — " +
                       "덱이 작아 같은 카드가 더 자주 온다";

            return "";
        }

        private static int CountCards(PartyMemberData m)
        {
            if (m == null || m.equipped == null) return 0;

            int n = 0;
            foreach (ComboCard c in m.equipped)
                if (c != null && c.Data != null) n++;

            return n;
        }
    }
}
