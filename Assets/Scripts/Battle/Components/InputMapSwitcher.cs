using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 카드 조작 맵(<c>BulletTime</c>)을 총공격 편집창(<see cref="AssaultEditorUI"/>)이 열려 있거나
    /// 창이 떠 조작이 잠긴 동안만 연다. 이벤트가 아니라 매 프레임 상태를 본다.
    ///
    /// 편집창은 이 맵의 <c>Navigate</c>로 포커스를 옮기고, 다른 창(대화 선택지 · 상점)은
    /// <see cref="PlayerInputController.MenuStep"/>으로 WASD 커서를 읽는다. 걷는 동안에는 닫혀 있다 —
    /// 열려 있으면 이동키가 편집창 칸 이동으로도 먹는다.
    /// </summary>
    [RequireComponent(typeof(PlayerInputController))]
    public class InputMapSwitcher : MonoBehaviour
    {
        private PlayerInputController input;

        private void Awake() => input = GetComponent<PlayerInputController>();

        private void Start() => Apply();

        private void Update() => Apply();

        /// <summary>
        /// 맵을 지금 상태에 맞춘다. Update와 테스트가 부른다.
        /// 값이 그대로면 <see cref="PlayerInputController"/> 가 알아서 넘긴다.
        /// </summary>
        public void Apply()
        {
            if (input == null) return;

            // 창은 모달이라 Gameplay가 잠겨 있어도 이동키(Navigate) 맵은 열려야 커서를 WASD로 옮긴다.
            input.BulletTimeMapEnabled = AssaultEditorUI.IsOpen || input.GameplaySuspended;
        }
    }
}
