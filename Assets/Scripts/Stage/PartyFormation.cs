using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 동료가 조작 중인 몸을 따라 서는 자리. <b>순수 함수</b>다.
    ///
    /// 이게 없으면 아레나 스테이지가 성립하지 않는다 — <see cref="AllyControl"/>은
    /// 적을 쫓는 것만 알아서, 적이 없는 <b>통로에서 동료가 그냥 서 있는다.</b>
    /// 그대로 걸어가면 다음 아레나가 혼자 시작되고, 문이 닫힌 뒤에는 부를 방법도 없다.
    ///
    /// 자리는 뒤쪽으로 벌린다. 앞에 세우면 플레이어의 시야와 평타 경로를 동료가 막는다.
    /// </summary>
    public static class PartyFormation
    {
        /// <summary>이 거리 안이면 다 온 것으로 친다. 좁게 잡으면 제자리에서 계속 떤다.</summary>
        public const float KeepRadius = 0.8f;

        /// <summary>
        /// 대형. 리더의 <b>뒤쪽</b>(-X)으로 벌리고 깊이를 엇갈려 둔다 —
        /// 한 줄로 세우면 뒤쪽 동료가 앞 동료에게 막혀 영영 못 따라온다.
        /// <c>SceneLayoutBuilder</c>의 시작 배치와 같은 모양이다.
        /// </summary>
        private static readonly Vector3[] Slots =
        {
            new Vector3(-1.6f, 0f,  1.0f),
            new Vector3(-1.6f, 0f, -1.0f),
            new Vector3(-2.8f, 0f,  2.0f),
            new Vector3(-2.8f, 0f, -2.0f),
        };

        public static int SlotCount => Slots.Length;

        /// <summary>슬롯 번호는 돌려 쓴다 — 동료가 대형보다 많아도 자리가 없어지지 않는다.</summary>
        public static Vector3 SlotOffset(int slot)
            => Slots[((slot % Slots.Length) + Slots.Length) % Slots.Length];

        /// <summary>
        /// 이 동료가 서야 할 월드 좌표.
        ///
        /// <paramref name="leaderFacingX"/>로 좌우를 뒤집는다. 리더가 왼쪽으로 갈 때도
        /// 대형이 오른쪽에 남아 있으면 <b>동료가 진행 방향 앞을 막는다</b> —
        /// 벨트스크롤에서 이건 그냥 조작이 안 되는 것처럼 느껴진다.
        /// </summary>
        public static Vector3 PointFor(Vector3 leaderGround, int slot, float leaderFacingX)
        {
            Vector3 offset = SlotOffset(slot);
            if (leaderFacingX < 0f) offset.x = -offset.x;

            return new Vector3(leaderGround.x + offset.x, 0f, leaderGround.z + offset.z);
        }

        /// <summary>자리로 붙어야 하는가. 대형 안이면 가만히 있는 편이 낫다.</summary>
        public static bool ShouldClose(float distance) => distance > KeepRadius;
    }
}
