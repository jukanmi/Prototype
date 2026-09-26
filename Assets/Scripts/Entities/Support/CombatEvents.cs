using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 누가 누구를 때렸든 한 번씩 울리는 <b>전투 전역 알림</b>.
    /// 시전자를 모르는 관전자(사운드 · HUD · 게이지 보상)가 여기에 붙는다.
    ///
    /// 예전에는 <see cref="Combat"/>의 static 이벤트였다. 그러면 소리 하나 내려는 쪽도
    /// 타격 판정 클래스 전체에 묶였다 — 알림판과 판정을 갈라 둔다.
    /// 쏘는 곳은 <see cref="Combat"/> 하나뿐이다.
    /// </summary>
    public static class CombatEvents
    {
        /// <summary>적중 한 번. 인자는 (때린 쪽, 맞은 쪽). 수치는 <see cref="OnAnyDamageDealt"/>가 준다.</summary>
        public static event Action<Combat, Combat> OnAnyHitLanded;

        /// <summary>
        /// 대시 패링 성공. 인자는 (막은 쪽, 패링당한 공격자) 순이다.
        /// 게이지 보상 · 연출 · 사운드가 전부 여기 붙는다 — Combat은 "막았다"만 알린다.
        /// 공격자를 모르는 타격(장판 등)을 막으면 두 번째 인자가 null이다.
        /// </summary>
        public static event Action<Combat, Combat> OnParried;

        /// <summary>
        /// 평타가 <b>실제로 적중했다</b>. 인자는 (때린 쪽, 맞은 쪽, 평타 번호) 순이다.
        /// 무적 · 패링으로 흘린 타격은 오지 않는다. 한 대가 여럿을 맞히면 같은 번호로 여러 번 온다 —
        /// 한 대당 한 번으로 세는 건 받는 쪽 몫이다(<see cref="HitData.basicSwing"/>).
        /// </summary>
        public static event Action<Combat, Combat, int> OnAnyBasicHitLanded;

        /// <summary>
        /// 피해가 <b>실제로</b> 들어갔다. 인자는 (때린 쪽, 맞은 쪽, 깎인 양) 순이다.
        ///
        /// <see cref="OnAnyHitLanded"/>와 나눠 둔 이유: 그쪽은 수치를 주지 않고, 이쪽은
        /// 방어력 · 보호막 · 피해감소가 전부 적용된 <b>최종 수치</b>를 준다.
        /// 무적 · 패링으로 흘린 타격이나 0딜 반격은 여기까지 오지 않는다.
        /// </summary>
        public static event Action<Combat, Combat, float> OnAnyDamageDealt;

        /// <summary>
        /// Enter Play Mode Options가 Domain Reload를 끄고 있어 static이 살아남는다.
        /// 리셋하지 않으면 지난 세션의 파괴된 구독자가 계속 호출된다(BattleRegistry와 같은 이유).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnAnyHitLanded = null;
            OnParried = null;
            OnAnyBasicHitLanded = null;
            OnAnyDamageDealt = null;
        }

        // ── 쏘는 쪽 (Combat 전용) ─────────────────────────

        internal static void RaiseHitLanded(Combat attacker, Combat victim) => OnAnyHitLanded?.Invoke(attacker, victim);

        internal static void RaiseParried(Combat defender, Combat attacker) => OnParried?.Invoke(defender, attacker);

        internal static void RaiseBasicHitLanded(Combat attacker, Combat victim, int swing)
            => OnAnyBasicHitLanded?.Invoke(attacker, victim, swing);

        /// <summary>구독자가 없으면 계산부터 건너뛸 수 있게 묻는 자리.</summary>
        internal static bool HasDamageListeners => OnAnyDamageDealt != null;

        internal static void RaiseDamageDealt(Combat attacker, Combat victim, float dealt)
            => OnAnyDamageDealt?.Invoke(attacker, victim, dealt);
    }
}
