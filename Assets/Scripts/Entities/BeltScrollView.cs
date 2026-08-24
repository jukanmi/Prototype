using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 2.5D 표현 규칙. 논리 좌표는 3D(X 좌우 / Z 깊이 / Y 높이)이고 <b>그게 곧 월드 좌표</b>다.
    /// 깊이를 화면에 보여 주는 일은 기울어진 카메라가 한다(<see cref="BeltScroll"/>).
    ///
    /// 그래서 여기가 하는 일은 세 가지뿐이다.
    /// <list type="number">
    /// <item>스프라이트를 카메라 쪽으로 돌린다(빌보드) — 안 그러면 기울어진 카메라에 눕혀 보인다.</item>
    /// <item>발이 바닥에 닿아 보이도록 <b>화면 위</b> 방향으로 올린다.</item>
    /// <item>그림자를 바닥 평면에 눕힌다.</item>
    /// </list>
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
        [Tooltip("깊이(Z) 1당 줄어드는 표시 배율. 0이면 크기가 일정하다. 모든 인스턴스가 같은 값이어야 한다.")]
        [SerializeField] private float depthScalePerUnit = 0.06f;
        [Tooltip("발이 바닥에 닿아 보이도록 스프라이트를 화면 위로 올리는 양. 보통 스프라이트 높이의 절반. 피벗이 발밑이면 0.")]
        [SerializeField] private float spriteOffsetY = 0.5f;
        [Tooltip("바라보는 쪽으로 스프라이트를 좌우 반전한다. 옆에서 본 시트에 필요하다.")]
        [SerializeField] private bool flipToFacing = false;
        [Tooltip("반전시킬 렌더러. 비우면 sprite에서 찾는다.")]
        [SerializeField] private SpriteRenderer facingRenderer;
        [Tooltip("그림자 기본 배율. 바닥 평면에 눕히므로 XY 모두 실제 지름이다 — 납작하게 보이는 건 카메라가 만든다.")]
        [SerializeField] private Vector3 shadowBaseScale = new Vector3(0.9f, 0.9f, 1f);
        [Tooltip("높이에 따라 그림자를 줄여 체공감을 준다.")]
        [SerializeField] private float shadowShrinkPerUnit = 0.12f;
        [Tooltip("그림자를 바닥에서 살짝 띄우는 양. 바닥 판과 같은 평면에 두면 깜빡인다.")]
        [SerializeField] private float shadowLift = 0.01f;
        [SerializeField] private SpriteRenderer[] sortedRenderers;
        [Tooltip("Z가 클수록(멀수록) 뒤에 그린다.")]
        [SerializeField] private float sortingPrecision = 100f;

        /// <summary>바닥 평면(XZ)에 눕히는 회전. 그림자와 바닥 판이 같이 쓴다.</summary>
        public static readonly Quaternion LieOnGround = Quaternion.Euler(90f, 0f, 0f);

        /// <summary>
        /// 몸 스프라이트가 붙은 자식. 색을 바꾸려는 쪽(<see cref="EnemyStateTint"/>)이
        /// 그림자를 잘못 집지 않도록 정확히 이 하나만 열어 준다.
        /// </summary>
        public Transform SpriteRoot => sprite;

        /// <summary>
        /// 정렬 순서에 통째로 더하는 값. 기본 0.
        ///
        /// 벽에서 걸어 나오는 적이 쓴다(<see cref="EnemySpawnGuard"/>) — 크게 음수를 주면
        /// 배경 벽보다도 뒤로 가서 <b>벽에 가려져 있다가 걸어 나오는 그림</b>이 된다.
        /// 페이드인보다 이쪽이 벨트스크롤 감각에 훨씬 잘 맞는다.
        /// </summary>
        public int SortingOffset { get; set; }

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

            // 조준점 등 다른 시스템도 같은 배율을 써야 한다. 한 벌만 유지한다.
            BeltScroll.DepthScalePerUnit = depthScalePerUnit;

            Vector3 ground = physics.GroundPosition;
            float height = Mathf.Max(0f, physics.Height);

            // depthRoot가 없는 프리팹은 예전 동작 그대로 둔다 — 배율 1.
            float scale = depthRoot != null ? BeltScroll.ScaleAt(ground.z) : 1f;

            // 발밑 보정은 <b>화면 위</b>로 올린다. 월드 Y로 올리면 기울기만큼(cosθ) 짧아져
            // 뒤쪽 캐릭터의 발이 그림자에 파묻힌다.
            Quaternion billboard = BeltScroll.Billboard;
            Vector3 screenUp = BeltScroll.ScreenUp;

            if (depthRoot != null)
            {
                // 배율은 여기서만 건다. Sprite의 localScale은 Animator 것이라
                // 거기 쓰면 스쿼시 · 스트레치가 매 프레임 지워진다.
                depthRoot.localScale = new Vector3(scale, scale, 1f);

                // 오프셋에도 배율을 곱해야 뒤쪽 캐릭터의 발이 그림자 위에 붙는다.
                depthRoot.position = BeltScroll.ToView(ground, height) + screenUp * (spriteOffsetY * scale);

                // 루트는 Facing 방향으로 돌아간다(Physics.Apply). 자식까지 돌면 옆면이 보이므로
                // 매 프레임 카메라 쪽으로 되돌린다 — 빌보드.
                depthRoot.rotation = billboard;
            }

            if (sprite != null)
            {
                if (depthRoot == null)
                {
                    sprite.position = BeltScroll.ToView(ground, height) + screenUp * spriteOffsetY;
                    sprite.rotation = billboard;
                }

                // 빌보드로 회전을 지웠으니 방향은 좌우 반전으로만 표현된다.
                // 시트는 오른쪽을 보고 그려져 있다.
                if (flipToFacing)
                {
                    if (facingRenderer == null) facingRenderer = sprite.GetComponent<SpriteRenderer>();
                    if (facingRenderer != null && Mathf.Abs(physics.Facing.x) > 0.0001f)
                        facingRenderer.flipX = physics.Facing.x < 0f;
                }
            }

            if (shadow != null)
            {
                // 그림자만은 빌보드가 아니다. 바닥 평면에 눕혀야 진짜 바닥에 붙은 것으로 읽힌다.
                shadow.position = BeltScroll.ToView(ground) + Vector3.up * shadowLift;
                shadow.rotation = LieOnGround;

                float shrink = Mathf.Clamp(1f - height * shadowShrinkPerUnit, 0.3f, 1f);
                shadow.localScale = shadowBaseScale * (shrink * scale);
            }

            ApplySorting(ground.z);
        }

        private void ApplySorting(float z)
        {
            if (sortedRenderers == null) return;

            int order = Mathf.RoundToInt(-z * sortingPrecision) + SortingOffset;
            for (int i = 0; i < sortedRenderers.Length; i++)
            {
                if (sortedRenderers[i] == null) continue;
                sortedRenderers[i].sortingOrder = order + i;
            }
        }
    }
}
