using System;
using UnityEngine;
using UnityEngine.UI;
using Prototype.YG;

namespace Prototype
{
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
            frame = SimpleUI.CreateImage(parent, $"Offer_{slot}", FrameColor).GetComponent<Image>();
            frame.raycastTarget = false;

            Root = (RectTransform)frame.transform;
            Root.sizeDelta = new Vector2(Width + FrameInset * 2f, Height + FrameInset * 2f);

            GameObject bodyGo = SimpleUI.CreateImage(Root, "Body", CardColor);
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
            GameObject go = SimpleUI.CreateImage(body, "Art", Color.white);
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
            nameLabel = SimpleUI.CreateText(body, "Name", "", 24, NameColor);
            Anchor(nameLabel, new Vector2(0f, 0.26f), new Vector2(1f, 0.38f));

            subLabel = SimpleUI.CreateText(body, "Sub", "", 16, SubColor);
            Anchor(subLabel, new Vector2(0f, 0.16f), new Vector2(1f, 0.26f));

            gradeLabel = SimpleUI.CreateText(body, "Grade", "", 17, GoldTextColor);
            Anchor(gradeLabel, new Vector2(0f, 0.04f), new Vector2(1f, 0.15f));
        }

        /// <summary>
        /// 합성 표시. 우측 상단 금색 배지 안에 ★를 얹는다.
        /// 배지를 깔아 두는 이유는 폰트에 ★ 글리프가 없어도 <b>무언가 붙어 있다</b>는 것은
        /// 읽히게 하기 위해서다(<c>StageResultUI</c>가 화살표를 사각형으로 지은 것과 같은 이유).
        /// </summary>
        private void BuildStar(RectTransform root)
        {
            starBadge = SimpleUI.CreateImage(root, "FuseStar", GoldColor);
            SimpleUI.Place(starBadge.transform, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(46f, 46f));
            starBadge.GetComponent<Image>().raycastTarget = false;

            Text star = SimpleUI.CreateText(starBadge.transform, "Mark", "★", 26, new Color(0.15f, 0.11f, 0.02f));
            SimpleUI.Stretch((RectTransform)star.transform);

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
