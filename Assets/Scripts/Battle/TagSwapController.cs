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
    /// 불릿타임은 이 시스템을 거치지 않는다. 진입하면 로스터 전원을 세워
    /// <see cref="ComboExecutor"/>가 지금까지처럼 콤보를 물리고, 실행이 끝나면
    /// 조작 중인 한 명만 남기고 다시 내린다.
    /// </summary>
    public class TagSwapController : MonoBehaviour
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

        [Tooltip("불릿타임에 전원을 세울 때의 배치. 조작 중인 몸 기준 상대 좌표이고 " +
                 "그 몸이 보는 방향에 맞춰 좌우가 뒤집힌다.\n\n" +
                 "로스터 칸이 아니라 <등장 순번>으로 쓴다 — 누가 조작 중이든 대형이 같아야 한다. " +
                 "모자라면 마지막 값을 재사용한다.")]
        [SerializeField]
        private Vector3[] benchOffsets =
        {
            new Vector3(-2.4f, 0f, -0.8f),
            new Vector3(-1.2f, 0f,  0.8f),
            new Vector3( 1.2f, 0f, -0.8f),
            new Vector3( 2.4f, 0f,  0.8f),
        };

        /// <summary>플레이어 + 동료 4명. Start에서 한 번 만든다.</summary>
        private readonly List<Entity> roster = new List<Entity>();

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

            public Seat(Vector3 ground, Vector3 facing)
            {
                Ground = ground;
                Facing = facing;
            }

            public static Seat Of(Entity e) => new Seat(e.Physics.GroundPosition, e.Physics.Facing);

            /// <summary>이 자리 기준의 상대 좌표. 보는 방향이 왼쪽이면 좌우를 뒤집는다.</summary>
            public Seat Offset(Vector3 local)
            {
                if (Facing.x < 0f) local.x = -local.x;
                return new Seat(Ground + local, Facing);
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
                bulletTime.Tactic.OnPhaseChanged += HandlePhaseChanged;

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
            if (next == TacticPhase.Freeze)
            {
                SummonAll();
                return;
            }

            // Resolve → RealTime 이 곧 "콤보 실행 완료"다. ResolveState.Tick이
            // Executor.IsRunning 이 내려가야 넘어오기 때문에, 이 전이보다 먼저 내리면
            // 시전 도중인 몸이 사라진다.
            if (prev == TacticPhase.Resolve && next == TacticPhase.RealTime)
                BenchOthers();
        }

        /// <summary>
        /// 로스터 전원을 세운다. 조작 중인 몸은 자리도 조작권도 건드리지 않는다.
        ///
        /// 오프셋을 <b>로스터 칸이 아니라 등장 순번으로</b> 집는 것이 핵심이다.
        /// 칸 번호로 집으면 조작 중인 몸이 건너뛰어지면서 그 칸의 오프셋이 통째로 비어,
        /// 누가 조작 중이냐에 따라 대형이 매번 달라진다.
        /// </summary>
        public void SummonAll()
        {
            Seat anchor = Current != null ? Seat.Of(Current) : lastSeat;

            int order = 0;

            for (int i = 0; i < roster.Count; i++)
            {
                if (i == CurrentIndex) continue;
                if (!TagSwapRules.IsSelectable(roster, i)) continue;

                Entity e = roster[i];
                Summon(e, anchor.Offset(OffsetFor(order)));
                order++;

                // 불려 나온 몸은 유저가 몰지 않는다. 한 프레임이라도 PlayerControl이 켜져 있으면
                // 같은 입력으로 두 몸이 동시에 움직인다.
                e.UseControl<AllyControl>();
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
            body.Physics.Teleport(seat.Value.Ground);

            // Teleport는 방향을 안 건드린다. 여기서 맞추지 않으면 새 몸이
            // 제 지난 방향 그대로 나와 등을 보이고 선다.
            body.Physics.Face(seat.Value.Facing);
        }

        private void Bench(Entity body)
        {
            if (body == null || !body.gameObject.activeSelf) return;
            body.gameObject.SetActive(false);
        }

        private Vector3 OffsetFor(int index)
        {
            if (benchOffsets == null || benchOffsets.Length == 0) return Vector3.zero;
            return benchOffsets[Mathf.Min(index, benchOffsets.Length - 1)];
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
