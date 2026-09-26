// SkillState — 타격을 실제로 내보낸다. 근접 상자 · 부채꼴 · 장판 · 투사체.

using System;
using UnityEngine;

namespace Prototype
{
    public partial class SkillState
    {
        private void FireNextHit()
        {
            if (nextHitIndex >= data.hitDataList.Count) return;

            HitData hit = ModifyHit(data.hitDataList[nextHitIndex]);

            // 카드가 실어 보낸 배율(황금 카드)을 <b>ModifyHit 다음에</b> 곱한다.
            // 차징은 ModifyHit에서 붙으므로 이 순서가 곧 "차징 × 황금"이다.
            // 넉백은 건드리지 않는다 — 황금은 데미지만 키우는 물건이라, 밀어내는 힘까지
            // 커지면 콤보 연계 거리가 카드 등급에 따라 달라져 체인이 무너진다.
            hit.damageData.damage *= ctx.DamageScale;

            bool last = nextHitIndex == data.hitDataList.Count - 1;

            // 시전자 이동 (일섬 파고들기, 백스탭, 돌진 등).
            // stepDistance가 지정되어 있으면 그 거리만큼 이동하고,
            // 레거시 호환으로 stepDistance == 0이어도 fixedOrigin 첫 타면 기존처럼 판정 깊이만큼 파고든다.
            float stepDist = hit.stepDistance;
            if (Mathf.Approximately(stepDist, 0f) && hit.fixedOrigin && nextHitIndex == 0)
                stepDist = CastRange(in hit).z;

            if (Mathf.Abs(stepDist) > 0.001f && ctx.caster != null)
            {
                Physics phys = ctx.CasterPhysics;
                if (phys != null)
                {
                    Vector3 baseDir = phys.Facing;
                    if (ctx.target != null && ctx.target.Physics != null)
                    {
                        Vector3 toTarget = ctx.target.Physics.GroundPosition - phys.GroundPosition;
                        toTarget.y = 0f;
                        if (toTarget.sqrMagnitude > 0.0001f)
                            baseDir = toTarget.normalized;
                    }

                    // 양수(+)면 전방/대상 방향, 음수(-)면 후방(백스탭)
                    Vector3 moveDir = stepDist > 0f ? baseDir : -baseDir;
                    float targetDistance = Mathf.Abs(stepDist);

                    // 밀어내는 게 아니라 건너뛴다. 충격량으로 파고들면 솔버가 시전자 몸으로
                    // 적을 같이 밀어버린다 — 베고 지나가는 그림이 아니라 밀고 가는 그림이 된다.
                    float toWall = WallFinder.DistanceToWall(phys.GroundPosition, moveDir, phys.WallMask);
                    float step = Mathf.Min(targetDistance, Mathf.Max(0f, toWall - ctx.caster.HurtboxSize.z * 0.5f));

                    // 전방 파고들기면 타겟을 보고, 후방 백스탭이면 시선(전방 타겟)을 유지한 채 뒤로 빠진다.
                    if (stepDist > 0f)
                        phys.Face(baseDir);

                    phys.Teleport(phys.GroundPosition + moveDir * step, phys.Height);
                    phys.ResetInertia();

                    BattleLog.Log(LogCategory.Skill,
                        $"  └ {data.skillName} {(stepDist > 0f ? "파고들기" : "백스탭")} {step:0.##} 유닛 (목표 {targetDistance:0.##})", ctx.caster);
                }
            }

            if (data.IsRanged) LaunchProjectile(in hit);
            else if (IsAreaCaster) FireArea(in hit);
            else FireMelee(in hit);

            // 돌진은 판정과 <b>같은 프레임</b>에 나가야 베고 지나가는 그림이 된다.
            // 타격보다 먼저 내면 선딜 동안 대상을 지나쳐 히트박스가 허공에서 열린다.
            if (last) ApplyLastHitEffects();

            BattleLog.Log(LogCategory.Skill,
                $"  └ {data.skillName} {nextHitIndex + 1}/{data.hitDataList.Count}타 발동 (t={timer:0.##}s)", ctx.caster);

            nextHitIndex++;
            nextHitTime = data.HitTime(nextHitIndex);
        }

