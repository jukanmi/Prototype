// 조우가 끝났는가 — 인구조사와 판정.
//
// 방(웨이브)과 아레나(라운드)가 <b>같은 규칙</b>을 쓴다. 예전에는 두 벌이었고,
// 세는 항목이 교집합 하나뿐이라 한쪽 것을 그대로 쓰면 반드시 틀렸다.
// 틀리는 방향까지 반대였다 — 아레나 칸을 빠뜨리면 조기 종료되고,
// 웨이브 칸을 빠뜨리면 적 없는 화면에서 영영 안 끝난다.
//
// 계획서: docs/Stage_Encounter_Unification_Plan.md (3.3)

namespace Prototype
{
    /// <summary>
    /// 조우가 지금 어떤 상태인지의 인구조사. <b>일곱 칸으로 나눠 세는 것 자체가 규칙이다.</b>
    ///
    /// 앞의 넷은 몸의 상태고, 뒤의 셋은 아직 안 나왔거나 못 나온 것들이다.
    /// 어느 칸이 위협으로 세어지는지가 <see cref="EncounterClearRules.Threats"/>에 있다.
    /// </summary>
    public struct EncounterCensus
    {
        /// <summary>아직 안 나온 예약분. 시차를 기다리는 중이거나 예고만 떠 있다.</summary>
        public int pending;

        /// <summary>
        /// 걸어 나오는 중. <b>판정이 꺼져 있어 때릴 수도 맞을 수도 없다.</b>
        /// 그래도 위협이다 — 곧 싸울 몸이고, 여기서 안 세면 조우가 조기 종료된다.
        /// </summary>
        public int entering;

        /// <summary>전투 중.</summary>
        public int fighting;

        /// <summary>
        /// 사망 연출 중. 오브젝트는 남아 있지만 위협이 아니다.
        /// 세면 시체가 사라질 때까지 문이 안 열려 몇 초씩 멈춘 것처럼 보인다.
        /// </summary>
        public int dying;

        /// <summary>이 조우가 실제로 낳은 적. 죽었어도 센다. 위협이 아니다.</summary>
        public int spawned;

        /// <summary>
        /// 소환에 실패한 횟수(데이터 · 프리팹 미배선).
        ///
        /// 세어 두지 않으면 판정이 <c>spawned &gt; 0</c>에 걸려 <b>영원히 안 끝난다</b> —
        /// 화면에는 적이 하나도 없는데 조우가 클리어되지 않는, 원인이 전혀 안 보이는 상태가 된다.
        /// </summary>
        public int spawnFailures;

        /// <summary>아직 안 들어온 증원. 지속 리젠이 없으면 0이다.</summary>
        public int reinforcementsLeft;
    }

    /// <summary>
    /// 조우 종료 판정. <b>순수 함수</b>다.
    ///
    /// 틀리는 방향이 둘인데 증상이 정반대다. 너무 일찍 끝내면 예약분이 다음 조우 위로 쏟아지고,
    /// 너무 늦게 끝내면 화면에 적이 하나도 없는데 스테이지가 안 넘어간다.
    /// 둘 다 화면만 봐서는 원인이 안 보인다 — 그래서 씬도 시간도 모르는 자리에 둔다.
    ///
    /// <see cref="WaveAdvance"/>를 여기서 받는 이유는 조건이 늘어날 자리가 여기 하나뿐이기
    /// 때문이다. 디렉터 안에 두면 조건을 늘릴 때마다 <c>MonoBehaviour</c>를 고쳐야 하고,
    /// 그러면 테스트가 못 따라온다.
    /// </summary>
    public static class EncounterClearRules
    {
        /// <summary>
        /// 아직 남아 있는 위협의 수.
        ///
        /// <b>사망 연출과 누적 마릿수와 소환 실패는 빠진다.</b> 그 셋은 "이미 지나간 것"이라
        /// 세면 조우가 안 끝난다. 반대로 예약분과 진입 중과 남은 증원은 "아직 올 것"이라
        /// 안 세면 조우가 일찍 끝난다.
        /// </summary>
        public static int Threats(in EncounterCensus census)
            => Max0(census.pending)
             + Max0(census.entering)
             + Max0(census.fighting)
             + Max0(census.reinforcementsLeft);

        /// <summary>
        /// 이 조우가 끝났는가.
        ///
        /// <paramref name="advance"/>가 아직 안 돌아가는 조건이면
        /// <see cref="WaveAdvance.AllCleared"/>로 접는다 — 접지 않으면 그 조우에서
        /// <b>스테이지가 영영 안 끝난다</b>. 접는 판단은 <see cref="WaveAdvanceRules"/>
        /// 한 군데에만 둔다.
        /// </summary>
        public static bool IsCleared(in EncounterCensus census, WaveAdvance advance)
        {
            switch (WaveAdvanceRules.Resolve(advance))
            {
                // 조건이 늘어나면 여기가 갈라진다. 지금은 접혀서 전부 전멸로 온다.
                case WaveAdvance.AllCleared:
                default:
                    return AllCleared(in census);
            }
        }

        /// <summary>
        /// 전멸 조건. 지금까지의 유일한 동작이다.
        ///
        /// 마지막 줄이 핵심이다. <b>한 기도 안 나온 조우를 클리어로 치면</b> 배선이 어긋났을 때
        /// 방이 열리고 그냥 지나간다. 다만 소환 자체가 실패했다면 기다려도 나올 것이 없으므로 넘어간다.
        /// </summary>
        private static bool AllCleared(in EncounterCensus census)
        {
            if (Threats(in census) > 0) return false;

            return census.spawned > 0 || census.spawnFailures > 0;
        }

        private static int Max0(int n) => n > 0 ? n : 0;
    }
}
