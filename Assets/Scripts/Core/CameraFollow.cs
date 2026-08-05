using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 벨트스크롤 카메라. 좌우(X)만 따라가고 세로는 고정한다.
    /// 깊이(Z)는 <see cref="BeltScrollView"/>가 화면 세로로 환산하므로 카메라는 기울이지 않는다.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("카메라가 머무는 세로 위치. 지면보다 살짝 위.")]
        [SerializeField] private float fixedY = 1.5f;
        [SerializeField] private float fixedZ = -10f;
        [Tooltip("따라붙는 속도. 클수록 즉각적.")]
        [SerializeField] private float smooth = 6f;
        [Tooltip("스테이지 좌우 경계. min >= max면 제한하지 않는다.")]
        [SerializeField] private float minX = 0f;
        [SerializeField] private float maxX = 0f;

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

            float x = target.position.x;
            if (maxX > minX) x = Mathf.Clamp(x, minX, maxX);

            var goal = new Vector3(x, fixedY, fixedZ);

            // 불릿타임에 멈추면 조준이 불편하다. 카메라는 실제 시간으로 움직인다.
            transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        }

        public void SetTarget(Transform t) => target = t;

        /// <summary>즉시 목표 지점으로 붙인다. 스테이지 진입 시.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            float x = target.position.x;
            if (maxX > minX) x = Mathf.Clamp(x, minX, maxX);

            transform.position = new Vector3(x, fixedY, fixedZ);
        }
    }
}
