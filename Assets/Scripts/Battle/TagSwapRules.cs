using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 태그 교대 순서를 정하는 규칙. <see cref="TagSwapController"/>에서 떼어 낸 순수 계산이다.
    ///
    /// 핵심 두 메서드가 <see cref="Entity"/>가 아니라 <b>술어</b>를 받는 이유는 EditMode 테스트
    /// 때문이다. 에디트모드에서는 <c>Awake</c>가 돌지 않아 <c>Combat.Die()</c>가
    /// <c>physics</c>에서 터진다 — 즉 "죽은 캐릭터"를 만들 방법이 없다. 술어로 받으면
    /// 순회 규칙만 씬 없이 검증할 수 있고, 로스터를 읽는 부분은 얇은 어댑터로 남는다.
    /// </summary>
    public static class TagSwapRules
    {
        /// <summary>
        /// <paramref name="current"/> <b>다음</b> 칸부터 한 바퀴 돌며 세울 수 있는 첫 칸.
        ///
        /// 세울 수 있는 칸이 <paramref name="current"/> 하나뿐이면 그대로 돌려준다 —
        /// "바꿀 사람이 없다"와 "아무도 없다"는 부르는 쪽이 다르게 다뤄야 하기 때문이다.
        /// 아무도 없으면 -1.
        /// </summary>
        public static int Next(int count, Func<int, bool> selectable, int current)
        {
            if (count <= 0 || selectable == null) return -1;

            // current가 범위 밖(-1 포함)이면 0번 앞에서 시작한 것으로 본다.
            int from = current >= 0 && current < count ? current : -1;

            for (int step = 1; step <= count; step++)
            {
                int i = Wrap(from + step, count);
                if (selectable(i)) return i;
            }

            // 한 바퀴를 다 돌아도 못 찾았다 = 자기 자신뿐이거나 아무도 없다.
            return from >= 0 && selectable(from) ? from : -1;
        }

        /// <summary>처음으로 세울 수 있는 칸. 시작 시 한 번 쓴다.</summary>
        public static int First(int count, Func<int, bool> selectable)
        {
            if (selectable == null) return -1;

            for (int i = 0; i < count; i++)
                if (selectable(i)) return i;

            return -1;
        }

        // ── 로스터 어댑터 ───────────────────────────────────

        public static int NextAlive(IReadOnlyList<Entity> roster, int current)
            => roster == null ? -1 : Next(roster.Count, i => IsSelectable(roster, i), current);

        public static int FirstAlive(IReadOnlyList<Entity> roster)
            => roster == null ? -1 : First(roster.Count, i => IsSelectable(roster, i));

        /// <summary>
        /// 그 칸을 필드에 세울 수 있는가. 빈 칸과 사망을 거른다.
        /// <b>활성 여부는 보지 않는다</b> — 지금 꺼져 있는 몸을 고르는 게 교대의 목적이다.
        /// </summary>
        public static bool IsSelectable(IReadOnlyList<Entity> roster, int index)
        {
            if (roster == null || index < 0 || index >= roster.Count) return false;

            Entity e = roster[index];
            return e != null && e.Combat != null && !e.Combat.IsDead;
        }

        private static int Wrap(int index, int count) => ((index % count) + count) % count;
    }
}
