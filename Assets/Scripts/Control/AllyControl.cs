using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 동료 자율 전투 BT. <b>조작 대상이 아닌 몸</b>이 이걸로 움직인다 —
    /// 태그로 필드에 선 한 명은 <see cref="PlayerControl"/>이 몰고, 불릿타임에 불려 나온
    /// 나머지가 여기에 해당한다.
    ///
    /// 지휘 중에는 정지한다 — ComboExecutor가 상태머신을 직접 강탈하기 때문(결정 로그 ②).
    /// </summary>
    public class AllyControl : Control
    {
        [Tooltip("근접 평타 사거리. 원거리 평타를 든 동료는 Entity의 사거리를 대신 쓴다.")]
        [SerializeField] private float attackRange = 2.2f;
        [SerializeField] private float attackInterval = 1.2f;
        [SerializeField] private float retargetInterval = 0.5f;
        [SerializeField] private float leashRange = 12f;

        [Tooltip("적이 없을 때 리더를 따라붙는 속도 배율. 1이면 평소 이동속도 그대로.")]
        [SerializeField] private float followSpeedScale = 1f;

        private Entity target;
        private float attackTimer;
        private float retargetTimer;

        public Entity Target => target;

        /// <summary>대형에서 이 동료의 자리. 등록 순서로 정해지고 한 번 잡히면 안 바뀐다.</summary>
        private int formationSlot = -1;

        public override void Tick(float dt)
        {
            Clear();

            // 지휘 중에는 어떤 명령도 내지 않는다. 상태머신은 Executor 소유.
            if (Owner == null || Owner.IsCommanded) return;
            if (Owner.IsBusy || CombatStateRules.IsStunned(Owner.Combat.CombatState)) return;

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

            // 적이 없으면(통로) 리더를 따라간다. 이게 없으면 동료가 아레나에 남고
            // 다음 아레나는 혼자 시작된다 — 문이 닫힌 뒤에는 부를 방법도 없다.
            if (target == null) { Follow(); return; }

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;

            float dist = toTarget.magnitude;

            // 리쉬 밖의 적은 없는 셈 친다. 그냥 멈춰 서면 스테이지를 넘어가는 동안 뒤처진다.
            if (dist > leashRange) { Follow(); return; }

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

        /// <summary>
        /// 조작 중인 몸을 대형 자리로 따라간다.
        ///
        /// 리더의 <b>발밑 좌표</b>가 아니라 대형 자리를 향한다는 점이 중요하다 —
        /// 전부 리더에게 직행하면 한 점에 뭉쳐 서로 밀어내고, 아레나에 들어가는 순간
        /// 다섯이 같은 칸에서 출발한다.
        /// </summary>
        private void Follow()
        {
            Entity leader = Leader();
            if (leader == null) return;

            Vector3 goal = PartyFormation.PointFor(
                leader.Physics.GroundPosition, Slot(), leader.Physics.Facing.x);

            Vector3 toGoal = goal - transform.position;
            toGoal.y = 0f;

            if (!PartyFormation.ShouldClose(toGoal.magnitude)) return;

            MoveDirection = toGoal.normalized * Mathf.Max(0.1f, followSpeedScale);
            Command = Command.Move;
        }

        /// <summary>
        /// 지금 유저가 모는 몸. <see cref="PlayerControl"/>이 붙어 있고 켜져 있는 쪽이다 —
        /// 태그 교대로 매 순간 바뀌므로 캐시하지 않는다.
        /// </summary>
        private Entity Leader()
        {
            foreach (Entity e in BattleRegistry.Allies)
            {
                if (e == null || e == Owner || !e.isActiveAndEnabled || e.Combat.IsDead) continue;
                if (e.Control is PlayerControl) return e;
            }

            return null;
        }

        /// <summary>
        /// 대형 자리. 등록 순서로 한 번만 정한다 — 매 프레임 다시 계산하면
        /// 앞의 동료가 죽을 때마다 번호가 밀려서 전원이 자리를 옮긴다.
        /// </summary>
        private int Slot()
        {
            if (formationSlot >= 0) return formationSlot;

            int n = 0;
            foreach (Entity e in BattleRegistry.Allies)
            {
                if (e == null) continue;
                if (e == Owner) { formationSlot = n; return formationSlot; }
                n++;
            }

            formationSlot = 0;
            return formationSlot;
        }
    }
}
