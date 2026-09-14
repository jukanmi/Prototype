using NUnit.Framework;

namespace Prototype.Tests
{
    /// <summary>지도 칸이 조우에 거는 보정. 애셋은 안 바뀌고 "이 한 기가 강화인가"만 정한다.</summary>
    public class EncounterModifierRulesTests
    {
        private static readonly EncounterModifier Elite = EncounterModifierRules.For(MapNodeKind.Elite);

        [Test]
        public void None_KeepsAuthoredValues()
        {
            for (int i = 0; i < 6; i++)
            {
                Assert.That(EncounterModifierRules.IsElite(false, EnemyRole.Melee, i, EncounterModifier.None), Is.False);
                Assert.That(EncounterModifierRules.IsElite(true, EnemyRole.Melee, i, EncounterModifier.None), Is.True);
            }
        }

        [Test]
        public void EliteNode_EveryThird_FromZero()
        {
            bool[] got = new bool[7];
            for (int i = 0; i < got.Length; i++)
                got[i] = EncounterModifierRules.IsElite(false, EnemyRole.Ranged, i, Elite);

            Assert.That(got, Is.EqualTo(new[] { true, false, false, true, false, false, true }));
        }

        /// <summary>보스는 보정으로 절대 강화되지 않는다 — 안 막으면 체력 2.5배 보스가 나온다.</summary>
        [Test]
        public void Boss_IsNeverPromotedByModifier()
        {
            for (int i = 0; i < 6; i++)
                Assert.That(EncounterModifierRules.IsElite(false, EnemyRole.Boss, i, Elite), Is.False);
        }

        [Test]
        public void AuthoredElite_StaysEvenForBoss()
        {
            Assert.That(EncounterModifierRules.IsElite(true, EnemyRole.Boss, 1, Elite), Is.True);
        }

        [Test]
        public void OnlyEliteKind_HasModifier()
        {
            Assert.That(Elite.IsNone, Is.False);

            foreach (MapNodeKind kind in new[] { MapNodeKind.Battle, MapNodeKind.Rest, MapNodeKind.Shop,
                                                 MapNodeKind.Event, MapNodeKind.Boss })
                Assert.That(EncounterModifierRules.For(kind).IsNone, Is.True, kind.ToString());
        }

        [Test]
        public void NegativeOrdinal_AndNegativeInterval_AreSafe()
        {
            Assert.That(EncounterModifierRules.IsElite(false, EnemyRole.Melee, -1, Elite), Is.False);
            Assert.That(new EncounterModifier(-3).IsNone, Is.True);
        }
    }
}
