using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 아레나 하나에서 도는 <b>라운드 사이클</b>.
    ///
    /// <code>
    ///   진입 → 락 → 예고 → 스폰 → 전투 → 클리어 판정 → 정비 → 언락
    /// </code>
    ///
    /// 진입은 <see cref="StageRunner"/>가 트리거 라인에서 알려 준다. 락은 두 가지를
    /// 동시에 한다 — 경계 소유자에게 아레나 경계를 넘겨 카메라를 좁히고, 입구 문을 닫는다.
    /// 되돌아갈 수 없다는 걸 <b>보여 줘야</b> 플레이어가 갇힌 걸 버그로 오해하지 않는다.
    ///
    /// 정비 구간(<see cref="maintenanceSeconds"/>)은 지금은 빈 시간이다.
    /// 죽은 동료 소환과 파티 재배치가 들어갈 자리이고, 그때까지는 숨 돌리는 틈으로 둔다.
    /// </summary>
    public class ArenaDirector : MonoBehaviour
    {
        private enum Phase
        {
            /// <summary>아직 플레이어가 안 들어왔다.</summary>
            Waiting,

            /// <summary>락 · 예고 · 스폰 · 전투. 이 안에서 라운드가 다 돈다.</summary>
            Running,

            /// <summary>클리어. 정비 시간을 세는 중.</summary>
            Maintenance,

            /// <summary>문이 열렸다. 이 아레나는 끝.</summary>
            Done,
        }

        [Tooltip("이 아레나가 속한 스테이지 번호. 라운드 표를 (스테이지, 아레나)로 찾는다.")]
        [SerializeField] private int stageNumber = StageWaveCatalog.ArenaStageNumber;

        [Tooltip("1부터. 인스펙터 라운드가 비어 있으면 이 번호로 ArenaRoundCatalog를 읽는다.")]
        [SerializeField] private int arenaNumber = 1;

        [Tooltip("비우면 ArenaRoundCatalog의 표를 쓴다.")]
        [SerializeField] private ArenaRound round;

        [Header("경계")]
        [SerializeField] private float minX = -6f;
        [SerializeField] private float maxX = 6f;

        [Header("문")]
        [Tooltip("들어온 쪽 문. 락과 동시에 닫히고 다시 열리지 않는다.")]
        [SerializeField] private ArenaGate entryGate;
        [Tooltip("나가는 쪽 문. 락과 동시에 닫히고 클리어하면 열린다. 마지막 아레나는 비워 둔다.")]
        [SerializeField] private ArenaGate exitGate;

        [Header("템포")]
        [Tooltip("클리어 직후 숨 돌리는 시간. 동료 소환·재배치가 들어갈 자리다.")]
        [SerializeField] private float maintenanceSeconds = 2.5f;

        [SerializeField] private EnemySpawnService spawner;

        /// <summary>아직 나오지 않은 한 기.</summary>
        private struct Pending
        {
            public EnemyRole role;
            public bool elite;
            public ArenaSpawnPlan plan;
            public bool telegraphed;
        }

        private readonly List<Pending> pending = new List<Pending>();

        /// <summary>이 라운드가 낳은 적. 클리어 판정은 씬 전체가 아니라 이 목록으로 한다.</summary>
        private readonly List<Enemy> spawned = new List<Enemy>();

        private Phase phase = Phase.Waiting;
        private float clock;
        private bool anySpawned;
        private int spawnFailures;

        // ── 바깥에서 보는 상태 ───────────────────────────

        public int ArenaNumber => arenaNumber;
        public bool IsDone => phase == Phase.Done;

        /// <summary>한 기라도 소환했는가. 스테이지 승리 판정의 첫 프레임 보호가 읽는다.</summary>
        public bool HasSpawnedAny => anySpawned;
        public bool IsRunning => phase == Phase.Running || phase == Phase.Maintenance;
        public StageSection Section => StageSection.Of(SectionKind.Arena, minX, maxX);

        /// <summary>이 아레나의 진입 트리거 라인. 왼쪽 경계다.</summary>
        public float TriggerLine => minX;

        private ArenaRound Round => round;

        private void Awake()
        {
            // 인스펙터를 비워 둔 채 만들어진 디렉터도 동작해야 한다.
            if (round == null || round.spawns == null || round.spawns.Length == 0)
                round = ArenaRoundCatalog.For(stageNumber, arenaNumber);

            if (spawner == null) spawner = GetComponent<EnemySpawnService>();
        }

        // ── 진입 · 락 ───────────────────────────────────

        /// <summary>
        /// 트리거 라인을 넘었다. <see cref="StageRunner"/>가 한 번만 부른다.
        /// </summary>
        public void Begin()
        {
            if (phase != Phase.Waiting) return;

            phase = Phase.Running;
            clock = 0f;
            anySpawned = false;
            spawnFailures = 0;

            spawned.Clear();
            pending.Clear();

            // 갇혔다는 걸 보여 준다. 조용히 벽만 세우면 조작이 씹힌 것으로 읽힌다.
            entryGate?.Close();
            exitGate?.Close();

            // 라운드마다 허용치가 다르다. 지난 라운드의 임대가 남아 있으면 정원이 어긋난다.
            EnemyAttackTokens.Pool.Clear();
            EnemyAttackTokens.Pool.SetCapacity(Round.attackTokens);

            Plan();

            Debug.Log($"[Arena {arenaNumber}] {ArenaRoundCatalog.ThemeOf(stageNumber, arenaNumber)} — {Round.label} " +
                      $"(적 {pending.Count}기, 벽 {Round.WallCount}방향, 동시 공격 {Round.attackTokens}기)");
        }

        /// <summary>저작한 묶음을 한 기씩 펼쳐 예약으로 바꾼다.</summary>
        private void Plan()
        {
            if (Round.spawns == null) return;

            for (int s = 0; s < Round.spawns.Length; s++)
            {
                RoundSpawn group = Round.spawns[s];

                for (int i = 0; i < group.Count; i++)
                    pending.Add(new Pending
                    {
                        role = group.role,
                        elite = group.elite,
                        plan = ArenaSpawnPlanner.Plan(in group, i, minX, maxX),
                    });
            }
        }

        // ── 라운드 ──────────────────────────────────────

        private void Update()
        {
            if (phase == Phase.Waiting || phase == Phase.Done) return;

            float dt = TimeControl.DeltaTime;
            if (dt <= 0f) return;

            clock += dt;

            if (phase == Phase.Maintenance)
            {
                if (clock >= maintenanceSeconds) Unlock();
                return;
            }

            TickTelegraphs();
            TickSpawns();

            if (RoundClearRules.IsCleared(Census(), anySpawned || spawnFailures > 0))
                EnterMaintenance();
        }

        /// <summary>예고. 스폰보다 <see cref="ArenaSpawnPlanner.TelegraphLead"/>초 앞선다.</summary>
        private void TickTelegraphs()
        {
            for (int i = 0; i < pending.Count; i++)
            {
                Pending p = pending[i];
                if (p.telegraphed || clock < p.plan.telegraphAt) continue;

                p.telegraphed = true;
                pending[i] = p;

                SpawnTelegraph.Show(p.plan.telegraphPoint,
                                    Mathf.Max(0.1f, p.plan.spawnAt - clock),
                                    MarkSize(p.plan.wall));
            }
        }

        /// <summary>때가 된 예약을 실제 적으로 바꾼다.</summary>
        private void TickSpawns()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (clock < pending[i].plan.spawnAt) continue;

                Pending p = pending[i];
                pending.RemoveAt(i);

                if (spawner == null)
                {
                    spawnFailures++;
                    continue;
                }

                Enemy enemy = spawner.SpawnFromWall(p.role, p.elite, in p.plan);

                if (enemy == null) { spawnFailures++; continue; }

                spawned.Add(enemy);
                anySpawned = true;
            }
        }

        /// <summary>
        /// 지금 이 라운드의 인구조사. <b>네 칸으로 나눠 세는 것 자체가 규칙이다</b> —
        /// 자세한 이유는 <see cref="RoundClearRules"/>에 적어 뒀다.
        /// </summary>
        private RoundCensus Census()
        {
            var census = new RoundCensus { pending = pending.Count };

            for (int i = 0; i < spawned.Count; i++)
            {
                Enemy e = spawned[i];
                if (e == null) continue;

                if (e.Combat.IsDead) { census.dying++; continue; }

                var control = e.GetComponent<EnemyControl>();
                if (control != null && control.IsEntering) census.entering++;
                else census.fighting++;
            }

            return census;
        }

        // ── 클리어 · 정비 · 언락 ─────────────────────────

        private void EnterMaintenance()
        {
            phase = Phase.Maintenance;
            clock = 0f;

            // 소환이 실패한 채로 넘어가면 "적이 안 나왔는데 문이 열렸다"가 된다.
            // 진행은 막지 않는다(막으면 방에 갇힌다) — 대신 원인을 콘솔에 남긴다.
            if (spawnFailures > 0)
                Debug.LogError($"[Arena {arenaNumber}] 소환에 {spawnFailures}번 실패했다. " +
                               "이 라운드의 적 일부 또는 전부가 나오지 않았다 — 위 에러의 배선을 확인할 것.", this);

            Debug.Log($"[Arena {arenaNumber}] 라운드 소탕 — 정비 {maintenanceSeconds:0.#}초");

            // TODO: 정비 — 죽은 동료 소환, 파티 재배치. 지금은 숨 돌리는 틈이다.
        }

        private void Unlock()
        {
            phase = Phase.Done;

            // 입구는 열지 않는다. 되돌아갈 수 있으면 아레나를 나눈 의미가 없다.
            exitGate?.Open();

            Debug.Log($"[Arena {arenaNumber}] 언락 — 출구 개방");
        }

        /// <summary>
        /// 예고 표식의 크기. 좌우 벽은 깊이 방향으로 길고, 앞뒤 벽은 가로로 길다 —
        /// 어느 벽인지가 모양만 봐도 읽혀야 한다.
        /// </summary>
        private static Vector2 MarkSize(SpawnWall wall)
            => ArenaSpawnPlanner.IsHorizontal(wall) ? new Vector2(0.7f, 2.0f) : new Vector2(2.2f, 0.7f);

        /// <summary>빌더가 경계와 문을 물려 준다.</summary>
        public void Configure(int stage, int number, float arenaMinX, float arenaMaxX,
                              ArenaGate entry, ArenaGate exit, EnemySpawnService service)
        {
            stageNumber = stage;
            arenaNumber = number;
            minX = arenaMinX;
            maxX = arenaMaxX;
            entryGate = entry;
            exitGate = exit;
            spawner = service;
        }
    }
}
