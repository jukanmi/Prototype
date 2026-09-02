using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적의 몸 색을 전투 상태와 <b>공격 예고</b>에 맞춰 물들인다.
    /// 스킬에 걸린 적을 난전에서 골라내고, 맞기 전에 "지금 온다"를 읽게 하기 위한 것이다.
    ///
    /// <b>Update가 없다</b> — 상태가 바뀌는 순간에만 일한다.
    /// <see cref="Enemy"/>가 스스로 붙이므로 프리팹 · 씬 배선이 없다.
    /// </summary>
    [RequireComponent(typeof(Combat))]
    public class EnemyStateTint : MonoBehaviour
    {
        [Tooltip("물들일 몸 렌더러. 비우면 스스로 찾는다. 그림자를 넣으면 안 된다.")]
        [SerializeField] private SpriteRenderer body;

        private Combat combat;
        private Entity owner;

        /// <summary>물들이기 전의 색. 적 종류를 구분하는 고유색이다.</summary>
        public Color BaseColor { get; private set; } = Color.white;

        public SpriteRenderer Body => body;

        private void Awake()
        {
            combat = GetComponent<Combat>();
            owner = GetComponent<Entity>();

            if (body == null) body = ResolveBody();
            if (body != null) BaseColor = body.color;
        }

        private void OnEnable()
        {
            if (combat != null)
            {
                combat.OnCombatStateChanged += HandleStateChanged;
                combat.OnGuardBreakChanged += HandleOverlayChanged;
                combat.OnDebuffsChanged += HandleDebuffsChanged;
            }

            if (owner != null) owner.OnTelegraphChanged += HandleOverlayChanged;
        }

        private void OnDisable()
        {
            if (combat != null)
            {
                combat.OnCombatStateChanged -= HandleStateChanged;
                combat.OnGuardBreakChanged -= HandleOverlayChanged;
                combat.OnDebuffsChanged -= HandleDebuffsChanged;
            }

            if (owner != null) owner.OnTelegraphChanged -= HandleOverlayChanged;
        }

        /// <summary>
        /// 아머는 <b>기본 상태</b>라 이벤트가 뜨지 않는다 — 보스는 첫 프레임부터 금색이어야 한다.
        /// 상태 변경만 기다리면 가드가 한 번 깨질 때까지 색이 안 붙는다.
        /// </summary>
        private void Start() => Apply(combat != null ? combat.CombatState : CombatState.Neutral);

        private void HandleStateChanged(CombatState prev, CombatState next) => Apply(next);

        /// <summary>예고 · 가드브레이크는 둘 다 겹침 표시라 같은 계산을 다시 돌리면 된다.</summary>
        private void HandleOverlayChanged(bool on)
            => Apply(combat != null ? combat.CombatState : CombatState.Neutral);

        /// <summary>디버프도 겹침 표시다. 시그니처만 달라 어댑터를 하나 더 둔다.</summary>
        private void HandleDebuffsChanged(Debuff mask)
            => Apply(combat != null ? combat.CombatState : CombatState.Neutral);

        /// <summary>
        /// 상태와 예고를 색에 반영한다.
        /// Neutral · Dead로 돌아오고 예고도 꺼지면 <see cref="CombatStateVisuals.Tint"/>가
        /// 기본 색을 그대로 돌려주므로 원복은 따로 처리하지 않는다.
        /// </summary>
        public void Apply(CombatState state)
        {
            if (body == null) return;

            body.color = CombatStateVisuals.Tint(BaseColor, state, ResolveOverlay());
        }

        /// <summary>
        /// 겹쳐 그릴 표시. 빙결 &gt; 스턴 &gt; 아머 &gt; 예고 순이다.
        ///
        /// 디버프가 아머를 이기는 이유: 얼어붙은 보스가 금색이면 "때려도 안 밀린다"는
        /// 거짓말이 된다 — 굳은 몸은 오히려 밀린다.
        /// 아머가 예고를 이기는 이유: 아머 중에는 어차피 못 끊으므로
        /// "지금 패링하면 된다"는 신호를 주면 역시 거짓말이 된다.
        ///
        /// 가드브레이크는 색을 주지 않는다(<see cref="CombatStateVisuals.GuardBreakLabel"/> 참고).
        /// </summary>
        private CombatOverlay ResolveOverlay()
        {
            if (combat != null && combat.HasDebuff(Debuff.Freeze)) return CombatOverlay.Frozen;
            if (combat != null && combat.HasDebuff(Debuff.Stun)) return CombatOverlay.Stunned;
            if (combat != null && combat.IsSuperArmored) return CombatOverlay.SuperArmor;
            if (owner != null && owner.IsTelegraphing) return CombatOverlay.Telegraph;

            return CombatOverlay.None;
        }

        /// <summary>
        /// 몸 렌더러를 찾는다.
        ///
        /// <c>GetComponentInChildren</c>은 쓰지 않는다 — 자식 순서에 따라 그림자를 집을 수 있고,
        /// 그러면 몸은 그대로인데 발밑 타원만 물든다.
        /// </summary>
        private SpriteRenderer ResolveBody()
        {
            var view = GetComponent<BeltScrollView>();
            if (view != null && view.SpriteRoot != null)
            {
                SpriteRenderer sr = view.SpriteRoot.GetComponent<SpriteRenderer>();
                if (sr != null) return sr;
            }

            // BeltScrollView가 없는 변종 프리팹은 루트에 몸이 붙어 있다.
            return GetComponent<SpriteRenderer>();
        }
    }
}
