using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 지금 조작하지 않는 동료가 <b>화면 밖 어디에 있다고 치는가</b>. <b>순수 함수</b>다.
    ///
    /// 이 좌표 한 벌이 세 곳에서 쓰인다 — 교대로 나가는 몸의 <b>목적지</b>,
    /// 불려 나오는 몸의 <b>출발지</b>, 그리고 가장자리 <b>표식</b>이 가리키는 자리.
    /// 그래서 여기가 틀리면 세 군데에서 동시에 틀린다. 다행히 그건 좋은 성질이다 —
    /// 표식이 있던 자리에서 실제로 몸이 나오므로 어긋나면 바로 눈에 띈다.
    ///
    /// <b>칸은 로스터 순번에 못박는다.</b> 살아 있는 사람만 모아 다시 매기면 한 명이 죽거나
    /// 교대할 때마다 나머지 표식이 우르르 자리를 바꾼다 — 유저는 "왼쪽이 마법사"를
    /// 학습할 수 없게 되고, 그 순간 표식은 정보가 아니라 소음이 된다.
    /// </summary>
    public static class StandbyRules
    {
        /// <summary>화면 끝에서 더 밀어내는 여유. 카메라가 조금 흔들려도 안 보여야 한다.</summary>
        public const float EdgeMargin = 2.5f;

        /// <summary>같은 쪽에 둘 이상 설 때 바깥으로 더 밀어내는 간격.</summary>
        public const float StackStep = 1.6f;

        /// <summary>
        /// 깊이 줄. 두 칸이 한 쌍으로 같은 줄을 좌우로 나눠 쓴다.
        ///
        /// 표식은 화면 가장자리에 물리므로 좌우로는 어차피 겹친다 —
        /// <b>둘을 갈라 보이게 하는 것은 깊이뿐</b>이다.
        /// </summary>
        private static readonly float[] Lanes = { 1.2f, -1.2f, 2.4f, -2.4f };

        /// <summary>이 칸이 어느 쪽에 서는가. +1이 오른쪽, -1이 왼쪽. 짝수 오른쪽 · 홀수 왼쪽.</summary>
        public static int SideOf(int slot) => Mathf.Max(0, slot) % 2 == 0 ? 1 : -1;

        /// <summary>같은 쪽에서 몇 번째인가. 0부터.</summary>
        public static int StackOf(int slot) => Mathf.Max(0, slot) / 2;

        /// <summary>이 칸의 깊이. 방 밖으로 나가지 않게 물린다.</summary>
        public static float LaneOf(int slot)
            => WaveSpawnPlanner.Clamp(Lanes[StackOf(slot) % Lanes.Length]);

        /// <summary>
        /// 이 칸의 대기 좌표.
        ///
        /// <b>카메라 기준이다.</b> 조작 캐릭터 기준이 아니라 화면 기준이어야
        /// "화면 밖"이라는 말이 그대로 성립하고, 카메라가 움직이면 표식도 같이 따라간다.
        /// 화면 밖 계산은 <see cref="EntranceRules.OffscreenX"/>를 그대로 쓴다 —
        /// 몸이 실제로 오갈 자리이므로 등장 연출과 <b>같은 함수</b>여야 한다.
        /// </summary>
        public static Vector3 Slot(int slot, float cameraX, float halfWidth)
        {
            int side = SideOf(slot);
            float margin = EdgeMargin + StackStep * StackOf(slot);

            // 착지점을 화면 중심으로 넘긴다 — "화면 끝에서 margin 만큼 밖"이라는 뜻이 된다.
            float x = EntranceRules.OffscreenX(cameraX, side, cameraX, halfWidth, margin);

            return new Vector3(x, 0f, LaneOf(slot));
        }
    }
}
