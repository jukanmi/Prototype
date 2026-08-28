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

        // 스킬이 자기 시전 범위로 히트박스를 갈아 끼울 수 있다(SkillData.castRangeScale).
        // 히트박스는 스킬끼리 공유되므로 원래 모양을 여기 기억해 두고 End에서 되돌린다.
        private Vector3 defaultSize;
        private Vector3 defaultLocalPos;
        private bool resized;

        // 켜는 순간의 겹침 스윕용. 히트박스가 여러 개 켜져도 한 프레임에 하나씩 도므로 공유해도 된다.
        private static readonly Collider[] SweepBuffer = new Collider[32];
        private int sweepMask;

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
            sweepMask = BuildSweepMask(gameObject.layer);

            defaultSize = box is BoxCollider b ? b.size : Vector3.zero;
            defaultLocalPos = transform.localPosition;

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
        public void Begin(in HitData data, in SkillVfx vfx) => Begin(in data, in vfx, Vector3.zero);

        /// <summary>
        /// 스킬이 <b>자기 시전 범위</b>로 히트박스를 갈아 끼워 켜는 경우.
        ///
        /// <paramref name="size"/>가 0이면 프리팹 모양 그대로다 — 범위를 안 적은 스킬은
        /// 예전과 똑같이 동작한다. 값이 있으면 크기와 함께 <b>중심도 다시 잡는다</b>:
        /// 로컬 z를 깊이의 절반에 두어 <b>시전자 몸 앞면에서 시작해 깊이만큼</b> 뻗게 한다.
        /// 그래야 "시전 범위 = 피격 범위 × 배수"가 화면에서 실제로 재어지는 말이 된다.
        ///
        /// 히트박스는 스킬끼리 공유되므로 <see cref="End"/>가 반드시 원복한다 —
        /// 안 그러면 다음 스킬과 평타가 남의 범위로 때린다.
        /// </summary>
        public void Begin(in HitData data, in SkillVfx vfx, Vector3 size)
        {
            hitData = data;
            style = vfx;
            alreadyHit.Clear();

            Resize(size);
            box.enabled = true;

            // 이미 겹쳐 있는 대상은 OnTriggerEnter를 다시 받지 못한다 — 새로 "진입"한 게 아니기 때문이다.
            // 껐다 켜는 것도 소용없다(사이에 물리 스텝이 없다). 그래서 켠 직후 직접 훑는다.
            // 이게 빠지면 올려베기 2·3타가 통째로 씹힌다.
            SweepOverlaps();

            // 휘두름 궤적은 없다. 시전자 스프라이트 시트가 이미 휘두르는 그림을 들고 있어서
            // 그 위에 선을 덧그리면 두 개가 겹쳐 보였다. 연출은 적중 순간에만 낸다.
        }

        /// <summary>이번 타격만 쓰는 크기로 바꾼다. BoxCollider가 아니면 조용히 넘어간다.</summary>
        private void Resize(Vector3 size)
        {
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f) return;
            if (!(box is BoxCollider b)) return;

            b.size = size;
            b.center = Vector3.zero;
            transform.localPosition = new Vector3(defaultLocalPos.x, defaultLocalPos.y, size.z * 0.5f);
            resized = true;
        }

        /// <summary><see cref="Resize"/>가 건드린 모양을 프리팹 값으로 되돌린다.</summary>
        private void RestoreSize()
        {
            if (!resized) return;

            if (box is BoxCollider b) b.size = defaultSize;
            transform.localPosition = defaultLocalPos;
            resized = false;
        }

        public void End()
        {
            RestoreSize();
            box.enabled = false;
            alreadyHit.Clear();
        }

        /// <summary>
        /// 켠 순간 이미 콜라이더 안에 들어와 있는 대상을 직접 잡는다.
        /// <see cref="EffectUtil.OverlapCombats"/>와 같은 방식이고,
        /// 중복은 <see cref="alreadyHit"/>가 그대로 막는다 — 뒤이어 오는 OnTriggerEnter와 겹쳐도 1회다.
        /// </summary>
        private void SweepOverlaps()
        {
            if (box == null) return;

            Bounds b = box.bounds;
            int count = UnityEngine.Physics.OverlapBoxNonAlloc(
                b.center, b.extents, SweepBuffer, transform.rotation, sweepMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
                TryHit(SweepBuffer[i]);
        }

        /// <summary>
        /// 이 히트박스가 부딪힐 수 있는 레이어. 충돌 매트릭스(<c>DynamicsManager</c>)를 그대로 읽는다 —
        /// 스윕이 자체 기준을 쓰면 벽이나 아군을 새로 뚫고 때린다.
        /// </summary>
        public static int BuildSweepMask(int layer)
        {
            int mask = 0;

            for (int i = 0; i < 32; i++)
            {
                if (!UnityEngine.Physics.GetIgnoreLayerCollision(layer, i))
                    mask |= 1 << i;
            }

            return mask;
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

            // 무적 · 패링으로 흘린 타격은 <b>없던 일</b>이다. 연출도 OnHit도 내지 않는다.
            //
            // OnHit은 혹을 둘 달고 다닌다 — 투사체 관통 수 소모(Projectile.HandleHit)와
            // 돌진 · 보스 패턴의 조기 종료(EnemySpecialSequence.HitOrWall)가 여기 매달려 있다.
            // 여기서 새면 완벽하게 흘려낸 공격이 명중과 같은 결과를 낸다.
            //
            // alreadyHit에서 빼지는 않는다. 한 번 켜는 동안 같은 대상은 1회라는 규약은
            // 그대로고, 무적 구간(다운 1.2s · 기상 0.4s)이 히트박스 수명보다 길다.
            bool landed = victim is IHittable hittable && attacker.Attack(hittable, in hitData);
            if (!landed) return;

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
