// 바닥 — 판정 규칙 · 판 하나 · 판들을 모으는 등록소.
// GroundRegistry가 GroundPlate를 모으고 GroundRules가 그 위에서 판정한다.

using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ GroundRules ═══════════════════════════════════════════

    /// <summary>
    /// 발판 하나. XZ 평면의 사각형과 그 높이다.
    ///
    /// 벨트스크롤에서 "밟고 있는가"는 <b>중심 한 점</b>으로 판정한다 — 발이 반쯤 걸친 상태를
    /// 물리로 풀면 가장자리에서 미끄러지는 느낌이 나고, 그건 이 장르가 원하는 감각이 아니다.
    /// </summary>
    public readonly struct GroundRect
    {
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float MinZ;
        public readonly float MaxZ;

        /// <summary>발판 윗면의 높이. 착지하면 여기에 선다.</summary>
        public readonly float Y;

        public GroundRect(float minX, float maxX, float minZ, float maxZ, float y)
        {
            MinX = Mathf.Min(minX, maxX);
            MaxX = Mathf.Max(minX, maxX);
            MinZ = Mathf.Min(minZ, maxZ);
            MaxZ = Mathf.Max(minZ, maxZ);
            Y = y;
        }

        public static GroundRect FromBounds(Bounds b)
            => new GroundRect(b.min.x, b.max.x, b.min.z, b.max.z, b.max.y);

        /// <summary><paramref name="margin"/>은 가장자리를 넓히는 여유. 음수면 좁아진다.</summary>
        public bool Contains(float x, float z, float margin = 0f)
            => x >= MinX - margin && x <= MaxX + margin
            && z >= MinZ - margin && z <= MaxZ + margin;

        public override string ToString()
            => $"x[{MinX:0.##}..{MaxX:0.##}] z[{MinZ:0.##}..{MaxZ:0.##}] y={Y:0.##}";
    }

    /// <summary>
    /// 발판 질의 규칙. 순수 함수라 씬 없이 테스트한다.
    ///
    /// <b>왜 따로 두는가.</b> 예전에는 바닥이 <c>groundY</c> float 하나, 즉 무한 평면이었다.
    /// 그래서 발판 그림을 벗어나도 y=0에 그대로 서 있었다. 낙차를 넣으려면 "여기 발판이 있나"를
    /// 물을 자리가 먼저 있어야 한다 — 이 클래스가 그 자리다.
    /// </summary>
    public static class GroundRules
    {
        /// <summary>
        /// 발판이 이만큼 위에 있어도 "머리 위"로 보지 않는다.
        /// 서 있는 판과 발 높이가 부동소수점 오차로 어긋나는 걸 흡수한다.
        /// </summary>
        public const float StepTolerance = 0.05f;

        /// <summary>
        /// <paramref name="point"/> 아래에 있는 발판 중 <b>가장 높은</b> 것의 높이.
        ///
        /// 머리 위 발판은 무시한다 — 아래에서 위층으로 순간이동시키면 안 된다.
        /// 겹친 발판(통로 타일 이음매)에서는 위쪽이 이긴다.
        /// </summary>
        public static bool TryHeightAt(IReadOnlyList<GroundRect> plates, Vector3 point,
                                       float margin, out float y)
        {
            y = 0f;
            if (plates == null) return false;

            bool found = false;

            for (int i = 0; i < plates.Count; i++)
            {
                GroundRect r = plates[i];

                if (!r.Contains(point.x, point.z, margin)) continue;
                if (r.Y > point.y + StepTolerance) continue;   // 머리 위 판

                if (!found || r.Y > y)
                {
                    y = r.Y;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>높이는 필요 없고 "발판 위인가"만 묻는 쪽.</summary>
        public static bool Supports(IReadOnlyList<GroundRect> plates, Vector3 point, float margin)
            => TryHeightAt(plates, point, margin, out _);
    }

    // ══ GroundPlate ═══════════════════════════════════════════

    /// <summary>
    /// 밟을 수 있는 바닥 한 장. 붙인 오브젝트의 XZ 범위가 곧 발판이다.
    ///
    /// 예전에는 바닥이 <c>Physics.groundY</c> float 하나였다 — y=0 무한 평면이라
    /// 바닥 그림을 벗어나도 그대로 서 있었고, 밖으로 못 나가게 막는 건 벽 콜라이더뿐이었다.
    /// 낙차를 넣으려면 "여기 발판이 있나"를 물을 대상이 필요하다. 이게 그 대상이다.
    ///
    /// <b>콜라이더가 아니다.</b> 유니티 물리에 맡기면 가장자리에서 미끄러지고 캡슐이 걸린다.
    /// 판정은 중심 한 점의 사각형 포함 검사다(<see cref="GroundRules"/>).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GroundPlate : MonoBehaviour
    {
        [Tooltip("발판 반폭(X)과 반깊이(Z). 둘 다 0이면 이 오브젝트의 Renderer 범위에서 잰다.")]
        [SerializeField] private Vector2 halfExtents = Vector2.zero;

        [Tooltip("발판 윗면 높이를 오브젝트 위치에서 얼마나 올릴지. 보통 0.")]
        [SerializeField] private float surfaceOffsetY = 0f;

        [Tooltip("씬 뷰에 발판 범위를 그린다.")]
        [SerializeField] private bool drawGizmo = true;

        private GroundRect cached;
        private int cachedFrame = -1;

        /// <summary>
        /// 월드 기준 발판 범위. 한 프레임에 한 번만 다시 잰다 —
        /// 유닛 수만큼 질의가 들어오는데 Renderer.bounds는 공짜가 아니다.
        /// </summary>
        public GroundRect Rect
        {
            get
            {
                if (cachedFrame == Time.frameCount) return cached;

                cached = Measure();
                cachedFrame = Time.frameCount;
                return cached;
            }
        }

        private void OnEnable() => GroundRegistry.Register(this);
        private void OnDisable() => GroundRegistry.Unregister(this);

        /// <summary>인스펙터에서 크기를 만지면 다음 질의에 바로 반영되게 한다.</summary>
        private void OnValidate() => cachedFrame = -1;

        private GroundRect Measure()
        {
            Vector3 c = transform.position;
            c.y += surfaceOffsetY;

            if (halfExtents.x > 0.0001f && halfExtents.y > 0.0001f)
                return new GroundRect(c.x - halfExtents.x, c.x + halfExtents.x,
                                      c.z - halfExtents.y, c.z + halfExtents.y, c.y);

            // 크기를 안 적었으면 그림에서 잰다. 바닥 판을 늘리면 발판도 같이 늘어난다 —
            // 그림과 판정이 갈라지는 것이 애초에 이 버그의 원인이었다.
            var r = GetComponent<Renderer>();
            if (r != null)
            {
                Bounds b = r.bounds;
                return new GroundRect(b.min.x, b.max.x, b.min.z, b.max.z, b.max.y + surfaceOffsetY);
            }

            // 마지막 보루. 크기를 못 재면 없는 발판으로 두는 편이 조용히 틀리는 것보다 낫다.
            return new GroundRect(c.x, c.x, c.z, c.z, c.y);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;

            GroundRect r = Measure();
            var center = new Vector3((r.MinX + r.MaxX) * 0.5f, r.Y, (r.MinZ + r.MaxZ) * 0.5f);
            var size = new Vector3(r.MaxX - r.MinX, 0.02f, r.MaxZ - r.MinZ);

            Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(center, size);
        }
    }

    // ══ GroundRegistry ═══════════════════════════════════════════

    /// <summary>
    /// 씬에 깔린 발판 목록. <see cref="GroundPlate"/>가 스스로 등록한다.
    ///
    /// <b>비어 있으면 무한 평면으로 답한다.</b> 발판을 아직 안 깐 씬(테스트 씬 · 훈련장)이
    /// 조용히 바닥을 잃으면 안 된다 — 등록된 판이 하나도 없을 때는 예전 동작 그대로다.
    /// </summary>
    public static class GroundRegistry
    {
        private static readonly List<GroundPlate> plates = new List<GroundPlate>();

        /// <summary>질의마다 새로 만들지 않도록 재사용한다. 매 프레임 유닛 수만큼 불린다.</summary>
        private static readonly List<GroundRect> scratch = new List<GroundRect>();

        /// <summary>
        /// Enter Play Mode Options가 Domain Reload를 끄고 있어 static이 살아남는다.
        /// 리셋하지 않으면 지난 세션의 파괴된 발판이 목록에 그대로 쌓인다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();

        public static int Count => plates.Count;

        /// <summary>발판이 하나도 없으면 판정을 하지 않는다 — 예전 무한 평면으로 답한다.</summary>
        public static bool HasPlates => plates.Count > 0;

        public static void Register(GroundPlate plate)
        {
            if (plate != null && !plates.Contains(plate)) plates.Add(plate);
        }

        public static void Unregister(GroundPlate plate) => plates.Remove(plate);

        public static void Clear()
        {
            plates.Clear();
            scratch.Clear();
        }

        /// <summary>
        /// <paramref name="point"/> 아래 발판의 높이. 없으면 <paramref name="fallback"/>.
        /// 등록된 발판이 하나도 없어도 <paramref name="fallback"/>이다.
        /// </summary>
        public static float HeightAt(Vector3 point, float fallback, float margin = 0f)
            => TryHeightAt(point, margin, out float y) ? y : fallback;

        public static bool TryHeightAt(Vector3 point, float margin, out float y)
        {
            y = 0f;
            if (plates.Count == 0) return false;

            Fill();
            return GroundRules.TryHeightAt(scratch, point, margin, out y);
        }

        /// <summary>
        /// 이 점에 발판이 있는가. <b>발판을 안 깐 씬에서는 항상 true</b>다 —
        /// 판정을 도입하는 것만으로 기존 씬이 허공에 뜨면 안 된다.
        /// </summary>
        public static bool Supports(Vector3 point, float margin = 0f)
        {
            if (plates.Count == 0) return true;

            Fill();
            return GroundRules.Supports(scratch, point, margin);
        }

        private static void Fill()
        {
            scratch.Clear();

            for (int i = 0; i < plates.Count; i++)
            {
                GroundPlate p = plates[i];
                if (p == null) continue;          // 파괴된 판. 다음 Unregister에서 빠진다.
                scratch.Add(p.Rect);
            }
        }
    }
}
