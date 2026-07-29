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
        [SerializeField] private Combat attacker;
        [SerializeField] private HitData hitData;

        private readonly HashSet<Combat> alreadyHit = new HashSet<Combat>();
        private Collider box;
        private SpriteRenderer marker;

        public Combat Attacker { get => attacker; set => attacker = value; }
        public HitData HitData { get => hitData; set => hitData = value; }

        /// <summary>지금 판정이 살아 있는지. 디버그 표시용.</summary>
        public bool IsActive => box != null && box.enabled;

        private void Awake()
        {
            box = GetComponent<Collider>();
            box.isTrigger = true;
            if (attacker == null) attacker = GetComponentInParent<Combat>();
            box.enabled = false;

            // 스프라이트가 붙어 있으면 판정이 살아 있는 동안만 보여 준다.
            marker = GetComponent<SpriteRenderer>();
            if (marker != null) marker.enabled = false;
        }

        /// <summary>히트박스 활성화. 한 번 켜는 동안 같은 대상은 1회만 맞는다.</summary>
        public void Begin(in HitData data)
        {
            hitData = data;
            alreadyHit.Clear();
            box.enabled = true;
            if (marker != null) marker.enabled = true;
        }

        public void End()
        {
            box.enabled = false;
            alreadyHit.Clear();
            if (marker != null) marker.enabled = false;
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
        }
    }
}
