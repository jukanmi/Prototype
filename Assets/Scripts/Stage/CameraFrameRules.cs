using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 카메라가 이번 프레임에 어디를 봐야 하는가. <b>순수 함수</b>다.
    ///
    /// 두 규칙뿐이고 둘 다 눈으로 검증하기 어렵다 — 카메라 버그는 "뭔가 이상한데"로만
    /// 보이지 숫자가 안 보이기 때문이다. 그래서 여기 떼어 놓고 테스트가 직접 부른다.
    /// </summary>
    public static class CameraFrameRules
    {
        /// <summary>
        /// 구간 밖을 보지 않도록 카메라 X를 물린다.
        ///
        /// <b>구간이 화면보다 좁으면 중앙에 고정한다.</b> 이게 아레나 락의 정체다 —
        /// 락을 위한 코드가 따로 있는 게 아니라, 아레나 경계가 화면 폭보다 좁아서
        /// 클램프 구간이 한 점으로 접힌 것뿐이다. 데드존 로직은 그대로 돌아도
        /// 결과가 안 바뀐다.
        /// </summary>
        /// <param name="desiredX">따라가고 싶은 좌표(보통 플레이어).</param>
        /// <param name="halfWidth">카메라가 한쪽으로 보는 폭. 직교 카메라면 size × aspect.</param>
        public static float ClampToSection(float desiredX, float sectionMin, float sectionMax, float halfWidth)
        {
            float min = sectionMin + halfWidth;
            float max = sectionMax - halfWidth;

            // 구간이 화면보다 좁다 — 어디를 봐도 밖이 보이므로 가운데가 가장 낫다.
            if (min >= max) return (sectionMin + sectionMax) * 0.5f;

            return Mathf.Clamp(desiredX, min, max);
        }

        /// <summary>
        /// 구간이 화면보다 좁아 카메라가 결국 고정되는가. 표시·디버그용 질문이다.
        /// </summary>
        public static bool IsLocked(float sectionMin, float sectionMax, float halfWidth)
            => sectionMin + halfWidth >= sectionMax - halfWidth;

        /// <summary>
        /// 데드존. 목표가 화면 중앙 밴드를 <b>벗어날 때만</b> 카메라를 민다.
        ///
        /// 없으면 통로에서 플레이어가 한 발짝 움직일 때마다 배경이 따라 흔들려서
        /// 눈이 피로하다. 밴드 안이면 지금 자리를 그대로 돌려준다.
        /// </summary>
        /// <returns>카메라가 향해야 할 X.</returns>
        public static float ApplyDeadZone(float cameraX, float targetX, float halfBand)
        {
            if (halfBand <= 0f) return targetX;

            float delta = targetX - cameraX;

            if (delta > halfBand) return targetX - halfBand;
            if (delta < -halfBand) return targetX + halfBand;

            return cameraX;
        }

        /// <summary>
        /// 직교 카메라가 한쪽으로 보는 폭. <paramref name="aspect"/>는 가로/세로다.
        /// </summary>
        public static float HalfWidth(float orthographicSize, float aspect)
            => Mathf.Max(0f, orthographicSize) * Mathf.Max(0.0001f, aspect);
    }
}
