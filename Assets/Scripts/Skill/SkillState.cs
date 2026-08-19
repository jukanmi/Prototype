using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스킬의 런타임 인스턴스. 실제로 동료가 움직이고 때리는 건 전부 여기서 일어난다.
    /// 피격당해도 중단되지 않는다 — 슈퍼아머(결정 로그 ③).
    /// 사망만 <see cref="StateMachine.ForceChangeState"/>로 관통한다.
    /// </summary>
    public class SkillState : IState
    {
        private readonly SkillData data;
        private SkillContext ctx;

        private float timer;
        private int nextHitIndex;
        private float nextHitTime;
        private bool finished;

        public SkillState(SkillData data, in SkillContext ctx)
        {
            this.data = data;
            this.ctx = ctx;
        }

        /// <summary>슈퍼아머. TryChangeState는 전부 거부된다.</summary>
        public virtual bool CanBeInterrupted => false;

        /// <summary>시전 중인 스킬. EntityAnimator가 클립을 갈아끼울 때 읽는다.</summary>
        public SkillData Data => data;

        /// <summary>후딜까지 끝났는지. ComboExecutor가 다음 슬롯으로 넘어갈 시점 판단에 쓴다.</summary>
        public virtual bool IsFinished => finished;

        /// <summary>파생 상태(차징)가 시전 맥락을 읽고 고칠 수 있게 연다.</summary>
        protected ref SkillContext Context => ref ctx;

        /// <summary>
        /// 이번 <see cref="Enter"/>에서 시전 위치를 잡을지.
        /// 차징은 모으기 시작 시점에 이미 자리를 잡았으므로 터질 때 다시 옮기지 않는다.
        /// </summary>
        protected virtual bool PlaceOnEnter => true;

        /// <summary>
        /// 타격 직전에 HitData를 손볼 마지막 기회. 차징이 배율을 여기서 태운다.
        /// 원본 에셋은 건드리지 않는다 — 값 복사본만 바뀐다.
        /// </summary>
        protected virtual HitData ModifyHit(HitData hit) => hit;

        public virtual void Enter()
        {
            timer = 0f;
            nextHitIndex = 0;
            finished = false;
            nextHitTime = data.castTime;

            // 조준은 좌표만 준다. 실제로 때릴 상대는 여기서 확정한다.
            ResolveTarget();

            BattleLog.Log(LogCategory.Skill,
                $"<b>{BattleLog.Name(ctx.caster)}</b> 시전: {data.skillName} | {data.attackType} | 선행 {data.requireState} → 결과 {data.resultState} | " +
                $"조준 {ctx.targetInfo.type} → 대상 {BattleLog.Name(ctx.target)} | {(ctx.isBulletTime ? "불릿타임" : "라이브")} | 히트 {data.hitDataList.Count}단",
                ctx.caster);

            // 자리를 먼저 잡아야 아래 연출과 효과가 전부 최종 위치를 기준으로 돈다.
            if (PlaceOnEnter) PlaceCaster();
            EmitCastVfx();
            ApplyEffects();

            // 선딜이 없으면 즉시 첫 타를 낸다.
            if (data.castTime <= 0f)
                FireNextHit();
        }

        public virtual void Tick(float dt)
        {
            if (finished) return;

            timer += dt;

            while (nextHitIndex < data.hitDataList.Count && timer >= nextHitTime)
                FireNextHit();

            if (nextHitIndex >= data.hitDataList.Count && timer >= EndTime)
            {
                ctx.caster?.SkillAttack?.End();
                finished = true;

                BattleLog.Log(LogCategory.Skill,
                    $"{BattleLog.Name(ctx.caster)} 종료: {data.skillName} (후딜 {data.recoveryTime:0.##}s 포함 {EndTime:0.##}s)", ctx.caster);
            }
        }

        public virtual void Exit()
        {
            ctx.caster?.SkillAttack?.End();
            finished = true;
        }

        protected float EndTime
        {
            get
            {
                int hits = Mathf.Max(1, data.hitDataList.Count);
                return data.castTime + data.hitInterval * (hits - 1) + data.recoveryTime;
            }
        }

        /// <summary>
        /// 시전 대상을 확정한다. 조준은 <b>좌표</b>만 주므로 그 좌표에서 가장 가까운 적을 상대로 삼는다.
        /// (<see cref="SkillContext.Origin"/> — GroundPoint면 찍은 자리, 아니면 시전자 자리)
        ///
        /// 조준 시점과 시전 시점 사이에 대상이 죽었어도 여기서 자동으로 다시 잡히므로
        /// 별도의 재타겟 경로가 필요 없다.
        /// </summary>
        protected void ResolveTarget()
        {
            if (ctx.target != null && ctx.target.Combat != null && !ctx.target.Combat.IsDead) return;

            ctx.target = BattleRegistry.NearestEnemy(ctx.Origin);
        }

        /// <summary>
        /// 결정론적 가이드. <b>근거리 직업은 대상 옆으로 순간이동한 뒤에 시작한다</b> —
        /// 제자리에서 휘두르면 조준한 곳에 판정이 안 닿아 콤보가 통째로 헛돈다.
        /// 원거리 직업은 사거리가 있으니 움직이지 않고 방향만 맞춘다.
        /// </summary>
        protected void PlaceCaster()
        {
            Physics phys = ctx.CasterPhysics;
            if (phys == null) return;

            if (TryGetCastSpot(phys, out Vector3 spot))
                phys.Teleport(spot);

            phys.Face(FaceDirection(phys));

            // 시전 중 관성은 전부 끊는다. 연계가 밀리는 오차를 차단.
            phys.ResetInertia();
        }

        /// <summary>
        /// 근거리 직업인지. 스킬 카드는 직업에 묶여 있으므로(<c>Ally.EquipSkill</c>)
        /// 에셋의 role이 곧 시전자의 직업이다.
        ///
        /// 투사체 유무로 가르지 않는 이유: 위저드 장판은 투사체가 없지만 근거리가 아니다.
        /// 그걸 근접으로 보면 마법사가 적진 한가운데로 순간이동한다.
        /// </summary>
        private bool IsMeleeCaster => data.role == Role.Tanker || data.role == Role.Warrior;

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
        /// </summary>
        private float BlastRadius => data.radius * ctx.RadiusScale;

        /// <summary>
        /// 시전을 시작할 자리. 옮길 필요가 없으면 false.
        ///
        /// 조준 방식과 무관하게 <b>대상 옆</b>이 답이다 — 찍은 좌표 위에 그대로 서면
        /// 적과 겹치거나 사거리 밖에 떨어져 판정이 안 닿는다.
        /// 원거리는 사거리가 있으므로 아예 움직이지 않는다.
        /// </summary>
        private bool TryGetCastSpot(Physics phys, out Vector3 spot)
        {
            spot = default;
            if (!IsMeleeCaster) return false;

            return TryApproach(phys.GroundPosition, ctx.target, out spot);
        }

        /// <summary>대상 옆에 서는 자리. 오던 쪽에 붙는다 — 대상을 관통해 넘어가지 않게.</summary>
        private bool TryApproach(Vector3 from, Entity target, out Vector3 spot)
        {
            spot = default;
            if (target == null || target.Physics == null) return false;
            if (target.Combat != null && target.Combat.IsDead) return false;

            // 벨트스크롤에서 Z가 어긋나면 후속타가 전부 빗나간다(Physics.SnapZ와 같은 이유).
            // X만 띄우고 깊이는 대상과 같은 레인에 맞춘다.
            // 계산은 KnockbackPreview가 들고 있다 — 프리뷰와 실전이 같은 자리를 잡아야 화살표가 맞는다.
            spot = KnockbackPreview.ApproachSpot(from, target.Physics.GroundPosition, data.ApproachDistance);

            // 이미 그 자리면 옮기지 않는다. 매 타격마다 미세하게 튀는 걸 막는다.
            Vector3 d = spot - from;
            d.y = 0f;
            return d.sqrMagnitude > 0.04f;
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

        /// <summary>
        /// 스킬이 터지는 자리를 한 번 크게 알린다.
        /// 반경은 판정과 같은 값을 쓴다 — 차징으로 커진 배율까지 그대로 따라간다.
        /// </summary>
        private void EmitCastVfx()
        {
            SkillVfx style = data.vfx.AsSkill();
            BattleVfx.Cast(ctx.Origin, data.radius * ctx.RadiusScale, in style);
        }

        private void ApplyEffects()
        {
            if (data.effects == null) return;

            for (int i = 0; i < data.effects.Count; i++)
            {
                if (data.effects[i] == null) continue;

                BattleLog.Log(LogCategory.Skill,
                    $"  └ 효과 적용: {data.effects[i].GetType().Name}", ctx.caster);
                data.effects[i].Apply(in ctx);
            }
        }

        private void FireNextHit()
        {
            if (nextHitIndex >= data.hitDataList.Count) return;

            HitData hit = ModifyHit(data.hitDataList[nextHitIndex]);

            if (data.IsRanged) LaunchProjectile(in hit);
            else if (IsAreaCaster) FireArea(in hit);
            else FireMelee(in hit);

            BattleLog.Log(LogCategory.Skill,
                $"  └ {data.skillName} {nextHitIndex + 1}/{data.hitDataList.Count}타 발동 (t={timer:0.##}s)", ctx.caster);

            nextHitIndex++;
            nextHitTime = data.castTime + data.hitInterval * nextHitIndex;
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
            Vector3 center = ctx.Origin;
            float r = BlastRadius;

            int hits = EffectUtil.AreaStrike(center, r, attacker, in hit, in style);

            if (hits == 0)
                BattleLog.Log(LogCategory.Skill,
                    $"  └ {data.skillName} 반경 {r:0.#} 안에 적 없음 — 헛침", ctx.caster);
        }

        /// <summary>근접 — 시전자에게 붙은 히트박스를 켠다. 기존 동작.</summary>
        private void FireMelee(in HitData hit)
        {
            Attack box = ctx.caster != null ? ctx.caster.SkillAttack : null;
            if (box == null) return;

            if (box.Attacker == null && ctx.caster != null)
                box.Attacker = ctx.caster.Combat;

            // 히트박스는 스킬끼리 공유된다(SkillAttack). 켤 때마다 이번 스킬 색을 다시 실어 준다.
            // AsSkill()로 스킬 표시를 같이 넘겨야 평타보다 크게 그려진다.
            SkillVfx style = data.vfx.AsSkill();
            box.Begin(in hit, in style);
        }

        /// <summary>원거리 — 타격마다 투사체를 하나씩 쏜다.</summary>
        private void LaunchProjectile(in HitData hit)
        {
            Physics phys = ctx.CasterPhysics;
            if (phys == null || ctx.caster == null) return;

            Projectile shot = Object.Instantiate(data.projectile);

            // 히트박스 레이어를 그대로 물려받아야 충돌 매트릭스가 맞는다.
            int layer = ctx.caster.BasicAttack != null
                ? ctx.caster.BasicAttack.gameObject.layer
                : ctx.caster.gameObject.layer;

            SkillVfx style = data.vfx.AsSkill();

            // 원거리는 이동하지 않으므로 대상이 사거리 밖이면 아무 일도 없이 소멸한다.
            // 반경 기반 헛침 경고로는 안 잡히는 경우라 여기서 남긴다.
            if (ctx.target != null)
            {
                Vector3 gap = ctx.target.Physics.GroundPosition - phys.GroundPosition;
                gap.y = 0f;

                if (gap.magnitude > data.projectileRange)
                    BattleLog.Warn(LogCategory.Skill,
                        $"  └ {data.skillName} 사거리 밖 — 대상까지 {gap.magnitude:0.#} > 사거리 {data.projectileRange:0.#}. 투사체가 도중에 사라진다",
                        ctx.caster);
            }

            shot.Launch(ctx.caster.Combat, in hit,
                        phys.GroundPosition, AimDirection(phys),
                        data.projectileSpeed, data.projectileRange, data.projectilePierce,
                        phys.WallMask, layer, in style, AimHeight(), BlastRadius);
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
        private float AimHeight()
        {
            if (ctx.target == null || ctx.target.Physics == null) return -1f;

            float h = ctx.target.Physics.Height;
            return h > 0.1f ? h : -1f;
        }

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
