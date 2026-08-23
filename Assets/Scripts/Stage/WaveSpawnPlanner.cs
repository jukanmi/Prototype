using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 한 기가 <b>어디서 나와 어디에 설지</b>. <see cref="StageDirector"/>가 이 결과대로 소환한다.
    /// </summary>
    public struct SpawnPlacement
    {
        /// <summary>튀어나오는 자리. 방 안쪽 벽 앞이다(아래 주석 참고).</summary>
        public Vector3 spawnPoint;

        /// <summary>걸어 들어가 자리 잡는 지점.</summary>
        public Vector3 entryPoint;

        /// <summary>도착한 뒤 AI가 깨어나기까지 서 있는 시간.</summary>
        public float holdSeconds;

        /// <summary>웨이브 시작 기준 등장 시각.</summary>
        public float appearAt;

        /// <summary>+1이면 오른쪽 벽, -1이면 왼쪽 벽.</summary>
        public int sideSign;
    }

    /// <summary>
    /// 역할에 맞는 등장 자리를 계산하는 <b>순수 함수</b>. 씬도 시간도 모른다 —
    /// 배치 의도가 지켜지는지는 눈으로 보기 어렵고(적이 여섯이면 이미 못 센다)
    /// 좌표 규칙이 곧 레벨 디자인이라, 테스트가 직접 부를 수 있는 자리에 둔다.
    ///
    /// <b>세 가지 규칙이 전부다.</b>
    /// <list type="number">
    /// <item><b>전사</b>는 벽 앞에서 걸어 들어와 전방을 채운다.</item>
    /// <item><b>돌진전사</b>는 플레이어와 같은 깊이(Z) 줄에 선다 — 돌진이 X축 직선이므로
    /// 그래야 깊이 이동으로 피하는 상황이 만들어진다. 대신 도착 후
    /// <see cref="ChargerHold"/>초를 서 있는다: 화면 밖에서 바로 꿰뚫고 들어오면 불합리하다.</item>
    /// <item><b>마법사</b>는 맵 최상단·최하단에 붙고, 플레이어와 같은 줄이면 반대쪽으로 넘긴다 —
    /// 같은 줄에 서면 걸어가는 김에 잡히고, 축을 옮겨 잡으러 가는 동선이 생기지 않는다.</item>
    /// </list>
    /// </summary>
    public static class WaveSpawnPlanner
    {
        // ── 방 규격 (SceneLayoutBuilder와 같은 값) ──

        /// <summary>방 좌우 반경. 벽 콜라이더가 여기 서 있다.</summary>
        public const float RoomHalfX = 6f;

        /// <summary>방 깊이 반경.</summary>
        public const float RoomHalfZ = 3f;

        /// <summary>
        /// 등장 지점이 벽에서 떨어지는 거리.
        ///
        /// <b>방 밖에서 소환하지 않는다.</b> 방은 사방이 콜라이더로 막혀 있어서 바깥에 놓으면
        /// 벽에 걸려 영영 못 들어온다. 벽 바로 앞에서 시작해 안쪽으로 걸어 들어오는 것으로
        /// "진입 모션"을 만든다 — 어차피 방 하나가 화면 하나라 벽 앞은 화면 가장자리다.
        /// </summary>
        public const float SpawnInset = 0.6f;

        /// <summary>몸통 반지름 여유. 깊이 좌표를 이 안쪽으로 물린다.</summary>
        public const float DepthInset = 0.6f;

        // ── 역할별 정착 지점 (벽에서 떨어지는 거리) ──

        private const float MeleeInset = 2.8f;
        private const float ChargerInset = 1.8f;
        private const float RangedInset = 1.2f;

        /// <summary>돌진전사가 도착 후 서 있는 시간. 기획 기준 1~1.5초.</summary>
        public const float ChargerHold = 1.2f;

        /// <summary>전사·마법사는 걸어 들어오는 것 자체가 예고라 따로 세우지 않는다.</summary>
        public const float DefaultHold = 0f;

        /// <summary>마법사가 플레이어와 같은 줄로 인정되는 깊이 차. 이보다 가까우면 반대쪽으로 넘긴다.</summary>
        public const float RangedLaneGap = 1.5f;

        /// <summary>전사가 벌려 서는 깊이 줄. 순서대로 돌려 쓴다.</summary>
        private static readonly float[] MeleeLanes = { 0f, 1.6f, -1.6f, 2.4f, -2.4f };

        /// <summary>돌진전사끼리 겹치지 않게 플레이어 줄에서 살짝 벌리는 값.</summary>
        private static readonly float[] ChargerLaneOffsets = { 0f, 1.2f, -1.2f };

        /// <summary>깊이 좌표의 절대 상한. 벽에 낀 채로 소환되지 않게.</summary>
        public static float MaxDepth => RoomHalfZ - DepthInset;

        /// <summary>
        /// 묶음의 <paramref name="index"/>번째가 나올 자리.
        /// </summary>
        /// <param name="spawn">저작한 한 줄.</param>
        /// <param name="index">묶음 안 순번. 좌우 분산과 깊이 줄이 여기서 갈린다.</param>
        /// <param name="playerZ">지금 플레이어의 깊이. 돌진전사와 마법사가 이 값을 기준으로 선다.</param>
        public static SpawnPlacement Plan(in WaveSpawn spawn, int index, float playerZ)
        {
            int side = SideSign(spawn.side, index);
            float z = DepthFor(spawn.role, index, playerZ);
            float entryX = side * (RoomHalfX - InsetFor(spawn.role));

            return new SpawnPlacement
            {
                spawnPoint = new Vector3(side * (RoomHalfX - SpawnInset), 0f, z),
                entryPoint = new Vector3(entryX, 0f, z),
                holdSeconds = spawn.role == EnemyRole.Charger ? ChargerHold : DefaultHold,
                appearAt = spawn.AppearAt(index),
                sideSign = side,
            };
        }

        /// <summary><see cref="SpawnSide.Both"/>는 짝수 오른쪽 · 홀수 왼쪽으로 번갈아 간다.</summary>
        public static int SideSign(SpawnSide side, int index)
        {
            switch (side)
            {
                case SpawnSide.Left:  return -1;
                case SpawnSide.Both:  return index % 2 == 0 ? 1 : -1;
                default:              return 1;
            }
        }

        /// <summary>
        /// 역할별 깊이 줄.
        ///
        /// 마법사만 <paramref name="playerZ"/>를 <b>피하는</b> 방향으로, 돌진전사는
        /// <b>맞추는</b> 방향으로 쓴다. 둘의 위협이 정반대라 규칙도 반대다.
        /// </summary>
        public static float DepthFor(EnemyRole role, int index, float playerZ)
        {
            switch (role)
            {
                case EnemyRole.Charger:
                {
                    float offset = ChargerLaneOffsets[Mathf.Abs(index) % ChargerLaneOffsets.Length];
                    return Clamp(playerZ + offset);
                }

                case EnemyRole.Ranged:
                {
                    bool topOk = Mathf.Abs(MaxDepth - playerZ) >= RangedLaneGap;
                    bool bottomOk = Mathf.Abs(-MaxDepth - playerZ) >= RangedLaneGap;

                    // 양쪽 구석이 다 열려 있으면 상·하로 갈라 세운다.
                    if (topOk && bottomOk) return index % 2 == 0 ? MaxDepth : -MaxDepth;

                    // 플레이어가 한쪽 구석에 박혀 있으면 전부 반대쪽으로. 방 깊이가 4.8이라
                    // 두 구석이 동시에 막히는 경우는 없다(RangedLaneGap의 두 배보다 넓다).
                    return topOk ? MaxDepth : -MaxDepth;
                }

                default:
                    return Clamp(MeleeLanes[Mathf.Abs(index) % MeleeLanes.Length]);
            }
        }

        /// <summary>깊이를 방 안으로 물린다. 벽에 낀 소환은 그대로 끼어 있는다.</summary>
        public static float Clamp(float z) => Mathf.Clamp(z, -MaxDepth, MaxDepth);

        private static float InsetFor(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Charger: return ChargerInset;
                case EnemyRole.Ranged:  return RangedInset;
                default:                return MeleeInset;
            }
        }
    }
}
