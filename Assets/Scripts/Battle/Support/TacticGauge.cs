namespace Prototype
{
    /// <summary>
    /// 불릿타임의 <b>자원</b> — 게이지 · 쿨타임 · 마나 코스트와, 게이지를 채워 주는 보상 규칙.
    /// <see cref="BulletTimeController"/>가 소유한다. 진입을 <b>허락할지</b>의 자원 쪽 절반이 여기 있고,
    /// 나머지 절반(이미 진입 중인가 · 콤보가 도는 중인가)은 컨트롤러가 본다.
    ///
    /// 값은 전부 컨트롤러의 인스펙터에서 온다 — 프리팹에 저장된 튜닝값을 옮기지 않으려고
    /// 필드는 그쪽에 두고 생성자로 넘겨받는다.
    /// </summary>
    public sealed class TacticGauge
    {
        public readonly struct Config
        {
            public readonly float Max;
            public readonly float Regen;
            public readonly float RequiredRatio;
            public readonly float ParryReward;
            public readonly float BasicHitReward;
            public readonly float ManaCost;
            public readonly float Cooldown;

            public Config(float max, float regen, float requiredRatio, float parryReward,
                          float basicHitReward, float manaCost, float cooldown)
            {
                Max = max;
                Regen = regen;
                RequiredRatio = requiredRatio;
                ParryReward = parryReward;
                BasicHitReward = basicHitReward;
                ManaCost = manaCost;
                Cooldown = cooldown;
            }
        }

        private readonly Config cfg;
        private readonly UnityEngine.Object logContext;
        private float cooldownTimer;

        /// <summary>평타 적중 보상을 이미 받은 평타 번호. 한 대가 여럿을 맞혀도 한 번만 주려고 든다.</summary>
        private readonly BasicHitLedger basicHitLedger = new BasicHitLedger();

        public TacticGauge(in Config config, UnityEngine.Object logContext = null)
        {
            cfg = config;
            this.logContext = logContext;
            Gauge = new Energy(EnergyType.BulletTimeGauge, config.Max);
        }

        public Energy Gauge { get; }

        /// <summary>진입에 필요한 게이지 비율. 막대 위 임계 눈금이 여기 선다.</summary>
        public float RequiredRatio => cfg.RequiredRatio;

        public float ManaCost => cfg.ManaCost;

        /// <summary>남은 쿨타임(초). 0이면 쿨타임 아님.</summary>
        public float CooldownRemaining => UnityEngine.Mathf.Max(0f, cooldownTimer);

        /// <summary>게이지만 놓고 봤을 때 진입선을 넘었는지. 쿨타임 · 마나는 안 본다.</summary>
        public bool IsReady => Gauge.Ratio >= cfg.RequiredRatio;

        /// <summary>
        /// 실제 시간으로 흐른다. 시간이 멈춰 있는 동안(<paramref name="frozen"/>)은 충전하지 않는다 —
        /// 정지 중 자가 충전을 막는다.
        /// </summary>
        public void Tick(float unscaledDt, bool frozen)
        {
            if (cooldownTimer > 0f) cooldownTimer -= unscaledDt;
            if (!frozen) Gauge.Recover(cfg.Regen * unscaledDt);
        }

        /// <summary>자원이 모자라 진입할 수 없는 이유. 진입할 수 있으면 null.</summary>
        public string Shortfall(Player payer)
        {
            if (cooldownTimer > 0f) return $"쿨타임 {cooldownTimer:0.##}s 남음";
            if (!IsReady) return $"게이지 {Gauge.Ratio * 100f:0}% / 필요 {cfg.RequiredRatio * 100f:0}%";

            if (cfg.ManaCost > 0f && payer != null)
            {
                Energy mana = payer.Energies.GetEnergy(EnergyType.Mana);
                if (mana == null || mana.CurValue < cfg.ManaCost) return "마나 부족";
            }

            return null;
        }

        /// <summary>진입 코스트 지불. Freeze 진입 시 1회.</summary>
        public void PayEntry(Player payer)
        {
            if (cfg.ManaCost > 0f && payer != null)
                payer.Energies.GetEnergy(EnergyType.Mana)?.Lose(cfg.ManaCost);
        }

        /// <summary>실행에 들어가며 게이지를 비우고 쿨을 돌린다.</summary>
        public void Spend()
        {
            Gauge.Lose(Gauge.MaxValue);
            cooldownTimer = cfg.Cooldown;
        }

        // ── 보상 ─────────────────────────────────────────

        /// <summary>
        /// 이 몸의 행동이 불릿타임 게이지를 채우는가. 아군 진영만 — 적의 패링 · 평타는 보상이 아니다.
        /// </summary>
        public static bool EarnsGauge(Entity actor) => actor != null && actor.Faction == Faction.Ally;

        /// <summary>
        /// 평타 적중이 보상감인가. 때린 쪽이 아군이고 <b>맞은 쪽이 적</b>이어야 한다.
        /// 히트박스가 이미 같은 진영을 거르지만, 진영을 모르는 몸(허수아비 · 파괴물)까지 치지 않게 한 번 더 본다.
        /// </summary>
        public static bool EarnsBasicHitGauge(Entity attacker, Entity victim)
            => EarnsGauge(attacker) && victim != null && victim.Faction == Faction.Enemy;

        /// <summary>
        /// 대시 패링 보상. 막은 쪽이 아군일 때만 준다 —
        /// 적이 패링했는데 플레이어 게이지가 차면 안 된다.
        /// </summary>
        public void RewardParry(Combat defender)
        {
            if (defender == null || !EarnsGauge(defender.Owner)) return;

            Gauge.Recover(cfg.ParryReward);

            BattleLog.Log(LogCategory.Combo,
                $"{BattleLog.Name(defender.Owner)} 패링 보상 — 게이지 +{cfg.ParryReward:0.#} " +
                $"({Gauge.Ratio * 100f:0}%)", logContext);
        }

        /// <summary>
        /// 평타 적중 보상. 한 대당 한 번 — 같은 번호로 두 번째 적을 맞힌 건 장부가 거른다.
        /// 로그는 남기지 않는다. 연타마다 한 줄씩 쌓여 전투 로그가 묻힌다.
        ///
        /// 보상받을 적중으로 인정됐으면 true. 게이지 보상이 0이어도 인정은 한다 —
        /// 평타 카드 추첨(<see cref="BasicAttackDrawRules"/>)이 같은 기준을 쓴다.
        /// </summary>
        public bool RewardBasicHit(Combat attacker, Combat victim, int swing)
        {
            if (attacker == null || victim == null) return false;
            if (!EarnsBasicHitGauge(attacker.Owner, victim.Owner)) return false;
            if (!basicHitLedger.TryClaim(attacker, swing)) return false;

            if (cfg.BasicHitReward > 0f) Gauge.Recover(cfg.BasicHitReward);
            return true;
        }
    }
}
