// 모달 안에서 칸을 옮기는 규칙. 그리는 것도 입력을 읽는 것도 여기 없다 — 인덱스 계산만 한다.

using System;

namespace Prototype
{
    /// <summary>
    /// 키보드로 고르는 <b>한 줄짜리 칸</b>의 커서 규칙.
    ///
    /// 잠긴 칸을 건너뛰는 판단이 화면마다 따로 있으면 "경험치가 모자란 단계에 커서가 얹히는"
    /// 식으로만 드러난다. 규칙을 한 군데 두고 <c>MenuCursorTests</c>가 지킨다.
    ///
    /// <b>감싸지 않는다.</b> 칸이 서너 개뿐인 줄에서 끝에서 반대쪽으로 튀면 어디까지 갔는지
    /// 손이 못 센다. 끝에 닿으면 그냥 멈춘다.
    /// </summary>
    public static class MenuCursor
    {
        /// <summary>커서를 둘 자리가 없다.</summary>
        public const int None = -1;

        /// <summary>처음 커서를 놓을 칸. 쓸 수 있는 칸이 하나도 없으면 <see cref="None"/>.</summary>
        public static int First(int count, Func<int, bool> enabled)
        {
            for (int i = 0; i < count; i++)
                if (IsEnabled(i, count, enabled)) return i;

            return None;
        }

        /// <summary>
        /// 한 칸 옮긴다. 잠긴 칸은 건너뛰고, 그 방향에 쓸 수 있는 칸이 더 없으면 제자리에 남는다.
        /// </summary>
        /// <param name="dir">-1은 왼쪽(위), +1은 오른쪽(아래). 0이면 움직이지 않는다.</param>
        public static int Step(int current, int count, int dir, Func<int, bool> enabled)
        {
            if (dir == 0) return current;

            // 커서가 줄 밖에 있으면 옮길 기준이 없다. 처음 놓는 것과 같게 친다.
            if (current < 0 || current >= count) return First(count, enabled);

            for (int i = current + Math.Sign(dir); i >= 0 && i < count; i += Math.Sign(dir))
                if (IsEnabled(i, count, enabled)) return i;

            return current;
        }

        /// <summary>
        /// 지금 자리가 아직 유효한가 보고, 아니면 다시 놓는다.
        /// 칸의 잠금이 바뀐 뒤(경험치를 쓰고 돌아온 단계 판) 부른다.
        /// </summary>
        public static int Clamp(int current, int count, Func<int, bool> enabled)
            => IsEnabled(current, count, enabled) ? current : First(count, enabled);

        private static bool IsEnabled(int index, int count, Func<int, bool> enabled)
        {
            if (index < 0 || index >= count) return false;
            return enabled == null || enabled(index);
        }
    }
}
