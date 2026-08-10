using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 상태머신을 Animator에 비춘다. <b>타이밍의 주인은 코드다</b> —
    /// Animation Event로 판정을 켜지 않는다.
    ///
    /// 이유: Animator는 Time.timeScale을 쓰는데 이 프로젝트는 그걸 안 건드린다(결정 로그 ⑥).
    /// 불릿타임에 <see cref="TimeControl.Scale"/>만 0이 되므로, Animator가 스스로 멈추지 않는다.
    /// 여기서 speed를 직접 맞춰 준다.
    /// </summary>
    [RequireComponent(typeof(Entity))]
    public class EntityAnimator : MonoBehaviour
    {
        /// <summary>컨트롤러에서 스킬 클립이 꽂히는 자리. 빌더가 같은 이름으로 만든다.</summary>
        public const string SkillSlotClip = "Skill_Placeholder";

        [SerializeField] private Animator animator;
        [Tooltip("상태 전환 시 섞는 시간(초). 0이면 즉시 끊어 바꾼다.")]
        [SerializeField] private float crossFade = 0.05f;

        private Entity entity;
        private AnimatorOverrideController overrides;
        private SkillData currentSkill;

        private void Awake()
        {
            entity = GetComponent<Entity>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (animator == null || animator.runtimeAnimatorController == null) return;

            // 스킬마다 클립을 갈아끼우려면 오버라이드 컨트롤러가 필요하다.
            overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = overrides;
        }

        private void Start()
        {
            if (entity == null || entity.StateMachine == null) return;

            entity.StateMachine.OnStateChanged += HandleStateChanged;

            // Entity.Start가 먼저 돌아 Idle 진입을 놓쳤을 수 있다. 현재 상태를 한 번 반영한다.
            Apply(entity.StateMachine.CurState);
        }

        private void OnDestroy()
        {
            if (entity != null && entity.StateMachine != null)
                entity.StateMachine.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            // 불릿타임에 같이 멈춘다. Time.timeScale은 건드리지 않는다.
            if (animator != null) animator.speed = TimeControl.Scale;
        }

        private void HandleStateChanged(IState prev, IState next) => Apply(next);

        private void Apply(IState state)
        {
            if (animator == null || state == null) return;

            if (state is SkillState skill)
            {
                SwapSkillClip(skill.Data);
                Play("Skill");
                return;
            }

            Play(StateToClip(state));
        }

        /// <summary>IdleState → "Idle". 상태 클래스 이름이 곧 Animator 상태 이름이다.</summary>
        private static string StateToClip(IState state)
        {
            string n = state.GetType().Name;
            return n.EndsWith("State") ? n.Substring(0, n.Length - "State".Length) : n;
        }

        private void Play(string stateName)
        {
            if (!HasState(stateName)) return;

            if (crossFade > 0f) animator.CrossFadeInFixedTime(stateName, crossFade, 0, 0f);
            else animator.Play(stateName, 0, 0f);
        }

        private bool HasState(string stateName)
            => animator.HasState(0, Animator.StringToHash(stateName));

        /// <summary>
        /// 스킬 클립을 Skill 슬롯에 꽂는다. <c>data.animation</c>이 비어 있으면(null)
        /// 오버라이드를 무조건 null로 덮어써 슬롯을 원래 클립(<see cref="SkillSlotClip"/>, 즉
        /// 기본 플레이스홀더)로 되돌린다 — 이전 스킬의 클립이 남아있으면 안 된다.
        /// </summary>
        private void SwapSkillClip(SkillData data)
        {
            if (overrides == null || data == null) return;
            if (currentSkill == data) return;

            currentSkill = data;
            overrides[SkillSlotClip] = data.animation;
        }
    }
}
