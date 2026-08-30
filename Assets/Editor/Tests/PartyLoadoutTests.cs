using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 프로젝트에 있는 모든 <see cref="PartyLoadout"/> · <see cref="PartyMemberData"/> 에셋을 훑는다.
    ///
    /// 이 검사가 없으면 저작 실수가 <b>플레이 도중에만</b> 드러난다 —
    /// 직업이 어긋난 카드는 조용히 덱에 들어가 손패 맨 앞을 막고, 증상은 "U키가 안 먹는다"다.
    /// </summary>
    public class PartyLoadoutTests
    {
        private static T[] All<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            var list = new T[guids.Length];

            for (int i = 0; i < guids.Length; i++)
                list[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));

            return list;
        }

        [Test]
        public void EveryMember_IsAuthoredConsistently()
        {
            foreach (PartyMemberData m in All<PartyMemberData>())
            {
                if (m == null) continue;

                Assert.That(PartyAssembleRules.IsValid(m, out string reason), Is.True,
                    $"{AssetDatabase.GetAssetPath(m)} — {reason}");
            }
        }

        [Test]
        public void EveryMember_HasFullEquipSlots()
        {
            foreach (PartyMemberData m in All<PartyMemberData>())
            {
                if (m == null) continue;

                Assert.That(m.equipped.Count, Is.EqualTo(Ally.EquipSlots),
                    $"{AssetDatabase.GetAssetPath(m)} 의 장착이 {m.equipped.Count}장이다 " +
                    $"(목표 {Ally.EquipSlots}장).");
            }
        }

        /// <summary>
        /// 표에 든 카드는 <see cref="Ally"/>에 <b>복제</b>되어야 한다.
        /// 참조를 공유하면 레벨업 합성의 황금 승급이 에셋에 눌러붙어 다음 런까지 따라간다.
        /// </summary>
        [Test]
        public void CloneCards_ReturnsIndependentInstances()
        {
            foreach (PartyMemberData m in All<PartyMemberData>())
            {
                if (m == null || m.equipped.Count == 0) continue;

                var clone = m.CloneCards();

                Assert.That(clone.Count, Is.EqualTo(m.equipped.Count));
                for (int i = 0; i < clone.Count; i++)
                {
                    Assert.That(clone[i], Is.Not.SameAs(m.equipped[i]),
                        $"{AssetDatabase.GetAssetPath(m)} 의 카드 {i}가 복제되지 않았다.");
                    Assert.That(clone[i].Data, Is.SameAs(m.equipped[i].Data));
                }
            }
        }

        [Test]
        public void EveryLoadout_FitsSlotCount()
        {
            foreach (PartyLoadout l in All<PartyLoadout>())
            {
                if (l == null) continue;

                Assert.That(l.members, Is.Not.Null, $"{AssetDatabase.GetAssetPath(l)} 의 members 가 null 이다.");
                Assert.That(l.members.Length, Is.LessThanOrEqualTo(PartyLoadout.MaxMembers),
                    $"{AssetDatabase.GetAssetPath(l)} 의 칸이 {PartyLoadout.MaxMembers}개를 넘는다 — " +
                    "넘친 동료는 조용히 잘린다.");
            }
        }

        /// <summary>
        /// 덱 장수는 <b>인원 × 4</b>여야 한다. 상수 16이 아니다 —
        /// 3인 로드아웃은 12장이 정상이고, 그걸 오류로 잡으면 3인 파티를 저작할 수 없다.
        ///
        /// 여기 걸리는 것은 "4인인데 15장" 같은 <b>빈 장착 칸</b>뿐이다.
        /// </summary>
        [Test]
        public void EveryLoadout_CardCountMatchesMemberCount()
        {
            foreach (PartyLoadout l in All<PartyLoadout>())
            {
                if (l == null) continue;

                string problem = DeckRules.Explain(PartyAssembleRules.CardCount(l), l.FilledCount);

                Assert.That(problem, Is.Null,
                    $"{AssetDatabase.GetAssetPath(l)} — {problem}");
            }
        }
    }
}
