using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 벨트스크롤 카메라. 좌우(X)만 따라가고 세로는 고정한다.
    ///
    /// <b>X축으로만 기울인다</b>(피치). 요·롤은 0이다 — 요를 섞으면 카메라 right에 z가 들어가
    /// <c>screenX = x·cosψ + z·sinψ</c>가 되고, "위로 걸으면 대각선으로 간다"가 되돌아온다.
    /// 피치만 주면 right가 (1,0,0)으로 남아 깊이가 가로로 새지 않는다.
    ///
    /// <b>아레나 락을 위한 분기가 없다.</b> 통로든 아레나든 하는 일은 똑같다 —
    /// 데드존을 적용하고, <see cref="StageBounds"/>가 들고 있는 구간 밖을 보지 않도록 물린다.
    /// 아레나는 그 구간이 화면 폭보다 좁을 뿐이고, 그러면 클램프가 한 점으로 접혀
    /// 카메라가 결과적으로 안 움직인다(<see cref="CameraFrameRules.ClampToSection"/>).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Tooltip("바닥을 내려다보는 각도. X축 피치. 0이면 정면. 50°에서 방 깊이 ±3이 화면 ±2.3으로 보인다.")]
        [Range(0f, 89f)]
        [SerializeField] private float tiltDegrees = BeltScroll.DefaultTiltDegrees;

        [Tooltip("바닥 중심에서 뒤로 물러나는 거리. 직교 카메라라 그림 크기와 무관하고, 클리핑 여유만 정한다.")]
        [SerializeField] private float distance = 10f;

        [Tooltip("화면 위쪽으로 프레임을 올리는 양. 지면이 화면 한가운데 오지 않게 한다.")]
        [SerializeField] private float framingLift = 0.5f;

        [Tooltip("따라붙는 속도. 클수록 즉각적.")]
        [SerializeField] private float smooth = 6f;

        [Tooltip("화면 중앙 밴드의 반폭. 목표가 이 밴드를 벗어날 때만 카메라가 밀린다. 0이면 항상 따라간다.")]
        [SerializeField] private float deadZoneHalfWidth = 1.5f;

        [Tooltip("StageBounds가 없는 씬에서 쓰는 고정 경계. min >= max면 제한하지 않는다.")]
        [SerializeField] private float minX = 0f;
        [SerializeField] private float maxX = 0f;

        private Camera cam;
        private StageBounds bounds;

        /// <summary>기울기만 담은 회전. 스프라이트 빌보드가 이걸 그대로 받아 쓴다.</summary>
        public Quaternion Rotation => Quaternion.Euler(tiltDegrees, 0f, 0f);

        /// <summary>
        /// 바닥의 <paramref name="pivotX"/>를 보게 하는 카메라 자리.
        ///
        /// 피치만 주므로 forward · up 모두 x 성분이 0이다 — 그래서 결과의 x가 곧 pivotX이고,
        /// 다음 프레임에 <c>transform.position.x</c>를 그대로 다시 읽어도 어긋나지 않는다.
        /// </summary>
        public Vector3 Rig(float pivotX)
        {
            Quaternion rot = Rotation;

            return new Vector3(pivotX, 0f, 0f)
                 - (rot * Vector3.forward) * distance
                 + (rot * Vector3.up) * framingLift;
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();

            if (target == null)
            {
                Player p = FindAnyObjectByType<Player>();
                if (p != null) target = p.transform;
            }
        }

        /// <summary>
        /// 인스펙터에서 각도를 만지는 즉시 반영한다.
        ///
        /// <c>[ExecuteAlways]</c>를 안 쓴다 — 그러면 에디트 모드에서도 매 프레임 카메라를
        /// 목표 쪽으로 끌어 씬이 계속 더티가 된다. 각도만 미리 보면 충분하다.
        /// </summary>
        private void OnValidate()
        {
            transform.rotation = Rotation;
            transform.position = Rig(transform.position.x);
        }

        private void LateUpdate()
        {
            transform.rotation = Rotation;

            if (target == null) return;

            transform.position = Frame(transform.position.x, Time.unscaledDeltaTime);
        }

        /// <summary>즉시 목표 지점으로 붙인다. 스테이지 진입 시.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            // dt를 크게 줘서 Lerp가 그대로 목표에 닿게 한다.
            transform.rotation = Rotation;
            transform.position = Frame(target.position.x, 1000f);
        }

        /// <summary>
        /// 이번 프레임에 카메라가 있어야 할 자리.
        ///
        /// <b>스케일 안 된 시간으로 움직인다.</b> 불릿타임 중에도 조준을 해야 하고,
        /// 라운드가 끝나는 순간은 마지막 적이 죽는 순간이라 시간이 늘어져 있기 쉬운데
        /// 거기서 카메라까지 굳으면 전환이 통째로 멎어 보인다.
        /// </summary>
        private Vector3 Frame(float fromX, float dt)
        {
            float desired = CameraFrameRules.ApplyDeadZone(fromX, target.position.x, deadZoneHalfWidth);
            float goalX = ClampToStage(desired);

            // 보간은 <b>바닥 위의 한 축</b>에서만 한다. 리그 위치를 통째로 Lerp하면
            // 기울기를 바꾸는 프레임에 카메라가 호를 그리며 흘러간다.
            float x = Mathf.Lerp(fromX, goalX, 1f - Mathf.Exp(-smooth * dt));

            return Rig(x);
        }

        private float ClampToStage(float desired)
        {
            StageBounds owner = Bounds();

            if (owner == null)
                return maxX > minX ? Mathf.Clamp(desired, minX, maxX) : desired;

            // 경계 보간을 여기서 한 번 민다. StageBounds도 자기 LateUpdate에서 돌리지만
            // 실행 순서가 뒤일 수 있어서, 그러면 카메라가 한 프레임 늦은 경계를 보고 떤다.
            owner.TickBlend();

            return CameraFrameRules.ClampToSection(desired, owner.CameraMin, owner.CameraMax, HalfWidth());
        }

        /// <summary>
        /// 카메라가 한쪽으로 보는 폭. 종횡비는 <b>매 프레임</b> 읽는다 —
        /// 에디터에서 게임 뷰 크기를 바꾸면 그대로 달라지고, 캐싱해 두면 그때부터
        /// 아레나 락이 어긋난 채로 돈다.
        ///
        /// 기울여도 이 값은 안 변한다. 피치는 화면 <b>가로</b>에 손대지 않는다.
        /// </summary>
        private float HalfWidth()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null || !cam.orthographic) return 0f;

            return CameraFrameRules.HalfWidth(cam.orthographicSize, cam.aspect);
        }

        private StageBounds Bounds()
        {
            if (bounds != null) return bounds;

            bounds = StageBounds.Instance;
            return bounds;
        }

        public void SetTarget(Transform t) => target = t;
    }
}
