using System;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// <see cref="StatusRules"/>의 표가 뚫리지 않게 잡아 두는 그물.
    ///
    /// 표가 전부 <c>default:</c> 폴백을 갖고 있어 새 <see cref="StatusKind"/>를 추가하고
    /// 매핑을 잊어도 컴파일이 통과한다 — 대신 화면에 영어 enum 이름이 뜨거나,
    /// 디버프인데 아무도 못 막는 상태가 조용히 생긴다. 전수 순회로 그걸 막는다.
    /// </summary>
    public class StatusRulesTests
    {
        [Test]
        public void EveryStatusKind_MapsExplicitly()
        {
            foreach (StatusKind kind in Enum.GetValues(typeof(StatusKind)))
            {
                Debuff bit = StatusRules.DebuffOf(kind);

                bool expectedDebuff = kind == StatusKind.Stun || kind == StatusKind.Freeze;

                Assert.That(StatusRules.IsDebuff(kind), Is.EqualTo(expectedDebuff),
                            $"{kind}의 디버프 여부가 표와 어긋난다");

                if (!expectedDebuff)
                    Assert.That(bit, Is.EqualTo(Debuff.None), $"버프 {kind}는 비트를 갖지 않는다");
            }
        }

        [Test]
        public void EveryStatusKind_HasALabelAndAColor()
        {
            foreach (StatusKind kind in Enum.GetValues(typeof(StatusKind)))
            {
                Assert.That(StatusEffectVisuals.Label(kind), Is.Not.EqualTo(kind.ToString()),
                            $"{kind}에 한글 라벨이 없어 폴백(영어 enum 이름)이 화면에 뜬다");

                Assert.That(StatusEffectVisuals.StatusColor(kind), Is.Not.EqualTo(Color.white),
                            $"{kind}에 색이 없어 폴백(흰색)으로 그려진다");
            }
        }

        [Test]
        public void EveryDebuffBit_RoundTripsToAStatusKind()
        {
            foreach (Debuff bit in StatusRules.Bits)
            {
                Assert.That(StatusRules.TryStatusOf(bit, out StatusKind kind), Is.True, $"{bit}를 되돌릴 수 없다");
                Assert.That(StatusRules.DebuffOf(kind), Is.EqualTo(bit), $"{bit} ↔ {kind} 왕복이 어긋난다");
            }
        }

        [Test]
        public void DebuffBits_AreDistinctPowersOfTwo()
        {
            Debuff seen = Debuff.None;

            foreach (Debuff bit in StatusRules.Bits)
            {
                int v = (int)bit;

                Assert.That(v, Is.GreaterThan(0), "None은 비트 목록에 들어가면 안 된다");
                Assert.That(v & (v - 1), Is.Zero, $"{bit}가 2의 거듭제곱이 아니다");
                Assert.That(seen & bit, Is.EqualTo(Debuff.None), $"{bit}가 이미 쓰인 비트와 겹친다");

                seen |= bit;
            }

            Assert.That(seen, Is.EqualTo(Debuff.All), "All 마스크가 모든 비트를 덮지 않는다");
            Assert.That(Debuff.ActionBlocking, Is.EqualTo(Debuff.All),
                        "지금은 둘 다 행동을 막는다 — 둔화처럼 행동은 되는 디버프가 생기면 이 줄을 고칠 것");
        }

        [Test]
        public void BlocksAction_IgnoresBuffsOnly()
        {
            Assert.That(StatusRules.BlocksAction(Debuff.None), Is.False);
            Assert.That(StatusRules.BlocksAction(Debuff.Stun), Is.True);
            Assert.That(StatusRules.BlocksAction(Debuff.Freeze), Is.True);
            Assert.That(StatusRules.BlocksAction(Debuff.Stun | Debuff.Freeze), Is.True);
        }
    }
}
