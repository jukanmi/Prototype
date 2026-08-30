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

        /// <summary>Domain Reload가 꺼져 있으면 static이 플레이 세션을 넘어 살아남는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cached = null;
            loaded = false;
        }

        /// <summary>고를 수 있는 조합 전부. 없으면 빈 배열 — null 검사를 흩뿌리지 않는다.</summary>
        public static IReadOnlyList<PartyLoadout> All()
        {
            if (loaded) return cached;

            loaded = true;
            cached = Resources.LoadAll<PartyLoadout>(ResourceFolder) ?? new PartyLoadout[0];
            return cached;
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
