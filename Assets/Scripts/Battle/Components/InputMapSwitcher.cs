using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 카드 조작 맵(<c>BulletTime</c>)을 총공격 편집창(<see cref="AssaultEditorUI"/>)이 열려 있는 동안만 연다.
    /// 편집창은 이 맵의 <c>Navigate</c>로 포커스를 옮긴다. 이벤트가 아니라 매 프레임 상태를 본다.
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

            // 편집창은 모달이라 Gameplay가 잠겨 있어도 이동키(Navigate) 맵은 열려야 한다.
            input.BulletTimeMapEnabled = AssaultEditorUI.IsOpen;
        }
    }
}
