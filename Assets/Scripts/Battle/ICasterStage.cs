namespace Prototype
{
    /// <summary>
    /// 콤보가 도는 동안 <b>지금 시전하는 한 명만</b> 화면에 세우는 무대.
    ///
    /// <see cref="ComboExecutor"/>는 이 계약만 알고 태그 시스템은 모른다 —
    /// 미배선(null)이면 무대 연출 없이 예전처럼 그대로 시전한다.
    /// <see cref="ISkillCutin"/>과 같은 관용구다.
    ///
    /// <b>자리는 무대가 정한다.</b> 시전자는 조작 캐릭터가 서 있던 자리에 등장한다 —
    /// 그래야 <see cref="SkillState"/>의 접근·조준이 플레이어가 보던 자리에서 뻗어 나간다.
    /// </summary>
    public interface ICasterStage
    {
        /// <summary>
        /// 이 시전자를 무대에 올린다.
        ///
        /// <b>내리는 일까지 여기서 한다</b> — 앞 슬롯에서 <see cref="Exit"/>를 받아 둔 몸은
        /// 이 시점에 내려간다. 내리기를 여기까지 미루는 이유는 같은 시전자가 연속 슬롯에
        /// 나올 때 한 프레임 깜빡이는 것을 막기 위해서다. 그 한 프레임에
        /// <c>OnDisable → ReleaseBody</c>가 돌아 스킬 상태가 통째로 날아간다.
        ///
        /// 이미 무대에 있는 몸은 자리를 다시 잡지 않는다 — 돌진으로 전진한 시전자가
        /// 다음 슬롯에서 제자리로 튕겨 돌아가면 안 된다.
        /// </summary>
        void Enter(Ally caster);

        /// <summary>
        /// 지금 시전자가 아직 무대에 <b>도착하지 않았는가</b>.
        ///
        /// <see cref="ComboExecutor"/>가 컷인 뒤에 이 값이 내려갈 때까지 기다린다 —
        /// 시전자가 화면 밖에서 날아오는 중에 스킬을 걸면, 접근·조준이 계산되는 기준점이
        /// 아직 화면 밖이라 스킬이 통째로 엉뚱한 데서 터진다.
        ///
        /// 무대 연출이 없는 구현은 <b>언제나 false</b>를 돌려주면 된다.
        /// 그러면 예전처럼 컷인이 끝나는 즉시 시전으로 넘어간다.
        /// </summary>
        bool IsEntering { get; }

        /// <summary>
        /// 이 시전자는 할 일이 끝났다고 알린다. <b>즉시 내리지는 않는다</b> —
        /// 실제로 내려가는 시점은 다음 <see cref="Enter"/>나 <see cref="Clear"/>다.
        /// </summary>
        void Exit(Ally caster);

        /// <summary>
        /// 무대를 비우고 조작 캐릭터를 되돌린다. 콤보 종료와 중단 양쪽에서 불린다.
        /// </summary>
        void Clear();
    }
}
