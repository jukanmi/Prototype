// 웨이브 한 세트 — 표 · 정의 · 항목 · 계획.
//   StageWaveCatalog  어떤 웨이브가 있는지
//   WaveDefinition    웨이브 하나의 정의
//   SpawnEntry        그 안의 스폰 한 줄
//   WaveSpawnPlanner  정의를 실제 스폰 좌표로 푸는 계산
// 하나를 고치면 나머지도 같이 봐야 해서 한 파일에 둔다.

using System;
using UnityEngine;

namespace Prototype
{
    // ══ StageWaveCatalog ═══════════════════════════════════════════

    /// <summary>
    /// <b>웨이브 스테이지</b>의 배치표. 방 하나에서 웨이브가 연달아 도는 방들이다.
    ///
    /// 애셋이 아니라 코드인 이유는 이 프로젝트의 다른 표(<c>EnemyPrefabBuilder.Variants</c>,
    /// <c>GameManager.DefaultStages</c>)와 같다 — 씬 · 애셋에 흩어 두면 "지금 3-2가 몇 기인가"를
    /// 다섯 군데를 열어 봐야 알 수 있고, 테스트가 표를 직접 읽을 수도 없다.
    /// 씬별로 손을 대고 싶으면 <see cref="StageDirector"/>의 인스펙터 배열이 이 표를 덮는다.
    ///
    /// <b>2 · 5스테이지는 여기 없다.</b> 그 둘은 아레나와 통로로 이뤄진 스크롤 스테이지라
    /// 웨이브가 아니라 라운드로 돌고, 구성은 <see cref="ArenaRoundCatalog"/>에 있다.
    ///
    /// <b>역할군 도입 순서가 곧 학습 순서다.</b>
    /// <list type="number">
    /// <item>1 입문 — 전사만. 변수를 빼고 타격감과 콤보에 집중시킨다.</item>
    /// <item>2 <i>(아레나)</i> — 돌진전사와 마법사가 벽에서 나오는 법을 여기서 배운다.</item>
    /// <item>3 역할군 조합 — 셋을 섞어 맵 전체를 쓰게 만든다.</item>
    /// <item>4 총력전 — 사각지대를 찾아 무적 판정을 쓰게 만든다.</item>
    /// <item>5 <i>(아레나)</i> 보스 — 미니 룸에서 손을 풀고 통로를 지나 보스방에 들어간다.</item>
    /// </list>
    /// </summary>
    public static class StageWaveCatalog
    {
        /// <summary>본편 스테이지 수. 아레나 스테이지를 포함한다.</summary>
        public const int StageCount = 5;

        /// <summary>아레나 · 통로로 도는 스테이지 번호. 이 둘만 웨이브가 아니다.</summary>
        public const int ArenaStageNumber = 2;

        /// <summary>보스 스테이지. 아레나 구조를 그대로 쓰되 마지막 방이 보스방이다.</summary>
        public const int BossStageNumber = 5;

        /// <summary>아레나와 통로로 도는 방인가.</summary>
        public static bool IsArenaStage(int stageNumber)
            => stageNumber == ArenaStageNumber || stageNumber == BossStageNumber;

        /// <summary>이 번호가 웨이브로 도는 방인가.</summary>
        public static bool IsWaveStage(int stageNumber)
            => stageNumber >= 1 && stageNumber <= StageCount && !IsArenaStage(stageNumber);

        /// <summary>전사만 나오는 앞 스테이지의 동시 공격 허용치.</summary>
        public const int EarlyTokens = 2;

        /// <summary>역할군이 섞이는 뒤 스테이지의 허용치. 3을 넘기면 회피가 성립하지 않는다.</summary>
        public const int LateTokens = 3;

        /// <summary>스테이지 주제. 로그와 디렉터가 읽는다.</summary>
        public static string ThemeOf(int stageNumber)
        {
            switch (Clamp(stageNumber))
            {
                case 1:  return "입문 — 기본 조작 & 공격 연계";
                case 2:  return "아레나 — 벽 뒤에서 오는 적";
                case 3:  return "역할군 조합 — 공간 제어";
                case 4:  return "총력전 — 숙련도 시험";
                default: return "보스 — 미니 룸 → 통로 → 보스방";
            }
        }

