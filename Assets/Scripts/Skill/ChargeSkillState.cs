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
    /// <b>실시간(U키 단발)은 다르다.</b> 큐가 없어 <see cref="Release"/>를 불러 줄 주체가 없으므로
    /// 머리 위 게이지(<see cref="ChargeGauge"/>)가 가득 차면 스스로 터진다.
    /// 실시간에서 최대 위력이 보장되는 대신, 모으는 시간만큼 발동이 늦는 게 대가다.
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

        /// <summary>
        /// 모으기 시작에서 이미 자리를 잡았다. 터질 때 다시 옮기면 순간이동이 두 번 보이고,
        /// 모으는 내내 서 있던 자리가 무의미해진다.
        /// </summary>
        protected override bool PlaceOnEnter => false;

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

            // 자리부터 잡고 모은다. 터질 때 옮기면 모으는 내내 엉뚱한 곳에 서 있게 된다.
            ResolveTarget();
            PlaceCaster();

            // 모으는 동안은 제자리에 선다. 실제 시전 타임라인은 Release에서 시작한다.
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

            // Release()와 같은 lerp를 모으는 동안 실시간으로 반영한다 — TryGetRangePreview가
            // 그대로 읽으므로 범위 표시가 모을수록 눈에 보이게 커진다. 실제 배율 확정은
            // 여전히 Release()의 lockedRatio다 — 여긴 미리보기용일 뿐.
            Context.radiusScale = Mathf.Lerp(1f, Data.maxChargeRadiusMul, ChargeRatio);

            // 실시간(U키 단발)에는 해제해 줄 주체가 없다 — 큐가 없으니 ComboExecutor도 없다.
            // 머리 위 게이지가 가득 찬 순간이 곧 발동 시점이다. 그대로 두면 만충인 채로
            // 서 있다가 Ally.realtimeSkillTimeout에 잘려 아무것도 안 터진다.
            if (!Context.isBulletTime && IsFullyCharged)
                Release();
        }

        /// <summary>게이지가 가득 찼는지. 실시간 자동 발동의 기준이다.</summary>
        public bool IsFullyCharged => chargeTimer >= Data.maxChargeTime;

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

        /// <summary>
        /// 모은 만큼 데미지와 밀어내는 힘을 키운다. 원본 에셋은 그대로 둔다.
        ///
        /// <b>배율이 필드마다 다르다</b> — 저작 단위가 힘이 아니라 거리 · 높이이기 때문이다.
        /// 밀치기 거리는 충격량에 정비례하므로 <c>mul</c>을 그대로 곱하지만,
        /// 정점 높이는 <c>v²/2g</c>라 속도의 <b>제곱</b>에 비례한다 —
        /// 여기에도 <c>mul</c>만 곱하면 모은 보람이 예전의 제곱근으로 줄어든다.
        /// </summary>
        protected override HitData ModifyHit(HitData hit)
        {
            float mul = DamageMultiplier;

            hit.damageData.damage *= mul;
            hit.pushDistance *= mul;
            hit.airborneHeight *= mul * mul;
            hit.aerialAirborneHeight *= mul * mul;

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
