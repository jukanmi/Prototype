using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 밟을 수 있는 바닥 한 장. 붙인 오브젝트의 XZ 범위가 곧 발판이다.
    ///
    /// 예전에는 바닥이 <c>Physics.groundY</c> float 하나였다 — y=0 무한 평면이라
    /// 바닥 그림을 벗어나도 그대로 서 있었고, 밖으로 못 나가게 막는 건 벽 콜라이더뿐이었다.
    /// 낙차를 넣으려면 "여기 발판이 있나"를 물을 대상이 필요하다. 이게 그 대상이다.
    ///
    /// <b>콜라이더가 아니다.</b> 유니티 물리에 맡기면 가장자리에서 미끄러지고 캡슐이 걸린다.
    /// 판정은 중심 한 점의 사각형 포함 검사다(<see cref="GroundRules"/>).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GroundPlate : MonoBehaviour
    {
        [Tooltip("발판 반폭(X)과 반깊이(Z). 둘 다 0이면 이 오브젝트의 Renderer 범위에서 잰다.")]
        [SerializeField] private Vector2 halfExtents = Vector2.zero;

        [Tooltip("발판 윗면 높이를 오브젝트 위치에서 얼마나 올릴지. 보통 0.")]
        [SerializeField] private float surfaceOffsetY = 0f;

        [Tooltip("씬 뷰에 발판 범위를 그린다.")]
        [SerializeField] private bool drawGizmo = true;

        private GroundRect cached;
        private int cachedFrame = -1;

        /// <summary>
        /// 월드 기준 발판 범위. 한 프레임에 한 번만 다시 잰다 —
        /// 유닛 수만큼 질의가 들어오는데 Renderer.bounds는 공짜가 아니다.
        /// </summary>
        public GroundRect Rect
        {
            get
            {
                if (cachedFrame == Time.frameCount) return cached;

                cached = Measure();
                cachedFrame = Time.frameCount;
                return cached;
            }
        }

        private void OnEnable() => GroundRegistry.Register(this);
        private void OnDisable() => GroundRegistry.Unregister(this);

        /// <summary>인스펙터에서 크기를 만지면 다음 질의에 바로 반영되게 한다.</summary>
        private void OnValidate() => cachedFrame = -1;

        private GroundRect Measure()
        {
            Vector3 c = transform.position;
            c.y += surfaceOffsetY;

            if (halfExtents.x > 0.0001f && halfExtents.y > 0.0001f)
                return new GroundRect(c.x - halfExtents.x, c.x + halfExtents.x,
                                      c.z - halfExtents.y, c.z + halfExtents.y, c.y);

            // 크기를 안 적었으면 그림에서 잰다. 바닥 판을 늘리면 발판도 같이 늘어난다 —
            // 그림과 판정이 갈라지는 것이 애초에 이 버그의 원인이었다.
            var r = GetComponent<Renderer>();
            if (r != null)
            {
                Bounds b = r.bounds;
                return new GroundRect(b.min.x, b.max.x, b.min.z, b.max.z, b.max.y + surfaceOffsetY);
            }

            // 마지막 보루. 크기를 못 재면 없는 발판으로 두는 편이 조용히 틀리는 것보다 낫다.
            return new GroundRect(c.x, c.x, c.z, c.z, c.y);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;

            GroundRect r = Measure();
            var center = new Vector3((r.MinX + r.MaxX) * 0.5f, r.Y, (r.MinZ + r.MaxZ) * 0.5f);
            var size = new Vector3(r.MaxX - r.MinX, 0.02f, r.MaxZ - r.MinZ);

            Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
