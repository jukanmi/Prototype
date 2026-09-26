// 파티 편성 표와 그 조립 규칙 · 아군 레이어.
// 몸을 실제로 세우는 PartyAssembler는 프리팹에 물려 파일명이 고정이다.

using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ PartyCatalog ═══════════════════════════════════════════

    /// <summary>
    /// 고를 수 있는 파티 조합 목록. <see cref="SkillCatalog"/>와 같은 형태로,
    /// <c>Resources</c> 경로 로드로만 잡는다.
    ///
    /// <b>왜 Resources 인가.</b> 파티 선택 화면은 코드로 스스로 지어지므로
    /// (<see cref="Prototype.PartySelectUI"/>) 인스펙터에 에셋을 물릴 자리가 없다.
    /// 씬 배선을 0으로 유지하려면 이 통로가 유일하다 — <see cref="SkillCatalog"/>가
    /// 같은 이유로 같은 선택을 했다.
    ///
    /// 에셋이 하나도 없어도 동작한다. 그때는 <see cref="PartyAssembler"/>의
    /// 인스펙터 값(<c>standaloneLoadout</c>)이 그대로 쓰인다.
    /// </summary>
    public static class PartyCatalog
    {
        /// <summary><c>Assets/Data/Resources/</c> 아래 이 폴더에 둬야 잡힌다.</summary>
        public const string ResourceFolder = "Party";

        /// <summary>아무것도 고르지 않았을 때 쓰는 조합의 에셋 이름.</summary>
        public const string DefaultLoadoutName = "Loadout_Default";

        private static PartyLoadout[] cached;
        private static bool loaded;

        private static PartyMemberData[] members;
        private static bool membersLoaded;

        /// <summary>Domain Reload가 꺼져 있으면 static이 플레이 세션을 넘어 살아남는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cached = null;
            loaded = false;
            members = null;
            membersLoaded = false;
            heroes = null;
            heroesLoaded = false;
        }

        /// <summary>
        /// 데려갈 수 있는 동료 전부. 파티 편성 화면의 명단이다.
        ///
        /// <b>직업 · 이름 순으로 정렬한다.</b> <c>Resources.LoadAll</c>은 순서를 보장하지 않아
        /// 그대로 두면 에셋을 하나 추가할 때마다 화면의 줄 순서가 바뀐다.
        /// </summary>
        public static IReadOnlyList<PartyMemberData> AllMembers()
        {
            if (membersLoaded) return members;

            membersLoaded = true;
            members = Resources.LoadAll<PartyMemberData>(ResourceFolder) ?? new PartyMemberData[0];

            System.Array.Sort(members, (a, b) =>
            {
                if (a == null || b == null) return a == b ? 0 : (a == null ? 1 : -1);

                int byRole = a.role.CompareTo(b.role);
                return byRole != 0 ? byRole : string.CompareOrdinal(a.Label, b.Label);
            });

            return members;
        }

        /// <summary>고를 수 있는 조합 전부. 없으면 빈 배열 — null 검사를 흩뿌리지 않는다.</summary>
        public static IReadOnlyList<PartyLoadout> All()
        {
            if (loaded) return cached;

            loaded = true;
            cached = Resources.LoadAll<PartyLoadout>(ResourceFolder) ?? new PartyLoadout[0];
            return cached;
        }

        private static PlayerData[] heroes;
        private static bool heroesLoaded;

        /// <summary>
        /// 고를 수 있는 주인공 전부. 지금은 하나뿐이라 편성 화면이 이 줄을 감춘다 —
        /// 둘 이상이 되는 순간 저절로 나타난다.
        /// </summary>
        public static IReadOnlyList<PlayerData> AllHeroes()
        {
            if (heroesLoaded) return heroes;

            heroesLoaded = true;
            heroes = Resources.LoadAll<PlayerData>(ResourceFolder) ?? new PlayerData[0];

            System.Array.Sort(heroes, (a, b) =>
            {
                if (a == null || b == null) return a == b ? 0 : (a == null ? 1 : -1);
                return string.CompareOrdinal(a.Label, b.Label);
            });

            return heroes;
        }

        /// <summary>
        /// 기본 조합. <see cref="DefaultLoadoutName"/>이 있으면 그것, 없으면 목록의 첫 번째.
        /// 목록이 비면 null.
        /// </summary>
        public static PartyLoadout Default()
        {
            IReadOnlyList<PartyLoadout> all = All();
            if (all.Count == 0) return null;

            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].name == DefaultLoadoutName) return all[i];

            return all[0];
        }
    }

    // ══ PartyAssembleRules ═══════════════════════════════════════════

    /// <summary>
    /// 로드아웃을 슬롯에 배치하는 규칙. <see cref="PartyAssembler"/>에서 떼어 낸 순수 계산이다.
    ///
    /// <see cref="TagSwapRules"/> · <see cref="Prototype.StageOutcomeRules"/>와 같은 이유로 분리한다 —
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
            => RolesOf(loadout != null ? loadout.members : null);

        public static List<Role> RolesOf(IReadOnlyList<PartyMemberData> party)
        {
            var roles = new List<Role>(PartyLoadout.MaxMembers);
            if (party == null) return roles;

            for (int i = 0; i < party.Count; i++)
            {
                PartyMemberData m = party[i];
                if (m == null || roles.Contains(m.role)) continue;
                roles.Add(m.role);
            }

            return roles;
        }

        /// <summary>
        /// 이 조합이 덱에 넣을 카드 수. <see cref="DeckRules.TargetSize(int)"/>와 비교해
        /// 저작 실수를 파티 편성 시점에 잡는다 — 전투에 들어가서야 아는 값이 아니다.
        ///
        /// <b>상수 16과 비교하지 말 것.</b> 3인 파티는 12장이 정상이다.
        /// </summary>
        public static int CardCount(PartyLoadout loadout)
            => CardCount(loadout != null ? loadout.members : null);

        public static int CardCount(IReadOnlyList<PartyMemberData> party)
        {
            if (party == null) return 0;

            int n = 0;
            for (int i = 0; i < party.Count; i++)
            {
                PartyMemberData m = party[i];
                if (m == null || m.equipped == null) continue;

                foreach (ComboCard c in m.equipped)
                    if (c != null && c.Data != null) n++;
            }

            return n;
        }

        // ── 편성 (파티 선택 화면) ────────────────────────

        /// <summary>
        /// 명단에서 한 명을 넣거나 뺀다. 이미 있으면 빼고, 없으면 뒤에 붙인다.
        ///
        /// <b>고른 순서가 곧 슬롯 순서, 즉 F키 교대 순환 순서다.</b> 중간을 빼면 뒤가 당겨진다 —
        /// 빈 칸을 남기면 "3번이 비었는데 4번으로 교대된다"가 되어 순환이 안 읽힌다.
        /// (<see cref="Resolve"/>가 남기는 빈 칸은 <b>뒤쪽</b>에만 생긴다.)
        ///
        /// 가득 찬 상태에서 새 사람을 누르면 <b>아무 일도 안 한다</b> — 조용히 누군가를
        /// 밀어내면 방금 뭘 잃었는지 화면에서 알 수가 없다.
        /// </summary>
        /// <returns>명단이 실제로 바뀌었으면 true.</returns>
        public static bool Toggle(IList<PartyMemberData> party, PartyMemberData member, int max)
        {
            if (party == null || member == null) return false;

            int at = party.IndexOf(member);
            if (at >= 0) { party.RemoveAt(at); return true; }

            if (party.Count >= max) return false;

            party.Add(member);
            return true;
        }

        /// <summary>
        /// 두 번 이상 든 직업. <b>막지는 않는다</b> — 탱커 둘도 정상 동작한다.
        ///
        /// 다만 덱이 그 직업으로 기울고(장착 8장), 한 명이 죽어도
        /// <c>purgeCardsOnAllyDeath</c>가 카드를 안 걷는다(같은 직업이 살아 있으므로).
        /// 의도한 것이면 그대로 두면 되고, 아니면 화면에서 보고 고칠 수 있어야 한다.
        /// </summary>
        public static List<Role> DuplicateRoles(IReadOnlyList<PartyMemberData> party)
        {
            var seen = new List<Role>(PartyLoadout.MaxMembers);
            var dup = new List<Role>();
            if (party == null) return dup;

            for (int i = 0; i < party.Count; i++)
            {
                PartyMemberData m = party[i];
                if (m == null) continue;

                if (seen.Contains(m.role)) { if (!dup.Contains(m.role)) dup.Add(m.role); }
                else seen.Add(m.role);
            }

            return dup;
        }

        /// <summary>
        /// 이 조합으로 런을 시작할 수 있는가.
        ///
        /// <b>한 명은 있어야 한다.</b> 동료가 0명이면 덱이 0장이라 U키도 불릿타임도
        /// 아무것도 안 나간다 — 플레이어 몸으로 평타만 치는 판이 된다.
        /// 그건 버그처럼 보이지, 선택처럼 보이지 않는다.
        /// </summary>
        public static bool CanStart(IReadOnlyList<PartyMemberData> party)
        {
            if (party == null) return false;

            for (int i = 0; i < party.Count; i++)
                if (party[i] != null) return true;

            return false;
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

    // ══ AllyLayers ═══════════════════════════════════════════

    /// <summary>
    /// 아군의 <b>레이어 배선</b>. <see cref="EnemyLayers"/>의 아군판이고, 존재 이유도 같다.
    ///
    /// <b>왜 이제 필요한가.</b> 지금까지 아군 레이어는 <c>SceneLayoutBuilder.AssignLayers</c>가
    /// <b>씬을 구울 때</b> 칠했다. 파티가 씬에 놓여 있었기 때문이다.
    /// 파티가 <see cref="PartyAssembler"/>의 프리팹 안으로 들어가면 그 빌더는 파티를 못 본다 —
    /// 아무도 안 칠하면 <c>Ally</c>가 Default(0) 레이어로 서고, <see cref="Attack"/>은
    /// 충돌 매트릭스를 그대로 읽으므로 결과는 <b>적을 통과하는 아군</b>이다.
    ///
    /// 증상이 "가끔 안 맞는다"가 아니라 "때려도 아무 일도 안 일어난다"라 오히려 놓치기 쉽다 —
    /// 스킬이 나가고 이펙트도 뜨는데 데미지만 없다.
    /// </summary>
    public static class AllyLayers
    {
        public const string HurtboxLayer = "AllyHurtbox";
        public const string HitboxLayer = "AllyHitbox";

        /// <summary>
        /// 아군 하나를 규약대로 맞춘다. 몸통은 피격 레이어, <see cref="Attack"/>이 붙은 자식은
        /// 전부 타격 레이어 — 평타 히트박스와 <c>SkillHitbox</c> 둘 다 해당한다.
        ///
        /// 비활성 자식까지 본다. 스킬 히트박스는 꺼져 있는 것이 정상이다.
        /// </summary>
        public static void Apply(GameObject root)
        {
            if (root == null) return;

            int hurt = LayerMask.NameToLayer(HurtboxLayer);
            int hit = LayerMask.NameToLayer(HitboxLayer);

            if (hurt < 0 || hit < 0)
            {
                BattleLog.Warn(LogCategory.Combat,
                    $"레이어 '{HurtboxLayer}' 또는 '{HitboxLayer}'가 프로젝트에 없다. " +
                    "'Prototype ▸ 씬 벨트스크롤 배치로 정리'를 한 번 돌려 레이어를 만들 것.", root);
                return;
            }

            root.layer = hurt;

            foreach (Attack attack in root.GetComponentsInChildren<Attack>(true))
                attack.gameObject.layer = hit;
        }
    }
}
