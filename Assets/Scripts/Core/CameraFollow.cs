using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 벨트스크롤 카메라. 좌우(X)만 따라가고 높이 · 깊이는 고정한다.
    ///
    /// 회전은 건드리지 않는다 — 깊이를 화면 세로로 눕히는 것이 이 카메라의 기울기 자체라,
    /// 따라다니면서 각도가 흔들리면 <see cref="BeltScrollView"/>의 Y축 빌보드 전제가 깨진다.
    /// 각도 · 거리는 <c>SceneLayoutBuilder.SetupCamera</c>가 세운다.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("카메라가 머무는 세로 위치. 기울인 만큼 지면 위로 올라가 있다.")]
        [SerializeField] private float fixedY = 10.67f;
        [SerializeField] private float fixedZ = -10.4f;
        [Tooltip("따라붙는 속도. 클수록 즉각적.")]
        [SerializeField] private float smooth = 6f;
        [Tooltip("스테이지 좌우 경계. min >= max면 제한하지 않는다. 보통 RoomBounds가 채운다.")]
        [SerializeField] private float minX = 0f;
        [SerializeField] private float maxX = 0f;

        /// <summary>
        /// <see cref="SetBounds"/>로 경계를 받았는지. 인스펙터 기본값(0, 0)과
        /// "맵이 화면보다 좁아 min == max"를 구별해야 해서 따로 든다.
        /// </summary>
        private bool bounded;

        private void Awake()
        {
            if (target == null)
            {
                Player p = FindAnyObjectByType<Player>();
                if (p != null) target = p.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            var goal = new Vector3(ClampedX(), fixedY, fixedZ);

            // 불릿타임에 멈추면 조준이 불편하다. 카메라는 실제 시간으로 움직인다.
            transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        }

        public void SetTarget(Transform t) => target = t;

        /// <summary>
        /// 스테이지 경계에서 이동 범위를 받는다(<see cref="RoomBounds.Apply"/>).
        ///
        /// 맵이 화면보다 좁으면 범위가 뒤집혀 들어온다. 그때는 가운데 고정이 정답이다 —
        /// 뒤집힌 채로 두면 카메라가 맵 양쪽 밖을 번갈아 비춘다.
        /// </summary>
        public void SetBounds(float min, float max)
        {
            if (min > max)
            {
                float center = (min + max) * 0.5f;
                min = max = center;
            }

            minX = min;
            maxX = max;
            bounded = true;
        }

        private float ClampedX()
        {
            float x = target.position.x;
            if (bounded || maxX > minX) x = Mathf.Clamp(x, minX, maxX);

            return x;
        }

        /// <summary>즉시 목표 지점으로 붙인다. 스테이지 진입 시.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            transform.position = new Vector3(ClampedX(), fixedY, fixedZ);
        }
    }
}
