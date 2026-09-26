using System;
using System.Collections.Generic;

namespace Prototype
{
    /// <summary>
    /// 전술 층의 페이즈. 기획서 "전술" 박스 = 실시간 전투 ↔ 불릿타임.
    /// 엔티티 상태머신(<see cref="IState"/>)과는 다른 층이므로 인터페이스를 공유하지 않는다.
    ///
    /// <code>
    /// RealTime ─E(진입 가능)→ Freeze ─freezeDuration→ Order ─E→ Resolve ─콤보 끝→ RealTime
    /// </code>
    /// </summary>
    public enum TacticPhase
    {
        /// <summary>실시간 전투. 플레이어가 직접 조작한다.</summary>
        RealTime,
        /// <summary>시간 정지 · 덱 셔플 · 손패 드로우. 입력을 받지 않는 연출 구간.</summary>
        Freeze,
        /// <summary>손패를 슬롯에 배치 · 회수 · 조준한다. 유저가 머무는 구간.</summary>
        Order,
        /// <summary>조립한 큐를 실행한다. 카드 편집 입력을 전부 무시한다.</summary>
        Resolve,
    }

    // ══ 전술 층 ═══════════════════════════════════════════
    // 상태 클래스는 전부 컨트롤러 안의 private 중첩 클래스다. 그래서 컨트롤러의 private 동작
    // (게이지 소모 · 시간 정지 · 큐 굳히기)을 부를 수 있는 건 이 페이즈들뿐이다 —
    // 밖에서는 E키(Enter)와 읽기 전용 값만 보인다.

    public partial class BulletTimeController
    {
        /// <summary>전술 페이즈 하나. 키 입력은 각 페이즈가 직접 해석한다.</summary>
        private abstract class TacticState
        {
            protected readonly BulletTimeController Ctx;
            protected readonly TacticStateMachine SM;

            protected TacticState(BulletTimeController ctx, TacticStateMachine sm)
            {
                Ctx = ctx;
                SM = sm;
            }

            public abstract TacticPhase Phase { get; }

            public virtual void Enter() { }
            /// <summary>dt는 항상 <see cref="TimeControl.UnscaledDeltaTime"/>. 정지 중에도 흘러야 한다.</summary>
            public virtual void Tick(float dt) { }
            public virtual void Exit() { }

            /// <summary>E키. 처리했으면 true.</summary>
            public virtual bool OnBulletTimeKey() => false;

            /// <summary>이 페이즈에서 손패 · 슬롯을 만질 수 있는지.</summary>
            public virtual bool AllowsCardEdit => false;
        }

        /// <summary>
        /// 전술 층 상태머신. 컨트롤러가 내부에 하나만 들고 있다.
        /// MonoBehaviour가 아니므로 씬 연결이 필요 없다.
        /// </summary>
        private sealed class TacticStateMachine
        {
            private readonly BulletTimeController ctx;
            private TacticState cur;

            public RealTimeState RealTime { get; }
            public FreezeState Freeze { get; }
            public OrderState Order { get; }
            public ResolveState Resolve { get; }

            public TacticPhase Phase => cur != null ? cur.Phase : TacticPhase.RealTime;

            public bool AllowsCardEdit => cur != null && cur.AllowsCardEdit;

            public TacticStateMachine(BulletTimeController ctx)
            {
                this.ctx = ctx;

                RealTime = new RealTimeState(ctx, this);
                Freeze = new FreezeState(ctx, this);
                Order = new OrderState(ctx, this);
                Resolve = new ResolveState(ctx, this);
            }

            public TacticState StateOf(TacticPhase phase)
            {
                switch (phase)
                {
                    case TacticPhase.Freeze: return Freeze;
                    case TacticPhase.Order: return Order;
                    case TacticPhase.Resolve: return Resolve;
                    default: return RealTime;
                }
            }

            /// <summary>BulletTimeController.Start에서 한 번 호출한다.</summary>
            public void Begin()
            {
                cur = RealTime;
                cur.Enter();
            }

