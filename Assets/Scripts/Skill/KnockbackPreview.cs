using UnityEngine;

namespace Prototype
{
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

                if (hit.knockbackForce > 0f)
                {
                    moves = true;
                    Vector3 dir = hit.ResolveDirection(castOrigin, casterFacing, pos);
                    pos += dir * Travel(in hit, victim, castOrigin, pos);
                }

                if (hit.launchForce > 0f)
                {
                    moves = true;
                    apex = Mathf.Max(apex, ApexHeight(hit.launchForce, victim.Gravity));
                }
            }

            if (!moves) return false;

            r = new Result { from = start, to = pos, apexHeight = apex };
            return true;
        }

        /// <summary>이 타격 하나가 만들어 내는 이동 거리. 모으기는 중심을 지나치지 않게 자른다.</summary>
        private static float Travel(in HitData hit, Physics victim, Vector3 center, Vector3 pos)
        {
            float force = hit.knockbackForce;

            // Combat.PullClamped와 같은 규칙 — 안 자르면 중심 근처의 적이 반대편으로 튄다.
            if (hit.mode == KnockbackMode.TowardCaster)
            {
                Vector3 flat = center - pos;
                flat.y = 0f;
                force = Mathf.Min(force, victim.ImpulseToTravel(flat.magnitude));
            }

            return victim.TravelForImpulse(force);
        }

        /// <summary>초기 속도 v로 띄웠을 때의 정점 높이. v² / 2g.</summary>
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
                if (hit.knockbackForce > 0f || hit.launchForce > 0f) return true;
            }

            return false;
        }
    }
}
