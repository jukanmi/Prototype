using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 전술 페이즈에 맞춰 불릿타임 액션 맵을 여닫는다.
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

        private PlayerInputController input;

        private void Awake()
        {
            input = GetComponent<PlayerInputController>();

            if (bulletTime == null) bulletTime = FindAnyObjectByType<BulletTimeController>();
        }

        // Awake에서 곧바로 맞추지 않는 이유 — BulletTimeController가 아직 Awake 전이면
        // tactic이 null이라 페이즈를 물어봐야 의미가 없다.
        private void Start() => Apply();

        private void Update() => Apply();

        /// <summary>
        /// 맵을 지금 상태에 맞춘다. Update와 테스트가 부른다.
        /// 값이 그대로면 <see cref="PlayerInputController"/> 가 알아서 넘긴다.
        /// </summary>
        public void Apply()
        {
            if (input == null) return;

            // 모달이 떠 있으면 카드 조작도 같이 닫는다. Gameplay는 컨트롤러가 직접 잠근다.
            bool cardEdit = !input.GameplaySuspended
                            && bulletTime != null && bulletTime.AllowsCardEdit;
            input.BulletTimeMapEnabled = cardEdit;
        }
    }
}
