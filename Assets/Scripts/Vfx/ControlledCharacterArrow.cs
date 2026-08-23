using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 플레이어가 현재 조작 중인 캐릭터(플레이어 또는 태그 교대된 동료) 머리 위에
    /// 화살표(<c>▼</c>)를 띄워 난전 중에도 내 위치를 즉시 식별할 수 있게 한다.
    ///
    /// <see cref="TagSwapController.Current"/>를 추적하며, 교대 시 새 몸으로 즉시 옮겨간다.
    /// <see cref="EnemyStateLabel"/>·<see cref="ChargeGauge"/>와 같은 자리(<c>[BattleVfx]</c>)에
    /// 붙어 씬 배선 없이 자동 동작한다.
    /// </summary>
    public class ControlledCharacterArrow : MonoBehaviour
    {
        [Tooltip("머리 위로 띄우는 기본 높이. 상태 게이지(StatusEffectBar, 2.25)보다 위여야 겹치지 않는다.")]
        [SerializeField] private float headOffset = 2.4f;

        [Tooltip("상하 부유(플로팅) 진폭.")]
        [SerializeField] private float bobHeight = 0.12f;

        [Tooltip("상하 부유 속도.")]
        [SerializeField] private float bobSpeed = 5f;

        [Tooltip("정렬 오프셋. 상태 게이지(340)보다 앞이다.")]
        [SerializeField] private int sortingOffset = 360;

        [Tooltip("월드 단위 글자 크기.")]
        [SerializeField] private float characterSize = 0.08f;

        [SerializeField] private int fontSize = 54;

        [Tooltip("화살표 기본 색상.")]
        [SerializeField] private Color arrowColor = new Color(1f, 0.86f, 0.35f, 1f);

        private TextMesh textMesh;
        private MeshRenderer meshRenderer;
        private TagSwapController cachedSwap;

        /// <summary>
        /// 화살표가 놓일 자리.
        ///
        /// <see cref="EnemyStateLabel.LabelPosition"/>과 같은 규칙이다 — 논리 좌표가 아니라
        /// <see cref="BeltScroll.ToView"/>를 거친 그리는 위치를 쓰고, 머리 오프셋과 부유 높이에는
        /// 깊이 배율을 먹인다(뒤에 선 캐릭터는 몸이 줄어 머리도 내려온다).
        /// </summary>
        public static Vector3 ArrowPosition(Vector3 ground, float height, float headOffset, float bobOffset = 0f)
            => BeltScroll.ToView(ground, height + (headOffset + bobOffset) * BeltScroll.ScaleAt(ground.z));

        /// <summary>시간에 따른 상하 부유 오프셋 계산 (순수 함수).</summary>
        public static float BobOffset(float unscaledTime, float speed, float height)
            => Mathf.Sin(unscaledTime * speed) * height;

        private void Awake()
        {
            EnsureRenderer();
        }

        private void LateUpdate()
        {
            Entity target = ResolveTarget();
            if (target == null || target.Combat == null || target.Combat.IsDead || !target.gameObject.activeSelf)
            {
                if (meshRenderer != null) meshRenderer.enabled = false;
                return;
            }

            Draw(target);
        }

        private Entity ResolveTarget()
        {
            if (cachedSwap == null)
                cachedSwap = FindAnyObjectByType<TagSwapController>();

            if (cachedSwap != null && cachedSwap.Current != null)
                return cachedSwap.Current;

            // 태그 컨트롤러가 없는 씬을 위한 폴백: 아군 중 PlayerControl을 쥐고 있는 몸
            var allies = BattleRegistry.Allies;
            if (allies != null)
            {
                for (int i = 0; i < allies.Count; i++)
                {
                    Entity e = allies[i];
                    if (e != null && e.Control is PlayerControl && e.gameObject.activeSelf && !e.Combat.IsDead)
                        return e;
                }
            }

            return null;
        }

        private void Draw(Entity target)
        {
            EnsureRenderer();

            Physics phys = target.Physics;
            Vector3 ground = phys.GroundPosition;

            float bob = BobOffset(Time.unscaledTime, bobSpeed, bobHeight);
            Vector3 pos = ArrowPosition(ground, phys.Height, headOffset, bob);

            textMesh.transform.position = pos;
            textMesh.transform.rotation = Quaternion.identity; // 빌보드

            textMesh.text = "▼";
            textMesh.color = arrowColor;

            // 캐릭터와 같은 깊이 정렬 공식(BeltScrollView.ApplySorting) 위에서 오프셋 적용
            meshRenderer.sortingOrder = Mathf.RoundToInt(-ground.z * 100f) + sortingOffset;
            meshRenderer.enabled = true;
        }

        private void EnsureRenderer()
        {
            if (textMesh != null && meshRenderer != null) return;

            var go = new GameObject("ControlledCharacterArrow");
            go.transform.SetParent(transform, false);

            textMesh = go.AddComponent<TextMesh>();
            textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textMesh.fontSize = fontSize;
            textMesh.fontStyle = FontStyle.Bold;
            textMesh.characterSize = characterSize;
            textMesh.anchor = TextAnchor.LowerCenter;
            textMesh.alignment = TextAlignment.Center;

            meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = textMesh.font.material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
        }
    }
}
