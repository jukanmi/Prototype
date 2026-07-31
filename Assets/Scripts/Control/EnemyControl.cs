using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적 AI. 아군을 추적하고 사거리에 들어오면 공격한다.
    /// 스테이지 종료 시 <see cref="SetActive"/>(false)로 정지시킨다.
    /// </summary>
    public class EnemyControl : Control
    {
        [SerializeField] private float attackRange = 1.8f;
        [SerializeField] private float attackInterval = 1.5f;
        [SerializeField] private float retargetInterval = 0.5f;

        private Entity target;
        private float attackTimer;
        private float retargetTimer;
        private bool active = true;

        public Entity Target => target;

        /// <summary>도발 등으로 타겟을 강제 지정한다.</summary>
        public void SetTarget(Entity forced)
        {
            target = forced;
            retargetTimer = retargetInterval;
        }

        public void SetActive(bool value)
        {
            active = value;
            if (!active) Clear();
        }

        public override void Tick(float dt)
        {
            Clear();
            if (!active) return;
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
                target = FindNearestAlly();
            }

            if (target == null) return;

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;

            // 원거리 평타를 든 적이 생기면 그쪽 사거리를 따른다.
            float reach = Owner != null && Owner.BasicIsRanged ? Owner.BasicAttackReach : attackRange;

            if (toTarget.magnitude <= reach)
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

        private Entity FindNearestAlly()
        {
            Entity best = null;
            float bestSqr = float.MaxValue;

            foreach (Entity e in BattleRegistry.Allies)
            {
                if (e == null || e.Combat.IsDead) continue;

                float sqr = (e.transform.position - transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = e;
            }

            return best;
        }
    }
}
