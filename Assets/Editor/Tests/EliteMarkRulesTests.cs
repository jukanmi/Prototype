using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 강화 개체 표시의 판단 — 언제 고리를 그리는지, 맥동 범위, HUD 이름.
    /// 고리가 실제로 발밑에 제대로 눕는지는 정예 칸을 재생해서 눈으로 본다.
    /// </summary>
    public class EliteMarkRulesTests
    {
        [Test]
        public void ShouldDraw_OnlyLiveVisibleElites()
        {
            Assert.That(EliteMarkRules.ShouldDraw(elite: true, dead: false, active: true, hiddenBehindWall: false), Is.True);

            Assert.That(EliteMarkRules.ShouldDraw(false, false, true, false), Is.False, "일반 적");
            Assert.That(EliteMarkRules.ShouldDraw(true, true, true, false), Is.False, "시체");
            Assert.That(EliteMarkRules.ShouldDraw(true, false, false, false), Is.False, "꺼진 몸");
        }

        /// <summary>벽 뒤에 숨겨 둔 채 걸어 나오는 동안은 안 그린다 — 경계 밖 허공에 고리만 뜬다.</summary>
        [Test]
        public void ShouldDraw_HiddenBehindWall_IsFalse()
        {
            Assert.That(EliteMarkRules.ShouldDraw(true, false, true, hiddenBehindWall: true), Is.False);
        }

        [Test]
        public void Pulse_StaysInRange()
        {
            for (float t = 0f; t < 3f; t += 0.037f)
            {
                Assert.That(EliteMarkRules.PulseAlpha(t),
                            Is.InRange(EliteMarkRules.MinAlpha - 1e-4f, EliteMarkRules.MaxAlpha + 1e-4f));
                Assert.That(EliteMarkRules.PulseScale(t),
                            Is.InRange(1f - 1e-4f, 1f + EliteMarkRules.PulseGrow + 1e-4f));
            }
        }

        /// <summary>반투명 바닥에서도 보여야 하고, 완전히 사라지는 순간이 있으면 깜빡임으로 읽힌다.</summary>
        [Test]
        public void Pulse_NeverFadesOut()
        {
            Assert.That(EliteMarkRules.MinAlpha, Is.GreaterThanOrEqualTo(0.5f));
        }

        [Test]
        public void ObjectName_AndHudName_RoundTrip()
        {
            string elite = EliteMarkRules.ObjectName("Warrior", true);
            string normal = EliteMarkRules.ObjectName("Warrior", false);

            Assert.That(elite, Is.EqualTo("Warrior" + EliteMarkRules.NameSuffix));
            Assert.That(normal, Is.EqualTo("Warrior"));

            Assert.That(EliteMarkRules.HudName(elite, true), Is.EqualTo(EliteMarkRules.HudPrefix + "Warrior"));
            Assert.That(EliteMarkRules.HudName(normal, false), Is.EqualTo("Warrior"));
        }

        /// <summary>표식이 붙은 이름이라도 강화가 아니면(이름만 남은 경우) 접미사를 떼고 접두사는 안 붙인다.</summary>
        [Test]
        public void HudName_StripsSuffix_EvenWhenNotElite()
        {
            Assert.That(EliteMarkRules.HudName("Mage" + EliteMarkRules.NameSuffix, false), Is.EqualTo("Mage"));
            Assert.That(EliteMarkRules.HudName(null, true), Is.EqualTo(EliteMarkRules.HudPrefix));
        }

        /// <summary>바닥에 이미 깔리는 색과 겹치지 않는가 — 사거리 원(하늘/빨강) · 소환 예고(빨강).</summary>
        [Test]
        public void AuraColor_IsDistinctFromFloorReds()
        {
            Color.RGBToHSV(EliteMarkRules.AuraColor, out float h, out float s, out float v);

            Assert.That(h, Is.InRange(0.72f, 0.85f), "보라 대역");
            Assert.That(s, Is.GreaterThan(0.5f));
        }
    }
}
