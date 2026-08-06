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
        Skill,
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
    }

    /// <summary>스킬이 요구하는 조준 방식. 불릿타임 중 유저가 직접 지정한다.</summary>
    public enum TargetingType
    {
        None,
        GroundPoint,
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
    }

    /// <summary>진영. 히트박스가 아군을 때리지 않도록 거르는 기준.</summary>
    public enum Faction
    {
        Ally,
        Enemy,
    }

    public enum GameState
    {
        Title,
        InGame,
        Paused,
        Settings,
        Dead,
    }
}
