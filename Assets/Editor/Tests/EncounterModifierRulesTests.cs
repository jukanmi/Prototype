using NUnit.Framework;

namespace Prototype.Tests
{
    /// <summary>
    /// 지도 칸이 조우에 거는 보정. 애셋은 안 바뀌고 "이 한 기가 강화인가"와 "방이 몇 %인가"만 정한다.
    /// 방 크기 통로: docs/Room_Size_Plan.md (6단계)
    /// </summary>
    public class EncounterModifierRulesTests
    {
        private static MapNode Node(MapNodeKind kind, int roomPercent = 100)
            => new MapNode(0, 0, 0, kind, "Stage_X", null, roomPercent);

        private static readonly EncounterModifier Elite = EncounterModifierRules.For(Node(MapNodeKind.Elite));

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
        public void OnlyEliteKind_HasEliteModifier()
        {
            Assert.That(Elite.HasElite, Is.True);
            Assert.That(Elite.EliteEvery, Is.EqualTo(EncounterModifierRules.EliteNodeEvery));

            foreach (MapNodeKind kind in new[] { MapNodeKind.Battle, MapNodeKind.Rest, MapNodeKind.Shop,
                                                 MapNodeKind.Event, MapNodeKind.Boss })
            {
                EncounterModifier m = EncounterModifierRules.For(Node(kind));
                Assert.That(m.HasElite, Is.False, kind.ToString());
                Assert.That(m.IsNone, Is.True, $"{kind}: 100% 방이면 보정이 없다");
            }
        }

        [Test]
        public void NegativeOrdinal_AndNegativeValues_AreSafe()
        {
            Assert.That(EncounterModifierRules.IsElite(false, EnemyRole.Melee, -1, Elite), Is.False);
            Assert.That(new EncounterModifier(-3, -5).IsNone, Is.True);
            Assert.That(new EncounterModifier(-3, -5).RoomPercent, Is.EqualTo(100));
        }

        // ── 방 크기 ─────────────────────────────────────

        /// <summary><c>default</c>가 곧 <see cref="EncounterModifier.None"/>이다. 방 크기 0이 100으로 읽혀야 한다.</summary>
        [Test]
        public void Default_RoomIsFull()
        {
            Assert.That(default(EncounterModifier).RoomPercent, Is.EqualTo(100));
            Assert.That(EncounterModifier.None.HasRoom, Is.False);
            Assert.That(EncounterModifier.None.IsNone, Is.True);
        }

        [Test]
        public void NoNode_IsNone()
        {
            Assert.That(EncounterModifierRules.For(null).IsNone, Is.True);
        }

        /// <summary>칸이 굴린 방 크기가 그대로 건너온다. 전투 칸은 방만, 정예 칸은 둘 다.</summary>
        [Test]
        public void For_CarriesTheNodesRoomPercent()
        {
            EncounterModifier battle = EncounterModifierRules.For(Node(MapNodeKind.Battle, 90));
            Assert.That(battle.RoomPercent, Is.EqualTo(90));
            Assert.That(battle.HasRoom, Is.True);
            Assert.That(battle.HasElite, Is.False);
            Assert.That(battle.IsNone, Is.False);

            EncounterModifier elite = EncounterModifierRules.For(Node(MapNodeKind.Elite, 110));
            Assert.That(elite.RoomPercent, Is.EqualTo(110));
            Assert.That(elite.EliteEvery, Is.EqualTo(EncounterModifierRules.EliteNodeEvery));
        }

        /// <summary>
        /// <b>방 크기만 있는 보정에서 강화 판정이 터지지 않는다.</b> 그 보정은 <c>IsNone</c>이 거짓인데
        /// 강화 간격이 0이라, 강화 판정이 <c>IsNone</c>으로 거르면 나머지 연산이 0으로 나눈다.
        /// </summary>
        [Test]
        public void RoomOnlyModifier_NeverPromotes_AndDoesNotDivideByZero()
        {
            EncounterModifier roomOnly = EncounterModifierRules.For(Node(MapNodeKind.Battle, 85));

            for (int i = 0; i < 6; i++)
            {
                Assert.That(EncounterModifierRules.IsElite(false, EnemyRole.Melee, i, roomOnly), Is.False);
                Assert.That(EncounterModifierRules.IsElite(true, EnemyRole.Melee, i, roomOnly), Is.True);
            }
        }

        /// <summary>손으로 만든 지도에서 보스 · 비전투 칸에 방 크기가 박혀 있어도 보정으로 안 건너간다.</summary>
        [TestCase(MapNodeKind.Boss)]
        [TestCase(MapNodeKind.Rest)]
        [TestCase(MapNodeKind.Shop)]
        [TestCase(MapNodeKind.Event)]
        public void NonBattleNodes_NeverCarryRoomPercent(MapNodeKind kind)
        {
            Assert.That(EncounterModifierRules.For(Node(kind, 90)).RoomPercent, Is.EqualTo(100));
        }

        /// <summary>전투 시작 로그(<c>BattleSceneController</c> · <c>StageDirector</c>)가 이 글을 그대로 찍는다.</summary>
        [Test]
        public void ToString_NamesWhatIsModified()
        {
            Assert.That(EncounterModifier.None.ToString(), Is.EqualTo("보정 없음"));
            Assert.That(EncounterModifierRules.For(Node(MapNodeKind.Elite)).ToString(), Is.EqualTo("3기마다 강화"));
            Assert.That(EncounterModifierRules.For(Node(MapNodeKind.Battle, 90)).ToString(), Is.EqualTo("방 90%"));
            Assert.That(EncounterModifierRules.For(Node(MapNodeKind.Elite, 110)).ToString(), Is.EqualTo("3기마다 강화 · 방 110%"));
        }
    }
}
