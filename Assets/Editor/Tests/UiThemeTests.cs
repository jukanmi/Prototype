using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// UI 규격(<see cref="UiTheme"/>) 자체의 약속 — 읽히는 대비, 금 · 청록의 역할 분리, 6단계 글자, 강조 표식.
    /// 근거는 <c>docs/UI_Unification_Plan.md</c> 2 · 3항.
    /// </summary>
    public class UiThemeTests
    {
        // ── 대비 ─────────────────────────────────────────

        [Test]
        public void BodyText_IsReadableOnPlates()
        {
            foreach (Color plate in new[] { UiTheme.Ink, UiTheme.InkDeep, UiTheme.Raised })
            {
                Assert.That(UiTheme.Contrast(UiTheme.TextMain, plate), Is.GreaterThanOrEqualTo(4.5f),
                            "본문 글자가 판 위에서 안 읽힌다(WCAG AA 4.5).");
                Assert.That(UiTheme.Contrast(UiTheme.TextSub, plate), Is.GreaterThanOrEqualTo(4.5f),
                            "보조 글자가 판 위에서 안 읽힌다.");
            }
        }

        [Test]
        public void GoldAndFocus_AreReadableOnInk()
        {
            Assert.That(UiTheme.Contrast(UiTheme.Gold, UiTheme.Ink), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(UiTheme.Contrast(UiTheme.Focus, UiTheme.Ink), Is.GreaterThanOrEqualTo(4.5f));
        }

        [Test]
        public void KeycapText_IsReadableOnKeycap()
        {
            Assert.That(UiTheme.Contrast(UiTheme.TextOnLight, UiTheme.TextMain), Is.GreaterThanOrEqualTo(4.5f));
        }

        // ── 역할 분리 ────────────────────────────────────

        [Test]
        public void Gold_IsNotFocus()
        {
            // 제목과 "지금 고른 것"이 같은 색이면 포커스가 제목 사이에 묻힌다(기획서 0항).
            Color.RGBToHSV(UiTheme.Gold, out float hg, out _, out _);
            Color.RGBToHSV(UiTheme.Focus, out float hf, out _, out _);
            float hueGap = Mathf.Abs(hg - hf);
            hueGap = Mathf.Min(hueGap, 1f - hueGap);
            Assert.That(hueGap, Is.GreaterThan(0.25f), "금색과 포커스 색의 색상이 너무 가깝다.");
        }

        [Test]
        public void ButtonKinds_HaveDistinctFills()
        {
            var kinds = (ButtonKind[])System.Enum.GetValues(typeof(ButtonKind));
            for (int i = 0; i < kinds.Length; i++)
                for (int j = i + 1; j < kinds.Length; j++)
                    Assert.That(UiKit.ButtonFill(kinds[i]), Is.Not.EqualTo(UiKit.ButtonFill(kinds[j])),
                                $"{kinds[i]} 와 {kinds[j]} 버튼이 같은 바탕이다.");
        }

        // ── 글자 ─────────────────────────────────────────

        [Test]
        public void Sizes_AreSixDescendingSteps()
        {
            Assert.That(UiTheme.Size.All, Has.Length.EqualTo(6));
            for (int i = 1; i < UiTheme.Size.All.Length; i++)
                Assert.That(UiTheme.Size.All[i], Is.LessThan(UiTheme.Size.All[i - 1]));
        }

        [TestCase(13, UiTheme.Size.Micro)]
        [TestCase(14, UiTheme.Size.Micro)]
        [TestCase(17, UiTheme.Size.Caption)]
        [TestCase(18, UiTheme.Size.Body)]     // 16 · 20 사이 같은 거리 → 큰 쪽
        [TestCase(22, UiTheme.Size.Body)]
        [TestCase(24, UiTheme.Size.Heading)]
        [TestCase(30, UiTheme.Size.Heading)]
        [TestCase(34, UiTheme.Size.Title)]
        [TestCase(40, UiTheme.Size.Title)]
        [TestCase(48, UiTheme.Size.Display)]
        [TestCase(80, UiTheme.Size.Display)]
        [TestCase(8, UiTheme.Size.Micro)]
        public void SnapSize_PicksNearestStep(int input, int expected)
        {
            Assert.That(UiTheme.SnapSize(input), Is.EqualTo(expected));
        }

        [Test]
        public void SnapSize_KeepsStepsAsIs()
        {
            foreach (int s in UiTheme.Size.All)
                Assert.That(UiTheme.SnapSize(s), Is.EqualTo(s));
        }

        [Test]
        public void Font_IsBundledAndHasHangul()
        {
            Font f = Resources.Load<Font>(UiTheme.FontPath);
            Assert.That(f, Is.Not.Null, $"Resources/{UiTheme.FontPath} 글꼴이 없다 — 내장 글꼴로 물러나 한글이 OS 글꼴로 그려진다.");
            Assert.That(UiTheme.Font, Is.SameAs(f));
            Assert.That(f.HasCharacter('가'), Is.True, "글꼴에 한글 글리프가 없다.");
        }

        // ── 강조 표식 ────────────────────────────────────

        [Test]
        public void Format_Keyword_BecomesGold()
        {
            string hex = UiTheme.ToHex(UiTheme.Gold);
            Assert.That(UiText.Format("{kw:뽑을 카드 더미}에 섞는다"),
                        Is.EqualTo($"<color={hex}>뽑을 카드 더미</color>에 섞는다"));
        }

        [Test]
        public void Format_Number_ColorsAgainstBase()
        {
            Assert.That(UiText.Format("{n:9|5}"), Is.EqualTo($"<color={UiTheme.ToHex(UiTheme.Buff)}>9</color>"));
            Assert.That(UiText.Format("{n:3|5}"), Is.EqualTo($"<color={UiTheme.ToHex(UiTheme.Hp)}>3</color>"));
            Assert.That(UiText.Format("{n:5|5}"), Is.EqualTo("5"));
            Assert.That(UiText.Format("{n:7}"), Is.EqualTo("7"));
        }

        [Test]
        public void Format_LeavesUnknownBracesAlone()
        {
            Assert.That(UiText.Format("{0}장 · {x:y} · {n:abc}"), Is.EqualTo("{0}장 · {x:y} · {n:abc}"));
            Assert.That(UiText.Format("닫히지 않은 {kw:괄호"), Is.EqualTo("닫히지 않은 {kw:괄호"));
            Assert.That(UiText.Format(""), Is.EqualTo(""));
            Assert.That(UiText.Format(null), Is.Null);
        }
    }
}
