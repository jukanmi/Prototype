namespace Prototype
{
    /// <summary>
    /// 라운드가 지금 어떤 상태인지의 인구조사. 네 칸으로 나눠 세는 것 자체가 규칙이다.
    /// </summary>
    public struct RoundCensus
    {
        /// <summary>아직 스폰되지 않은 예약분. 예고만 떠 있거나 시차를 기다리는 중이다.</summary>
        public int pending;

        /// <summary>벽에서 걸어 나오는 중. 판정이 꺼져 있어 <b>때릴 수도 맞을 수도 없다</b>.</summary>
        public int entering;

        /// <summary>전투 중.</summary>
        public int fighting;

        /// <summary>사망 연출 중. 오브젝트는 남아 있지만 위협이 아니다.</summary>
        public int dying;
    }

    /// <summary>
    /// 라운드 클리어 판정. <b>이 구조에서 가장 자주 터지는 부분</b>이라 순수 함수로 떼어 둔다.
    ///
    /// 살아 있는 적 수만 세면 안 된다. 마지막 몹이 죽는 순간 스폰 대기 중인 몹이 남아 있으면
    /// 라운드가 <b>조기 종료</b>되고, 그 뒤에 예약분이 튀어나와 이미 열린 문 앞에서 싸우게 된다.
    /// 반대로 사망 연출 중인 개체를 세면 시체가 사라질 때까지 문이 안 열려 몇 초씩 멈춘 것처럼 보인다.
    ///
    /// 그래서 <b>예약분 + 진입 중 + 전투 중</b>을 합친 값으로 판정하고,
    /// 사망 연출은 카운트에서 빼되 오브젝트는 남겨 둔다.
    /// </summary>
    public static class RoundClearRules
    {
        /// <summary>아직 남아 있는 위협의 수. 사망 연출은 빠진다.</summary>
        public static int Threats(in RoundCensus census)
            => Max0(census.pending) + Max0(census.entering) + Max0(census.fighting);

        /// <summary>
        /// 클리어인가.
        ///
        /// <paramref name="anySpawned"/>가 없으면 <b>한 기도 안 나온 라운드가 즉시 클리어</b>된다 —
        /// 배선이 어긋났을 때 아레나가 열리고 지나가지므로 원인이 전혀 안 보인다.
        /// </summary>
        public static bool IsCleared(in RoundCensus census, bool anySpawned)
            => anySpawned && Threats(in census) == 0;

        private static int Max0(int n) => n > 0 ? n : 0;
    }
}
