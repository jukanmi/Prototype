using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 평타 한 벌. 히트박스 · 타격(<see cref="HitData"/>) · 투사체 · 단계 표가 여기 모인다.
    ///
    /// <b>평타는 단계 표(<see cref="stages"/>) 하나로 정의된다.</b> 단타는 칸 하나, 3연타는 칸 셋 —
    /// 공격키를 누를 때마다 다음 칸으로 넘어가고, 마지막 칸에서는 눌러도 반응이 없다.
    /// 타이밍은 칸마다 적는다. "비우면 기본값" 같은 두 번째 층은 없다.
    ///
    /// 이어치기는 유저가 모는 몸에서만 일어난다 — <see cref="AttackState"/>가 선입력
    /// (<see cref="AttackInputBuffer"/>)을 보고 넘어가는데, 그 버퍼를 채우는 건 <see cref="PlayerPilot"/> 하나뿐이다.
    /// 그래서 <b>적에게 단계를 채우면 조용히 1타에서 멈춘다.</b> 이 저작 실수는
    /// <see cref="Validate"/> 경고와 <c>BasicComboPrefabTests.EnemiesStaySingleHit</c>가 잡는다.
    ///
    /// 단계별로 <b>완전한 HitData를 두지 않는</b> 규약은 <see cref="BasicComboRules"/>에 적혀 있다.
    /// 여기 든 <see cref="basicHit"/>이 기반 한 벌이고, 단계는 그 위에 델타만 얹는다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Prototype/평타")]
    public sealed class BasicAttackProfile : MonoBehaviour
    {
        [Header("히트박스")]
        [Tooltip("비우면 자식에서 찾는다.")]
        [SerializeField] private Attack basicAttack;

        [Tooltip("스킬 전용 히트박스. 비우면 평타 히트박스를 재사용한다.")]
        [SerializeField] private Attack skillAttack;

        [Header("타격")]
        [Tooltip("모든 칸이 공유하는 타격. 칸은 이 위에 배율과(필요하면) 반응만 얹는다.")]
        [SerializeField]
        private HitData basicHit = new HitData
        {
            damageData = new DamageData(10f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            pushDistance = 0.375f,
            hitStunDuration = 0.3f,
        };

        [Header("원거리")]
        [Tooltip("넣으면 평타가 투사체가 된다. 비우면 앞에 히트박스를 켜는 근접 평타.")]
        [SerializeField] private Projectile projectile;
        [SerializeField] private float projectileSpeed = 16f;
        [SerializeField] private float projectileRange = 9f;
        [SerializeField] private int projectilePierce = 0;

        [Header("단계")]
        [Tooltip("평타 전부. 단타는 칸 하나, 연타는 그 수만큼. 적은 칸 하나만 둘 것 — AI는 이어치기를 못 한다.\n\n" +
                 "반응(상태 · 넉백 · 띄우기) 칸은 overrideReaction 을 켠 칸에서만 산다.")]
        [SerializeField]
        private BasicAttackStage[] stages =
        {
            new BasicAttackStage { label = "1타", windup = 0.12f, activeEnd = 0.24f, total = 0.45f, damageMultiplier = 1f },
        };

        // ── 히트박스 ────────────────────────────────────

        public Attack Hitbox => basicAttack;

        /// <summary>스킬이 쓸 히트박스. 따로 안 두면 평타 것을 그대로 쓴다.</summary>
        public Attack SkillHitbox => skillAttack != null ? skillAttack : basicAttack;

        /// <summary>자식에서 히트박스를 찾아 물린다. <see cref="Entity.Awake"/>가 한 번 부른다.</summary>
        public void ResolveHitbox(Combat attacker)
        {
            if (basicAttack == null) basicAttack = GetComponentInChildren<Attack>(true);
            if (basicAttack != null && basicAttack.Attacker == null) basicAttack.Attacker = attacker;
        }

        // ── 단계 ────────────────────────────────────────

        /// <summary>단계 수. 0이면 평타가 없다(<see cref="Validate"/>가 알린다).</summary>
        public int StageCount => stages != null ? stages.Length : 0;

        /// <summary>연타를 저작한 몸인지.</summary>
        public bool HasCombo => StageCount > 1;

        /// <summary>이 단계에 재생할 클립. 없으면 null — 애니메이터가 기본 평타 클립으로 떨어진다.</summary>
        public AnimationClip ClipFor(int stage) => Has(stage) ? stages[stage].clip : null;

        /// <summary>단계 타이밍. 없는 단계면 전부 0 — 상태가 즉시 끝난다.</summary>
        public BasicAttackTiming TimingFor(int stage)
        {
            if (Has(stage)) return BasicComboRules.ResolveTiming(in stages[stage]);

            BasicAttackStage none = default;
            return BasicComboRules.ResolveTiming(in none);
        }

        /// <summary>
        /// 단계를 갈아 끼운다. <c>null</c>이나 빈 배열은 무시한다 —
        /// 표에 안 적었다고 프리팹의 연타가 사라지면 안 된다.
        ///
        /// <b><see cref="Entity.Awake"/>보다 먼저</b> 불러야 안전하다.
        /// <see cref="EntityAnimator"/>가 Awake에서 1타 클립을 잡아 두고
        /// 그 위에 오버라이드를 씌우기 때문이다.
        /// </summary>
        public void ConfigureCombo(BasicAttackStage[] source)
        {
            if (source == null || source.Length == 0) return;
            stages = source;
        }

        /// <summary>단계의 델타를 얹는다. 없는 단계면 기반 그대로.</summary>
        private HitData ApplyStage(in HitData basic, int stage)
            => Has(stage) ? BasicComboRules.BuildStageHit(in basic, in stages[stage]) : basic;

        private bool Has(int stage) => stages != null && stage >= 0 && stage < stages.Length;

        // ── 타격 ────────────────────────────────────────

        /// <summary>
        /// <paramref name="stage"/>타째의 타격. 공격력을 실은 뒤에 단계 델타가 얹힌다 —
        /// 순서가 뒤집히면 배율이 프리팹에 적힌 기본 데미지에만 걸린다.
        /// 원본은 건드리지 않는다.
        /// </summary>
        public HitData Build(Stats stats, int stage)
        {
            HitData h = basicHit;
            if (stats != null)
                h.damageData.damage = stats.GetValue(StatType.AttackPower, h.damageData.damage);

            return ApplyStage(in h, stage);
        }

        // ── 원거리 ──────────────────────────────────────

        /// <summary>평타가 날아가는지. <see cref="AttackState"/>가 이걸로 갈린다.</summary>
        public bool IsRanged => projectile != null;

        public Projectile Projectile => projectile;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileRange => projectileRange;
        public int ProjectilePierce => projectilePierce;

        /// <summary>
        /// 원거리 평타의 유효 사거리. AI가 이 거리에서 멈춰 선다.
        /// 최대 사거리보다 짧게 잡아 가장자리에서 헛쏘지 않게 한다.
        /// </summary>
        public float Reach => projectileRange * 0.8f;

        /// <summary>
        /// 평타를 투사체로 바꾼다. 0 이하 값은 조용히 최소값으로 올린다 —
        /// 저작 실수로 제자리에 서는 투사체를 만들지 않는다.
        /// </summary>
        public void ConfigureProjectile(Projectile prefab, float speed, float range, int pierce)
        {
            projectile = prefab;
            projectileSpeed = Mathf.Max(0.1f, speed);
            projectileRange = Mathf.Max(0.5f, range);
            projectilePierce = Mathf.Max(0, pierce);
        }

        // ── 검증 ────────────────────────────────────────

        /// <summary>
        /// 저작 실수를 로그로 알린다. <see cref="Entity.Start"/>가 한 번 부른다.
        ///
        /// 선딜이 전체 길이 이상이면 히트박스가 켜지기 전에 상태가 끝난다 —
        /// 공격이 조용히 사라지고 모션만 남는다. 예고를 길게 잡다가 밟기 쉬운 함정이다.
        /// </summary>
        public void Validate(Object context)
        {
            if (StageCount == 0)
            {
                BattleLog.Warn(LogCategory.Combat,
                    $"{name}: 평타 단계가 하나도 없다 — 이 몸은 평타를 못 친다. 칸을 하나 이상 둘 것.", context);
                return;
            }

            // 적은 선입력을 못 채워 1타에서 멈춘다. 게임은 도니까 여기서 말해 두지 않으면 모른다.
            if (HasCombo && context is Enemy)
                BattleLog.Warn(LogCategory.Combat,
                    $"{name}: 적에게 연타 단계 {StageCount}개가 들어갔다 — AI는 이어치기를 못 해 1타에서 멈춘다. " +
                    "칸을 하나만 둘 것.", context);

            for (int i = 0; i < StageCount; i++)
            {
                BasicAttackTiming t = TimingFor(i);
                if (t.windup < t.activeEnd && t.activeEnd <= t.total) continue;

                BattleLog.Warn(LogCategory.Combat,
                    $"{name}: 평타 {i + 1}타 타이밍이 어긋났다 " +
                    $"(선딜 {t.windup:0.##} / 판정끝 {t.activeEnd:0.##} / 전체 {t.total:0.##}). " +
                    "windup < activeEnd <= total 순서를 지킬 것.", context);
            }
        }
    }
}
