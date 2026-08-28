using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 태그 교대. 로스터(플레이어 + 동료 4명)에서 <b>한 명만</b> 필드에 세우고,
    /// 그 한 명을 유저가 직접 조작한다. 철권 태그·후레쉬맨 교대와 같은 구조다.
    ///
    /// 두 가지를 동시에 한다:
    /// <list type="number">
    /// <item><b>몸 전환</b> — 내려가는 몸은 <c>SetActive(false)</c>, 올라오는 몸은 활성화 후
    /// 직전 자리로 배치. 뒷정리는 <c>OnDisable</c>이 맡는다.</item>
    /// <item><b>조종 인계</b> — 씬에 하나뿐인 <see cref="PlayerPilot"/>에게 올라온 몸을 넘긴다.
    /// 나머지 몸은 아무 처리도 필요 없다 — 조종사가 안 보는 몸은 그냥 안 움직인다.</item>
    /// </list>
    ///
    /// 이 컴포넌트는 <b>절대 꺼지지 않는 오브젝트</b>에 붙어야 한다 —
    /// <see cref="BattleCommander"/>·입력과 한 자리에 두는 게 맞다.
    ///
    /// 불릿타임에도 <b>화면에는 한 명뿐</b>이라는 규칙이 그대로 유지된다.
    /// 이 컴포넌트가 <see cref="ICasterStage"/>를 구현해 <see cref="ComboExecutor"/>의
    /// 무대 노릇을 한다 — 카드가 발동되는 순간 그 직업이 조작 캐릭터가 서 있던 자리에
    /// 등장했다가 슬롯이 끝나면 내려간다. 예전에는 진입 즉시 로스터 전원을 세웠는데,
    /// 그 시점엔 어떤 카드가 나갈지조차 정해지지 않아 카드를 안 쓰는 직업까지 나왔다.
    /// </summary>
    public class TagSwapController : MonoBehaviour, ICasterStage
    {
        [Header("참조")]
        [SerializeField] private Player player;
        [SerializeField] private BulletTimeController bulletTime;

        [Tooltip("비우면 씬에서 찾는다. 교대할 때마다 새 몸을 따라가게 만든다.")]
        [SerializeField] private CameraFollow cameraFollow;

        [Tooltip("비우면 씬에서 찾는다. 조준 기준점을 새 몸으로 옮긴다.")]
        [SerializeField] private TargetSelector targetSelector;

        [Tooltip("비우면 씬에서 찾는다. 유저가 모는 몸을 여기에 넘긴다.")]
        [SerializeField] private PlayerPilot pilot;

        [Header("교대")]
        [Tooltip("교대 후 다시 교대할 수 있을 때까지의 시간.")]
        [SerializeField] private float swapCooldown = 1.5f;

        [Tooltip("무대에 둘 이상이 설 때(차징으로 붙잡힌 몸) 비켜서는 자리. " +
                 "지금 시전 중인 몸은 언제나 앵커 정위치이고, 이 값은 그 앞칸들에만 쓰인다.\n\n" +
                 "조작 중인 몸 기준 상대 좌표이고 그 몸이 보는 방향에 맞춰 좌우가 뒤집힌다. " +
                 "모자라면 마지막 값을 재사용한다.")]
        [SerializeField]
        private Vector3[] benchOffsets =
        {
            new Vector3(-2.4f, 0f, -0.8f),
            new Vector3(-1.2f, 0f,  0.8f),
            new Vector3( 1.2f, 0f, -0.8f),
            new Vector3( 2.4f, 0f,  0.8f),
        };

        [Tooltip("콤보가 끝나고 조작 캐릭터가 돌아올 자리.\n\n" +
                 "켜면 마지막 시전자가 끝낸 자리 — 콤보가 적진으로 파고들었는데 플레이어만 " +
                 "뒤로 튕겨 나가지 않는다. 끄면 콤보를 시작한 제자리로 돌아온다.")]
        [SerializeField] private bool returnToLastCasterSpot = true;

        /// <summary>플레이어 + 동료 4명. Start에서 한 번 만든다.</summary>
        private readonly List<Entity> roster = new List<Entity>();

        /// <summary>지금 무대에 선 몸. <b>마지막 칸이 시전 중인 몸</b>이고 앵커 정위치를 받는다.</summary>
        private readonly List<Entity> onStage = new List<Entity>();

        /// <summary>
        /// <see cref="ICasterStage.Exit"/>를 받았지만 아직 안 내린 몸.
        ///
        /// 곧바로 내리지 않는 이유는 같은 시전자가 연속 슬롯에 나올 때 한 프레임
        /// 깜빡이기 때문이다 — 그 사이 <c>OnDisable → ReleaseBody</c>가 돌아
        /// 다음 슬롯의 스킬 상태가 통째로 날아간다.
        /// </summary>
        private readonly List<Entity> leaving = new List<Entity>();

        /// <summary>
        /// 콤보 내내 고정되는 무대 기준점. 첫 <see cref="ICasterStage.Enter"/>에서 한 번 잡는다.
        ///
        /// 슬롯마다 다시 잡으면 직전 시전자가 돌진한 만큼 무대가 맵을 가로질러 흘러간다.
        /// </summary>
        private Seat? stageAnchor;

        private float cooldownTimer;

        /// <summary>
        /// 교대 자리. <b>위치만으로는 부족하다</b> — <see cref="Physics.Teleport"/>는 방향을
        /// 건드리지 않아서, 자리만 물려주면 새 몸이 제 지난 방향 그대로 나온다.
        /// 오른쪽으로 밀고 나가다 교대했는데 새 몸이 왼쪽을 보고 서는 게 그 증상이다.
        /// </summary>
        private readonly struct Seat
        {
            public readonly Vector3 Ground;
            public readonly Vector3 Facing;

            /// <summary>
            /// 바닥에서 뜬 높이. 공중에서 교대했는데 새 몸만 바닥에 서면 콤보가 끊긴다 —
            /// <see cref="Ground"/>는 Y를 바닥으로 눌러 버리므로 높이를 따로 들고 간다.
            /// </summary>
            public readonly float Height;

            public Seat(Vector3 ground, Vector3 facing, float height = 0f)
            {
                Ground = ground;
                Facing = facing;
                Height = height;
            }

            public static Seat Of(Entity e)
                => new Seat(e.Physics.GroundPosition, e.Physics.Facing, e.Physics.Height);

            /// <summary>이 자리 기준의 상대 좌표. 보는 방향이 왼쪽이면 좌우를 뒤집는다.</summary>
            public Seat Offset(Vector3 local)
            {
                if (Facing.x < 0f) local.x = -local.x;
                return new Seat(Ground + local, Facing, Height);
            }
        }

        /// <summary>
        /// 마지막으로 확정된 자리. 조작 중인 몸이 죽어 <see cref="Current"/>가 비는 순간에도
        /// 소환 기준이 있어야 한다 — 없으면 이 컴포넌트의 좌표(대개 원점)로 떨어진다.
        /// </summary>
        private Seat lastSeat;

        /// <summary>
        /// 등퇴장이 도는 동안 카메라 · 조준이 물려 있는 고정점.
        /// 처음 필요할 때 만든다(<see cref="FollowSpot"/>).
        /// </summary>
        private Transform cameraAnchor;

        /// <summary>
        /// 사망 구독. <see cref="Combat.OnDead"/>가 인자를 주지 않아 몸마다 클로저를
        /// 하나씩 만든다 — <see cref="BulletTimeController"/>가 쓰는 방식과 같다.
        /// </summary>
        private readonly List<(Combat combat, Action handler)> deathHooks = new List<(Combat, Action)>();

        /// <summary>지금 조작 중인 몸의 로스터 칸. 아무도 못 세웠으면 -1.</summary>
        public int CurrentIndex { get; private set; } = -1;

        public IReadOnlyList<Entity> Roster => roster;

        /// <summary>
        /// 조작하지 않는 몸들이 화면 밖 어디에서 기다리는가.
        ///
        /// <b>여기가 소유한다.</b> 로스터 · 조작 중인 칸을 이미 이쪽이 알고 있어서,
        /// 다른 데서 또 한 벌을 들면 반드시 어긋난다. 교대 · 시전 등퇴장이 이 좌표를
        /// 목적지와 출발지로 쓰고, 가장자리 표식이 같은 좌표를 그린다.
        ///
        /// <b>처음 물어볼 때 만든다.</b> <c>Start</c>에서 만들면 그 전에 읽는 쪽
        /// (표식은 <c>[BattleVfx]</c>에서 스스로 깨어난다)이 실행 순서에 따라 null을 잡는다.
        /// 로스터 목록은 비우기만 하고 갈아 끼우지 않으므로 한 번 묶어 두면 계속 유효하다.
        /// </summary>
        public PartyStandby Standby
        {
            get
            {
                if (standby == null) standby = new PartyStandby(roster);
                return standby;
            }
        }

        private PartyStandby standby;

        public Entity Current
            => CurrentIndex >= 0 && CurrentIndex < roster.Count ? roster[CurrentIndex] : null;

        /// <summary>
        /// 조작 캐릭터가 서 있는 — 등퇴장 연출 중이면 <b>서게 될</b> — 바닥 좌표.
        ///
        /// <see cref="Current"/>의 트랜스폼을 직접 읽지 말고 이쪽을 쓸 것.
        /// 연출이 도는 동안에는 두 몸이 동시에 활성이고 조작 캐릭터는 화면 밖에 있어서,
        /// 좌표를 몸에서 읽으면 <b>대기 자리</b>가 나온다. 웨이브 배치처럼
        /// "플레이어가 지금 어디 있나"를 묻는 쪽이 그걸 집으면 규칙이 통째로 어긋난다
        /// (<see cref="WaveSpawnPlanner.DepthFor"/>).
        /// </summary>
        public Vector3 ControlledGround => CurrentSeat().Ground;

        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);

        /// <summary>교대 키가 지금 먹히는가. 실시간이 아니거나 쿨이 남았으면 false.</summary>
        public bool CanSwap
            => cooldownTimer <= 0f
               && bulletTime != null
               && bulletTime.Phase == TacticPhase.RealTime;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<Player>();
            if (bulletTime == null) bulletTime = FindAnyObjectByType<BulletTimeController>();
            if (cameraFollow == null) cameraFollow = FindAnyObjectByType<CameraFollow>();
            if (targetSelector == null) targetSelector = FindAnyObjectByType<TargetSelector>();
            if (pilot == null) pilot = FindAnyObjectByType<PlayerPilot>();

            if (pilot == null)
                BattleLog.Warn(LogCategory.State,
                    "PlayerPilot이 씬에 없다 — 아무도 몸을 몰지 않는다", this);
        }

        /// <summary>
        /// 구독과 초기 배치는 <b>Start</b>에서 한다. <see cref="BulletTimeController.Tactic"/>이
        /// 그쪽 Awake에서 만들어지므로 여기서 Awake에 붙으면 실행 순서에 따라 null을 잡는다.
        /// </summary>
        private void Start()
        {
            BuildRoster();

            if (bulletTime != null)
            {
                bulletTime.Tactic.OnPhaseChanged += HandlePhaseChanged;

                // 무대는 이쪽에서 꽂는다. Executor가 태그 시스템을 찾아다니면
                // 의존이 거꾸로 서고, 스케줄러 없이 도는 에디트모드 테스트가 씬을 타게 된다.
                if (bulletTime.Executor != null) bulletTime.Executor.Stage = this;
                else BattleLog.Warn(LogCategory.State,
                    "ComboExecutor가 없어 무대를 배선하지 못했다 — 불릿타임 시전자가 안 나온다", this);
            }

            SubscribeDeaths();
            InitializeRoster();
        }

        private void OnDestroy()
        {
            if (bulletTime != null && bulletTime.Tactic != null)
                bulletTime.Tactic.OnPhaseChanged -= HandlePhaseChanged;

            UnsubscribeDeaths();
        }

        private void Update()
        {
            if (cooldownTimer > 0f) cooldownTimer -= TimeControl.DeltaTime;
        }

        // ── 로스터 ──────────────────────────────────────────

        /// <summary>플레이어가 0번, 동료가 1~4번. 이 순서가 곧 교대 순환 순서다.</summary>
        private void BuildRoster()
        {
            roster.Clear();

            if (player == null)
            {
                BattleLog.Warn(LogCategory.State, "Player를 못 찾아 태그 로스터를 못 만들었다", this);
                return;
            }

            roster.Add(player);

            foreach (Ally a in player.Party)
                roster.Add(a);   // null 칸도 그대로 넣는다. 순환 규칙이 알아서 건너뛴다.
        }

        // ── 교대 ────────────────────────────────────────────

        /// <summary>다음 생존자로 돌린다. 세울 사람이 자기뿐이면 아무 일도 안 한다.</summary>
        public bool SwapNext()
        {
            if (!CanSwap)
            {
                BattleLog.Log(LogCategory.State,
                    $"교대 거부 — {(cooldownTimer > 0f ? $"쿨 {cooldownTimer:0.##}s 남음" : $"{bulletTime?.Phase} 페이즈")}", this);
                return false;
            }

            int next = TagSwapRules.NextAlive(roster, CurrentIndex);
            if (next < 0 || next == CurrentIndex)
            {
                BattleLog.Log(LogCategory.State, "교대 거부 — 세울 수 있는 다른 몸이 없다", this);
                return false;
            }

            SwapTo(next);
            return true;
        }

        /// <summary>
        /// 지정한 몸을 필드에 세우고 조작권을 넘긴다. 이미 조작 중이면 그대로 true.
        ///
        /// <b>쿨타임을 보지 않는다</b> — 부르는 쪽(U키 카드 · 조작 중인 몸의 사망)은 이미
        /// "이 몸이어야 한다"가 정해진 경로라 여기서 막으면 카드가 통째로 불발된다.
        /// 대신 쿨은 다시 돌린다.
        ///
        /// <b>등퇴장 연출도 붙이지 않는다.</b> 부르는 쪽이 <c>UseTopCard</c>인데, 그쪽은
        /// 여기서 돌아온 <b>같은 프레임에</b> <c>CanCastCard</c>를 본다 — 연출이 돌고 있으면
        /// 그 검사에서 떨어져 카드는 손패에 남고, 유저에게는 "U를 눌렀는데 아무 일도
        /// 안 일어났다"로만 보인다. 카드가 주목적이고 교대는 부수효과다.
        /// </summary>
        public bool EnsureActive(Entity body)
        {
            if (body == null) return false;

            if (ReferenceEquals(body, Current) && body.gameObject.activeSelf)
            {
                // 이미 조작 중인데 아직 날아오는 중일 수 있다. 그 자리에서 끝내 버린다 —
                // "0.35초 뒤에 다시 누르라"는 답이 될 수 없고, 화면 밖에서 시전되면 더 나쁘다.
                EntranceDirector.Finish(body);
                return true;
            }

            int index = roster.IndexOf(body);
            if (!TagSwapRules.IsSelectable(roster, index)) return false;

            SwapTo(index, animate: false);
            return true;
        }

        /// <summary>
        /// <paramref name="animate"/>가 false면 예전처럼 그 자리에서 즉시 바꿔치기한다.
        /// 카드 즉시 사용과 첫 배치가 그쪽이다 — 둘 다 "지금 당장"이 곧 요구사항이라
        /// 0.35초를 끼워 넣을 자리가 없다.
        /// </summary>
        private void SwapTo(int index, bool animate = true)
        {
            Entity outgoing = Current;
            Entity incoming = roster[index];

            // 새 몸은 나가는 몸의 자리와 방향을 그대로 물려받는다. 첫 배치처럼 물려줄
            // 사람이 없으면 제 자리에 그냥 서고, 그 자리가 이후의 기준이 된다.
            //
            // <b>연출을 시작하기 전에 읽는다</b> — 퇴장이 시작되면 나가는 몸은 곧바로 움직인다.
            Seat? seat = outgoing != null ? Seat.Of(outgoing) : (Seat?)null;

            // 물려받을 좌석이 없으면 날아올 목적지도 없다. 첫 배치가 그렇다.
            // 자기 자신으로 교대하는 경우도 뺀다 — 제 자리로 날아오려고 화면 밖까지
            // 갔다 돌아오는 그림이 된다.
            bool relay = animate && seat.HasValue && !ReferenceEquals(outgoing, incoming);

            if (outgoing != null && !ReferenceEquals(outgoing, incoming))
            {
                if (relay) Leave(outgoing);
                else Bench(outgoing);
            }

            if (relay) FlyIn(incoming, seat.Value);
            else Summon(incoming, seat);

            // 조작권은 <b>즉시</b> 넘긴다. 날아오는 동안 입력이 먹지 않는 것은
            // Entity.IsEntering이 이미 막고 있으므로, 여기서 미루면 착지 직후 한 프레임을 놓친다.
            Possess(incoming);

            CurrentIndex = index;
            cooldownTimer = swapCooldown;

            // <b>몸이 아니라 좌석에서 읽는다.</b> 연출 중이라면 incoming은 아직 화면 밖 대기 자리에
            // 있어서 Seat.Of(incoming)이 그 좌표를 준다 — 그 값이 무대 기준점으로 새면
            // 다음 콤보가 통째로 화면 밖에서 열린다.
            lastSeat = seat ?? Seat.Of(incoming);

            // 연출이 있으면 카메라는 FlyIn이 좌석에 묶어 두고, 착지할 때 몸으로 넘긴다.
            if (!relay) FollowBody(incoming);

            BattleLog.Log(LogCategory.State,
                $"<b>태그 교대</b> {BattleLog.Name(outgoing)} → {BattleLog.Name(incoming)} " +
                $"(쿨 {swapCooldown:0.#}s{(relay ? ", 등퇴장" : "")})", this);
        }

        /// <summary>
        /// 조작 캐릭터가 <b>서 있어야 할</b> 자리.
        ///
        /// 연출 중에는 몸을 읽지 않는다 — 그때 몸은 화면 밖 대기 자리에 있고,
        /// 착지 목표는 <see cref="lastSeat"/>가 이미 들고 있다. F로 교대한 직후 곧바로
        /// E를 누르면 실제로 이 창이 열리고, 걸러 내지 않으면 콤보 무대가 통째로 화면 밖에 선다.
        /// </summary>
        private Seat CurrentSeat()
        {
            Entity body = Current;

            if (body == null || body.IsEntering) return lastSeat;

            return Seat.Of(body);
        }

        /// <summary>카메라와 조준 기준을 새 몸으로 옮긴다. <b>착지한 뒤에</b> 부른다.</summary>
        private void FollowBody(Entity body)
        {
            if (body == null) return;

            cameraFollow?.SetTarget(body.transform);
            targetSelector?.SetCursorOrigin(body.transform);
        }

        /// <summary>
        /// 교대가 도는 동안 카메라를 <b>좌석에 묶어 둔다.</b>
        ///
        /// 몸을 따라가게 두면 양쪽이 다 샌다 — 나가는 몸을 계속 보면 카메라가 그대로
        /// 화면 밖으로 끌려나가고, 들어오는 몸으로 미리 넘기면 아직 화면 밖인 그쪽으로 끌려나간다.
        /// 교대는 제자리 인계라 <b>카메라는 안 움직이는 것이 맞다</b>.
        /// </summary>
        private void FollowSpot(Vector3 ground)
        {
            if (cameraAnchor == null)
            {
                var go = new GameObject("[TagSwapAnchor]");
                go.transform.SetParent(transform, false);
                cameraAnchor = go.transform;
            }

            cameraAnchor.position = ground;

            cameraFollow?.SetTarget(cameraAnchor);
            targetSelector?.SetCursorOrigin(cameraAnchor);
        }

        // ── 빙의 ────────────────────────────────────────────

        /// <summary>
        /// 조작권을 옮긴다. <b>조종사가 몸 밖에 하나뿐이라 한 줄이면 끝난다</b> —
        /// 예전에는 로스터를 전부 돌며 몸마다 붙은 Control을 껐다 켰다(빙의) 했다.
        ///
        /// 나머지 몸은 아무 처리도 필요 없다. 조종사가 안 보는 몸은 그냥 안 움직인다.
        /// 그게 "불려 나온 시전자가 컷인 도중 제 발로 걸어 다니던" 문제의 해결이다.
        /// </summary>
        private void Possess(Entity incoming)
        {
            pilot?.Take(incoming);
        }

        // ── 불릿타임 ────────────────────────────────────────

        private void HandlePhaseChanged(TacticPhase prev, TacticPhase next)
        {
            // Freeze에서는 아무도 세우지 않는다. 어떤 카드가 나갈지는 다음 Order 페이즈에서
            // 정해지고, 실제 등장은 ComboExecutor가 슬롯마다 ICasterStage로 요청한다.

            // Resolve → RealTime 이 곧 "콤보 실행 완료"다. 무대는 Executor가 이미 비웠지만,
            // 타임아웃 · 중단으로 새어 나온 몸이 있을 수 있어 안전망으로 한 번 더 훑는다.
            if (prev == TacticPhase.Resolve && next == TacticPhase.RealTime)
                BenchOthers();
        }

        // ── 무대(ICasterStage) ──────────────────────────────

        /// <summary>
        /// 시전자를 무대에 세운다. 앞 슬롯에서 내려가기로 예약된 몸도 여기서 정리한다.
        ///
        /// <b>이미 무대에 있는 몸은 자리를 다시 잡지 않는다</b> — 돌진으로 전진한 시전자가
        /// 다음 슬롯에서 제자리로 튕겨 돌아가면 콤보가 통째로 어색해진다.
        /// </summary>
        void ICasterStage.Enter(Ally caster)
        {
            if (caster == null) return;

            // 무대를 처음 세운다: 기준점을 잡고 조작 캐릭터를 내린다.
            // Bench는 SetActive(false)일 뿐 트랜스폼을 안 건드리므로 앵커는 그대로 유효하다.
            if (stageAnchor == null)
            {
                stageAnchor = CurrentSeat();

                // 조작 중인 몸이 곧 시전자면 껐다 켤 이유가 없다. 태그 교대로 동료를 몰고 있을 때
                // 그 동료의 카드가 나가는 경우다 — 껐다 켜면 OnDisable → ReleaseBody가 헛돈다.
                if (!ReferenceEquals(Current, caster)) Leave(Current);
            }

            // 예약된 퇴장을 실행한다. 다시 불려 나온 몸은 내리지 않는다.
            //
            // 여기서 비로소 나가므로 <b>앞 시전자의 퇴장과 이번 시전자의 등장이 겹친다</b> —
            // 한쪽이 화면 밖으로 빠지는 동안 다른 쪽이 들어오는 릴레이가 된다.
            // 이 겹침이 곧 슬롯 사이의 빈 시간을 없애 준다.
            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(leaving[i], caster)) continue;
                Leave(leaving[i]);
            }
            leaving.Clear();

            bool alreadyUp = onStage.Contains(caster);

            // 마지막 칸이 곧 "지금 시전 중"이다. 이미 서 있던 몸도 맨 뒤로 옮긴다.
            onStage.Remove(caster);
            onStage.Add(caster);

            // 이미 무대에 있는 몸은 다시 부르지 않는다. 나갔다 들어오면 연속 슬롯마다
            // 왕복이 생기고, 돌진으로 전진한 시전자가 제자리로 튕겨 돌아간다.
            if (!alreadyUp)
                FlyIn(caster, stageAnchor.Value);

            Reseat();

            // 불려 나온 몸에는 아무도 안 붙인다. 조종사는 조작 캐릭터만 보고,
            // 몸에 자율 BT가 없으므로 시전자는 스킬이 나갈 때까지 가만히 서 있는다.

            // 카메라는 지금 때리는 쪽을 본다. 숨은 조작 캐릭터를 계속 보면
            // 시전자가 돌진해 나간 뒤 화면에 아무것도 안 남는다.
            //
            // <b>날아오는 동안에는 앵커를 본다</b> — FlyIn이 이미 그렇게 묶어 두고
            // 착지할 때 시전자로 넘긴다. 이미 서 있던 몸이면 곧바로 넘긴다.
            if (alreadyUp) FollowBody(caster);
        }

        /// <summary>
        /// 지금 시전자가 아직 화면 밖에서 날아오는 중인가.
        ///
        /// <see cref="ComboExecutor"/>가 컷인 뒤에 이 값을 보고 기다린다 —
        /// 착지 자리가 곧 스킬의 접근 · 조준 기준점이라, 도착 전에 시전하면
        /// 화면 밖에서 스킬이 터진다.
        ///
        /// 무대의 마지막 칸이 곧 시전 중인 몸이다(<see cref="onStage"/>).
        /// </summary>
        bool ICasterStage.IsEntering
        {
            get
            {
                if (onStage.Count == 0) return false;

                Entity caster = onStage[onStage.Count - 1];
                return caster != null && caster.IsEntering;
            }
        }

        /// <summary>
        /// 할 일이 끝났다고 표시만 한다. 실제로 내려가는 시점은 다음
        /// <see cref="ICasterStage.Enter"/>나 <see cref="ICasterStage.Clear"/>다.
        /// </summary>
        void ICasterStage.Exit(Ally caster)
        {
            if (caster == null) return;
            if (!onStage.Remove(caster)) return;

            if (!leaving.Contains(caster)) leaving.Add(caster);
        }

        /// <summary>무대를 비우고 조작 캐릭터를 되돌린다.</summary>
        void ICasterStage.Clear()
        {
            // 복귀 자리는 내리기 <b>전에</b> 읽어야 한다. 내린 뒤엔 마지막 시전자가 누구였는지
            // 알 수 없다.
            Seat? home = null;
            if (returnToLastCasterSpot && onStage.Count > 0)
            {
                Entity last = onStage[onStage.Count - 1];

                // 아직 날아오는 중인 몸이면 그 좌표는 화면 밖이다. 착지 목표(앵커)를 쓴다 —
                // 중단(Abort)으로 여기 들어오면 실제로 그 창이 열린다.
                if (last != null) home = last.IsEntering ? stageAnchor : Seat.Of(last);
            }

            for (int i = 0; i < onStage.Count; i++) Leave(onStage[i]);
            for (int i = 0; i < leaving.Count; i++) Leave(leaving[i]);

            onStage.Clear();
            leaving.Clear();

            // 무대를 세운 적이 없으면 되돌릴 것도 없다 — 조작 캐릭터는 계속 서 있었다.
            if (stageAnchor == null) return;

            Seat spot = home ?? stageAnchor.Value;
            stageAnchor = null;

            // Current를 다시 읽는다. 콤보 도중 사망 자동교대가 돌았으면 다른 몸일 수 있다.
            Entity back = Current;
            if (back == null) return;

            // 조작 캐릭터도 콤보가 시작될 때 화면 밖으로 나갔다. 같은 자리에서 돌아온다.
            FlyIn(back, spot);
            Possess(back);

            // 몸이 아니라 <b>돌아갈 자리</b>에서 읽는다. 지금 back은 아직 대기 자리에 있다.
            lastSeat = spot;
        }

        /// <summary>
        /// 무대 위 몸들을 앵커 기준으로 다시 앉힌다.
        ///
        /// 마지막 칸(시전 중)은 정위치라 손대지 않는다 — 그 좌표에서 스킬의 접근·조준이
        /// 계산되므로 흔들리면 안 된다. 차징으로 붙잡힌 앞칸만 옆으로 비켜난다.
        /// </summary>
        private void Reseat()
        {
            if (stageAnchor == null) return;

            Seat anchor = stageAnchor.Value;

            for (int i = 0; i < onStage.Count; i++)
            {
                Entity e = onStage[i];
                if (e == null) continue;

                Vector3 offset = TagSwapRules.StageOffset(i, onStage.Count, benchOffsets);
                if (offset == Vector3.zero) continue;

                e.Physics.Teleport(anchor.Offset(offset).Ground);
            }
        }

        /// <summary>조작 중인 한 명만 남기고 내린다.</summary>
        public void BenchOthers()
        {
            for (int i = 0; i < roster.Count; i++)
            {
                if (i == CurrentIndex) continue;

                Entity e = roster[i];
                // 죽은 몸은 그대로 둔다. 여기서 끄면 사망 연출과 디스폰이 통째로 잘린다.
                if (e == null || e.Combat.IsDead) continue;

                Bench(e);
            }
        }

        // ── 세우기 · 내리기 ─────────────────────────────────

        private void Summon(Entity body, Seat? seat)
        {
            if (body == null) return;

            body.gameObject.SetActive(true);
            if (!seat.HasValue) return;

            // Teleport가 접지 · 관성 · 낙하속도를 함께 정리한다. 위치만 대입하면
            // 내려가기 직전의 넉백 속도를 그대로 물고 다시 선다.
            // 높이까지 넘긴다 — 공중에서 교대하면 새 몸도 같은 높이에서 이어받아야 한다.
            body.Physics.Teleport(seat.Value.Ground, seat.Value.Height);

            // Teleport는 방향을 안 건드린다. 여기서 맞추지 않으면 새 몸이
            // 제 지난 방향 그대로 나와 등을 보이고 선다.
            body.Physics.Face(seat.Value.Facing);
        }

        /// <summary>
        /// <b>즉시</b> 내린다. 초기화 · 안전망 · 무대 전용이다.
        /// 연출을 붙여 내리는 쪽은 <see cref="Leave"/>.
        /// </summary>
        private void Bench(Entity body)
        {
            if (body == null || !body.gameObject.activeSelf) return;
            body.gameObject.SetActive(false);
        }

        /// <summary>
        /// 화면 밖 <b>자기 대기 자리</b>로 날려 보낸 뒤 내린다.
        ///
        /// 목적지가 곧 표식이 뜰 자리다 — 다음에 이 몸은 같은 곳에서 다시 나온다.
        /// 그래서 좌표가 틀리면 "나간 곳과 다른 데서 들어온다"로 즉시 드러난다.
        ///
        /// 죽은 몸은 그대로 둔다. 여기서 날리면 사망 연출과 디스폰이 통째로 잘린다.
        /// </summary>
        private void Leave(Entity body)
        {
            if (body == null || !body.gameObject.activeSelf) return;
            if (body.Combat != null && body.Combat.IsDead) return;

            if (!Standby.TryPointFor(body, out Vector3 exit))
            {
                // 로스터 밖의 몸이면 물러날 자리가 없다. 예전처럼 그냥 내린다.
                Bench(body);
                return;
            }

            EntranceSpec spec = EntranceSpec.Default(body.Physics.GroundPosition, exit);
            spec.height = body.Physics.Height;

            // 방향은 비워 둔다 — 진행 방향을 그대로 보므로 화면 밖으로 뛰어 나가는 그림이 된다.
            spec.unscaled = true;
            spec.onArrive = () => Bench(body);

            EntranceDirector.Play(body, in spec);
        }

        /// <summary>
        /// 대기 자리에서 좌석으로 날아 들어온다.
        ///
        /// <b>카메라는 착지한 뒤에 넘긴다.</b> 지금 넘기면 아직 화면 밖인 몸을 따라
        /// 화면이 통째로 끌려나간다 — 그동안은 좌석을 본다.
        /// </summary>
        private void FlyIn(Entity body, in Seat seat)
        {
            if (body == null) return;

            body.gameObject.SetActive(true);

            if (!Standby.TryPointFor(body, out Vector3 start))
            {
                // 로스터 밖의 몸이면 날아올 자리가 없다. 예전처럼 그 자리에 세운다.
                Summon(body, seat);
                FollowBody(body);
                return;
            }

            Vector3 home = seat.Ground;
            FollowSpot(home);

            EntranceSpec spec = EntranceSpec.Default(start, home);
            spec.facing = seat.Facing;
            spec.height = seat.Height;

            // 게임 시간으로 굴리면 안 된다. 교대 직후 불릿타임에 들어가면 TimeControl.Scale이
            // 0으로 내려가, 몸은 화면 밖에 얼어붙고 입력은 잠긴 채로 남는다.
            // 교대는 조작권 인계라 중간에 멎으면 그대로 게임이 멈춘 것으로 보인다.
            spec.unscaled = true;

            spec.onArrive = () => FollowBody(body);

            EntranceDirector.Play(body, in spec);
        }

        // ── 초기화 · 사망 ───────────────────────────────────

        private void InitializeRoster()
        {
            if (roster.Count == 0) return;

            for (int i = 0; i < roster.Count; i++)
                Bench(roster[i]);

            CurrentIndex = -1;

            int first = TagSwapRules.FirstAlive(roster);
            if (first < 0)
            {
                BattleLog.Warn(LogCategory.State, "로스터에 세울 몸이 없다", this);
                return;
            }

            // 첫 배치는 교대가 아니다 — 물려받을 좌석도, 나갈 사람도 없다.
            // 연출을 붙이면 스테이지가 열리자마자 플레이어가 화면 밖에서 날아 들어온다.
            SwapTo(first, animate: false);

            // 시작하자마자 쿨이 도는 건 이상하다. 첫 배치는 교대로 치지 않는다.
            cooldownTimer = 0f;
        }

        private void SubscribeDeaths()
        {
            foreach (Entity e in roster)
            {
                if (e == null || e.Combat == null) continue;

                Entity dead = e;
                Action handler = () => HandleDied(dead);

                dead.Combat.OnDead += handler;
                deathHooks.Add((dead.Combat, handler));
            }
        }

        private void UnsubscribeDeaths()
        {
            for (int i = 0; i < deathHooks.Count; i++)
                if (deathHooks[i].combat != null)
                    deathHooks[i].combat.OnDead -= deathHooks[i].handler;

            deathHooks.Clear();
        }

        /// <summary>
        /// 조작 중인 몸이 죽으면 다음 생존자를 세운다.
        /// 시체는 내리지 않는다 — 사망 연출과 디스폰이 스스로 끝난다.
        /// </summary>
        private void HandleDied(Entity dead)
        {
            if (dead == null || !ReferenceEquals(dead, Current)) return;

            int next = TagSwapRules.NextAlive(roster, CurrentIndex);
            if (next < 0 || next == CurrentIndex)
            {
                BattleLog.Warn(LogCategory.State,
                    $"{BattleLog.Name(dead)} 사망 — 세울 몸이 남지 않았다", this);
                CurrentIndex = -1;
                return;
            }

            BattleLog.Log(LogCategory.State, $"{BattleLog.Name(dead)} 사망 — 자동 교대", this);

            // SwapTo가 시체를 내리려 들지 않도록 먼저 자리에서 뗀다.
            // 자리는 교대와 같은 규칙 — 쓰러진 곳과 보던 방향을 그대로 물려받는다.
            //
            // 몸이 아니라 좌석에서 읽는다. 날아 들어오는 도중에 도트 피해로 죽으면
            // 시체가 화면 밖에 있고, 그 좌표를 물려주면 다음 몸이 화면 밖에 서 버린다.
            Seat seat = CurrentSeat();
            CurrentIndex = -1;

            // 날아오는 도중에 죽었을 수 있다. 안 끊으면 시체의 도착 콜백이 나중에 돌아
            // 카메라를 <b>시체에게</b> 넘겨 버린다.
            EntranceDirector.Cancel(dead);

            // 시체는 날려 보내지 않는다 — 사망 연출과 디스폰이 스스로 끝나야 한다.
            // 들어오는 쪽만 연출을 받는다. 그 0.35초는 판정이 꺼져 있어 안전하기도 하다.
            FlyIn(roster[next], seat);
            Possess(roster[next]);

            CurrentIndex = next;
            cooldownTimer = swapCooldown;
            lastSeat = seat;
        }
    }
}
