namespace Prototype
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
    /// 목록이 비어 있다. 그 순간 그대로 판정하면 <b>적 0명 = 승리</b>가 되어,
    /// 시작하자마자 결과 화면이 뜬다 — <see cref="CanJudge"/>가 그걸 막는다.
    ///
    /// 패배 쪽은 한 발 더 나간다. 등록 목록이 비는 창은 첫 프레임뿐이 아니라 <b>전투 내내</b>
    /// 생긴다(교대 · 콤보 시전자 전환). 그래서 전멸은 등록 목록이 아니라 파티 명부에 묻는다 —
    /// <see cref="PartyWiped"/>.
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
        /// 파티가 전멸했는가. <b>명부(로스터)가 있으면 그쪽이 이긴다.</b>
        ///
        /// <see cref="BattleRegistry"/>를 그대로 못 쓰는 이유가 이 함수의 존재 이유다 —
        /// 거기에는 <b>필드에 선 몸 하나만</b> 들어 있고(벤치로 내려간 동료는 <c>OnDisable</c>에서
        /// 빠진다), 교대와 콤보 시전자 전환 사이에는 아무도 등록돼 있지 않은 프레임이 반드시 생긴다.
        /// 특히 불릿타임 콤보 중에는 조작 캐릭터가 벤치로 내려가고 시전자만 서 있어서,
        /// <b>그 시전자가 죽으면 등록 목록이 통째로 빈다</b>. 그 한 프레임을 전멸로 읽어서
        /// 파티 체력바가 멀쩡한데 패배 화면이 뜨는 버그가 났다.
        ///
        /// 명부는 체력바가 보는 것과 같은 목록이다(<c>TagSwapController.Roster</c>) —
        /// 화면에 보이는 것과 판정이 어긋나지 않는 유일한 방법이다.
        ///
        /// <paramref name="rosterSize"/>가 0이면 명부가 없는 씬(스킬 실험장 · 훈련장)이거나
        /// 로스터를 못 만든 경우다. 그때만 등록 목록으로 떨어진다 — 명부가 비었다고
        /// 전멸로 치면 배선 실수가 곧바로 패배 화면이 된다.
        /// </summary>
        public static bool PartyWiped(int rosterSize, int rosterAlive, int registeredAlive)
            => rosterSize > 0 ? rosterAlive <= 0 : registeredAlive <= 0;

        /// <summary>
        /// 출구선이 오른쪽 벽에서 떨어지는 거리.
        ///
        /// 캐릭터는 몸통 반지름(≈0.5) 때문에 벽 앞에서 멈춘다. 문턱을 벽에 맞추면 영영 닿지 않으므로
        /// 그보다 안쪽에 둔다. 100% 방(벽 x = 6)에서 문턱 5가 나오는 값이다.
        ///
        /// <b>방 비율을 안 탄다.</b> 이건 방의 치수가 아니라 몸통 반지름이 정하는 여유고,
        /// 몸은 방이 줄어도 안 줄어든다. 80% 방에 0.8을 곱하면 여유가 0.3으로 줄어 다시 아슬아슬해진다.
        /// </summary>
        public const float ExitInset = 1f;

        /// <summary>
        /// 이 방의 출구선. <b>실제</b> 방의 오른쪽 벽에서 <see cref="ExitInset"/>만큼 안쪽이다.
        ///
        /// 방 크기 보정(<see cref="RoomRules"/>)이 벽을 옮기므로 문턱도 같이 움직여야 한다 —
        /// 고정 5로 두면 80% 방(벽 x = 4.8)에서는 캐릭터가 4.3에서 막혀 <b>출구가 영영 안 열린다.</b>
        /// 반대로 125% 방(벽 x = 7.5)에서는 아직 한참 남았는데 2.5나 일찍 열린다.
        /// </summary>
        public static float ExitLine(float roomMaxX) => roomMaxX - ExitInset;

        /// <summary>
        /// 우측 출구에 닿았는가. 방 오른쪽 벽 앞에 서면 다음 스테이지로 넘어간다.
        /// 문턱은 <see cref="ExitLine"/>이 방에서 푼다.
        /// </summary>
        public static bool ReachedExit(float x, float exitX) => x >= exitX;
    }

    // ══ EncounterClearRules ═════════════════════════════════════════
    // 조우가 끝났는가 — 인구조사와 판정.
    //
    // 방(웨이브)과 아레나(라운드)가 <b>같은 규칙</b>을 쓴다. 예전에는 두 벌이었고,
    // 세는 항목이 교집합 하나뿐이라 한쪽 것을 그대로 쓰면 반드시 틀렸다.
    // 틀리는 방향까지 반대였다 — 아레나 칸을 빠뜨리면 조기 종료되고,
    // 웨이브 칸을 빠뜨리면 적 없는 화면에서 영영 안 끝난다.
    //
    // 계획서: docs/Stage_Encounter_Unification_Plan.md (3.3)
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
