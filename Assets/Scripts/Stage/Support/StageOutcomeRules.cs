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
}
