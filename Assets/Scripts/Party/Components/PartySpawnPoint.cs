using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 이 스테이지에서 파티가 서는 자리. 씬마다 하나.
    ///
    /// <b>프리팹화가 깨뜨리는 유일한 좌표가 이것이다.</b> 지금까지 시작 자리는
    /// "씬에 놓인 <see cref="Player"/> 인스턴스의 위치"였다(방 왼쪽 <c>x = -3</c>).
    /// 파티가 <see cref="PartyAssembler"/> 안으로 들어가면 그 좌표는 프리팹 좌표(0,0,0)가 되어
    /// 모든 스테이지에서 방 한가운데서 시작하게 된다.
    ///
    /// 그래서 자리만 씬에 남긴다 — 방마다 다른 값이고, 방이 아는 값이다.
    /// </summary>
    public class PartySpawnPoint : MonoBehaviour
    {
        [Tooltip("처음 바라보는 방향. 벨트스크롤이라 보통 오른쪽(+X)이다.\n" +
                 "Physics.Teleport 는 방향을 안 건드리므로 여기서 따로 정해 줘야 한다 — " +
                 "안 그러면 첫 몸이 프리팹에 저장된 방향 그대로 등을 보이고 설 수 있다.")]
        [SerializeField] private Vector3 facing = Vector3.right;

        /// <summary>
        /// 씬에 <see cref="PartySpawnPoint"/>가 없을 때 쓰는 자리.
        /// 지금까지 <c>SceneLayoutBuilder</c>가 Player를 놓던 좌표와 같다.
        /// </summary>
        public static readonly Vector3 Fallback = new Vector3(-3f, 0f, 0f);

        /// <summary>바닥 좌표. Y는 접지가 다시 잡으므로 0으로 눌러 넘긴다.</summary>
        public Vector3 Ground => new Vector3(transform.position.x, 0f, transform.position.z);

        public Vector3 Facing
        {
            get
            {
                Vector3 f = facing;
                f.y = 0f;
                return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.right;
            }
        }

        /// <summary>
        /// 씬에서 자리를 찾는다. 없으면 <see cref="Fallback"/>.
        /// 둘 이상이면 첫 번째를 쓰고 경고한다 — 조용히 하나를 고르면
        /// "왜 저기서 시작하지"를 씬을 다 뒤져야 알게 된다.
        /// </summary>
        public static void Resolve(out Vector3 ground, out Vector3 facing)
        {
            PartySpawnPoint[] points = Object.FindObjectsByType<PartySpawnPoint>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            if (points == null || points.Length == 0)
            {
                ground = Fallback;
                facing = Vector3.right;
                return;
            }

            if (points.Length > 1)
                BattleLog.Warn(LogCategory.State,
                    $"PartySpawnPoint 가 {points.Length}개다 — '{points[0].name}'을(를) 쓴다.", points[0]);

            ground = points[0].Ground;
            facing = points[0].Facing;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.9f);
            Vector3 g = Ground;

            Gizmos.DrawWireSphere(g, 0.4f);
            Gizmos.DrawLine(g, g + Vector3.up * 1.6f);
            Gizmos.DrawRay(g + Vector3.up * 0.8f, Facing * 1.2f);
        }
#endif
    }
}
