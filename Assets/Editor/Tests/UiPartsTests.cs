using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.Tests
{
    /// <summary>
    /// 규격 부품(<see cref="UiKit"/>)이 규격대로 지어지는가 — 글꼴 · 외곽선 · 6단계 크기, 창 골격, 버튼 · 선택지 · 배지.
    /// 호버 · 열림 애니메이션은 재생해야 돌아서 여기서 안 본다(기획서 8항 목록).
    /// </summary>
    public class UiPartsTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp() => root = new GameObject("Root", typeof(RectTransform));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        // ── 글자 ─────────────────────────────────────────

        [Test]
        public void NewText_UsesThemeFont_SnapsSize_AndHasOutline()
        {
            Text t = UiKit.NewText(root.transform, "T", 17, UiTheme.TextMain, FontStyle.Normal);

            Assert.That(t.font, Is.SameAs(UiTheme.Font));
            Assert.That(t.fontSize, Is.EqualTo(UiTheme.Size.Caption), "글자 크기가 6단계로 끌려오지 않았다.");
            Assert.That(t.GetComponent<Outline>(), Is.Not.Null, "외곽선 없는 글자는 배경 일러스트 위에서 묻힌다.");
            Assert.That(t.supportRichText, Is.True, "키워드 강조(UiText)가 안 먹는다.");
        }

        [Test]
        public void TitleText_GetsDropShadowOnTop()
        {
            Text t = UiKit.NewText(root.transform, "T", UiTheme.Size.Title, UiTheme.Gold, FontStyle.Bold);

            // Outline 은 Shadow 의 파생 — 외곽선 하나 + 그림자 하나.
            Assert.That(t.GetComponents<Shadow>(), Has.Length.EqualTo(2));
        }

        [Test]
        public void AddOutline_IsIdempotent()
        {
            Text t = UiKit.NewText(root.transform, "T", UiTheme.Size.Body, UiTheme.TextMain, FontStyle.Normal);
            UiKit.AddOutline(t);
            UiKit.AddOutline(t);
            Assert.That(t.GetComponents<Outline>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void NoOutline_RemovesAllEffects()
        {
            Text t = UiKit.NewText(root.transform, "T", UiTheme.Size.Title, UiTheme.TextMain, FontStyle.Normal);
            UiKit.NoOutline(t);
            Assert.That(t.GetComponents<Shadow>(), Is.Empty);
        }

        [Test]
        public void Dress_SwapsFontOnExistingTexts()
        {
            var go = new GameObject("Old", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(root.transform, false);
            Text old = go.GetComponent<Text>();
            old.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            UiKit.Dress(root.transform);

            Assert.That(old.font, Is.SameAs(UiTheme.Font));
            Assert.That(old.GetComponent<Outline>(), Is.Not.Null);
        }

        // ── 버튼 ─────────────────────────────────────────

        [Test]
        public void CreateButton_HasFocusRim_AndThemeLabel()
        {
            Button b = UiKit.CreateButton(root.transform, "B", "확인", UiTheme.ButtonNormal, new Vector2(200, 56), 20);

            Assert.That(b.GetComponent<UiFocusRim>(), Is.Not.Null, "호버 · 포커스 표시가 전 화면 공통이어야 한다.");
            Text label = b.GetComponentInChildren<Text>();
            Assert.That(label.color, Is.EqualTo(UiTheme.TextMain));
            Assert.That(b.colors.highlightedColor, Is.EqualTo(Color.white),
                        "호버는 테두리로 표시한다 — 틴트까지 겹치면 두 겹이 된다.");
        }

        [TestCase(ButtonKind.Primary)]
        [TestCase(ButtonKind.Normal)]
        [TestCase(ButtonKind.Danger)]
        [TestCase(ButtonKind.Ghost)]
        public void NewButton_FillFollowsKind(ButtonKind kind)
        {
            Button b = UiKit.NewButton(root.transform, "B", "확인", kind);

            Assert.That(b.GetComponent<Image>().color, Is.EqualTo(UiKit.ButtonFill(kind)));
            Assert.That(((RectTransform)b.transform).sizeDelta.y, Is.EqualTo(UiTheme.ButtonHeight.Medium));
            Assert.That(b.GetComponentInChildren<Text>().fontSize, Is.EqualTo(UiTheme.Size.Body));
        }

        // ── 창 ───────────────────────────────────────────

        [Test]
        public void Modal_HasTheOneSkeleton()
        {
            UiKit.ModalParts m = UiKit.Modal(root.transform, "일시정지", new Vector2(640, 480));

            Assert.That(m.Backdrop.raycastTarget, Is.True, "바탕막이 클릭을 막아야 뒤의 버튼이 안 눌린다.");
            Assert.That(m.Panel.transform.parent, Is.SameAs(m.Backdrop.transform));
            Assert.That(m.Panel.GetComponent<Outline>(), Is.Not.Null, "창 판에 테두리가 없다.");
            Assert.That(m.Title.text, Is.EqualTo("일시정지"));
            Assert.That(m.Title.color, Is.EqualTo(UiTheme.Gold));
            Assert.That(m.Title.fontSize, Is.EqualTo(UiTheme.Size.Title));
            Assert.That(m.Body.parent, Is.SameAs(m.Panel.transform));
            Assert.That(m.Buttons.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
        }

        // ── 선택지 · 배지 · 키 안내 ──────────────────────

        [Test]
        public void OptionRow_TitleGold_BodyFormatted_NoCenterLabel()
        {
            UiKit.OptionRowParts row = UiKit.OptionRow(root.transform, "Opt", null,
                                                       "휴식", "체력을 {kw:30%} 회복한다", new Vector2(800, 96));

            Assert.That(row.Button.transform.Find("Label"), Is.Null, "가운데 라벨이 제목 · 설명과 겹친다.");
            Assert.That(row.Title.color, Is.EqualTo(UiTheme.Gold));
            Assert.That(row.Title.text, Is.EqualTo("휴식"));
            Assert.That(row.Body.text, Does.Contain("<color="), "설명의 키워드 표식이 강조로 안 바뀌었다.");
            Assert.That(row.Icon, Is.Null);
        }

        [Test]
        public void SetUnavailable_GreysOut_AndExplains()
        {
            UiKit.OptionRowParts row = UiKit.OptionRow(root.transform, "Opt", null, "강화", "카드를 강화한다", new Vector2(800, 96));
            UiKit.SetUnavailable(row, "강화할 카드가 없다.");

            Assert.That(row.Button.interactable, Is.False);
            Assert.That(row.Title.color, Is.EqualTo(UiTheme.TextOff));
            UiTooltip tip = row.Button.GetComponent<UiTooltip>();
            Assert.That(tip, Is.Not.Null);
            Assert.That(tip.Body, Is.EqualTo("강화할 카드가 없다."));
        }

        [Test]
        public void Badge_HidesAtZero()
        {
            var icon = UiKit.NewImage(root.transform, "Deck", Color.white);
            Text badge = UiKit.Badge(icon.transform, 20);

            Assert.That(badge.text, Is.EqualTo("20"));
            Assert.That(badge.transform.parent.gameObject.activeSelf, Is.True);

            UiKit.SetBadge(badge, 0);
            Assert.That(badge.transform.parent.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void KeyHint_KeycapTextHasNoOutline()
        {
            RectTransform hint = UiKit.KeyHint(root.transform, "Hint", "ESC", "설정");

            Text key = hint.Find("Keycap/Key").GetComponent<Text>();
            Assert.That(key.text, Is.EqualTo("ESC"));
            Assert.That(key.color, Is.EqualTo(UiTheme.TextOnLight));
            Assert.That(key.GetComponents<Shadow>(), Is.Empty, "밝은 키캡 위 어두운 글자에 검은 외곽선은 번진다.");
            Assert.That(hint.Find("Action").GetComponent<Text>().text, Is.EqualTo("설정"));
        }
    }
}