        /// <summary>
        /// 스테이지 하나의 웨이브 목록. <b>매번 새로 만든다</b> —
        /// <see cref="WaveDefinition"/>은 클래스라 한 벌을 돌려주면 디렉터가 인스펙터에서
        /// 만진 값이 표 원본을 오염시킨다(<c>GameManager.DefaultStages</c>와 같은 이유).
        ///
        /// 아레나 스테이지 번호를 주면 <b>빈 배열</b>이다. 그 방들은 웨이브로 돌지 않는다 —
        /// 조용히 다른 스테이지의 표를 돌려주면 아레나에 웨이브가 겹쳐 나온다.
        /// </summary>
        public static WaveDefinition[] For(int stageNumber)
        {
            int stage = Clamp(stageNumber);
            if (IsArenaStage(stage)) return new WaveDefinition[0];

            switch (stage)
            {
                case 1:  return Stage1();
                case 3:  return Stage3();
                default: return Stage4();
            }
        }

        private static int Clamp(int stageNumber) => Mathf.Clamp(stageNumber, 1, StageCount);

        // ── 1 입문 ──────────────────────────────────────
        // 돌진도 원거리도 없다. 콤보 · 잡기 · 기본 피격만 남기고 변수를 전부 뺀다.

        private static WaveDefinition[] Stage1() => new[]
        {
            new WaveDefinition
            {
                label = "1-1 전사 3기 순차 — 한 명씩 상대하는 법",
                attackTokens = EarlyTokens,
                spawns = new[] { WaveSpawn.Of(EnemyRole.Melee, 3, SpawnSide.Right, 0f, 1.6f) },
            },
            new WaveDefinition
            {
                label = "1-2 전사 4기 양방향 포위 — 등 뒤를 보게 만든다",
                attackTokens = EarlyTokens,
                spawns = new[] { WaveSpawn.Of(EnemyRole.Melee, 4, SpawnSide.Both, 0f, 0.7f) },
            },
            new WaveDefinition
            {
                label = "1-B 강화 전사 1기 + 전사 2기 — 첫 체력 벽",
                attackTokens = EarlyTokens,
                spawns = new[]
                {
                    WaveSpawn.Of(EnemyRole.Melee, 1, SpawnSide.Right, 0f,   0f,   elite: true),
                    WaveSpawn.Of(EnemyRole.Melee, 2, SpawnSide.Both,  1.2f, 0.6f),
                },
            },
        };

        // ── 3 역할군 조합 ────────────────────────────────
        // 전사가 길을 막고 마법사가 구석에서 깎을 때 돌진전사가 라인을 민다.
        // 여기서부터 토큰 3 — 셋이 동시에 압박해야 맵 전체를 쓰게 된다.

        private static WaveDefinition[] Stage3() => new[]
        {
            new WaveDefinition
            {
                label = "3-1 돌진전사 2기 + 마법사 1기 — 피할 곳이 이미 견제당한다",
                attackTokens = LateTokens,
                spawns = new[]
                {
                    WaveSpawn.Of(EnemyRole.Charger, 2, SpawnSide.Both, 1.0f, 1.2f),
                    WaveSpawn.Of(EnemyRole.Ranged,  1, SpawnSide.Right),
                },
            },
            new WaveDefinition
            {
                label = "3-2 전사 3기 + 돌진전사 1기 + 마법사 2기 — 세 위협 동시 대응",
                attackTokens = LateTokens,
                spawns = new[]
                {
                    WaveSpawn.Of(EnemyRole.Melee,   3, SpawnSide.Right, 0f,   0.7f),
                    WaveSpawn.Of(EnemyRole.Ranged,  2, SpawnSide.Both,  0.5f, 0.4f),
                    WaveSpawn.Of(EnemyRole.Charger, 1, SpawnSide.Left,  3.0f),
                },
            },
            new WaveDefinition
            {
                label = "3-3 전사 2기(전방) + 돌진 2기(기습) + 마법사 2기(후방)",
                attackTokens = LateTokens,
                spawns = new[]
                {
                    WaveSpawn.Of(EnemyRole.Melee,   2, SpawnSide.Right, 0f,   0.6f),
                    WaveSpawn.Of(EnemyRole.Ranged,  2, SpawnSide.Both,  0.5f, 0.4f),
                    // 기습은 반대쪽 벽에서 온다. 전사와 붙어 있는 등 뒤가 열린다.
                    WaveSpawn.Of(EnemyRole.Charger, 2, SpawnSide.Left,  3.5f, 1.4f),
                },
            },
        };

