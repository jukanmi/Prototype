// 아레나 한 판의 기하 — 벽에서 나오는 배치와 그 벽의 목록.
// 진행은 StageDirector 가 조우 목록으로 돈다. 문(ArenaGate)은 각자 파일로 남는다.

using UnityEngine;

namespace Prototype
{
    // ══ ArenaSpawnPlanner ═══════════════════════════════════════════

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
        /// 저작한 한 줄이 어디서 어떻게 나올지. <b>한 줄이 한 기</b>다.
        ///
        /// 벽면 위 자리를 <see cref="WaveSpawnEntry.alongWall"/>에서 그대로 받는다 —
        /// 묶음이 사라지면서 "몇 번째 중 몇 기"를 셀 수가 없어졌기 때문이다.
        /// 대신 저작자가 벽 위 어디인지를 직접 고를 수 있게 됐다.
        /// </summary>
        public static SpawnPlacement PlanFromWall(in WaveSpawnEntry entry, float arenaMinX, float arenaMaxX)
            => Place(entry.wall, entry.AlongWall, entry.AppearAt, HoldFor(entry.role), arenaMinX, arenaMaxX);

        /// <summary>벽과 벽면 위 비율로 좌표를 푼다. 두 저작 단위가 공유하는 기하다.</summary>
        private static SpawnPlacement Place(SpawnWall wall, float t, float appearAt, float hold,
                                            float arenaMinX, float arenaMaxX)
        {
            Vector3 spawnPoint, entryPoint, telegraphPoint;
            float entryLine;

            switch (wall)
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

            return new SpawnPlacement
            {
                wall = wall,
                spawnPoint = spawnPoint,
                entryPoint = entryPoint,
                telegraphPoint = telegraphPoint,
                entryLine = entryLine,
                telegraphAt = Mathf.Max(0f, appearAt - TelegraphLead),
                appearAt = appearAt,
                holdSeconds = hold,
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

    // ══ SpawnWall ═══════════════════════════════════════════

    /// <summary>
    /// 적이 튀어나오는 벽. 아레나는 사방이 막혀 있으므로 방향은 넷뿐이다.
    ///
    /// <b>난이도는 마릿수가 아니라 방향으로 올린다.</b> 몹을 늘리면 그냥 오래 걸리지만,
    /// 방향을 늘리면 "재배치 개입이 의미가 있는가"라는 질문이 조우마다 강해진다.
    /// </summary>
    public enum SpawnWall
    {
        /// <summary>정면(깊이 +Z). 화면 위쪽 뒷벽이다.</summary>
        Front,

        /// <summary>왼쪽(-X).</summary>
        Left,

        /// <summary>오른쪽(+X).</summary>
        Right,

        /// <summary>후방(깊이 -Z). 화면 아래, 플레이어 등 뒤다.</summary>
        Back,

        /// <summary>
        /// <b>벽이 아니다.</b> 방 안에서 나오는 배치(<see cref="SpawnPlacement"/>)가 쓴다.
        ///
        /// 반드시 <b>맨 뒤</b>에 있어야 한다. 이 열거형은 애셋에 정수로 저장되므로
        /// 중간에 끼우면 이미 저작된 라운드의 벽이 통째로 밀린다.
        /// 저작 기본값은 여전히 <see cref="Front"/>(0)다.
        /// </summary>
        None,
    }
}
