using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>광역 판정이 필요한 효과의 공통 헬퍼.</summary>
    public static class EffectUtil
    {
        private static readonly Collider[] Buffer = new Collider[64];
        private static readonly HashSet<Combat> Seen = new HashSet<Combat>();

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
    /// 모으기. 찍은 지점으로 텔포한 <b>뒤의 시전자 위치</b>를 중심으로 반경 내 적을 끌어당긴다.
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

            // 중심은 텔포가 끝난 시점의 시전자 위치.
            Vector3 center = caster.transform.position;

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
            };

            int caught = EffectUtil.OverlapCombats(center, radius, caster, c => caster.Attack(c, in hit));

            BattleLog.Log(LogCategory.Skill,
                $"  └ PullEffect: 중심 {center} 반경 {radius:0.#} → {caught}마리 흡입", caster);
            if (caught == 0)
                BattleLog.Warn(LogCategory.Skill, "  └ PullEffect 헛침 — 반경 안에 적이 없다", caster);
        }
    }

    /// <summary>띄우기. 반경 내 적을 공중으로 올린다. 시동기의 실체.</summary>
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

            var hit = new HitData
            {
                damageData = new DamageData(damage),
                nextState = CombatState.AerialHit,
                mode = KnockbackMode.Up,
                launchForce = launchForce,
                hitStunDuration = hitStun,
            };

            EffectUtil.OverlapCombats(caster.transform.position, radius, caster, c => caster.Attack(c, in hit));
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
            EffectRunner.Instance.Schedule(duration, () => caster.ClearShield());
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
            EffectRunner.Instance.Schedule(duration, () => caster.SetDamageCut(0f));
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

            EffectUtil.OverlapCombats(caster.transform.position, radius, caster, c =>
            {
                var ec = c.GetComponent<EnemyControl>();
                if (ec != null) ec.SetTarget(casterEntity);
            });
        }
    }

    /// <summary>둔화. 반경 내 적의 이동속도를 일정 시간 깎는다.</summary>
    [Serializable]
    public class SlowEffect : ISkillEffect
    {
        [SerializeField] private float radius = 4f;
        [Range(0.1f, 1f)][SerializeField] private float multiplier = 0.5f;
        [SerializeField] private float duration = 3f;

        public void Apply(in SkillContext ctx)
        {
            Combat caster = ctx.CasterCombat;
            if (caster == null) return;

            EffectUtil.OverlapCombats(caster.transform.position, radius, caster, c =>
            {
                Entity e = c.Owner;
                if (e == null) return;

                float original = e.Stats.GetValue(StatType.MoveSpeed, 6f);
                e.Stats.Set(StatType.MoveSpeed, original * multiplier);
                EffectRunner.Instance.Schedule(duration, () => e.Stats.Set(StatType.MoveSpeed, original));
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

            Vector3 dir = phys.Facing;
            if (ctx.targetInfo.type == TargetingType.Direction && ctx.targetInfo.direction.sqrMagnitude > 0.0001f)
                dir = ctx.targetInfo.direction;
            else if (ctx.targetInfo.type == TargetingType.EnemyUnit && ctx.targetInfo.unit != null)
                dir = ctx.targetInfo.unit.transform.position - phys.Transform.position;

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
            EffectRunner.Instance.Schedule(duration, () => caster.SetLifesteal(0f));
        }
    }
}
