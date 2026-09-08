// 타격 한 번의 데이터와 그것을 받는 인터페이스.
// IHittable.Hit(in HitData, Combat)이라 시그니처가 곧 이 구조체다.

using System;
using UnityEngine;

namespace Prototype
{
    // ══ HitData ═══════════════════════════════════════════

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

        [Header("타이밍")]
        [Tooltip("앞 사건(시전 시작 · 앞 타)으로부터 이 타까지의 간격(초).\n\n" +
                 "0이면 SkillData.hitInterval로 떨어진다 — 간격이 일정한 다단히트는 안 채워도 된다.\n" +
                 "타마다 선딜이 다른 스킬(연격 0.2 / 0.2 / 0.3)만 여기를 쓴다.")]
        public float castDelay;

        [Header("시전 범위")]
        [Tooltip("이 타만의 시전 범위 배수. 0이면 SkillData.castRangeScale로 떨어진다.\n\n" +
                 "타마다 범위가 다른 스킬(연격 — 벨수록 위로 넓어진다)이 여기를 쓴다.")]
        public Vector3 castRangeScale;

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

        [Tooltip("밀어낼 거리(유닛). 기획서 '밀치기 강도' 행을 그대로 적는다. 0이면 안 민다.")]
        public float pushDistance;

        [Tooltip("띄울 높이(유닛). 기획서 '에어본 강도(높이)' 행을 그대로 적는다. 0이면 안 띄운다.\n\n" +
                 "<b>음수면 아래로 꽂는다</b> — 같은 크기로 띄울 힘을 반대로 쓴다. " +
                 "마무리기(내려찍기)의 '수직으로 끌어내리고 다운시킴'이 그 자리다.\n" +
                 "떠 있는 적만 꽂힌다 — 지상 적은 이미 바닥이라 아무 일도 안 일어나고, " +
                 "다운은 착지가 만든다(CombatStateRules.OnGroundContact).")]
        public float airborneHeight;

        [Tooltip("대상이 이미 떠 있을 때 대신 쓰는 띄우기 높이. 0이면 airborneHeight를 그대로 쓴다. " +
                 "지상 첫 타는 히트박스가 닿는 높이까지만 띄우면 되지만, 공중 연계는 이미 올라간 몸을 " +
                 "다시 밀어 올려야 해서 같은 값으로는 모자란다. 두 값을 나눠 두면 시작 높이를 " +
                 "건드리지 않고 공중만 조절할 수 있다.")]
        public float aerialAirborneHeight;

        [Tooltip("이미 떠 있는 대상을 airborneHeight 위로는 올리지 않는다.\n\n" +
                 "기획서 비고 '이 스킬의 에어본 높이를 초과해서 띄우지 않음' 행. " +
                 "끄면 공중 대상이 맞을수록 조금씩 더 높이 뜬다(Physics.airLaunchScale).\n" +
                 "내려찍기(음수 높이)에는 걸리지 않는다 — 위로 올리는 타격에만 있는 상한이다.")]
        public bool capAirborne;

        [Tooltip("TowardCaster(모으기)일 때 끌어올 목표 지점을 시전 범위의 몇 배 앞에 둘지.\n\n" +
                 "0이면 기준점(시전자 · 찍은 좌표)까지 그대로 끌어온다.\n" +
                 "기획서 '피격 범위 전방 1/3 지점까지'가 0.333이다 — 시전자 발밑이 아니라 " +
                 "조금 앞에 모아 두면 후속 광역이 시전자를 비껴간다.")]
        public float pullAnchorRatio;

        public float hitStunDuration;

        [Tooltip("이 타격이 거는 행동 불능. None이면 안 건다.\n\n" +
                 "위의 hitStunDuration(피격 경직)과 다른 축이다 — 경직은 다음 타격이 덮어쓰는 " +
                 "짧은 반응이고, 이쪽은 debuffDuration이 다 갈 때까지 남는다. " +
                 "0으로 로드되는 기존 애셋은 None이라 동작이 그대로다.")]
        public Debuff debuff;

        [Tooltip("행동 불능 지속시간. 같은 디버프가 이미 걸려 있으면 긴 쪽이 남는다.")]
        public float debuffDuration;

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
                    return Vector3.zero; // 수평 성분 없음. airborneHeight만 사용.
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

    // ══ Interfaces ═══════════════════════════════════════════

    /// <summary>피격 판정을 받을 수 있는 대상. 상태 전이 · 넉백까지 포함한다.</summary>
    public interface IHittable
    {
        bool Hit(in HitData hitData, Combat attacker);
    }

    /// <summary>데미지만 받는 대상. 파괴 가능한 오브젝트 등.</summary>
    public interface IDamageable
    {
        void TakeDamage(in DamageData damageData);
    }
}
