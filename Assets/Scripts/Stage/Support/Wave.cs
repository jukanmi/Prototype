// 웨이브 한 세트 — 지형 · 어휘 · 진입 · 계획.
//   StageWaveCatalog  스테이지가 몇 개고 어디가 아레나인가
//   EnemyRole/SpawnSide  배치를 짤 때 고르는 축
//   SpawnEntry        소환된 적이 걸어 들어와 자리 잡는 연출
//   RoomRect/RoomRules  방 크기와 그 비율 규칙 — 스폰 계산이 받는 방
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

    // ══ RoomRect · RoomRules ═══════════════════════════════════════════

    /// <summary>
    /// 적을 세울 수 있는 구역. XZ 평면의 사각형이고 높이는 모른다.
    ///
    /// <b>반경 둘이 아니라 네 변으로 든다.</b> 아레나가 이미 <c>minX</c>/<c>maxX</c>로 저작하고,
    /// 원점 중심을 가정한 <c>Mathf.Abs(x) &lt;= 반경</c> 비교는 방이 옮겨지는 순간 전부 틀린다.
    ///
    /// <see cref="GroundRect"/>와 다르다. 저쪽은 <b>밟는 면</b>이고 이쪽은 <b>소환 가능 구역</b>이다 —
    /// 발판이 여러 장인 방에서 둘이 같을 이유가 없다.
    ///
    /// 인스펙터에서 뒤집어 적어도 읽을 때 정렬한다(<see cref="EncounterSite.MinX"/>와 같은 규약).
    /// <c>default</c>는 크기 0인 방이라 쓰면 안 된다 — 기준 방은 <see cref="Default"/>다.
    ///
    /// 계획서: docs/Room_Size_Plan.md (2.3 · 1단계)
    /// </summary>
    [System.Serializable]
    public struct RoomRect
    {
        [SerializeField] private float minX;
        [SerializeField] private float maxX;
        [SerializeField] private float minZ;
        [SerializeField] private float maxZ;

        public RoomRect(float minX, float maxX, float minZ, float maxZ)
        {
            this.minX = Mathf.Min(minX, maxX);
            this.maxX = Mathf.Max(minX, maxX);
            this.minZ = Mathf.Min(minZ, maxZ);
            this.maxZ = Mathf.Max(minZ, maxZ);
        }

        /// <summary>
        /// 기준 방 — 100%일 때의 웨이브 방. 씬의 벽(반폭 6 · 반깊이 3)이 이 값으로 서 있다.
        /// 방 크기의 숫자가 코드에 남는 곳은 여기 한 군데다.
        /// </summary>
        public static readonly RoomRect Default = new RoomRect(-6f, 6f, -3f, 3f);

        public float MinX => Mathf.Min(minX, maxX);
        public float MaxX => Mathf.Max(minX, maxX);
        public float MinZ => Mathf.Min(minZ, maxZ);
        public float MaxZ => Mathf.Max(minZ, maxZ);

        public float CenterX => (minX + maxX) * 0.5f;
        public float CenterZ => (minZ + maxZ) * 0.5f;

        public float HalfX => (MaxX - MinX) * 0.5f;
        public float HalfZ => (MaxZ - MinZ) * 0.5f;

        public override string ToString()
            => $"x[{MinX:0.##}..{MaxX:0.##}] z[{MinZ:0.##}..{MaxZ:0.##}]";
    }

    /// <summary>
    /// 방의 네 벽. <b>이름이 앞/뒤가 아니라 가까움/멂이다.</b>
    ///
    /// 씬의 <c>Wall_Front</c>는 z = −3.25(화면 아래)에 서 있는데 <see cref="SpawnWall.Front"/>는 +Z(화면 위)다.
    /// 둘 중 하나를 따라 이름을 지으면 나머지 하나와 반드시 반대가 된다. 카메라에서의 거리로 부르면 헷갈릴 게 없다.
    /// </summary>
    public enum RoomSide
    {
        /// <summary>−X.</summary>
        Left,

        /// <summary>+X.</summary>
        Right,

        /// <summary>−Z. 카메라에 가까운 쪽, 화면 아래.</summary>
        Near,

        /// <summary>+Z. 카메라에서 먼 쪽, 화면 위 뒷벽.</summary>
        Far,
    }

    /// <summary>벽 하나가 설 자리. 높이는 씬이 정하므로 중심의 y는 0이다.</summary>
    public readonly struct WallPose
    {
        /// <summary>바닥 위 벽 중심. y는 0 — 부르는 쪽이 원래 높이를 유지한다.</summary>
        public readonly Vector3 Center;

        /// <summary>벽이 뻗는 길이.</summary>
        public readonly float Length;

        /// <summary>벽 두께. 방 안쪽 면이 방 변에 오도록 중심이 반 두께만큼 밖에 있다.</summary>
        public readonly float Thickness;

        /// <summary>X축으로 뻗는 벽인가(가까운 벽 · 먼 벽).</summary>
        public readonly bool AlongX;

        public WallPose(Vector3 center, float length, float thickness, bool alongX)
        {
            Center = center;
            Length = length;
            Thickness = thickness;
            AlongX = alongX;
        }

        /// <summary><c>BoxCollider.size</c>에 그대로 넣을 값.</summary>
        public Vector3 ColliderSize(float height)
            => AlongX ? new Vector3(Length, height, Thickness) : new Vector3(Thickness, height, Length);
    }

    /// <summary>
    /// 방 크기 비율의 규칙. <b>순수 함수</b>라 씬 없이 테스트한다.
    ///
    /// 비율은 <b>정수 백분율</b>이고 0은 "보정 없음"이다 — <see cref="EncounterModifier.None"/>이
    /// <c>default</c>라서 새 칸의 기본값이 0이고, 레시피 애셋에 필드가 없을 때도 0으로 읽힌다.
    ///
    /// 계획서: docs/Room_Size_Plan.md (2.3 · 2.5 · 3항)
    /// </summary>
    public static class RoomRules
    {
        /// <summary>보정 없음. 저작한 크기 그대로.</summary>
        public const int FullPercent = 100;

        /// <summary>
        /// 하한. <b>스폰 계산이 아니라 적 AI 수치가 정한다</b> — 마법사 후퇴 거리(4)가
        /// 등장 순간 기본 배치부터 걸리지 않는 가장 작은 방이다(계획서 2.5). 애셋과의 결속은 2단계 테스트가 본다.
        /// </summary>
        public const int MinPercent = 80;

        /// <summary>상한. 뒷벽 윗부분이 고정 카메라 화면 안에 남는 가장 큰 방이다(계획서 2.5).</summary>
        public const int MaxPercent = 125;

        /// <summary>굴림 단위. 97%와 100%는 눈으로 구분이 안 되고 로그만 지저분해진다.</summary>
        public const int Step = 5;

        /// <summary>
        /// 고정 카메라(직교 5 · 16:9)가 옆벽을 화면 안에 담는 최대 반폭. 화면 반폭 8.9에서 벽 두께를 뺐다.
        /// <b>계산 추정이다</b> — 씬에서 확인하고 조인다.
        /// </summary>
        public const float MaxHalfX = 8.4f;

        /// <summary>
        /// 뒷벽(높이 4)이 잘리지 않는 최대 반깊이. 기울기 50° · lift 0.5에서
        /// <c>0.766·z + 0.643·4 − 0.5 ≤ 5</c>. <b>계산 추정이다.</b>
        /// </summary>
        public const float MaxHalfZ = 3.8f;

        private const float Eps = 0.0001f;

        /// <summary>0 이하는 보정 없음(100)으로 읽는다.</summary>
        public static int Normalize(int percent) => percent <= 0 ? FullPercent : percent;

        /// <summary>
        /// 저작 가능한 값인가. 0(보정 없음)이거나, 범위 안의 <see cref="Step"/> 배수.
        /// 레시피 검사가 부른다 — 조용히 물리지 않고 저작자에게 알린다.
        /// </summary>
        public static bool IsValidPercent(int percent)
        {
            if (percent == 0) return true;
            return percent >= MinPercent && percent <= MaxPercent && percent % Step == 0;
        }

        /// <summary>
        /// 런타임에서 쓸 수 있는 값으로 맞춘다. 0은 100, 나머지는 가까운 단위로 반올림한 뒤 범위로 물린다.
        /// 저작 실수를 감추는 용도가 아니다 — 부르는 쪽이 원래 값과 다르면 경고한다.
        /// </summary>
        public static int ClampPercent(int percent)
        {
            int p = Mathf.RoundToInt(Normalize(percent) / (float)Step) * Step;
            return Mathf.Clamp(p, MinPercent, MaxPercent);
        }

        /// <summary>
        /// 중심을 그대로 두고 반폭 · 반깊이에 같은 비율을 건다. 0은 100이다.
        /// 축마다 따로 두지 않는 이유는 계획서 2.3 — 한 비율로 두 축의 한계가 다 들어온다.
        /// </summary>
        public static RoomRect Scale(in RoomRect room, int percent)
        {
            float k = Normalize(percent) / (float)FullPercent;

            float hx = room.HalfX * k;
            float hz = room.HalfZ * k;

            return new RoomRect(room.CenterX - hx, room.CenterX + hx, room.CenterZ - hz, room.CenterZ + hz);
        }

        /// <summary>
        /// <paramref name="from"/> 방 안의 자리를 <paramref name="to"/> 방의 같은 <b>상대 위치</b>로 옮긴다. 높이는 그대로.
        ///
        /// 씬 스폰 지점 · 파티 시작 자리가 쓴다 — "방 안 배치"는 방 비율을 따라간다(계획서 3항 ②).
        /// 크기 0인 방에서 옮기면 상대 위치가 없으므로 새 방의 중심으로 보낸다.
        /// </summary>
        public static Vector3 MovePoint(Vector3 point, in RoomRect from, in RoomRect to)
        {
            float x = from.HalfX > Eps
                ? to.CenterX + (point.x - from.CenterX) / from.HalfX * to.HalfX
                : to.CenterX;

            float z = from.HalfZ > Eps
                ? to.CenterZ + (point.z - from.CenterZ) / from.HalfZ * to.HalfZ
                : to.CenterZ;

            return new Vector3(x, point.y, z);
        }

        /// <summary>
        /// 벽 하나가 설 자리. <b>안쪽 면이 방 변에 온다</b> — 중심은 반 두께만큼 밖이다.
        ///
        /// 길이는 지금 씬 규격을 그대로 따른다. 좌우 벽은 방 깊이만큼, 가까운 · 먼 벽은
        /// 모서리를 덮도록 방 폭 + 두께 둘(Stage_01: 옆벽 6, 앞뒤 벽 13).
        /// </summary>
        public static WallPose WallFor(RoomSide side, in RoomRect room, float thickness)
        {
            float t = Mathf.Max(0f, thickness);
            float half = t * 0.5f;

            switch (side)
            {
                case RoomSide.Left:
                    return new WallPose(new Vector3(room.MinX - half, 0f, room.CenterZ), room.HalfZ * 2f, t, alongX: false);

                case RoomSide.Right:
                    return new WallPose(new Vector3(room.MaxX + half, 0f, room.CenterZ), room.HalfZ * 2f, t, alongX: false);

                case RoomSide.Near:
                    return new WallPose(new Vector3(room.CenterX, 0f, room.MinZ - half), room.HalfX * 2f + t * 2f, t, alongX: true);

                default:   // Far
                    return new WallPose(new Vector3(room.CenterX, 0f, room.MaxZ + half), room.HalfX * 2f + t * 2f, t, alongX: true);
            }
        }

        /// <summary>
        /// 방을 따라 늘고 주는 그림(바닥 · 뒷벽 그림)의 새 <c>localScale</c>.
        /// <b>바닥과 나란한 축만</b> 비율을 받고, 높이 방향 축은 그대로 둔다.
        ///
        /// 축마다 월드에서 어느 쪽을 향하는지 보고 가른다 — 눕힌 바닥(로컬 x · y가 월드 x · z)은 두 축이 다 줄고,
        /// 세운 뒷벽 그림(로컬 y가 월드 y)은 폭만 준다. 이름이나 종류로 가르면 새 그림을 넣을 때마다 분기가 는다.
        /// 기울어진 축은 바닥에 비친 길이만큼만 비율을 받는다. X와 Z에 같은 비율을 거므로 어느 수평 방향이든 같다.
        /// </summary>
        /// <param name="rotation">그림의 월드 회전.</param>
        public static Vector3 StretchScale(Vector3 localScale, Quaternion rotation, int percent)
        {
            float k = Normalize(percent) / (float)FullPercent;

            return new Vector3(
                localScale.x * AxisFactor(rotation * Vector3.right, k),
                localScale.y * AxisFactor(rotation * Vector3.up, k),
                localScale.z * AxisFactor(rotation * Vector3.forward, k));
        }

        /// <summary>월드 방향이 바닥에 비친 길이(0~1)만큼 1에서 <paramref name="k"/>로 옮긴 배율.</summary>
        private static float AxisFactor(Vector3 worldAxis, float k)
        {
            float flat = Mathf.Clamp01(new Vector2(worldAxis.x, worldAxis.z).magnitude);
            return Mathf.Lerp(1f, k, flat);
        }

        /// <summary>
        /// 스폰 규칙이 버티는 크기인가. 마법사 깊이 줄이 플레이어 줄을 피할 자리가 남아야 한다 —
        /// 안 남으면 <see cref="WaveSpawnPlanner.RangedDepth"/>의 탐색이 전부 실패하고 폴백으로 떨어진다.
        ///
        /// 마법사 후퇴 거리 조건(계획서 2.5 첫 줄)은 여기 없다. 정착 거리가 비율로 바뀌는 2단계에서
        /// 적 애셋을 읽는 테스트가 <see cref="MinPercent"/>와 묶는다.
        /// </summary>
        public static bool IsLargeEnough(in RoomRect room)
            => room.HalfZ - WaveSpawnPlanner.DepthInset >= WaveSpawnPlanner.RangedLaneGap - Eps;

        /// <summary>고정 카메라 화면 안에 네 벽이 다 담기는가.</summary>
        public static bool FitsCamera(in RoomRect room)
            => room.HalfX <= MaxHalfX + Eps && room.HalfZ <= MaxHalfZ + Eps;

        /// <summary>
        /// 이 방을 실제로 쓸 수 있는가 — 충분히 크고 화면 안이다.
        /// 기준 방이 <see cref="RoomRect.Default"/>와 다른 씬에서 <c>기준 × 범위</c>를 다시 볼 때 쓴다(계획서 8단계).
        /// </summary>
        public static bool Fits(in RoomRect room) => IsLargeEnough(room) && FitsCamera(room);
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
    ///
    /// <b>방은 인자로 받는다.</b> 지도 칸이 방 크기 비율을 굴리므로 방이 상수일 수 없다.
    /// 방이 바뀔 때 거리는 뜻에 따라 셋으로 갈린다(계획서 docs/Room_Size_Plan.md 3항).
    /// <list type="bullet">
    /// <item>① <b>벽에 붙는 자리</b> — 등장 지점(<see cref="SpawnInset"/>). 벽을 따라가고 거리는 고정.</item>
    /// <item>② <b>방 안 배치</b> — 정착 지점. 반폭의 <b>비율</b>이라 방과 같이 줄고 는다.</item>
    /// <item>③ <b>몸 · 사거리</b> — 깊이 줄 간격, 마법사 줄 회피, 몸통 여유. 고정.</item>
    /// </list>
    /// 기본값 오버로드는 두지 않는다. 방을 빠뜨린 호출이 조용히 기준 방으로 돌면 그 경로만 옛 방에 적이 선다.
    /// </summary>
    public static class WaveSpawnPlanner
    {
        /// <summary>
        /// ① 등장 지점이 벽에서 떨어지는 거리. 방 크기와 무관한 몸통 하나다.
        ///
        /// <b>방 밖에서 소환하지 않는다.</b> 방은 사방이 콜라이더로 막혀 있어서 바깥에 놓으면
        /// 벽에 걸려 영영 못 들어온다. 벽 바로 앞에서 시작해 안쪽으로 걸어 들어오는 것으로
        /// "진입 모션"을 만든다 — 어차피 방 하나가 화면 하나라 벽 앞은 화면 가장자리다.
        /// </summary>
        public const float SpawnInset = 0.6f;

        /// <summary>③ 몸통 반지름 여유. 깊이 좌표를 이 안쪽으로 물린다.</summary>
        public const float DepthInset = 0.6f;

        // ── ② 역할별 정착 지점 — 벽에서 반폭의 몇 할 안쪽인가 ──
        //
        // 기준 방(반폭 6)에서 벽으로부터 2.8 · 1.8 · 1.2였던 값을 비율로 옮겼다. 그래서 100% 방의 답은 그대로다.
        // 고정 거리로 두면 좁은 방에서 좌우 전사 두 무리가 나오자마자 플레이어를 낀다 —
        // 방이 좁아진 것이 아니라 진형이 바뀐 것이 된다(계획서 3.1).

        public const float MeleeSettle = 2.8f / 6f;
        public const float ChargerSettle = 1.8f / 6f;
        public const float RangedSettle = 1.2f / 6f;

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

        private const float LaneEps = 0.0001f;

        /// <summary>
        /// 방 중심에서 깊이 좌표가 갈 수 있는 거리. 벽에 낀 채로 소환되지 않게 몸통 여유만큼 안쪽이다.
        /// <b>중심 기준 거리</b>이지 월드 좌표가 아니다.
        /// </summary>
        public static float MaxDepth(in RoomRect room) => Mathf.Max(0f, room.HalfZ - DepthInset);

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
        /// <param name="playerZ">지금 플레이어의 깊이. 월드 좌표.</param>
        /// <param name="room">적을 세울 방. 월드 좌표.</param>
        public static SpawnPlacement PlanAuto(in WaveSpawnEntry entry, int laneIndex, float playerZ, in RoomRect room)
        {
            int side = SideSign(entry.side, laneIndex);
            float z = DepthFor(entry.role, laneIndex, playerZ, in room);
            float entryX = room.CenterX + side * SettleOffset(entry.role, entry.side, laneIndex, in room);

            return new SpawnPlacement
            {
                spawnPoint = new Vector3(room.CenterX + side * (room.HalfX - SpawnInset), 0f, z),
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
        public static SpawnPlacement PlanAt(in WaveSpawnEntry entry, Vector3 point, in RoomRect room)
        {
            Vector3 ground = ClampIntoRoom(point, in room);

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
        public static bool IsInsideRoom(Vector3 point, in RoomRect room)
            => Mathf.Abs(point.x - room.CenterX) <= room.HalfX - SpawnInset + LaneEps
               && Mathf.Abs(point.z - room.CenterZ) <= MaxDepth(in room) + LaneEps;

        /// <summary>방 밖 좌표를 소환 가능한 자리로 당긴다. 높이는 접지가 다시 잡으므로 0으로 누른다.</summary>
        public static Vector3 ClampIntoRoom(Vector3 point, in RoomRect room)
        {
            float limitX = Mathf.Max(0f, room.HalfX - SpawnInset);
            float x = Mathf.Clamp(point.x, room.CenterX - limitX, room.CenterX + limitX);

            return new Vector3(x, 0f, Clamp(point.z, in room));
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
        /// <returns>월드 깊이 좌표.</returns>
        public static float DepthFor(EnemyRole role, int index, float playerZ, in RoomRect room)
        {
            switch (role)
            {
                case EnemyRole.Charger:
                    return ChargerDepth(index, playerZ, in room);

                case EnemyRole.Ranged:
                    return RangedDepth(index, playerZ, in room);

                default:
                    return MeleeDepth(index, in room);
            }
        }

        /// <summary>깊이를 방 안으로 물린다. 벽에 낀 소환은 그대로 끼어 있는다.</summary>
        public static float Clamp(float z, in RoomRect room)
        {
            float max = MaxDepth(in room);
            return Mathf.Clamp(z, room.CenterZ - max, room.CenterZ + max);
        }

        /// <summary>② 정착 지점이 벽에서 반폭의 몇 할 안쪽인가.</summary>
        public static float SettleFor(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Charger: return ChargerSettle;
                case EnemyRole.Ranged:  return RangedSettle;
                default:                return MeleeSettle;
            }
        }

        /// <summary>
        /// 전사의 깊이 줄. <see cref="MeleeLanes"/>를 순서대로 돌려 쓰되 <b>방 밖 줄은 건너뛴다.</b>
        ///
        /// 줄 간격은 몸 두 개가 떨어져 서는 거리라 방에 비례시키지 않는다(③). 그런데 방 밖 줄을
        /// 벽으로 물리기만 하면, 80% 방(깊이 ±1.8)에서 ±1.6과 ±2.4가 0.2 차이로 <b>접혀 겹친다.</b>
        /// 돌진전사(<see cref="ChargerDepth"/>)가 같은 이유로 먼저 겪고 고친 문제다.
        ///
        /// 줄을 한 바퀴 다 쓰고 돌려 쓸 때, <see cref="SpawnSide.Both"/>는 방 안 줄 수가 홀수(1 · 3 · 5)라
        /// 다음 바퀴의 같은 줄이 반대편 벽에서 나와 겹치지 않는다. <b>한쪽에서만 나오면</b> 앞 바퀴와 같은 점에 선다.
        /// 그래서 한쪽 등장만 몇 번째 바퀴인지(<see cref="MeleeCycle"/>)만큼 정착 지점을 안쪽으로 당긴다(<see cref="SettleOffset"/>).
        /// 기준 방은 다섯 줄이라 0~4번은 예전과 같은 답이고, 80% 방은 세 줄이라 한쪽 등장 4번째 전사부터 당겨진다.
        /// </summary>
        public static float MeleeDepth(int index, in RoomRect room)
        {
            float max = MaxDepth(in room);
            int inRoom = MeleeLanesInRoom(in room);

            // 0번 줄(중심)은 크기 0인 방이 아니면 항상 들어온다. 못 세면 중심이 가장 안전하다.
            if (inRoom == 0) return room.CenterZ;

            int wanted = Mathf.Abs(index) % inRoom;
            int found = 0;

            foreach (float lane in MeleeLanes)
            {
                if (Mathf.Abs(lane) > max + LaneEps) continue;
                if (found == wanted) return room.CenterZ + lane;
                found++;
            }

            return room.CenterZ;
        }

        /// <summary>③ 전사 줄을 한 바퀴 돌려 쓸 때마다 정착 지점이 안쪽으로 당겨지는 거리. 몸 하나다.</summary>
        public const float MeleeWrapStep = 1.2f;

        /// <summary>이 전사가 방 안 줄을 몇 바퀴째 쓰는가. 0이면 첫 바퀴.</summary>
        public static int MeleeCycle(int index, in RoomRect room)
        {
            int inRoom = MeleeLanesInRoom(in room);
            return inRoom > 0 ? Mathf.Abs(index) / inRoom : 0;
        }

        /// <summary>
        /// 정착 지점이 방 중심에서 떨어진 거리(부호 없음). 역할의 ② 비율에서 나오고,
        /// 한쪽에서만 나오는 전사는 줄을 돌려 쓴 바퀴만큼 안쪽으로 당긴다. 중심을 넘지는 않는다.
        ///
        /// <b>양쪽 등장은 안 당긴다.</b> 증원이 <see cref="SpawnSide.Both"/>로 번호를 계속 올리는데,
        /// 당기면 늦게 온 증원일수록 플레이어 코앞에 서고 100% 방의 답까지 바뀐다.
        /// </summary>
        public static float SettleOffset(EnemyRole role, SpawnSide side, int index, in RoomRect room)
        {
            float offset = room.HalfX * (1f - SettleFor(role));

            bool melee = role != EnemyRole.Charger && role != EnemyRole.Ranged;
            if (melee && side != SpawnSide.Both)
                offset -= MeleeCycle(index, in room) * MeleeWrapStep;

            return Mathf.Max(0f, offset);
        }

        private static int MeleeLanesInRoom(in RoomRect room)
        {
            float max = MaxDepth(in room);

            int count = 0;
            foreach (float lane in MeleeLanes)
                if (Mathf.Abs(lane) <= max + LaneEps) count++;

            return count;
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
        public static float ChargerDepth(int index, float playerZ, in RoomRect room)
        {
            float max = MaxDepth(in room);

            // 계산은 방 중심 기준으로 하고 돌려줄 때 월드로 옮긴다.
            float lane = Mathf.Clamp(playerZ - room.CenterZ, -max, max);
            int wanted = Mathf.Abs(index) % ChargerLanes;
            int found = 0;

            // 0, +1, −1, +2, −2 … 순으로 훑어 방 안에 들어오는 줄만 센다.
            for (int rung = 0; rung < ChargerLanes * 4; rung++)
            {
                float candidate = lane + RungOffset(rung) * ChargerLaneStep;
                if (Mathf.Abs(candidate) > max + LaneEps) continue;

                if (found == wanted) return room.CenterZ + candidate;
                found++;
            }

            // 방이 줄 셋을 못 담을 만큼 좁으면 여기 온다. 플레이어 줄이 가장 안전하다.
            return room.CenterZ + lane;
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
        public static float RangedDepth(int index, float playerZ, in RoomRect room)
        {
            float max = MaxDepth(in room);
            float player = playerZ - room.CenterZ;

            int wanted = Mathf.Abs(index);
            int found = 0;

            for (int rung = 0; rung < 12; rung++)
            {
                float sign = rung % 2 == 0 ? 1f : -1f;
                float candidate = sign * (max - rung / 2 * RangedLaneStep);

                if (Mathf.Abs(candidate) > max + LaneEps) continue;
                if (Mathf.Abs(candidate - player) < RangedLaneGap - LaneEps) continue;

                if (found == wanted) return room.CenterZ + candidate;
                found++;
            }

            // RoomRules.IsLargeEnough를 지키는 방이면 여기 오지 않는다. 와도 먼 구석이 낫다.
            return room.CenterZ + (player >= 0f ? -max : max);
        }
    }
}
