namespace Prototype.YG
{
    /// <summary>스테이지의 결말.</summary>
    public enum StageOutcome
    {
        /// <summary>아직 싸우는 중.</summary>
        Undecided,
        Victory,
        Defeat,
    }

    /// <summary>
    /// 승패 판정 규칙. <b>순수 함수만</b> 둔다 — 씬도 시간도 모른다.
    ///
    /// 떼어 놓은 이유는 첫 프레임 오판정 때문이다. <see cref="Prototype.BattleRegistry"/>는
    /// Entity 가 <c>Start</c>에서 스스로 등록하는 구조라, 씬이 올라온 직후 한두 프레임은
    /// 목록이 비어 있다. 그 순간 그대로 판정하면 <b>적 0명 = 승리</b>이면서 동시에
    /// <b>산 아군 0명 = 패배</b>가 되어, 시작하자마자 결과 화면이 뜬다.
    /// </summary>
    public static class StageOutcomeRules
    {
        /// <summary>
        /// 판정을 시작해도 되는가. 양쪽이 최소 한 명씩 등록을 마쳐야 한다.
        ///
        /// 한 번 true 가 되면 부르는 쪽이 그 사실을 붙들고 다시 묻지 않는다 —
        /// 적이 전멸한 뒤에는 이 조건이 다시 false 가 되기 때문이다.
        /// </summary>
        public static bool CanJudge(int registeredEnemies, int registeredAllies)
            => registeredEnemies > 0 && registeredAllies > 0;

        /// <summary>
        /// 결말. <b>패배가 승리보다 앞선다</b> — 마지막 적과 마지막 아군이 같은 프레임에
        /// 쓰러졌다면 이긴 것으로 쳐 주지 않는다.
        ///
        /// 웨이브 없는 스테이지(씬에 적을 직접 놓은 방)를 위한 지름길이다.
        /// 웨이브가 도는 방은 <see cref="Evaluate(int, bool, bool)"/>를 쓴다.
        /// </summary>
        public static StageOutcome Evaluate(int aliveEnemies, bool allAlliesDead)
            => Evaluate(aliveEnemies, allAlliesDead, wavesRemaining: false);

        /// <summary>
        /// 웨이브가 도는 방의 결말.
        ///
        /// <paramref name="wavesRemaining"/>이 없으면 <b>첫 웨이브만 잡고 스테이지가 끝난다</b>.
        /// 웨이브 사이에는 살아 있는 적이 0명인 구간이 반드시 생기는데(다음 웨이브가 아직 안 나왔다),
        /// 그 한 프레임이 그대로 승리로 잡히기 때문이다.
        ///
        /// 패배는 그 영향을 받지 않는다 — 뒤에 웨이브가 몇 개 남았든 아군이 전멸하면 진 것이다.
        /// </summary>
        public static StageOutcome Evaluate(int aliveEnemies, bool allAlliesDead, bool wavesRemaining)
        {
            if (allAlliesDead) return StageOutcome.Defeat;
            if (wavesRemaining) return StageOutcome.Undecided;
            if (aliveEnemies <= 0) return StageOutcome.Victory;

            return StageOutcome.Undecided;
        }

        /// <summary>
        /// 우측 출구에 닿았는가. 방 오른쪽 벽(x = 6) 앞에 서면 다음 스테이지로 넘어간다.
        ///
        /// 벽 좌표를 그대로 쓰지 않는 이유는 몸통 반지름 때문이다 —
        /// 캐릭터는 벽에 막혀 x ≈ 5.5 에서 멈추므로, 문턱이 6이면 영영 닿지 않는다.
        /// </summary>
        public static bool ReachedExit(float x, float exitX) => x >= exitX;
    }
}
