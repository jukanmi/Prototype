using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 아군의 평타. <b>연타다.</b>
    ///
    /// 단계 표가 비어 있으면 단발로 떨어진다 — 아트가 아직 없는 동료도 그대로 돌아야 한다.
    /// 이어치기는 유저가 모는 몸에서만 일어난다: <see cref="AttackState"/>가 선입력
    /// (<see cref="AttackInputBuffer"/>)을 보고 넘어가는데, 그 버퍼를 채우는 건
    /// <see cref="PlayerPilot"/> 하나뿐이기 때문이다.
    /// </summary>
    [AddComponentMenu("Prototype/평타 - 아군 (연타)")]
    public sealed class AllyBasicAttack : BasicAttackProfile
    {
        [Tooltip("평타 연타 단계. 비워 두면 단발 평타 그대로다.\n\n" +
                 "각 칸의 타이밍이 0이면 위의 기본 평타 값으로 떨어진다.\n" +
                 "반응(상태 · 넉백 · 띄우기) 칸은 overrideReaction 을 켠 칸에서만 산다.")]
        [SerializeField] private BasicAttackStage[] stages = new BasicAttackStage[0];

        public override int StageCount => stages != null && stages.Length > 0 ? stages.Length : 1;

        public override AnimationClip ClipFor(int stage) => Has(stage) ? stages[stage].clip : null;

        public override BasicAttackTiming TimingFor(int stage)
        {
            if (!Has(stage)) return base.TimingFor(stage);

            return BasicComboRules.ResolveTiming(in stages[stage], Windup, ActiveEnd, Total);
        }

        /// <summary>
        /// 단계를 갈아 끼운다. <c>null</c>이나 빈 배열은 무시한다 —
        /// 표에 안 적었다고 프리팹의 연타가 사라지면 안 된다.
        ///
        /// <b><see cref="Entity.Awake"/>보다 먼저</b> 불러야 안전하다.
        /// <see cref="EntityAnimator"/>가 Awake에서 1타 클립을 잡아 두고
        /// 그 위에 오버라이드를 씌우기 때문이다.
        /// </summary>
        public override void ConfigureCombo(BasicAttackStage[] source)
        {
            if (source == null || source.Length == 0) return;
            stages = source;
        }

        protected override HitData ApplyStage(in HitData basic, int stage)
            => Has(stage) ? BasicComboRules.BuildStageHit(in basic, in stages[stage]) : basic;

        private bool Has(int stage) => stages != null && stage >= 0 && stage < stages.Length;
    }
}
