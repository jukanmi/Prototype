using System.Collections;

namespace Prototype
{
    /// <summary>
    /// 스킬이 나가기 직전에 끼어드는 인트로 연출.
    /// <see cref="ComboExecutor"/>는 이 계약만 알고 UI 구현은 모른다 —
    /// 미배선(null)이면 컷인 없이 곧바로 스킬로 간다.
    /// </summary>
    public interface ISkillCutin
    {
        /// <summary>
        /// 컷인을 한 번 재생한다. Executor가 반환된 열거자를 소진할 때까지 슬롯이 대기한다.
        /// <b>호출 즉시</b> 연출을 시작해야 한다(시간 정지 · 패널 표시) —
        /// 반환값을 펌프하지 않는 호출자도 상태 변화를 관측할 수 있어야 하기 때문.
        /// </summary>
        IEnumerator Play(Ally caster, SkillData data);

        /// <summary>
        /// 재생을 즉시 중단하고 정지시킨 시간을 되돌린다.
        /// <see cref="ComboExecutor.Abort"/>가 부른다 — StopCoroutine으로 잘린 코루틴은
        /// finally가 돌지 않아 <see cref="TimeControl.Scale"/>이 0에 묶이기 때문.
        /// </summary>
        void Cancel();
    }
}
