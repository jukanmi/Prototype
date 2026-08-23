using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스테이지 한 판의 <b>웨이브 진행</b>. 표(<see cref="StageWaveCatalog"/>)를 읽어
    /// 때가 되면 적을 소환하고, 다 잡히면 다음 웨이브를 연다.
    ///
    /// <b>승패는 여기서 판정하지 않는다.</b> 그건 <see cref="Prototype.YG.BattleSceneController"/>
    /// 몫이고, 그쪽은 <see cref="WavesRemaining"/> 하나만 물어본다 — 남은 웨이브가 있는데
    /// 적이 0명인 순간(웨이브 사이 빈 구간)에 승리로 판정하면 첫 웨이브만 잡고 스테이지가 끝난다.
    ///
    /// 디렉터가 없는 씬(SampleScene · Stage_Mini · 훈련장)은 지금까지처럼 씬에 놓인 적으로
    /// 그대로 돈다 — 이 컴포넌트는 있으면 웨이브가 생기고, 없으면 아무것도 바꾸지 않는다.
    /// </summary>
    public class StageDirector : StageProgressSource
    {
        [Tooltip("1~5. 인스펙터 웨이브 목록이 비어 있으면 이 번호로 StageWaveCatalog를 읽는다.")]
        [SerializeField] private int stageNumber = 1;

        [Tooltip("비우면 StageWaveCatalog의 표를 쓴다. 이 씬만 다르게 굴리고 싶을 때 채운다.")]
        [SerializeField] private WaveDefinition[] waves;

        [Header("템포")]
        [Tooltip("웨이브를 전멸시키고 다음 웨이브가 열리기까지의 숨 돌릴 틈.")]
        [SerializeField] private float waveGap = 2f;

        [Tooltip("적을 실제로 내놓는 창구. 비우면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private EnemySpawnService spawner;

        /// <summary>아직 나오지 않은 한 기. 웨이브가 열릴 때 통째로 예약된다.</summary>
        private struct Pending
        {
            public EnemyRole role;
            public bool elite;
            public SpawnPlacement placement;
            public float dueAt;      // 웨이브 경과 시간 기준
        }

        private readonly List<Pending> pending = new List<Pending>();

        /// <summary>이 웨이브가 낳은 적. 전멸 판정은 씬 전체가 아니라 이 목록으로 한다.</summary>
        private readonly List<Enemy> spawned = new List<Enemy>();

        private int waveIndex = -1;
        private float waveClock;
        private float gapLeft;
        private int reinforcedSoFar;
        private float reinforceClock;

        /// <summary>
        /// 이번 웨이브에서 소환에 실패한 횟수(데이터 · 프리팹 미배선).
        ///
        /// 세어 두지 않으면 전멸 판정이 <c>spawned.Count > 0</c>에 걸려 <b>영원히 안 끝난다</b> —
        /// 화면에는 적이 하나도 없는데 스테이지가 클리어되지 않는, 원인이 전혀 안 보이는 상태가 된다.
        /// </summary>
        private int spawnFailures;
        private bool finished;
        private bool started;

        // ── 바깥에서 보는 상태 ───────────────────────────

        /// <summary>사람에게 보여줄 웨이브 번호. 시작 전이면 0.</summary>
        public int CurrentWaveNumber => waveIndex + 1;

        public int WaveCount => Waves != null ? Waves.Length : 0;

        /// <summary>지금 웨이브. 시작 전이거나 다 끝났으면 null.</summary>
        public WaveDefinition CurrentWave
            => waveIndex >= 0 && waveIndex < WaveCount ? Waves[waveIndex] : null;

        /// <summary>
        /// 아직 나올 적이 남았는가. <b>승리 판정이 이 값을 본다</b> —
        /// 웨이브 사이의 빈 구간과 등장 대기 중인 적이 전부 여기 걸린다.
        /// </summary>
        public override bool ThreatsRemaining => !finished;

        public override bool HasSpawnedAny => spawnedAny;

        private bool spawnedAny;

        private WaveDefinition[] Waves => waves;

        // ── 수명 ────────────────────────────────────────

        private void Awake()
        {
            // 인스펙터를 비워 둔 채 만들어진 디렉터도 동작해야 한다.
            // GameManager.stageScenes 와 같은 폴백 규약이다.
            if (waves == null || waves.Length == 0)
                waves = StageWaveCatalog.For(stageNumber);

            if (spawner == null) spawner = GetComponent<EnemySpawnService>();
        }

        private void Start()
        {
            if (WaveCount == 0)
            {
                BattleLog.Warn(LogCategory.State, $"{name}: 웨이브가 하나도 없다. 이 스테이지는 즉시 끝난다.", this);
                finished = true;
                return;
            }

            Debug.Log($"[StageDirector] 스테이지 {stageNumber} — {StageWaveCatalog.ThemeOf(stageNumber)} " +
                      $"(웨이브 {WaveCount}개)");

            BeginWave(0);
            started = true;
        }

        private void OnDestroy()
        {
            // 씬 경계를 넘어 살아남는 전역이다. BattleRegistry 와 같은 이유로 여기서 비운다.
            EnemyAttackTokens.Pool.ResetAll();
        }

        private void Update()
        {
            if (!started || finished) return;

            float dt = TimeControl.DeltaTime;
            if (dt <= 0f) return;

            if (gapLeft > 0f)
            {
                gapLeft -= dt;
                if (gapLeft <= 0f) BeginWave(waveIndex + 1);
                return;
            }

            waveClock += dt;

            ReleasePending();
            TickReinforcements(dt);

            if (WaveCleared()) CompleteWave();
        }

        // ── 웨이브 ──────────────────────────────────────

        private void BeginWave(int index)
        {
            if (index >= WaveCount)
            {
                finished = true;
                Debug.Log("[StageDirector] 모든 웨이브 소탕 완료");
                return;
            }

            waveIndex = index;
            waveClock = 0f;
            reinforcedSoFar = 0;
            reinforceClock = 0f;

            spawned.Clear();
            pending.Clear();
            spawnFailures = 0;

            WaveDefinition wave = Waves[index];

            // 웨이브마다 허용치가 다르다. 지난 웨이브의 임대가 남아 있으면 정원이 어긋나므로 비운다.
            EnemyAttackTokens.Pool.Clear();
            EnemyAttackTokens.Pool.SetCapacity(wave.attackTokens);

            float playerZ = PlayerDepth();

            if (wave.spawns != null)
            {
                for (int s = 0; s < wave.spawns.Length; s++)
                {
                    WaveSpawn group = wave.spawns[s];

                    for (int i = 0; i < group.Count; i++)
                    {
                        SpawnPlacement place = WaveSpawnPlanner.Plan(in group, i, playerZ);

                        pending.Add(new Pending
                        {
                            role = group.role,
                            elite = group.elite,
                            placement = place,
                            dueAt = place.appearAt,
                        });
                    }
                }
            }

            Debug.Log($"[StageDirector] 웨이브 {CurrentWaveNumber}/{WaveCount} — {wave.label} " +
                      $"(적 {pending.Count}기, 동시 공격 {wave.attackTokens}기)");

            // 대기 0초짜리는 이번 프레임에 바로 나와야 한다. 안 그러면 웨이브 시작이 한 프레임 빈다.
            ReleasePending();
        }

        /// <summary>때가 된 예약을 실제 적으로 바꾼다.</summary>
        private void ReleasePending()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].dueAt > waveClock) continue;

                Pending p = pending[i];
                pending.RemoveAt(i);

                Spawn(p.role, p.elite, in p.placement);
            }
        }

        /// <summary>
        /// 지속 리젠. 최종 웨이브에서 "다 잡고 한숨 돌리는" 구간을 없앤다.
        /// <b>상한(<c>reinforceCap</c>)이 없으면 이길 수 없는 방이 된다</b> — 그래서 상한이 먼저다.
        /// </summary>
        private void TickReinforcements(float dt)
        {
            WaveDefinition wave = CurrentWave;
            if (wave == null || !wave.HasReinforcements) return;
            if (reinforcedSoFar >= wave.reinforceCap) return;

            // 예약이 다 풀리기 전에는 세지 않는다. 첫 등장과 증원이 겹쳐 쏟아진다.
            if (pending.Count > 0) return;

            reinforceClock += dt;
            if (reinforceClock < wave.reinforceInterval) return;

            reinforceClock = 0f;
            reinforcedSoFar++;

            WaveSpawn group = WaveSpawn.Of(wave.reinforceRole, 1, SpawnSide.Both);
            SpawnPlacement place = WaveSpawnPlanner.Plan(in group, reinforcedSoFar, PlayerDepth());

            Debug.Log($"[StageDirector] 증원 {reinforcedSoFar}/{wave.reinforceCap}");
            Spawn(wave.reinforceRole, false, in place);
        }

        /// <summary>이 웨이브가 낳은 적이 전부 죽었고 더 나올 것도 없는가.</summary>
        private bool WaveCleared()
        {
            if (pending.Count > 0) return false;

            WaveDefinition wave = CurrentWave;
            if (wave != null && wave.HasReinforcements && reinforcedSoFar < wave.reinforceCap) return false;

            for (int i = 0; i < spawned.Count; i++)
            {
                Enemy e = spawned[i];
                if (e != null && !e.Combat.IsDead) return false;
            }

            // 한 기도 안 나온 웨이브를 클리어로 치면 표가 잘못됐을 때 스테이지가 그냥 끝난다.
            // 다만 소환 자체가 실패했다면 기다려도 나올 것이 없으므로 넘어간다.
            return spawned.Count > 0 || spawnFailures > 0;
        }

        private void CompleteWave()
        {
            Debug.Log($"[StageDirector] 웨이브 {CurrentWaveNumber} 소탕");

            if (waveIndex + 1 >= WaveCount)
            {
                finished = true;
                Debug.Log("[StageDirector] 모든 웨이브 소탕 완료");
                return;
            }

            gapLeft = Mathf.Max(0.01f, waveGap);
        }

        // ── 소환 ────────────────────────────────────────

        private void Spawn(EnemyRole role, bool elite, in SpawnPlacement place)
        {
            if (spawner == null)
            {
                spawnFailures++;
                BattleLog.Warn(LogCategory.State, $"{name}: EnemySpawnService가 없다. 소환을 건너뛴다.", this);
                return;
            }

            Enemy enemy = spawner.SpawnAtEdge(role, elite, in place);

            if (enemy == null) { spawnFailures++; return; }

            spawned.Add(enemy);
            spawnedAny = true;
        }

        /// <summary>
        /// 지금 조작 중인 몸의 깊이. 돌진전사와 마법사가 이 값을 기준으로 줄을 고른다.
        /// 아무도 없으면 방 한가운데로 본다.
        /// </summary>
        private static float PlayerDepth()
        {
            foreach (Entity e in BattleRegistry.Allies)
            {
                if (e == null || !e.isActiveAndEnabled || e.Combat.IsDead) continue;
                return e.transform.position.z;
            }

            return 0f;
        }
    }
}
