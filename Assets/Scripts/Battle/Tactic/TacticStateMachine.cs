using System;

namespace Prototype
{
    /// <summary>
    /// 전술 층 상태머신. <see cref="BulletTimeController"/>가 내부에 하나만 들고 있다.
    /// MonoBehaviour가 아니므로 씬 연결이 필요 없다.
    /// </summary>
    public class TacticStateMachine
    {
        private readonly BulletTimeController ctx;
        private TacticState cur;

        public RealTimeState RealTime { get; }
        public FreezeState Freeze { get; }
        public OrderState Order { get; }
        public ResolveState Resolve { get; }

        public TacticPhase Phase => cur != null ? cur.Phase : TacticPhase.RealTime;

        /// <summary>손패 · 슬롯 편집이 열려 있는 페이즈인지. UI가 이 값으로 켜고 끈다.</summary>
        public bool AllowsCardEdit => cur != null && cur.AllowsCardEdit;

        public event Action<TacticPhase, TacticPhase> OnPhaseChanged; // (prev, next)

        public TacticStateMachine(BulletTimeController ctx)
        {
            this.ctx = ctx;

            RealTime = new RealTimeState(ctx, this);
            Freeze = new FreezeState(ctx, this);
            Order = new OrderState(ctx, this);
            Resolve = new ResolveState(ctx, this);
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
            OnPhaseChanged?.Invoke(prev, next.Phase);
        }

        // ── 입력 진입점. 현재 페이즈만 해석한다 ─────────────

        public bool OnBulletTimeKey() => cur != null && cur.OnBulletTimeKey();

        public bool OnExecuteKey() => cur != null && cur.OnExecuteKey();
    }
}
