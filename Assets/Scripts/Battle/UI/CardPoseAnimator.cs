using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 목표 포즈를 향해 미끄러지는 지수 감쇠. 집기와 놓기가 <b>같은 코드 한 줄</b>이 된다 —
    /// 목표만 바꿔 주면 올라갈 때와 내려올 때의 감이 저절로 대칭이 된다.
    ///
    /// <c>Lerp(cur, target, speed * dt)</c>가 아니라 <c>1 - e^(-speed·dt)</c>를 쓴다.
    /// 프레임레이트가 흔들려도 같은 시간이 지나면 같은 자리에 온다 — 반 스텝 두 번이
    /// 한 스텝과 정확히 같다. 그리고 계수가 항상 1 미만이라 목표를 <b>넘어가지 않는다</b>.
    ///
    /// dt는 반드시 <see cref="TimeControl.UnscaledDeltaTime"/>을 넘긴다.
    /// 불릿타임 중에는 <see cref="TimeControl.Scale"/>이 0이라 배율 시간으로는 UI가 얼어붙는다.
    /// </summary>
    public static class CardPoseAnimator
    {
        /// <summary>클수록 빠르게 붙는다. 14면 체감상 0.2초쯤에 자리를 잡는다.</summary>
        public const float DefaultSpeed = 14f;

        /// <summary>게이지 채움용. 카드보다 느리게 흘러야 "빠져나간다"로 읽힌다.</summary>
        public const float GaugeSpeed = 7f;

        /// <summary>이번 프레임에 목표 쪽으로 얼마나 갈지. 0~1.</summary>
        public static float Weight(float dt, float speed)
        {
            if (dt <= 0f || speed <= 0f) return 0f;
            return 1f - Mathf.Exp(-speed * dt);
        }

        public static float Step(float cur, float target, float dt, float speed)
            => Mathf.Lerp(cur, target, Weight(dt, speed));

        public static CardPose Step(in CardPose cur, in CardPose target, float dt, float speed)
        {
            float t = Weight(dt, speed);

            return new CardPose(
                Vector2.Lerp(cur.pos, target.pos, t),
                Mathf.Lerp(cur.angle, target.angle, t),
                Mathf.Lerp(cur.scale, target.scale, t));
        }

        public static CardPose Step(in CardPose cur, in CardPose target, float dt)
            => Step(in cur, in target, dt, DefaultSpeed);

        /// <summary>목표에 사실상 닿았는지. 남은 미동을 끊어 정지 상태를 확정한다.</summary>
        public static bool IsSettled(in CardPose cur, in CardPose target)
            => Vector2.SqrMagnitude(cur.pos - target.pos) < 0.01f &&
               Mathf.Abs(cur.angle - target.angle) < 0.01f &&
               Mathf.Abs(cur.scale - target.scale) < 0.0005f;
    }
}
