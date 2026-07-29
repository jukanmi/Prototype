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
}
