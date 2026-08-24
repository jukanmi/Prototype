using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
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
}
