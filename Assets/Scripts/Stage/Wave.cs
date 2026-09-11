// 웨이브 한 세트 — 지형 · 어휘 · 진입 · 계획.
//   StageWaveCatalog  스테이지가 몇 개고 어디가 아레나인가
//   EnemyRole/SpawnSide  배치를 짤 때 고르는 축
//   SpawnEntry        소환된 적이 걸어 들어와 자리 잡는 연출
//   WaveSpawnPlanner  저작한 한 줄을 실제 스폰 좌표로 푸는 계산
// 하나를 고치면 나머지도 같이 봐야 해서 한 파일에 둔다.
//
// 웨이브 <b>내용</b>은 여기 없다. 애셋(WaveAsset)이 들고 씬 보드가 목록을 든다.

using UnityEngine;

namespace Prototype
{
    // ══ StageWaveCatalog ═══════════════════════════════════════════

    /// <summary>
    /// 스테이지의 <b>지형</b>. 몇 개인가, 어디가 아레나인가, 주제가 무엇인가.
    ///
    /// <b>웨이브 배치는 여기 없다.</b> 예전에는 이 표가 배치까지 들고 있었지만,
    /// 웨이브가 애셋(<see cref="WaveAsset"/>)으로 옮겨 가면서 그 몫이 빠졌다.
    /// 남은 것은 <c>GameManager</c> · <c>BattleSceneController</c>가 함께 읽는 번호 규약뿐이다.
    ///
    /// <b>2 · 5스테이지의 배치도 여기 없다.</b> 그 둘은 아레나와 통로로 이뤄진 스크롤 스테이지지만
    /// 도는 방식은 같다 — 자리가 붙은 조우일 뿐이고, 내용은 똑같이 <see cref="WaveAsset"/>에 있다.
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

