using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 동료 제어. 라이브 페이즈에는 BT로 자율 전투하고,
    /// 지휘 중에는 정지한다 — ComboExecutor가 상태머신을 직접 강탈하기 때문(결정 로그 ②).
    /// </summary>
    public class AllyControl : Control
    {
        [Tooltip("근접 평타 사거리. 원거리 평타를 든 동료는 Entity의 사거리를 대신 쓴다.")]
        [SerializeField] private float attackRange = 2.2f;
        [SerializeField] private float attackInterval = 1.2f;
        [SerializeField] private float retargetInterval = 0.5f;
        [SerializeField] private float leashRange = 12f;

        private Entity target;
        private float attackTimer;
        private float retargetTimer;

        /// <summary>true면 BT를 완전히 정지한다. Executor가 켜고 끈다.</summary>
        public bool IsCommanded { get; set; }

        public Entity Target => target;

        public override void Tick(float dt)
        {
            Clear();

            // 지휘 중에는 어떤 명령도 내지 않는다. 상태머신은 Executor 소유.
            if (IsCommanded) return;
            if (Owner != null && (Owner.IsBusy || CombatStateRules.IsStunned(Owner.Combat.CombatState))) return;

            attackTimer -= dt;
            retargetTimer -= dt;

            HandleBehaviourTree();
        }

        private void HandleBehaviourTree()
        {
            if (retargetTimer <= 0f || target == null || target.Combat.IsDead)
            {
                retargetTimer = retargetInterval;
                target = BattleRegistry.NearestEnemy(transform.position);
            }

            if (target == null) return;

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;

            float dist = toTarget.magnitude;
            if (dist > leashRange) return;

            // 활을 든 동료가 근접까지 붙으면 원거리로 만든 의미가 없다.
            float reach = Owner != null && Owner.BasicIsRanged ? Owner.BasicAttackReach : attackRange;

            if (dist <= reach)
            {
                if (attackTimer <= 0f)
                {
                    attackTimer = attackInterval;
                    Command = Command.Attack;
                }
                return;
            }

            MoveDirection = toTarget.normalized;
            Command = Command.Move;
        }
    }
}
