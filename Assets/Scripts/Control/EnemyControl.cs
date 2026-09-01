using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적 제어. 감지 · 타이머 · 게이트를 맡고 <b>판단은 브레인에 위임</b>한다.
    /// 종류별 행동 차이는 브레인 애셋을 갈아끼워 만든다.
    /// 스테이지 종료 시 <see cref="SetActive"/>(false)로 정지시킨다.
    /// </summary>
    public class EnemyControl : Control
    {
        [Tooltip("EnemyData.brain이 비었을 때 쓰는 폴백.")]
        [SerializeField] private EnemyBrainAsset brain;

        [Header("타이머")]
        [SerializeField] private float attackInterval = 1.5f;
        [SerializeField] private float retargetInterval = 0.5f;
        [Tooltip("특수 행동 기본 쿨. 평타 쿨과 따로 돈다. 브레인이 쿨을 실어 보내면 그쪽이 우선한다.")]
        [SerializeField] private float specialInterval = 4f;


        [Header("브레인 파라미터")]
        [SerializeField] private EnemyBrainParams parameters = new EnemyBrainParams
        {
            attackRange = 1.8f,
            leashRange = 0f,
        };

        private Entity target;
        private float attackTimer;
        private float retargetTimer;
        private bool active = true;

        /// <summary>진입 연출. <see cref="BeginSpawnEntry"/>가 채우고, 끝나면 스스로 비운다.</summary>
        private SpawnEntry entry;

        /// <summary>지금 공격권(<see cref="EnemyAttackTokens"/>)을 쥐고 있는가.</summary>
        private bool holdsToken;

        /// <summary>쥔 토큰의 임대 기간. 휘두르는 동안 이 값으로 계속 갱신한다.</summary>
        private float tokenLease = AttackTokenPool.BasicLease;

        /// <summary>
        /// 토큰을 얻고 나서 실제로 휘두르는 상태가 되기까지 봐 주는 시간.
        ///
        /// 명령을 낸 프레임에는 아직 <see cref="Entity.IsBusy"/>가 false다 —
        /// 상태 전이는 이 Tick 다음에 <c>StateMachine.Tick</c>에서 일어난다.
        /// 그 한 프레임을 안 봐 주면 얻자마자 반납해 토큰이 아무 의미가 없어진다.
        /// </summary>
        private const float TokenGrace = 0.35f;

        private float tokenGrace;

        /// <summary>특수 행동 실행기. Entity 하나에 하나만 붙는다(<see cref="IEnemySpecialAction"/>).</summary>
        private IEnemySpecialAction special;

        /// <summary>패턴별 남은 쿨. 크기는 실행기가 가진 패턴 수와 같다.</summary>
        private float[] specialTimers;

        public Entity Target => target;

        /// <summary>폴백 브레인. 프리팹 배선 검사용 읽기 전용 창구.</summary>
        public EnemyBrainAsset Brain => brain;

        /// <summary>지금 특수 행동을 실행 중인지. 디버그 HUD가 읽는다.</summary>
        public bool IsRunningSpecial => special != null && special.IsRunning;

        /// <summary>
        /// 특수 행동 실행기. 읽기 전용 창구다 —
        /// 바닥 범위 표시(<see cref="AttackRangeIndicator"/>)가 매 프레임 GetComponent를 돌지 않게.
        /// </summary>
        public IEnemySpecialAction Special => special;

        protected override void Awake()
        {
            base.Awake();

            special = GetComponent<IEnemySpecialAction>();

            // 패턴이 MaxSpecials를 넘으면 비트마스크로 준비 상태를 못 전한다.
            // 잘라 쓰면 뒤쪽 패턴이 영영 안 나오므로 조용히 넘기지 않는다.
            int count = special != null ? Mathf.Max(0, special.Count) : 0;
            if (count > EnemyBrainContext.MaxSpecials)
            {
                BattleLog.Warn(LogCategory.State,
                    $"{name}: 특수 행동이 {count}개다. {EnemyBrainContext.MaxSpecials}개까지만 쓸 수 있다.", this);
                count = EnemyBrainContext.MaxSpecials;
            }

            specialTimers = new float[count];
        }

        /// <summary>
        /// 평타 쿨을 배율로 줄인다. 격노처럼 <b>영구히</b> 빨라지는 버프가 쓴다.
        ///
        /// 되돌리는 경로는 없다 — 이 배율을 쓰는 패턴이 1회성이고, 전투가 끝나면
        /// 씬이 통째로 다시 올라오므로 원복할 자리가 없다.
        /// </summary>
        public void ScaleAttackInterval(float scale)
        {
            if (scale <= 0f || Mathf.Approximately(scale, 1f)) return;

            attackInterval *= scale;
            BattleLog.Log(LogCategory.State, $"{name} 평타 쿨 {scale:0.##}배 → {attackInterval:0.##}초", this);
        }

        /// <summary>도발 등으로 타겟을 강제 지정한다.</summary>
        public void SetTarget(Entity forced)
        {
            target = forced;
            retargetTimer = retargetInterval;
        }

        public void SetActive(bool value)
        {
            active = value;
            if (!active)
            {
                Clear();
                if (special != null) special.Cancel();
                if (Owner != null) Owner.SetTelegraph(false);

                // 정지한 적이 공격권을 물고 있으면 그 자리는 영영 안 열린다.
                ReleaseAttackToken();
                entry = null;
            }
        }

        // ── 진입 연출 ────────────────────────────────────

        /// <summary>
        /// 소환된 직후 <see cref="StageDirector"/>가 부른다. 정착 지점까지 걸어간 뒤
        /// <paramref name="holdSeconds"/>초를 서 있고, 그동안 브레인은 돌지 않는다.
        ///
        /// AI를 끄는 대신 이 컴포넌트가 <c>Command.Move</c>를 내는 이유는 걷는 그림 때문이다 —
        /// <c>Physics.Move</c>를 직접 밀면 상태머신이 Idle에 남아 서 있는 채로 미끄러진다.
        /// </summary>
        public void BeginSpawnEntry(Vector3 entryPoint, float holdSeconds)
        {
            entry = new SpawnEntry(entryPoint, holdSeconds);
        }

        /// <summary>진입 연출이 아직 도는 중인가. 디버그 표시가 읽는다.</summary>
        public bool IsEntering => entry != null && entry.IsActive;

        /// <summary>
        /// EnemyData의 행동 수치를 주입한다. Enemy.Awake가 호출한다.
        /// data가 null이면 인스펙터 값을 그대로 쓴다 — 데이터를 안 꽂은 프리팹도 동작해야 한다.
        /// </summary>
        public void ApplyData(EnemyData data)
        {
            if (data == null) return;

            if (data.brain != null) brain = data.brain;

            attackInterval = data.attackInterval;
            retargetInterval = data.retargetInterval;
            specialInterval = data.specialInterval;
            parameters.attackRange = data.attackRange;
            parameters.leashRange = data.leashRange;
            parameters.preferredMinRange = data.preferredMinRange;
            parameters.specialRange = data.specialRange;

            // 0이면 사거리 판정에 영영 들어가지 않는다. 저작 실수를 조용히 넘기지 않는다.
            // leashRange는 0이 "무제한"이라는 정상값이므로 검사하지 않는다.
            if (parameters.attackRange <= 0f)
                BattleLog.Warn(LogCategory.State,
                    $"{name}: EnemyData '{data.name}'의 attackRange가 0이다. 이 적은 공격하지 않는다.", this);
        }

        // 배선 누락을 조용히 넘기지 않는다. ApplyData는 Awake에서 끝나므로 Start에서 판단한다.
        private void Start()
        {
            if (brain == null)
                BattleLog.Warn(LogCategory.State, $"{name}: 브레인이 없다. 이 적은 움직이지 않는다.", this);
        }

        public override void Tick(float dt)
        {
            Clear();
            if (!active) return;

            UpdateAttackToken();

            // 진입 연출 중에는 브레인이 돌지 않는다. 걸어 들어가 자리를 잡는 것까지가 전부다 —
            // 쿨 타이머도 여기서는 흐르지 않아, 돌진전사는 선딜레이가 끝나는 순간이 곧 준비 완료다.
            if (TickSpawnEntry(dt)) return;

            if (brain == null) return;

            attackTimer -= dt;
            retargetTimer -= dt;

            for (int i = 0; i < specialTimers.Length; i++)
                specialTimers[i] -= dt;

            // 가드브레이크는 완전 무방비다. 경직만으로는 부족하다 —
            // 경직이 풀리는 순간 다시 휘두르면 무방비 구간이 아니게 된다.
            if (Owner != null && Owner.Combat.IsGuardBroken)
            {
                if (special != null) special.Cancel();
                Owner.SetTelegraph(false);
                ReleaseAttackToken();
                return;
            }

            // 경직·사망 중에는 특수 행동이 이어지면 안 된다. 무적 관통처럼 보인다.
            //
            // 디버프(스턴 · 빙결)도 같이 본다. 아래 IsBusy 게이트가 행동은 이미 막지만
            // 그건 return일 뿐이라 <b>공격권을 쥔 채</b> 굳는다 — 얼어붙은 적 하나가
            // 토큰을 붙들고 있으면 멀쩡한 다른 적들이 덤비지 못한다.
            if (Owner != null && (CombatStateRules.IsStunned(Owner.Combat.CombatState) ||
                                  Owner.HasDebuff(Debuff.ActionBlocking)))
            {
                if (special != null) special.Cancel();
                Owner.SetTelegraph(false);   // 맞은 순간 예고는 없던 일이 된다
                ReleaseAttackToken();
                return;
            }

            // 실행 중인 특수 행동이 우선. 새 판단을 받으면 매 프레임 다시 시작된다.
            if (special != null && special.IsRunning)
            {
                special.Tick(dt);
                return;
            }

            if (Owner != null && Owner.IsBusy) return;

            Retarget();

            EnemyBrainContext ctx = BuildContext(dt);
            UpdateAttackTelegraph(in ctx);

            EnemyIntent intent = brain.Decide(ctx);

            // 다구리 방지. 공격권을 못 얻은 적은 사거리 안에서 기다린다 —
            // 몰려 있는 그림은 그대로 두고 들어오는 타격 수만 일정하게 유지한다.
            if (NeedsAttackToken(intent.kind) && !TakeAttackToken(intent.kind))
            {
                if (Owner != null) Owner.SetTelegraph(false);
                return;   // Clear()로 이미 비어 있다. 제자리에서 기회를 노린다.
            }

            if (intent.kind == EnemyActionKind.Special)
            {
                StartSpecial(in intent, dt);
                return;
            }

            Drive(intent.command, intent.moveDirection);

            // 쿨 소모는 여기서 판단한다. 브레인이 별도 플래그를 돌려주면 항상 이 조건과 같은 값이 되어 중복이다.
            if (intent.command == Command.Attack)
                attackTimer = attackInterval;
        }

        /// <summary>
        /// 평타 예고. <b>남은 쿨이 선딜보다 짧아지면</b> 켠다.
        ///
        /// 선딜(<see cref="Entity.BasicAttackWindup"/>)만 쓰면 공격 모션과 동시에 켜져
        /// 예고가 아니라 통보가 된다. 쿨 끝자락 선딜만큼을 미리 얹어, 총 선딜 두 배 동안 번쩍인다 —
        /// 유저가 !를 보고 대시를 누를 시간이 그만큼 생긴다.
        ///
        /// 특수 행동은 자기 예고 단계(<see cref="EnemySpecialPhase.Telegraph"/>)를 따로 갖고 있어
        /// 여기까지 오지 않는다 — 위쪽에서 이미 return한다.
        /// </summary>
        private void UpdateAttackTelegraph(in EnemyBrainContext ctx)
        {
            if (Owner == null) return;

            // 이미 휘두르는 중이면 AttackState가 예고를 쥐고 있다. 여기서 건드리면 선딜 표시가 끊긴다.
            if (Owner.StateMachine != null && Owner.StateMachine.CurState == Owner.AttackState) return;

            bool inRange = ctx.target != null && ctx.distance <= ctx.p.attackRange;
            Owner.SetTelegraph(inRange && attackTimer <= Owner.BasicAttackWindup);
        }

        // ── 진입 연출 ────────────────────────────────────

        /// <summary>
        /// 진입 연출을 한 프레임 굴린다. <b>true면 이번 프레임은 여기서 끝</b>이다 —
        /// 브레인도 쿨 타이머도 돌지 않는다.
        /// </summary>
        private bool TickSpawnEntry(float dt)
        {
            if (entry == null) return false;

            if (!entry.IsActive)
            {
                entry = null;
                return false;
            }

            Vector3 dir = entry.Tick(transform.position, dt);

            if (dir.sqrMagnitude > 0.0001f)
            {
                Drive(Command.Move, dir);
            }

            return true;
        }

        // ── 동시 공격 제한 ───────────────────────────────

        /// <summary>평타와 특수 행동만 공격권을 요구한다. 이동·대기는 몇이든 자유다.</summary>
        private static bool NeedsAttackToken(EnemyActionKind kind)
            => kind == EnemyActionKind.Attack || kind == EnemyActionKind.Special;

        /// <summary>지금 몸이 실제로 뭔가를 휘두르고 있는가. 임대를 갱신할지 판단한다.</summary>
        private bool IsSwinging
            => (Owner != null && Owner.IsBusy) || (special != null && special.IsRunning);

        private bool TakeAttackToken(EnemyActionKind kind)
        {
            float lease = kind == EnemyActionKind.Special
                ? AttackTokenPool.SpecialLease
                : AttackTokenPool.BasicLease;

            if (!EnemyAttackTokens.Pool.TryAcquire(this, EnemyAttackTokens.Now, lease))
                return false;

            holdsToken = true;
            tokenLease = lease;
            tokenGrace = TokenGrace;
            return true;
        }

        /// <summary>
        /// 쥔 공격권을 놓을 때가 됐는지 본다.
        ///
        /// 휘두르는 동안에는 <b>임대를 매 프레임 갱신</b>한다. 그래서 풀의 만료는
        /// "이 적이 더 이상 Tick 하지 않는다"는 뜻만 갖는다 — 씬이 내려갔거나 파괴됐거나.
        /// 불릿타임으로 게임 시간이 멈춰도 Control.Tick 은 계속 도므로 여기서 얼지 않는다.
        /// </summary>
        private void UpdateAttackToken()
        {
            if (!holdsToken) return;

            if (IsSwinging)
            {
                EnemyAttackTokens.Pool.TryAcquire(this, EnemyAttackTokens.Now, tokenLease);
                tokenGrace = TokenGrace;
                return;
            }

            // 명령을 낸 프레임에는 아직 휘두르는 상태가 아니다. 그 한 프레임을 봐 준다.
            tokenGrace -= TimeControl.UnscaledDeltaTime;
            if (tokenGrace <= 0f) ReleaseAttackToken();
        }

        private void ReleaseAttackToken()
        {
            if (!holdsToken) return;

            holdsToken = false;
            EnemyAttackTokens.Pool.Release(this);
        }

        /// <summary>
        /// 파괴 · 비활성 경로. 죽은 적이 공격권을 물고 가면 남은 적들이 아무도 못 때린다.
        /// 임대 만료가 결국은 열어 주지만, 그 몇 초가 전투에서는 대단히 길다.
        /// </summary>
        private void OnDisable() => ReleaseAttackToken();

        /// <summary>
        /// 브레인이 지목한 패턴을 실행기에 넘긴다.
        /// 쿨은 <b>실제로 시작됐을 때만</b> 태운다 — 1회성 패턴이 거절해도 다른 패턴이 막히지 않게.
        /// </summary>
        private void StartSpecial(in EnemyIntent intent, float dt)
        {
            if (special == null) return;

            int i = intent.specialIndex;
            if (i < 0 || i >= specialTimers.Length)
            {
                BattleLog.Warn(LogCategory.State,
                    $"{name}: 브레인이 없는 특수 행동 {i}번을 지목했다. 실행기는 {specialTimers.Length}개를 갖고 있다.", this);
                return;
            }

            if (!special.TryStart(i, target)) return;

            specialTimers[i] = intent.specialCooldown > 0f ? intent.specialCooldown : specialInterval;
            special.Tick(dt);
        }

        private void Retarget()
        {
            // 활성 검사가 필요한 이유는 동료가 교대로 내려가기 때문이다. 죽은 게 아니라
            // 꺼진 것이라 IsDead로는 안 걸리고, 쿨이 돌 때까지 빈 좌표를 향해 걸어간다.
            if (retargetTimer > 0f && target != null && target.isActiveAndEnabled && !target.Combat.IsDead) return;

            retargetTimer = retargetInterval;
            target = FindNearestAlly();
        }

        private EnemyBrainContext BuildContext(float dt)
        {
            Vector3 toTarget = Vector3.zero;
            float distance = 0f;

            if (target != null)
            {
                toTarget = target.transform.position - transform.position;
                toTarget.y = 0f;                 // 거리 판정은 XZ 평면만. 높이는 무시한다.
                distance = toTarget.magnitude;
            }

            // struct 복사 — 인스펙터/EnemyData 원본값은 건드리지 않는다.
            EnemyBrainParams p = parameters;

            // 원거리 평타를 든 적은 근접 사거리 대신 투사체 사거리를 따른다.
            if (Owner != null && Owner.BasicIsRanged) p.attackRange = Owner.BasicAttackReach;

            int readyMask = SpecialReadyMask();

            return new EnemyBrainContext
            {
                self = Owner,
                target = target,
                toTarget = toTarget,
                distance = distance,
                attackReady = attackTimer <= 0f,
                specialReady = (readyMask & 1) != 0,
                specialReadyMask = readyMask,
                healthRatio = HealthRatio(),
                p = p,
                dt = dt,
            };
        }

        /// <summary>쿨이 끝난 패턴의 비트를 세운다. 실행기가 없으면 0 — 아무 특수도 못 쓴다.</summary>
        private int SpecialReadyMask()
        {
            if (special == null) return 0;

            int mask = 0;
            for (int i = 0; i < specialTimers.Length; i++)
                if (specialTimers[i] <= 0f) mask |= 1 << i;

            return mask;
        }

        /// <summary>
        /// 남은 체력 비율. 체력이 아직 안 잡힌 순간에는 1(만피)로 본다 —
        /// 0으로 떨어지면 첫 프레임에 빈사 전용 패턴이 터진다.
        /// </summary>
        private float HealthRatio()
        {
            if (Owner == null || Owner.Combat == null) return 1f;

            Energy hp = Owner.Combat.Health;
            return hp == null || hp.MaxValue <= 0f ? 1f : hp.Ratio;
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