            public void Tick(float unscaledDt)
            {
                cur?.Tick(unscaledDt);
            }

            public void ChangeTo(TacticState next)
            {
                if (next == null || next == cur) return;

                TacticPhase prev = Phase;

                cur?.Exit();
                cur = next;
                next.Enter();

                BattleLog.Log(LogCategory.Bullet, $"전술 페이즈: {prev} → <b>{next.Phase}</b>", ctx);
                ctx.OnPhaseChanged?.Invoke(prev, next.Phase);
            }

            /// <summary>입력 진입점. 현재 페이즈만 해석한다.</summary>
            public bool OnBulletTimeKey() => cur != null && cur.OnBulletTimeKey();
        }

        /// <summary>실시간 전투. 플레이어 직접 조작 · 게이지 충전 · U키 단발 사용 구간.</summary>
        private sealed class RealTimeState : TacticState
        {
            public RealTimeState(BulletTimeController ctx, TacticStateMachine sm) : base(ctx, sm) { }

            public override TacticPhase Phase => TacticPhase.RealTime;

            public override void Enter()
            {
                TimeControl.Scale = 1f;

                // 실행이 끝난 직후일 수 있다. 손패가 비어 있으면 여기서 마저 채운다.
                Ctx.RefillHand();
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

        /// <summary>시간 정지 연출. 코스트를 내고 시간을 멈춘 뒤 잠깐 머물다 Order로 넘어간다.</summary>
        private sealed class FreezeState : TacticState
        {
            private float timer;

            public FreezeState(BulletTimeController ctx, TacticStateMachine sm) : base(ctx, sm) { }

            public override TacticPhase Phase => TacticPhase.Freeze;

            public override void Enter()
            {
                timer = 0f;

                Ctx.Budget.PayEntry(Ctx.player);
                Ctx.FreezeTime();
                Ctx.OnEnter?.Invoke();
            }

            public override void Tick(float dt)
            {
                timer += dt;
                if (timer >= Ctx.freezeDuration)
                    SM.ChangeTo(SM.Order);
            }
        }

        /// <summary>손패 편집 구간. E가 곧 실행이다.</summary>
        private sealed class OrderState : TacticState
        {
            public OrderState(BulletTimeController ctx, TacticStateMachine sm) : base(ctx, sm) { }

            public override TacticPhase Phase => TacticPhase.Order;

            public override bool AllowsCardEdit => true;

            public override bool OnBulletTimeKey()
            {
                SM.ChangeTo(SM.Resolve);
                return true;
            }
        }

        /// <summary>손패를 큐로 굳혀 실행한다. 실행기가 멈추면 실시간으로 돌아간다.</summary>
        private sealed class ResolveState : TacticState
        {
            public ResolveState(BulletTimeController ctx, TacticStateMachine sm) : base(ctx, sm) { }

            public override TacticPhase Phase => TacticPhase.Resolve;

            public override void Enter()
            {
                Ctx.targetSelector?.Cancel();
                TimeControl.Scale = 1f;
                Ctx.Budget.Spend();

                BattleLog.Log(LogCategory.Bullet, "<b>불릿타임 해제</b> — TimeControl.Scale = 1", Ctx);
                Queue<ComboSlot> queue = Ctx.Cards.TakeQueue(Ctx.ResolveCaster);
                Ctx.OnExit?.Invoke();

                if (queue.Count > 0 && Ctx.executor != null)
                    Ctx.executor.Execute(queue);
                else
                    Ctx.RefillHand();   // 실행할 게 없으면 Executor가 안 돌아 보충 신호도 안 온다
            }

            public override void Tick(float dt)
            {
                // 큐가 비어 있었으면 Executor가 시작조차 안 하므로 곧바로 복귀한다.
                if (Ctx.executor == null || !Ctx.executor.IsRunning)
                    SM.ChangeTo(SM.RealTime);
            }

            public override bool OnBulletTimeKey()
            {
                BattleLog.Log(LogCategory.Bullet, "불릿타임 진입 거부 — 콤보 실행 중", Ctx);
                return false;
            }
        }
    }
}