        private static int Clamp(int stageNumber) => Mathf.Clamp(stageNumber, 1, StageCount);

    }

    // ══ EnemyRole · SpawnSide ═══════════════════════════════════════════

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
    /// 같은 역할 중 짝수 번째는 오른쪽, 홀수 번째는 왼쪽으로 갈라진다(양방향 포위 · 교차 돌진).
    /// </summary>
    public enum SpawnSide
    {
        Right,
        Left,
        Both,
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

        /// <summary>구석이 모자랄 때 마법사가 안쪽으로 물러나는 한 칸.</summary>
        public const float RangedLaneStep = 0.9f;

        /// <summary>전사가 벌려 서는 깊이 줄. 순서대로 돌려 쓴다.</summary>
        private static readonly float[] MeleeLanes = { 0f, 1.6f, -1.6f, 2.4f, -2.4f };

        /// <summary>돌진전사끼리 겹치지 않게 벌리는 간격.</summary>
        public const float ChargerLaneStep = 1.2f;

        /// <summary>한 웨이브에서 서로 다른 줄을 갖는 돌진전사 수. 넘으면 앞 줄을 다시 쓴다.</summary>
        public const int ChargerLanes = 3;

        /// <summary>깊이 좌표의 절대 상한. 벽에 낀 채로 소환되지 않게.</summary>
        public static float MaxDepth => RoomHalfZ - DepthInset;

        /// <summary>
        /// 저작한 한 줄의 <b>자동 배치</b>.
        ///
        /// 순번을 스스로 세지 않고 <paramref name="laneIndex"/>로 받는다. 한 줄이 한 기라
        /// 줄 자체는 자기가 몇 번째인지 모르기 때문이다 —
        /// <see cref="WaveLayout.AutoLaneIndices"/>가 웨이브 전체를 보고 <b>역할별 통산 번호</b>를
        /// 매겨 넘긴다. 그 번호가 왜 역할별이어야 하는지는 그쪽 주석에 적어 뒀다.
        /// </summary>
        /// <param name="entry">저작한 한 줄. <see cref="SpawnOrigin.Point"/>면 이 함수를 부르면 안 된다.</param>
        /// <param name="laneIndex">이 웨이브에서 같은 역할 중 몇 번째 자동 배치인가.</param>
        /// <param name="playerZ">지금 플레이어의 깊이.</param>
        public static SpawnPlacement PlanAuto(in WaveSpawnEntry entry, int laneIndex, float playerZ)
        {
            int side = SideSign(entry.side, laneIndex);
            float z = DepthFor(entry.role, laneIndex, playerZ);
            float entryX = side * (RoomHalfX - InsetFor(entry.role));

            return new SpawnPlacement
            {
                spawnPoint = new Vector3(side * (RoomHalfX - SpawnInset), 0f, z),
                entryPoint = new Vector3(entryX, 0f, z),
                holdSeconds = HoldFor(entry.role),
                appearAt = entry.AppearAt,
                telegraphAt = TelegraphAtFor(in entry),
                telegraphPoint = new Vector3(entryX, 0f, z),
                wall = SpawnWall.None,
            };
        }

        /// <summary>
        /// 씬에 찍어 둔 지점에서 나오는 줄. <b>걸어 들어오지 않는다</b> —
        /// 지점이 곧 설 자리라, 나타나는 자리와 정착 자리가 같다.
        ///
        /// 좌표는 방 안으로 물린다. 방은 사방이 콜라이더라 바깥에 찍힌 지점에서 소환하면
        /// 벽에 걸려 영영 못 들어오고, 증상은 "그 적이 안 나온다"로만 보인다.
        /// 지점이 애초에 방 밖인지는 <see cref="IsInsideRoom"/>으로 따로 물어 경고한다 —
        /// 조용히 당겨 놓기만 하면 저작자가 자기 실수를 영영 모른다.
        /// </summary>
        public static SpawnPlacement PlanAt(in WaveSpawnEntry entry, Vector3 point)
        {
            Vector3 ground = ClampIntoRoom(point);

            return new SpawnPlacement
            {
                spawnPoint = ground,
                entryPoint = ground,
                holdSeconds = HoldFor(entry.role),
                appearAt = entry.AppearAt,
                telegraphAt = TelegraphAtFor(in entry),
                telegraphPoint = ground,
                wall = SpawnWall.None,
            };
        }

        /// <summary>
        /// 이 줄의 예고가 뜨는 시각. 예고가 없는 등장이면 <b>음수</b>다.
        ///
        /// 판단이 배치 쪽에 있는 이유는 부르는 쪽이 둘이기 때문이다 —
        /// 디렉터가 따로 계산하면 방과 아레나가 서로 다른 예고 규칙을 갖게 된다.
        /// </summary>
        public static float TelegraphAtFor(in WaveSpawnEntry entry)
            => entry.motion == SpawnMotion.Burrow ? BurrowRules.TelegraphAt(entry.AppearAt) : -1f;

        /// <summary>도착 후 서 있는 시간. 돌진전사만 예고 시간을 갖는다.</summary>
        public static float HoldFor(EnemyRole role)
            => role == EnemyRole.Charger ? ChargerHold : DefaultHold;

        /// <summary>이 지점이 소환 가능한 방 안인가. 저작 검증이 읽는다.</summary>
        public static bool IsInsideRoom(Vector3 point)
            => Mathf.Abs(point.x) <= RoomHalfX - SpawnInset && Mathf.Abs(point.z) <= MaxDepth;

        /// <summary>방 밖 좌표를 소환 가능한 자리로 당긴다. 높이는 접지가 다시 잡으므로 0으로 누른다.</summary>
        public static Vector3 ClampIntoRoom(Vector3 point)
        {
            float limitX = RoomHalfX - SpawnInset;
            return new Vector3(Mathf.Clamp(point.x, -limitX, limitX), 0f, Clamp(point.z));
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
                    return ChargerDepth(index, playerZ);

                case EnemyRole.Ranged:
                    return RangedDepth(index, playerZ);

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

        /// <summary>
        /// 돌진전사의 깊이 줄. <b>0번은 반드시 플레이어와 같은 줄</b>이고, 나머지는 옆으로 벌린다.
        ///
        /// 벌린 줄이 방 밖으로 나가면 <b>건너뛰고 다음 줄을 찾는다.</b> 예전에는 그냥 벽으로
        /// 물렸는데(<c>Clamp</c>), 플레이어가 깊이 끝에 붙어 있으면 두 줄이 같은 벽 좌표로
        /// 접혀서 <b>돌진전사 둘이 같은 자리에 겹쳐 섰다</b>. 4-1(교차 돌진 3기)이 그 조합이다.
        ///
        /// 플레이어가 방 안에 있으면 예전과 같은 답을 낸다 — 0, +한 칸, −한 칸.
        /// </summary>
        public static float ChargerDepth(int index, float playerZ)
        {
            float lane = Clamp(playerZ);
            int wanted = Mathf.Abs(index) % ChargerLanes;
            int found = 0;

            // 0, +1, −1, +2, −2 … 순으로 훑어 방 안에 들어오는 줄만 센다.
            for (int rung = 0; rung < ChargerLanes * 4; rung++)
            {
                float candidate = lane + RungOffset(rung) * ChargerLaneStep;
                if (Mathf.Abs(candidate) > MaxDepth + 0.0001f) continue;

                if (found == wanted) return candidate;
                found++;
            }

            // 방이 이 값보다 좁아진 적은 없지만, 못 찾으면 플레이어 줄이 가장 안전하다.
            return lane;
        }

        /// <summary>0, +1, −1, +2, −2 … 사다리.</summary>
        private static int RungOffset(int rung) => (rung + 1) / 2 * (rung % 2 == 1 ? 1 : -1);

        /// <summary>
        /// 마법사의 깊이 줄. 위 구석 → 아래 구석 → 한 칸 안쪽 위 → 한 칸 안쪽 아래 … 순으로
        /// 훑으면서 <b>플레이어 줄에서 <see cref="RangedLaneGap"/>만큼 떨어진 자리만</b> 센다.
        ///
        /// 예전에는 구석 두 개만 번갈아 썼다. 마법사가 셋이면 0번과 2번이 같은 구석에 겹쳤고,
        /// 좌우도 같은 주기라 <b>같은 점에 두 기가 섰다</b>. 4-2(마법사 3기 탄막)가 그 조합이다.
        ///
        /// 플레이어 줄을 건너뛰는 규칙은 그대로다 — 같은 줄에 서면 전사와 싸우다
        /// 옆걸음질만 해도 닿아서, 축을 옮겨 잡으러 가는 동선이 아예 생기지 않는다.
        /// </summary>
        public static float RangedDepth(int index, float playerZ)
        {
            int wanted = Mathf.Abs(index);
            int found = 0;

            for (int rung = 0; rung < 12; rung++)
            {
                float sign = rung % 2 == 0 ? 1f : -1f;
                float candidate = sign * (MaxDepth - rung / 2 * RangedLaneStep);

                if (Mathf.Abs(candidate) > MaxDepth + 0.0001f) continue;
                if (Mathf.Abs(candidate - playerZ) < RangedLaneGap - 0.0001f) continue;

                if (found == wanted) return candidate;
                found++;
            }

            // 방 깊이가 RangedLaneGap 의 두 배보다 넓어 여기까지 오지 않는다. 와도 먼 구석이 낫다.
            return playerZ >= 0f ? -MaxDepth : MaxDepth;
        }
    }
}