        // ── 4 총력전 ─────────────────────────────────────
        // 적의 공격 판정이 겹치지 않는 사각지대를 찾아 메가크래시 · 잡기 무적을 쓰게 만든다.
        // 최종 웨이브의 지속 리젠이 "다 잡고 쉬는" 구간을 없앤다.

        private static WaveDefinition[] Stage4() => new[]
        {
            new WaveDefinition
            {
                label = "4-1 돌진전사 3기 교차 돌진 — 좌우에서 번갈아 들어온다",
                attackTokens = LateTokens,
                spawns = new[] { WaveSpawn.Of(EnemyRole.Charger, 3, SpawnSide.Both, 0.5f, 1.1f) },
            },
            new WaveDefinition
            {
                label = "4-2 마법사 3기 탄막 + 전사 3기 — 안전지대가 사라진다",
                attackTokens = LateTokens,
                spawns = new[]
                {
                    WaveSpawn.Of(EnemyRole.Ranged, 3, SpawnSide.Both,  0f,   0.4f),
                    WaveSpawn.Of(EnemyRole.Melee,  3, SpawnSide.Right, 1.0f, 0.6f),
                },
            },
            new WaveDefinition
            {
                label = "4-3 엘리트 돌진 2기 + 마법사 2기 + 지속 리젠 전사",
                attackTokens = LateTokens,
                spawns = new[]
                {
                    WaveSpawn.Of(EnemyRole.Charger, 2, SpawnSide.Both, 1.0f, 1.6f, elite: true),
                    WaveSpawn.Of(EnemyRole.Ranged,  2, SpawnSide.Both, 0f,   0.4f),
                },
                // 리젠은 "빨리 끝내라"는 압박이다. 상한이 없으면 이길 수 없는 방이 된다.
                reinforceInterval = 7f,
                reinforceRole = EnemyRole.Melee,
                reinforceCap = 4,
            },
        };
    }

    // ══ WaveDefinition ═══════════════════════════════════════════

    /// <summary>
    /// 적의 <b>역할</b>. 종류가 아니라 역할이다 — 배치를 짤 때 우리가 실제로 고르는 축이고,
    /// <see cref="WaveSpawnPlanner"/>가 등장 자리를 이 값 하나로 정한다.
    ///
    /// <list type="bullet">
    /// <item><see cref="Melee"/> 전사 — 전방에서 이동과 타격을 받아내며 난전을 만든다.</item>
    /// <item><see cref="Charger"/> 돌진전사 — X축 직선 돌진으로 콤보를 끊고 깊이(Z) 회피를 강제한다.</item>
    /// <item><see cref="Ranged"/> 마법사 — 상하단 구석에서 투사체로 안전지대를 깎는다.</item>
    /// <item><see cref="Boss"/> 보스 — 패턴을 여럿 든 단일 개체. 방 하나를 통째로 차지한다.</item>
    /// </list>
    /// </summary>
    public enum EnemyRole
    {
        Melee,
        Charger,
        Ranged,

        /// <summary>
        /// 보스. 웨이브 표에는 쓰지 않는다 — 잡몹 자리에 섞어 놓으면 동시 등장 규칙이
        /// 그대로 적용되어 보스가 둘씩 나오는 표를 실수로 만들 수 있다.
        /// 아레나 라운드에서 한 기만 쓴다.
        /// </summary>
        Boss,
    }

    /// <summary>
    /// 어느 쪽 벽에서 들어오는가. <see cref="Both"/>는 <b>번갈아</b>다 —
    /// 같은 묶음의 짝수 번째는 오른쪽, 홀수 번째는 왼쪽으로 갈라진다(양방향 포위 · 교차 돌진).
    /// </summary>
    public enum SpawnSide
    {
        Right,
        Left,
        Both,
    }

    /// <summary>
    /// 웨이브 안의 한 묶음. "돌진전사 2기를 1.5초 시차로 왼쪽에서" 같은 한 줄이다.
    /// </summary>
    [Serializable]
    public struct WaveSpawn
    {
        public EnemyRole role;

