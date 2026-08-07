using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적의 몸 색을 전투 상태에 맞춰 물들인다. 스킬에 걸린 적을 난전에서 골라내기 위한 것.
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

        /// <summary>물들이기 전의 색. 적 종류를 구분하는 고유색이다.</summary>
        public Color BaseColor { get; private set; } = Color.white;

        public SpriteRenderer Body => body;

        private void Awake()
        {
            combat = GetComponent<Combat>();

            if (body == null) body = ResolveBody();
            if (body != null) BaseColor = body.color;
        }

        private void OnEnable()
        {
            if (combat != null) combat.OnCombatStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (combat != null) combat.OnCombatStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(CombatState prev, CombatState next) => Apply(next);

        /// <summary>
        /// 상태를 색에 반영한다.
        /// Neutral · Dead로 돌아오면 <see cref="CombatStateVisuals.Tint"/>가
        /// 기본 색을 그대로 돌려주므로 원복은 따로 처리하지 않는다.
        /// </summary>
        public void Apply(CombatState state)
        {
            if (body == null) return;
            body.color = CombatStateVisuals.Tint(BaseColor, state);
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
