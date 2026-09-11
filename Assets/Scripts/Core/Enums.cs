namespace Prototype
{
    /// <summary>바닥(XZ) 위에 있는지, 공중(Y)에 떠 있는지.</summary>
    public enum PhysicsState
    {
        Ground,
        Aerial,
    }

    /// <summary>전투 상태. 전이 규칙은 <see cref="CombatStateRules"/>가 단독으로 관리한다.</summary>
    public enum CombatState
    {
        Neutral,
        LightHit,
        AerialHit,
        Knockback,
        WallBound,
        Down,
        Getup,
        Dead,
    }

    /// <summary>Control이 이번 프레임에 내린 명령.</summary>
    public enum Command
    {
        None,
        Move,
        Jump,
        Attack,
        Dash,
    }

    /// <summary>동료 직업. 덱은 직업별 4장 x 4직업 = 16장.</summary>
    public enum Role
    {
        Tanker,
        Warrior,
        Archer,
        Wizard,
    }

    /// <summary>스킬의 성격. 콤보 전이 설계용 분류.</summary>
    public enum AttackType
    {
        Gather,      // 모으기
        Launcher,    // 띄우기 (시동기)
        Strike,      // 공격기
        Push,        // 밀치기
        Charge,      // 차징
    }

    /// <summary>넉백 방향 계산 방식. 방향은 타격 순간에 계산한다.</summary>
    public enum KnockbackMode
    {
        /// <summary>시전자 정면 고정 (평타 · 돌진).</summary>
        Fixed,
        /// <summary>시전자 쪽으로 끌어당김 (모으기).</summary>
        TowardCaster,
        /// <summary>시전자 반대쪽으로 방사 (밀치기).</summary>
        AwayFromCaster,
        /// <summary>위로 띄움.</summary>
        Up,

        /// <summary>
        /// <b>가장 가까운 벽</b>으로 날린다(밀치기 전용).
        ///
        /// <see cref="AwayFromCaster"/>는 시전자가 선 자리에 따라 적이 허공으로 날아가
        /// 벽바운드가 운에 맡겨졌다. 밀치기의 존재 이유가 벽바운드 연계이므로 방향을
        /// 벽이 정하게 한다 — 값이 4번이라 기존 에셋의 0~3 로드는 그대로다.
        /// </summary>
        TowardWall,
    }

    /// <summary>
    /// 스킬이 자동으로 겨눌 상대를 고르는 규칙. 스킬 에셋마다 정한다.
    ///
    /// 밀치기를 <b>가장 먼 적</b>에게 걸면 벽까지의 거리가 확보되고,
    /// 모으기·시동기는 <b>가까운 적</b>이어야 콤보가 손 안에서 끝난다.
    /// </summary>
    public enum TargetPick
    {
        Nearest,
        Farthest,

        /// <summary>
        /// <b>공중에 뜬 적</b> 중 가장 가까운 하나. 아무도 안 떠 있으면
        /// <see cref="Nearest"/>로 떨어진다.
        ///
        /// 마무리기(내려찍기)가 쓴다 — 콤보의 마지막 타는 띄워 둔 적을 찍어야 값어치가 있는데,
        /// Nearest로 고르면 바로 옆에 서 있던 다른 적에게 나가 콤보가 끊긴다.
        /// 값이 2번이라 기존 에셋의 0~1 로드는 그대로다.
        /// </summary>
        NearestAerial,
    }

    /// <summary>스킬이 요구하는 조준 방식. 불릿타임 중 유저가 직접 지정한다.</summary>
    public enum TargetingType
    {
        None,
        GroundPoint,
        /// <summary>
        /// <b>사용 중단.</b> 대상 지정은 전부 <see cref="GroundPoint"/>로 옮겼다 —
        /// 상대는 좌표에서 시전 순간에 뽑는 편이 재타겟 · 사망 처리를 전부 없애 준다.
        /// 값을 지우면 뒤의 <see cref="Direction"/>이 3→2로 밀려 기존 .asset이 통째로 오독되므로
        /// <b>자리만 남겨 둔다</b>. 새 스킬에 쓰지 말 것 — SkillTableBuilder 검증이 잡는다.
        /// </summary>
        EnemyUnit,
        Direction,
    }

    public enum StatType
    {
        MoveSpeed,
        DashSpeed,
        JumpHeight,
        JumpTime,
        AttackPower,
        Defense,
    }

    public enum EnergyType
    {
        Health,
        Mana,
        BulletTimeGauge,
        /// <summary>보스의 가드. 0이 되면 가드브레이크 — 슈퍼아머가 풀린다.</summary>
        Guard,
    }

    /// <summary>
    /// 지속시간을 갖고 걸렸다 풀리는 상태. <see cref="StatusEffects"/>가 목록으로 들고 있다.
    ///
    /// <b>피격 경직은 여기 없다</b> — 그건 <see cref="CombatState"/>의 일이다. 반대로
    /// 스턴 · 빙결은 여기 있다. 둘의 차이는 "누가 덮어쓸 수 있는가"다: 경직은 다음 타격이
    /// 덮어쓰는 짧은 반응이고(그래서 벽스턴 1.2초가 뒤이은 평타 0.3초에 지워졌다),
    /// 디버프는 시간이 다 갈 때까지 남는다.
    /// </summary>
    public enum StatusKind
    {
        Shield,
        DamageCut,
        Lifesteal,

        /// <summary>행동 불능. 시간은 여기가 재고, 몸은 <see cref="StunState"/>가 붙잡는다.</summary>
        Stun,

        /// <summary>완전 정지. 스턴과 규칙이 같고 지속시간 · 연출만 다르다.</summary>
        Freeze,

        /// <summary>공중 바인드. 그 자리 그 높이에 못 박힌다 — 중력도 넉백도 안 먹고 데미지만 받는다.</summary>
        AirBind,
    }

    /// <summary>
    /// 행동 불능 계열을 한 벌의 비트로 접은 것. <see cref="StatusKind"/>가 "무엇이 걸렸나"라면
    /// 이쪽은 "그래서 지금 뭘 못 하나"다 — 판정하는 쪽이 종류를 몰라도 되게 하려고 나눴다.
    ///
    /// <b>불릿타임에는 안 닳는다.</b> <see cref="StatusEffects"/>를 굴리는 <see cref="Combat.Tick"/>이
    /// 스케일된 dt를 받기 때문인데, 이건 사고가 아니라 의도다 — 불릿타임은 유저가 카드를 고르는
    /// 시간이라 실시간으로 녹으면 오래 고민한 쪽이 벌을 받는다.
    /// </summary>
    [System.Flags]
    public enum Debuff
    {
        None    = 0,
        Stun    = 1 << 0,
        Freeze  = 1 << 1,
        AirBind = 1 << 2,

        /// <summary>제 의지로 움직이는 것을 막는 것 전부. 지금은 셋 다지만, 앞으로 붙을
        /// 둔화 · 침묵처럼 <b>행동은 되는</b> 디버프를 여기서 걸러 내기 위해 따로 둔다.</summary>
        ActionBlocking = Stun | Freeze | AirBind,

        /// <summary>모든 디버프. "버프는 남기고 디버프만 지운다"고 말할 때 쓰는 마스크.</summary>
        All = Stun | Freeze | AirBind,
    }

    /// <summary>진영. 히트박스가 아군을 때리지 않도록 거르는 기준.</summary>
    public enum Faction
    {
        Ally,
        Enemy,
    }
}
