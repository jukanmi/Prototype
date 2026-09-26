using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 게임플레이 전용 시간 배율. 결정 로그 ⑥ — Time.timeScale은 쓰지 않는다.
    /// UI와 애니메이션까지 멈추면 불릿타임 중 손패 조작이 불가능하기 때문.
    /// </summary>
    public static class TimeControl
    {
        /// <summary>0이면 완전 정지. BulletTimeController가 갱신한다.</summary>
        public static float Scale { get; set; } = 1f;

        /// <summary>엔티티 · 상태머신 · 물리에 주입할 dt.</summary>
        public static float DeltaTime => Time.deltaTime * Scale;

        /// <summary>UI · 조준 · 입력용 dt. 배율 영향을 받지 않는다.</summary>
        public static float UnscaledDeltaTime => Time.deltaTime;

        public static bool IsFrozen => Scale <= 0.0001f;

        public static void Reset() => Scale = 1f;

        /// <summary>
        /// Domain Reload가 꺼져 있으면 static이 플레이 세션을 넘어 살아남는다.
        /// 불릿타임 중에 정지하면 Scale이 0인 채로 남아 다음 플레이가 멈춘 상태로 시작한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
