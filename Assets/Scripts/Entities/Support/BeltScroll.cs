using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 벨트스크롤 공간의 기준. 논리 좌표(X 좌우 / Z 깊이 / Y 높이)가 <b>곧 월드 좌표</b>다.
    ///
    /// 깊이를 화면에서 보이게 하는 일은 <b>카메라가 한다</b> — X축으로만 <see cref="DefaultTiltDegrees"/>
    /// 만큼 숙인다(피치). 요·롤은 0이다. 그래야 카메라 right가 (1,0,0)으로 남아
    /// <c>screenX = x</c>가 되고, 깊이가 가로로 새지 않는다.
    ///
    /// 예전에는 카메라를 안 기울이고 Z를 화면 세로·가로로 손수 접었다.
    /// 그 가로 접기(oblique shear)가 "위로 걸으면 대각선으로 간다"의 원인이었고,
    /// 논리 좌표의 사각형을 화면에서 평행사변형으로 만들어 바닥 그림과도 어긋났다.
    /// 지금은 <see cref="ToView"/>가 높이만 더하는 항등식이다.
    /// </summary>
    public static class BeltScroll
    {
        /// <summary>
        /// 카메라 피치. 깊이 z는 화면 세로로 <c>z·sinθ</c>, 높이 y는 <c>y·cosθ</c>로 보인다.
        /// 50°에서 방 깊이 ±3이 화면 ±2.30, 점프 높이는 0.64배로 보인다.
        /// </summary>
        public const float DefaultTiltDegrees = 50f;

        private static Camera cached;

        /// <summary>
        /// 기준 카메라. 빌보드와 화면 축을 여기서 얻는다.
        ///
        /// 각도를 static 필드로 따로 들고 있지 않는다 — 씬의 카메라가 유일한 출처여야
        /// 인스펙터에서 기울기를 만졌을 때 스프라이트가 즉시 따라온다.
        /// </summary>
        public static Camera Cam
        {
            get
            {
                if (cached == null) cached = Camera.main;
                return cached;
            }
            set => cached = value;
        }

        /// <summary>
        /// 카메라를 정면으로 마주 보는 회전. 스프라이트 · 텍스트가 전부 이걸 쓴다.
        /// 카메라가 없으면(테스트) 항등 회전 — 기울기 0과 같다.
        /// </summary>
        public static Quaternion Billboard
        {
            get
            {
                Camera c = Cam;
                return c != null ? c.transform.rotation : Quaternion.identity;
            }
        }

        /// <summary>
        /// 화면 위쪽에 해당하는 월드 방향 <c>(0, cosθ, sinθ)</c>.
        /// 머리 위 게이지처럼 <b>화면에서</b> 일정하게 띄워야 하는 것들이 쓴다 —
        /// 월드 Y로 올리면 기울기만큼 짧아져 뒤쪽 캐릭터에서 라벨이 머리에 박힌다.
        /// </summary>
        public static Vector3 ScreenUp
        {
            get
            {
                Camera c = Cam;
                return c != null ? c.transform.up : Vector3.up;
            }
        }

        /// <summary>화면 오른쪽에 해당하는 월드 방향. 피치만 주므로 사실상 (1,0,0)이다.</summary>
        public static Vector3 ScreenRight
        {
            get
            {
                Camera c = Cam;
                return c != null ? c.transform.right : Vector3.right;
            }
        }

        /// <summary>
        /// 깊이 1당 줄어드는 표시 배율. z=0이 기준 1.0이다.
        ///
        /// 카메라가 직교라 <b>진짜 원근은 없다</b>. 뒤쪽이 작아 보이는 건 전부 이 흉내다.
        /// 끄고 싶으면 0으로 두면 된다.
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

        /// <summary>
        /// 논리 좌표 → 그리는 위치. height는 점프 높이다.
        ///
        /// 카메라가 깊이를 처리하므로 <b>더는 접지 않는다</b>. 높이를 월드 Y로 얹기만 한다.
        /// 호출부를 남겨 둔 이유는 "여기가 그리는 위치를 정하는 자리"라는 표시가 필요해서다.
        /// </summary>
        public static Vector3 ToView(Vector3 ground, float height = 0f)
            => ground + Vector3.up * height;

        /// <summary>그리는 위치 → 바닥 좌표. 높이만 떨군다.</summary>
        public static Vector3 ToGround(Vector3 world, float groundY = 0f)
            => new Vector3(world.x, groundY, world.z);

        /// <summary>
        /// 화면 좌표 → 바닥 좌표. 마우스 피킹이 쓴다.
        ///
        /// 역행렬을 손으로 풀지 않는다 — 카메라에서 레이를 쏴 <c>y = groundY</c> 평면과
        /// 만나는 점을 잡는다. 기울기를 바꿔도 이 함수는 고칠 데가 없다.
        /// </summary>
        public static Vector3 ScreenToGround(Camera cam, Vector2 screenPos, float groundY = 0f)
        {
            if (cam == null) return new Vector3(0f, groundY, 0f);

            var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            Ray ray = cam.ScreenPointToRay(screenPos);

            // 직교 카메라를 바닥과 나란히 두지 않는 한 반드시 만난다. 못 만나면 원점으로 접는다.
            return plane.Raycast(ray, out float t) ? ray.GetPoint(t) : new Vector3(0f, groundY, 0f);
        }
    }
}