        /// <summary>
        /// 장판 — 시전 기준점 반경 안의 적을 한 번에 때린다.
        /// 반경은 <see cref="SkillData.radius"/>를 그대로 쓴다 —
        /// 조준 링 · 헛침 경고 · 시전 연출이 전부 같은 값을 그리고 있으므로
        /// 판정만 다른 기준을 쓰면 "보이는 곳과 맞는 곳"이 어긋난다.
        /// </summary>
        private void FireArea(in HitData hit)
        {
            Combat attacker = ctx.CasterCombat;
            if (attacker == null) return;

            SkillVfx style = data.vfx.AsSkill();
            float r = BlastRadius(in hit);

            // 띄운 대상은 발밑이 아니라 그 고도에서 터뜨린다. 발밑 구는 공중의 몸에 안 닿아
            // 공중 적을 노리는 장판(사슬 속박)이 정확히 고른 그 적을 헛친다. 지상 대상은 고도 0이라 그대로다.
            Vector3 center = ctx.Origin;
            if (ctx.target != null && ctx.target.Physics != null)
                center.y += ctx.target.Physics.Height;

            int hits = EffectUtil.AreaStrike(center, r, attacker, in hit, in style);

            if (hits == 0)
                BattleLog.Log(LogCategory.Skill,
                    $"  └ {data.skillName} 반경 {r:0.#} 안에 적 없음 — 헛침", ctx.caster);
        }

        /// <summary>근접 — 시전자에게 붙은 히트박스를 켠다. 부채꼴 스킬은 히트박스를 안 쓴다.</summary>
        private void FireMelee(in HitData hit)
        {
            if (hit.IsCone) { FireCone(in hit); return; }

            // impactOffset이 지정되어 있으면 시전자 앞이 아닌 대상 적 위치를 중심으로 BoxStrike를 친다.
            if (hit.HasTargetOrigin && ctx.target != null)
            {
                Combat attacker = ctx.CasterCombat;
                Physics phys = ctx.CasterPhysics;
                if (attacker != null && phys != null)
                {
                    Vector3 range = CastRange(in hit);
                    if (range == Vector3.zero && ctx.caster != null)
                        range = ctx.caster.HurtboxSize;
                    Vector3 center = GetTargetImpactCenter(in hit);
                    SkillVfx targetStyle = data.vfx.AsSkill();
                    EffectUtil.BoxStrike(center, range, phys.Facing, attacker, in hit, in targetStyle);
                    return;
                }
            }

            // 고정 궤적 타격 — 시전자가 돌진으로 지나가도 시전 시작점 궤적에 남아 공간을 벤다.
            if (hit.fixedOrigin)
            {
                Combat attacker = ctx.CasterCombat;
                Physics phys = ctx.CasterPhysics;
                if (attacker != null && phys != null)
                {
                    Vector3 range = CastRange(in hit);
                    Vector3 center = castOrigin + phys.Facing * (range.z * 0.5f);
                    SkillVfx fixedStyle = data.vfx.AsSkill();
                    EffectUtil.BoxStrike(center, range, phys.Facing, attacker, in hit, in fixedStyle);
                    return;
                }
            }

            // 타격마다 다시 맞춘다. 시전 시작에 한 번만 올려 두면 시전자는 그대로 낙하하는데
            // 대상은 부양(airHitLift)으로 떠 있어서, 다단히트 뒤쪽 타가 아래에서 헛돈다.
            MatchTargetHeight();

            Attack box = ctx.caster != null ? ctx.caster.SkillAttack : null;
            if (box == null) return;

            if (box.Attacker == null && ctx.caster != null)
                box.Attacker = ctx.caster.Combat;

            // 히트박스는 스킬끼리 공유된다(SkillAttack). 켤 때마다 이번 스킬 색과 범위를 다시 실어 준다.
            // AsSkill()로 스킬 표시를 같이 넘겨야 평타보다 크게 그려진다.
            SkillVfx style = data.vfx.AsSkill();
            box.Begin(in hit, in style, CastRange(in hit));
        }

