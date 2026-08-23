using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 2.5D 표현 규칙. 논리 좌표는 3D(X 좌우 / Z 깊이 / Y 높이)지만
    /// 스프라이트는 <b>(X, Z + Y)</b> 위치에 그린다.
    /// 가시성 확보를 위해 그림자는 항상 바닥 (X, Z)에 고정한다.
    ///
    /// 깊이 배율은 <c>depthRoot</c>(Sprite의 부모)가 받는다 — Sprite의 localScale은 애니 클립 소유다.
    /// </summary>
    [ExecuteAlways]
    public class BeltScrollView : MonoBehaviour
    {
        [Tooltip("스프라이트를 담은 자식. 루트는 논리 좌표를 유지한다.")]
        [SerializeField] private Transform sprite;
        [Tooltip("깊이 배율만 담당하는 노드. Sprite의 부모다. 비우면 깊이 배율을 적용하지 않는다.")]
        [SerializeField] private Transform depthRoot;
        [Tooltip("바닥에 고정되는 그림자.")]
        [SerializeField] private Transform shadow;
        [Tooltip("깊이(Z)가 화면 세로로 환산되는 비율. 벨트의 기울기. tan θ에 해당한다.")]
        [SerializeField] private float depthToScreen = 0.5f;
        [Tooltip("깊이(Z)가 화면 가로로 밀리는 비율. 바닥이 평행사변형으로 기운다. 0이면 정면 투영.")]
        [SerializeField] private float depthToScreenX = 0.45f;
        [Tooltip("깊이(Z) 1당 줄어드는 표시 배율. 0이면 크기가 일정하다. 모든 인스턴스가 같은 값이어야 한다.")]
        [SerializeField] private float depthScalePerUnit = 0.06f;
        [Tooltip("발이 바닥에 닿아 보이도록 스프라이트를 위로 올리는 양. 보통 스프라이트 높이의 절반. 피벗이 발밑이면 0.")]
        [SerializeField] private float spriteOffsetY = 0.5f;
        [Tooltip("바라보는 쪽으로 스프라이트를 좌우 반전한다. 옆에서 본 시트에 필요하다.")]
        [SerializeField] private bool flipToFacing = false;
        [Tooltip("반전시킬 렌더러. 비우면 sprite에서 찾는다.")]
        [SerializeField] private SpriteRenderer facingRenderer;
        [Tooltip("좌우 반전을 localScale.x 부호로 거는 노드. 몸이 렌더러 여러 장으로 쪼개진 " +
                 "캐릭터(스켈레탈 리그)는 flipX로 못 뒤집는다. 비우면 facingRenderer.flipX를 쓴다.")]
        [SerializeField] private Transform facingRoot;
        [Tooltip("그림자 기본 배율. 바닥에 눕혀 보이도록 Y를 납작하게 준다.")]
        [SerializeField] private Vector3 shadowBaseScale = new Vector3(0.9f, 0.35f, 1f);
        [Tooltip("높이에 따라 그림자를 줄여 체공감을 준다.")]
        [SerializeField] private float shadowShrinkPerUnit = 0.12f;
        [SerializeField] private SpriteRenderer[] sortedRenderers;
        [Tooltip("Z가 클수록(멀수록) 뒤에 그린다.")]
        [SerializeField] private float sortingPrecision = 100f;

        /// <summary>
        /// 몸 스프라이트가 붙은 자식. 색을 바꾸려는 쪽(<see cref="EnemyStateTint"/>)이
        /// 그림자를 잘못 집지 않도록 정확히 이 하나만 열어 준다.
        /// </summary>
        public Transform SpriteRoot => sprite;

        private Physics physics;

        private void Awake()
        {
            physics = GetComponentInParent<Physics>();
        }

        private void LateUpdate() => Sync();

        /// <summary>
        /// 한 프레임 분의 표현 갱신. 에디트 모드 테스트가 프레임을 기다리지 않고 직접 부른다.
        /// </summary>
        public void Sync()
        {
            if (physics == null)
            {
                physics = GetComponentInParent<Physics>();
                if (physics == null) return;
            }

            // 조준점 등 다른 시스템도 같은 비율로 투영해야 한다. 한 벌만 유지한다.
            BeltScroll.DepthToScreen = depthToScreen;
            BeltScroll.DepthToScreenX = depthToScreenX;
            BeltScroll.DepthScalePerUnit = depthScalePerUnit;

            Vector3 ground = physics.GroundPosition;
            float height = Mathf.Max(0f, physics.Height);

            // depthRoot가 없는 프리팹은 예전 동작 그대로 둔다 — 배율 1.
            float scale = depthRoot != null ? BeltScroll.ScaleAt(ground.z) : 1f;

            if (depthRoot != null)
            {
                // 배율은 여기서만 건다. Sprite의 localScale은 Animator 것이라
                // 거기 쓰면 스쿼시 · 스트레치가 매 프레임 지워진다.
                depthRoot.localScale = new Vector3(scale, scale, 1f);

                // 오프셋에도 배율을 곱해야 뒤쪽 캐릭터의 발이 그림자 위에 붙는다.
                depthRoot.position = BeltScroll.ToView(ground, height + spriteOffsetY * scale);

                // 루트는 Facing 방향으로 돌아간다(Physics.Apply). 자식까지 돌면 옆면이 보이므로
                // 월드 회전을 매 프레임 되돌린다 — 빌보드.
                depthRoot.rotation = Quaternion.identity;
            }

            if (sprite != null)
            {
                if (depthRoot == null)
                {
                    sprite.position = BeltScroll.ToView(ground, height + spriteOffsetY);
                    sprite.rotation = Quaternion.identity;
                }

                // 빌보드로 회전을 지웠으니 방향은 좌우 반전으로만 표현된다.
                // 시트는 오른쪽을 보고 그려져 있다.
                if (flipToFacing && Mathf.Abs(physics.Facing.x) > 0.0001f)
                    ApplyFacing(physics.Facing.x < 0f);
            }

            if (shadow != null)
            {
                shadow.position = BeltScroll.ToView(ground);
                shadow.rotation = Quaternion.identity;

                float shrink = Mathf.Clamp(1f - height * shadowShrinkPerUnit, 0.3f, 1f);
                shadow.localScale = shadowBaseScale * (shrink * scale);
            }

            ApplySorting(ground.z);
        }

        /// <summary>
        /// 왼쪽을 보게 뒤집는다.
        ///
        /// <paramref name="left"/>가 참이면 좌향이다. 렌더러 한 장짜리는 flipX로 끝나지만,
        /// 몸이 파츠 여러 장으로 쪼개진 캐릭터는 장마다 flipX를 걸면 <b>각자 제자리에서</b>
        /// 뒤집혀 리그가 분해된다 — 그쪽은 부모 노드의 scale.x 부호로 통째로 거울을 놔야 한다.
        /// </summary>
        private void ApplyFacing(bool left)
        {
            if (facingRoot != null)
            {
                Vector3 s = facingRoot.localScale;
                float want = left ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);

                // 매 프레임 대입하면 프리팹 인스턴스가 계속 더티가 된다.
                if (!Mathf.Approximately(s.x, want))
                {
                    s.x = want;
                    facingRoot.localScale = s;
                }

                return;
            }

            if (facingRenderer == null && sprite != null) facingRenderer = sprite.GetComponent<SpriteRenderer>();
            if (facingRenderer != null) facingRenderer.flipX = left;
        }

        private void ApplySorting(float z)
        {
            if (sortedRenderers == null) return;

            int order = Mathf.RoundToInt(-z * sortingPrecision);
            for (int i = 0; i < sortedRenderers.Length; i++)
            {
                if (sortedRenderers[i] == null) continue;
                sortedRenderers[i].sortingOrder = order + i;
            }
        }
    }
}
