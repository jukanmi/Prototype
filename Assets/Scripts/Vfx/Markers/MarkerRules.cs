using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 화면 밖 대상을 <b>가장자리 어디에 물려</b> 표시할 것인가. <b>순수 함수</b>다.
    ///
    /// 벨트스크롤이라 여기도 한 축으로 접힌다 — 카메라는 X로만 움직이고 깊이(Z)는 언제나
    /// 화면 안이므로, 밖으로 나가는 것은 X뿐이고 <b>깊이는 그대로 그린다</b>.
    /// 그래서 같은 쪽 표식 둘을 갈라 보이게 하는 것도 깊이다
    /// (<see cref="StandbyRules"/>가 대기 칸을 깊이로 벌려 두는 이유).
    ///
    /// 카메라를 인자로 받는다 — 씬 없이 검증하기 위해서이고,
    /// <see cref="CameraFrameRules"/> · <see cref="EntranceRules"/>와 같은 관용구다.
    /// </summary>
    public static class MarkerRules
    {
        /// <summary>가장자리에서 안쪽으로 물리는 양. 표식이 화면 밖으로 반쯤 잘리지 않게.</summary>
        public const float EdgeInset = 0.9f;

        /// <summary>
        /// 표식을 그릴 X. 화면 안이면 대상 좌표 그대로, 밖이면 <b>가장자리에 물린다</b>.
        ///
        /// 물리는 것이 핵심이다 — 화면 밖 좌표에 그대로 그리면 표식도 같이 안 보이고,
        /// 그러면 "어디 있는지 모르겠다"를 풀려던 것이 그대로 남는다.
        /// </summary>
        public static float ClampToEdge(float x, float cameraX, float halfWidth, float inset = EdgeInset)
        {
            float edge = Mathf.Max(0f, halfWidth - Mathf.Max(0f, inset));

            return Mathf.Clamp(x, cameraX - edge, cameraX + edge);
        }

        /// <summary>표식이 놓일 지상 좌표. 깊이는 손대지 않는다.</summary>
        public static Vector3 Anchor(Vector3 ground, float cameraX, float halfWidth, float inset = EdgeInset)
            => new Vector3(ClampToEdge(ground.x, cameraX, halfWidth, inset), ground.y, ground.z);

        /// <summary>대상이 화면 밖이라 표식이 가장자리에 물렸는가. 화살표를 눕힐지 결정한다.</summary>
        public static bool IsClamped(float x, float cameraX, float halfWidth, float inset = EdgeInset)
            => !Mathf.Approximately(x, ClampToEdge(x, cameraX, halfWidth, inset));

        /// <summary>
        /// 대상이 어느 쪽으로 벗어났는가. 화면 안이면 0, 오른쪽 밖이면 +1, 왼쪽 밖이면 -1.
        /// </summary>
        public static int OffscreenSide(float x, float cameraX, float halfWidth, float inset = EdgeInset)
        {
            float edge = Mathf.Max(0f, halfWidth - Mathf.Max(0f, inset));

            if (x > cameraX + edge) return 1;
            if (x < cameraX - edge) return -1;
            return 0;
        }

        /// <summary>
        /// 표식이 가리킬 방향(도). 0°가 화면 위(<c>▲</c> 기준)이고 시계 방향으로 돈다.
        ///
        /// 화면 안이면 위를 가리킨다 — 머리 위 표식이므로 아래를 짚는 것이 자연스럽지만,
        /// 그건 글리프(<c>▼</c>)가 이미 하고 있어서 회전은 0이 기본이다.
        /// 밖이면 그쪽 가로 방향으로 눕는다.
        /// </summary>
        public static float PointerAngle(int side)
        {
            if (side > 0) return 90f;    // 오른쪽
            if (side < 0) return -90f;   // 왼쪽
            return 0f;
        }

        /// <summary>
        /// 대상까지의 가로 거리. 표식 옆에 "얼마나 멀리"를 적거나 크기를 줄일 때 쓴다.
        /// 화면 안이면 0이다.
        /// </summary>
        public static float OffscreenDistance(float x, float cameraX, float halfWidth, float inset = EdgeInset)
        {
            float edge = Mathf.Max(0f, halfWidth - Mathf.Max(0f, inset));

            if (x > cameraX + edge) return x - (cameraX + edge);
            if (x < cameraX - edge) return (cameraX - edge) - x;
            return 0f;
        }
    }
}
