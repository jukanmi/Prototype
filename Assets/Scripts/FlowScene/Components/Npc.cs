using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 말을 거는 사람의 대사. <see cref="Interactable"/>과 같은 오브젝트에 붙인다 —
    /// 다가가 F를 누르면 대화창(<see cref="DialogueWindow"/>)에 대사가 뜨고, 끝까지 읽으면
    /// Interactable에 꽂은 결과(상점 · 이벤트 · 제단 등)가 이어진다. 이름은 Interactable의 라벨을 쓴다.
    /// 말하는 동안 대화창 위에 이 사람의 그림(<see cref="Portrait"/>)이 선다.
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class Npc : MonoBehaviour
    {
        [Tooltip("한 칸이 대화창 한 장. 위에서부터 차례로 넘어간다. 비우면 대사 없이 바로 결과로 간다.")]
        [SerializeField, TextArea(2, 4)] private string[] lines = new string[0];

        [Tooltip("서 있을 때 바라보는 쪽. 비전투 칸은 플레이어가 왼쪽에서 들어오므로 기본은 왼쪽이다. 몸(Physics)이 있을 때만 쓴다.")]
        [SerializeField] private Vector3 facing = Vector3.left;

        [Tooltip("대화창 위에 세울 그림. 비우면 몸 스프라이트를 그대로 쓴다(몸이 움직이면 같이 움직인다).")]
        [SerializeField] private Sprite portrait;

        private SpriteRenderer body;

        public string[] Lines => lines;

        /// <summary>대화창 위 그림. 꽂은 그림이 없으면 지금 몸에 그려진 스프라이트. 둘 다 없으면 null.</summary>
        public Sprite Portrait
        {
            get
            {
                if (portrait != null) return portrait;
                if (body == null) body = FindBody();
                return body != null ? body.sprite : null;
            }
        }

        private void Start()
        {
            if (TryGetComponent(out Physics physics)) physics.Face(facing);
        }

        /// <summary>몸 스프라이트 — 그림자가 아니라 <see cref="BeltScrollView.SpriteRoot"/>의 것. 뷰가 없으면 첫 렌더러.</summary>
        private SpriteRenderer FindBody()
        {
            BeltScrollView view = GetComponentInChildren<BeltScrollView>();
            if (view != null && view.SpriteRoot != null && view.SpriteRoot.TryGetComponent(out SpriteRenderer r)) return r;

            return GetComponentInChildren<SpriteRenderer>();
        }
    }
}
