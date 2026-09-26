// 카드 제안 — 후보 뽑기 규칙 · 지급 규칙 · 한 장을 그리는 뷰.
// 뽑고(CardOfferRules) 주고(CardGrantRules) 보여 주는(CardOfferView) 한 흐름이다.

using System.Collections.Generic;
using System;
using UnityEngine.UI;
using UnityEngine;

namespace Prototype
{
    // ══ CardOfferRules ═══════════════════════════════════════════

    /// <summary>레벨업 선택지 한 칸.</summary>
    public struct CardOffer
    {
        public SkillData data;

        /// <summary>황금으로 나왔는지. 합성이 서면 확률과 무관하게 참이다.</summary>
        public bool golden;

        /// <summary>
        /// 고르면 <b>합성</b>이 되는 자리. 이미 같은 카드 일반판을 두 장 들고 있다는 뜻이다.
        /// UI가 카드 우측 상단에 ★를 다는 근거이고, 고르면 그 두 장이 사라지고 황금 한 장이 된다.
        /// </summary>
        public bool fuses;

        public bool IsEmpty => data == null;
    }

    /// <summary>
    /// 레벨업 선택지를 짜는 규칙. 순수 함수다.
    ///
    /// <b>난수를 주입받는다.</b> <c>UnityEngine.Random</c>을 직접 부르면 20% · 50% 경계를
    /// 검증할 방법이 없어진다. 실사용은 <c>() =&gt; UnityEngine.Random.value</c>를 넘긴다.
    ///
    /// 소비 순서는 <b>고르기 먼저, 황금 판정 나중</b>이다 —
    /// 3장을 다 뽑은 뒤 그 순서대로 황금을 굴린다. 테스트가 이 순서에 기댄다.
    /// </summary>
    public static class CardOfferRules
    {
        /// <summary>한 번에 보여 주는 카드 수.</summary>
        public const int OfferCount = 3;

        /// <summary>몇 장이 모이면 합성인가. 두 장을 들고 있을 때 <b>세 장째</b>가 선택지에 뜨면 성립한다.</summary>
        public const int FuseCopies = 3;

        /// <summary>
        /// 선택지를 짠다. 모집단이 <see cref="OfferCount"/>보다 적으면 있는 만큼만 낸다.
        /// </summary>
        /// <param name="pool">뽑을 수 있는 스킬 전부. null·중복은 걸러진다.</param>
        /// <param name="owned">지금 덱에 든 카드. 합성 판정에 쓴다.</param>
        /// <param name="tier">0 · 1 · 2. 황금 확률을 정한다.</param>
        /// <param name="roll">0 이상 1 미만을 돌려주는 난수원.</param>
        public static List<CardOffer> Build(IReadOnlyList<SkillData> pool,
                                            IReadOnlyList<ComboCard> owned,
                                            int tier,
                                            Func<float> roll)
        {
            var offers = new List<CardOffer>(OfferCount);
            if (pool == null || roll == null) return offers;

            List<SkillData> bag = Distinct(pool);
            if (bag.Count == 0) return offers;

            int take = Math.Min(OfferCount, bag.Count);

            for (int i = 0; i < take; i++)
            {
                int index = Index(roll(), bag.Count);
                offers.Add(new CardOffer { data = bag[index] });
                bag.RemoveAt(index);
            }

            // 황금은 고른 순서 그대로 한 장씩 따로 굴린다. 선택지 전체에 한 번만 굴리면
            // "세 장 다 황금 아니면 세 장 다 일반"이 되어 고를 이유가 사라진다.
            float chance = ExpRules.GoldenChanceOf(tier);

            for (int i = 0; i < offers.Count; i++)
            {
                CardOffer offer = offers[i];

                bool fuses = CountNormalCopies(owned, offer.data) == FuseCopies - 1;

                // 합성은 확률을 이긴다. 어차피 황금이 되는 자리라 굴려 봐야 결과가 같다.
                // 다만 난수 소비 횟수는 유지한다 — 안 그러면 뒤 카드의 결과가 밀린다.
                bool rolled = roll() < chance;

                offer.fuses = fuses;
                offer.golden = fuses || rolled;

                offers[i] = offer;
            }

            return offers;
        }

        /// <summary>
        /// 이 스킬의 <b>일반</b> 카드를 몇 장 들고 있는지. 황금은 세지 않는다 —
        /// 황금 위의 등급이 없어서 합성해 봐야 갈 곳이 없다.
        /// </summary>
        public static int CountNormalCopies(IReadOnlyList<ComboCard> owned, SkillData data)
        {
            if (owned == null || data == null) return 0;

            int n = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                ComboCard c = owned[i];
                if (c != null && !c.Golden && c.Data == data) n++;
            }

            return n;
        }

