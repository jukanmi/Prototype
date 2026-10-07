using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>Serialized scene adapter. The hero is permanently controlled; CastDirector owns assists.</summary>
    [RequireComponent(typeof(AssistManager), typeof(CastDirector))]
    public class TagSwapController : MonoBehaviour
    {
        [SerializeField] private Player player;
        [SerializeField] private BulletTimeController bulletTime;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private CameraAnchor cameraAnchor;
        [SerializeField] private TargetSelector targetSelector;
        [SerializeField] private PlayerPilot pilot;
        private readonly List<Entity> roster = new List<Entity>();
        private Vector3 seedGround;
        private Vector3 seedFacing = Vector3.right;
        private bool hasSeed;
<<<<<<< Updated upstream:Assets/Scripts/Battle/Components/TagSwapController.cs

        /// <summary>
        /// 사망 구독. <see cref="Combat.OnDead"/>가 인자를 주지 않아 몸마다 클로저를
        /// 하나씩 만든다 — <see cref="BulletTimeController"/>가 쓰는 방식과 같다.
        /// </summary>
        private readonly List<(Combat combat, Action handler)> deathHooks = new List<(Combat, Action)>();

        private int currentIndex = -1;

        /// <summary>
        /// 지금 조작 중인 몸의 로스터 칸. 아무도 못 세웠으면 -1.
        /// 바뀔 때마다 <see cref="BattleRegistry.Controlled"/>에 알린다 — 밖에서는 그쪽에 묻는다.
        /// </summary>
        public int CurrentIndex
        {
            get => currentIndex;
            private set
            {
                currentIndex = value;
                BattleRegistry.SetControlled(Current);
            }
        }

=======
>>>>>>> Stashed changes:Assets/Scripts/Battle/TagSwapController.cs
        public IReadOnlyList<Entity> Roster => roster;
        public Entity Current => player;
        public int CurrentIndex => player != null ? 0 : -1;
        public int AliveCount => player != null && !player.Combat.IsDead ? 1 : 0;
        public Vector3 ControlledGround => player != null ? player.Physics.GroundPosition : seedGround;
        public float CooldownRemaining => 0f;
        public bool CanSwap => false;
        public bool SwapNext() => false;
        public bool EnsureActive(Entity body) => body != null && body == player;
        public void SetHero(Player hero) { if (hero != null) player = hero; }
        public void SeedSeat(Vector3 ground, Vector3 facing)
        {
            seedGround = ground;
            seedFacing = facing.sqrMagnitude > 0.0001f ? facing : Vector3.right;
            hasSeed = true;
            if (cameraAnchor == null) cameraAnchor = GetComponentInChildren<CameraAnchor>(true);
            cameraAnchor?.SnapTo(ground.x);
        }
<<<<<<< Updated upstream:Assets/Scripts/Battle/Components/TagSwapController.cs

        public Entity Current
            => CurrentIndex >= 0 && CurrentIndex < roster.Count ? roster[CurrentIndex] : null;

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

=======
>>>>>>> Stashed changes:Assets/Scripts/Battle/TagSwapController.cs
        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<Player>();
            if (bulletTime == null) bulletTime = FindAnyObjectByType<BulletTimeController>();
            if (pilot == null) pilot = FindAnyObjectByType<PlayerPilot>();
            if (cameraFollow == null) cameraFollow = FindAnyObjectByType<CameraFollow>();
            if (cameraAnchor == null) cameraAnchor = GetComponentInChildren<CameraAnchor>(true);
            if (targetSelector == null) targetSelector = FindAnyObjectByType<TargetSelector>();
        }
<<<<<<< Updated upstream:Assets/Scripts/Battle/Components/TagSwapController.cs

        /// <summary>
        /// 구독과 초기 배치는 <b>Start</b>에서 한다. 로스터는 <see cref="PartyAssembler"/>가
        /// Awake에서 몸을 다 만든 뒤에야 완성된다.
        /// </summary>
        private void Start()
        {
            BuildRoster();

            if (bulletTime != null)
            {
                bulletTime.OnPhaseChanged += HandlePhaseChanged;

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
            BattleRegistry.SetControlled(null);

            if (bulletTime != null)
                bulletTime.OnPhaseChanged -= HandlePhaseChanged;

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

=======
        private void Start()
        {
            if (player == null) return;
>>>>>>> Stashed changes:Assets/Scripts/Battle/TagSwapController.cs
            roster.Add(player);
            roster.AddRange(player.Party);
            player.TimeDomain = TimeDomain.Battle;
            player.gameObject.SetActive(true);
            if (hasSeed) { player.Physics.Teleport(seedGround); player.Physics.Face(seedFacing); }
            pilot?.Take(player);
            if (cameraAnchor != null) cameraAnchor.SetFocus(player.transform);
            else cameraFollow?.SetTarget(player.transform);
            targetSelector?.SetCursorOrigin(player.transform);
            CastDirector director = GetComponent<CastDirector>();
            director.Configure(player, bulletTime);
            if (bulletTime != null) bulletTime.SetCastDirector(director);
        }
    }
}
