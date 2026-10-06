using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 레벨업 선택지가 뽑히는 <b>모집단</b>. "동료가 가질 수 있는 스킬" 전부를 한 줄로 세운 표다.
    ///
    /// <see cref="VfxLibrary"/>와 같은 형태다 — <c>Resources</c> 경로 로드로만 잡는다.
    /// 레벨업 화면은 코드로 스스로 지어지므로 인스펙터에 에셋을 물릴 자리가 없고,
    /// 씬 배선을 0으로 유지하려면 이 통로가 유일하다.
    ///
    /// <b>에셋이 없어도 동작한다.</b> 없으면 지금 덱에 든 카드에서 모집단을 긁는다 —
    /// 훈련장·테스트 씬을 단독으로 켜도 레벨업이 굴러가야 하기 때문이다.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillCatalog", menuName = "Prototype/Skill Catalog")]
    public class SkillCatalog : ScriptableObject
    {
        /// <summary><c>Assets/Data/Resources/</c> 아래 이 이름으로 둬야 잡힌다.</summary>
        public const string ResourcePath = "SkillCatalog";

        [Tooltip("레벨업에서 뽑힐 수 있는 스킬 전부. 비어 있으면 덱에 든 카드로 폴백한다.")]
        [SerializeField] private List<SkillData> skills = new List<SkillData>();

        public IReadOnlyList<SkillData> Skills => skills;

        private static SkillCatalog cached;
        private static bool loaded;

        /// <summary>없으면 null. 호출부는 null을 폴백 신호로 본다.</summary>
        public static SkillCatalog Get()
        {
            if (loaded) return cached;

            loaded = true;
            cached = Resources.Load<SkillCatalog>(ResourcePath);
            return cached;
        }

        /// <summary>
        /// 선택지를 뽑을 모집단. 표가 있으면 그것을, 없으면 <paramref name="fallback"/>에서 긁는다.
        ///
        /// <paramref name="roles"/>에 든 직업만 남긴다 — 시전할 동료가 없는 카드는
        /// 뽑아 줘 봐야 손패 맨 앞을 막는 짐이 된다. 비어 있으면(파티 배선 전 · 테스트 코드)
        /// <b>거르지 않는다</b>. 조용히 0장을 돌려주면 레벨업 화면이 이유 없이 빈다.
        /// </summary>
        public static List<SkillData> Pool(IReadOnlyList<Role> roles, IReadOnlyList<ComboCard> fallback)
        {
            var pool = new List<SkillData>();

            SkillCatalog catalog = Get();
            if (catalog != null) Fill(pool, catalog.skills, roles);

            if (pool.Count == 0 && fallback != null)
                for (int i = 0; i < fallback.Count; i++)
                    Add(pool, fallback[i] != null ? fallback[i].Data : null, roles);

            return pool;
        }

        private static void Fill(List<SkillData> pool, List<SkillData> source, IReadOnlyList<Role> roles)
        {
            if (source == null) return;
            for (int i = 0; i < source.Count; i++) Add(pool, source[i], roles);
        }

        private static void Add(List<SkillData> pool, SkillData data, IReadOnlyList<Role> roles)
        {
            if (data == null || pool.Contains(data)) return;
            if (!Allows(roles, data.role)) return;

            pool.Add(data);
        }

        private static bool Allows(IReadOnlyList<Role> roles, Role role)
        {
            if (roles == null || roles.Count == 0) return true;

            for (int i = 0; i < roles.Count; i++)
                if (roles[i] == role) return true;

            return false;
        }

        /// <summary>Domain Reload가 꺼져 있으면 static이 플레이 세션을 넘어 살아남는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cached = null;
            loaded = false;
        }
    }
}
