using System.Collections.Generic;

namespace Prototype
{
    /// <summary>실시간 전투. 플레이어 직접 조작 · 게이지 충전 구간.</summary>
    public class RealTimeState : TacticState
    {
        public RealTimeState(BulletTimeController ctx, TacticStateMachine sm) : base(ctx, sm) { }

        public override TacticPhase Phase => TacticPhase.RealTime;

        public override void Enter()
        {
            Ctx.ResumeTime();
        }

        public override bool OnBulletTimeKey()
        {
            if (!Ctx.CanEnter)
            {
                BattleLog.Log(LogCategory.Bullet, $"불릿타임 진입 거부 — {Ctx.BlockReason()}", Ctx);
                return false;
            }

            SM.ChangeTo(SM.Freeze);
            return true;
        }
    }

    /// <summary>
    /// 시간 정지 · 덱 셔플 · 드로우. 연출용 구간이라 입력을 받지 않는다.
    /// 길이가 0이면 다음 프레임에 곧바로 Order로 넘어간다.
    /// </summary>
    public class FreezeState : TacticState
    {
        private float timer;

        public FreezeState(BulletTimeController ctx, TacticStateMachine sm) : base(ctx, sm) { }

        public override TacticPhase Phase => TacticPhase.Freeze;

        public override void Enter()
        {
            timer = 0f;

            Ctx.PayEntryCost();
            Ctx.FreezeTime();
            Ctx.DrawHand();
            Ctx.RaiseEnter();
        }

        public override void Tick(float dt)
        {
            timer += dt;
            if (timer >= Ctx.FreezeDuration)
                SM.ChangeTo(SM.Order);
        }
    }

    /// <summary>손패 조작 구간. 유저가 실제로 머무는 곳.</summary>
    public class OrderState : TacticState
    {
        public OrderState(BulletTimeController ctx, TacticStateMachine sm) : base(ctx, sm) { }

        public override TacticPhase Phase => TacticPhase.Order;

        public override bool AllowsCardEdit => true;

        // E와 Space 모두 실행. 기존 조작(E 토글 / Space 실행)을 그대로 유지한다.
        public override bool OnBulletTimeKey() => GoResolve();
        public override bool OnExecuteKey() => GoResolve();

        private bool GoResolve()
        {
            SM.ChangeTo(SM.Resolve);
            return true;
        }
    }

    /// <summary>
    /// 조립한 큐를 실행한다. 시간이 다시 흐르고, 카드 편집 입력은 전부 막힌다.
    /// 실행이 끝나야 RealTime으로 돌아간다 — 그 전엔 재진입 불가.
    /// </summary>
    public class ResolveState : TacticState
    {
        public ResolveState(BulletTimeController ctx, TacticStateMachine sm) : base(ctx, sm) { }

        public override TacticPhase Phase => TacticPhase.Resolve;

        public override void Enter()
        {
            Ctx.CancelTargeting();
            Ctx.ResumeTime();
            Ctx.ConsumeGauge();
            Ctx.StartCooldown();

            Queue<ComboSlot> queue = Ctx.CollectQueue();
            Ctx.RaiseExit();

            if (queue != null && queue.Count > 0)
                Ctx.Executor?.Execute(queue);
        }

        public override void Tick(float dt)
        {
            // 큐가 비어 있었으면 Executor가 시작조차 안 하므로 곧바로 복귀한다.
            if (Ctx.Executor == null || !Ctx.Executor.IsRunning)
                SM.ChangeTo(SM.RealTime);
        }

        public override bool OnBulletTimeKey()
        {
            BattleLog.Log(LogCategory.Bullet, "불릿타임 진입 거부 — 콤보 실행 중", Ctx);
            return false;
        }

        public override bool OnExecuteKey() => false;
    }
}