        [Tooltip("이 묶음의 마릿수.")]
        [Min(1)] public int count;

        public SpawnSide side;

        [Tooltip("웨이브가 시작되고 첫 기가 나타나기까지의 시간.")]
        [Min(0f)] public float delay;

        [Tooltip("한 기씩 벌리는 간격. 0이면 전부 동시에 나온다.")]
        [Min(0f)] public float interval;

        [Tooltip("강화 개체. 체력·공격력이 배로 오르고 이름에 (강화)가 붙는다.")]
        public bool elite;

        /// <summary>0이나 음수로 저작된 마릿수는 1로 본다 — 아무도 안 나오는 묶음은 실수다.</summary>
        public int Count => Mathf.Max(1, count);

        /// <summary>이 묶음의 <paramref name="index"/>번째가 나타나는 시각(웨이브 시작 기준).</summary>
        public float AppearAt(int index) => Mathf.Max(0f, delay) + Mathf.Max(0f, interval) * Mathf.Max(0, index);

        /// <summary>마지막 한 기가 나타나는 시각. 디렉터가 "다 나왔는가"를 여기서 안다.</summary>
        public float LastAppearAt => AppearAt(Count - 1);

        public static WaveSpawn Of(EnemyRole role, int count,
                                   SpawnSide side = SpawnSide.Right,
                                   float delay = 0f, float interval = 0f, bool elite = false)
            => new WaveSpawn
            {
                role = role,
                count = count,
                side = side,
                delay = delay,
                interval = interval,
                elite = elite,
            };
    }

    /// <summary>
    /// 웨이브 하나. <b>전멸시켜야 다음으로 넘어간다</b> — 판정은 <see cref="StageDirector"/>가 한다.
    ///
    /// <see cref="attackTokens"/>가 이 프로젝트의 다구리 방지책이다. 한 화면에 적이 여섯이어도
    /// 동시에 공격을 <b>시도</b>할 수 있는 적은 이 수까지고, 나머지는 사거리 안에서 기다린다.
    /// 토큰이 없으면 6기가 동시에 휘둘러 회피가 성립하지 않는 구간이 생긴다.
    /// </summary>
    [Serializable]
    public class WaveDefinition
    {
        [Tooltip("로그에 찍히는 이름. 배치 의도를 한 줄로 적어 둔다.")]
        public string label = "";

        public WaveSpawn[] spawns = new WaveSpawn[0];

        [Tooltip("동시에 공격을 시도할 수 있는 적의 수. 2~3을 권장한다.")]
        [Range(1, 6)] public int attackTokens = 2;

        [Header("지속 리젠 (최종 웨이브용)")]
        [Tooltip("0보다 크면 이 간격마다 증원이 한 기씩 들어온다.")]
        [Min(0f)] public float reinforceInterval;

        public EnemyRole reinforceRole = EnemyRole.Melee;

        [Tooltip("증원 총량 상한. 0이면 증원하지 않는다.")]
        [Min(0)] public int reinforceCap;

        /// <summary>웨이브 시작 시 예약되는 총 마릿수. 증원은 세지 않는다 — 그건 나중에 결정된다.</summary>
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

        /// <summary>이 웨이브에 지속 리젠이 붙어 있는가.</summary>
        public bool HasReinforcements => reinforceInterval > 0f && reinforceCap > 0;

        /// <summary>역할별 마릿수. 배치 검사가 표를 그대로 읽을 수 있게 연다.</summary>
        public int CountOf(EnemyRole role)
        {
            int n = 0;
            if (spawns != null)
                for (int i = 0; i < spawns.Length; i++)
                    if (spawns[i].role == role) n += spawns[i].Count;
            return n;
        }
    }

    // ══ SpawnEntry ═══════════════════════════════════════════

