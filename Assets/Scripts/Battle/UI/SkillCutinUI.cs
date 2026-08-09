using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스킬 발동 직전에 화면 왼쪽으로 밀려 들어오는 컷인.
    /// 초상화가 먼저, 스킬명이 <see cref="LabelDelay"/>만큼 뒤따라 들어와 층을 이룬다.
    /// </summary>
    public class SkillCutinUI : MonoBehaviour
    {
        /// <summary>화면 밖 → 제자리. ease-out.</summary>
        public const float SlideIn = 0.12f;

        /// <summary>제자리에 머무는 시간. 스킬명을 읽을 여유.</summary>
        public const float Hold = 0.25f;

        /// <summary>제자리 → 화면 밖. ease-in.</summary>
        public const float SlideOut = 0.12f;

        /// <summary>스킬명 라벨이 초상화보다 늦게 들어오는 간격.</summary>
        public const float LabelDelay = 0.06f;

        /// <summary>컷인 전체 길이. 늦게 나가는 라벨까지 기다린다.</summary>
        public static float Duration => SlideIn + Hold + SlideOut + LabelDelay;

        /// <summary>
        /// 경과 시간을 0(화면 밖 대기 위치) ~ 1(등장 위치)로 접는다.
        /// <paramref name="delay"/>만큼 곡선 전체가 뒤로 밀린다 — 라벨이 초상화를 뒤따르게.
        /// </summary>
        public static float SlideAmount(float elapsed, float delay)
        {
            float t = elapsed - delay;

            if (t <= 0f) return 0f;

            if (t < SlideIn)
            {
                // ease-out: 빠르게 들어와 부드럽게 멈춘다.
                float x = t / SlideIn;
                return 1f - (1f - x) * (1f - x);
            }

            if (t < SlideIn + Hold) return 1f;

            if (t < SlideIn + Hold + SlideOut)
            {
                // ease-in: 천천히 떨어졌다 빠르게 빠진다.
                float x = (t - SlideIn - Hold) / SlideOut;
                return 1f - x * x;
            }

            return 0f;
        }
    }
}
