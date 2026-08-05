using System;
using UnityEngine;

namespace Prototype
{
    [Serializable]
    public struct DamageData
    {
        public float damage;

        public DamageData(float damage)
        {
            this.damage = damage;
        }
    }

    /// <summary>
    /// 한 번의 타격이 담고 있는 정보 전부.
    /// Combat의 멤버를 갱신하지 않고 <b>인자로만</b> 흘려보낸다.
    /// </summary>
    [Serializable]
    public struct HitData
    {
        public DamageData damageData;

        [Header("상태 전이")]
        [Tooltip("선행 조건. Neutral이면 조건 없음.")]
        public CombatState targetState;
        [Tooltip("적중 후 대상이 들어갈 결과 상태.")]
        public CombatState nextState;
        [Tooltip("다운된 적에게도 적중하는 바닥쓸기(OTG) 판정인지.")]
        public bool canOtg;

        [Header("넉백")]
        public KnockbackMode mode;
        [Tooltip("mode == Fixed 일 때만 사용. 시전자 로컬 기준 방향.")]
        public Vector3 fixedDir;
        public float knockbackForce;
        public float launchForce;
        public float hitStunDuration;

        [Tooltip("모으기 계열에서 Z축을 시전자에 맞춰 정렬한다. 벨트스크롤 특성상 Z가 어긋나면 후속타가 빗나감.")]
        public bool snapZ;

        /// <summary>
        /// 넉백 방향을 <b>타격 순간에</b> 계산한다.
        /// 고정 Vector3로는 모으기(TowardCaster)를 표현할 수 없다.
        /// </summary>
        public Vector3 ResolveDirection(Vector3 casterPos, Vector3 casterForward, Vector3 targetPos)
        {
            switch (mode)
            {
                case KnockbackMode.TowardCaster:
                {
                    Vector3 d = casterPos - targetPos;
                    d.y = 0f;
                    return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.zero;
                }
                case KnockbackMode.AwayFromCaster:
                {
                    Vector3 d = targetPos - casterPos;
                    d.y = 0f;
                    return d.sqrMagnitude > 0.0001f ? d.normalized : casterForward;
                }
                case KnockbackMode.Up:
                    return Vector3.zero; // 수평 성분 없음. launchForce만 사용.
                case KnockbackMode.Fixed:
                default:
                {
                    Vector3 local = fixedDir.sqrMagnitude > 0.0001f ? fixedDir : Vector3.forward;
                    Vector3 d = Quaternion.LookRotation(SafeForward(casterForward)) * local;
                    d.y = 0f;
                    return d.sqrMagnitude > 0.0001f ? d.normalized : SafeForward(casterForward);
                }
            }
        }

        private static Vector3 SafeForward(Vector3 f)
        {
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }
    }
}
