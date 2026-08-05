using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 2.5D 표현 규칙. 논리 좌표는 3D(X 좌우 / Z 깊이 / Y 높이)지만
    /// 스프라이트는 <b>(X, Z + Y)</b> 위치에 그린다.
    /// 가시성 확보를 위해 그림자는 항상 바닥 (X, Z)에 고정한다.
    /// </summary>
    [ExecuteAlways]
    public class BeltScrollView : MonoBehaviour
    {
        [Tooltip("스프라이트를 담은 자식. 루트는 논리 좌표를 유지한다.")]
        [SerializeField] private Transform sprite;
        [Tooltip("바닥에 고정되는 그림자.")]
        [SerializeField] private Transform shadow;
        [Tooltip("깊이(Z)가 화면 세로로 환산되는 비율. 벨트의 기울기. tan θ에 해당한다.")]
        [SerializeField] private float depthToScreen = 0.5f;
        [Tooltip("발이 바닥에 닿아 보이도록 스프라이트를 위로 올리는 양. 보통 스프라이트 높이의 절반.")]
        [SerializeField] private float spriteOffsetY = 0.5f;
        [Tooltip("그림자 기본 배율. 바닥에 눕혀 보이도록 Y를 납작하게 준다.")]
        [SerializeField] private Vector3 shadowBaseScale = new Vector3(0.9f, 0.35f, 1f);
        [Tooltip("높이에 따라 그림자를 줄여 체공감을 준다.")]
        [SerializeField] private float shadowShrinkPerUnit = 0.12f;
        [SerializeField] private SpriteRenderer[] sortedRenderers;
        [Tooltip("Z가 클수록(멀수록) 뒤에 그린다.")]
        [SerializeField] private float sortingPrecision = 100f;

        private Physics physics;

        private void Awake()
        {
            physics = GetComponentInParent<Physics>();
        }

        private void LateUpdate()
        {
            if (physics == null)
            {
                physics = GetComponentInParent<Physics>();
                if (physics == null) return;
            }

            // 조준점 등 다른 시스템도 같은 비율로 투영해야 한다. 한 벌만 유지한다.
            BeltScroll.DepthToScreen = depthToScreen;

            Vector3 ground = physics.GroundPosition;
            float height = Mathf.Max(0f, physics.Height);

            if (sprite != null)
            {
                sprite.position = BeltScroll.ToView(ground, height + spriteOffsetY);
                // 루트는 Facing 방향으로 돌아간다(Physics.Apply). 스프라이트까지 돌면 옆면이 보이므로
                // 월드 회전을 매 프레임 되돌린다 — 빌보드.
                sprite.rotation = Quaternion.identity;
            }

            if (shadow != null)
            {
                shadow.position = BeltScroll.ToView(ground);
                shadow.rotation = Quaternion.identity;

                float shrink = Mathf.Clamp(1f - height * shadowShrinkPerUnit, 0.3f, 1f);
                shadow.localScale = shadowBaseScale * shrink;
            }

            ApplySorting(ground.z);
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
