using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 동료 = 스킬의 주체.
    /// 라이브 페이즈에는 고유기를, 불릿타임에는 장착한 콤보 카드를 쓴다.
    /// 실제 움직임과 타격은 전부 <see cref="SkillState"/>가 수행한다.
    /// </summary>
    public class Ally : Entity
    {
        public const int EquipSlots = 4;

        [Header("정체성")]
        [SerializeField] private Role role;

        [Tooltip("라이브 페이즈 고유기. 단축키로 직접 발동한다.")]
        [SerializeField] private SkillData selfSkill;

        [Tooltip("직업당 6종 중 4장. 덱 16장의 1/4을 이룬다.")]
        [SerializeField] private List<ComboCard> equipped = new List<ComboCard>(EquipSlots);

        [Tooltip("실시간 스킬이 끝나지 않을 때 강제로 Idle로 되돌리는 상한. " +
                 "ComboExecutor의 slotTimeout과 같은 역할.")]
        [SerializeField] private float realtimeSkillTimeout = 5f;

        private AllyControl allyControl;
        private float selfSkillCooldown;

        /// <summary>실시간으로 밀어넣은 스킬의 뒷정리 코루틴. 한 번에 하나만 돈다.</summary>
        private Coroutine releaseRoutine;

        public Role Role => role;
        public SkillData SelfSkill => selfSkill;
        public IReadOnlyList<ComboCard> Equipped => equipped;
        public AllyControl AllyControl => allyControl;

        /// <summary>Executor가 상태머신을 강탈 중인지.</summary>
        public bool IsCommanded
        {
            get => allyControl != null && allyControl.IsCommanded;
            set { if (allyControl != null) allyControl.IsCommanded = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            allyControl = Control as AllyControl;
        }

        protected override void Start()
        {
            base.Start();
            BattleRegistry.RegisterAlly(this);
        }

        protected override void Update()
        {
            base.Update();

            if (selfSkillCooldown > 0f)
                selfSkillCooldown -= TimeControl.DeltaTime;
        }

        // ── 덱 구성 (포스트 배틀 전용) ────────────────────

        public bool EquipSkill(ComboCard card)
        {
            if (card == null || equipped.Count >= EquipSlots) return false;
            if (card.Data != null && card.Data.role != role) return false;

            equipped.Add(card);
            return true;
        }

        public bool RemoveSkill(ComboCard card)
        {
            return equipped.Remove(card);
        }

        // ── 라이브 페이즈 고유기 ──────────────────────────

        public bool CanCastSelfSkill => selfSkill != null && selfSkillCooldown <= 0f && !Combat.IsDead && !IsBusy;

        /// <summary>ASDF 고유기. AI가 조준값을 채운다 — 유저 지정 없음.</summary>
        public bool CastSelfSkill()
        {
            if (!CanCastSelfSkill) return false;

            // 대상은 비워 둔다. AutoTarget이 박아 준 좌표에서 SkillState가 직접 뽑는다.
            var ctx = new SkillContext
            {
                data = selfSkill,
                caster = this,
                targetInfo = AutoTarget(selfSkill),
                isBulletTime = false,
                comboIndex = -1,
            };

            BattleLog.Log(LogCategory.Skill,
                $"{name} 고유기: {selfSkill.skillName} (쿨 {selfSkill.cooldown:0.#}s)", this);

            IState state = selfSkill.CreateState(in ctx);
            StateMachine.ForceChangeState(state);
            BeginRelease(state);

            selfSkillCooldown = selfSkill.cooldown;
            return true;
        }

        // ── 콤보 카드 (실시간 U키 단발) ──────────────────

        /// <summary>실시간 단발 카드를 받을 수 있는 상태인지. 카드 쿨타임은 덱이 관리하므로 보지 않는다.</summary>
        public bool CanCastCard => !Combat.IsDead && !IsBusy && !IsCommanded;

        /// <summary>
        /// 손패 카드 한 장을 즉시 발동한다.
        /// <see cref="ComboExecutor"/>를 거치지 않고 상태머신에 바로 밀어넣는다 —
        /// 실시간 사용은 콤보 큐가 아니라 즉발이기 때문.
        /// </summary>
        public bool CastCard(SkillData data, in TargetInfo info)
        {
            if (data == null || !CanCastCard) return false;

            // 대상은 비워 둔다. 찍은 좌표가 있으면 그쪽이 이겨야 하므로
            // SkillState가 좌표에서 직접 뽑게 맡긴다.
            var ctx = new SkillContext
            {
                data = data,
                caster = this,
                targetInfo = info,
                isBulletTime = false,
                comboIndex = -1,
            };

            IState state = data.CreateState(in ctx);
            StateMachine.ForceChangeState(state);
            BeginRelease(state);

            return true;
        }

        // ── 실시간 스킬 뒷정리 ────────────────────────────

        /// <summary>
        /// <see cref="SkillState"/>는 후딜이 끝나도 <c>finished</c> 표시만 하고 <b>스스로 상태를 빠져나오지 않는다</b>.
        /// 콤보 경로는 <see cref="ComboExecutor"/>가 끝난 뒤 Idle로 되돌려 주지만,
        /// 실시간 경로(U키 카드 · ASDF 고유기)는 Executor를 거치지 않으므로 되돌릴 주체가 없다.
        /// 그대로 두면 슈퍼아머(<c>CanBeInterrupted == false</c>)인 채로 남아
        /// 이 동료가 영영 <see cref="Entity.IsBusy"/>가 되고, 이후 카드가 전부 "이전 동작 중"으로 거부된다.
        /// </summary>
        private void BeginRelease(IState state)
        {
            if (releaseRoutine != null) StopCoroutine(releaseRoutine);
            releaseRoutine = StartCoroutine(ReleaseWhenFinished(state));
        }

        private IEnumerator ReleaseWhenFinished(IState state)
        {
            var skillState = state as SkillState;
            float elapsed = 0f;

            while (elapsed < realtimeSkillTimeout)
            {
                if (Combat.IsDead) break;
                // 사망 등으로 관통당했거나 다른 상태로 넘어갔으면 이 코루틴이 할 일은 없다.
                if (StateMachine.CurState != state) break;
                // Executor가 지휘를 가져갔으면 되돌리는 것도 그쪽 몫이다.
                if (IsCommanded) break;
                if (skillState != null && skillState.IsFinished) break;

                // 정지 중에는 스킬도 함께 멈춘다(Entity.Update가 dt<=0이면 Tick을 건너뛴다).
                // 같은 시계를 써야 불릿타임 동안 타임아웃이 헛돌지 않는다.
                elapsed += TimeControl.DeltaTime;
                yield return null;
            }

            if (elapsed >= realtimeSkillTimeout)
                BattleLog.Warn(LogCategory.Combo,
                    $"{BattleLog.Name(this)} 실시간 스킬 타임아웃 {realtimeSkillTimeout:0.#}s — 강제로 Idle", this);

            if (StateMachine.CurState == state && !Combat.IsDead && !IsCommanded)
                StateMachine.ForceChangeState(IdleState);

            releaseRoutine = null;
        }

        /// <summary>
        /// 이 동료가 겨눌 적. 지휘 대상이 잡혀 있으면 그쪽을, 없으면 최근접 적을 쓴다.
        /// 자동 조준과 실시간 카드가 같은 기준을 쓰도록 한 곳에 모아 둔다.
        /// </summary>
        public Entity PreferredTarget
            => allyControl != null && allyControl.Target != null && !allyControl.Target.Combat.IsDead
                ? allyControl.Target
                : BattleRegistry.NearestEnemy(transform.position);

        /// <summary>
        /// 유저 조준이 없을 때 쓰는 자동 조준. 대상의 <b>좌표</b>만 뽑아 담는다 —
        /// 대상 자체는 시전 순간 <see cref="SkillState.ResolveTarget"/>이 다시 고른다.
        /// </summary>
        public TargetInfo AutoTarget(SkillData data)
        {
            if (data == null) return TargetInfo.None;

            Entity target = PreferredTarget;

            switch (data.targeting)
            {
                case TargetingType.GroundPoint:
                    return TargetInfo.Ground(target != null ? target.Physics.GroundPosition : Physics.GroundPosition);
                case TargetingType.Direction:
                    return TargetInfo.Dir(target != null
                        ? target.Physics.GroundPosition - Physics.GroundPosition
                        : Physics.Facing);
                default:
                    return TargetInfo.None;
            }
        }

        private void OnDestroy()
        {
            BattleRegistry.Unregister(this);
        }
    }
}
