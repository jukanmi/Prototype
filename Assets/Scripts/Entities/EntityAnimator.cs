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

        /// <summary>평타 연타가 클립을 갈아끼우는 자리. Animator 상태 이름이다.</summary>
        private const string AttackState = "Attack";

        private Entity entity;
        private AnimatorOverrideController overrides;
        private SkillData currentSkill;

        /// <summary>
        /// <see cref="AttackState"/>에 원래 물려 있던 클립. 연타가 이 자리를 갈아끼우므로
        /// <b>오버라이드를 씌우기 전에</b> 잡아 둬야 한다 — 씌운 뒤에 읽으면 이미 바뀐 값이 나온다.
        /// 이게 오버라이드 사전의 키이자, 1타로 되돌릴 때의 원상복구 값이다.
        /// </summary>
        private AnimationClip baseAttackClip;
        private AnimationClip currentAttackClip;

        /// <summary>
        /// 지금 상태의 클립 재생 배율. 클립 길이를 <b>코드가 정한 상태 길이</b>에 맞춘다.
        ///
        /// 이게 없으면 모션과 판정이 따로 논다 — 평타 클립이 0.36초인데 선딜이 0.5초면
        /// 휘두르는 그림이 다 끝난 뒤에 히트박스가 켜지고, 남은 시간은 마지막 프레임으로 얼어 있다.
        /// 타이밍의 주인이 코드라는 원칙을 지키려면 그림 쪽을 늘리고 줄여야 한다.
        /// </summary>
        private float stateSpeed = 1f;

        private void Awake()
        {
            entity = GetComponent<Entity>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (animator == null || animator.runtimeAnimatorController == null) return;

            baseAttackClip = FindClip(animator.runtimeAnimatorController, AttackState);

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
            if (animator != null) animator.speed = TimeControl.Scale * stateSpeed;
        }

        /// <summary>
        /// 상태머신을 거치지 않고 클립을 직접 지정한다.
        ///
        /// 보스 패턴(<see cref="BossPatternAction"/>)이 쓴다. 특수 행동은 IState가 아니라
        /// 실행기가 돌리는 것이라 상태 전이가 일어나지 않고, 그래서 <see cref="Apply"/>가
        /// 불리지 않는다 — 가만히 두면 예고·발동 내내 Idle 클립이 돈다.
        ///
        /// 없는 상태 이름은 조용히 무시된다. 아트가 아직 없는 패턴도 동작해야 한다.
        /// </summary>
        public void PlayState(string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName)) return;
            Play(stateName);
        }

        /// <summary>
        /// 평타 <paramref name="stage"/>타째 모션을 튼다. 상태는 하나(<see cref="AttackState"/>)로 두고
        /// 거기 물린 <b>클립만</b> 갈아끼운다 — 컨트롤러에 상태를 늘리면
        /// 같은 컨트롤러를 공유하는 잡몹 · 보스까지 저작 부담이 번진다.
        ///
        /// 배율을 여기서 같이 맞추는 게 핵심이다. <see cref="PlayState"/>는 speed를 안 건드려서
        /// 10프레임짜리 마무리가 1타 길이로 우겨넣어진 채 배속으로 보인다.
        /// </summary>
        public void PlayBasicAttackStage(int stage, float duration)
        {
            if (animator == null) return;

            AnimationClip clip = entity != null ? entity.GetBasicStageClip(stage) : null;
            SwapAttackClip(clip);

            float length = clip != null ? clip.length : ClipLength(AttackState);
            stateSpeed = duration > 0f && length > 0f ? length / duration : 1f;

            Play(AttackState);
        }

        /// <summary>
        /// 평타 슬롯의 클립을 바꾼다. null이면 원본으로 되돌린다 —
        /// 1타이거나 아트를 아직 안 채운 단계가 그렇다.
        /// </summary>
        private void SwapAttackClip(AnimationClip clip)
        {
            if (overrides == null || baseAttackClip == null) return;
            if (currentAttackClip == clip) return;

            currentAttackClip = clip;
            overrides[baseAttackClip] = clip;
        }

        private void HandleStateChanged(IState prev, IState next) => Apply(next);

        private void Apply(IState state)
        {
            if (animator == null || state == null) return;

            // 평타는 단계마다 클립이 다르다. 상태에 들어오는 건 언제나 1타(Enter가 0으로 리셋한다) —
            // 2·3타는 상태 전이 없이 AttackState가 직접 PlayBasicAttackStage를 부른다.
            if (entity != null && ReferenceEquals(state, entity.AttackState))
            {
                PlayBasicAttackStage(entity.AttackState.Stage, entity.ActiveAttackTotal);
                return;
            }

            if (state is SkillState skill)
            {
                SwapSkillClip(skill.Data);
                stateSpeed = SpeedFor("Skill", skill.Data != null ? skill.Data.TotalDuration : 0f);
                Play("Skill");
                return;
            }

            string clip = StateToClip(state);
            stateSpeed = SpeedFor(clip, DurationOf(state));
            Play(clip);
        }

        /// <summary>
        /// 이 상태가 <b>코드상 몇 초짜리인지</b>. 0이면 길이를 맞추지 않는다(그대로 재생).
        ///
        /// 경직 · 다운처럼 지속이 피격마다 달라지는 상태는 맞추지 않는다 —
        /// 매번 재생 속도가 흔들리면 같은 동작이 다른 동작으로 보인다.
        /// </summary>
        private float DurationOf(IState state)
        {
            if (entity == null) return 0f;

            // 지상 평타는 여기 안 온다 — 단계마다 길이가 달라 Apply가 따로 처리한다.
            return ReferenceEquals(state, entity.AerialAttackState) ? entity.BasicAttackTotal : 0f;
        }

        /// <summary>
        /// 클립을 <paramref name="duration"/>초에 걸쳐 재생할 배율.
        /// 클립을 못 찾거나 길이가 없으면 1 — 아트가 아직 없는 상태도 동작해야 한다.
        /// </summary>
        private float SpeedFor(string stateName, float duration)
        {
            if (duration <= 0f) return 1f;

            float length = ClipLength(stateName);
            return length > 0f ? length / duration : 1f;
        }

        /// <summary>
        /// Animator 상태 이름으로 클립 길이를 찾는다.
        ///
        /// 클립 이름은 <c>{직업}_{상태}</c> 규칙이라(Ally_Attack) 접미사로 맞춘다.
        /// 런타임 API만 쓴다 — AnimatorController는 에디터 전용이라 빌드에서 못 읽는다.
        /// </summary>
        private float ClipLength(string stateName)
        {
            AnimationClip c = animator != null ? FindClip(animator.runtimeAnimatorController, stateName) : null;
            return c != null ? c.length : 0f;
        }

        /// <summary>상태 이름에 해당하는 클립을 컨트롤러에서 찾는다. 없으면 null.</summary>
        private static AnimationClip FindClip(RuntimeAnimatorController rac, string stateName)
        {
            if (rac == null || string.IsNullOrEmpty(stateName)) return null;

            AnimationClip[] clips = rac.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip c = clips[i];
                if (c == null) continue;

                // "Ally_AerialAttack"이 "_Attack"에 걸리지 않게 구분자를 포함해서 본다.
                if (c.name == stateName || c.name.EndsWith("_" + stateName))
                    return c;
            }

            return null;
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
