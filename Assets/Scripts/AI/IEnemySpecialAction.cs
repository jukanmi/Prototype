namespace Prototype
{
    /// <summary>
    /// 평타가 아닌 행동의 <b>실행기</b>. 예고 · 이동 · 히트박스처럼 인스턴스가 있어야 되는 일을 맡는다.
    /// 판단(언제 쓸지)은 <see cref="IEnemyBrain"/>이, 타이머는 <see cref="EnemyControl"/>이 갖는다.
    ///
    /// <b>Entity 하나에 구현체는 하나만 붙인다.</b> 패턴이 여럿이면 컴포넌트를 늘리지 말고
    /// 한 구현체가 <see cref="Count"/>개의 패턴을 들게 한다 — 여러 컴포넌트에 인덱스를 나눠 주면
    /// 인스펙터의 컴포넌트 순서가 곧 패턴 번호가 되어, 순서를 바꾸는 것만으로 보스가 다른 기술을 쓴다.
    /// </summary>
    public interface IEnemySpecialAction
    {
        /// <summary>가진 패턴 수. EnemyControl이 쿨 타이머 배열의 크기를 여기에 맞춘다.</summary>
        int Count { get; }

        bool IsRunning { get; }

        /// <summary>
        /// 시작 시도. 거절하면(false) 쿨이 소모되지 않는다 —
        /// 1회성 패턴은 두 번째 요청을 여기서 막으면 브레인이 자연히 다음 패턴을 고른다.
        /// </summary>
        bool TryStart(int index, Entity target);

        /// <summary>EnemyControl이 실행 중에만 부른다.</summary>
        void Tick(float dt);

        /// <summary>피격 · 사망 · AI 정지. 어느 단계든 흔적 없이 되돌린다.</summary>
        void Cancel();
    }
}
