using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스테이지 하나의 <b>경계 단일 출처</b>. 맵 프리팹 루트에 붙는다.
    ///
    /// 전에는 방 크기가 <c>SceneLayoutBuilder</c>의 <c>private const</c>였다. 맵이 코드로
    /// 생성되던 시절에는 그게 유일한 출처라 맞았지만, 맵을 프리팹으로 만들기 시작하면
    /// 스테이지마다 크기가 달라진다 — 상수로는 표현할 수가 없다.
    ///
    /// 실제로 캐릭터를 막는 것은 여기 적힌 숫자가 아니라 <c>Wall</c> 레이어 콜라이더다.
    /// 이 값은 그 콜라이더가 놓인 자리를 <b>다른 시스템에게 알려 주는</b> 용도다 —
    /// 카메라가 맵 밖을 비추지 않게 하고, 출구 문턱이 어디인지 알려 준다.
    /// 그래서 콜라이더를 옮기면 이 값도 같이 고쳐야 한다.
    /// </summary>
    public class RoomBounds : MonoBehaviour
    {
        [Tooltip("플레이 가능한 좌우 범위. 벽 콜라이더 안쪽 면과 맞춘다.")]
        [SerializeField] private float minX = -6f;
        [SerializeField] private float maxX =  6f;

        [Tooltip("벨트 깊이. 넓히면 원거리 회피가 쉬워져 전투 밸런스가 바뀐다.")]
        [SerializeField] private float minZ = -3f;
        [SerializeField] private float maxZ =  3f;

        [Tooltip("여기까지 걸어가면 다음 스테이지로 넘어간다. 오른쪽 벽에서 몸통 반지름만큼 안쪽.")]
        [SerializeField] private float exitX = 5f;

        public float MinX  => minX;
        public float MaxX  => maxX;
        public float MinZ  => minZ;
        public float MaxZ  => maxZ;
        public float ExitX => exitX;

        /// <summary>맵 가로 중심. 카메라가 맵보다 넓게 볼 때 여기 고정된다.</summary>
        public float CenterX => (minX + maxX) * 0.5f;

        private void Awake() => Apply(Camera.main);

        /// <summary>
        /// 카메라에 이동 범위를 물린다. 씬마다 <see cref="CameraFollow"/> 인스펙터를
        /// 손으로 맞추면 맵을 늘릴 때마다 빠뜨린다 — 맵이 직접 알려 주게 한다.
        /// </summary>
        public void Apply(Camera cam)
        {
            if (cam == null) return;

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) return;

            (float min, float max) = CameraClamp(cam);
            follow.SetBounds(min, max);
        }

        /// <summary>
        /// 카메라가 맵 밖(빈 공간)을 비추지 않는 X 범위.
        ///
        /// 화면 반폭을 고정값으로 못 쓴다 — 원근 카메라라 화면 위쪽(먼 쪽)이 더 넓고,
        /// 그 폭이 FOV · 피치 · 종횡비에 전부 달려 있다. 그래서 실제로 바닥 평면에
        /// 광선을 쏴서 재고, 가장 넓은 쪽을 쓴다.
        /// </summary>
        public (float min, float max) CameraClamp(Camera cam)
        {
            float half = GroundHalfWidth(cam);
            return (minX + half, maxX - half);
        }

        /// <summary>화면 네 귀퉁이가 바닥에 닿는 지점 중 카메라에서 가로로 가장 먼 거리.</summary>
        private static float GroundHalfWidth(Camera cam)
        {
            var ground = new Plane(Vector3.up, Vector3.zero);
            float camX = cam.transform.position.x;
            float widest = 0f;

            for (int i = 0; i < Corners.Length; i++)
            {
                Ray ray = cam.ViewportPointToRay(Corners[i]);

                // 지평선 위를 향하는 귀퉁이는 바닥에 안 닿는다. 그건 건너뛴다.
                if (!ground.Raycast(ray, out float distance)) continue;

                widest = Mathf.Max(widest, Mathf.Abs(ray.GetPoint(distance).x - camX));
            }

            return widest;
        }

        private static readonly Vector3[] Corners =
        {
            new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f),
            new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
        };

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            var center = new Vector3(CenterX, 0f, (minZ + maxZ) * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(maxX - minX, 0f, maxZ - minZ));

            Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.9f);
            Gizmos.DrawLine(new Vector3(exitX, 0f, minZ), new Vector3(exitX, 0f, maxZ));
        }
    }
}
