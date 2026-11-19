using System.Collections.Generic;

namespace Prototype
{
    /// <summary>
    /// 전술 층의 페이즈 — 실시간 전투와 총공격 실행.
    /// 기계는 엔티티와 같은 <see cref="StateMachine"/>이다 — 인스턴스만 따로 쓴다.
    ///
    /// <code>
    /// RealTime ─게이지 가득 · 적 있음→ Resolve ─콤보 끝→ RealTime
    /// </code>
    /// </summary>
    public enum TacticPhase
    {
        /// <summary>실시간 전투. 플레이어가 직접 조작한다.</summary>
        RealTime,
        /// <summary>총공격 슬롯으로 만든 큐를 실행한다.</summary>
        Resolve,
    }

    // ══ 전술 층 ═══════════════════════════════════════════
    // 상태 클래스는 전부 컨트롤러 안의 private 중첩 클래스다. 그래서 컨트롤러의 private 동작
    // (게이지 소모 · 큐 실행)을 부를 수 있는 건 이 페이즈들뿐이다 — 밖에서는 읽기 전용 값만 보인다.

    public partial class BulletTimeController
    {
        /// <summary>전술 페이즈 하나.</summary>
        private abstract class TacticState : IState
        {
            protected readonly BulletTimeController Ctx;

            protected TacticState(BulletTimeController ctx) { Ctx = ctx; }

            public abstract TacticPhase Phase { get; }

            /// <summary>전술 페이즈는 언제든 넘어갈 수 있다.</summary>
            public bool CanBeInterrupted => true;

            public virtual void Enter() { }
            /// <summary>dt는 항상 <see cref="TimeControl.UnscaledDeltaTime"/>.</summary>
            public virtual void Tick(float dt) { }
            public virtual void Exit() { }
        }

        /// <summary>실시간 전투. 플레이어 직접 조작 · 게이지 충전 · 평타 적중 스킬 발동 구간.</summary>
        private sealed class RealTimeState : TacticState
        {
            public RealTimeState(BulletTimeController ctx) : base(ctx) { }

            public override TacticPhase Phase => TacticPhase.RealTime;

            public override void Enter()
            {
                TimeControl.Scale = 1f;
                // 빈 손패는 실행기를 안 돌리지만 붙잡힌 자동 시전은 풀어야 한다.
                Ctx.Director?.EndSelection();

                // 실행이 끝난 직후일 수 있다. 손패가 비어 있으면 여기서 마저 채운다.
                Ctx.RefillHand();
            }
        }

        /// <summary>총공격 큐를 실행한다. 실행기가 멈추면 실시간으로 돌아간다.</summary>
        private sealed class ResolveState : TacticState
        {
            public ResolveState(BulletTimeController ctx) : base(ctx) { }

            public override TacticPhase Phase => TacticPhase.Resolve;

            public override void Enter()
            {
                TimeControl.Scale = 1f;
                Ctx.Budget.PayEntry(Ctx.player);
                Ctx.Budget.Spend();

                Queue<ComboSlot> queue = Ctx.assaultQueue;
                Ctx.assaultQueue = null;

                if (queue != null && queue.Count > 0 && Ctx.executor != null)
                    Ctx.executor.Execute(queue);
                else
                    Ctx.RefillHand();   // 실행할 게 없으면 Executor가 안 돌아 보충 신호도 안 온다
            }

            public override void Tick(float dt)
            {
                // 큐가 비어 있었으면 Executor가 시작조차 안 하므로 곧바로 복귀한다.
                if (Ctx.executor == null || !Ctx.executor.IsRunning)
                {
                    Ctx.assaultCast = false;
                    Ctx.ChangePhase(Ctx.realTimeState);
                }
            }
        }
    }
}
