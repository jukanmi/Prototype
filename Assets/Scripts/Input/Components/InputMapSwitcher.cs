using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 전술 페이즈와 조준 여부에 맞춰 불릿타임 액션 맵 둘을 여닫는다.
    ///
    /// 두 맵은 <b>절대 동시에 켜지지 않는다.</b> 기본 바인딩에서 J는 카드 놓기(BulletTime)와
    /// 조준 확정(BulletTimeSkillShot) 양쪽에 걸려 있어서, 겹치면 한 번 누른 J가
    /// 조준을 확정하고 그 카드를 놓는 것까지 한 프레임에 해 버린다.
    ///
    /// 이벤트(<c>OnEnter</c>/<c>OnExit</c>)가 아니라 매 프레임 상태를 본다.
    /// 두 이벤트는 Freeze 진입과 Resolve 진입에 걸려 있어 실제로 조작이 열리는 구간
    /// (<see cref="BulletTimeController.AllowsCardEdit"/>)과 한 프레임씩 어긋난다.
    /// </summary>
    [RequireComponent(typeof(PlayerInputController))]
    public class InputMapSwitcher : MonoBehaviour
    {
        [Tooltip("비우면 씬에서 찾는다.")]
        [SerializeField] private BulletTimeController bulletTime;

        [Tooltip("비우면 씬에서 찾는다. 조준 중인지를 여기서 본다.")]
        [SerializeField] private TargetSelector targetSelector;

        private PlayerInputController input;

        private void Awake()
        {
            input = GetComponent<PlayerInputController>();

            if (bulletTime == null) bulletTime = FindAnyObjectByType<BulletTimeController>();
            if (targetSelector == null) targetSelector = FindAnyObjectByType<TargetSelector>();
        }

        // Awake에서 곧바로 맞추지 않는 이유 — BulletTimeController가 아직 Awake 전이면
        // tactic이 null이라 페이즈를 물어봐야 의미가 없다.
        private void Start() => Apply();

        private void Update() => Apply();

        /// <summary>
        /// 두 맵을 지금 상태에 맞춘다. Update와 테스트가 부른다.
        /// 값이 그대로면 <see cref="PlayerInputController"/> 가 알아서 넘긴다.
        /// </summary>
        public void Apply()
        {
            if (input == null) return;

            // 모달이 떠 있으면 카드 조작도 같이 닫는다. Gameplay는 컨트롤러가 직접 잠근다.
            bool cardEdit = !input.GameplaySuspended
                            && bulletTime != null && bulletTime.AllowsCardEdit;
            bool aiming = cardEdit && targetSelector != null && targetSelector.IsSelecting;

            // 두 대입 사이에는 입력 처리가 끼어들 프레임 경계가 없으므로 순서는 상관없다.
            input.BulletTimeMapEnabled = cardEdit && !aiming;
            input.SkillShotMapEnabled = aiming;
        }
    }
}
