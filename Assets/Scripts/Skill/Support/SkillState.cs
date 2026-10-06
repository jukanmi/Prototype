// 스킬 시전 상태 — 수명(Enter · Tick · Exit)과 시전 효과.
// 조준 · 자리 잡기 · 범위 미리보기는 SkillState.Aim.cs, 타격 발사는 SkillState.Fire.cs,
// 입력 문맥은 SkillContext.cs.

using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스킬의 런타임 인스턴스. 실제로 동료가 움직이고 때리는 건 전부 여기서 일어난다.
    /// 피격당해도 중단되지 않는다 — 슈퍼아머(결정 로그 ③).
    /// 사망만 <see cref="StateMachine.ForceChangeState"/>로 관통한다.
    /// </summary>
    public partial class SkillState : IState
    {
        private readonly SkillData data;
        private SkillContext ctx;

        private float timer;
        private int nextHitIndex;
        private float nextHitTime;
        private bool finished;
        private Vector3 castOrigin;
        private SkillFx fx;

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

        public virtual void Enter()
        {
            timer = 0f;
            nextHitIndex = 0;
            finished = false;
            nextHitTime = data.HitTime(0);

            // 조준은 좌표만 준다. 실제로 때릴 상대는 여기서 확정한다.
            ResolveTarget();

            BattleLog.Log(LogCategory.Skill,
                $"<b>{BattleLog.Name(ctx.caster)}</b> 시전: {data.skillName} | {data.attackType} | 선행 {data.requireState} → 결과 {data.resultState} | " +
                $"조준 {ctx.targetInfo.type} → 대상 {BattleLog.Name(ctx.target)} | {(ctx.isBulletTime ? "불릿타임" : "라이브")} | 히트 {data.hitDataList.Count}단",
                ctx.caster);

            // 자리를 먼저 잡아야 아래 연출과 효과가 전부 최종 위치를 기준으로 돈다.
            PlaceCaster();

            Physics phys = ctx.CasterPhysics;
            castOrigin = phys != null ? phys.GroundPosition : ctx.Origin;
            EmitCastVfx();
            fx = SkillFx.Create(data, in ctx);
            fx?.OnCast();
            ApplyEffects();

            // 설치기는 타격을 몸에서 떼어 낸다. 시전자는 EndTime(castTime + recoveryTime)에 풀려나고
            // 후속타는 Installation이 자기 시계로 설치 지점에서 낸다 — 불릿타임이면 다음 슬롯과 겹친다.
            if (data.install)
            {
                Installation.Place(data, in ctx, ctx.Origin);
                nextHitIndex = data.hitDataList.Count;   // Tick의 while이 안 돌고 EndTime에 finished
                return;
            }

            // 선딜이 없으면 즉시 첫 타를 낸다.
            if (nextHitTime <= 0f)
                FireNextHit();
        }

        public virtual void Tick(float dt)
        {
            if (finished) return;

            timer += dt;
            fx?.Tick(timer);

            while (nextHitIndex < data.hitDataList.Count && timer >= nextHitTime)
                FireNextHit();

            if (nextHitIndex >= data.hitDataList.Count && timer >= EndTime)
            {
                ctx.caster?.SkillAttack?.End();
                finished = true;
                ReleaseFx();

                BattleLog.Log(LogCategory.Skill,
                    $"{BattleLog.Name(ctx.caster)} 종료: {data.skillName} (후딜 {data.recoveryTime:0.##}s 포함 {EndTime:0.##}s)", ctx.caster);
            }
        }

        public virtual void Exit()
        {
            ctx.caster?.SkillAttack?.End();
            finished = true;
            ReleaseFx();
        }

        /// <summary>전용 연출을 놓아준다. 남은 연출은 끝까지 재생된다.</summary>
        private void ReleaseFx()
        {
            fx?.Release();
            fx = null;
        }

        /// <summary>
        /// 후딜까지 끝나는 시각. <see cref="SkillData.TotalDuration"/>과 <b>같은 함수</b>를 본다 —
        /// 콤보 큐가 잡아 둔 시간과 실제 종료가 어긋나면 다음 슬롯이 겹치거나 빈다.
        /// </summary>
        protected float EndTime => data.TotalDuration;

        /// <summary>
        /// 스킬이 터지는 자리를 한 번 크게 알린다.
        /// 반경은 판정과 같은 값을 쓴다 — 차징으로 커진 배율까지 그대로 따라간다.
        /// </summary>
        private void EmitCastVfx()
        {
            SkillVfx style = data.vfx.AsSkill();
            BattleVfx.Cast(ctx.Origin, data.radius, in style);
        }

        /// <summary>
        /// 시전 순간에 도는 효과. <see cref="ILastHitEffect"/>는 여기서 빠지고
        /// <see cref="ApplyLastHitEffects"/>가 마지막 타격에 맞춰 낸다.
        /// </summary>
        private void ApplyEffects()
        {
            ApplyEffects(lastHit: false);

            // 때릴 게 아예 없는 스킬이면 마지막 타격이 영영 안 온다. 여기서 같이 내보낸다 —
            // 안 그러면 효과가 조용히 증발한다(검증이 hitDataList 비었다고 이미 경고하는 경우다).
            // 설치기도 같다 — 타격은 Installation이 내므로 이 상태에는 마지막 타격이 없다.
            if (data.install || data.hitDataList == null || data.hitDataList.Count == 0)
                ApplyLastHitEffects();
        }

        /// <summary>마지막 타격과 함께 도는 효과. 시전자를 움직이는 돌진이 여기 속한다.</summary>
        private void ApplyLastHitEffects() => ApplyEffects(lastHit: true);

        private void ApplyEffects(bool lastHit)
        {
            if (data.effects == null) return;

            for (int i = 0; i < data.effects.Count; i++)
            {
                ISkillEffect effect = data.effects[i];
                if (effect == null) continue;
                if (effect is ILastHitEffect != lastHit) continue;

                BattleLog.Log(LogCategory.Skill,
                    $"  └ 효과 적용: {effect.GetType().Name}" +
                    (lastHit ? " (마지막 타격)" : string.Empty), ctx.caster);
                effect.Apply(in ctx);
            }
        }

    }
}
