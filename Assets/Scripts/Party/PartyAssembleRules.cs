using System.Collections.Generic;

namespace Prototype
{
    /// <summary>
    /// 로드아웃을 슬롯에 배치하는 규칙. <see cref="PartyAssembler"/>에서 떼어 낸 순수 계산이다.
    ///
    /// <see cref="TagSwapRules"/> · <see cref="Prototype.YG.StageOutcomeRules"/>와 같은 이유로 분리한다 —
    /// EditMode 테스트는 <c>Awake</c>가 돌지 않아 씬을 세울 수 없다. 규칙만 여기 두면
    /// 씬 없이 검증되고, 슬롯을 실제로 만지는 부분은 얇은 어댑터로 남는다.
    /// </summary>
    public static class PartyAssembleRules
    {
        /// <summary>
        /// 로드아웃을 슬롯 수에 맞춰 편다. 모자라면 뒤를 null로 채우고, 넘치면 자른다.
        ///
        /// <b>빈 칸을 앞으로 당기지 않는다.</b> 슬롯 순서가 곧 교대 순환 순서(F키)라,
        /// 저작자가 1번에 탱커를 두었으면 3번이 비어도 탱커는 1번이어야 한다.
        /// </summary>
        public static PartyMemberData[] Resolve(PartyLoadout loadout, int slotCount)
        {
            var result = new PartyMemberData[slotCount < 0 ? 0 : slotCount];
            if (loadout == null || loadout.members == null) return result;

            int n = loadout.members.Length < result.Length ? loadout.members.Length : result.Length;
            for (int i = 0; i < n; i++) result[i] = loadout.members[i];

            return result;
        }

        /// <summary>
        /// 이 동료의 저작이 성립하는가. 성립하지 않으면 <paramref name="reason"/>에 이유가 담긴다.
        ///
        /// <b>직업 불일치가 핵심</b>이다. <see cref="BulletTimeController.ResolveCaster"/>는
        /// 카드의 <c>SkillData.role</c>로 시전자를 찾는다. 마법사 동료가 궁수 카드를 들면
        /// 그 카드는 덱에 들어가서 손패 맨 앞을 막고 <b>영영 안 나간다</b> —
        /// 에러도 경고도 없이 U키만 먹통이 된다. 그래서 여기서 잡는다.
        /// </summary>
        public static bool IsValid(PartyMemberData member, out string reason)
        {
            reason = null;
            if (member == null) { reason = "빈 칸"; return true; }   // 빈 칸은 정상이다

            if (member.equipped == null || member.equipped.Count == 0)
            {
                reason = $"{member.Label}: 장착 카드가 0장이다";
                return false;
            }

            for (int i = 0; i < member.equipped.Count; i++)
            {
                ComboCard c = member.equipped[i];
                if (c == null || c.Data == null)
                {
                    reason = $"{member.Label}: 장착 {i}번 칸이 비어 있다";
                    return false;
                }

                if (c.Data.role != member.role)
                {
                    reason = $"{member.Label}({member.role}): 장착 {i}번이 " +
                             $"{c.Data.role} 카드({c.Data.skillName})다 — 시전자가 없어 영영 안 나간다";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 이 파티가 시전할 수 있는 직업들. <see cref="BulletTimeController.AvailableRoles"/>의
        /// 저작 시점 판본이다 — 사망은 보지 않는다.
        ///
        /// 파티 선택 화면이 "이 조합으로 뽑을 수 있는 스킬"을 미리 보여줄 때 쓴다.
        /// </summary>
        public static List<Role> RolesOf(PartyLoadout loadout)
        {
            var roles = new List<Role>(PartyLoadout.MaxMembers);
            if (loadout == null || loadout.members == null) return roles;

            foreach (PartyMemberData m in loadout.members)
            {
                if (m == null || roles.Contains(m.role)) continue;
                roles.Add(m.role);
            }

            return roles;
        }

        /// <summary>
        /// 이 로드아웃이 덱에 넣을 카드 수. <see cref="Deck.Size"/>와 비교해
        /// 저작 실수를 파티 선택 시점에 잡는다.
        /// </summary>
        public static int CardCount(PartyLoadout loadout)
        {
            if (loadout == null || loadout.members == null) return 0;

            int n = 0;
            foreach (PartyMemberData m in loadout.members)
            {
                if (m == null || m.equipped == null) continue;

                foreach (ComboCard c in m.equipped)
                    if (c != null && c.Data != null) n++;
            }

            return n;
        }

        /// <summary>
        /// 쓸 로드아웃을 고른다. <b>Boot 경유와 단독 실행이 갈리는 유일한 지점</b>이다.
        ///
        /// <see cref="BulletTimeController"/>의 <c>StartupMode</c>와 같은 우선순위다 —
        /// 런 단위 결정은 런의 주인(<c>GameManager</c>)이 이기고, 그가 없을 때만
        /// 씬 인스펙터 값이 쓰인다.
        /// </summary>
        public static PartyLoadout Pick(PartyLoadout fromRun, PartyLoadout standalone)
            => fromRun != null ? fromRun : standalone;
    }
}
