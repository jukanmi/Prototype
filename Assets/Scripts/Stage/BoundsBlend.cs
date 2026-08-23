using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 경계값 하나를 부드럽게 갈아 끼우는 <b>순수 상태</b>.
    ///
    /// 라운드마다 바뀌는 것은 <b>이 안에 든 경계값 하나뿐</b>이다 — 카메라 코드에는
    /// "지금 아레나인가 통로인가" 하는 분기가 없다. 아레나에 들어가면 경계가 좁아지고,
    /// 좁아진 경계에 클램프가 걸려 카메라가 결과적으로 안 움직인다. 분기를 두지 않는 것이
    /// 이 방식의 전부이자 장점이다.
    ///
    /// <b>보간은 스케일 안 된 시간으로 돌린다.</b> 라운드가 끝나는 순간은 마지막 적이
    /// 죽는 순간이라 불릿타임이 걸려 있기 쉬운데, 게임 시간으로 돌리면 그 동안 카메라가
    /// 굳어 버린다. 경계 전환은 연출이지 게임플레이가 아니다.
    /// </summary>
    public struct BoundsBlend
    {
        /// <summary>기본 전환 시간. 이보다 길면 늘어지고, 짧으면 뚝 끊긴다.</summary>
        public const float DefaultDuration = 0.3f;

        private float fromMin, fromMax;
        private float toMin, toMax;
        private float duration;
        private float elapsed;

        /// <summary>0~1. 보간이 없으면 1.</summary>
        public float Progress => duration <= 0.0001f ? 1f : Mathf.Clamp01(elapsed / duration);

        public bool IsBlending => Progress < 1f;

        public float Min => Mathf.Lerp(fromMin, toMin, Smooth(Progress));
        public float Max => Mathf.Lerp(fromMax, toMax, Smooth(Progress));

        /// <summary>목표 경계. 보간 중에도 "어디로 가는 중인지"는 이 값이다.</summary>
        public float TargetMin => toMin;
        public float TargetMax => toMax;

        /// <summary>보간 없이 즉시 맞춘다. 스테이지가 처음 올라올 때.</summary>
        public void Snap(float min, float max)
        {
            fromMin = toMin = min;
            fromMax = toMax = max;
            duration = 0f;
            elapsed = 0f;
        }

        /// <summary>
        /// 새 경계로 넘어간다. <b>지금 보이는 값에서 출발한다</b> —
        /// 목표값에서 출발하면 전환 도중에 또 전환이 걸릴 때 카메라가 튄다.
        /// 같은 목표를 다시 주면 아무 일도 하지 않는다(매 프레임 부르는 자리다).
        /// </summary>
        public void To(float min, float max, float seconds = DefaultDuration)
        {
            if (Mathf.Approximately(toMin, min) && Mathf.Approximately(toMax, max)) return;

            fromMin = Min;
            fromMax = Max;
            toMin = min;
            toMax = max;
            duration = Mathf.Max(0f, seconds);
            elapsed = 0f;
        }

        public void Tick(float unscaledDt)
        {
            if (!IsBlending) return;
            elapsed += Mathf.Max(0f, unscaledDt);
        }

        /// <summary>양끝을 눕힌 곡선. 선형으로 두면 전환의 시작과 끝이 툭 걸린다.</summary>
        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