        /// <summary>0 이상 1 미만을 칸 번호로. 1이 들어와도 범위를 넘지 않게 물린다.</summary>
        private static int Index(float value, int count)
        {
            int i = (int)(value * count);
            if (i < 0) return 0;
            return i >= count ? count - 1 : i;
        }

        private static List<SkillData> Distinct(IReadOnlyList<SkillData> pool)
        {
            var bag = new List<SkillData>(pool.Count);

            for (int i = 0; i < pool.Count; i++)
            {
                SkillData d = pool[i];
                if (d != null && !bag.Contains(d)) bag.Add(d);
            }

            return bag;
        }
    }

    // ══ CardGrantRules ═══════════════════════════════════════════

    /// <summary>
    /// 고른 선택지를 덱에 반영하는 규칙. 순수 함수라 리스트만 넘기면 된다.
    ///
    /// <b>합성은 덱을 늘리지 않는다</b> — 들고 있던 일반 두 장이 사라지고 황금 한 장이 들어오므로
    /// 장수는 오히려 하나 줄고 질이 오른다. 이게 "같은 카드만 계속 나오는" 답답함의 출구다.
    /// </summary>
    public static class CardGrantRules
    {
        /// <summary>
        /// 고른 카드를 덱에 넣는다. <b>실제로 들어간 카드</b>를 돌려준다(실패하면 null) —
        /// 부르는 쪽이 같은 인스턴스를 손패에도 꽂으므로, 새로 만들게 두면 런 덱과 손패에
        /// 서로 다른 몸이 하나씩 생긴다.
        ///
        /// <paramref name="pick"/>이 합성인데 보유분이 모자라면 <b>합성 없이 한 장만</b> 들어온다.
        /// 선택지를 짠 뒤 덱이 바뀌었을 때(동료 사망으로 카드가 걷힌 경우 등) 조용히 어긋나느니
        /// 카드 한 장이라도 손에 쥐여 주는 편이 낫다.
        /// </summary>
        public static ComboCard Apply(List<ComboCard> deck, in CardOffer pick)
        {
            if (deck == null || pick.IsEmpty) return null;

            if (pick.fuses)
                RemoveNormalCopies(deck, pick.data, CardOfferRules.FuseCopies - 1);

            var card = new ComboCard(pick.data, WeightOf(pick.data), pick.golden);
            deck.Add(card);

            return card;
        }

        /// <summary>
        /// 일반 카드를 최대 <paramref name="count"/>장까지 걷어낸다. 실제로 걷어낸 장수를 돌려준다.
        /// 황금은 건드리지 않는다.
        /// </summary>
        public static int RemoveNormalCopies(List<ComboCard> deck, SkillData data, int count)
        {
            if (deck == null || data == null || count <= 0) return 0;

            int removed = 0;

            for (int i = deck.Count - 1; i >= 0 && removed < count; i--)
            {
                ComboCard c = deck[i];
                if (c == null || c.Golden || c.Data != data) continue;

                deck.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// 새 카드의 드로우 가중치. 시작 덱을 굽는 SkillTableBuilder가 시동기에 2를 주는 것과
        /// 같은 규칙이라, 레벨업으로 얻은 카드가 시작 덱과 다르게 굴지 않는다.
        /// </summary>
        public static float WeightOf(SkillData data)
            => data != null && data.IsStarterType ? 2f : 1f;
    }

    // ══ CardOfferView ═══════════════════════════════════════════

    /// <summary>
    /// 레벨업 선택지 카드 <b>한 장</b>의 그림. <c>BulletTimeGaugeWidget</c>과 같은 꼴로,
    /// MonoBehaviour가 아니라 <see cref="Build"/>로 지어지고 참조만 들고 있는 위젯이다.
    ///
    /// <b>황금 테두리는 카드 뒤에 한 겹 더 깐 금색 판이다.</b> 카드 배경색을 금색으로 바꾸는
    /// 방식은 못 쓴다 — 손패(<see cref="ComboBoardUI"/>)에서 배경색은 이미 다음 카드 · 체인 ·
    /// 조준 · 집힘을 나타내는 자리라, 등급까지 같은 채널에 실으면 둘 다 안 읽힌다.
    /// </summary>
    public class CardOfferView
    {
        public const float Width = 260f;
        public const float Height = 360f;

        /// <summary>테두리가 카드 밖으로 삐져나오는 두께.</summary>
        private const float FrameInset = 6f;

        /// <summary>아트가 카드 안에서 물러나는 여백.</summary>
        private const float ArtInset = 12f;

        private static readonly Color FrameColor = new Color(0.08f, 0.09f, 0.11f);
        private static readonly Color GoldColor = new Color(1f, 0.80f, 0.28f);
        private static readonly Color CardColor = new Color(0.22f, 0.26f, 0.30f);
        private static readonly Color NameColor = Color.white;
        private static readonly Color SubColor = new Color(0.72f, 0.76f, 0.80f);
        private static readonly Color GoldTextColor = new Color(1f, 0.86f, 0.45f);

        private Image frame;
        private Image art;
        private Text nameLabel;
        private Text subLabel;
        private Text gradeLabel;
        private GameObject starBadge;
        private Button button;

        public RectTransform Root { get; private set; }

        /// <summary>이 칸을 눌렀을 때. 인덱스를 넘긴다.</summary>
        public event Action<int> OnPicked;

        private int index;

        public void Build(Transform parent, int slot)
        {
            index = slot;

            // 테두리 판이 곧 카드의 루트다. 카드 본체는 이 안에 FrameInset만큼 물려 들어간다.
            frame = UiKit.CreateImage(parent, $"Offer_{slot}", FrameColor).GetComponent<Image>();
            frame.raycastTarget = false;

            Root = (RectTransform)frame.transform;
            Root.sizeDelta = new Vector2(Width + FrameInset * 2f, Height + FrameInset * 2f);

            GameObject bodyGo = UiKit.CreateImage(Root, "Body", CardColor);
            var body = (RectTransform)bodyGo.transform;
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(FrameInset, FrameInset);
            body.offsetMax = new Vector2(-FrameInset, -FrameInset);

            Image bodyImage = bodyGo.GetComponent<Image>();
            bodyImage.raycastTarget = true;

            button = bodyGo.AddComponent<Button>();
            button.targetGraphic = bodyImage;
            button.onClick.AddListener(() => OnPicked?.Invoke(index));

            BuildArt(body);
            BuildLabels(body);
            BuildStar(Root);
        }

        /// <summary>아트는 카드 위쪽 절반을 채운다. 아이콘이 없는 스킬은 그냥 비워 둔다.</summary>
        private void BuildArt(RectTransform body)
        {
            GameObject go = UiKit.CreateImage(body, "Art", Color.white);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0.38f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(ArtInset, ArtInset);
            rect.offsetMax = new Vector2(-ArtInset, -ArtInset);

            art = go.GetComponent<Image>();
            art.preserveAspect = true;
            art.raycastTarget = false;
            art.enabled = false;
        }

        private void BuildLabels(RectTransform body)
        {
            nameLabel = UiKit.CreateText(body, "Name", "", 24, NameColor);
            Anchor(nameLabel, new Vector2(0f, 0.26f), new Vector2(1f, 0.38f));

            subLabel = UiKit.CreateText(body, "Sub", "", 16, SubColor);
            Anchor(subLabel, new Vector2(0f, 0.16f), new Vector2(1f, 0.26f));

            gradeLabel = UiKit.CreateText(body, "Grade", "", 17, GoldTextColor);
            Anchor(gradeLabel, new Vector2(0f, 0.04f), new Vector2(1f, 0.15f));
        }

        /// <summary>
        /// 합성 표시. 우측 상단 금색 배지 안에 ★를 얹는다.
        /// 배지를 깔아 두는 이유는 폰트에 ★ 글리프가 없어도 <b>무언가 붙어 있다</b>는 것은
        /// 읽히게 하기 위해서다(<c>StageResultUI</c>가 화살표를 사각형으로 지은 것과 같은 이유).
        /// </summary>
        private void BuildStar(RectTransform root)
        {
            starBadge = UiKit.CreateImage(root, "FuseStar", GoldColor);
            UiKit.Place(starBadge.transform, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(46f, 46f));
            starBadge.GetComponent<Image>().raycastTarget = false;

            Text star = UiKit.CreateText(starBadge.transform, "Mark", "★", 26, new Color(0.15f, 0.11f, 0.02f));
            UiKit.Stretch((RectTransform)star.transform);

            starBadge.SetActive(false);
        }

        private static void Anchor(Component target, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)target.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(8f, 0f);
            rect.offsetMax = new Vector2(-8f, 0f);
        }

        public void Show(in CardOffer offer)
        {
            Root.gameObject.SetActive(true);

            SkillData data = offer.data;

            frame.color = offer.golden ? GoldColor : FrameColor;

            art.sprite = data != null ? data.icon : null;
            art.enabled = art.sprite != null;

            nameLabel.text = data != null ? data.skillName : "(빈 카드)";
            subLabel.text = data != null ? $"{data.role} · {data.attackType}" : "";

            gradeLabel.text = offer.fuses
                ? $"<b>합성</b> — 들고 있던 2장이 황금 1장이 된다"
                : offer.golden
                    ? $"<b>황금</b> — 데미지 x{ExpRules.GoldenDamageMul:0.#}"
                    : "";
            gradeLabel.supportRichText = true;

            starBadge.SetActive(offer.fuses);
        }

        public void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }

        public void SetInteractable(bool value)
        {
            if (button != null) button.interactable = value;
        }
    }
}
