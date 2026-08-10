using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 논리 좌표(X 좌우 / Z 깊이 / Y 높이) ↔ 화면에 실제로 그리는 위치의 변환.
    ///
    /// 카메라를 기울이지 않으므로 <b>깊이 Z는 그대로 두면 화면에서 사라진다</b>.
    /// 그래서 Z를 화면 세로로 접어 넣는다: <c>screenY = Y + Z · DepthToScreen</c>.
    /// 캐릭터 · 그림자 · 조준점이 <b>전부 이 함수를 거쳐야</b> 같은 공간에 놓인다.
    /// </summary>
    public static class BeltScroll
    {
        /// <summary>
        /// 깊이가 화면 세로로 환산되는 비율. tan θ에 해당한다.
        /// <see cref="BeltScrollView"/>가 자기 설정값으로 덮어쓴다.
        /// </summary>
        public static float DepthToScreen { get; set; } = 0.5f;

        /// <summary>
        /// 깊이가 화면 <b>가로</b>로 환산되는 비율. 논리 좌표의 사각형을 화면에서
        /// 평행사변형으로 민다(oblique 투영) — 바닥이 기울어 보여 깊이가 살아난다.
        /// 0이면 정면 투영.
        /// </summary>
        public static float DepthToScreenX { get; set; } = 0.45f;

        /// <summary>
        /// 깊이 1당 줄어드는 표시 배율. z=0이 기준 1.0이다.
        /// <see cref="BeltScrollView"/>가 자기 설정값으로 덮어쓴다.
        /// </summary>
        public static float DepthScalePerUnit { get; set; } = 0.06f;

        /// <summary>
        /// 배율 하한. 방 밖으로 밀려나거나 계수를 크게 잡으면 1 - z·k가 0 아래로 내려가
        /// 스프라이트가 좌우로 뒤집히거나 사라진다.
        /// </summary>
        public const float MinScale = 0.05f;

        /// <summary>
        /// 깊이에 따른 표시 배율. <b>논리 좌표에는 영향이 없다</b> —
        /// 판정은 3D 콜라이더가 그대로 하고, 이건 그리는 크기만 바꾼다.
        /// </summary>
        public static float ScaleAt(float z) => Mathf.Max(MinScale, 1f - z * DepthScalePerUnit);

        /// <summary>논리 좌표 → 그리는 위치. height는 점프 높이.</summary>
        public static Vector3 ToView(Vector3 ground, float height = 0f)
            => new Vector3(
                ground.x + ground.z * DepthToScreenX,
                ground.y + ground.z * DepthToScreen + height,
                ground.z);

        /// <summary>
        /// 그리는 위치 → 논리 좌표. 마우스 피킹처럼 화면에서 거꾸로 들어올 때 쓴다.
        /// 바닥(height 0)이라고 가정한다.
        ///
        /// 가로 밀림이 섞여 있어도 역변환은 성립한다 — 세로에서 깊이를 먼저 되찾고,
        /// 그 깊이로 가로에서 밀림을 빼면 된다. 순서를 뒤집으면 못 푼다.
        /// </summary>
        public static Vector3 ToGround(Vector3 view, float groundY = 0f)
        {
            if (DepthToScreen <= 0.0001f) return new Vector3(view.x, groundY, view.z);

            float z = (view.y - groundY) / DepthToScreen;
            return new Vector3(view.x - z * DepthToScreenX, groundY, z);
        }
    }
}
