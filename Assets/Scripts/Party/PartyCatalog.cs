using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 고를 수 있는 파티 조합 목록. <see cref="SkillCatalog"/>와 같은 형태로,
    /// <c>Resources</c> 경로 로드로만 잡는다.
    ///
    /// <b>왜 Resources 인가.</b> 파티 선택 화면은 코드로 스스로 지어지므로
    /// (<see cref="Prototype.YG.PartySelectUI"/>) 인스펙터에 에셋을 물릴 자리가 없다.
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
}
