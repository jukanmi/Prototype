// SkillState — 누구를 · 어디서 때리나. 대상 확정, 시전 자리 잡기, 범위 미리보기.

using System;
using UnityEngine;

namespace Prototype
{
    public partial class SkillState
    {
        /// <summary>
        /// 지금 이 스킬이 때릴 자리. 선딜부터 후딜까지 계속 true다 — 발동 순간에만 보이면
        /// "어디로 나갈지"가 아니라 "방금 어디였는지"가 된다(<see cref="AttackRangeIndicator"/>가
        /// 적 예고를 그리는 것과 같은 발상, <see cref="IEnemySpecialAction.TryGetRange"/> 참고).
        ///
        /// 원거리(투사체)는 착탄 지점이 시전 위치와 다르므로 그리지 않는다 —
        /// 틀린 자리에 그리는 것보다 안 그리는 게 낫다.
        /// </summary>
        public virtual bool TryGetRangePreview(out AttackRangePreview range)
        {
            range = default;
            if (finished || ctx.caster == null || data.hitDataList.Count == 0)
                return false;

            float progress = Mathf.Clamp01(timer / Mathf.Max(0.01f, EndTime));
            HitData hit = data.hitDataList[Mathf.Clamp(nextHitIndex, 0, data.hitDataList.Count - 1)];

            // 원거리는 착탄 지점이 날아가 봐야 아는 것이라 틀린 자리에 그리느니 안 그린다.
            // 도착 폭발만 예외 — 터질 자리가 쏘기 전에 정해져 있으니 다음 타의 탄착점에 그린다.
            if (data.IsRanged)
            {
                if (!data.detonateOnArrival || !TryGetArrivalPoint(in hit, out Vector3 aim)) return false;

                aim.y = ctx.target.Physics.GroundPosition.y;
                range = AttackRangePreview.FromCircle(aim, BlastRadius(in hit), progress);
                return true;
            }

            if (hit.IsCone)
            {
                Physics phys = ctx.CasterPhysics;
                if (phys == null) return false;

                float radius = ctx.caster.HurtboxSize.x * hit.castRangeScale.x;
                range = AttackRangePreview.FromCone(phys.GroundPosition, phys.Facing, radius,
                                                     hit.castConeAngle, progress);
                return true;
            }

            if (!data.UsesRadius)
            {
                Physics phys = ctx.CasterPhysics;
                if (phys == null) return false;

                if (!hit.HasCastRange) return false;
                Vector3 scale = hit.castRangeScale;

                // Attack.Resize와 같은 규약 — 상자는 몸 앞면(z = size.z * 0.5)에서 시작한다.
                // 다르면 "표시 밖인데 맞았다"가 된다.
                Vector3 size = Vector3.Scale(ctx.caster.HurtboxSize, scale);

                // 대상 위치 기준 타격 (연환파쇄궁 2·3·4타 등):
                // impactOffset이 지정되어 있으면 대상 적 위치를 중심으로 프리뷰 상자를 띄운다.
                if (hit.HasTargetOrigin && ctx.target != null)
                {
                    Vector3 impactCenter = GetTargetImpactCenter(in hit);
                    range = AttackRangePreview.FromBox(impactCenter, phys.Facing,
                                                       Vector3.zero, size, 0f, progress);
                    return true;
                }

                // 고정되는 건 <b>자리</b>뿐이다. 방향은 지금 보는 쪽을 그대로 쓴다 —
                // 파고들 때 Face(dir)로 이미 진행 방향에 서고, 시전 중에는 아무도 몸을 돌리지 않는다.
                Vector3 baseOrigin = hit.fixedOrigin ? castOrigin : phys.GroundPosition;

                range = AttackRangePreview.FromBox(baseOrigin, phys.Facing,
                                                   new Vector3(0f, 0f, size.z * 0.5f), size, 0f, progress);
                return true;
            }

            if (data.install && hit.HasCastRange && ctx.caster != null)
            {
                Vector3 hurtbox = ctx.caster.HurtboxSize;
                Vector3 size = new Vector3(hurtbox.x * hit.castRangeScale.x,
                                           hurtbox.y * hit.castRangeScale.y,
                                           hurtbox.z * hit.castRangeScale.z);
                range = AttackRangePreview.FromBox(ctx.Origin, Vector3.forward, Vector3.zero, size, 0f, progress);
                return true;
            }

            if (data.IsAreaSkill)
            {
                range = AttackRangePreview.FromCircle(ctx.Origin, BlastRadius(in hit), progress);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 시전 대상을 확정한다. 조준은 <b>좌표</b>만 주므로 그 좌표에서 규칙대로 한 명을 고른다
        /// (<see cref="SkillContext.Origin"/> — GroundPoint면 찍은 자리, 아니면 시전자 자리).
        ///
        /// 규칙은 <see cref="SkillData.targetPick"/> 하나뿐이다 — 가장 가까운 적이거나 가장 먼 적.
        /// 반경 훑기 · 부채꼴 검사를 두지 않는 이유는 유저가 "어디로 나갈지"를 눈으로 알아야 하기 때문이다.
        ///
        /// 조준 시점과 시전 시점 사이에 대상이 죽었어도 여기서 자동으로 다시 잡히므로
        /// 별도의 재타겟 경로가 필요 없다.
        /// </summary>
        protected void ResolveTarget()
        {
            if (ctx.target != null && ctx.target.Combat != null && !ctx.target.Combat.IsDead) return;

            // 찍은 좌표가 있으면 그 좌표가 곧 지정이다 — 유저가 직접 조준했거나,
            // 자동 조준(Ally.AutoTarget)이 이미 targetPick으로 고른 적의 자리다.
            if (ctx.targetInfo.type == TargetingType.GroundPoint)
            {
                ctx.target = BattleRegistry.NearestEnemy(ctx.Origin);
                return;
            }

            // 좌표가 없는 조준(None · Direction)은 시전자 기준으로 규칙대로 한 명 고른다.
            Vector3 from = ctx.CasterPhysics != null ? ctx.CasterPhysics.GroundPosition : ctx.Origin;
            ctx.target = BattleRegistry.PickEnemy(from, data.targetPick);
        }

        /// <summary>
        /// 자동 발동의 전부. <b>대상보다 멀면 사거리 안까지 들어가고, 그다음 때린다.</b>
        ///
        /// 예전에는 직업으로 갈렸다 — 근접만 이동하고 원거리는 제자리. 그래서 같은 카드가
        /// 시전자에 따라 다르게 움직였고, 원거리는 사거리 밖이면 아무 일도 없이 투사체만 증발했다.
        /// 규칙을 하나로 줄이면 유저가 외울 것도 하나다.
        /// </summary>
        protected void PlaceCaster()
        {
            Physics phys = ctx.CasterPhysics;
            if (phys == null) return;

            // 공중 시전은 수평으로 붙고 수직으로 떨어진다 — 대상 바로 위를 잡아야 꽂는 그림이 나온다.
            // 높이를 실은 Teleport는 태그 교대가 쓰던 경로 그대로다(Aerial 진입 · 수직속도 0).
            if (data.CastsAboveTarget && TryGetAirCastSpot(out Vector3 above, out float height))
            {
                phys.Teleport(above, height);
            }
            else
            {
                bool moved = TryGetCastSpot(phys, out Vector3 spot);
                float lift = AerialTargetHeight();

                // 뜬 대상은 높이까지 따라간다. 안 따라가면 히트박스가 시전자 허리춤에 생겨
                // 공중 콤보가 통째로 헛친다 — 판정을 키워서 메우면 지상 판정까지 같이 부푼다.
                if (moved || lift > 0f)
                    phys.Teleport(moved ? spot : phys.GroundPosition, lift);
            }

            phys.Face(FaceDirection(phys));

            // 시전 중 관성은 전부 끊는다. 연계가 밀리는 오차를 차단.
            phys.ResetInertia();
        }

        /// <summary>
        /// 장판인지 — 원거리 직업인데 날릴 투사체가 없는 스킬(융기 · 중력장 · 그물사격 …).
        ///
        /// 이런 스킬을 시전자 히트박스로 때리면 원거리는 제자리에 서 있으므로
        /// <b>판정이 전부 자기 발밑에서 터진다</b>. 찍은 좌표에는 연출과 광역 효과만 가고
        /// 데미지는 안 따라가는 상태가 된다. 그래서 기준점 반경으로 직접 때린다.
        /// </summary>
        private bool IsAreaCaster => data.IsAreaSkill;

        /// <summary>
        /// 타격 반경. 즉시 장판이든 투사체 도착 폭발이든 같은 값을 쓴다 —
        /// 조준 링이 그리는 원이 곧 맞는 범위여야 한다.
        /// 타별 반경(<see cref="HitData.radius"/>)이 있으면 그쪽, 없으면 스킬 공통값이다.
        /// </summary>
        private float BlastRadius(in HitData hit) => data.RadiusFor(in hit);

        /// <summary>
        /// 공중 시전 자리. 수평으로는 <b>대상 좌표 그대로</b>, 수직으로는 대상 머리 위
        /// <see cref="SkillData.ApproachDistance"/>만큼이다.
        ///
        /// 높이를 지면이 아니라 <b>대상 기준</b>으로 재는 이유: 이미 높이 떠 있는 적을
        /// 지면 기준으로 재면 시전자가 적보다 아래에 서서 아래에서 올려치게 된다.
        ///
        /// 대상이 없으면 false — 올라갈 이유가 없다. 제자리에서 헛친다.
        /// </summary>
        private bool TryGetAirCastSpot(out Vector3 spot, out float height)
        {
            spot = default;
            height = 0f;

            Physics t = ctx.target != null ? ctx.target.Physics : null;
            if (t == null) return false;

            spot = t.GroundPosition;
            height = t.Height + data.ApproachDistance;
            return true;
        }

        /// <summary>
        /// 대상이 공중에 떠 있으면 그 높이, 아니면 0.
        ///
        /// <b>근접만</b>이다. 투사체 · 장판은 시전자 몸에 붙은 히트박스로 때리지 않으므로
        /// 시전자를 띄워 봐야 판정이 달라지지 않고 그림만 이상해진다.
        /// 내려찍기(<see cref="SkillData.CastsAboveTarget"/>)도 제외한다 — 그쪽은 같은 높이가 아니라
        /// 대상 <b>위</b>를 잡아야 한다.
        /// </summary>
        private float AerialTargetHeight()
        {
            if (data.CastsAboveTarget || data.IsRanged || IsAreaCaster) return 0f;

            Physics t = ctx.target != null ? ctx.target.Physics : null;
            if (t == null || t.PhysicsState != PhysicsState.Aerial) return 0f;

            return Mathf.Max(0f, t.Height);
        }

        /// <summary>
        /// 시전자 높이를 대상에 다시 맞춘다. 수평은 건드리지 않는다 —
        /// 접근 거리는 <see cref="PlaceCaster"/>가 이미 잡아 뒀고, 매 타격마다 파고들면
        /// 다단히트가 대상을 밀고 다니는 것처럼 보인다.
        /// </summary>
        private void MatchTargetHeight()
        {
            float lift = AerialTargetHeight();
            if (lift <= 0f) return;

            Physics phys = ctx.CasterPhysics;
            if (phys == null) return;

            // 이미 맞아 있으면 그대로 둔다. 매 타격 Teleport는 수직속도를 0으로 만들어
            // 시전자가 눈에 띄게 덜컥거린다.
            if (Mathf.Abs(phys.Height - lift) < 0.05f) return;

            phys.Teleport(phys.GroundPosition, lift);
        }

        /// <summary>
        /// 시전을 시작할 자리. 옮길 필요가 없으면 false.
        /// 직업을 보지 않는다 — <see cref="SkillData.ApproachDistance"/>가 성격별 거리를 이미 접어 준다.
        /// </summary>
        private bool TryGetCastSpot(Physics phys, out Vector3 spot)
        {
            spot = default;
            if (ctx.target == null || ctx.target.Physics == null) return false;

            // 이미 닿는 거리면 안 움직인다. ApproachDistance로 재면 맞출 수 있는데도
            // 사거리 절반까지 끌려 들어간다.
            Vector3 gap = ctx.target.Physics.GroundPosition - phys.GroundPosition;
            gap.y = 0f;
            if (gap.magnitude <= CastReach) return false;

            return TryApproach(phys.GroundPosition, ctx.target, data.ApproachDistance, out spot);
        }

        /// <summary>제자리에서 맞출 수 있는 거리. 투사체는 사거리, 장판은 반경, 근접은 접근 거리.</summary>
        private float CastReach
        {
            get
            {
                if (data.IsRanged) return data.projectileRange;
                if (IsAreaCaster) return data.radius;
                return data.ApproachDistance;
            }
        }

        /// <summary>
        /// 대상에게서 <paramref name="distance"/>만큼 떨어진 자리. 오던 쪽에 붙는다 —
        /// 대상을 관통해 반대편으로 넘어가지 않게.
        ///
        /// <b>이미 그만큼 가까우면 움직이지 않는다.</b> 멀 때만 들어가는 게 규칙이라,
        /// 코앞에 붙어 있던 원거리를 억지로 뒤로 밀어내지 않는다.
        /// </summary>
        public static bool TryApproach(Vector3 from, Entity target, float distance, out Vector3 spot)
        {
            spot = default;
            if (target == null || target.Physics == null) return false;
            if (target.Combat != null && target.Combat.IsDead) return false;

            Vector3 targetPos = target.Physics.GroundPosition;

            Vector3 gap = targetPos - from;
            gap.y = 0f;

            // 사거리 안이면 그대로 쏘거나 휘두른다. 여기서 걸러야 매 타격마다 미세하게 튀지 않는다.
            if (gap.magnitude <= distance) return false;

            // 벨트스크롤에서 Z가 어긋나면 후속타가 전부 빗나간다(Physics.SnapZ와 같은 이유).
            // X만 띄우고 깊이는 대상과 같은 레인에 맞춘다.
            // 계산은 KnockbackPreview가 들고 있다 — 프리뷰와 실전이 같은 자리를 잡아야 화살표가 맞는다.
            spot = KnockbackPreview.ApproachSpot(from, targetPos, distance);
            return true;
        }

        /// <summary>
        /// 시전 후 바라볼 방향. 좌표가 아니라 <b>대상</b>을 본다 —
        /// 근거리는 이미 그 좌표로 옮겨 온 뒤라 찍은 지점을 향하면 0벡터가 되어 방향이 안 잡힌다.
        /// </summary>
        private Vector3 FaceDirection(Physics phys)
        {
            if (ctx.targetInfo.type == TargetingType.Direction)
                return ctx.targetInfo.direction;

            Vector3 from = phys.GroundPosition;

            if (ctx.target != null)
                return ctx.target.Physics.GroundPosition - from;

            // 살아 있는 적이 하나도 없을 때의 마지막 수단.
            Vector3 toPoint = ctx.targetInfo.point - from;
            toPoint.y = 0f;
            return toPoint.sqrMagnitude > 0.0001f ? toPoint : phys.Facing;
        }
    }
}
