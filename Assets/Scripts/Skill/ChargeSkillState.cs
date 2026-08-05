using UnityEngine;

namespace Prototype
{
    /// <summary>차징 스킬을 큐 밖에서 붙잡아 두기 위한 최소 계약.</summary>
    public interface IChargeState
    {
        /// <summary>아직 모으는 중인지.</summary>
        bool IsCharging { get; }
        /// <summary>0~1. 예측 UI가 위력을 보여줄 때 쓴다.</summary>
        float ChargeRatio { get; }
        /// <summary>모으기를 끝내고 실제 시전을 시작한다.</summary>
        void Release();
    }

    /// <summary>
    /// 모았다가 나중에 터지는 스킬.
    ///
    /// 불릿타임에서 슬롯 순서가 오면 <b>모으기만 시작하고 큐를 막지 않는다</b>.
    /// 뒤 슬롯이 전부 끝난 뒤 <see cref="Release"/>로 터진다.
    /// 그래서 차징기를 앞에 놓을수록 오래 모이고 세진다 — 슬롯 배치가 곧 위력이다.
    ///
    /// 피격당해도 유지된다(슈퍼아머). 사망만 관통한다.
    /// </summary>
    public class ChargeSkillState : SkillState, IChargeState
    {
        private bool charging = true;
        private float chargeTimer;
        private float lockedRatio;

        public ChargeSkillState(SkillData data, in SkillContext ctx) : base(data, in ctx) { }

        public bool IsCharging => charging;

        /// <summary>0~1. 해제 후에는 터질 때의 값으로 고정된다.</summary>
        public float ChargeRatio => charging
            ? Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, Data.maxChargeTime))
            : lockedRatio;

        /// <summary>모으는 동안은 끝난 게 아니다. Executor가 기다리지 않게 별도로 판단한다.</summary>
        public override bool IsFinished => !charging && base.IsFinished;

        public override void Enter()
        {
            charging = true;
            chargeTimer = 0f;

            // 모으는 동안 제자리에 선다. 실제 시전 준비는 Release에서.
            Physics phys = Context.CasterPhysics;
            if (phys != null)
            {
                phys.Move(Vector3.zero, 0f);
                phys.ResetInertia();
            }

            BattleLog.Log(LogCategory.Skill,
                $"<b>{BattleLog.Name(Context.caster)}</b> 차징 시작: {Data.skillName} " +
                $"(최대 {Data.maxChargeTime:0.##}s → 위력 x{Data.maxChargeDamageMul:0.##})",
                Context.caster);
        }

        public override void Tick(float dt)
        {
            if (!charging)
            {
                base.Tick(dt);
                return;
            }

            // 상한을 넘겨도 더 세지지 않는다. 콤보가 길다고 무한히 강해지면 안 된다.
            chargeTimer = Mathf.Min(chargeTimer + dt, Data.maxChargeTime);
        }

        public void Release()
        {
            if (!charging) return;

            lockedRatio = ChargeRatio;
            charging = false;

            // 커진 범위를 광역 효과가 읽을 수 있게 맥락에 실어 준다.
            Context.radiusScale = Mathf.Lerp(1f, Data.maxChargeRadiusMul, lockedRatio);

            BattleLog.Log(LogCategory.Skill,
                $"<b>{BattleLog.Name(Context.caster)}</b> 차징 해제: {Data.skillName} " +
                $"| {chargeTimer:0.##}s 모음 ({lockedRatio * 100f:0}%) " +
                $"| 위력 x{DamageMultiplier:0.##} 범위 x{Context.RadiusScale:0.##}",
                Context.caster);

            // 여기서부터 평소의 시전 타임라인이 돈다.
            base.Enter();
        }

        private float DamageMultiplier => Mathf.Lerp(1f, Data.maxChargeDamageMul, lockedRatio);

        /// <summary>모은 만큼 데미지와 밀어내는 힘을 키운다. 원본 에셋은 그대로 둔다.</summary>
        protected override HitData ModifyHit(HitData hit)
        {
            float mul = DamageMultiplier;

            hit.damageData.damage *= mul;
            hit.knockbackForce *= mul;
            hit.launchForce *= mul;

            return hit;
        }

        public override void Exit()
        {
            // 모으는 도중 밀려났으면(사망 등) 아무것도 터뜨리지 않는다.
            if (charging)
            {
                charging = false;
                BattleLog.Log(LogCategory.Skill,
                    $"{BattleLog.Name(Context.caster)} 차징 취소: {Data.skillName}", Context.caster);
            }

            base.Exit();
        }
    }
}
