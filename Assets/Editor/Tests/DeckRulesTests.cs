using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 덱 목표 장수. 상수 16을 파티 인원에서 유도한 값으로 바꾼 규칙이라,
    /// 3인 파티와 <b>동료 영구 사망</b>이 둘 다 여기에 걸린다.
    /// </summary>
    public class DeckRulesTests
    {
        private readonly List<Object> spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in spawned) Object.DestroyImmediate(o);
            spawned.Clear();
        }

        private PartyMemberData Member()
        {
            var m = ScriptableObject.CreateInstance<PartyMemberData>();
            spawned.Add(m);
            return m;
        }

        // ── 목표 장수 ───────────────────────────────────

        [TestCase(0f, true)]
        [TestCase(0.4999f, true)]
        [TestCase(0.5f, false)]
        [TestCase(1f, false)]
        public void BasicAttackDraw_HasFiftyPercentThreshold(float roll, bool expected)
        {
            Assert.That(BasicAttackDrawRules.ShouldDraw(roll), Is.EqualTo(expected));
        }

        private SkillData Skill()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            spawned.Add(skill);
            return skill;
        }

        [Test]
        public void BasicAttackDraw_AllNormalCardsEqual_GoldenHasHalfWeight()
        {
            var cards = new[]
            {
                new ComboCard(Skill(), 100f), new ComboCard(Skill(), 0.01f),
                new ComboCard(Skill(), 100f, true), new ComboCard(Skill(), 0.01f, true),
            };
            var counts = new int[4];
            // 각 가중치 구간의 중심을 순회하므로 난수에 따라 실패하지 않는다.
            for (int i = 0; i < 600; i++)
            {
                counts[BasicAttackDrawRules.SelectIndex(cards, (i + 0.5f) / 600f)]++;
            }

            Assert.That(counts, Is.EqualTo(new[] { 200, 200, 100, 100 }));
            Assert.That(BasicAttackDrawRules.SelectIndex(cards, 1f), Is.EqualTo(3));
        }

        [Test]
        public void BasicAttackDraw_SkipsMissingSkillsAndEmptyPools()
        {
            var valid = new ComboCard(Skill());
            Assert.That(BasicAttackDrawRules.SelectIndex(null, 0f), Is.EqualTo(-1));
            Assert.That(BasicAttackDrawRules.SelectIndex(new ComboCard[0], 0f), Is.EqualTo(-1));
            Assert.That(BasicAttackDrawRules.SelectIndex(new[] { null, new ComboCard(null) }, 0f), Is.EqualTo(-1));
            Assert.That(BasicAttackDrawRules.SelectIndex(new[] { null, valid }, 0f), Is.EqualTo(1));
        }

        [Test]
        [TestCase(0f, true)]
        [TestCase(0.9f, false)]
        public void BasicAttackDraw_ConsumesOneExistingCardFromHandOrDeck(float roll, bool fromHand)
        {
            var hand = new Hand();
            var handCard = new ComboCard(Skill());
            var deckCard = new ComboCard(Skill(), golden: true);
            hand.Add(handCard);
            var deck = new Deck(new[] { deckCard });
            int handChanges = 0, deckChanges = 0;
            hand.OnChanged += () => handChanges++;
            deck.OnChanged += () => deckChanges++;

            ComboCard selected = BasicAttackDrawRules.TakeCard(hand, deck, roll);
            Assert.That(selected, Is.SameAs(fromHand ? handCard : deckCard));
            Assert.That(hand.Count, Is.EqualTo(fromHand ? 0 : 1));
            Assert.That(deck.Count, Is.EqualTo(fromHand ? 1 : 0));
            Assert.That(handChanges, Is.EqualTo(fromHand ? 1 : 0));
            Assert.That(deckChanges, Is.EqualTo(fromHand ? 0 : 1));
            var discard = new Discard();
            discard.Add(selected);
            Assert.That(hand.Count + deck.Count + discard.Count, Is.EqualTo(2));
        }

        [Test]
        public void BasicAttackDraw_EmptyPilesDoNotCreateCards()
        {
            Assert.That(BasicAttackDrawRules.TakeCard(new Hand(), new Deck(), 0f), Is.Null);
        }

        [Test]
        public void BasicAttackDraw_ExcludesUnavailableCastersBeforeSelecting()
        {
            var hand = new Hand();
            var unavailable = new ComboCard(Skill());
            var available = new ComboCard(Skill());
            hand.Add(unavailable);
            var deck = new Deck(new[] { available });
            Assert.That(BasicAttackDrawRules.TakeCard(hand, deck, 0f, c => c == available), Is.SameAs(available));
            Assert.That(hand.GetCard(0), Is.SameAs(unavailable));
            Assert.That(BasicAttackDrawRules.TakeCard(hand, deck, 0f, c => false), Is.Null);
            Assert.That(hand.Count, Is.EqualTo(1));
        }

        [Test]
        public void TargetSize_ScalesWithMemberCount()
        {
            Assert.That(DeckRules.TargetSize(4), Is.EqualTo(Deck.Size), "4인은 만석 기준값과 같아야 한다.");
            Assert.That(DeckRules.TargetSize(3), Is.EqualTo(12));
            Assert.That(DeckRules.TargetSize(2), Is.EqualTo(8));
            Assert.That(DeckRules.TargetSize(1), Is.EqualTo(Ally.EquipSlots));
        }

        [Test]
        public void TargetSize_ZeroOrNegative_IsZero()
        {
            // 예외가 아니라 0이다 — 동료가 전멸한 프레임에도 이 함수가 불린다.
            Assert.That(DeckRules.TargetSize(0), Is.EqualTo(0));
            Assert.That(DeckRules.TargetSize(-1), Is.EqualTo(0));
        }

        /// <summary>
        /// <see cref="Player.Party"/>는 빈 칸에 null이 들어 있고, 영구 사망한 동료의 슬롯도
        /// <see cref="PartyAssembler"/>가 지워 null이 된다. 구멍을 세면 목표가 안 줄어든다.
        /// </summary>
        [Test]
        public void CountFilled_SkipsHoles()
        {
            var party = new[] { Member(), null, Member(), null };

            Assert.That(DeckRules.CountFilled(party), Is.EqualTo(2));
            Assert.That(DeckRules.TargetSize(party), Is.EqualTo(8));
        }

        [Test]
        public void CountFilled_NullList_IsZero()
        {
            Assert.That(DeckRules.CountFilled((IReadOnlyList<PartyMemberData>)null), Is.EqualTo(0));
            Assert.That(DeckRules.CountFilled((IReadOnlyList<Ally>)null), Is.EqualTo(0));
        }

        // ── 순환 속도 ───────────────────────────────────

        [Test]
        public void CycleHands_IsDeckSizeOverHandSize()
        {
            Assert.That(DeckRules.CycleHands(16), Is.EqualTo(4));
            Assert.That(DeckRules.CycleHands(12), Is.EqualTo(3));
            Assert.That(DeckRules.CycleHands(8), Is.EqualTo(2));
            Assert.That(DeckRules.CycleHands(0), Is.EqualTo(0));
        }

        // ── 저작 실수 판별 ───────────────────────────────

        /// <summary>
        /// <b>이 테스트가 규칙의 핵심이다.</b> 인원이 적어서 덱이 작은 것은 실수가 아니고,
        /// 인원 대비 장수가 안 맞는 것만 실수다.
        /// </summary>
        [Test]
        public void Explain_SmallPartyIsNotAnError()
        {
            Assert.That(DeckRules.Explain(12, 3), Is.Null, "3인 12장은 정상이다.");
            Assert.That(DeckRules.Explain(8, 2), Is.Null);
            Assert.That(DeckRules.Explain(16, 4), Is.Null);
        }

        [Test]
        public void Explain_EmptyEquipSlot_IsAnError()
        {
            // 4인인데 15장 = 누군가 장착 칸이 비었다.
            Assert.That(DeckRules.Explain(15, 4), Is.Not.Null);
            Assert.That(DeckRules.Explain(11, 3), Is.Not.Null);
        }

        /// <summary>목표보다 <b>많아도</b> 어긋난 것이다 — 5장을 든 동료가 있다는 뜻이다.</summary>
        [Test]
        public void Explain_TooManyCards_IsAnError()
        {
            Assert.That(DeckRules.Explain(17, 4), Is.Not.Null);
        }

        [Test]
        public void Explain_EmptyParty_IsConsistent()
        {
            Assert.That(DeckRules.Explain(0, 0), Is.Null);
            Assert.That(DeckRules.Explain(4, 0), Is.Not.Null, "동료가 0명인데 카드가 있다.");
        }
    }
}
