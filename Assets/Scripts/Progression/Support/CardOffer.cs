// 카드 제안 — 후보 뽑기 규칙 · 지급 규칙 · 한 장을 그리는 뷰.
// 뽑고(CardOfferRules) 주고(CardGrantRules) 보여 주는(CardOfferView) 한 흐름이다.

using System.Collections.Generic;
using System;
using UnityEngine.UI;
using UnityEngine;

namespace Prototype
{
    // ══ CardOfferRules ═══════════════════════════════════════════

    /// <summary>카드 선택지 한 칸.</summary>
    public struct CardOffer
    {
        public SkillData data;

        public bool IsEmpty => data == null;
    }

    /// <summary>
    /// 카드 선택지를 짜는 규칙. 순수 함수다.
    /// <b>난수를 주입받는다.</b> 실사용은 <c>() =&gt; UnityEngine.Random.value</c>를 넘긴다.
    /// </summary>
    public static class CardOfferRules
    {
        /// <summary>한 번에 보여 주는 카드 수.</summary>
        public const int OfferCount = 3;

        /// <summary>선택지를 짠다. 모집단이 <see cref="OfferCount"/>보다 적으면 있는 만큼만 낸다.</summary>
        /// <param name="pool">뽑을 수 있는 스킬 전부. null·중복은 걸러진다.</param>
        /// <param name="roll">0 이상 1 미만을 돌려주는 난수원.</param>
        public static List<CardOffer> Build(IReadOnlyList<SkillData> pool, Func<float> roll)
        {
            var offers = new List<CardOffer>(OfferCount);
            if (pool == null || roll == null) return offers;

            List<SkillData> bag = Distinct(pool);
            int take = Math.Min(OfferCount, bag.Count);

            for (int i = 0; i < take; i++)
            {
                int index = Index(roll(), bag.Count);
                offers.Add(new CardOffer { data = bag[index] });
                bag.RemoveAt(index);
            }

            return offers;
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

    /// <summary>고른 선택지를 덱에 반영하는 규칙. 순수 함수라 리스트만 넘기면 된다.</summary>
    public static class CardGrantRules
    {
        /// <summary>
        /// 고른 카드를 덱에 넣는다. <b>실제로 들어간 카드</b>를 돌려준다(실패하면 null) —
        /// 부르는 쪽이 같은 인스턴스를 손패에도 꽂으므로, 새로 만들게 두면 런 덱과 손패에
        /// 서로 다른 몸이 하나씩 생긴다.
        /// </summary>
        public static ComboCard Apply(List<ComboCard> deck, in CardOffer pick)
        {
            if (deck == null || pick.IsEmpty) return null;

            var card = new ComboCard(pick.data, WeightOf(pick.data));
            deck.Add(card);

            return card;
        }

        /// <summary>
        /// 새 카드의 드로우 가중치. 시작 덱을 굽는 SkillTableBuilder가 시동기에 2를 주는 것과
        /// 같은 규칙이라, 보상으로 얻은 카드가 시작 덱과 다르게 굴지 않는다.
        /// </summary>
        public static float WeightOf(SkillData data)
            => data != null && data.IsStarterType ? 2f : 1f;
    }

    // ══ CardOfferView ═══════════════════════════════════════════

    /// <summary>
    /// 카드 선택지 <b>한 장</b>의 그림. <c>BulletTimeGaugeWidget</c>과 같은 꼴로,
    /// MonoBehaviour가 아니라 <see cref="Build"/>로 지어지고 참조만 들고 있는 위젯이다.
    ///
    /// 카드 한 장의 규격 그림(기획서 4항 <c>CardView</c>, 6단계):
    /// <code>
    /// ┌──────────────┐ ← 종류 띠(직업 색)
    /// │    아트      │
    /// ├──────────────┤
    /// │  이름(금)    │ Heading
    /// │ 직업 · 형태  │ Caption
    /// │  설명        │ Caption, {kw:…} 강조
    /// └──────────────┘ ← 놋쇠 테두리, 포커스 땐 청록 띠가 뒤에서 내민다
    /// </code>
    /// </summary>
    public class CardOfferView
    {
        public const float Width = 260f;
        public const float Height = 360f;

        /// <summary>테두리가 카드 밖으로 삐져나오는 두께.</summary>
        private const float FrameInset = 6f;

        /// <summary>아트가 카드 안에서 물러나는 여백.</summary>
        private const float ArtInset = 12f;

        /// <summary>종류 띠 두께.</summary>
        public const float RoleBandHeight = 8f;

        private static readonly Color FrameColor = UiTheme.Ink;
        private static readonly Color CardColor = UiTheme.Raised;
        private static readonly Color NameColor = UiTheme.Gold;
        private static readonly Color SubColor = UiTheme.TextSub;
        private static readonly Color DescColor = UiTheme.TextMain;

        private Image frame;
        private Image art;
        private Image roleBand;
        private Text nameLabel;
        private Text subLabel;
        private Text descLabel;
        private Button button;

        // 테스트가 읽는다.
        public Text NameLabel => nameLabel;
        public Text DescLabel => descLabel;
        public Image RoleBand => roleBand;

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
            UiKit.AddRim(bodyImage, UiTheme.Rim);

            button = bodyGo.AddComponent<Button>();
            button.targetGraphic = bodyImage;
            button.onClick.AddListener(() => OnPicked?.Invoke(index));

            BuildRoleBand(body);
            BuildArt(body);
            BuildLabels(body);
        }

        /// <summary>맨 위 종류 띠 — 직업 색. 레퍼런스 카드의 종류 리본 자리.</summary>
        private void BuildRoleBand(RectTransform body)
        {
            roleBand = UiKit.NewImage(body, "RoleBand", UiTheme.TextOff);
            var rect = roleBand.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, RoleBandHeight);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>아트는 카드 위쪽 절반을 채운다. 아이콘이 없는 스킬은 그냥 비워 둔다.</summary>
        private void BuildArt(RectTransform body)
        {
            GameObject go = UiKit.CreateImage(body, "Art", Color.white);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0.46f);
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
            nameLabel = UiKit.NewText(body, "Name", UiTheme.Size.Heading, NameColor, FontStyle.Bold);
            UiKit.Band(nameLabel, new Vector2(0f, 0.36f), new Vector2(1f, 0.46f), 8f);

            subLabel = UiKit.CreateText(body, "Sub", "", UiTheme.Size.Caption, SubColor);
            UiKit.Band(subLabel, new Vector2(0f, 0.29f), new Vector2(1f, 0.36f), 8f);

            descLabel = UiKit.CreateText(body, "Desc", "", UiTheme.Size.Caption, DescColor);
            descLabel.alignment = TextAnchor.UpperCenter;
            descLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            descLabel.verticalOverflow = VerticalWrapMode.Truncate;
            UiKit.Band(descLabel, new Vector2(0f, 0.03f), new Vector2(1f, 0.28f), 14f);
        }

        public void Show(in CardOffer offer)
        {
            Root.gameObject.SetActive(true);

            SkillData data = offer.data;

            art.sprite = data != null ? data.icon : null;
            art.enabled = art.sprite != null;

            nameLabel.text = data != null ? data.skillName : "(빈 카드)";
            subLabel.text = data != null ? $"{data.role} · {data.attackType}" : "";
            descLabel.text = data != null ? UiText.Format(data.description) : "";
            roleBand.color = data != null ? SkillCutinUI.RoleColor(data.role) : UiTheme.TextOff;
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
