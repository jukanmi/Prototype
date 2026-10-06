using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 테스트용 카메라 리그.
    ///
    /// <see cref="BeltScroll.Cam"/>은 비어 있으면 <c>Camera.main</c>을 집는다 — 그러면
    /// 그때 열려 있던 씬에 따라 빌보드와 화면 축이 달라져 테스트가 흔들린다.
    /// 기울기를 검증하는 쪽은 반드시 여기서 명시적으로 물려 준다.
    /// </summary>
    internal static class BeltScrollTestCamera
    {
        public static float Tilt => BeltScroll.DefaultTiltDegrees;

        /// <summary>sin θ — 바닥 깊이 1이 화면 세로로 환산되는 비율.</summary>
        public static float Sin => Mathf.Sin(Tilt * Mathf.Deg2Rad);

        /// <summary>cos θ — 월드 높이 1이 화면 세로로 환산되는 비율.</summary>
        public static float Cos => Mathf.Cos(Tilt * Mathf.Deg2Rad);

        public static GameObject Attach() => Attach(Tilt);

        public static GameObject Attach(float tiltDegrees)
        {
            var go = new GameObject("BeltScrollTestCamera");
            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;

            // X축 하나만 돌린다. 요·롤이 섞이면 화면 가로에 깊이가 새어 들어온다.
            go.transform.rotation = Quaternion.Euler(tiltDegrees, 0f, 0f);

            BeltScroll.Cam = cam;
            return go;
        }

        public static void Detach(GameObject rig)
        {
            BeltScroll.Cam = null;
            if (rig != null) Object.DestroyImmediate(rig);
        }
    }
}
