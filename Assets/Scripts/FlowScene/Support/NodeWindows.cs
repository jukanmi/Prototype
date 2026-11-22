// 비전투 칸 · 마을의 창 — 공용 조각과 상점 창.
// 전부 코드로 짓는다. 판단(값 · 살 수 있는가)은 GoldRules · NodeRules에 있고 여기는 그리기만 한다.
// 이벤트 · 모닥불은 창이 아니라 화면 아래 대화창(DialogueWindow)에서 이어진다.

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
        public static readonly Color BackdropColor = UiTheme.Ink.WithAlpha(1f);
        public static readonly Color PanelColor = UiTheme.Ink.WithAlpha(0.97f);
        public static readonly Color TitleColor = UiTheme.Gold;
        public static readonly Color BodyColor = UiTheme.TextMain;
        public static readonly Color SubColor = UiTheme.TextSub;
        public static readonly Color ButtonColor = UiTheme.ButtonNormal;
        public static readonly Color ExitColor = UiKit.ButtonFill(ButtonKind.Ghost);
        public static readonly Color WarnColor = UiTheme.Hp;

        public static readonly Vector2 ExitSize = new Vector2(240f, 64f);

        /// <summary>아래 대화창 윗변과 그 위에 얹는 패널 사이.</summary>
        public const float DialogueGap = 20f;

        /// <summary>화면을 덮는 바탕과 가운데 패널. 패널을 돌려준다.</summary>
        public static RectTransform Panel(Transform root, string title, Vector2 size)
        {
            Image backdrop = UiKit.NewImage(root, "Backdrop", BackdropColor, raycast: true);
            UiKit.Stretch(backdrop.rectTransform);

            return TitledPanel(backdrop.transform, title, size, new Vector2(0.5f, 0.5f), Vector2.zero);
        }

        /// <summary>
        /// 화면을 덮지 않고 아래 대화창(<see cref="DialogueWindow"/>) 바로 위에 뜨는 패널 — 칸 화면이 그대로 보인다.
        /// 조작 설명은 대화창이 맡는다(<see cref="DialogueWindow.Hold"/>).
        /// </summary>
        public static RectTransform PanelAboveDialogue(Transform root, string title, Vector2 size)
            => TitledPanel(root, title, size, new Vector2(0.5f, 0f), new Vector2(0f, DialogueWindow.Top + DialogueGap));

        private static RectTransform TitledPanel(Transform parent, string title, Vector2 size, Vector2 anchor, Vector2 position)
        {
            Image panel = UiKit.NewImage(parent, "Panel", PanelColor);
            RectTransform rect = UiKit.Place(panel, anchor, position, size);

            Text t = UiKit.NewText(rect, "Title", 40, TitleColor, FontStyle.Bold);
            t.text = title;
            UiKit.Place(t, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(size.x, 60f));

            return rect;
        }

        /// <summary>패널 아래쪽 가운데 버튼 하나. [닫기] 등이 쓴다.</summary>
        public static Button BottomButton(RectTransform panel, string label, Color color, Action onClick)
        {
            Button b = UiKit.CreateButton(panel, "Btn_" + label, label, color, ExitSize, 26);
            UiKit.Place(b, new Vector2(0.5f, 0f), new Vector2(0f, 60f), ExitSize);
            b.onClick.AddListener(() => onClick?.Invoke());
            return b;
        }

        /// <summary>
        /// 키보드로도 누를 수 있게 이 칸을 잡아 둔다(EventSystem 선택). 마을 창 · 일시정지가 쓴다.
        /// 상점 · 선택지는 따로 커서(<see cref="KeyMenu"/>)를 쓴다.
        /// </summary>
        public static void Focus(Selectable target)
        {
            if (target == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        /// <summary>
        /// 이 파티가 쓸 수 있는 카드 전부. 죽은 동료의 직업은 빠진다.
        /// 단독 실행이면 편성을 몰라 거르지 않는다.
        /// </summary>
        public static List<SkillData> CardPool(IReadOnlyList<PartyMemberData> roster)
        {
            RunProgression run = RunProgression.Current;
            List<Role> roles = roster != null ? NodeRules.AvailableRoles(roster, run.Party) : null;
            return SkillCatalog.Pool(roles, run.Cards);
        }
    }

    // ══ ShopWindow ═══════════════════════════════════════════

    /// <summary>
    /// 카드 세 장을 골드로 사는 창. 들어올 때 한 번 뽑고, 산 칸은 "구매함"으로 잠긴다.
    /// 카드 선택 그림(<see cref="CardOfferView"/>)을 그대로 쓴다 — 같은 카드가
    /// 두 화면에서 다르게 생기면 같은 물건으로 안 읽힌다.
    ///
    /// 키보드로 고른다(<see cref="KeyMenu"/>) — 좌우로 카드, 아래로 [닫기], F · Enter로 산다.
    /// 보고 있는 카드는 금색 테두리가 깜빡이고 조금 커진다. 마우스 클릭도 그대로 된다.
    /// </summary>
    public class ShopWindow
    {
        private const float Gap = 40f;

        /// <summary>대화창 위 · 런 바 아래에 들어가는 크기(대화창 윗변 + 간격에서 위로 700).</summary>
        private static readonly Vector2 Size = new Vector2(1180f, 700f);

        /// <summary>대화창에 적는 상점 조작. 키는 지금 묶인 키를 그때그때 묻는다.</summary>
        public static string Controls()
        {
            string key = InputHints.Key(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Interact);
            return $"방향키 · WASD  고르기     [{key}]  사기     ESC  닫기";
        }

        /// <summary>보고 있는 카드의 확대 배율. 테두리와 함께 "이 카드"를 가리킨다.</summary>
        private const float FocusScale = 1.06f;

        private readonly List<CardOffer> offers;
        private readonly CardOfferView[] views;
        private readonly Text[] prices;
        private readonly bool[] sold;
        private readonly Text gold;
        private readonly Text notice;
        private readonly KeyMenu menu;

        private static RunProgression Run => RunProgression.Current;

        /// <param name="onClose">[닫기]. 창만 닫는다 — 칸은 오른쪽 벽으로 걸어가야 끝난다.</param>
        public ShopWindow(Transform root, IReadOnlyList<PartyMemberData> roster, Action onClose)
        {
            // 화면을 덮지 않고 대화창 위에 뜬다. 조작 설명은 대화창에 적는다(Controls).
            RectTransform panel = NodeWindows.PanelAboveDialogue(root, "상점", Size);
            menu = new KeyMenu(panel);

            gold = UiKit.CreateText(panel, "Gold", "", 28, NodeWindows.TitleColor);
            UiKit.Place(gold, new Vector2(1f, 1f), new Vector2(-130f, -50f), new Vector2(220f, 40f));

            notice = UiKit.CreateText(panel, "Notice", "", 22, NodeWindows.WarnColor);
            UiKit.Place(notice, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(900f, 36f));

            offers = CardOfferRules.Build(NodeWindows.CardPool(roster), () => UnityEngine.Random.value);
            views = new CardOfferView[offers.Count];
            prices = new Text[offers.Count];
            sold = new bool[offers.Count];

            float step = CardOfferView.Width + Gap;
            float left = -step * (offers.Count - 1) * 0.5f;

            for (int i = 0; i < offers.Count; i++)
            {
                int captured = i;

                var view = new CardOfferView();
                view.Build(panel, i);
                UiKit.Place(view.Root, new Vector2(0.5f, 0.5f), new Vector2(left + step * i, 50f), view.Root.sizeDelta);
                view.Show(offers[i]);
                view.OnPicked += Buy;
                views[i] = view;

                prices[i] = UiKit.CreateText(panel, $"Price_{i}", "", 26, UiTheme.TextMain);
                UiKit.Place(prices[i], new Vector2(0.5f, 0.5f),
                            new Vector2(left + step * i, 50f - CardOfferView.Height * 0.5f - 36f),
                            new Vector2(CardOfferView.Width, 36f));

                menu.Add(view.Root, 0, () => CanBuy(captured), () => Buy(captured));
            }

            if (offers.Count == 0) notice.text = "살 수 있는 카드가 없다.";

            Button exit = NodeWindows.BottomButton(panel, "닫기", NodeWindows.ExitColor, onClose);
            menu.Add((RectTransform)exit.transform, 1, null, onClose);

            menu.Moved += (from, to) =>
            {
                Scale(from, 1f);
                Scale(to, FocusScale);
            };

            Refresh();
            KeyMenuHost.Attach(root.gameObject, menu);
        }

        private bool CanBuy(int index)
            => index >= 0 && index < offers.Count && !sold[index] && Run.Gold >= GoldRules.CardPriceNow();

        /// <summary>카드만 키운다. [닫기]는 테두리만으로 충분하다.</summary>
        private void Scale(int index, float scale)
        {
            if (index < 0 || index >= views.Length) return;
            views[index].Root.localScale = new Vector3(scale, scale, 1f);
        }

        private void Buy(int index)
        {
            if (index < 0 || index >= offers.Count || sold[index]) return;

            int price = GoldRules.CardPriceNow();
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
                int price = GoldRules.CardPriceNow();
                bool affordable = Run.Gold >= price;

                views[i].SetInteractable(!sold[i] && affordable);
                prices[i].text = sold[i] ? "구매함" : $"{price} 골드";
                prices[i].color = sold[i] ? NodeWindows.SubColor : affordable ? UiTheme.TextMain : NodeWindows.WarnColor;
            }

            // 방금 산 카드는 잠겼다 — 커서가 그 위에 남지 않게 다음 칸으로 옮긴다.
            menu.Refresh();
        }
    }
}
