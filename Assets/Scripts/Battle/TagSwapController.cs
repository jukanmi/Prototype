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
    /// <item><b>빙의 전환</b> — 올라온 몸이 <see cref="PlayerControl"/>을 쓰게 하고,
    /// 나머지는 <see cref="AllyControl"/>(자율 BT)로 돌린다.
    /// <see cref="Entity.UseControl{T}"/>가 그 스위치다.</item>
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
        /// 사망 구독. <see cref="Combat.OnDead"/>가 인자를 주지 않아 몸마다 클로저를
        /// 하나씩 만든다 — <see cref="BulletTimeController"/>가 쓰는 방식과 같다.
        /// </summary>
        private readonly List<(Combat combat, Action handler)> deathHooks = new List<(Combat, Action)>();

        /// <summary>지금 조작 중인 몸의 로스터 칸. 아무도 못 세웠으면 -1.</summary>
        public int CurrentIndex { get; private set; } = -1;

        public IReadOnlyList<Entity> Roster => roster;

        public Entity Current
            => CurrentIndex >= 0 && CurrentIndex < roster.Count ? roster[CurrentIndex] : null;

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

        private void SwapTo(int index)
        {
            Entity outgoing = Current;
            Entity incoming = roster[index];

            // 새 몸은 나가는 몸의 자리와 방향을 그대로 물려받는다. 첫 배치처럼 물려줄
            // 사람이 없으면 제 자리에 그냥 서고, 그 자리가 이후의 기준이 된다.
            Seat? seat = outgoing != null ? Seat.Of(outgoing) : (Seat?)null;

            if (outgoing != null && !ReferenceEquals(outgoing, incoming))
                Bench(outgoing);

            Summon(incoming, seat);
            Possess(incoming);

            CurrentIndex = index;
            cooldownTimer = swapCooldown;
            lastSeat = Seat.Of(incoming);

            FollowNewBody(incoming);

            BattleLog.Log(LogCategory.State,
                $"<b>태그 교대</b> {BattleLog.Name(outgoing)} → {BattleLog.Name(incoming)} " +
                $"(쿨 {swapCooldown:0.#}s)", this);
        }

        /// <summary>카메라와 조준 기준을 새 몸으로 옮긴다.</summary>
        private void FollowNewBody(Entity body)
        {
            if (body == null) return;

            cameraFollow?.SetTarget(body.transform);
            targetSelector?.SetCursorOrigin(body.transform);
        }

        // ── 빙의 ────────────────────────────────────────────

        /// <summary>
        /// 조작권을 옮긴다. 새 몸은 유저가 몰고, 나머지는 자율 BT로 돌린다.
        ///
        /// 플레이어 몸에는 <see cref="AllyControl"/>이 없다 — 그쪽은
        /// <see cref="Entity.UseControl{T}"/>가 null을 돌려주며 아무도 안 모는 상태가 되고,
        /// 불릿타임에 불려 나와도 서 있기만 한다. 의도된 동작이다.
        /// </summary>
        private void Possess(Entity incoming)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                Entity e = roster[i];
                if (e == null) continue;

                if (ReferenceEquals(e, incoming)) e.UseControl<PlayerControl>();
                else e.UseControl<AllyControl>();
            }
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
                stageAnchor = Current != null ? Seat.Of(Current) : lastSeat;

                // 조작 중인 몸이 곧 시전자면 껐다 켤 이유가 없다. 태그 교대로 동료를 몰고 있을 때
                // 그 동료의 카드가 나가는 경우다 — 껐다 켜면 OnDisable → ReleaseBody가 헛돈다.
                if (!ReferenceEquals(Current, caster)) BenchStage(Current);
            }

            // 예약된 퇴장을 실행한다. 다시 불려 나온 몸은 내리지 않는다.
            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(leaving[i], caster)) continue;
                BenchStage(leaving[i]);
            }
            leaving.Clear();

            bool alreadyUp = onStage.Contains(caster);

            // 마지막 칸이 곧 "지금 시전 중"이다. 이미 서 있던 몸도 맨 뒤로 옮긴다.
            onStage.Remove(caster);
            onStage.Add(caster);

            if (!alreadyUp)
                Summon(caster, stageAnchor);

            Reseat();

            // 불려 나온 몸을 유저가 몰면 같은 입력으로 두 몸이 움직인다.
            if (!ReferenceEquals(caster, Current)) caster.UseControl<AllyControl>();

            // 카메라는 지금 때리는 쪽을 본다. 숨은 조작 캐릭터를 계속 보면
            // 시전자가 돌진해 나간 뒤 화면에 아무것도 안 남는다.
            cameraFollow?.SetTarget(caster.transform);
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
                if (last != null) home = Seat.Of(last);
            }

            for (int i = 0; i < onStage.Count; i++) BenchStage(onStage[i]);
            for (int i = 0; i < leaving.Count; i++) BenchStage(leaving[i]);

            onStage.Clear();
            leaving.Clear();

            // 무대를 세운 적이 없으면 되돌릴 것도 없다 — 조작 캐릭터는 계속 서 있었다.
            if (stageAnchor == null) return;

            Seat spot = home ?? stageAnchor.Value;
            stageAnchor = null;

            // Current를 다시 읽는다. 콤보 도중 사망 자동교대가 돌았으면 다른 몸일 수 있다.
            Entity back = Current;
            if (back == null) return;

            Summon(back, spot);
            Possess(back);

            lastSeat = Seat.Of(back);
            FollowNewBody(back);
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

        /// <summary>
        /// 무대에서 내린다. 죽은 몸은 그대로 둔다 —
        /// 여기서 끄면 사망 연출과 디스폰이 통째로 잘린다(<see cref="BenchOthers"/>와 같은 이유).
        /// </summary>
        private void BenchStage(Entity body)
        {
            if (body == null || body.Combat == null || body.Combat.IsDead) return;
            Bench(body);
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

        private void Bench(Entity body)
        {
            if (body == null || !body.gameObject.activeSelf) return;
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
            Seat seat = Seat.Of(dead);
            CurrentIndex = -1;

            Summon(roster[next], seat);
            Possess(roster[next]);

            CurrentIndex = next;
            cooldownTimer = swapCooldown;
            lastSeat = seat;

            FollowNewBody(roster[next]);
        }
    }
}
