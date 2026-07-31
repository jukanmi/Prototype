using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 유저가 조준으로 확정한 값. 불릿타임 중에는 유저가, 라이브 페이즈에는 AI가 채운다.
    /// </summary>
    [Serializable]
    public struct TargetInfo
    {
        public TargetingType type;
        /// <summary>GroundPoint — 텔레포트 · 장판 중심 좌표.</summary>
        public Vector3 point;
        /// <summary>EnemyUnit — 지정 대상.</summary>
        public Entity unit;
        /// <summary>Direction — 지정 방향.</summary>
        public Vector3 direction;

        public static TargetInfo None => new TargetInfo { type = TargetingType.None };

        public static TargetInfo Ground(Vector3 p) => new TargetInfo { type = TargetingType.GroundPoint, point = p };
        public static TargetInfo Unit(Entity e) => new TargetInfo { type = TargetingType.EnemyUnit, unit = e, point = e != null ? e.transform.position : Vector3.zero };
        public static TargetInfo Dir(Vector3 d) => new TargetInfo { type = TargetingType.Direction, direction = d };

        public bool IsValid
        {
            get
            {
                switch (type)
                {
                    case TargetingType.EnemyUnit: return unit != null && !unit.Combat.IsDead;
                    case TargetingType.Direction: return direction.sqrMagnitude > 0.0001f;
                    default: return true;
                }
            }
        }
    }

    /// <summary>
    /// SkillState가 읽는 <b>유일한 입력</b>. 여기 없는 정보에는 의존하지 않는다.
    /// </summary>
    public struct SkillContext
    {
        public SkillData data;
        public Entity caster;
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

        /// <summary>시전 기준점. 지정 좌표가 있으면 그것을, 없으면 시전자 위치를 쓴다.</summary>
        public Vector3 Origin
        {
            get
            {
                if (targetInfo.type == TargetingType.GroundPoint) return targetInfo.point;
                if (caster != null) return caster.transform.position;
                return Vector3.zero;
            }
        }
    }
}
