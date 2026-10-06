// 넉백 예측과 그 계산에 필요한 벽 찾기.
// WallFinder의 소비자가 KnockbackPreview뿐이다.

using UnityEngine;

namespace Prototype
{
    // ══ KnockbackPreview ═══════════════════════════════════════════

    /// <summary>
    /// "이 카드를 놓으면 적이 <b>어디로</b> 가는가"를 미리 계산한다.
    ///
    /// 지금까지 조준 중에 보이는 건 사거리 원(<see cref="RangeIndicator"/>)뿐이라
    /// 밀치기 · 모으기가 대상을 어디로 보낼지 알 수 없었다 — 콤보 설계가 도박이 됐다.
    ///
    /// <b>실전과 같은 함수만 쓴다.</b> 방향은 <see cref="HitData.ResolveDirection"/>,
    /// 거리는 <see cref="Physics.TravelForImpulse"/>, 모으기 클램프는 <see cref="Combat"/>와
    /// 같은 식이다. 여기서 계산을 복제하면 프리뷰가 곧 거짓말이 된다.
    /// </summary>
    public static class KnockbackPreview
    {
        /// <summary>착지 링의 기본 반지름. 그리는 쪽과 테스트가 같은 값을 본다.</summary>
        public const float LandingRingRadius = 0.4f;

        public struct Result
        {
            /// <summary>지금 서 있는 자리(논리 바닥 좌표).</summary>
            public Vector3 from;
            /// <summary>모든 타격을 맞고 난 뒤 멈출 자리.</summary>
            public Vector3 to;
            /// <summary>가장 세게 띄우는 타격의 도달 높이. 0이면 안 뜬다.</summary>
            public float apexHeight;
        }

        /// <summary>
        /// 대상 옆에 서는 자리. <see cref="SkillState.TryApproach"/>가 쓰던 계산을 그대로 꺼내 왔다 —
        /// 프리뷰가 시전 기준점을 다르게 잡으면 화살표가 통째로 어긋난다.
        /// 오던 쪽에 붙고, 깊이(Z)는 대상 레인에 맞춘다.
        /// </summary>
        public static Vector3 ApproachSpot(Vector3 from, Vector3 targetPos, float approachDistance)
        {
            float side = from.x >= targetPos.x ? 1f : -1f;
            return new Vector3(targetPos.x + side * approachDistance, from.y, targetPos.z);
        }

        /// <summary>
        /// <paramref name="victim"/>이 이 스킬을 다 맞았을 때의 최종 위치.
        /// 밀치기 · 띄우기가 하나도 없는 스킬이면 false — 그릴 게 없다.
        /// </summary>
        public static bool TryPredict(SkillData data, Vector3 castOrigin, Vector3 casterFacing,
                                      Physics victim, out Result r)
        {
            r = default;
            if (data == null || victim == null || data.hitDataList == null) return false;

            Vector3 start = victim.GroundPosition;
            Vector3 pos = start;
            float apex = 0f;
            bool moves = false;

            // 매 타격이 위치를 갱신한다. 다단히트는 앞 타의 결과에서 다음 방향을 잡으므로
            // 마지막 위치가 진짜 착지점이다.
            for (int i = 0; i < data.hitDataList.Count; i++)
            {
                HitData hit = data.hitDataList[i];

                if (hit.pushDistance > 0f)
                {
                    moves = true;

                    // 실전(Combat.ApplyKnockback)과 같은 오버로드를 쓴다 — 밀치기는 벽이 방향을 정하므로
                    // 벽 레이어를 안 넘기면 화살표만 시전자 반대쪽을 가리킨다.
                    Vector3 dir = hit.ResolveDirection(castOrigin, casterFacing, pos, victim.WallMask);
                    pos += dir * Travel(in hit, castOrigin, pos, dir, victim.WallMask);
                }

                // 실전과 같은 선택 규칙 — 이미 떠 있는 대상은 aerialAirborneHeight가 우선한다.
                float height = victim.PhysicsState == PhysicsState.Aerial && hit.aerialAirborneHeight > 0f
                    ? hit.aerialAirborneHeight
                    : hit.airborneHeight;

                if (height > 0f)
                {
                    moves = true;
                    // 저작값이 곧 정점이다 — 환산을 거치지 않으므로 화살표와 데이터가 어긋날 자리가 없다.
                    apex = Mathf.Max(apex, height);
                }
            }

            if (!moves) return false;

            r = new Result { from = start, to = pos, apexHeight = apex };
            return true;
        }

        /// <summary>
        /// 이 타격 하나가 만들어 내는 이동 거리. 모으기는 중심을, 밀치기는 벽을 지나치지 않게 자른다.
        ///
        /// <c>pushDistance</c>가 이미 거리라 <b>충격량 왕복이 없다</b> —
        /// 실전(<see cref="Combat"/>)과 프리뷰가 같은 수를 보고 같은 곳에서 자른다.
        /// </summary>
        private static float Travel(in HitData hit, Vector3 center, Vector3 pos, Vector3 dir, LayerMask wallMask)
        {
            float travel = hit.pushDistance;

            // Combat.PushClamped와 같은 규칙 — 안 자르면 중심 근처의 적이 반대편으로 튄다.
            if (hit.mode == KnockbackMode.TowardCaster)
            {
                Vector3 flat = center - pos;
                flat.y = 0f;
                travel = Mathf.Min(travel, flat.magnitude);
            }

            // 벽으로 미는 타격은 벽에서 멈춘다. 안 자르면 화살표가 벽을 뚫고 나가
            // "저기까지 날아간다"는 거짓말이 된다 — 실제로는 벽에 닿아 튕긴다.
            if (hit.mode == KnockbackMode.TowardWall)
            {
                float toWall = WallFinder.DistanceToWall(pos, dir, wallMask);
                if (!float.IsInfinity(toWall)) travel = Mathf.Min(travel, toWall);
            }

            return travel;
        }

        /// <summary>
        /// 초기 속도 v로 띄웠을 때의 정점 높이. v² / 2g.
        /// <see cref="Physics.LaunchForHeight"/>의 역함수 — 저작값이 실제로 그 높이를 내는지 검증할 때 쓴다.
        /// </summary>
        public static float ApexHeight(float launchForce, float gravity)
        {
            if (launchForce <= 0f || gravity <= 0.0001f) return 0f;
            return launchForce * launchForce / (2f * gravity);
        }

        /// <summary>밀치기 · 띄우기를 하나라도 들고 있는 스킬인지. 표시 여부를 이걸로 정한다.</summary>
        public static bool MovesTarget(SkillData data)
        {
            if (data == null || data.hitDataList == null) return false;

            for (int i = 0; i < data.hitDataList.Count; i++)
            {
                HitData hit = data.hitDataList[i];

                // 내리꽂기(음수 높이)도 대상을 움직인다 — 0만 빼면 마무리기가 프리뷰에서 사라진다.
                if (hit.pushDistance > 0f || hit.airborneHeight != 0f) return true;
            }

            return false;
        }
    }

    // ══ WallFinder ═══════════════════════════════════════════

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
