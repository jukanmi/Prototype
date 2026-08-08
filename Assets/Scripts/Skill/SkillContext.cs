using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 조준으로 확정한 값. <b>좌표와 방향만 담는다</b> —
    /// 실제로 때릴 상대는 시전 순간에 <see cref="SkillState.ResolveTarget"/>이 좌표에서 다시 뽑는다.
    /// 조준과 시전 사이에 대상이 죽거나 움직여도 알아서 맞는다.
    /// </summary>
    [Serializable]
    public struct TargetInfo
    {
        public TargetingType type;
        /// <summary>GroundPoint — 텔레포트 · 장판 중심 좌표.</summary>
        public Vector3 point;
        /// <summary>Direction — 지정 방향.</summary>
        public Vector3 direction;

        public static TargetInfo None => new TargetInfo { type = TargetingType.None };

        public static TargetInfo Ground(Vector3 p) => new TargetInfo { type = TargetingType.GroundPoint, point = p };
        public static TargetInfo Dir(Vector3 d) => new TargetInfo { type = TargetingType.Direction, direction = d };

        public bool IsValid
            => type != TargetingType.Direction || direction.sqrMagnitude > 0.0001f;
    }

    /// <summary>
    /// SkillState가 읽는 <b>유일한 입력</b>. 여기 없는 정보에는 의존하지 않는다.
    /// </summary>
    public struct SkillContext
    {
        public SkillData data;
        public Entity caster;
        /// <summary>시전 순간에 확정되는 상대. 비어 있으면 <see cref="SkillState.ResolveTarget"/>이 채운다.</summary>
        public Entity target;
        public TargetInfo targetInfo;
        public bool isBulletTime;
        public int comboIndex;

        /// <summary>
        /// 차징으로 커진 범위 배율. 광역 효과가 자기 반경에 곱한다.
        /// 차징이 아니면 1이다.
        /// </summary>
        public float radiusScale;

        /// <summary>0이면 미설정으로 보고 1을 준다. 구조체라 기본값이 0이기 때문.</summary>
        public float RadiusScale => radiusScale > 0f ? radiusScale : 1f;

        public Combat CasterCombat => caster != null ? caster.Combat : null;
        public Physics CasterPhysics => caster != null ? caster.Physics : null;

        /// <summary>
        /// 시전 기준점. 광역 판정 · 광역 효과 · 시전 연출이 전부 이 한 점을 공유한다.
        ///
        /// 찍은 좌표가 있으면 그것을, 없으면 <b>확정된 대상 자리</b>를 쓴다.
        /// 시전자 자리로 떨어뜨리면 방향 지정 스킬(충격파 등)이 조준과 무관하게
        /// 자기 발밑에서 터진다 — 원거리는 이제 움직이지 않기 때문.
        /// </summary>
        public Vector3 Origin
        {
            get
            {
                if (targetInfo.type == TargetingType.GroundPoint) return targetInfo.point;
                if (target != null && target.Physics != null) return target.Physics.GroundPosition;
                if (caster != null) return caster.transform.position;
                return Vector3.zero;
            }
        }
    }
}
