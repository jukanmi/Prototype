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
        [Tooltip("대상이 이미 떠 있을 때 대신 쓰는 띄우기 힘. 0이면 launchForce를 그대로 쓴다. " +
                 "지상 첫 타는 히트박스가 닿는 높이까지만 띄우면 되지만, 공중 연계는 이미 올라간 몸을 " +
                 "다시 밀어 올려야 해서 같은 값으로는 모자란다. 두 값을 나눠 두면 시작 높이를 " +
                 "건드리지 않고 공중만 조절할 수 있다.")]
        public float airLaunchForce;
        public float hitStunDuration;

        [Tooltip("이 타격이 깎는 가드 게이지(보스 전용). 0이면 상대의 defaultGuardDamage를 쓴다.\n\n" +
                 "0을 기본으로 둔 이유는 기존 애셋 전부가 0으로 로드되기 때문이다 — " +
                 "데이터를 안 채워도 동작하고, 가드 파괴가 특기인 스킬만 값을 준다.")]
        public float guardDamage;

        [Tooltip("모으기 계열에서 Z축을 기준점에 맞춰 정렬한다. 벨트스크롤 특성상 Z가 어긋나면 후속타가 빗나감.")]
        public bool snapZ;

        /// <summary>
        /// 넉백 방향과 Z 정렬의 기준점. 비어 있으면 시전자 위치를 쓴다.
        ///
        /// 장판은 시전자와 <b>떨어진 좌표</b>에서 터진다 — 기준점을 안 넘기면
        /// 그물사격이 찍은 자리가 아니라 궁수 발밑으로 적을 끌어모은다.
        /// 에셋에 저장할 값이 아니라 시전 순간에만 실리는 값이라 직렬화하지 않는다.
        /// </summary>
        [NonSerialized] public Vector3 origin;
        [NonSerialized] public bool hasOrigin;

        /// <summary>기준점을 실은 복사본. 원본은 건드리지 않는다.</summary>
        public HitData WithOrigin(Vector3 p)
        {
            HitData copy = this;
            copy.origin = p;
            copy.hasOrigin = true;
            return copy;
        }

        /// <summary>
        /// 넉백 방향을 <b>타격 순간에</b> 계산한다.
        /// 고정 Vector3로는 모으기(TowardCaster)를 표현할 수 없다.
        ///
        /// <see cref="KnockbackMode.TowardWall"/>은 벽 레이어를 알아야 하므로
        /// <see cref="ResolveDirection(Vector3, Vector3, Vector3, LayerMask)"/>를 써야 한다 —
        /// 이 오버로드로 부르면 시전자 반대쪽(밀치기의 옛 동작)으로 떨어진다.
        /// </summary>
        public Vector3 ResolveDirection(Vector3 casterPos, Vector3 casterForward, Vector3 targetPos)
        {
            switch (mode)
            {
                case KnockbackMode.TowardWall:
                    goto case KnockbackMode.AwayFromCaster;

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

        /// <summary>
        /// 벽 레이어까지 아는 방향 계산. <see cref="KnockbackMode.TowardWall"/>만 다르게 돌고
        /// 나머지는 그대로 위임한다 — 실전(<see cref="Combat"/>)과 프리뷰가 같은 값을 본다.
        /// </summary>
        public Vector3 ResolveDirection(Vector3 casterPos, Vector3 casterForward, Vector3 targetPos,
                                        LayerMask wallMask)
        {
            Vector3 fallback = ResolveDirection(casterPos, casterForward, targetPos);

            return mode == KnockbackMode.TowardWall
                ? WallFinder.PushDirection(targetPos, wallMask, fallback)
                : fallback;
        }

        private static Vector3 SafeForward(Vector3 f)
        {
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }
    }
}
