using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 같은 스킬 자동 합성 규칙(임시). 덱에는 <c>스킬이름:개수</c>만 저장하고,
    /// 개수의 최상위 비트로 레벨을 정한다. 4개 = 3렙, 7개 = 3렙 +3개.
    /// </summary>
    public static class SkillFusion
    {
        /// <summary>레벨당 데미지 배율 증가분(곱연산).</summary>
        public const float DamagePerLevel = 1.1f;

        /// <summary>
        /// 개수 → (레벨, 레벨 몫을 뺀 잔여 개수). 0 이하는 (0, 0).
        /// 레벨 = 최상위 비트 위치 + 1, 잔여 = count - 2^(레벨-1).
        /// </summary>
        public static (int level, int extra) FromCount(int count)
        {
            if (count <= 0) return (0, 0);

            int level = 1;
            while ((count >> level) > 0) level++;

            return (level, count - (1 << (level - 1)));
        }

        /// <summary>다음 레벨까지 레벨 도달 후 더 모아야 하는 개수. 레벨 N → 2^(N-1).</summary>
        public static int NeededForNext(int level) => level <= 0 ? 1 : 1 << (level - 1);

        /// <summary>"Lv3 3/4" — 레벨 도달 후 남은 개수 / 다음 레벨까지 필요한 개수.</summary>
        public static string Describe(int count)
        {
            (int level, int extra) = FromCount(count);
            return $"Lv{level} {extra}/{NeededForNext(level)}";
        }

        /// <summary>레벨 N의 데미지 배율. 1렙 = 1배, 이후 레벨마다 ×1.1.</summary>
        public static float DamageMul(int level)
            => level <= 1 ? 1f : Mathf.Pow(DamagePerLevel, level - 1);

        public static float DamageMulFromCount(int count) => DamageMul(FromCount(count).level);
    }
}
