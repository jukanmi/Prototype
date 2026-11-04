// 스킬 효과 — 인터페이스 · 구현 모음 · 실행기.
// 효과를 하나 추가하면 셋을 같이 봐야 한다.

using System.Collections.Generic;
using System;
using UnityEngine;

namespace Prototype
{
    // ══ ISkillEffect ═══════════════════════════════════════════

    /// <summary>
    /// 스킬의 부가 효과 한 조각.
    /// 스킬 종류를 switch로 분기하는 대신 이 인터페이스의 <b>조합</b>으로 24종을 커버한다.
    /// 새 스킬 = SO 에셋 1개 추가. 기존 코드 수정 0.
    /// </summary>
    public interface ISkillEffect
    {
        void Apply(in SkillContext ctx);
    }

    /// <summary>
    /// 시전 순간이 아니라 <b>마지막 타격에</b> 도는 효과.
    ///
    /// 기본은 시전 순간(<c>SkillState.Enter</c>)이다. 그런데 시전자를 움직이는 효과는
    /// 그 시점에 돌면 안 된다 — 선딜 동안 먼저 날아가 버려서, 정작 히트박스가 열릴 때는
    /// 이미 대상을 지나쳐 있다. 돌진 베기가 그래서 헛쳤다(선딜 0.12s 동안 약 2유닛 이동).
    ///
    /// 타격과 같이 내면 <b>베고 지나가는</b> 그림이 되고 판정도 붙는다.
    /// 이 인터페이스만 달면 되고 <see cref="ISkillEffect"/> 계약은 그대로다 —
    /// 나머지 효과는 한 줄도 안 바뀐다.
    /// </summary>
    public interface ILastHitEffect : ISkillEffect
    {
    }

    // ══ Effects ═══════════════════════════════════════════

    /// <summary>
    /// 모으기. <see cref="SkillContext.Origin"/>을 중심으로 반경 내 적을 끌어당긴다.
    /// 적을 개별 지정하지 않는다 — 좌표 하나만 받는다.
    /// </summary>
    [Serializable]
    public class PullEffect : ISkillEffect
    {
        [SerializeField] private float radius = 4f;
        [Tooltip("최대로 끌어올 거리(유닛).")]
        [SerializeField] private float pullDistance = 1.5f;
        [SerializeField] private float damage = 5f;
        [SerializeField] private float hitStun = 0.4f;

        public void Apply(in SkillContext ctx)
        {
            Combat caster = ctx.CasterCombat;
            if (caster == null) return;

            // 중심은 조준한 좌표. 원거리 직업은 실시간에 텔포하지 않으므로
            // 시전자 위치로 잡으면 범위가 발밑에 생긴다.
            Vector3 center = ctx.Origin;
            float r = radius;   // 차징으로 커진 만큼 넓어진다

            var hit = new HitData
            {
                damageData = new DamageData(damage),
                targetState = CombatState.Neutral,
                nextState = CombatState.LightHit,
                mode = KnockbackMode.TowardCaster,
                pushDistance = pullDistance,
                hitStunDuration = hitStun,
                // 벨트스크롤에서 Z가 어긋나면 후속 연계가 전부 빗나간다.
                snapZ = true,
            // 시전자가 아니라 중심으로 끌어당긴다 — 원거리는 멀리 서 있으므로
            // 시전자 기준이면 적이 찍은 자리가 아니라 궁수 발밑으로 모인다.
            }.WithOrigin(center);

            // 실제로 들어간 것만 센다. OverlapCombats가 돌려주는 건 "반경 안에 몇이 있었나"라
            // 무적으로 흘린 적까지 흡입한 것으로 보고된다 — 아래 헛침 경고가 그때 침묵한다.
            int caught = 0;
            EffectUtil.OverlapCombats(center, r, caster, c => { if (caster.Attack(c, in hit)) caught++; });

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
        [Tooltip("띄울 높이(유닛).")]
        [SerializeField] private float airborneHeight = 2.4f;
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
                airborneHeight = airborneHeight,
                hitStunDuration = hitStun,
            }.WithOrigin(center);

            EffectUtil.OverlapCombats(center, radius, caster,
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
                var taunted = c.GetComponent<ITauntable>();
                if (taunted != null) taunted.SetTarget(casterEntity);
            });
        }
    }

    /// <summary>
    /// 차징 · 돌진. 시전자를 지정 방향으로 밀어낸다.
    ///
    /// <see cref="ILastHitEffect"/>라 <b>마지막 타격과 같이</b> 나간다. 시전 순간에 내면
    /// 선딜 동안 먼저 달려가 히트박스가 열릴 때는 대상을 지나쳐 있다.
    /// </summary>
    [Serializable]
    public class ChargeEffect : ILastHitEffect
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
