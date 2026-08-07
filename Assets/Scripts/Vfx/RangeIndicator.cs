using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 불릿타임 조준 중 스킬 시전 범위를 바닥에 그린다.
    ///
    /// <see cref="TargetSelector"/>가 이미 논리 좌표 · 반경 · 헛침 여부를 다 들고 있다.
    /// 지금까지는 <c>OnDrawGizmosSelected</c>뿐이라 에디터에서 그 오브젝트를 고른 동안만 보였다.
    /// 여기서는 실제 게임 화면에 그린다.
    /// </summary>
    public class RangeIndicator : MonoBehaviour
    {
        private const int Segments = 40;

        [SerializeField] private TargetSelector selector;

        [Tooltip("반경 안에 적이 있을 때.")]
        [SerializeField] private Color okColor = new Color(0.35f, 0.9f, 1f, 0.85f);
        [Tooltip("반경 안에 적이 하나도 없을 때 — 헛침 경고.")]
        [SerializeField] private Color whiffColor = new Color(1f, 0.35f, 0.35f, 0.85f);
        // 공격 VFX가 0.13~0.16으로 그린다. 0.06은 그 옆에서 안 읽혔다.
        [SerializeField] private float lineWidth = 0.12f;
        [Tooltip("초당 깜빡임 횟수. 0이면 고정.")]
        [SerializeField] private float pulseSpeed = 2.2f;

        private LineRenderer ring;
        private LineRenderer cursor;
        private Material shared;
        private float pulse;

        private void Awake()
        {
            shared = BattleVfx.CreateMaterial();

            ring = CreateLine("RangeRing", Segments);
            cursor = CreateLine("RangeCursor", 4);
        }

        private LineRenderer CreateLine(string name, int points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.loop = true;
            lr.positionCount = points;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = shared;
            lr.enabled = false;

            return lr;
        }

        // 조준은 시간이 멈춘 동안 돌아간다. TargetSelector가 커서를 옮긴 뒤에 그려야
        // 한 프레임 밀리지 않으므로 LateUpdate에서 처리한다.
        private void LateUpdate()
        {
            // 씬이 바뀌면 참조가 끊긴다. 이 오브젝트는 DontDestroyOnLoad라 매번 확인한다.
            if (selector == null) selector = FindAnyObjectByType<TargetSelector>();

            bool show = selector != null
                        && selector.IsSelecting
                        && selector.Current != null
                        && selector.Current.targeting == TargetingType.GroundPoint;

            ring.enabled = show;
            cursor.enabled = show;
            if (!show) return;

            // 시간이 멈춰 있으므로 unscaled로 돈다. TargetSelector와 같은 시계.
            pulse += TimeControl.UnscaledDeltaTime * pulseSpeed;

            Color c = selector.WillWhiff ? whiffColor : okColor;
            // 하한 0.55는 실효 알파가 0.47까지 떨어져 배경에 묻혔다. 깜빡임은 남기고 바닥만 올린다.
            c.a *= pulseSpeed > 0f ? Mathf.Lerp(0.75f, 1f, (Mathf.Sin(pulse * Mathf.PI * 2f) + 1f) * 0.5f) : 1f;

            DrawRing(selector.CursorPoint, selector.Current.radius, c);
            DrawCursor(selector.CursorViewPoint, c);
        }

        /// <summary>
        /// 논리 XZ 원을 화면 좌표로 접는다. 접히고 나면 바닥에 누운 타원으로 보인다 —
        /// 캐릭터가 서 있는 평면과 같은 공간이라야 "저기가 사거리 안"이 읽힌다.
        /// </summary>
        private void DrawRing(Vector3 center, float radius, Color c)
        {
            center.y = 0f;

            for (int i = 0; i < Segments; i++)
            {
                float a = i * (360f / Segments) * Mathf.Deg2Rad;
                Vector3 ground = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                ring.SetPosition(i, BeltScroll.ToView(ground));
            }

            ring.startColor = c;
            ring.endColor = c;
            ring.startWidth = lineWidth;
            ring.endWidth = lineWidth;

            // 바닥에 깔린 표시다. 같은 깊이의 캐릭터보다 뒤에 그려야 발을 가리지 않는다.
            ring.sortingOrder = Mathf.RoundToInt(-center.z * 100f) - 20;
        }

        /// <summary>커서 자리의 작은 마름모. 원이 커지면 중심을 놓치기 쉽다.</summary>
        private void DrawCursor(Vector3 viewPoint, Color c)
        {
            const float s = 0.18f;

            cursor.SetPosition(0, viewPoint + new Vector3(0f, s, 0f));
            cursor.SetPosition(1, viewPoint + new Vector3(s, 0f, 0f));
            cursor.SetPosition(2, viewPoint + new Vector3(0f, -s, 0f));
            cursor.SetPosition(3, viewPoint + new Vector3(-s, 0f, 0f));

            cursor.startColor = c;
            cursor.endColor = c;
            cursor.startWidth = lineWidth;
            cursor.endWidth = lineWidth;

            // 커서만은 항상 보여야 한다. 캐릭터 위로 올린다.
            cursor.sortingOrder = Mathf.RoundToInt(-viewPoint.z * 100f) + 200;
        }

        private void OnDestroy()
        {
            if (shared != null) Destroy(shared);
        }
    }
}
