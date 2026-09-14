// 비전투 칸의 창 — 휴식 · 상점 · 이벤트.
// 전부 코드로 짓는다. 그림이 생기기 전까지 이 창들이 곧 그 칸의 화면이다.
// 판단(값 · 고를 수 있는가)은 GoldRules · NodeRules · RunEventRules에 있고 여기는 그리기만 한다.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Prototype
{
    // ══ NodeWindows ═══════════════════════════════════════════

    public static class NodeWindows
    {
        public static readonly Color BackdropColor = new Color(0.04f, 0.05f, 0.07f, 1f);
        public static readonly Color PanelColor = new Color(0.11f, 0.12f, 0.15f, 0.98f);
        public static readonly Color TitleColor = new Color(1f, 0.86f, 0.45f);
        public static readonly Color BodyColor = new Color(0.85f, 0.87f, 0.90f);
        public static readonly Color SubColor = new Color(0.72f, 0.76f, 0.80f);
        public static readonly Color ButtonColor = new Color(0.24f, 0.30f, 0.38f);
        public static readonly Color ExitColor = new Color(0.30f, 0.32f, 0.36f);
        public static readonly Color WarnColor = new Color(1f, 0.55f, 0.45f);

        public static readonly Vector2 ExitSize = new Vector2(240f, 64f);

        /// <summary>화면을 덮는 바탕과 가운데 패널. 패널을 돌려준다.</summary>
        public static RectTransform Panel(Transform root, string title, Vector2 size)
        {
            Image backdrop = UiKit.NewImage(root, "Backdrop", BackdropColor, raycast: true);
            UiKit.Stretch(backdrop.rectTransform);

            Image panel = UiKit.NewImage(backdrop.transform, "Panel", PanelColor);
            RectTransform rect = UiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, size);

            Text t = UiKit.NewText(rect, "Title", 40, TitleColor, FontStyle.Bold);
            t.text = title;
            UiKit.Place(t, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(size.x, 60f));

            return rect;
        }

        /// <summary>패널 아래쪽 가운데 버튼 하나. [나가기] · [계속]이 쓴다.</summary>
        public static Button BottomButton(RectTransform panel, string label, Color color, Action onClick)
        {
            Button b = UiKit.CreateButton(panel, "Btn_" + label, label, color, ExitSize, 26);
            UiKit.Place(b, new Vector2(0.5f, 0f), new Vector2(0f, 60f), ExitSize);
            b.onClick.AddListener(() => onClick?.Invoke());
            return b;
        }

        /// <summary>
        /// 키보드로도 누를 수 있게 첫 칸을 잡아 둔다. 이 씬에는 전투 입력(<c>PlayerInputController</c>)이
        /// 없어서, EventSystem의 방향키 · 제출 입력이 유일한 키보드 통로다.
        /// </summary>
        public static void Focus(Selectable target)
        {
            if (target == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        /// <summary>
        /// 이 파티가 쓸 수 있는 카드 전부. 죽은 동료의 직업은 빠진다.
        /// 단독 실행이면 편성을 몰라 거르지 않는다(레벨업 화면의 폴백과 같다).
        /// </summary>
        public static List<SkillData> CardPool(IReadOnlyList<PartyMemberData> roster)
        {
            RunProgression run = RunProgression.Current;
            List<Role> roles = roster != null ? NodeRules.AvailableRoles(roster, run.Party) : null;
            return SkillCatalog.Pool(roles, run.Cards);
        }

        public static Text Line(RectTransform panel, string name, string content, int size, Color color,
                                Vector2 position, float width)
        {
            Text t = UiKit.CreateText(panel, name, content, size, color);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Place(t, new Vector2(0.5f, 0.5f), position, new Vector2(width, size * 4f));
            return t;
        }

        // ── 휴식 ────────────────────────────────────────

        /// <summary>회복은 이미 끝난 뒤다. 결과만 적고 [계속]을 낸다.</summary>
        public static void Rest(Transform root, float healRatio, Action onContinue)
        {
            RectTransform panel = Panel(root, "휴식", new Vector2(760f, 420f));

            Line(panel, "Body", $"모닥불 앞에서 숨을 고른다.\n파티 전원의 체력이 {healRatio:0%} 회복됐다.",
                 28, BodyColor, new Vector2(0f, 30f), 640f);

            Focus(BottomButton(panel, "계속", ButtonColor, onContinue));
        }
    }

    // ══ ShopWindow ═══════════════════════════════════════════

    /// <summary>
    /// 카드 세 장을 골드로 사는 창. 들어올 때 한 번 뽑고, 산 칸은 "구매함"으로 잠긴다.
    /// 레벨업 화면의 카드 그림(<see cref="CardOfferView"/>)을 그대로 쓴다 — 같은 카드가
    /// 두 화면에서 다르게 생기면 같은 물건으로 안 읽힌다.
    /// </summary>
    public class ShopWindow
    {
        private const float Gap = 40f;

        private readonly IReadOnlyList<PartyMemberData> roster;
        private readonly List<CardOffer> offers;
        private readonly CardOfferView[] views;
        private readonly Text[] prices;
        private readonly bool[] sold;
        private readonly Text gold;
        private readonly Text notice;

        private static RunProgression Run => RunProgression.Current;

        public ShopWindow(Transform root, IReadOnlyList<PartyMemberData> roster, Action onLeave)
        {
            this.roster = roster;

            RectTransform panel = NodeWindows.Panel(root, "상점", new Vector2(1180f, 760f));

            gold = UiKit.CreateText(panel, "Gold", "", 28, NodeWindows.TitleColor);
            UiKit.Place(gold, new Vector2(1f, 1f), new Vector2(-130f, -50f), new Vector2(220f, 40f));

            notice = UiKit.CreateText(panel, "Notice", "", 22, NodeWindows.WarnColor);
            UiKit.Place(notice, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(900f, 36f));

            offers = CardOfferRules.Build(Pool(), Run.Cards, NodeRules.ShopOfferTier, () => UnityEngine.Random.value);
            views = new CardOfferView[offers.Count];
            prices = new Text[offers.Count];
            sold = new bool[offers.Count];

            float step = CardOfferView.Width + Gap;
            float left = -step * (offers.Count - 1) * 0.5f;

            for (int i = 0; i < offers.Count; i++)
            {
                var view = new CardOfferView();
                view.Build(panel, i);
                UiKit.Place(view.Root, new Vector2(0.5f, 0.5f), new Vector2(left + step * i, 60f), view.Root.sizeDelta);
                view.Show(offers[i]);
                view.OnPicked += Buy;
                views[i] = view;

                prices[i] = UiKit.CreateText(panel, $"Price_{i}", "", 26, Color.white);
                UiKit.Place(prices[i], new Vector2(0.5f, 0.5f),
                            new Vector2(left + step * i, 60f - CardOfferView.Height * 0.5f - 36f),
                            new Vector2(CardOfferView.Width, 36f));
            }

            if (offers.Count == 0) notice.text = "살 수 있는 카드가 없다.";

            Button exit = NodeWindows.BottomButton(panel, "나가기", NodeWindows.ExitColor, onLeave);
            Refresh();

            // 살 수 있는 첫 카드에 커서를 둔다. 하나도 못 사면 [나가기]다.
            Selectable first = exit;
            for (int i = 0; i < views.Length; i++)
                if (Run.Gold >= GoldRules.PriceOf(offers[i]))
                {
                    first = views[i].Root.GetComponentInChildren<Button>();
                    break;
                }

            NodeWindows.Focus(first);
        }

        private List<SkillData> Pool() => NodeWindows.CardPool(roster);

        private void Buy(int index)
        {
            if (index < 0 || index >= offers.Count || sold[index]) return;

            int price = GoldRules.PriceOf(offers[index]);
            if (!Run.TrySpendGold(price))
            {
                notice.text = $"골드가 모자라다 ({Run.Gold} / {price})";
                return;
            }

            ComboCard card = Run.Grant(offers[index]);
            sold[index] = true;
            notice.text = card != null ? $"{card.Data.skillName} 을(를) 샀다." : "";

            Debug.Log($"[Shop] 구매 — {offers[index].data.skillName} {price}골드 (남은 골드 {Run.Gold})");
            Refresh();
        }

        private void Refresh()
        {
            gold.text = $"골드 {Run.Gold}";

            for (int i = 0; i < offers.Count; i++)
            {
                int price = GoldRules.PriceOf(offers[i]);
                bool affordable = Run.Gold >= price;

                views[i].SetInteractable(!sold[i] && affordable);
                prices[i].text = sold[i] ? "구매함" : $"{price} 골드";
                prices[i].color = sold[i] ? NodeWindows.SubColor : affordable ? Color.white : NodeWindows.WarnColor;
            }
        }
    }

    // ══ EventWindow ═══════════════════════════════════════════

    /// <summary>
    /// 글 한 토막과 선택지 버튼. 하나를 고르면 결과를 적고 [계속]만 남는다 —
    /// 되돌리거나 두 번 고를 수 없다.
    /// </summary>
    public class EventWindow
    {
        private static readonly Vector2 ChoiceSize = new Vector2(760f, 64f);

        private readonly IReadOnlyList<PartyMemberData> roster;
        private readonly Action onLeave;
        private readonly RectTransform panel;
        private readonly List<Button> choiceButtons = new List<Button>();
        private readonly Text body;

        private static RunProgression Run => RunProgression.Current;

        public EventWindow(Transform root, RunEventAsset asset, IReadOnlyList<PartyMemberData> roster, Action onLeave)
        {
            this.roster = roster;
            this.onLeave = onLeave;

            panel = NodeWindows.Panel(root, asset != null ? asset.title : "이벤트", new Vector2(1000f, 700f));

            Text gold = UiKit.CreateText(panel, "Gold", $"골드 {Run.Gold}", 26, NodeWindows.TitleColor);
            UiKit.Place(gold, new Vector2(1f, 1f), new Vector2(-120f, -50f), new Vector2(200f, 40f));

            body = NodeWindows.Line(panel, "Body", asset != null ? asset.body : "(이벤트가 비어 있다)",
                                    26, NodeWindows.BodyColor, new Vector2(0f, 170f), 860f);

            if (asset == null || asset.ChoiceCount == 0)
            {
                ShowContinue();
                return;
            }

            for (int i = 0; i < asset.choices.Length; i++)
            {
                RunEventChoice choice = asset.choices[i];
                bool can = RunEventRules.CanChoose(in choice, Run.Gold);

                string label = can ? choice.label : $"{choice.label}  (골드 {-choice.goldDelta} 필요)";
                Button b = UiKit.CreateButton(panel, $"Choice_{i}", label, NodeWindows.ButtonColor, ChoiceSize, 24);
                UiKit.Place(b, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f - i * (ChoiceSize.y + 18f)), ChoiceSize);
                b.interactable = can;
                b.onClick.AddListener(() => Choose(choice));

                choiceButtons.Add(b);
            }

            foreach (Button b in choiceButtons)
                if (b.interactable) { NodeWindows.Focus(b); return; }

            // 하나도 못 고르면 갇힌다. 저작 검사(NodeSceneWiringTests)가 막지만 씬 밖 애셋까지는 못 본다.
            ShowContinue();
        }

        private void Choose(RunEventChoice choice)
        {
            if (!RunEventRules.Apply(in choice, Run, roster, NodeWindows.CardPool(roster),
                                     () => UnityEngine.Random.value, out ComboCard card))
                return;

            foreach (Button b in choiceButtons) b.gameObject.SetActive(false);

            string outcome = string.IsNullOrWhiteSpace(choice.outcome) ? "" : choice.outcome + "\n\n";
            string cardLine = card != null ? $"\n{card.Data.skillName} 카드를 얻었다." : "";
            body.text = $"{outcome}{RunEventRules.Summary(in choice)}{cardLine}";

            Debug.Log($"[Event] 선택 — {choice.label} · {RunEventRules.Summary(in choice)} (골드 {Run.Gold})");
            ShowContinue();
        }

        private void ShowContinue()
            => NodeWindows.Focus(NodeWindows.BottomButton(panel, "계속", NodeWindows.ButtonColor, onLeave));
    }
}
