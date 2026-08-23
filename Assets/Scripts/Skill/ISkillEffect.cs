namespace Prototype
{
    /// <summary>
    /// 스킬의 부가 효과 한 조각.
    /// 스킬 종류를 switch로 분기하는 대신 이 인터페이스의 <b>조합</b>으로 24종을 커버한다.
    /// 새 스킬 = SO 에셋 1개 추가. 기존 코드 수정 0.
    /// </summary>
    public interface ISkillEffect
    {
        void Apply(in SkillContext ctx);
    }

    /// <summary>
    /// 시전 순간이 아니라 <b>마지막 타격에</b> 도는 효과.
    ///
    /// 기본은 시전 순간(<c>SkillState.Enter</c>)이다. 그런데 시전자를 움직이는 효과는
    /// 그 시점에 돌면 안 된다 — 선딜 동안 먼저 날아가 버려서, 정작 히트박스가 열릴 때는
    /// 이미 대상을 지나쳐 있다. 돌진 베기가 그래서 헛쳤다(선딜 0.12s 동안 약 2유닛 이동).
    ///
    /// 타격과 같이 내면 <b>베고 지나가는</b> 그림이 되고 판정도 붙는다.
    /// 이 인터페이스만 달면 되고 <see cref="ISkillEffect"/> 계약은 그대로다 —
    /// 나머지 효과는 한 줄도 안 바뀐다.
    /// </summary>
    public interface ILastHitEffect : ISkillEffect
    {
    }
}
