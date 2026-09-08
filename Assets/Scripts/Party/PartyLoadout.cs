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

        [Tooltip("태그 로스터 0번. 비우면 Player 프리팹의 고정 스펙으로 돈다.\n\n" +
                 "주인공은 카드를 안 내므로 덱 장수에 영향을 주지 않는다 — " +
                 "여기서 갈리는 것은 평타와 조작감(대시 쿨 · 선입력 창)이다.")]
        public PlayerData hero;

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

        /// <summary>
        /// 화면에서 짠 조합. <b>에셋이 아니다</b> — 메모리에만 있는 인스턴스다.
        ///
        /// <see cref="PartyAssembler"/>는 조합이 에셋인지 아닌지 구분하지 않으므로
        /// 이걸 그대로 <see cref="Prototype.GameManager.SelectLoadout"/>에 넘기면 된다.
        /// <c>GameManager</c>가 Boot 씬에 상주하며 참조를 들고 있는 동안 살아 있고,
        /// 게임을 끄면 사라진다 — 저장은 하지 않는다.
        ///
        /// 넘긴 순서가 그대로 슬롯 순서, 즉 <b>F키 교대 순환 순서</b>가 된다.
        /// </summary>
        public static PartyLoadout CreateRuntime(IReadOnlyList<PartyMemberData> picked, string label = "편성한 파티")
        {
            var l = CreateInstance<PartyLoadout>();
            l.name = "Loadout_Runtime";
            l.loadoutName = label;
            l.Fill(picked);

            return l;
        }

        /// <summary>
        /// 칸을 다시 채운다. 앞에서부터 넣고 남는 칸은 비운다.
        ///
        /// 편성 화면이 고칠 때마다 새 인스턴스를 만드는 대신 이걸 쓴다 —
        /// <c>GameManager</c>가 이미 참조를 들고 있으므로, 같은 물건을 고치면
        /// 넘기는 쪽과 받는 쪽이 어긋날 일이 없다.
        /// </summary>
        public void Fill(IReadOnlyList<PartyMemberData> picked)
        {
            if (members == null || members.Length != MaxMembers)
                members = new PartyMemberData[MaxMembers];

            for (int i = 0; i < MaxMembers; i++)
                members[i] = picked != null && i < picked.Count ? picked[i] : null;
        }

        /// <summary>주인공을 갈아 끼운다. 편성 화면이 부른다.</summary>
        public void SetHero(PlayerData value) => hero = value;
    }
}
