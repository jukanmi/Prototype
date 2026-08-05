using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 타격 · 피격 · 상태 전이의 단일 창구.
    /// 온힛 트리거 · 흡혈 · 콤보 카운트를 여기 한 곳에서 처리한다(결정 로그 ②).
    /// </summary>
    [RequireComponent(typeof(Physics))]
    public class Combat : MonoBehaviour, IHittable, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float lifestealRatio = 0f;
        [SerializeField] private float damageCutRatio = 0f;
        [SerializeField] private float downDuration = 1.2f;
        [SerializeField] private float getupDuration = 0.4f;

        private Physics physics;
        private Entity owner;
        private Energy health;

        private float hitStunDuration;
        private float stunTimer;
        private float shield;

        /// <summary>공중에서 맞은 횟수. 기상(Getup) 완료 시에만 리셋된다(결정 로그 ⑦).</summary>
        private int airHitCount;

        public CombatState CombatState { get; private set; } = CombatState.Neutral;
        public Energy Health => health;
        public Physics Physics => physics;
        public Entity Owner => owner != null ? owner : owner = GetComponent<Entity>();
        public int AirHitCount => airHitCount;
        public bool IsDead => CombatState == CombatState.Dead;

        /// <summary>공격이 실제로 적중했을 때. 흡혈 · 콤보 카운트 · 이펙트가 여기 붙는다.</summary>
        public event Action<Combat, HitData> OnHitLanded;

        /// <summary>누가 누구를 때렸든 한 번씩. 시전자를 모르는 관전자(HUD 등)가 붙는다.</summary>
        public static event Action<Combat, Combat> OnAnyHitLanded;

        /// <summary>
        /// Enter Play Mode Options가 Domain Reload를 끄고 있어 static이 살아남는다.
        /// 리셋하지 않으면 지난 세션의 파괴된 구독자가 계속 호출된다(BattleRegistry와 같은 이유).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => OnAnyHitLanded = null;

        /// <summary>피격이 실제로 반영됐을 때. 경직 상태 진입 신호.</summary>
        public event Action<HitData, CombatState> OnHitTaken;
        public event Action<CombatState, CombatState> OnCombatStateChanged;
        public event Action OnDead;

        private void Awake()
        {
            physics = GetComponent<Physics>();
            owner = GetComponent<Entity>();
            health = new Energy(EnergyType.Health, maxHealth);
        }

        private void OnEnable()
        {
            physics.OnLand += HandleLand;
            physics.OnWallHit += HandleWallHit;
        }

        private void OnDisable()
        {
            physics.OnLand -= HandleLand;
            physics.OnWallHit -= HandleWallHit;
        }

        /// <summary>Entity가 스케일된 dt로 호출한다.</summary>
        public void Tick(float dt)
        {
            if (stunTimer <= 0f) return;

            stunTimer -= dt;
            if (stunTimer > 0f) return;

            CombatState prev = CombatState;
            CombatState next = CombatStateRules.OnStunEnd(prev);
            if (next == prev) return;

            // 다운 → 기상 → 복귀는 각각 고유 지속시간을 갖는다.
            if (next == CombatState.Getup)
            {
                SetCombatState(next);
                owner?.RequestHitReaction(next);
                stunTimer = getupDuration;
                return;
            }

            // airHitCount 리셋은 오직 기상 완료 시점에만(결정 로그 ⑦).
            if (prev == CombatState.Getup)
            {
                OnGetupComplete();
                return;
            }

            SetCombatState(next);
            owner?.RequestHitReaction(next);
            if (next == CombatState.Neutral && owner != null)
                owner.StateMachine.TryChangeState(owner.IdleState);
        }

        // ── 때리는 쪽 ───────────────────────────────────

        /// <summary>
        /// 판정과 부가효과의 주체. Attack 컴포넌트는 대상만 넘겨준다.
        /// </summary>
        public void Attack(IHittable target, in HitData hit)
        {
            if (target == null || IsDead) return;
            if (ReferenceEquals(target, this)) return;

            if (!target.Hit(in hit, this)) return;

            if (lifestealRatio > 0f)
            {
                float heal = hit.damageData.damage * lifestealRatio;
                health.Recover(heal);
                BattleLog.Log(LogCategory.Combat, $"{name} 흡혈 +{heal:0.#} (HP {health.CurValue:0.#})", this);
            }

            BattleLog.Log(LogCategory.Combat,
                $"{name} → {BattleLog.Name((target as Combat))} 적중 | dmg {hit.damageData.damage:0.#} | {hit.mode} | 결과요청 {hit.nextState}", this);

            OnHitLanded?.Invoke(this, hit);
            if (target is Combat victim)
                OnAnyHitLanded?.Invoke(this, victim);
        }

        // ── 맞는 쪽 ─────────────────────────────────────

        public bool Hit(in HitData hit, Combat attacker)
        {
            if (IsDead) return false;

            // 무적 판정 — 다운/기상은 OTG를 제외하면 통과하지 않는다.
            if (CombatStateRules.IsInvincible(CombatState, hit.canOtg))
            {
                BattleLog.Log(LogCategory.Combat,
                    $"{name} <color=#808080>무적으로 흘림</color> ({CombatState}, OTG {hit.canOtg})", this);
                return false;
            }

            TakeDamage(hit.damageData);
            if (IsDead) return true;

            // 슈퍼아머: 상태머신이 전이를 거부하면 경직 · 넉백을 적용하지 않는다.
            // 데미지는 이미 들어갔다(결정 로그 ③).
            CombatState next = CombatStateRules.Next(CombatState, in hit, airHitCount);
            if (owner != null && !owner.RequestHitReaction(next))
            {
                BattleLog.Log(LogCategory.Combat,
                    $"{name} <color=#FFD166>슈퍼아머</color> — 경직·넉백 무시 (데미지만 적용)", this);
                return true;
            }

            ApplyKnockback(in hit, attacker);

            CombatState before = CombatState;
            SetCombatState(next);

            hitStunDuration = hit.hitStunDuration;
            stunTimer = hitStunDuration;

            if (next == CombatState.AerialHit)
            {
                airHitCount++;
                // 맞을수록 무겁게 떨어진다. 무한 홀딩 차단.
                float g = CombatStateRules.AirGravityScale(airHitCount);
                physics.AddGravity(g);
                BattleLog.Log(LogCategory.Combat,
                    $"{name} 공중히트 {airHitCount}/{CombatStateRules.MaxAirHit} → 중력 x{g:0.00}", this);
            }

            BattleLog.Log(LogCategory.Combat,
                $"{name} 피격 | {before} → <b>{next}</b> | HP {health.CurValue:0.#}/{health.MaxValue:0.#} | 경직 {hit.hitStunDuration:0.##}s", this);

            OnHitTaken?.Invoke(hit, next);
            return true;
        }

        public void TakeDamage(in DamageData damageData)
        {
            if (IsDead) return;

            float dmg = damageData.damage * (1f - Mathf.Clamp01(damageCutRatio));

            // 보호막이 먼저 닳는다.
            if (shield > 0f)
            {
                float absorbed = Mathf.Min(shield, dmg);
                shield -= absorbed;
                dmg -= absorbed;
                BattleLog.Log(LogCategory.Combat, $"{name} 보호막 흡수 {absorbed:0.#} (잔여 {shield:0.#})", this);
            }

            if (dmg > 0f)
                health.Lose(dmg);

            if (health.IsEmpty)
                Die();
        }

        private void ApplyKnockback(in HitData hit, Combat attacker)
        {
            Vector3 casterPos = attacker != null ? attacker.transform.position : transform.position;
            Vector3 casterFwd = attacker != null ? attacker.Physics.Facing : physics.Facing;

            // 관성 강제 초기화 — 연계 스킬이 빗나가는 오차를 차단한다.
            physics.ResetInertia();

            if (hit.snapZ && attacker != null)
                physics.SnapZ(casterPos.z);

            Vector3 dir = hit.ResolveDirection(casterPos, casterFwd, transform.position);
            if (hit.knockbackForce > 0f)
                physics.AddImpulse(dir, hit.knockbackForce, resetInertia: false);

            if (hit.launchForce > 0f)
                physics.AddLaunch(hit.launchForce);
        }

        private void Die()
        {
            BattleLog.Log(LogCategory.Combat, $"<b>{name} 사망</b> — ForceChangeState로 관통", this);
            SetCombatState(CombatState.Dead);
            stunTimer = 0f;
            physics.ResetInertia();
            owner?.ForceDead();
            OnDead?.Invoke();
        }

        // ── 물리 이벤트 반응 ─────────────────────────────

        private void HandleLand()
        {
            CombatState next = CombatStateRules.OnGroundContact(CombatState);
            if (next == CombatState) return;

            BattleLog.Log(LogCategory.Physics, $"{name} 착지 | {CombatState} → <b>{next}</b>", this);

            SetCombatState(next);
            owner?.RequestHitReaction(next);
            if (next == CombatState.Down)
            {
                stunTimer = downDuration;
                physics.ResetInertia();
            }
        }

        private void HandleWallHit(Vector3 normal)
        {
            CombatState next = CombatStateRules.OnWallContact(CombatState);
            if (next == CombatState) return;

            BattleLog.Log(LogCategory.Physics, $"{name} 벽 접촉 | {CombatState} → <b>{next}</b>", this);

            SetCombatState(next);
            // 벽에서 튕겨 나오며 공중 체류 시간을 번다.
            physics.AddImpulse(normal, 4f, resetInertia: true);
            physics.AddLaunch(3f);
        }

        /// <summary>기상 완료. 공중 콤보 카운트와 중력 보정을 여기서만 되돌린다(결정 로그 ⑦).</summary>
        public void OnGetupComplete()
        {
            BattleLog.Log(LogCategory.Combat,
                $"{name} 기상 완료 — airHitCount {airHitCount} → 0, 중력 보정 해제", this);

            airHitCount = 0;
            physics.AddGravity(1f);
            SetCombatState(CombatState.Neutral);
            if (owner != null)
                owner.StateMachine.TryChangeState(owner.IdleState);
        }

        private void SetCombatState(CombatState next)
        {
            if (CombatState == next) return;

            CombatState prev = CombatState;
            CombatState = next;
            OnCombatStateChanged?.Invoke(prev, next);
        }

        // ── 외부 조작 (ISkillEffect 용) ───────────────────

        public void SetDamageCut(float ratio) => damageCutRatio = Mathf.Clamp01(ratio);
        public void SetLifesteal(float ratio) => lifestealRatio = Mathf.Max(0f, ratio);

        /// <summary>EnemyData 등 외부 테이블로 최대 체력을 덮어쓴다.</summary>
        public void SetMaxHealth(float value, bool refill = true) => health.SetMax(value, refill);

        public float Shield => shield;
        public void AddShield(float amount) => shield += Mathf.Max(0f, amount);
        public void ClearShield() => shield = 0f;
    }
}
