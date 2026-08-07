using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 유니티 객체를 만지지 않는 순수 표라서 GameObject 없이 그대로 검증된다.
    /// </summary>
    public class CombatStateVisualsTests
    {
        /// <summary>머리 위에 뜨는 6개 상태. Neutral · Dead는 빠진다.</summary>
        private static readonly CombatState[] Shown =
        {
            CombatState.LightHit,
            CombatState.AerialHit,
            CombatState.Knockback,
            CombatState.WallBound,
            CombatState.Down,
            CombatState.Getup,
        };

        [Test]
        public void NeutralAndDead_AreNotShown()
        {
            Assert.That(CombatStateVisuals.ShouldShow(CombatState.Neutral), Is.False);
            Assert.That(CombatStateVisuals.ShouldShow(CombatState.Dead), Is.False);
        }

        [Test]
        public void EveryOtherState_IsShown()
        {
            foreach (CombatState s in Shown)
                Assert.That(CombatStateVisuals.ShouldShow(s), Is.True, s.ToString());
        }

        [Test]
        public void ShownStates_HaveNonEmptyLabel()
        {
            foreach (CombatState s in Shown)
                Assert.That(CombatStateVisuals.Label(s), Is.Not.Empty, s.ToString());
        }

        [Test]
        public void HiddenStates_HaveEmptyLabel()
        {
            Assert.That(CombatStateVisuals.Label(CombatState.Neutral), Is.Empty);
            Assert.That(CombatStateVisuals.Label(CombatState.Dead), Is.Empty);
        }

        [Test]
        public void ShownStates_HaveDistinctLabels()
        {
            // 두 상태가 같은 글자를 쓰면 라벨이 상태를 구분해 주지 못한다.
            for (int i = 0; i < Shown.Length; i++)
                for (int j = i + 1; j < Shown.Length; j++)
                    Assert.That(CombatStateVisuals.Label(Shown[i]),
                                Is.Not.EqualTo(CombatStateVisuals.Label(Shown[j])),
                                $"{Shown[i]} 와 {Shown[j]} 가 같은 라벨을 쓴다");
        }

        [Test]
        public void Tint_LeavesHiddenStatesAlone()
        {
            var baseColor = new Color(0.9f, 0.3f, 0.28f, 1f);

            Assert.That(CombatStateVisuals.Tint(baseColor, CombatState.Neutral), Is.EqualTo(baseColor));
            Assert.That(CombatStateVisuals.Tint(baseColor, CombatState.Dead), Is.EqualTo(baseColor));
        }

        [Test]
        public void Tint_KeepsBaseAlpha()
        {
            // 사망 페이드가 알파를 쓴다. 상태색 알파가 새어 들어오면 죽는 연출이 끊긴다.
            var baseColor = new Color(0.9f, 0.3f, 0.28f, 0.4f);

            foreach (CombatState s in Shown)
                Assert.That(CombatStateVisuals.Tint(baseColor, s).a,
                            Is.EqualTo(0.4f).Within(0.0001f), s.ToString());
        }

        [Test]
        public void Tint_LandsBetweenBaseAndStateColor()
        {
            var baseColor = new Color(0.9f, 0.3f, 0.28f, 1f);
            Color state = CombatStateVisuals.StateColor(CombatState.AerialHit);
            Color mixed = CombatStateVisuals.Tint(baseColor, CombatState.AerialHit);

            Assert.That(mixed.r, Is.EqualTo(Mathf.Lerp(baseColor.r, state.r, CombatStateVisuals.TintStrength)).Within(0.0001f));
            Assert.That(mixed.g, Is.EqualTo(Mathf.Lerp(baseColor.g, state.g, CombatStateVisuals.TintStrength)).Within(0.0001f));
            Assert.That(mixed.b, Is.EqualTo(Mathf.Lerp(baseColor.b, state.b, CombatStateVisuals.TintStrength)).Within(0.0001f));
        }

        [Test]
        public void Tint_DoesNotFullyCoverBaseColor()
        {
            // 완전히 덮으면 어떤 종류의 적이었는지 알 수 없게 된다.
            var melee = new Color(0.90f, 0.30f, 0.28f, 1f);
            var ranged = new Color(0.35f, 0.62f, 1f, 1f);

            Color a = CombatStateVisuals.Tint(melee, CombatState.Down);
            Color b = CombatStateVisuals.Tint(ranged, CombatState.Down);

            Assert.That(a, Is.Not.EqualTo(b), "고유색이 다르면 물든 색도 달라야 한다");
        }

        [Test]
        public void ShownStates_HaveDistinctColors()
        {
            for (int i = 0; i < Shown.Length; i++)
                for (int j = i + 1; j < Shown.Length; j++)
                    Assert.That(CombatStateVisuals.StateColor(Shown[i]),
                                Is.Not.EqualTo(CombatStateVisuals.StateColor(Shown[j])),
                                $"{Shown[i]} 와 {Shown[j]} 가 같은 색을 쓴다");
        }
    }
}
