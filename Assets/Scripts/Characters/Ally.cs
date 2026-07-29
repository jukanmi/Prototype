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

        private AllyControl allyControl;
        private float selfSkillCooldown;

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

            Entity target = allyControl != null && allyControl.Target != null
                ? allyControl.Target
                : BattleRegistry.NearestEnemy(transform.position);

            var ctx = new SkillContext
            {
                data = selfSkill,
                caster = this,
                target = target,
                targetInfo = AutoTarget(selfSkill, target),
                isBulletTime = false,
                comboIndex = -1,
            };

            BattleLog.Log(LogCategory.Skill,
                $"{name} 고유기: {selfSkill.skillName} (쿨 {selfSkill.cooldown:0.#}s) | 대상 {BattleLog.Name(target)}", this);

            IState state = selfSkill.CreateState(in ctx);
            StateMachine.ForceChangeState(state);
            selfSkillCooldown = selfSkill.cooldown;
            return true;
        }

        /// <summary>라이브 페이즈에서는 유저가 조준하지 않으므로 AI가 대신 채운다.</summary>
        private TargetInfo AutoTarget(SkillData data, Entity target)
        {
            switch (data.targeting)
            {
                case TargetingType.GroundPoint:
                    return TargetInfo.Ground(target != null ? target.Physics.GroundPosition : Physics.GroundPosition);
                case TargetingType.EnemyUnit:
                    return TargetInfo.Unit(target);
                case TargetingType.Direction:
                    return TargetInfo.Dir(target != null
                        ? target.transform.position - transform.position
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
