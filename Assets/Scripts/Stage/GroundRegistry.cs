using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
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