    /// <summary>
    /// 소환된 적의 <b>진입 연출</b>. 걸어 들어가 자리를 잡고, 잠시 서 있다가 AI에 몸을 넘긴다.
    ///
    /// 이게 없으면 돌진전사가 화면 가장자리에서 곧장 꿰뚫고 들어와 <b>불합리하게 느껴진다</b> —
    /// 반응할 시간이 아니라 반응할 <i>정보</i>가 없기 때문이다. 걸어 들어오는 모습을 보여 주고
    /// 1초 남짓 세워 두면, 유저가 "저기서 온다"를 읽고 축을 옮길 수 있다.
    ///
    /// 순수 C#이다. <see cref="EnemyControl"/>이 이걸 들고 매 프레임 방향을 받아
    /// <c>Command.Move</c>로 옮기므로, 걷는 애니메이션도 벽 충돌도 평소 이동과 같은 경로를 탄다.
    /// </summary>
    public sealed class SpawnEntry
    {
        public enum Phase
        {
            /// <summary>정착 지점으로 걸어가는 중.</summary>
            Walking,

            /// <summary>도착. 선딜레이를 세는 중 — 여기서 유저에게 읽을 시간을 준다.</summary>
            Holding,

            /// <summary>끝. AI가 몸을 가져간다.</summary>
            Done,
        }

        /// <summary>도착으로 치는 거리. 몸통 반지름(0.5)보다 작으면 벽·다른 적에 밀려 영영 못 닿는다.</summary>
        public const float DefaultArriveRadius = 0.6f;

        /// <summary>
        /// 걷기 제한 시간. 이걸 넘기면 도착한 것으로 친다.
        ///
        /// 없으면 안 된다 — 정착 지점에 먼저 온 적이 서 있거나 벽 모서리에 끼면
        /// 그 적은 <b>영원히 AI가 안 깨어난다</b>. 화면에는 멀쩡히 서 있는데 아무것도 안 하고,
        /// 스테이지는 그 적이 안 죽어서 안 끝난다.
        /// </summary>
        public const float DefaultWalkTimeout = 4f;

        private readonly Vector3 target;
        private readonly float holdSeconds;
        private readonly float arriveRadiusSqr;
        private readonly float walkTimeout;

        private float walked;
        private float holdLeft;

        public Phase Current { get; private set; } = Phase.Walking;

        /// <summary>아직 AI에 몸을 안 넘겼는가.</summary>
        public bool IsActive => Current != Phase.Done;

        /// <summary>걸어가는 목표 지점(지상 좌표).</summary>
        public Vector3 Target => target;

        /// <summary>남은 선딜레이. 0이면 이번 프레임에 깨어난다.</summary>
        public float HoldRemaining => holdLeft;

        public SpawnEntry(Vector3 target, float holdSeconds,
                          float arriveRadius = DefaultArriveRadius,
                          float walkTimeout = DefaultWalkTimeout)
        {
            this.target = new Vector3(target.x, 0f, target.z);
            this.holdSeconds = Mathf.Max(0f, holdSeconds);
            arriveRadiusSqr = Mathf.Max(0.01f, arriveRadius) * Mathf.Max(0.01f, arriveRadius);
            this.walkTimeout = Mathf.Max(0.1f, walkTimeout);
        }

        /// <summary>
        /// 한 프레임 진행하고 <b>이번 프레임의 이동 방향</b>을 돌려준다.
        /// 서 있어야 하는 구간과 끝난 뒤에는 <see cref="Vector3.zero"/>다.
        /// </summary>
        /// <param name="position">지금 위치. 높이는 무시한다.</param>
        public Vector3 Tick(Vector3 position, float dt)
        {
            if (Current == Phase.Done) return Vector3.zero;

            if (Current == Phase.Holding)
            {
                holdLeft -= dt;
                if (holdLeft <= 0f) Current = Phase.Done;
                return Vector3.zero;
            }

            Vector3 to = target - position;
            to.y = 0f;

            walked += dt;

            if (to.sqrMagnitude <= arriveRadiusSqr || walked >= walkTimeout)
            {
                Arrive();
                return Vector3.zero;
            }

            return to.normalized;
        }

        /// <summary>연출을 지금 끝낸다. 맞았거나 스테이지가 끝났을 때 부른다.</summary>
        public void Finish() => Current = Phase.Done;

        private void Arrive()
        {
            if (holdSeconds <= 0f)
            {
                Current = Phase.Done;
                return;
            }

            Current = Phase.Holding;
            holdLeft = holdSeconds;
        }
    }

    // ══ WaveSpawnPlanner ═══════════════════════════════════════════

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
