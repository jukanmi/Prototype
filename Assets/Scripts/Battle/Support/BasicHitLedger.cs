using System.Collections.Generic;

namespace Prototype
{
    /// <summary>
    /// 평타 한 대당 보상을 한 번만 내주는 장부. 순수 규칙이라 씬 없이 테스트한다.
    ///
    /// 한 대가 적 여럿을 베거나 관통하면 적중 신호가 같은 번호(<see cref="HitData.basicSwing"/>)로
    /// 여러 번 온다. 그중 첫 번째만 통과시킨다.
    ///
    /// <b>때린 몸마다 마지막 번호 하나만 기억한다.</b> 한 몸의 평타는 한 대씩 차례로 나가므로
    /// 새 번호가 오면 앞 대는 끝난 것이다 — 번호를 전부 쌓아 둘 필요가 없다.
    /// 전역 하나로 두면 두 동료의 평타가 섞여 들어올 때 앞 대의 두 번째 적중이 다시 보상을 받는다.
    /// </summary>
    public sealed class BasicHitLedger
    {
        private readonly Dictionary<object, int> lastClaimed = new Dictionary<object, int>();

        /// <summary>이 몸의 이 평타가 처음 보상을 청구하면 true. 평타 번호 0(평타 아님)은 늘 거절한다.</summary>
        public bool TryClaim(object attacker, int swing)
        {
            if (attacker == null || swing == 0) return false;

            if (lastClaimed.TryGetValue(attacker, out int last) && last == swing) return false;

            lastClaimed[attacker] = swing;
            return true;
        }
    }
}
