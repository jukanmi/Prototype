using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// "여기서 가장 가까운 벽은 어느 쪽인가". <see cref="KnockbackMode.TowardWall"/> 하나를 위해 존재한다.
    ///
    /// 밀치기는 벽바운드로 이어져야 값어치가 있는데, 방향을 시전자 기준으로 잡으면
    /// 시전자가 어디 섰느냐에 따라 적이 방 한복판으로 날아가 아무 일도 안 일어난다.
    /// 방향을 <b>벽</b>이 정하면 밀치기는 언제 걸어도 벽으로 간다.
    ///
    /// 계산은 실전(<see cref="Combat"/>)과 프리뷰(<see cref="KnockbackPreview"/>)가 같은 함수를 쓴다 —
    /// 두 벌로 갈리면 화살표가 곧 거짓말이 된다.
    /// </summary>
    public static class WallFinder
    {
        /// <summary>
        /// 탐색 방향 — <b>좌우뿐</b>이다.
        ///
        /// 앞뒤(±Z)를 넣으면 안 된다. 벨트스크롤의 방은 Z로 좁고(예: ±3.25) X로 넓어서(±6.25),
        /// 웬만한 자리에서 "가장 가까운 벽"이 늘 안쪽 벽이 된다. 그런데 Z는 슬램할 벽이 아니라
        /// <b>레인 경계</b>다 — 그쪽으로 밀면 적이 화면 안쪽으로 밀려 들어가 벽에 처박는 그림이 안 나온다.
        ///
        /// 좌우 벽이 둘 다 없으면 아예 "벽 없음"으로 보고 호출부가 옛 방향으로 떨어진다.
        /// </summary>
        private static readonly Vector3[] Probes = { Vector3.right, Vector3.left };

        /// <summary>이 거리까지만 벽을 찾는다. 넘어가면 "벽이 없다"로 본다.</summary>
        public const float DefaultSearchRange = 60f;

        /// <summary>레이 시작 높이. 바닥에 딱 붙이면 지면 콜라이더에 먼저 걸린다.</summary>
        private const float ProbeHeight = 0.6f;

        /// <summary>
        /// 가장 가까운 벽 쪽 방향(수평 단위벡터). 벽을 못 찾으면 false —
        /// 호출부가 기존 방향(밀치기면 시전자 반대쪽)으로 떨어진다.
        /// </summary>
        public static bool TryFindDirection(Vector3 from, LayerMask wallMask, out Vector3 dir,
                                            float searchRange = DefaultSearchRange)
        {
            dir = Vector3.zero;

            Vector3 origin = new Vector3(from.x, from.y + ProbeHeight, from.z);
            float best = float.MaxValue;

            for (int i = 0; i < Probes.Length; i++)
            {
                if (!UnityEngine.Physics.Raycast(origin, Probes[i], out RaycastHit hit,
                                                 searchRange, wallMask, QueryTriggerInteraction.Ignore))
                    continue;

                if (hit.distance >= best) continue;

                best = hit.distance;
                dir = Probes[i];
            }

            return best < float.MaxValue;
        }

        /// <summary>
        /// 벽까지의 거리. 프리뷰가 "여기서 벽에 닿는다"를 그릴 때 쓴다.
        /// 못 찾으면 <see cref="float.PositiveInfinity"/>.
        /// </summary>
        public static float DistanceToWall(Vector3 from, Vector3 dir, LayerMask wallMask,
                                           float searchRange = DefaultSearchRange)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f) return float.PositiveInfinity;

            Vector3 origin = new Vector3(from.x, from.y + ProbeHeight, from.z);

            return UnityEngine.Physics.Raycast(origin, dir.normalized, out RaycastHit hit,
                                               searchRange, wallMask, QueryTriggerInteraction.Ignore)
                ? hit.distance
                : float.PositiveInfinity;
        }

        /// <summary>
        /// 밀치기 한 방의 최종 방향. 벽을 찾으면 벽 쪽, 못 찾으면 <paramref name="fallback"/>.
        /// 실전과 프리뷰가 이 한 줄을 공유한다.
        /// </summary>
        public static Vector3 PushDirection(Vector3 from, LayerMask wallMask, Vector3 fallback)
        {
            if (TryFindDirection(from, wallMask, out Vector3 toWall)) return toWall;

            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.right;
        }
    }
}
