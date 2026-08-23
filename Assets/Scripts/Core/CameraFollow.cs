using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 벨트스크롤 카메라. 좌우(X)만 따라가고 세로는 고정한다.
    /// 깊이(Z)는 <see cref="BeltScrollView"/>가 화면 세로로 환산하므로 카메라는 기울이지 않는다.
    ///
    /// <b>아레나 락을 위한 분기가 없다.</b> 통로든 아레나든 하는 일은 똑같다 —
    /// 데드존을 적용하고, <see cref="StageBounds"/>가 들고 있는 구간 밖을 보지 않도록 물린다.
    /// 아레나는 그 구간이 화면 폭보다 좁을 뿐이고, 그러면 클램프가 한 점으로 접혀
    /// 카메라가 결과적으로 안 움직인다(<see cref="CameraFrameRules.ClampToSection"/>).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("카메라가 머무는 세로 위치. 지면보다 살짝 위.")]
        [SerializeField] private float fixedY = 1.5f;
        [SerializeField] private float fixedZ = -10f;
        [Tooltip("따라붙는 속도. 클수록 즉각적.")]
        [SerializeField] private float smooth = 6f;

        [Tooltip("화면 중앙 밴드의 반폭. 목표가 이 밴드를 벗어날 때만 카메라가 밀린다. 0이면 항상 따라간다.")]
        [SerializeField] private float deadZoneHalfWidth = 1.5f;

        [Tooltip("StageBounds가 없는 씬에서 쓰는 고정 경계. min >= max면 제한하지 않는다.")]
        [SerializeField] private float minX = 0f;
        [SerializeField] private float maxX = 0f;

        private Camera cam;
        private StageBounds bounds;

        private void Awake()
        {
            cam = GetComponent<Camera>();

            if (target == null)
            {
                Player p = FindAnyObjectByType<Player>();
                if (p != null) target = p.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            transform.position = Frame(transform.position.x, Time.unscaledDeltaTime);
        }

        /// <summary>즉시 목표 지점으로 붙인다. 스테이지 진입 시.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            // dt를 크게 줘서 Lerp가 그대로 목표에 닿게 한다.
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
            float x = ClampToStage(desired);

            var goal = new Vector3(x, fixedY, fixedZ);
            var from = new Vector3(fromX, transform.position.y, transform.position.z);

            return Vector3.Lerp(from, goal, 1f - Mathf.Exp(-smooth * dt));
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
