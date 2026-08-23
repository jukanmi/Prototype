using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 벽에서 나오는 한 기의 계획. <see cref="SpawnPlacement"/>에 벽 연출용 정보를 더 얹는다.
    /// </summary>
    public struct ArenaSpawnPlan
    {
        /// <summary>어느 벽에서 나오는가. 예고 표식이 뜰 자리를 여기서 정한다.</summary>
        public SpawnWall wall;

        /// <summary>벽 <b>안쪽</b>(화면 밖) 시작 좌표. 여기서는 아직 벽에 가려 있다.</summary>
        public Vector3 spawnPoint;

        /// <summary>걸어 나와 멈출 목표 셀.</summary>
        public Vector3 entryPoint;

        /// <summary>예고 표식이 뜨는 자리. 벽면 위다.</summary>
        public Vector3 telegraphPoint;

        /// <summary>
        /// 이 선을 넘는 순간 정렬 순서를 앞으로 올린다 — 벽 뒤에 있다가 걸어 나오는 그림이 된다.
        /// 벽이 좌우면 X, 앞뒤면 Z 기준이다.
        /// </summary>
        public float entryLine;

        /// <summary>라운드 시작 기준, 예고가 뜨는 시각.</summary>
        public float telegraphAt;

        /// <summary>라운드 시작 기준, 실제로 몸이 나타나는 시각.</summary>
        public float spawnAt;

        /// <summary>목표 셀에 도착한 뒤 서 있는 시간.</summary>
        public float holdSeconds;
    }

    /// <summary>
    /// 벽 뒤 몬스터의 등장 계획. <b>순수 함수</b>다.
    ///
    /// <b>예고 → 진입 → 전투</b> 세 단계로 나눈다.
    /// <list type="number">
    /// <item><b>예고</b> — 스폰 <see cref="TelegraphLead"/>초 전에 해당 벽에 표식이 뜬다.
    /// 플레이어가 뒤를 잡히는 건 실력 부족이어야지 정보 부족이면 안 된다.</item>
    /// <item><b>진입</b> — 목표 셀까지 직선으로 걸어 나온다. 이 동안 <b>공격·피격 판정이 전부 꺼져 있다</b>
    /// (<see cref="EnemySpawnGuard"/>). 벽 안쪽에서 원거리 공격이 날아오거나, 플레이어가 벽을 향해
    /// 광역기를 써서 안 보이는 적을 잡아 버리는 상황을 막는다.</item>
    /// <item><b>전투</b> — 목표 셀에 닿으면 일반 AI가 몸을 가져간다.</item>
    /// </list>
    ///
    /// 한 묶음을 동시에 쏟지 않고 <see cref="StaggerStep"/>초씩 어긋나게 내보낸다.
    /// 동시 스폰은 몹이 한 점에 겹쳐 서로 밀어내고, 시각적으로도 갑자기 튀어나온 느낌이 된다.
    /// </summary>
    public static class ArenaSpawnPlanner
    {
        /// <summary>아레나 깊이 반경. 방 규격(<c>SceneLayoutBuilder</c>)과 같은 값이다.</summary>
        public const float ArenaHalfZ = WaveSpawnPlanner.RoomHalfZ;

        /// <summary>예고가 스폰보다 앞서는 시간.</summary>
        public const float TelegraphLead = 0.8f;

        /// <summary>한 기씩 어긋나는 간격. 기획 기준 0.2~0.3초.</summary>
        public const float StaggerStep = 0.25f;

        /// <summary>벽 안쪽으로 파묻히는 깊이. 여기서 시작해 걸어 나온다.</summary>
        public const float WallInset = 1.6f;

        /// <summary>목표 셀이 벽에서 떨어지는 거리. 벽에 붙어 서면 때릴 자리가 안 나온다.</summary>
        public const float EntryDepth = 2.2f;

        /// <summary>정렬 순서를 앞으로 올리는 선이 벽에서 떨어지는 거리.</summary>
        public const float EntryLineInset = 0.4f;

        /// <summary>몸통 반지름 여유. 벽면을 따라 벌려 설 때 양끝을 이만큼 물린다.</summary>
        public const float EdgeMargin = 1.2f;

        /// <summary>돌진전사가 목표 셀에서 서 있는 시간. 나오자마자 꿰뚫고 들어오면 불합리하다.</summary>
        public const float ChargerHold = WaveSpawnPlanner.ChargerHold;

        /// <summary>
        /// 보스가 등장하고 멈춰 서 있는 시간. 돌진전사보다 길다 —
        /// 등장 자체가 연출이고, 여기서 플레이어가 <b>거리를 다시 잡을</b> 시간이 나온다.
        /// </summary>
        public const float BossHold = 1.8f;

        /// <summary>
        /// 묶음의 <paramref name="index"/>번째가 어디서 어떻게 나올지.
        /// </summary>
        /// <param name="spawn">저작한 한 줄.</param>
        /// <param name="index">묶음 안 순번. 벽면을 따라 벌려 서는 자리와 시차가 여기서 갈린다.</param>
        /// <param name="arenaMinX">아레나 왼쪽 경계(월드).</param>
        /// <param name="arenaMaxX">아레나 오른쪽 경계(월드).</param>
        public static ArenaSpawnPlan Plan(in RoundSpawn spawn, int index, float arenaMinX, float arenaMaxX)
        {
            int count = spawn.Count;
            float spawnAt = Mathf.Max(0f, spawn.delay) + StaggerStep * Mathf.Max(0, index);

            // 벽면을 따라 균등하게 벌려 선다. (i+1)/(n+1) 이라 양끝에 붙지 않는다.
            float t = (index + 1f) / (count + 1f);

            Vector3 spawnPoint, entryPoint, telegraphPoint;
            float entryLine;

            switch (spawn.wall)
            {
                case SpawnWall.Left:
                {
                    float z = Along(-ArenaHalfZ, ArenaHalfZ, t);
                    spawnPoint = new Vector3(arenaMinX - WallInset, 0f, z);
                    entryPoint = new Vector3(arenaMinX + EntryDepth, 0f, z);
                    telegraphPoint = new Vector3(arenaMinX, 0f, z);
                    entryLine = arenaMinX + EntryLineInset;
                    break;
                }

                case SpawnWall.Right:
                {
                    float z = Along(-ArenaHalfZ, ArenaHalfZ, t);
                    spawnPoint = new Vector3(arenaMaxX + WallInset, 0f, z);
                    entryPoint = new Vector3(arenaMaxX - EntryDepth, 0f, z);
                    telegraphPoint = new Vector3(arenaMaxX, 0f, z);
                    entryLine = arenaMaxX - EntryLineInset;
                    break;
                }

                case SpawnWall.Back:
                {
                    float x = Along(arenaMinX, arenaMaxX, t);
                    spawnPoint = new Vector3(x, 0f, -ArenaHalfZ - WallInset);
                    entryPoint = new Vector3(x, 0f, -ArenaHalfZ + EntryDepth);
                    telegraphPoint = new Vector3(x, 0f, -ArenaHalfZ);
                    entryLine = -ArenaHalfZ + EntryLineInset;
                    break;
                }

                default:   // Front
                {
                    float x = Along(arenaMinX, arenaMaxX, t);
                    spawnPoint = new Vector3(x, 0f, ArenaHalfZ + WallInset);
                    entryPoint = new Vector3(x, 0f, ArenaHalfZ - EntryDepth);
                    telegraphPoint = new Vector3(x, 0f, ArenaHalfZ);
                    entryLine = ArenaHalfZ - EntryLineInset;
                    break;
                }
            }

            return new ArenaSpawnPlan
            {
                wall = spawn.wall,
                spawnPoint = spawnPoint,
                entryPoint = entryPoint,
                telegraphPoint = telegraphPoint,
                entryLine = entryLine,
                telegraphAt = Mathf.Max(0f, spawnAt - TelegraphLead),
                spawnAt = spawnAt,
                holdSeconds = HoldFor(spawn.role),
            };
        }

        /// <summary>목표 셀에 닿은 뒤 서 있는 시간. 덤비기 전에 읽을 틈을 주는 역할이다.</summary>
        public static float HoldFor(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Charger: return ChargerHold;
                case EnemyRole.Boss:    return BossHold;
                default:                return 0f;
            }
        }

        /// <summary>벽이 좌우면 X축, 앞뒤면 Z축을 따라 걸어 나온다.</summary>
        public static bool IsHorizontal(SpawnWall wall)
            => wall == SpawnWall.Left || wall == SpawnWall.Right;

        /// <summary>
        /// 진입선을 <b>넘었는가</b>. 벽마다 넘는 방향이 반대라 부호를 여기서 한 번에 정리한다 —
        /// 부르는 쪽에 흩어 두면 네 벽 중 하나가 반드시 반대로 들어간다.
        /// </summary>
        public static bool HasCrossedEntryLine(SpawnWall wall, Vector3 position, float entryLine)
        {
            switch (wall)
            {
                case SpawnWall.Left:  return position.x >= entryLine;
                case SpawnWall.Right: return position.x <= entryLine;
                case SpawnWall.Back:  return position.z >= entryLine;
                default:              return position.z <= entryLine;   // Front
            }
        }

        /// <summary>벽면을 따라 벌려 서는 좌표. 양끝은 몸통 반지름만큼 물린다.</summary>
        private static float Along(float min, float max, float t)
        {
            float lo = min + EdgeMargin;
            float hi = max - EdgeMargin;

            // 아레나가 여백 두 개보다 좁으면 가운데 한 줄로 세운다.
            if (lo >= hi) return (min + max) * 0.5f;

            return Mathf.Lerp(lo, hi, t);
        }
    }
}
