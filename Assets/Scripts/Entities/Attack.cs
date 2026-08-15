using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 히트박스. 닿은 대상을 <b>전달만</b> 한다.
    /// 판정과 부가효과는 전부 <see cref="Combat.Attack"/>에서 처리한다(결정 로그 ②).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Attack : MonoBehaviour
    {
        /// <summary>타격 이펙트가 터지는 몸통 높이. 발밑(0)보다 위여야 맞은 것처럼 보인다.</summary>
        private const float ImpactHeight = 0.7f;
        private const float ImpactRadius = 0.55f;

        [SerializeField] private Combat attacker;
        [SerializeField] private HitData hitData;

        [Tooltip("적중 순간 타격 이펙트를 그린다.")]
        [SerializeField] private bool impactVfx = true;

        private readonly HashSet<Combat> alreadyHit = new HashSet<Combat>();
        private Collider box;

        // 적중은 Begin보다 몇 프레임 뒤에 일어난다. 그동안 이번 타격의 색을 붙들고 있어야 한다.
        private SkillVfx style;

        public Combat Attacker { get => attacker; set => attacker = value; }
        public HitData HitData { get => hitData; set => hitData = value; }

        /// <summary>지금 판정이 살아 있는지. 디버그 표시용.</summary>
        public bool IsActive => box != null && box.enabled;

        /// <summary>실제로 적중했을 때. 투사체가 관통 횟수를 세는 데 쓴다.</summary>
        public event System.Action<Combat> OnHit;

        private void Awake()
        {
            box = GetComponent<Collider>();
            box.isTrigger = true;
            if (attacker == null) attacker = GetComponentInParent<Combat>();
            box.enabled = false;

            WarnIfDuplicated();
        }

        /// <summary>
        /// 한 오브젝트에 Attack이 둘 이상이면 <b>같은 콜라이더를 공유</b>한다.
        /// 그러면 켠 적 없는 쪽까지 <c>OnTriggerEnter</c>를 받아, 비어 있는 hitData(데미지 0)로
        /// 먼저 때리고 <see cref="alreadyHit"/>에 대상을 선점한다 — 맞은 판정은 나는데 데미지가 0이 된다.
        ///
        /// 씬에서 실수로 컴포넌트를 하나 더 붙이면 생기고, 증상이 "가끔 안 아프다"라 추적이 어렵다.
        /// </summary>
        private void WarnIfDuplicated()
        {
            int count = GetComponents<Attack>().Length;
            if (count < 2) return;

            BattleLog.Warn(LogCategory.Combat,
                $"{name}: Attack 컴포넌트가 {count}개다. 같은 콜라이더를 공유해 데미지 0짜리 타격이 " +
                "먼저 소비된다 — 하나만 남길 것.", this);
        }

        /// <summary>히트박스 활성화. 한 번 켜는 동안 같은 대상은 1회만 맞는다.</summary>
        public void Begin(in HitData data) => Begin(in data, default);

        /// <summary>
        /// 스킬이 자기 이펙트 색을 실어 켜는 경우.
        /// vfx가 <c>default</c>면 전역 색으로 떨어진다.
        /// </summary>
        public void Begin(in HitData data, in SkillVfx vfx)
        {
            hitData = data;
            style = vfx;
            alreadyHit.Clear();

            // 콜라이더가 이미 켜진 채로 대상과 계속 겹쳐 있으면(다단히트 콤보)
            // enabled = true를 다시 대입해도 OnTriggerEnter가 재발화하지 않는다.
            // 껐다 켜서 강제로 새 진입으로 인식시킨다.
            box.enabled = false;
            box.enabled = true;

            // 휘두름 궤적은 없다. 시전자 스프라이트 시트가 이미 휘두르는 그림을 들고 있어서
            // 그 위에 선을 덧그리면 두 개가 겹쳐 보였다. 연출은 적중 순간에만 낸다.
        }

        public void End()
        {
            box.enabled = false;
            alreadyHit.Clear();
        }

        private void OnTriggerEnter(Collider other) => TryHit(other);
        private void OnCollisionEnter(Collision collision) => TryHit(collision.collider);

        private void TryHit(Collider other)
        {
            if (attacker == null || other == null) return;

            Combat victim = other.GetComponentInParent<Combat>();
            if (victim == null || victim == attacker) return;

            // 같은 진영은 때리지 않는다.
            if (victim.Owner != null && attacker.Owner != null &&
                victim.Owner.Faction == attacker.Owner.Faction) return;

            if (!alreadyHit.Add(victim)) return;

            if (victim is IHittable hittable)
                attacker.Attack(hittable, in hitData);

            if (impactVfx) EmitImpact(victim);

            OnHit?.Invoke(victim);
        }

        /// <summary>맞은 쪽 몸통 높이에서 터뜨린다. 발밑에서 터지면 타격이 아니라 착지로 읽힌다.</summary>
        private void EmitImpact(Combat victim)
        {
            if (victim.Physics == null) return;

            Vector3 ground = victim.Physics.GroundPosition;
            ground.y = 0f;

            // 파편이 때린 쪽 반대로 튀도록 방향을 준다.
            Vector3 away = ground - new Vector3(transform.position.x, 0f, transform.position.z);

            BattleVfx.Impact(ground, victim.Physics.Height + ImpactHeight, away, ImpactRadius, in style);
        }
    }
}
