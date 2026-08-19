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

        [Header("벽 바운드")]
        [Tooltip("반발 계수. 1이면 들어온 속도 그대로 튕긴다.")]
        [SerializeField] private float wallRestitution = 1.5f;
        [Tooltip("최소 반사 속력. 살살 닿아도 이만큼은 튕긴다.")]
        [SerializeField] private float wallMinBounce = 10f;
        [Tooltip("이 속도로 처박으면 띄우기가 최대가 된다.")]
        [SerializeField] private float wallHardSpeed = 18f;
        [Tooltip("벽에서 뜨는 높이 속도. 약하게 → 세게 박았을 때.")]
        [SerializeField] private float wallMinLaunch = 3f;
        [SerializeField] private float wallMaxLaunch = 15f;
        [Tooltip("연속 재바운드 방지. 이 시간 안에는 다시 튕기지 않는다.")]
        [SerializeField] private float wallBounceCooldown = 0.2f;

        [Header("대시 패링")]
        [Tooltip("대시 시작 후 패링 판정이 열려 있는 시간.\n\n" +
                 "예고(!)가 basicAttackWindup의 두 배 동안 떠 있으므로, 그 안에 누른 대시가 " +
                 "타격까지 살아 있으려면 이 값이 넉넉해야 한다.\n" +
                 "PlayerControl.dashCooldown(0.6)보다 크면 사실상 상시 무적이 되니 그보다는 작게 둘 것.")]
        [SerializeField] private float parryWindow = 0.45f;
        [Tooltip("전방으로 인정할 부채꼴의 전체 폭(도). 180이면 옆구리까지 막고 등 뒤만 통과한다.")]
        [SerializeField] private float parryAngle = 180f;
        [Tooltip("패링에 성공하면 이 시간 동안 완전 무적이 된다. 다대일에서 동시에 들어오는 " +
                 "나머지 타격까지 흘려 내는 구간이라, 이 값이 곧 '한 번의 패링으로 몇 대를 받아치는가'다.")]
        [SerializeField] private float parrySuccessInvuln = 0.3f;
        [Tooltip("패링당한 공격자가 먹는 경직.")]
        [SerializeField] private float parryCounterStun = 0.6f;
        [Tooltip("반격 밀치기. 0이면 제자리에서 경직만 먹는다.")]
        [SerializeField] private float parryCounterKnockback = 4f;

        [Header("가드 (보스)")]
        [Tooltip("가드 게이지 최대치. 단위는 <b>타격 횟수</b>다 — 10이면 열 대 맞고 깨진다.\n\n" +
                 "0 이하면 가드 시스템을 쓰지 않는다(잡몹 기본값). 값이 있으면 이 개체는 평소 " +
                 "슈퍼아머고, 맞아도 밀리거나 멈추지 않고 데미지만 받는다.")]
        [SerializeField] private float maxGuard = 0f;
        [Tooltip("hit.guardDamage가 비어 있는 타격이 깎는 양. 1이 곧 '한 대'다.\n\n" +
                 "데미지에 비례시키지 않는 이유: 연타로 깨는 맛을 살리기 위해서다. " +
                 "큰 기술 한 방보다 빠르게 몰아치는 쪽이 벽을 먼저 허문다.")]
        [SerializeField] private float defaultGuardDamage = 1f;
        [Tooltip("가드가 0이 됐을 때 무방비로 있는 시간. 이 구간에만 경직 · 넉백 · 공중 콤보가 통한다.")]
        [SerializeField] private float guardBreakDuration = 4f;
        [Tooltip("마지막 피격 후 이 시간이 지나면 가드가 자연 회복을 시작한다.")]
        [SerializeField] private float guardRegenDelay = 3f;
        [Tooltip("자연 회복 속도(초당 몇 대분). 연타를 끊으면 벽이 도로 서야 한다.")]
        [SerializeField] private float guardRegen = 2f;

        private Physics physics;
        private Entity owner;
        private Energy health;

        private float hitStunDuration;
        private float stunTimer;
        private float shield;
        private float wallBounceTimer;

        /// <summary>패링 판정이 열려 있는 남은 시간. 대시가 연다.</summary>
        private float parryTimer;

        /// <summary>패링 성공으로 얻은 무적의 남은 시간. 이 구간은 방향을 보지 않는다.</summary>
        private float parryInvulnTimer;

        private Energy guard;
        /// <summary>가드브레이크로 무방비인 남은 시간.</summary>
        private float guardBreakTimer;
        /// <summary>안 맞고 버틴 시간. 이게 다 차면 가드가 자연 회복을 시작한다.</summary>
        private float guardIdleTimer;

        /// <summary>공중에서 맞은 횟수. 기상(Getup) 완료 시에만 리셋된다(결정 로그 ⑦).</summary>
        private int airHitCount;

        public CombatState CombatState { get; private set; } = CombatState.Neutral;

        /// <summary>
        /// Awake 없이 접근하는 경로(에디터 테스트 · 생성기)를 위해 첫 접근에 만든다.
        /// Owner를 지연 해석하는 것과 같은 이유다.
        /// </summary>
        public Energy Health => health != null ? health : health = new Energy(EnergyType.Health, maxHealth);
        public Physics Physics => physics;
        public Entity Owner => owner != null ? owner : owner = GetComponent<Entity>();
        public int AirHitCount => airHitCount;
        public bool IsDead => CombatState == CombatState.Dead;

        /// <summary>패링 판정이 지금 열려 있는지.</summary>
        public bool IsParrying => parryTimer > 0f;

        /// <summary>패링 창 길이. <see cref="CombatStateRules.TelegraphLead"/>와 짝이 맞는지 보는 쪽이 읽는다.</summary>
        public float ParryWindow => parryWindow;

        /// <summary>패링 성공 직후의 무적 구간인지.</summary>
        public bool IsParryInvulnerable => parryInvulnTimer > 0f;

        // ── 가드 ────────────────────────────────────────

        /// <summary>가드 시스템을 쓰는 개체인가. 잡몹은 false다.</summary>
        public bool HasGuard => maxGuard > 0f;

        /// <summary>
        /// 가드 게이지. <see cref="Health"/>와 같은 이유로 첫 접근에 만든다 —
        /// Awake 없이 접근하는 경로(에디터 테스트 · 생성기)가 있다.
        /// </summary>
        public Energy Guard => guard != null ? guard : guard = new Energy(EnergyType.Guard, maxGuard);

        /// <summary>가드가 깨져 무방비인지. 이 구간에만 경직 · 넉백 · 공중 콤보가 통한다.</summary>
        public bool IsGuardBroken => guardBreakTimer > 0f;

        /// <summary>
        /// 지금 슈퍼아머인지. 가드를 가진 개체의 <b>기본 상태</b>다 —
        /// 깨지거나 죽었을 때만 풀린다.
        /// </summary>
        public bool IsSuperArmored => HasGuard && !IsGuardBroken && !IsDead;

        /// <summary>가드가 깨질 때 true, 복구될 때 false. 연출 · 라벨 · 사운드가 여기 붙는다.</summary>
        public event Action<bool> OnGuardBreakChanged;

        /// <summary>
        /// 가드 수치를 외부 테이블(EnemyData 등)로 덮어쓴다.
        /// <see cref="SetMaxHealth"/>와 같은 자리의 창구다 — private [SerializeField]를 밖에서 만지지 않게.
        /// </summary>
        public void SetGuard(float max, float breakDuration)
        {
            maxGuard = Mathf.Max(0f, max);
            guardBreakDuration = Mathf.Max(0f, breakDuration);

            guardBreakTimer = 0f;
            guardIdleTimer = 0f;
            Guard.SetMax(maxGuard, refill: true);
        }

        /// <summary>공격이 실제로 적중했을 때. 흡혈 · 콤보 카운트 · 이펙트가 여기 붙는다.</summary>
        public event Action<Combat, HitData> OnHitLanded;

        /// <summary>누가 누구를 때렸든 한 번씩. 시전자를 모르는 관전자(HUD 등)가 붙는다.</summary>
        public static event Action<Combat, Combat> OnAnyHitLanded;

        /// <summary>
        /// 대시 패링 성공. 인자는 (막은 쪽, 패링당한 공격자) 순이다.
        /// 게이지 보상 · 연출 · 사운드가 전부 여기 붙는다 — Combat은 "막았다"만 알린다.
        /// 공격자를 모르는 타격(장판 등)을 막으면 두 번째 인자가 null이다.
        /// </summary>
        public static event Action<Combat, Combat> OnParried;

        /// <summary>
        /// Enter Play Mode Options가 Domain Reload를 끄고 있어 static이 살아남는다.
        /// 리셋하지 않으면 지난 세션의 파괴된 구독자가 계속 호출된다(BattleRegistry와 같은 이유).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnAnyHitLanded = null;
            OnParried = null;
        }

        /// <summary>피격이 실제로 반영됐을 때. 경직 상태 진입 신호.</summary>
        public event Action<HitData, CombatState> OnHitTaken;
        public event Action<CombatState, CombatState> OnCombatStateChanged;
        public event Action OnDead;

        private void Awake()
        {
            physics = GetComponent<Physics>();
            owner = GetComponent<Entity>();

            // 이미 만들어진 것은 덮지 않는다. 컴포넌트 간 Awake 순서는 보장되지 않아
            // Enemy.Awake가 먼저 돌면 ApplyData가 주입한 최대치를 여기서 날려 버린다 —
            // 보스 HP 400이 프리팹 값 100으로 되돌아가는 증상이 그것이었다.
            if (health == null) health = new Energy(EnergyType.Health, maxHealth);
            if (guard == null) guard = new Energy(EnergyType.Guard, maxGuard);
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
            if (wallBounceTimer > 0f) wallBounceTimer -= dt;
            if (parryTimer > 0f) parryTimer -= dt;
            if (parryInvulnTimer > 0f) parryInvulnTimer -= dt;

            TickGuard(dt);

            // 타이머가 이미 0이어도 빠져나가지 않는다. 착지로만 풀리는 상태(넉백 · 공중피격)는
            // 타이머가 다 닳은 뒤에 지면으로 옮겨질 수 있다 — 교대 복귀의 Teleport가 그렇다.
            // 여기서 끊으면 복구 검사가 다시는 돌지 않아 경직이 영구히 남는다.
            if (stunTimer > 0f)
            {
                stunTimer -= dt;
                if (stunTimer > 0f) return;
            }

            CombatState prev = CombatState;
            CombatState next = CombatStateRules.OnStunEnd(prev);
            if (next == prev)
            {
                // 넉백 · 공중피격 계열은 착지로만 풀린다. 그런데 OnLand 없이 지면에 서는
                // 경로가 둘 있다 — launchForce 없는 넉백(애초에 뜨질 않는다)과
                // Physics.Teleport(교대 복귀 · 불릿타임 배치). 그대로 두면 경직이 고착되므로
                // 지면에 있으면 착지와 같게 처리해 다운 흐름에 합류시킨다.
                if (physics.PhysicsState == PhysicsState.Ground &&
                    CombatStateRules.OnGroundContact(prev) != prev)
                    HandleLand();
                return;
            }

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
                Health.Recover(heal);
                BattleLog.Log(LogCategory.Combat, $"{name} 흡혈 +{heal:0.#} (HP {Health.CurValue:0.#})", this);
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

            // 패링 성공 후의 무적 구간 — 방향을 보지 않는다. 사방에서 동시에 들어오는
            // 타격을 통째로 흘려 내는 게 이 구간의 존재 이유다.
            if (IsParryInvulnerable)
            {
                BattleLog.Log(LogCategory.Combat,
                    $"{name} <color=#4CC9F0>패링 무적</color>으로 흘림 — {BattleLog.Name(attacker)} " +
                    $"(남은 {parryInvulnTimer:0.##}s)", this);

                CounterAttack(attacker);
                return false;
            }

            // 대시 패링 — 데미지가 들어가기 전에 본다. 막았으면 맞지 않은 것으로 친다.
            if (TryParry(in hit, attacker)) return false;

            TakeDamage(hit.damageData);
            if (IsDead) return true;

            // 가드를 먼저 깎는다. 이 타격이 가드를 0으로 만들면 아래 아머 검사가 이미 풀려 있어
            // <b>깨뜨린 그 타격부터</b> 경직이 걸린다 — 브레이크가 한 대 늦게 열리지 않는다.
            DrainGuard(in hit);

            // 가드를 가진 개체는 평소가 슈퍼아머다. 데미지만 받고 밀리거나 멈추지 않는다.
            if (IsSuperArmored)
            {
                BattleLog.Log(LogCategory.Combat,
                    $"{name} <color=#FFD166>슈퍼아머</color> — 경직·넉백 무시 " +
                    $"(데미지만 적용, 가드 {Guard.CurValue:0}/{Guard.MaxValue:0})", this);
                return true;
            }

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
                $"{name} 피격 | {before} → <b>{next}</b> | HP {Health.CurValue:0.#}/{Health.MaxValue:0.#} | 경직 {hit.hitStunDuration:0.##}s", this);

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
                Health.Lose(dmg);

            if (Health.IsEmpty)
                Die();
        }

        /// <summary>
        /// 이 타격이 <b>어디서 왔는지</b>. 장판은 시전자와 떨어진 좌표에서 터지므로 기준점을 따로 싣고 오고,
        /// 없으면 시전자 위치를 쓴다 — 근접 히트박스와 투사체는 그게 맞다.
        /// 넉백 방향과 패링 전방 판정이 같은 기준을 봐야 해서 한 곳에 모아 둔다.
        /// </summary>
        private bool TryResolveHitOrigin(in HitData hit, Combat attacker, out Vector3 origin)
        {
            if (hit.hasOrigin)
            {
                origin = hit.origin;
                return true;
            }

            if (attacker != null)
            {
                origin = attacker.transform.position;
                return true;
            }

            // 공격자도 기준점도 없다 — 방향을 알 수 없다.
            origin = transform.position;
            return false;
        }

        private void ApplyKnockback(in HitData hit, Combat attacker)
        {
            TryResolveHitOrigin(in hit, attacker, out Vector3 casterPos);
            Vector3 casterFwd = attacker != null ? attacker.Physics.Facing : physics.Facing;

            // 관성 강제 초기화 — 연계 스킬이 빗나가는 오차를 차단한다.
            physics.ResetInertia();

            if (hit.snapZ && (attacker != null || hit.hasOrigin))
                physics.SnapZ(casterPos.z);

            Vector3 dir = hit.ResolveDirection(casterPos, casterFwd, transform.position);
            if (hit.knockbackForce > 0f)
                physics.AddImpulse(dir, PullClamped(hit, casterPos), resetInertia: false);

            if (hit.launchForce > 0f)
                physics.AddLaunch(hit.launchForce);
        }

        /// <summary>
        /// 끌어당기기의 충격량을 <b>중심까지의 거리</b>로 잘라 준다.
        ///
        /// <see cref="Physics.AddImpulse"/>는 지수감쇠라 이동거리가 <c>force / impulseDamping</c>로
        /// 고정된다 — 거리와 무관하다. 그래서 고정값을 쓰면 중심 가까이 있던 적이 중심을 지나쳐
        /// 반대편으로 튀고, 맞은편 적과 교차하면서 오히려 흩어진다.
        ///
        /// 잘라 두면 <c>knockbackForce</c>는 "최대 끌어올 거리"의 의미가 되고,
        /// 멀리 있는 적만 힘을 다 쓰므로 전원이 중심에 모인다.
        /// </summary>
        private float PullClamped(in HitData hit, Vector3 center)
        {
            if (hit.mode != KnockbackMode.TowardCaster) return hit.knockbackForce;

            Vector3 flat = center - transform.position;
            flat.y = 0f;

            return Mathf.Min(hit.knockbackForce, physics.ImpulseToTravel(flat.magnitude));
        }

        // ── 가드 · 가드브레이크 ──────────────────────────

        /// <summary>
        /// 브레이크 타이머와 자연 회복. <see cref="Tick"/>이 스케일된 dt로 부른다.
        /// </summary>
        private void TickGuard(float dt)
        {
            if (!HasGuard) return;

            if (guardBreakTimer > 0f)
            {
                guardBreakTimer -= dt;
                if (guardBreakTimer > 0f) return;

                // 브레이크가 끝나면 가드는 가득 찬 상태로 돌아온다 — 다시 벽이 된다.
                guardBreakTimer = 0f;
                Guard.Recover(Guard.MaxValue);
                OnGuardBreakChanged?.Invoke(false);

                BattleLog.Log(LogCategory.Combat, $"{name} 가드 회복 — 슈퍼아머 복귀", this);
                return;
            }

            if (Guard.IsFull) return;

            // 맞는 동안은 회복하지 않는다. 찔끔찔끔 때리다 말면 처음부터 다시다.
            if (guardIdleTimer > 0f)
            {
                guardIdleTimer -= dt;
                if (guardIdleTimer > 0f) return;

                // 지연을 넘긴 만큼만 회복에 쓴다. 남은 dt를 버리면 프레임이 길 때
                // (불릿타임 복귀 · 에디터 스텝) 회복이 한 프레임씩 밀린다.
                dt = -guardIdleTimer;
                guardIdleTimer = 0f;
            }

            Guard.Recover(guardRegen * dt);
        }

        /// <summary>
        /// 이 타격이 깎는 가드. 0이 되면 <see cref="BreakGuard"/>가 무방비 구간을 연다.
        /// 브레이크 중에는 더 깎지 않는다 — 이미 바닥이고, 회복 시점은 타이머가 정한다.
        /// </summary>
        private void DrainGuard(in HitData hit)
        {
            if (!HasGuard || IsGuardBroken) return;

            float loss = GuardLossOf(in hit);
            if (loss <= 0f) return;

            Guard.Lose(loss);
            guardIdleTimer = guardRegenDelay;

            if (Guard.IsEmpty) BreakGuard();
        }

        /// <summary>
        /// 이 타격이 깎을 가드량. 명시값이 있으면 그것을(= 몇 대분인지), 없으면 한 대로 친다.
        ///
        /// <b>데미지가 0인 타격은 가드를 깎지 않는다</b> — 패링 반격처럼 "기회"만 주는 판정이
        /// 벽을 대신 허물면 안 된다. 가드만 깎는 타격을 만들려면 <c>guardDamage</c>를 명시하면 된다.
        /// </summary>
        private float GuardLossOf(in HitData hit)
        {
            if (hit.guardDamage > 0f) return hit.guardDamage;

            return hit.damageData.damage > 0f ? defaultGuardDamage : 0f;
        }

        /// <summary>
        /// 가드브레이크. 정해진 시간 동안 슈퍼아머가 풀리고 아무 행동도 하지 못한다
        /// (<see cref="EnemyControl"/>이 이 값을 보고 명령을 끊는다).
        ///
        /// <see cref="ClearHitStun"/>을 부르는 이유: 여기까지 오는 동안은 아머라 경직이 없었지만
        /// 공중 히트 누적 · 중력 보정 · 벽바운드 쿨 같은 잔재가 남아 있다.
        /// 그걸 정리해야 이어지는 콤보가 <b>처음부터</b> 걸린다.
        /// </summary>
        private void BreakGuard()
        {
            guardBreakTimer = guardBreakDuration;
            guardIdleTimer = 0f;

            ClearHitStun();
            OnGuardBreakChanged?.Invoke(true);

            BattleLog.Log(LogCategory.Combat,
                $"{name} <color=#E24AFF><b>가드 브레이크</b></color> — {guardBreakDuration:0.##}s 무방비", this);
        }

        // ── 대시 패링 ───────────────────────────────────

        /// <summary>
        /// 패링 판정을 연다. 대시 명령을 처리하는 <see cref="EntityState"/>가 부른다.
        ///
        /// 대시는 상태가 아니라 <see cref="Physics.Dash"/> 한 번으로 끝나는 속도 덮어쓰기라
        /// "대시 도중"이라는 시간이 코드에 없다. 그 시간을 여기 타이머로만 만든다 —
        /// 상태머신도 대시 감각도 건드리지 않는다.
        /// </summary>
        public void BeginParryWindow()
        {
            if (IsDead) return;

            parryTimer = parryWindow;
            BattleLog.Log(LogCategory.Combat, $"{name} 패링 창 열림 ({parryWindow:0.##}s)", this);
        }

        /// <summary>
        /// 전방에서 들어온 타격을 막았는가. 막았으면 <see cref="Hit"/>가 false를 돌려주고
        /// 그 타격은 <b>없던 일이 된다</b> — 데미지 · 경직 · 흡혈 · 피격음까지 전부.
        ///
        /// <b>성립하는 건 첫 한 대뿐이고, 그 대가로 짧은 무적을 얻는다</b>(<see cref="parrySuccessInvuln"/>).
        /// 개별 타격을 하나씩 지우는 방식이면 다대일에서 한 명분만 막고 나머지를 그대로 맞아
        /// 패링이 사실상 무의미해진다. 뒤이어 들어오는 타격은 무적 구간이 받는다.
        /// </summary>
        private bool TryParry(in HitData hit, Combat attacker)
        {
            if (!IsParrying) return false;

            // 어디서 온 타격인지 모르면 막을 수 없다.
            if (!TryResolveHitOrigin(in hit, attacker, out Vector3 origin)) return false;

            if (!CombatStateRules.IsFrontal(physics.Facing, origin - transform.position, parryAngle))
                return false;

            // 창을 닫고 무적으로 갈아탄다. 반격이 돌아와도 두 번 성립하지 않는다.
            parryTimer = 0f;
            parryInvulnTimer = parrySuccessInvuln;

            BattleLog.Log(LogCategory.Combat,
                $"{name} <color=#4CC9F0><b>패링</b></color> — {BattleLog.Name(attacker)}의 공격을 흘렸다 " +
                $"(무효 dmg {hit.damageData.damage:0.#} | 무적 {parrySuccessInvuln:0.##}s)", this);

            CounterAttack(attacker);
            OnParried?.Invoke(this, attacker);

            return true;
        }

        /// <summary>
        /// 패링당한 쪽에 되돌려 주는 경직. 데미지는 0이다 — 패링의 보상은 딜이 아니라 <b>기회</b>다.
        ///
        /// <see cref="Attack"/>이 아니라 <see cref="Hit"/>를 직접 부른다.
        /// 흡혈 · 콤보 카운트 · 피격음이 반격에 붙으면 안 되기 때문이다.
        /// 상대가 슈퍼아머면 기존 규칙대로 경직만 무시된다 — 그건 정상 동작이다.
        /// </summary>
        private void CounterAttack(Combat attacker)
        {
            if (attacker == null || attacker.IsDead) return;

            var counter = new HitData
            {
                damageData = new DamageData(0f),
                targetState = CombatState.Neutral,
                nextState = CombatState.LightHit,
                mode = KnockbackMode.AwayFromCaster,
                knockbackForce = parryCounterKnockback,
                launchForce = 0f,
                hitStunDuration = parryCounterStun,
            };

            attacker.Hit(in counter, this);
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

        private void HandleWallHit(Physics.WallHit wall)
        {
            // 벽 모서리에서 접촉이 연달아 들어오면 같은 자리에서 계속 튕긴다.
            if (wallBounceTimer > 0f) return;

            CombatState next = CombatStateRules.OnWallContact(CombatState);
            if (next == CombatState) return;

            wallBounceTimer = wallBounceCooldown;

            // 세게 처박을수록 크게 튕기고 높이 뜬다. 0~1로 정규화해 한 값으로 전부 몬다.
            float force = Mathf.Clamp01(wall.speed / Mathf.Max(0.01f, wallHardSpeed));

            SetCombatState(next);
            owner?.RequestHitReaction(next);

            float back = physics.Reflect(wall.normal, wallRestitution, wallMinBounce);
            physics.AddLaunch(Mathf.Lerp(wallMinLaunch, wallMaxLaunch, force));

            EmitWallVfx(in wall, force);

            BattleLog.Log(LogCategory.Physics,
                $"{name} <b>벽 바운드</b> | {CombatState} → <b>{next}</b> | " +
                $"충돌 {wall.speed:0.#} → 반사 {back:0.#} (세기 {force * 100f:0}%)", this);
        }

        /// <summary>부딪힌 벽면에서 터뜨린다. 세게 박을수록 크게.</summary>
        private void EmitWallVfx(in Physics.WallHit wall, float force)
        {
            Vector3 ground = wall.point;
            float height = Mathf.Max(0f, ground.y - physics.GroundY);
            ground.y = 0f;

            // 방향은 벽 바깥쪽 — 파편이 벽에서 튀어나오는 것처럼 읽힌다.
            BattleVfx.WallBounce(ground, height, wall.normal, Mathf.Lerp(0.5f, 1.3f, force));
        }

        /// <summary>
        /// 경직을 통째로 지우고 중립으로 되돌린다. <b>태그로 내려가는 몸</b>이 쓴다
        /// (<see cref="Entity.ReleaseBody"/>).
        ///
        /// 내려가는 몸은 <c>SetActive(false)</c>로 꺼져 <see cref="Tick"/>이 멈춘다 —
        /// 경직을 물고 내려가면 그 상태가 그대로 얼어붙고, 다시 설 때
        /// <see cref="Physics.Teleport"/>가 착지 이벤트 없이 지면에 세우기 때문에
        /// 착지로만 풀리는 상태(넉백 · 공중피격)가 영영 안 풀린다.
        /// 필드에서 뺀 몸에 경직을 남길 이유도 없다.
        ///
        /// 사망은 건드리지 않는다 — 시체를 중립으로 되돌리면 다시 섰을 때 되살아난다.
        /// </summary>
        public void ClearHitStun()
        {
            if (IsDead) return;

            stunTimer = 0f;
            wallBounceTimer = 0f;
            parryTimer = 0f;
            parryInvulnTimer = 0f;
            airHitCount = 0;
            physics?.AddGravity(1f);

            SetCombatState(CombatState.Neutral);
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
        public void SetMaxHealth(float value, bool refill = true) => Health.SetMax(value, refill);

        public float Shield => shield;
        public void AddShield(float amount) => shield += Mathf.Max(0f, amount);
        public void ClearShield() => shield = 0f;
    }
}
