using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 이 런을 굴릴 파티 조합. Boot 씬에서 고른 결과물이자, 단독 실행의 기본값이기도 하다.
    ///
    /// 칸은 4개이고 <b>null 칸을 허용한다</b> — 3인 파티도 정상이다.
    /// <see cref="TagSwapRules"/>가 이미 빈 칸을 건너뛰므로 교대 순환이 그대로 돈다.
    /// </summary>
    [CreateAssetMenu(menuName = "Prototype/Party Loadout", fileName = "Loadout_")]
    public class PartyLoadout : ScriptableObject
    {
        /// <summary><see cref="Player.Party"/>의 칸 수와 같아야 한다.</summary>
        public const int MaxMembers = 4;

        [Tooltip("파티 선택 화면에 뜨는 이름.")]
        public string loadoutName = "기본 파티";

        [Tooltip("동료 4명. 빈 칸을 둬도 된다 — 3인 파티가 그대로 돈다.\n" +
                 "다만 덱은 4명 × 4장 = 16장을 기준으로 경고를 띄우므로 " +
                 "빈 칸이 있으면 BulletTimeController 가 장수 경고를 낸다(무해).")]
        public PartyMemberData[] members = new PartyMemberData[MaxMembers];

        public IReadOnlyList<PartyMemberData> Members => members;

        /// <summary>실제로 채워진 동료 수.</summary>
        public int FilledCount
        {
            get
            {
                if (members == null) return 0;

                int n = 0;
                for (int i = 0; i < members.Length; i++)
                    if (members[i] != null) n++;

                return n;
            }
        }

        public string Label => !string.IsNullOrEmpty(loadoutName) ? loadoutName : name;
    }
}
