using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 돌진의 <b>실행</b>. 단계는 <see cref="EnemySpecialSequence"/>가 세고, 여기서는
    /// 회전 · 대쉬 · 히트박스처럼 인스턴스가 있어야 되는 일만 한다.
    /// 브레인은 무상태여야 하므로 이 컴포넌트가 프리팹에 붙는다.
    ///
    /// 패턴이 하나뿐인 실행기다. 여럿을 쓰는 보스는 <see cref="BossPatternAction"/>을 붙인다.
    /// </summary>
    [RequireComponent(typeof(Entity))]
    public class EnemyChargeAction : MonoBehaviour, IEnemySpecialAction
    {
        [Header("타이밍")]
        [Tooltip("예고. 이 동안 제자리에서 타겟을 노려본다.")]
        [SerializeField] private float telegraphDuration = 0.6f;
        [Tooltip("돌진 지속. 벽·적중으로 더 일찍 끝날 수 있다.")]
        [SerializeField] private float chargeDuration = 0.55f;
        [Tooltip("후딜. 반격당하는 구간.")]
        [SerializeField] private float recoveryDuration = 0.8f;

        [Header("돌진")]
        [SerializeField] private float chargeSpeed = 16f;
        [Tooltip("돌진 데미지 = 공격력 x 이 배율.")]
        [SerializeField] private float damageScale = 1.5f;

        [Tooltip("돌진 전용 히트박스. 비우면 평타 히트박스를 쓴다.")]
        [SerializeField] private Attack chargeHitbox;

        [SerializeField] private HitData chargeHit = new HitData
        {
            damageData = new DamageData(15f),
            targetState = CombatState.Neutral,
            nextState = CombatState.Knockback,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            knockbackForce = 8f,
            hitStunDuration = 0.3f,
        };

        private Entity owner;
        private EnemySpecialSequence sequence;
        private Entity target;
        private bool wired;

        public EnemySpecialPhase Phase => sequence != null ? sequence.Phase : EnemySpecialPhase.Idle;
        public bool IsRunning => sequence != null && sequence.IsRunning;

        /// <summary>돌진 하나뿐이다. 브레인은 언제나 0번을 지목한다.</summary>
        public int Count => 1;

        /// <summary>돌진 전용 히트박스. 프리팹 배선 검사용 읽기 전용 창구.</summary>
        public Attack ChargeHitbox => chargeHitbox;

        /// <summary>?. 가 안전하도록 유니티의 가짜 null을 진짜 null로 정규화해서 돌려준다.</summary>
        private Attack Hitbox
        {
            get
            {
                if (chargeHitbox != null) return chargeHitbox;
                if (owner != null && owner.BasicAttack != null) return owner.BasicAttack;
                return null;
            }
        }

        private void Awake()
        {
            owner = GetComponent<Entity>();
            sequence = new EnemySpecialSequence(telegraphDuration, chargeDuration, recoveryDuration);
        }

        /// <summary>돌진 시작. 이미 돌고 있으면 거절한다 — 쿨 소모는 성공했을 때만이다.</summary>
        public bool TryStart(int index, Entity chargeTarget)
        {
            if (index != 0) return false;
            if (sequence == null || sequence.IsRunning) return false;
            if (chargeTarget == null) return false;

            target = chargeTarget;
            sequence.Begin();
            owner.SetTelegraph(sequence.ShouldShowTelegraph);

            BattleLog.Log(LogCategory.State, $"{name} 돌진 예고 시작 → {chargeTarget.name}", this);
            return true;
        }

        /// <summary>EnemyControl이 실행 중에만 부른다.</summary>
        public void Tick(float dt)
        {
            if (sequence == null || !sequence.IsRunning) return;

            EnemySpecialPhase before = sequence.Phase;
            sequence.Tick(dt, AimDirection());
            EnemySpecialPhase after = sequence.Phase;

            if (before != after) HandleTransition(after);

            // 돌진 시작 TelegraphLead초 전부터만 번쩍인다. 예고 0.6초를 통째로 켜면
            // !를 보고 누른 대시가 돌진이 오기 전에 끝난다.
            owner.SetTelegraph(sequence.ShouldShowTelegraph);

            switch (after)
            {
                case EnemySpecialPhase.Telegraph:
                    // 예고 중에는 계속 따라 돈다. 여기까지가 유도다.
                    owner.Physics.Face(AimDirection());
                    owner.Physics.Move(Vector3.zero, 0f);
                    break;

                case EnemySpecialPhase.Active:
                    // Dash는 속도를 매 프레임 덮어쓴다. 감속에 먹히지 않게 계속 밀어 준다.
                    owner.Physics.Dash(sequence.LockedDirection, chargeSpeed);
                    break;
            }
        }

        /// <summary>피격 · 사망 · AI 정지. 진행 중인 돌진을 흔적 없이 되돌린다.</summary>
        public void Cancel()
        {
            if (sequence == null || !sequence.IsRunning) return;

            sequence.Cancel();
            Teardown();
            owner.SetTelegraph(false);

            BattleLog.Log(LogCategory.State, $"{name} 돌진 취소", this);
        }

        private void HandleTransition(EnemySpecialPhase next)
        {
            switch (next)
            {
                case EnemySpecialPhase.Active:
                    Wire();
                    Hitbox?.Begin(BuildChargeHit());
                    break;

                case EnemySpecialPhase.Recovery:
                    Teardown();
                    owner.Physics.ResetInertia();
                    owner.Physics.Move(Vector3.zero, 0f);
                    break;

                case EnemySpecialPhase.Idle:
                    Teardown();
                    target = null;
                    break;
            }
        }

        /// <summary>돌진 데미지는 스탯을 따라간다. 인스펙터 값은 배율의 기준일 뿐이다.</summary>
        private HitData BuildChargeHit()
        {
            HitData h = chargeHit;
            h.damageData.damage = owner.Stats.GetValue(StatType.AttackPower, h.damageData.damage) * damageScale;
            return h;
        }

        private Vector3 AimDirection()
        {
            if (target == null) return owner.Physics.Facing;

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            return to.sqrMagnitude > 0.0001f ? to : owner.Physics.Facing;
        }

        // ── 구독 관리 ───────────────────────────────────
        // 돌진 중에만 붙인다. 항상 붙여 두면 평타 적중이 돌진을 끊는다.

        private void Wire()
        {
            if (wired) return;
            wired = true;

            Attack box = Hitbox;
            if (box != null) box.OnHit += HandleHit;
            owner.Physics.OnWallHit += HandleWall;
        }

        private void Teardown()
        {
            if (wired)
            {
                wired = false;

                Attack box = Hitbox;
                if (box != null) box.OnHit -= HandleHit;
                if (owner != null && owner.Physics != null) owner.Physics.OnWallHit -= HandleWall;
            }

            Hitbox?.End();
        }

        private void HandleHit(Combat victim) => sequence.HitOrWall();
        private void HandleWall(Physics.WallHit wall) => sequence.HitOrWall();

        private void OnDisable()
        {
            Cancel();
        }
    }
}
