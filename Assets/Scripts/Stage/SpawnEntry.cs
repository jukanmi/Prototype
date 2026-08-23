using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 소환된 적의 <b>진입 연출</b>. 걸어 들어가 자리를 잡고, 잠시 서 있다가 AI에 몸을 넘긴다.
    ///
    /// 이게 없으면 돌진전사가 화면 가장자리에서 곧장 꿰뚫고 들어와 <b>불합리하게 느껴진다</b> —
    /// 반응할 시간이 아니라 반응할 <i>정보</i>가 없기 때문이다. 걸어 들어오는 모습을 보여 주고
    /// 1초 남짓 세워 두면, 유저가 "저기서 온다"를 읽고 축을 옮길 수 있다.
    ///
    /// 순수 C#이다. <see cref="EnemyControl"/>이 이걸 들고 매 프레임 방향을 받아
    /// <c>Command.Move</c>로 옮기므로, 걷는 애니메이션도 벽 충돌도 평소 이동과 같은 경로를 탄다.
    /// </summary>
    public sealed class SpawnEntry
    {
        public enum Phase
        {
            /// <summary>정착 지점으로 걸어가는 중.</summary>
            Walking,

            /// <summary>도착. 선딜레이를 세는 중 — 여기서 유저에게 읽을 시간을 준다.</summary>
            Holding,

            /// <summary>끝. AI가 몸을 가져간다.</summary>
            Done,
        }

        /// <summary>도착으로 치는 거리. 몸통 반지름(0.5)보다 작으면 벽·다른 적에 밀려 영영 못 닿는다.</summary>
        public const float DefaultArriveRadius = 0.6f;

        /// <summary>
        /// 걷기 제한 시간. 이걸 넘기면 도착한 것으로 친다.
        ///
        /// 없으면 안 된다 — 정착 지점에 먼저 온 적이 서 있거나 벽 모서리에 끼면
        /// 그 적은 <b>영원히 AI가 안 깨어난다</b>. 화면에는 멀쩡히 서 있는데 아무것도 안 하고,
        /// 스테이지는 그 적이 안 죽어서 안 끝난다.
        /// </summary>
        public const float DefaultWalkTimeout = 4f;

        private readonly Vector3 target;
        private readonly float holdSeconds;
        private readonly float arriveRadiusSqr;
        private readonly float walkTimeout;

        private float walked;
        private float holdLeft;

        public Phase Current { get; private set; } = Phase.Walking;

        /// <summary>아직 AI에 몸을 안 넘겼는가.</summary>
        public bool IsActive => Current != Phase.Done;

        /// <summary>걸어가는 목표 지점(지상 좌표).</summary>
        public Vector3 Target => target;

        /// <summary>남은 선딜레이. 0이면 이번 프레임에 깨어난다.</summary>
        public float HoldRemaining => holdLeft;

        public SpawnEntry(Vector3 target, float holdSeconds,
                          float arriveRadius = DefaultArriveRadius,
                          float walkTimeout = DefaultWalkTimeout)
        {
            this.target = new Vector3(target.x, 0f, target.z);
            this.holdSeconds = Mathf.Max(0f, holdSeconds);
            arriveRadiusSqr = Mathf.Max(0.01f, arriveRadius) * Mathf.Max(0.01f, arriveRadius);
            this.walkTimeout = Mathf.Max(0.1f, walkTimeout);
        }

        /// <summary>
        /// 한 프레임 진행하고 <b>이번 프레임의 이동 방향</b>을 돌려준다.
        /// 서 있어야 하는 구간과 끝난 뒤에는 <see cref="Vector3.zero"/>다.
        /// </summary>
        /// <param name="position">지금 위치. 높이는 무시한다.</param>
        public Vector3 Tick(Vector3 position, float dt)
        {
            if (Current == Phase.Done) return Vector3.zero;

            if (Current == Phase.Holding)
            {
                holdLeft -= dt;
                if (holdLeft <= 0f) Current = Phase.Done;
                return Vector3.zero;
            }

            Vector3 to = target - position;
            to.y = 0f;

            walked += dt;

            if (to.sqrMagnitude <= arriveRadiusSqr || walked >= walkTimeout)
            {
                Arrive();
                return Vector3.zero;
            }

            return to.normalized;
        }

        /// <summary>연출을 지금 끝낸다. 맞았거나 스테이지가 끝났을 때 부른다.</summary>
        public void Finish() => Current = Phase.Done;

        private void Arrive()
        {
            if (holdSeconds <= 0f)
            {
                Current = Phase.Done;
                return;
            }

            Current = Phase.Holding;
            holdLeft = holdSeconds;
        }
    }
}
