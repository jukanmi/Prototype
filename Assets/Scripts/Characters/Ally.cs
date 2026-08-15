using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 동료 = 스킬의 주체. 실시간에는 손패 카드를, 불릿타임에는 콤보 큐를 받아 시전한다.
    /// 실제 움직임과 타격은 전부 <see cref="SkillState"/>가 수행한다.
    /// </summary>
    public class Ally : Entity
    {
        public const int EquipSlots = 4;

        [Header("정체성")]
        [SerializeField] private Role role;

        [Tooltip("컷인에 뜨는 얼굴. 비우면 직업 색 박스로 대체된다.")]
        [SerializeField] private Sprite portrait;

        [Tooltip("직업당 6종 중 4장. 덱 16장의 1/4을 이룬다.")]
        [SerializeField] private List<ComboCard> equipped = new List<ComboCard>(EquipSlots);

        [Tooltip("실시간 스킬이 끝나지 않을 때 강제로 Idle로 되돌리는 상한. " +
                 "ComboExecutor의 slotTimeout과 같은 역할.")]
        [SerializeField] private float realtimeSkillTimeout = 5f;

        /// <summary>실시간으로 밀어넣은 스킬의 뒷정리 코루틴. 한 번에 하나만 돈다.</summary>
        private Coroutine releaseRoutine;

        /// <summary>
        /// 자율 전투 BT. 지금 이 몸을 몰고 있는지와 무관하게 붙어 있다 —
        /// 조준 기준(<see cref="PreferredTarget"/>)을 여기서 얻기 때문이다.
        /// </summary>
        private AllyControl allyControl;

        public Role Role => role;
        public Sprite Portrait => portrait;
        public IReadOnlyList<ComboCard> Equipped => equipped;
        public AllyControl AllyControl => allyControl != null ? allyControl : allyControl = GetComponent<AllyControl>();

        protected override void Awake()
        {
            base.Awake();
            allyControl = GetComponent<AllyControl>();
        }

        /// <summary>
        /// 등록이 <c>Start</c>가 아니라 여기인 이유는 <see cref="TagSwapController"/>가
        /// 벤치에 앉은 몸을 <c>SetActive(false)</c>로 내리기 때문이다. Start에 두면
        /// 꺼진 채 시작한 동료는 영영 등록되지 않는다.
        /// </summary>
        private void OnEnable()
        {
            BattleRegistry.RegisterAlly(this);
        }

        /// <summary>
        /// 내려가기 전 뒷정리. 등록을 빼지 않으면 적 AI가
        /// <see cref="BattleRegistry.Allies"/>를 보고 화면에 없는 몸의 좌표로 걸어간다.
        /// 나머지는 <see cref="Entity.ReleaseBody"/>가 한다.
        /// </summary>
        private void OnDisable()
        {
            BattleRegistry.Unregister(this);

            releaseRoutine = null;   // SetActive(false)가 이미 죽였다. 참조만 끊는다.
            ReleaseBody();
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
        /// 실시간 경로(U키 카드)는 Executor를 거치지 않으므로 되돌릴 주체가 없다.
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

            // 차징은 게이지가 찰 때까지 제자리에 서 있는 게 정상 동작이다. 모으는 시간까지
            // 같은 예산에 넣으면 maxChargeTime이 긴 스킬이 터지기도 전에 잘린다.
            float timeout = realtimeSkillTimeout;
            if (state is IChargeState && skillState != null && skillState.Data != null)
                timeout += skillState.Data.maxChargeTime;

            float elapsed = 0f;

            while (elapsed < timeout)
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

            if (elapsed >= timeout)
                BattleLog.Warn(LogCategory.Combo,
                    $"{BattleLog.Name(this)} 실시간 스킬 타임아웃 {timeout:0.#}s — 강제로 Idle", this);

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
        /// 손패 카드가 조준 없이 발동할 때 쓴다.
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

    }
}