        /// <summary>
        /// 근접 — <b>전방 부채꼴</b>. 콜라이더로는 만들 수 없는 모양이라 직접 훑는다.
        /// 반지름은 범위 배수의 x를 쓴다(SkillData.castConeAngle 주석과 같은 규약).
        /// </summary>
        private void FireCone(in HitData hit)
        {
            Physics phys = ctx.CasterPhysics;
            Combat attacker = ctx.CasterCombat;
            if (phys == null || attacker == null) return;

            Vector3 origin = phys.GroundPosition;
            float radius = ConeRadius(in hit);

            // 모으기 기준점을 먼저 실어 둔다 — 넉백 방향과 Z 정렬이 전부 이 점을 본다.
            HitData swing = hit.pullAnchorRatio > 0f
                ? hit.WithOrigin(origin + phys.Facing.normalized * (radius * hit.pullAnchorRatio))
                : hit;

            SkillVfx style = data.vfx.AsSkill();
            int hits = EffectUtil.ConeStrike(origin, phys.Facing, radius, hit.castConeAngle,
                                             attacker, in swing, in style);

            if (hits == 0)
                BattleLog.Log(LogCategory.Skill,
                    $"  └ {data.skillName} 부채꼴 반지름 {radius:0.#} · {hit.castConeAngle:0}도 안에 적 없음 — 헛침",
                    ctx.caster);
        }

        /// <summary>부채꼴 반지름. 범위 배수의 x를 시전자 피격 가로폭에 곱한다.</summary>
        private float ConeRadius(in HitData hit)
        {
            if (ctx.caster == null) return 0f;
            return ctx.caster.HurtboxSize.x * hit.castRangeScale.x;
        }

        /// <summary>
        /// 이 타의 근접 히트박스 크기. 기획서는 범위를 <b>시전자 피격 범위의 배수</b>로 적으므로
        /// 여기서 실제 유닛으로 편다. 범위를 안 적은 타는 0 — 프리팹 모양 그대로 간다.
        ///
        /// 범위는 타별이다(연격 — 벨수록 위로 넓어진다).
        /// </summary>
        private Vector3 CastRange(in HitData hit)
        {
            if (ctx.caster == null) return Vector3.zero;

            if (!hit.HasCastRange) return Vector3.zero;

            Vector3 scale = hit.castRangeScale;
            Vector3 hurtbox = ctx.caster.HurtboxSize;

            return new Vector3(hurtbox.x * scale.x, hurtbox.y * scale.y, hurtbox.z * scale.z);
        }

        /// <summary>원거리 — 타격마다 투사체를 하나씩 쏜다.</summary>
        private void LaunchProjectile(in HitData hit)
        {
            Physics phys = ctx.CasterPhysics;
            if (phys == null || ctx.caster == null) return;

            Projectile shot = UnityEngine.Object.Instantiate(data.projectile);

            // 히트박스 레이어를 그대로 물려받아야 충돌 매트릭스가 맞는다.
            int layer = ctx.caster.BasicAttack != null
                ? ctx.caster.BasicAttack.gameObject.layer
                : ctx.caster.gameObject.layer;

            SkillVfx style = data.vfx.AsSkill();

            // 원거리는 이동하지 않으므로 대상이 사거리 밖이면 아무 일도 없이 소멸한다.
            // 반경 기반 헛침 경고로는 안 잡히는 경우라 여기서 남긴다.
            // 도착 폭발은 사거리가 곧 대상까지 거리라 이 경고가 거짓말이 된다 — 건너뛴다.
            if (ctx.target != null && !data.detonateOnArrival)
            {
                Vector3 gap = ctx.target.Physics.GroundPosition - phys.GroundPosition;
                gap.y = 0f;

                if (gap.magnitude > data.projectileRange)
                    BattleLog.Warn(LogCategory.Skill,
                        $"  └ {data.skillName} 사거리 밖 — 대상까지 {gap.magnitude:0.#} > 사거리 {data.projectileRange:0.#}. 투사체가 도중에 사라진다",
                        ctx.caster);
            }

            Vector3 dir = AimDirection(phys);
            float range = data.projectileRange;
            float height = shot.AimHeight(phys, ctx.target?.Physics);

            // 도착 폭발은 탄착점이 곧 사거리 끝 — 투사체가 시전자 자리에서 출발하므로 거리를 그대로 넘긴다.
            // 대상이 없으면 그냥 사거리 끝에서 터진다.
            if (data.detonateOnArrival && TryGetArrivalPoint(in hit, out Vector3 aim))
            {
                dir = aim - phys.GroundPosition;
                dir.y = 0f;
                range = dir.magnitude;
                height = Mathf.Max(0f, aim.y);
            }

            shot.Launch(ctx.caster.Combat, in hit,
                        phys.GroundPosition, dir,
                        data.projectileSpeed, range, data.projectilePierce,
                        phys.WallMask, layer, in style, height, BlastRadius(in hit),
                        data.detonateOnArrival);
        }

