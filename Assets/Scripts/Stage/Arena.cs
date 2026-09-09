// 아레나 한 판의 구성 — 스폰 계획 · 라운드 · 라운드 표.
// 진행을 모는 ArenaDirector와 문(ArenaGate)은 프리팹에 물려 각자 파일로 남는다.

using System;
using UnityEngine;

namespace Prototype
{
    // ══ ArenaSpawnPlanner ═══════════════════════════════════════════

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

    // ══ ArenaRound ═══════════════════════════════════════════

    /// <summary>
    /// 적이 튀어나오는 벽. 아레나는 사방이 막혀 있으므로 방향은 넷뿐이다.
    ///
    /// <b>난이도는 마릿수가 아니라 방향으로 올린다.</b> 몹을 늘리면 그냥 오래 걸리지만,
    /// 방향을 늘리면 "재배치 개입이 의미가 있는가"라는 질문이 라운드마다 강해진다.
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
    }

    /// <summary>라운드 안의 한 묶음. "돌진전사 2기를 왼쪽 벽에서" 같은 한 줄이다.</summary>
    [Serializable]
    public struct RoundSpawn
    {
        public EnemyRole role;

        [Min(1)] public int count;

        public SpawnWall wall;

        [Tooltip("라운드가 시작되고 이 묶음의 예고가 뜨기까지의 시간.")]
        [Min(0f)] public float delay;

        [Tooltip("강화 개체.")]
        public bool elite;

        public int Count => Mathf.Max(1, count);

        public static RoundSpawn Of(EnemyRole role, int count, SpawnWall wall,
                                    float delay = 0f, bool elite = false)
            => new RoundSpawn { role = role, count = count, wall = wall, delay = delay, elite = elite };
    }

    /// <summary>
    /// 아레나 하나에서 도는 라운드.
    ///
    /// 사이클은 <b>진입 → 락 → 예고 → 스폰 → 전투 → 클리어 판정 → 언락</b>이다.
    /// (정비 구간은 아직 비어 있다 — 동료 소환·재배치가 들어갈 자리다.)
    /// </summary>
    [Serializable]
    public class ArenaRound
    {
        [Tooltip("로그에 찍히는 이름. 이 라운드로 무엇을 검증하는지 한 줄로 적어 둔다.")]
        public string label = "";

        public RoundSpawn[] spawns = new RoundSpawn[0];

        [Tooltip("동시에 공격을 시도할 수 있는 적의 수. 2~3을 권장한다.")]
        [Range(1, 6)] public int attackTokens = 2;

        /// <summary>이 라운드에 나오는 총 마릿수.</summary>
        public int TotalSpawnCount
        {
            get
            {
                int n = 0;
                if (spawns != null)
                    for (int i = 0; i < spawns.Length; i++) n += spawns[i].Count;
                return n;
            }
        }

        public int CountOf(EnemyRole role)
        {
            int n = 0;
            if (spawns != null)
                for (int i = 0; i < spawns.Length; i++)
                    if (spawns[i].role == role) n += spawns[i].Count;
            return n;
        }

        /// <summary>쓰이는 벽의 가짓수. 라운드가 진행될수록 늘어나야 한다.</summary>
        public int WallCount
        {
            get
            {
                if (spawns == null) return 0;

                int mask = 0;
                for (int i = 0; i < spawns.Length; i++) mask |= 1 << (int)spawns[i].wall;

                int n = 0;
                while (mask != 0) { n += mask & 1; mask >>= 1; }
                return n;
            }
        }

        public bool UsesWall(SpawnWall wall)
        {
            if (spawns == null) return false;
            for (int i = 0; i < spawns.Length; i++)
                if (spawns[i].wall == wall) return true;
            return false;
        }
    }

    // ══ ArenaRoundCatalog ═══════════════════════════════════════════

    /// <summary>
    /// 아레나 스테이지의 라운드 배치표. 스테이지마다 아레나가 여럿이라 <b>(스테이지, 아레나)</b>로 찾는다.
    ///
    /// <b>라운드마다 스폰 방향을 늘린다.</b> 마릿수를 늘리면 같은 싸움이 길어질 뿐이지만,
    /// 방향을 늘리면 "재배치 개입이 의미가 있는가"라는 검증 질문이 라운드가 진행될수록
    /// 강하게 압박받는다. 같은 난이도 상승에서 이쪽이 훨씬 재밌다.
    ///
    /// <list type="bullet">
    /// <item><b>2스테이지</b> — 1R 정면 하나 → 2R 좌우 양쪽. 시너지 붕괴 검증을 마지막에 몰아 둔다.</item>
    /// <item><b>5스테이지(보스)</b> — 미니 룸에서 몸을 풀고 통로를 지나 보스방에 들어간다.
    /// 구성은 기존 <c>Stage_Mini</c> · <c>Stage_Boss</c>를 그대로 옮긴 것이다.</item>
    /// </list>
    /// </summary>
    public static class ArenaRoundCatalog
    {
        public const int EarlyTokens = StageWaveCatalog.EarlyTokens;
        public const int LateTokens = StageWaveCatalog.LateTokens;

        /// <summary>이 스테이지에 아레나가 몇 개인가. 아레나 스테이지가 아니면 0.</summary>
        public static int ArenaCountOf(int stageNumber)
            => StageWaveCatalog.IsArenaStage(stageNumber) ? 2 : 0;

        /// <summary>아레나 번호(1부터). 범위 밖은 양끝으로 물린다.</summary>
        public static ArenaRound For(int stageNumber, int arenaNumber)
        {
            int arena = Clamp(arenaNumber);

            if (stageNumber == StageWaveCatalog.BossStageNumber)
                return arena == 1 ? BossWarmup() : BossRound();

            return arena == 1 ? Round1() : Round2();
        }

        /// <summary>아레나 주제. 로그가 읽는다.</summary>
        public static string ThemeOf(int stageNumber, int arenaNumber)
        {
            int arena = Clamp(arenaNumber);

            if (stageNumber == StageWaveCatalog.BossStageNumber)
                return arena == 1 ? "몸풀기 — 미니 룸" : "보스전";

            return arena == 1
                ? "돌진 대응 — 정면 한 방향"
                : "원거리 회피 & 시너지 붕괴 — 좌우 양방향";
        }

        private static int Clamp(int arenaNumber) => Mathf.Clamp(arenaNumber, 1, 2);

        // ── 2스테이지 1라운드: 정면 벽 하나 ──────────────
        // 방향이 하나뿐이라 "어디서 나오는가"를 배우는 데 온전히 집중할 수 있다.
        // 돌진전사가 정면에서 곧장 내려오므로 깊이(Z) 회피가 그대로 답이 된다.

        private static ArenaRound Round1() => new ArenaRound
        {
            label = "1R 정면 벽 — 전사 3 + 돌진전사 2",
            attackTokens = EarlyTokens,
            spawns = new[]
            {
                RoundSpawn.Of(EnemyRole.Melee,   3, SpawnWall.Front),
                RoundSpawn.Of(EnemyRole.Charger, 2, SpawnWall.Front, delay: 3.0f),
            },
        };

        // ── 2스테이지 2라운드: 좌우 양쪽 ─────────────────
        // 마법사를 좌우 벽에 하나씩 붙여 두면 어느 쪽을 보든 반대쪽에서 견제받는다.
        // 여기가 시너지 붕괴 검증 자리다 — 동료를 잃은 채로 이 구성을 상대할 수 있는가.

        private static ArenaRound Round2() => new ArenaRound
        {
            label = "2R 좌우 양방향 — 마법사 2 + 전사 3 + 돌진전사 1",
            attackTokens = LateTokens,
            spawns = new[]
            {
                RoundSpawn.Of(EnemyRole.Ranged,  1, SpawnWall.Left),
                RoundSpawn.Of(EnemyRole.Ranged,  1, SpawnWall.Right),
                RoundSpawn.Of(EnemyRole.Melee,   2, SpawnWall.Left,  delay: 1.2f),
                RoundSpawn.Of(EnemyRole.Melee,   1, SpawnWall.Right, delay: 1.2f),
                RoundSpawn.Of(EnemyRole.Charger, 1, SpawnWall.Right, delay: 4.0f),
            },
        };

        // ── 보스 스테이지 1라운드: 미니 룸 ───────────────
        // 기존 Stage_Mini 를 그대로 옮겼다 — 적 하나. 보스 앞에서 손을 푸는 자리이지
        // 소모전을 시키는 자리가 아니다. 여기서 체력을 깎아 두면 보스전이 운이 된다.

        private static ArenaRound BossWarmup() => new ArenaRound
        {
            label = "미니 룸 — 전사 1기",
            attackTokens = EarlyTokens,
            spawns = new[] { RoundSpawn.Of(EnemyRole.Melee, 1, SpawnWall.Front) },
        };

        // ── 보스 스테이지 2라운드: 보스방 ────────────────
        // 기존 Stage_Boss 그대로 — 보스 한 기뿐이다. 호위를 붙이지 않는 것은 의도다.
        // 보스는 패턴을 여럿 들고 있어서, 잡몹이 섞이면 어느 예고가 누구 것인지 안 읽힌다.

        private static ArenaRound BossRound() => new ArenaRound
        {
            label = "보스전 — 전사 보스 1기",
            attackTokens = EarlyTokens,
            spawns = new[] { RoundSpawn.Of(EnemyRole.Boss, 1, SpawnWall.Front, delay: 0.6f) },
        };
    }
}
