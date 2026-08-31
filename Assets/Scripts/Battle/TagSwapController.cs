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
    /// 무대 노릇을 한다 — 카드가 발동되는 순간 그 직업이 앞 시전자가 끝낸 자리에 서고,
    /// 콤보가 끝나면 마지막 시전자가 그대로 조작 캐릭터가 된다.
    ///
    /// <b>자리는 파티 전체가 하나만 쓴다</b>(<see cref="partySeat"/>). 몸마다 좌표를 따로
    /// 들지 않으므로 교대 · 시전 등장이 전부 같은 자리에서 일어나고, 되돌아갈 자리도 없다.
    /// </summary>
    public class TagSwapController : MonoBehaviour, ICasterStage
    {
        [Header("참조")]
        [SerializeField] private Player player;
        [SerializeField] private BulletTimeController bulletTime;

        [Tooltip("비우면 씬에서 찾는다. 앵커가 없는 씬(스킬 실험용)에서만 쓰는 폴백이다.")]
        [SerializeField] private CameraFollow cameraFollow;

        [Tooltip("카메라가 바라보는 앵커. 있으면 카메라 대상을 직접 안 바꾸고 여기만 옮긴다.")]
        [SerializeField] private CameraAnchor cameraAnchor;

        [Tooltip("비우면 씬에서 찾는다. 조준 기준점을 새 몸으로 옮긴다.")]
        [SerializeField] private TargetSelector targetSelector;

        [Tooltip("비우면 씬에서 찾는다. 유저가 모는 몸을 여기에 넘긴다.")]
        [SerializeField] private PlayerPilot pilot;

        [Header("교대")]
        [Tooltip("교대 후 다시 교대할 수 있을 때까지의 시간.")]
        [SerializeField] private float swapCooldown = 1.5f;

        /// <summary>플레이어 + 동료 4명. Start에서 한 번 만든다.</summary>
        private readonly List<Entity> roster = new List<Entity>();

        /// <summary>지금 무대에 선 몸. <b>마지막 칸이 시전 중인 몸</b>이다.</summary>
        private readonly List<Entity> onStage = new List<Entity>();

        /// <summary>
        /// <see cref="ICasterStage.Exit"/>를 받았지만 아직 안 내린 몸.
        ///
        /// 곧바로 내리지 않는 이유는 같은 시전자가 연속 슬롯에 나올 때 한 프레임
        /// 깜빡이기 때문이다 — 그 사이 <c>OnDisable → ReleaseBody</c>가 돌아
        /// 다음 슬롯의 스킬 상태가 통째로 날아간다.
        /// </summary>
        private readonly List<Entity> leaving = new List<Entity>();

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
        }

        /// <summary>
        /// 파티가 공유하는 <b>단 하나의 자리</b>. 필드에 선 몸이 움직인 만큼 따라간다.
        /// 몸마다 좌표를 따로 두지 않으므로 교대 · 시전 등장은 전부 여기서 일어난다.
        /// </summary>
        private Seat partySeat;

        /// <summary>
        /// <see cref="SeedSeat"/>로 자리를 받았는가. 받았으면 <b>첫 배치가 그 자리에서</b> 일어난다.
        ///
        /// 파티가 프리팹 안으로 들어가면서 필요해졌다 — 예전에는 씬에 놓인 몸의 좌표가 곧
        /// 시작 자리였지만, 이제 그 좌표는 프리팹 좌표(원점)라 모든 방에서 한가운데서 시작하게 된다.
        /// </summary>
        private bool hasSeed;

        /// <summary>
        /// 사망 구독. <see cref="Combat.OnDead"/>가 인자를 주지 않아 몸마다 클로저를
        /// 하나씩 만든다 — <see cref="BulletTimeController"/>가 쓰는 방식과 같다.
        /// </summary>
        private readonly List<(Combat combat, Action handler)> deathHooks = new List<(Combat, Action)>();

        /// <summary>지금 조작 중인 몸의 로스터 칸. 아무도 못 세웠으면 -1.</summary>
        public int CurrentIndex { get; private set; } = -1;

        public IReadOnlyList<Entity> Roster => roster;

        public Entity Current
            => CurrentIndex >= 0 && CurrentIndex < roster.Count ? roster[CurrentIndex] : null;

        /// <summary>
        /// 파티가 서 있는 바닥 좌표. <see cref="Current"/>의 트랜스폼을 직접 읽지 말고 이쪽을 쓸 것 —
        /// 콤보 중에는 조작 캐릭터가 꺼져 있고 무대에 선 시전자가 그 자리를 들고 있다
        /// (<see cref="WaveSpawnPlanner.DepthFor"/>).
        /// </summary>
        public Vector3 ControlledGround => CurrentSeat().Ground;

        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);

        /// <summary>교대 키가 지금 먹히는가. 실시간이 아니거나 쿨이 남았으면 false.</summary>
        public bool CanSwap
            => cooldownTimer <= 0f
               && bulletTime != null
               && bulletTime.Phase == TacticPhase.RealTime;

        /// <summary>
        /// 로스터 0번을 꽂는다. <see cref="PartyAssembler"/>가 <c>Awake</c>(-200)에서 부르므로
        /// 이 컴포넌트의 <see cref="Awake"/>(0)보다 <b>먼저</b> 도착하고, 아래 폴백은 건너뛴다.
        ///
        /// 동료마다 프리팹이 갈리면서 필요해졌다 — 주인공이 런타임 생성물이 되어
        /// <c>BattleInputBuilder</c>가 프리팹에 구워 두던 참조가 더는 없다.
        /// </summary>
        public void SetHero(Player hero)
        {
            if (hero != null) player = hero;
        }

        private void Awake()
        {
            // 폴백은 남긴다. 파티 없이 도는 스킬 실험 씬이 이 줄로 살아 있고,
            // PartyAssembler 가 -200 에서 몸을 먼저 만들므로 여기서도 안전하게 잡힌다.
            if (player == null) player = FindAnyObjectByType<Player>();
            if (bulletTime == null) bulletTime = FindAnyObjectByType<BulletTimeController>();
            if (cameraFollow == null) cameraFollow = FindAnyObjectByType<CameraFollow>();
            if (cameraAnchor == null) cameraAnchor = GetComponentInChildren<CameraAnchor>(true);
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
        /// </summary>
        public bool EnsureActive(Entity body)
        {
            if (body == null) return false;
            if (ReferenceEquals(body, Current) && body.gameObject.activeSelf) return true;

            int index = roster.IndexOf(body);
            if (!TagSwapRules.IsSelectable(roster, index)) return false;

            SwapTo(index);
            return true;
        }

        /// <summary>내려가는 몸은 끄고 올라오는 몸을 같은 자리에 세운다.</summary>
        private void SwapTo(int index)
        {
            Entity outgoing = Current;
            Entity incoming = roster[index];

            // 새 몸은 나가는 몸의 자리를 그대로 물려받는다. 파티는 자리를 하나만 쓴다.
            // 물려줄 사람이 없는 첫 배치는 씬이 정한 자리(SeedSeat)를 쓰고,
            // 그것도 없으면 새 몸이 이미 선 자리가 그 자리가 된다.
            Seat seat = outgoing != null ? Seat.Of(outgoing)
                      : hasSeed ? partySeat
                      : Seat.Of(incoming);

            if (outgoing != null && !ReferenceEquals(outgoing, incoming)) Bench(outgoing);

            Summon(incoming, seat);
            Possess(incoming);

            CurrentIndex = index;
            cooldownTimer = swapCooldown;
            partySeat = seat;
            FollowBody(incoming);

            BattleLog.Log(LogCategory.State,
                $"<b>태그 교대</b> {BattleLog.Name(outgoing)} → {BattleLog.Name(incoming)} " +
                $"(쿨 {swapCooldown:0.#}s)", this);
        }

        /// <summary>
        /// 이 스테이지에서 파티가 처음 설 자리를 받는다. <see cref="PartySpawnPoint"/>를 읽은
        /// <see cref="PartyAssembler"/>가 <c>Awake</c>에서 부른다 — <see cref="Start"/>보다 앞이다.
        ///
        /// <b>방향까지 받는 이유</b>는 <see cref="Prototype.Physics.Teleport"/>가 방향을 안 건드리기
        /// 때문이다. 자리만 주면 첫 몸이 프리팹에 저장된 방향 그대로 나와 등을 보이고 설 수 있다.
        /// </summary>
        public void SeedSeat(Vector3 ground, Vector3 facing)
        {
            partySeat = new Seat(ground, facing.sqrMagnitude > 0.0001f ? facing : Vector3.right);
            hasSeed = true;

            // 여기서 직접 찾는다. 부르는 쪽(PartyAssembler)이 실행 순서 -200이라
            // 이 컴포넌트의 Awake보다 먼저 도착한다 — Awake의 폴백에 기대면 null이다.
            if (cameraAnchor == null) cameraAnchor = GetComponentInChildren<CameraAnchor>(true);

            // 앵커도 같이 옮긴다. 안 그러면 카메라가 원점에서 스폰 자리까지 한 번 미끄러진다 —
            // 스테이지가 열리는 첫 0.5초가 통째로 흘러가는 그림이 된다.
            cameraAnchor?.SnapTo(ground.x);
        }

        /// <summary>파티가 서 있는 자리. 필드에 아무도 없으면 마지막으로 확정된 자리.</summary>
        private Seat CurrentSeat()
        {
            Entity body = Current;
            return body != null ? Seat.Of(body) : partySeat;
        }

        /// <summary>
        /// 카메라와 조준 기준을 새 몸으로 옮긴다. <b>착지한 뒤에</b> 부른다.
        ///
        /// <b>조준 기준은 앵커를 안 거친다.</b> 커서 원점이 보간되면 몸보다 늦게 따라와
        /// 조준이 밀린다 — 카메라는 부드러워야 하고 조준은 즉각적이어야 한다.
        /// </summary>
        private void FollowBody(Entity body)
        {
            if (body == null) return;

            // 앵커가 있으면 카메라 대상은 영영 앵커다. 여기서는 무엇을 비출지만 바꾼다.
            if (cameraAnchor != null) cameraAnchor.SetFocus(body.transform);
            else cameraFollow?.SetTarget(body.transform);

            targetSelector?.SetCursorOrigin(body.transform);
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
        /// <b>앞 시전자가 끝낸 자리에 선다</b> — 콤보 시작 자리를 붙들고 있으면
        /// 돌진해 나간 만큼이 슬롯마다 되감긴다.
        /// </summary>
        void ICasterStage.Enter(Ally caster)
        {
            if (caster == null) return;

            // 지금 무대에 선 몸이 곧 다음 자리다. 아무도 없으면 조작 캐릭터 자리.
            Entity prev = onStage.Count > 0 ? onStage[onStage.Count - 1]
                        : leaving.Count > 0 ? leaving[leaving.Count - 1] : Current;
            if (prev != null && prev.gameObject.activeSelf) partySeat = Seat.Of(prev);

            // 조작 중인 몸이 곧 시전자면 껐다 켤 이유가 없다 — OnDisable → ReleaseBody가 헛돈다.
            if (!ReferenceEquals(Current, caster)) Bench(Current);

            // 예약된 퇴장을 실행한다. 다시 불려 나온 몸은 내리지 않는다.
            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(leaving[i], caster)) continue;
                Bench(leaving[i]);
            }
            leaving.Clear();

            bool alreadyUp = onStage.Contains(caster);

            // 마지막 칸이 곧 "지금 시전 중"이다. 이미 서 있던 몸도 맨 뒤로 옮긴다.
            onStage.Remove(caster);
            onStage.Add(caster);

            // 이미 무대에 있는 몸은 자리를 다시 잡지 않는다. 돌진으로 전진한 시전자가
            // 다음 슬롯에서 제자리로 튕겨 돌아가면 콤보가 통째로 어색해진다.
            if (!alreadyUp) Summon(caster, partySeat);

            // 카메라는 지금 때리는 쪽을 본다. 숨은 조작 캐릭터를 계속 보면
            // 시전자가 돌진해 나간 뒤 화면에 아무것도 안 남는다.
            FollowBody(caster);
        }

        /// <summary>등장 연출이 없다. 시전자는 부른 그 프레임에 자리에 선다.</summary>
        bool ICasterStage.IsEntering => false;

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

        /// <summary>마지막 시전자가 그 자리에서 조작 캐릭터가 된다. 나머지는 내린다.</summary>
        void ICasterStage.Clear()
        {
            // 정상 종료면 Exit로 leaving에, 중단이면 onStage에 남는다. 둘 다 마지막 칸이다.
            Entity keep = onStage.Count > 0 ? onStage[onStage.Count - 1]
                        : leaving.Count > 0 ? leaving[leaving.Count - 1] : null;

            int index = keep != null ? roster.IndexOf(keep) : -1;
            if (!TagSwapRules.IsSelectable(roster, index)) { keep = null; index = -1; }

            for (int i = 0; i < onStage.Count; i++)
                if (!ReferenceEquals(onStage[i], keep)) Bench(onStage[i]);

            for (int i = 0; i < leaving.Count; i++)
                if (!ReferenceEquals(leaving[i], keep)) Bench(leaving[i]);

            onStage.Clear();
            leaving.Clear();

            if (keep != null)
            {
                CurrentIndex = index;
                Possess(keep);
                partySeat = Seat.Of(keep);
                FollowBody(keep);
                return;
            }

            // 마지막 시전자가 죽었다. 조작 캐릭터를 그 자리에 다시 세운다.
            Entity back = Current;
            if (back == null) return;

            Summon(back, partySeat);
            Possess(back);
            FollowBody(back);
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
        /// 내린다. 트랜스폼은 안 건드린다 — 자리는 파티가 하나만 쓴다.
        /// 죽은 몸은 그대로 둔다. 여기서 끄면 사망 연출과 디스폰이 통째로 잘린다.
        /// </summary>
        private void Bench(Entity body)
        {
            if (body == null || !body.gameObject.activeSelf) return;
            if (body.Combat != null && body.Combat.IsDead) return;

            body.gameObject.SetActive(false);
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

            SwapTo(first);

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
            Seat seat = CurrentSeat();
            CurrentIndex = -1;

            // 시체는 내리지 않는다 — 사망 연출과 디스폰이 스스로 끝나야 한다.
            Summon(roster[next], seat);
            Possess(roster[next]);
            FollowBody(roster[next]);

            CurrentIndex = next;
            cooldownTimer = swapCooldown;
            partySeat = seat;
        }
    }
}
