namespace Prototype
{
    // ══ AssaultBlock ═══════════════════════════════════════════

    /// <summary>총공격을 지금 못 내는 이유. <see cref="None"/>이면 낼 수 있다.</summary>
    public enum AssaultBlock
    {
        None,

        /// <summary>총공격이 이미 돌고 있다(또는 실행기가 없다).</summary>
        Busy,

        /// <summary>주인공이 쓰러졌다.</summary>
        HeroDown,

        /// <summary>게이지 · 쿨타임 · 마나가 모자라다. 자세한 이유는 <see cref="TacticGauge.Shortfall"/>.</summary>
        Charging,

        /// <summary>노릴 적이 없다 — 내면 게이지만 날아간다.</summary>
        NoEnemy,

        /// <summary>낼 슬롯이 없다 — 4칸이 전부 비었거나 시전자가 없다.</summary>
        NoSlots,
    }

    // ══ AssaultRules ═══════════════════════════════════════════

    /// <summary>
    /// 총공격의 두 규칙 — <b>언제 나가는가</b>와 <b>언제 편성을 바꾸는가</b>. 순수 함수라 씬 없이 테스트한다.
    ///
    /// <b>나가는 건 총공격키를 눌렀을 때뿐이다.</b> 이 규칙을 부르는 길은 <see cref="BulletTimeController.TryFireAssault"/>
    /// 하나고, 그걸 부르는 건 총공격키(<see cref="BattleCommander"/>)뿐이다. 예전에는 컨트롤러가 매 프레임 이 검사를 스스로 돌려서
    /// 게이지가 차는 순간 · 다음 방의 적이 나타나는 순간 · 편집창을 닫는 순간에 저절로 나갔다 — 고친 버그다.
    ///
    /// <b>편성은 칸(노드) 안에서, 싸움 밖에서만</b> 바꾼다. 칸의 지휘자가 지금 상태를 아래 규칙에 넣어
    /// <see cref="IAssaultEditGate"/>로 답한다. 마을도 문을 건다. 지도는 문 자체가 없다(<see cref="AssaultEditGate"/>).
    /// </summary>
    public static class AssaultRules
    {
        // ── 발동 ─────────────────────────────────────────

        /// <summary>
        /// 큐를 짓기 전에 볼 수 있는 조건. <b>순서가 곧 우선순위</b>다 — 여럿이 걸리면 앞의 이유를 돌려준다.
        ///
        /// 슬롯(<see cref="AssaultBlock.NoSlots"/>)은 큐를 지어 봐야 알아서 <see cref="CheckQueue"/>가 따로 본다.
        /// 큐부터 지으면 게이지가 덜 찼을 때 누를 때마다 "슬롯 건너뜀" 경고가 쌓인다.
        /// </summary>
        public static AssaultBlock CheckFire(bool running, bool heroDown, bool resourcesReady, int aliveEnemies)
        {
            if (running) return AssaultBlock.Busy;
            if (heroDown) return AssaultBlock.HeroDown;
            if (!resourcesReady) return AssaultBlock.Charging;
            if (aliveEnemies <= 0) return AssaultBlock.NoEnemy;
            return AssaultBlock.None;
        }

        /// <summary>지은 큐에 낼 슬롯이 하나라도 있는가.</summary>
        public static AssaultBlock CheckQueue(int slotCount)
            => slotCount > 0 ? AssaultBlock.None : AssaultBlock.NoSlots;

        /// <summary>로그 · 안내에 쓸 한 줄.</summary>
        public static string Describe(AssaultBlock block)
        {
            switch (block)
            {
                case AssaultBlock.None: return "발동";
                case AssaultBlock.Busy: return "총공격이 이미 진행 중";
                case AssaultBlock.HeroDown: return "주인공이 쓰러졌다";
                case AssaultBlock.Charging: return "게이지가 덜 찼다";
                case AssaultBlock.NoEnemy: return "노릴 적이 없다";
                case AssaultBlock.NoSlots: return "총공격 슬롯이 비었다";
                default: return block.ToString();
            }
        }

        // ── 편성 ─────────────────────────────────────────

        /// <summary>
        /// 전투 칸의 편성 문. <b>이긴 뒤</b>, 또는 <b>방 사이 통로</b>(앞 방을 깨고 다음 방에 들어서기 전)에서 열린다.
        /// 방 안에서 싸우는 중 · 진 판 · 칸을 떠나는 중이면 닫힌다.
        /// </summary>
        /// <param name="betweenRooms">다음 방 진입선을 기다리는 중(<see cref="StageProgressSource.AwaitingEntry"/>).</param>
        public static bool BattleNodeAllowsEdit(StageOutcome outcome, bool betweenRooms, bool leaving)
            => !leaving && (outcome == StageOutcome.Victory || (outcome == StageOutcome.Undecided && betweenRooms));

        /// <summary>
        /// 비전투 칸(상점 · 이벤트 · 모닥불)의 편성 문. 싸움이 벌어지지 않았고(또는 끝났고) · 지지 않았고 ·
        /// 칸을 떠나는 중이 아니면 열린다. 이벤트가 싸움으로 번지면 이길 때까지 닫힌다.
        /// </summary>
        public static bool PeacefulNodeAllowsEdit(bool fighting, bool defeated, bool leaving)
            => !fighting && !defeated && !leaving;
    }
}
