using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.Tests
{
    /// <summary>
    /// 7단계 — 규격 스프라이트(<see cref="UiTheme.Art"/>)가 들어와 있고, 부품이 그것을 9-slice로 쓰는가.
    /// 스프라이트가 빠지면 화면은 단색 사각형으로 조용히 물러나므로, 여기서 시끄럽게 잡는다.
    /// </summary>
    public class UiArtTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp() => root = new GameObject("Root", typeof(RectTransform));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void AllSpritesAreBundled()
        {
            Assert.That(UiTheme.Art.Plate, Is.Not.Null, "Resources/Ui/Plate");
            Assert.That(UiTheme.Art.Well, Is.Not.Null, "Resources/Ui/Well");
            Assert.That(UiTheme.Art.Keycap, Is.Not.Null, "Resources/Ui/Keycap");
            Assert.That(UiTheme.Art.FocusGlow, Is.Not.Null, "Resources/Ui/FocusGlow");
            Assert.That(UiTheme.Art.Badge, Is.Not.Null, "Resources/Ui/Badge");
            Assert.That(UiTheme.Art.IconHp, Is.Not.Null, "Resources/Ui/Icon_Hp");
            Assert.That(UiTheme.Art.IconGold, Is.Not.Null, "Resources/Ui/Icon_Gold");
            Assert.That(UiTheme.Art.IconDeck, Is.Not.Null, "Resources/Ui/Icon_Deck");
            Assert.That(UiTheme.Art.IconFloor, Is.Not.Null, "Resources/Ui/Icon_Floor");
        }

        [Test]
        public void PanelSprites_HaveNineSliceBorders()
        {
            foreach (Sprite s in new[] { UiTheme.Art.Plate, UiTheme.Art.Well, UiTheme.Art.Keycap, UiTheme.Art.FocusGlow })
            {
                Assert.That(s, Is.Not.Null);
                Assert.That(s.border.x, Is.GreaterThan(0f), $"{s.name} 에 9-slice 테두리가 없다 — 늘리면 모서리가 뭉개진다.");
                Assert.That(s.border.w, Is.GreaterThan(0f), s.name);
            }
        }

        [Test]
        public void GlowSprite_PaddingMatchesSpread()
        {
            // 발광은 판보다 GlowSpread 만큼 크게 깐다 — 스프라이트의 투명 여백이 그보다 작으면 고리가 판 안으로 먹고 들어온다.
            Sprite s = UiTheme.Art.FocusGlow;
            Assert.That(s.border.x, Is.GreaterThanOrEqualTo(UiTheme.Art.GlowSpread));
        }

        [Test]
        public void FramedPlate_UsesSlicedPlate()
        {
            Image img = UiKit.FramedPlate(root.transform, "P");
            Assert.That(img.sprite, Is.SameAs(UiTheme.Art.Plate));
            Assert.That(img.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(img.color, Is.EqualTo(UiTheme.Ink), "스프라이트가 흰색이라 색은 여전히 토큰이 정해야 한다.");
        }

        [Test]
        public void Well_UsesSlicedWell()
        {
            Image img = UiKit.Well(root.transform, "W");
            Assert.That(img.sprite, Is.SameAs(UiTheme.Art.Well));
            Assert.That(img.type, Is.EqualTo(Image.Type.Sliced));
        }

        [Test]
        public void Button_UsesRoundedPlate_KeepsKindColor()
        {
            Button b = UiKit.NewButton(root.transform, "B", "확인", ButtonKind.Danger);
            Image img = b.GetComponent<Image>();
            Assert.That(img.sprite, Is.SameAs(UiTheme.Art.Plate));
            Assert.That(img.color, Is.EqualTo(UiKit.ButtonFill(ButtonKind.Danger)));
        }

        [Test]
        public void KeyHint_UsesKeycap()
        {
            RectTransform hint = UiKit.KeyHint(root.transform, "H", "E", "총공격");
            Assert.That(hint.Find("Keycap").GetComponent<Image>().sprite, Is.SameAs(UiTheme.Art.Keycap));
        }

        [Test]
        public void RunBar_UsesIcons()
        {
            RunBarUI bar = RunBarUI.Create(root.transform, RunBarUI.Mode.Overlay);

            Assert.That(bar.Gold.transform.parent.Find("Icon").GetComponent<Image>().sprite, Is.SameAs(UiTheme.Art.IconGold));
            Assert.That(bar.Hp.transform.parent.Find("Icon").GetComponent<Image>().sprite, Is.SameAs(UiTheme.Art.IconHp));
            Assert.That(bar.SkillListLabel.transform.parent.Find("Icon").GetComponent<Image>().sprite, Is.SameAs(UiTheme.Art.IconDeck));

            // 아랫변은 찢어진 가장자리 없이 놋쇠 가는 줄 하나다.
            Image edge = bar.transform.Find("Plate/Edge").GetComponent<Image>();
            Assert.That(edge.sprite, Is.Null, "런 바 아랫변에 장식 스프라이트가 붙었다 — 깔끔한 줄이어야 한다.");
            Assert.That(edge.color, Is.EqualTo(UiTheme.Rim));
            Assert.That(edge.rectTransform.sizeDelta.y, Is.EqualTo(UiTheme.RimWidth));
        }
    }
}
