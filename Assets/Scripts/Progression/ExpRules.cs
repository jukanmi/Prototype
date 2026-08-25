using System;

namespace Prototype
{
    /// <summary>
    /// 경험치와 레벨업 단계의 <b>수치 전부</b>. 순수 함수라 씬을 켜지 않고 검증한다
    /// (<see cref="RoundClearRules"/> · <see cref="StageOutcomeRules"/>와 같은 이유).
    ///
    /// <code>
    ///   BaseCost(L)   = 10 × 1.1^(L-1)     레벨업할 때마다 다음 요구치가 1.1배
    ///   Cost(L, tier) = ceil(BaseCost(L) × [1.0, 1.5, 2.0][tier])
    ///   황금 확률      = [0%, 20%, 50%][tier]   선택지 카드 1장마다 따로 굴린다
    /// </code>
    ///
    /// <b>단계는 레벨 상승폭을 바꾸지 않는다</b> — 어느 단계로 올리든 레벨은 +1이다.
    /// 비싼 단계를 사는 이유는 오직 황금 카드 확률이고, 그래서 "싸게 여러 번 vs 모아서 크게"가
    /// 라운드마다 실제 선택이 된다. 단계가 레벨까지 더 올려 주면 3단계가 언제나 정답이 되어
    /// 선택이 사라진다.
    ///
    /// 소모는 <b>그 단계 값만</b> 빠진다. 남은 경험치는 다음 레벨업으로 이월된다.
    /// </summary>
    public static class ExpRules
    {
        /// <summary>1단계 · 2단계 · 3단계.</summary>
        public const int TierCount = 3;

        /// <summary>1레벨 1단계의 비용. 적 한 기가 떨어뜨리는 경험치와 같게 맞췄다.</summary>
        public const int BaseCost = 10;

        /// <summary>레벨이 오를 때마다 요구 경험치에 곱해지는 값.</summary>
        public const double LevelGrowth = 1.1;

        /// <summary>황금 카드의 데미지 배율.</summary>
        public const float GoldenDamageMul = 1.5f;

        /// <summary>레벨은 1부터 센다.</summary>
        public const int FirstLevel = 1;

        private static readonly double[] CostMul = { 1.0, 1.5, 2.0 };
        private static readonly float[] Golden = { 0f, 0.20f, 0.50f };

        /// <summary>
        /// 올림 직전에 빼 주는 오차 여유.
        ///
        /// <c>10 × 1.1</c>은 부동소수점에서 정확히 11이 아니라 11.000000000000002다.
        /// 그대로 올리면 2레벨 1단계가 11이 아니라 <b>12</b>가 되어 표와 어긋난다.
        /// </summary>
        private const double CeilEpsilon = 1e-6;

        public static bool IsValidTier(int tier) => tier >= 0 && tier < TierCount;

        /// <summary>이 레벨의 1단계 비용(올림 전 실수값).</summary>
        public static double BaseCostOf(int level)
            => BaseCost * Math.Pow(LevelGrowth, Math.Max(FirstLevel, level) - FirstLevel);

        /// <summary>이 레벨에서 그 단계로 올리는 데 드는 경험치. 잘못된 단계는 살 수 없게 int.MaxValue.</summary>
        public static int CostOf(int level, int tier)
            => IsValidTier(tier)
                ? (int)Math.Ceiling(BaseCostOf(level) * CostMul[tier] - CeilEpsilon)
                : int.MaxValue;

        /// <summary>이 단계에서 카드 한 장이 황금으로 나올 확률. 0~1.</summary>
        public static float GoldenChanceOf(int tier) => IsValidTier(tier) ? Golden[tier] : 0f;

        public static bool CanAfford(int exp, int level, int tier)
            => IsValidTier(tier) && exp >= CostOf(level, tier);

        /// <summary>지금 살 수 있는 가장 비싼 단계. 하나도 못 사면 -1.</summary>
        public static int HighestAffordableTier(int exp, int level)
        {
            for (int tier = TierCount - 1; tier >= 0; tier--)
                if (CanAfford(exp, level, tier)) return tier;

            return -1;
        }

        /// <summary>사람에게 보여 줄 번호. 0번 단계가 "1단계"다.</summary>
        public static int TierNumber(int tier) => tier + 1;
    }
}
