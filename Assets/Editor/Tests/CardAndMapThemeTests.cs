using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.Tests
{
    /// <summary>
    /// 6단계 — 카드 한 장의 규격 그림(<see cref="CardOfferView"/>)과 지도 칸 상태색. 기획서 4항 CardView · 5.4.
    /// </summary>
    public class CardAndMapThemeTests
    {
        private GameObject root;
        private SkillData skill;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Root", typeof(RectTransform));
            skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "회전베기";
            skill.description = "주변 적에게 {kw:광역} 피해 {n:12|10}";
            skill.role = Role.Warrior;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(skill);
        }

        private CardOfferView ShowCard()
        {
            var view = new CardOfferView();
            view.Build(root.transform, 0);
            var offer = new CardOffer { data = skill };
            view.Show(in offer);
            return view;
        }

        [Test]
        public void Card_NameIsGoldHeading()
        {
            CardOfferView view = ShowCard();

            Assert.That(view.NameLabel.text, Is.EqualTo("회전베기"));
            Assert.That(view.NameLabel.color, Is.EqualTo(UiTheme.Gold));
            Assert.That(view.NameLabel.fontSize, Is.EqualTo(UiTheme.Size.Heading));
        }

        [Test]
        public void Card_DescriptionUsesKeywordMarkup()
        {
            CardOfferView view = ShowCard();

            Assert.That(view.DescLabel.text, Does.Contain(UiText.Kw("광역")));
            Assert.That(view.DescLabel.text, Does.Contain(UiText.Num(12, 10)), "기본보다 큰 수치가 초록으로 안 바뀌었다.");
            Assert.That(view.DescLabel.text, Does.Not.Contain("{"), "표식이 그대로 새어 나왔다.");
        }

        [Test]
        public void Card_RoleBandTakesRoleColor()
        {
            CardOfferView view = ShowCard();
            Assert.That(view.RoleBand.color, Is.EqualTo(SkillCutinUI.RoleColor(Role.Warrior)));
        }

        [Test]
        public void Card_BodyHasRim()
        {
            CardOfferView view = ShowCard();
            Transform body = view.Root.Find("Body");
            Assert.That(body.GetComponent<Outline>(), Is.Not.Null, "카드 테두리가 없다.");
        }

        [Test]
        public void MapRim_SelectableIsFocus_VisitedIsGold()
        {
            Color selectable = RunMapScreen.RimColor(MapNodeState.Selectable);
            Color visited = RunMapScreen.RimColor(MapNodeState.Visited);
            Color closed = RunMapScreen.RimColor(MapNodeState.Closed);

            Assert.That(selectable.WithAlpha(1f), Is.EqualTo(UiTheme.Focus), "갈 수 있는 칸은 청록이다.");
            Assert.That(visited.WithAlpha(1f), Is.EqualTo(UiTheme.Gold), "지나온 칸은 금색이다.");
            Assert.That(closed.a, Is.LessThan(selectable.a), "못 가는 칸의 테두리가 갈 수 있는 칸만큼 진하다.");
        }
    }
}
