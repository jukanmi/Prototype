namespace Prototype
{
    /// <summary>
    /// 전술 층의 페이즈. 기획서 "전술" 박스 = 실시간 전투 ↔ 불릿타임.
    /// 엔티티 상태머신(<see cref="IState"/>)과는 다른 층이므로 인터페이스를 공유하지 않는다.
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

    /// <summary>전술 페이즈 하나. 키 입력은 각 페이즈가 직접 해석한다.</summary>
    public abstract class TacticState
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

        /// <summary>Space키. 처리했으면 true.</summary>
        public virtual bool OnBulletTimeKey() => false;

        /// <summary>Spacebar. 처리했으면 true.</summary>
        public virtual bool OnExecuteKey() => false;

        /// <summary>이 페이즈에서 손패 · 슬롯을 만질 수 있는지.</summary>
        public virtual bool AllowsCardEdit => false;
    }
}