        /// <summary>
        /// 모든 타격(투사체 도착 폭발 · 대상 기준 히트스캔 · 장판)의 공통 착탄/타격 위치.
        /// 대상 정중앙 + 타별 impactOffset(대상 크기 배수). 대상이 없으면 ctx.Origin.
        /// </summary>
        private Vector3 GetImpactPoint(in HitData hit)
        {
            if (ctx.target != null && ctx.target.Physics != null)
            {
                Vector3 size = ctx.target.HurtboxSize;
                Vector3 center = ctx.target.Physics.GroundPosition
                    + Vector3.up * (ctx.target.Physics.Height + size.y * 0.5f);

                return center + Vector3.Scale(hit.impactOffset, size);
            }

            return ctx.Origin;
        }

        /// <summary>
        /// 대상 위치 기준 타격(BoxStrike)의 판정 중심.
        /// 대상 위치 + 타별 impactOffset(대상 크기 배수).
        /// BoxStrike의 높이 판정(GroundPosition.y + Height * 0.5f)에 맞춘다.
        /// </summary>
        private Vector3 GetTargetImpactCenter(in HitData hit)
        {
            if (ctx.target != null && ctx.target.Physics != null)
            {
                Vector3 size = ctx.target.HurtboxSize;
                Vector3 center = ctx.target.Physics.GroundPosition
                    + Vector3.up * (ctx.target.Physics.Height * 0.5f);

                return center + Vector3.Scale(hit.impactOffset, size);
            }

            return ctx.Origin;
        }

        /// <summary>
        /// 도착 폭발의 탄착점 — 대상 히트박스 정중앙 + 타별 오프셋(히트박스 배수).
        /// 발사와 프리뷰가 같은 점을 봐야 "표시한 자리에서 터진다"가 성립한다.
        /// </summary>
        private bool TryGetArrivalPoint(in HitData hit, out Vector3 aim)
        {
            aim = default;
            if (ctx.target == null || ctx.target.Physics == null) return false;

            aim = GetImpactPoint(in hit);
            return true;
        }

        /// <summary>
        /// 발사 높이. 대상이 떠 있으면 그 높이로 쏜다.
        ///
        /// 투사체는 고정 높이(0.6)로 바닥을 긁고 지나간다. 적 몸통 캡슐이 높이 1이라
        /// 띄운 뒤에는 겹치는 구간이 사라져 <b>공중 콤보의 마무리가 전부 빗나갔다</b> —
        /// 아처 체인(그물사격 → 상승 화살 → 연속 사격 → 강력 사격)이 시동 직후 끊기던 원인.
        ///
        /// 지상 대상에는 음수를 돌려 프리팹 기본 높이를 그대로 쓴다.
        /// </summary>
        /// <summary>
        /// 발사 방향. 원거리는 제자리에서 쏘므로 <b>대상을 우선</b> 겨눈다 —
        /// 찍은 좌표를 그대로 쏘면 그 사이에 적이 움직인 만큼 빗나간다.
        /// </summary>
        private Vector3 AimDirection(Physics phys)
        {
            if (ctx.targetInfo.type == TargetingType.Direction)
                return ctx.targetInfo.direction;

            Vector3 from = phys.GroundPosition;

            if (ctx.target != null)
                return ctx.target.Physics.GroundPosition - from;

            if (ctx.targetInfo.type == TargetingType.GroundPoint)
                return ctx.targetInfo.point - from;

            return phys.Facing;
        }
    }
}
