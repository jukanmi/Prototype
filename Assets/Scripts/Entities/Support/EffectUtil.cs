// 광역 판정 헬퍼 — 반경 · 상자 · 부채꼴 안의 Combat을 찾아 때린다.
// 스킬만이 아니라 투사체 폭발(Projectile)도 쓰므로 Actor 층에 둔다. 파편 연출은 IBattleVfx로 요청한다.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>광역 판정이 필요한 효과의 공통 헬퍼.</summary>
    public static class EffectUtil
    {
        /// <summary>적중 이펙트가 터지는 몸통 높이 · 크기. <see cref="Attack"/>과 같은 값.</summary>
        private const float ImpactHeight = 0.7f;
        private const float ImpactRadius = 0.55f;

        private static readonly Collider[] Buffer = new Collider[64];
        private static readonly HashSet<Combat> Seen = new HashSet<Combat>();

        /// <summary>
        /// 기준점 반경을 한 번에 때린다. <b>스킬 타격의 유일한 광역 경로</b> —
        /// 즉시 터지는 장판도, 투사체가 도착해 터지는 폭발도 전부 여기를 지난다.
        ///
        /// 넉백 방향과 Z 정렬은 시전자가 아니라 <paramref name="center"/> 기준으로 돈다.
        /// 원거리는 멀리 서 있으므로 시전자 기준이면 적이 시전자 발밑으로 끌려간다.
        /// </summary>
        /// <param name="skip">이미 다른 경로로 맞은 대상. 투사체 직격이 여기 들어온다.</param>
        public static int AreaStrike(Vector3 center, float radius, Combat attacker,
                                     in HitData hit, in SkillVfx style, Combat skip = null)
        {
            if (attacker == null || radius <= 0f) return 0;

            // in 파라미터는 람다에 캡처할 수 없으므로 미리 꺼내 둔다.
            HitData blast = hit.WithOrigin(center);
            SkillVfx vfx = style;
            Combat skipped = skip;
            Vector3 flat = new Vector3(center.x, 0f, center.z);

            int hits = 0;

            OverlapCombats(center, radius, attacker, c =>
            {
                if (c == skipped) return;

                // 흘린 대상은 세지도, 파편을 튀기지도 않는다.
                // 이 hits는 "헛침" 경고의 근거라 무적을 세어 넣으면 헛친 장판이 적중으로 보고된다.
                if (!attacker.Attack(c, in blast)) return;
                hits++;

                if (c.Physics == null) return;

                // 파편이 중심에서 바깥으로 튀도록 방향을 준다(Attack.EmitImpact와 같은 규칙).
                Vector3 ground = c.Physics.GroundPosition;
                ground.y = 0f;

                BattleVfxSink.Current?.Impact(ground, c.Physics.Height + ImpactHeight,
                                              ground - flat, ImpactRadius, in vfx);
            });

            return hits;
        }

        /// <summary>
        /// 지정된 중심점, 크기, 방향의 <b>고정 박스 영역</b>을 한 번에 때린다.
        /// 일섬 등 시전자가 돌진으로 이동해도 지나온 궤적 전체를 고정하여 타격할 때 쓴다.
        /// </summary>
        public static int BoxStrike(Vector3 center, Vector3 size, Vector3 facing,
                                    Combat attacker, in HitData hit, in SkillVfx style)
        {
            if (attacker == null || size.x <= 0f || size.y <= 0f || size.z <= 0f) return 0;

            Vector3 flatFacing = new Vector3(facing.x, 0f, facing.z);
            if (flatFacing.sqrMagnitude <= 0.0001f) flatFacing = Vector3.forward;
            flatFacing.Normalize();

            Quaternion rot = Quaternion.LookRotation(flatFacing, Vector3.up);
            Quaternion invRot = Quaternion.Inverse(rot);

            HitData swing = hit.WithOrigin(center);
            SkillVfx vfx = style;
            Vector3 half = size * 0.5f;

            int hits = 0;
            float maxR = Mathf.Max(size.x, size.z) * 1.5f;

            OverlapCombats(center, maxR, attacker, c =>
            {
                if (c.Physics == null) return;

                Vector3 local = invRot * (c.Physics.GroundPosition - center);
                if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.z) > half.z) return;

                float yDiff = c.Physics.GroundPosition.y + c.Physics.Height * 0.5f - center.y;
                if (Mathf.Abs(yDiff) > half.y) return;

                if (!attacker.Attack(c, in swing)) return;
                hits++;

                Vector3 ground = c.Physics.GroundPosition;
                ground.y = 0f;

                BattleVfxSink.Current?.Impact(ground, c.Physics.Height + ImpactHeight,
                                              flatFacing, ImpactRadius, in vfx);
            });

            return hits;
        }

        /// <summary>
        /// <b>전방 부채꼴</b>을 한 번에 때린다. 반경 안이면서 정면에서 <paramref name="angle"/>의
        /// 절반 안에 든 대상만 맞는다.
        ///
        /// 박스 히트박스로는 "앞으로 길게 뻗되 옆으로는 안 닿는" 판정을 만들 수 없다 —
        /// 길이를 주면 폭도 같이 늘어난다. 사슬처럼 뻗는 스킬이 이 경로를 쓴다.
        ///
        /// 각도 검사는 <see cref="CombatStateRules"/>의 정면 판정과 같은 규칙이다(수평만 본다).
        /// 벨트스크롤이라 높이차로 부채꼴이 빗나가면 저작 의도와 어긋난다.
        /// </summary>
        public static int ConeStrike(Vector3 origin, Vector3 facing, float radius, float angle,
                                     Combat attacker, in HitData hit, in SkillVfx style)
        {
            if (attacker == null || radius <= 0f || angle <= 0f) return 0;

            Vector3 flatFacing = new Vector3(facing.x, 0f, facing.z);
            if (flatFacing.sqrMagnitude <= 0.0001f) return 0;
            flatFacing.Normalize();

            HitData swing = hit;
            SkillVfx vfx = style;
            float half = angle * 0.5f;
            Vector3 flatOrigin = new Vector3(origin.x, 0f, origin.z);

            int hits = 0;

            OverlapCombats(origin, radius, attacker, c =>
            {
                if (c.Physics == null) return;

                Vector3 to = c.Physics.GroundPosition - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(flatFacing, to) > half) return;

                if (!attacker.Attack(c, in swing)) return;
                hits++;

                Vector3 ground = c.Physics.GroundPosition;
                ground.y = 0f;

                BattleVfxSink.Current?.Impact(ground, c.Physics.Height + ImpactHeight,
                                              ground - flatOrigin, ImpactRadius, in vfx);
            });

            return hits;
        }

        /// <summary>
        /// 중심 반경 안의 Combat을 모은다. 시전자 본인과 <b>같은 진영</b>은 제외한다.
        /// 진영 필터가 없으면 모으기가 동료까지 빨아들인다 — Attack 히트박스와 같은 기준을 쓴다.
        /// </summary>
        public static int OverlapCombats(Vector3 center, float radius, Combat exclude, Action<Combat> onEach)
        {
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(center, radius, Buffer);
            int hit = 0;

            // 같은 Combat이 콜라이더 여러 개를 갖고 있으면 중복으로 잡힌다.
            Seen.Clear();

            Faction? casterFaction = exclude != null && exclude.Owner != null
                ? exclude.Owner.Faction
                : (Faction?)null;

            for (int i = 0; i < count; i++)
            {
                Combat c = Buffer[i] != null ? Buffer[i].GetComponentInParent<Combat>() : null;
                if (c == null || c == exclude || c.IsDead) continue;
                if (!Seen.Add(c)) continue;

                if (casterFaction.HasValue && c.Owner != null && c.Owner.Faction == casterFaction.Value)
                    continue;

                onEach?.Invoke(c);
                hit++;
            }

            return hit;
        }

        /// <summary>
        /// 반경 안의 살아 있는 적.
        /// </summary>
        public static int EnemiesInRadius(Vector3 center, float radius, List<Combat> outList = null)
        {
            outList?.Clear();
            int n = 0;

            OverlapCombats(center, radius, null, c =>
            {
                if (c.Owner == null || c.Owner.Faction != Faction.Enemy) return;
                outList?.Add(c);
                n++;
            });

            return n;
        }
    }
}
