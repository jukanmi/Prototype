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
