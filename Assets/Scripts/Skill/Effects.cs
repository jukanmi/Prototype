using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>광역 판정이 필요한 효과의 공통 헬퍼.</summary>
    public static class EffectUtil
    {
        /// <summary>적중 이펙트가 터지는 몸통 높이 · 크기. <see cref="Attack"/>과 같은 값.</summary>
        private const float ImpactHeight = 0.7f;
        private const float ImpactRadius = 0.55f;

        private static readonly Collider[] Buffer = new Collider[64];
        private static readonly HashSet<Combat> Seen = new HashSet<Combat>();

        /// <summary>
        /// 기준점 반경을 한 번에 때린다. <b>스킬 타격의 유일한 광역 경로</b> —
        /// 즉시 터지는 장판도, 투사체가 도착해 터지는 폭발도 전부 여기를 지난다.
        ///
        /// 넉백 방향과 Z 정렬은 시전자가 아니라 <paramref name="center"/> 기준으로 돈다.
        /// 원거리는 멀리 서 있으므로 시전자 기준이면 적이 시전자 발밑으로 끌려간다.
        /// </summary>
        /// <param name="skip">이미 다른 경로로 맞은 대상. 투사체 직격이 여기 들어온다.</param>
        public static int AreaStrike(Vector3 center, float radius, Combat attacker,
                                     in HitData hit, in SkillVfx style, Combat skip = null)
        {
            if (attacker == null || radius <= 0f) return 0;

            // in 파라미터는 람다에 캡처할 수 없으므로 미리 꺼내 둔다.
            HitData blast = hit.WithOrigin(center);
            SkillVfx vfx = style;
            Combat skipped = skip;
            Vector3 flat = new Vector3(center.x, 0f, center.z);

            int hits = 0;

            OverlapCombats(center, radius, attacker, c =>
            {
                if (c == skipped) return;

                attacker.Attack(c, in blast);
                hits++;

                if (c.Physics == null) return;

                // 파편이 중심에서 바깥으로 튀도록 방향을 준다(Attack.EmitImpact와 같은 규칙).
                Vector3 ground = c.Physics.GroundPosition;
                ground.y = 0f;

                BattleVfx.Impact(ground, c.Physics.Height + ImpactHeight,
                                 ground - flat, ImpactRadius, in vfx);
            });

            return hits;
        }

        /// <summary>
        /// 중심 반경 안의 Combat을 모은다. 시전자 본인과 <b>같은 진영</b>은 제외한다.
        /// 진영 필터가 없으면 모으기가 동료까지 빨아들인다 — Attack 히트박스와 같은 기준을 쓴다.
        /// </summary>
        public static int OverlapCombats(Vector3 center, float radius, Combat exclude, Action<Combat> onEach)
        {
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(center, radius, Buffer);
            int hit = 0;

            // 같은 Combat이 콜라이더 여러 개를 갖고 있으면 중복으로 잡힌다.
            Seen.Clear();

            Faction? casterFaction = exclude != null && exclude.Owner != null
                ? exclude.Owner.Faction
                : (Faction?)null;

            for (int i = 0; i < count; i++)
            {
                Combat c = Buffer[i] != null ? Buffer[i].GetComponentInParent<Combat>() : null;
                if (c == null || c == exclude || c.IsDead) continue;
                if (!Seen.Add(c)) continue;

                if (casterFaction.HasValue && c.Owner != null && c.Owner.Faction == casterFaction.Value)
                    continue;

                onEach?.Invoke(c);
                hit++;
            }

            return hit;
        }
    }

    /// <summary>
    /// 모으기. <see cref="SkillContext.Origin"/>을 중심으로 반경 내 적을 끌어당긴다.
    /// 적을 개별 지정하지 않는다 — 좌표 하나만 받는다.
    /// </summary>
    [Serializable]
    public class PullEffect : ISkillEffect
    {
        [SerializeField] private float radius = 4f;
        [SerializeField] private float force = 12f;
        [SerializeField] private float damage = 5f;
        [SerializeField] private float hitStun = 0.4f;

        public void Apply(in SkillContext ctx)
        {
            Combat caster = ctx.CasterCombat;
            if (caster == null) return;

            // 중심은 조준한 좌표. 원거리 직업은 실시간에 텔포하지 않으므로
            // 시전자 위치로 잡으면 범위가 발밑에 생긴다.
            Vector3 center = ctx.Origin;
            float r = radius * ctx.RadiusScale;   // 차징으로 커진 만큼 넓어진다

            var hit = new HitData
            {
                damageData = new DamageData(damage),
                targetState = CombatState.Neutral,
                nextState = CombatState.LightHit,
                mode = KnockbackMode.TowardCaster,
                knockbackForce = force,
                hitStunDuration = hitStun,
                // 벨트스크롤에서 Z가 어긋나면 후속 연계가 전부 빗나간다.
                snapZ = true,
            // 시전자가 아니라 중심으로 끌어당긴다 — 원거리는 멀리 서 있으므로
            // 시전자 기준이면 적이 찍은 자리가 아니라 궁수 발밑으로 모인다.
            }.WithOrigin(center);

            int caught = EffectUtil.OverlapCombats(center, r, caster, c => caster.Attack(c, in hit));

            BattleLog.Log(LogCategory.Skill,
                $"  └ PullEffect: 중심 {center} 반경 {r:0.#} → {caught}마리 흡입", caster);
            if (caught == 0)
                BattleLog.Warn(LogCategory.Skill, "  └ PullEffect 헛침 — 반경 안에 적이 없다", caster);
        }
    }

    /// <summary>띄우기. <see cref="SkillContext.Origin"/> 반경 내 적을 공중으로 올린다. 시동기의 실체.</summary>
    [Serializable]
    public class AirborneEffect : ISkillEffect
    {
        [SerializeField] private float radius = 2.5f;
        [SerializeField] private float launchForce = 12f;
        [SerializeField] private float damage = 8f;
        [SerializeField] private float hitStun = 0.6f;

        public void Apply(in SkillContext ctx)
        {
            Combat caster = ctx.CasterCombat;
            if (caster == null) return;

            Vector3 center = ctx.Origin;

            var hit = new HitData
            {
                damageData = new DamageData(damage),
                nextState = CombatState.AerialHit,
                mode = KnockbackMode.Up,
                launchForce = launchForce,
                hitStunDuration = hitStun,
            }.WithOrigin(center);

            EffectUtil.OverlapCombats(center, radius * ctx.RadiusScale, caster,
                                      c => caster.Attack(c, in hit));
        }
    }

    /// <summary>보호막. 지속시간 동안 최대 체력을 늘려 흡수한다.</summary>
    [Serializable]
    public class ShieldEffect : ISkillEffect
    {
        [SerializeField] private float amount = 30f;
        [SerializeField] private float duration = 5f;

        public void Apply(in SkillContext ctx)
        {
            Combat caster = ctx.CasterCombat;
            if (caster == null) return;

            caster.AddShield(amount);

            // 지속시간을 Combat이 들고 있어야 머리 위 게이지가 남은 시간을 그린다.
            // EffectRunner에 맡기면 걸린 사실이 익명 콜백 안에만 남는다.
            caster.Statuses.Apply(StatusKind.Shield, duration, () => caster.ClearShield());
        }
    }

    /// <summary>피해감소. 지속시간 동안 받는 데미지를 깎는다.</summary>
    [Serializable]
    public class DamageCutEffect : ISkillEffect
    {
        [Range(0f, 1f)][SerializeField] private float ratio = 0.5f;
        [SerializeField] private float duration = 4f;

        public void Apply(in SkillContext ctx)
        {
            Combat caster = ctx.CasterCombat;
            if (caster == null) return;

            caster.SetDamageCut(ratio);
            caster.Statuses.Apply(StatusKind.DamageCut, duration, () => caster.SetDamageCut(0f));
        }
    }

    /// <summary>도발. 반경 내 적의 타겟을 시전자로 고정한다.</summary>
    [Serializable]
    public class TauntEffect : ISkillEffect
    {
        [SerializeField] private float radius = 6f;

        public void Apply(in SkillContext ctx)
        {
            Combat caster = ctx.CasterCombat;
            // in 파라미터는 람다에 캡처할 수 없으므로 미리 꺼내 둔다.
            Entity casterEntity = ctx.caster;
            if (caster == null || casterEntity == null) return;

            EffectUtil.OverlapCombats(ctx.Origin, radius, caster, c =>
            {
                var ec = c.GetComponent<EnemyControl>();
                if (ec != null) ec.SetTarget(casterEntity);
            });
        }
    }

    /// <summary>차징 · 돌진. 시전자를 지정 방향으로 밀어낸다.</summary>
    [Serializable]
    public class ChargeEffect : ISkillEffect
    {
        [SerializeField] private float speed = 20f;

        public void Apply(in SkillContext ctx)
        {
            Physics phys = ctx.CasterPhysics;
            if (phys == null) return;

            // 방향 지정이면 그대로, 아니면 확정된 대상 쪽으로 파고든다.
            Vector3 dir = phys.Facing;
            if (ctx.targetInfo.type == TargetingType.Direction && ctx.targetInfo.direction.sqrMagnitude > 0.0001f)
                dir = ctx.targetInfo.direction;
            else if (ctx.target != null)
                dir = ctx.target.Physics.GroundPosition - phys.GroundPosition;

            phys.Dash(dir, speed);
        }
    }

    /// <summary>흡혈. 지속시간 동안 준 피해의 일부를 회복한다.</summary>
    [Serializable]
    public class LifestealEffect : ISkillEffect
    {
        [Range(0f, 1f)][SerializeField] private float ratio = 0.3f;
        [SerializeField] private float duration = 5f;

        public void Apply(in SkillContext ctx)
        {
            Combat caster = ctx.CasterCombat;
            if (caster == null) return;

            caster.SetLifesteal(ratio);
            caster.Statuses.Apply(StatusKind.Lifesteal, duration, () => caster.SetLifesteal(0f));
        }
    }
}
