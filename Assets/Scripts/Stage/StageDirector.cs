using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스테이지 한 판의 <b>조우 진행</b>. 씬의 <see cref="StageWaveBoard"/>가 든 목록을 읽어
    /// 때가 되면 적을 소환하고, 다 잡히면 다음 조우를 연다.
    ///
    /// <b>웨이브 방과 아레나가 같은 목록으로 돈다.</b> 갈리는 것은 조우 하나의 속성뿐이다 —
    /// 자리(<see cref="EncounterSite"/>)가 있으면 벽에서 나오고 카메라가 잠기며 문이 닫힌다.
    /// 숨 돌리는 틈도 조우마다 따로 들고 있다.
    ///
    /// <b>승패는 여기서 판정하지 않는다.</b> 그건 <see cref="Prototype.BattleSceneController"/>
    /// 몫이고, 그쪽은 <see cref="WavesRemaining"/> 하나만 물어본다 — 남은 웨이브가 있는데
    /// 적이 0명인 순간(웨이브 사이 빈 구간)에 승리로 판정하면 첫 웨이브만 잡고 스테이지가 끝난다.
    ///
    /// 디렉터가 없는 씬(SampleScene · Stage_Mini · 훈련장)은 지금까지처럼 씬에 놓인 적으로
    /// 그대로 돈다 — 이 컴포넌트는 있으면 웨이브가 생기고, 없으면 아무것도 바꾸지 않는다.
    /// </summary>
    public class StageDirector : StageProgressSource
    {
        [Tooltip("1~5. 로그와 주제 문구에만 쓴다. 어떤 웨이브가 도는지는 씬의 보드가 정한다.")]
        [SerializeField] private int stageNumber = 1;

        [Tooltip("이 방의 웨이브 목록과 스폰 지점 표. 비우면 씬에서 찾는다.")]
        [SerializeField] private StageWaveBoard board;

        [Tooltip("적을 실제로 내놓는 창구. 비우면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private EnemySpawnService spawner;

        /// <summary>아직 나오지 않은 한 기. 웨이브가 열릴 때 통째로 예약된다.</summary>
        private struct Pending
        {
            public EnemyRole role;
            public bool elite;
            public SpawnMotion motion;
            public SpawnPlacement placement;
            public float dueAt;      // 웨이브 경과 시간 기준

            /// <summary>이미 띄웠는가. 매 프레임 다시 띄우면 표식이 쌓인다.</summary>
            public bool telegraphed;
        }

        private readonly List<Pending> pending = new List<Pending>();

        /// <summary>이 웨이브가 낳은 적. 전멸 판정은 씬 전체가 아니라 이 목록으로 한다.</summary>
        private readonly List<Enemy> spawned = new List<Enemy>();

        private int waveIndex = -1;

        /// <summary>열리기를 기다리는 조우. <see cref="waveIndex"/>는 아직 앞 조우를 가리킨다.</summary>
        private int waitingIndex;

        /// <summary>진입선을 넘기를 기다리는 중인가.</summary>
        private bool waitingForLine;

        /// <summary>
        /// 지금 조우가 <b>실제로 돌고 있는가.</b> 조우 사이에는 거짓이다.
        ///
        /// 없으면 안 된다 — 끝난 조우도 <see cref="waveIndex"/>가 가리키고 있어서,
        /// 그것만 보면 카메라가 <b>깬 방에 잠긴 채 안 풀린다.</b> 그러면 플레이어가
        /// 다음 자리로 걸어갈 수가 없고, 증상은 "문은 열렸는데 못 나간다"가 된다.
        /// </summary>
        private bool running;

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

        public int WaveCount => encounters != null ? encounters.Length : 0;

        /// <summary>지금 조우. 시작 전이거나 다 끝났으면 빈 칸이다.</summary>
        private StageEncounter Current
            => waveIndex >= 0 && waveIndex < WaveCount ? encounters[waveIndex] : default;

        /// <summary>지금 웨이브. 시작 전이거나 다 끝났으면 null.</summary>
        public WaveAsset CurrentWave => Current.content;

        /// <summary>
        /// 아직 나올 적이 남았는가. <b>승리 판정이 이 값을 본다</b> —
        /// 웨이브 사이의 빈 구간과 등장 대기 중인 적이 전부 여기 걸린다.
        /// </summary>
        public override bool ThreatsRemaining => !finished;

        public override bool HasSpawnedAny => spawnedAny;

        /// <summary>
        /// 지금 돌고 있는 조우의 자리. 카메라 락이 이 값을 읽는다.
        /// 자리 없는 조우(웨이브 방)와 조우 사이에는 <c>null</c>이다 — 그때는 카메라가 풀린다.
        /// </summary>
        public EncounterSite ActiveSite => running ? Current.site : null;

        private bool spawnedAny;

        /// <summary>
        /// 이번 판이 돌 <b>조우 목록</b>. 씬 보드가 주는 웨이브를 자리 없는 조우로 편 것이다.
        /// 아레나가 합류하면 자리 있는 조우가 여기 섞인다(6단계).
        ///
        /// 애셋은 디스크의 것을 그대로 참조하므로 여기서 값을 고치면 안 된다.
        /// </summary>
        private StageEncounter[] encounters = new StageEncounter[0];

        // ── 수명 ────────────────────────────────────────

        private void Awake()
        {
            if (spawner == null) spawner = GetComponent<EnemySpawnService>();
            if (board == null) board = StageWaveBoard.Find();

            encounters = FromBoard();
        }

        /// <summary>
        /// 씬의 보드에서 목록을 받는다.
        ///
        /// <b>못 받으면 빈 목록이다. 번호로 몰래 폴백하지 않는다.</b>
        /// 3 · 4스테이지가 1번 표로 돌던 버그(<c>c325639f</c>)가 정확히 그 조용한 폴백에서 나왔다.
        /// 웨이브가 하나도 없는 스테이지는 <see cref="Start"/>가 경고를 내고 즉시 끝낸다.
        /// </summary>
        private StageEncounter[] FromBoard()
        {
            if (board == null)
            {
                Debug.LogError($"[StageDirector] {name}: 씬에 StageWaveBoard 가 없다. " +
                               "조우를 하나도 못 돌린다.", this);
                return new StageEncounter[0];
            }

            board.ReportIssues();

            var list = new List<StageEncounter>();

            for (int i = 0; i < board.WaveCount; i++)
            {
                StageEncounter e = board.EncounterAt(i);

                // 빈 칸은 건너뛰되 조용히는 아니다. 보드의 검증이 이미 경고를 냈다.
                if (e.content != null) list.Add(e);
            }

            return list.ToArray();
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

            started = true;
            OpenOrWait(0);
        }

        private void OnDestroy()
        {
            // 씬 경계를 넘어 살아남는 전역이다. BattleRegistry 와 같은 이유로 여기서 비운다.
            EnemyAttackTokens.Pool.ResetAll();
        }

        private void Update()
        {
            if (!started || finished) return;

            // 전면 UI(레벨업 · 덱 편집)가 떠 있는 동안은 웨이브 간격을 세지 않는다.
            // 안 세우면 카드를 고르는 사이에 다음 웨이브가 등 뒤에서 쏟아진다.
            if (GameplayModal.IsOpen) return;

            float dt = TimeControl.DeltaTime;
            if (dt <= 0f) return;

            if (waitingForLine)
            {
                // 진입선을 넘었는가. 넘기 전에는 이 조우가 아예 안 도므로 적도 안 나온다.
                StageEncounter next = encounters[waitingIndex];
                if (next.site != null && PlayerX() >= next.site.TriggerLine) BeginWave(waitingIndex);
                return;
            }

            if (gapLeft > 0f)
            {
                gapLeft -= dt;
                if (gapLeft <= 0f) BeginWave(waitingIndex);
                return;
            }

            waveClock += dt;

            // 예고가 먼저다. 같은 프레임에 표식과 몸이 같이 뜨면 예고가 아니다.
            ReleaseTelegraphs();
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
            waitingIndex = index;
            waitingForLine = false;
            running = true;
            waveClock = 0f;
            reinforcedSoFar = 0;
            reinforceClock = 0f;

            spawned.Clear();
            pending.Clear();
            spawnFailures = 0;

            StageEncounter encounter = encounters[index];
            WaveAsset wave = encounter.content;

            // 갇혔다는 걸 보여 준다. 조용히 벽만 세우면 조작이 씹힌 것으로 읽힌다.
            encounter.site?.CloseGates();

            // 웨이브마다 허용치가 다르다. 지난 웨이브의 임대가 남아 있으면 정원이 어긋나므로 비운다.
            EnemyAttackTokens.Pool.Clear();
            EnemyAttackTokens.Pool.SetCapacity(wave.AttackTokens);

            QueueSpawns(in encounter);

            Debug.Log($"[StageDirector] 웨이브 {CurrentWaveNumber}/{WaveCount} — {wave.label} " +
                      $"(적 {pending.Count}기, 동시 공격 {wave.AttackTokens}기)");

            // 대기 0초짜리는 이번 프레임에 바로 나와야 한다. 안 그러면 웨이브 시작이 한 프레임 빈다.
            ReleaseTelegraphs();
            ReleasePending();
        }

        /// <summary>
        /// 웨이브의 스폰 줄을 전부 예약으로 바꾼다. 한 줄이 한 기다.
        ///
        /// 자리는 두 갈래다. 씬 지점을 부르는 줄은 보드에 물어보고, 나머지는 역할별 규칙에 맡긴다.
        /// 순번(<see cref="WaveLayout.AutoLaneIndices"/>)은 <b>웨이브 전체를 한 번에 보고</b> 매긴다 —
        /// 줄마다 따로 세면 전사 다섯이 같은 깊이 줄에 겹쳐 선다.
        /// </summary>
        private void QueueSpawns(in StageEncounter encounter)
        {
            WaveSpawnEntry[] spawns = encounter.content.spawns;
            if (spawns == null || spawns.Length == 0) return;

            // 자리가 있으면 벽에서 나온다. 좌표가 구간 상대라 방 규칙을 쓰면 엉뚱한 데 선다.
            if (encounter.HasSite)
            {
                QueueFromWalls(in encounter, spawns);
                return;
            }

            QueueInRoom(encounter.content, spawns);
        }

        /// <summary>자리에 묶인 조우. 벽면 분산은 줄이 직접 들고 있다.</summary>
        private void QueueFromWalls(in StageEncounter encounter, WaveSpawnEntry[] spawns)
        {
            EncounterSite site = encounter.site;

            for (int i = 0; i < spawns.Length; i++)
            {
                if (spawns[i].motion != SpawnMotion.FromWall)
                    BattleLog.Warn(LogCategory.State,
                        $"{name}: '{encounter.content.label}'의 {i}번 줄이 {spawns[i].motion} 인데 " +
                        "자리에 묶인 조우다 — 벽에서 나오게 한다.", this);

                SpawnPlacement place = ArenaSpawnPlanner.PlanFromWall(in spawns[i], site.MinX, site.MaxX);

                pending.Add(new Pending
                {
                    role = spawns[i].role,
                    elite = spawns[i].elite,
                    motion = SpawnMotion.FromWall,
                    placement = place,
                    dueAt = place.appearAt,
                });
            }
        }

        /// <summary>
        /// 방 하나가 통째로 자리인 조우. 씬 지점을 부르는 줄은 보드에 물어보고,
        /// 나머지는 역할별 규칙에 맡긴다.
        ///
        /// 순번(<see cref="WaveLayout.AutoLaneIndices"/>)은 <b>조우 전체를 한 번에 보고</b> 매긴다 —
        /// 줄마다 따로 세면 전사 다섯이 같은 깊이 줄에 겹쳐 선다.
        /// </summary>
        private void QueueInRoom(WaveAsset wave, WaveSpawnEntry[] spawns)
        {
            var points = new Vector3[spawns.Length];
            var atPoint = new bool[spawns.Length];

            // 순번을 매기기 <b>전에</b> 지점부터 다 풀어 둔다. 못 찾아 자동으로 떨어진 줄도
            // 순번을 받아야 하는데, 애셋만 보고는 그 줄이 떨어질지 알 수가 없다.
            for (int i = 0; i < spawns.Length; i++)
                atPoint[i] = ResolvePoint(in spawns[i], wave, out points[i]);

            int[] lanes = WaveLayout.AutoLaneIndices(spawns, atPoint);
            float playerZ = PlayerDepth();

            for (int i = 0; i < spawns.Length; i++)
            {
                WaveSpawnEntry entry = spawns[i];

                SpawnPlacement place = atPoint[i]
                    ? WaveSpawnPlanner.PlanAt(in entry, points[i])
                    : WaveSpawnPlanner.PlanAuto(in entry, lanes[i], playerZ);

                pending.Add(new Pending
                {
                    role = entry.role,
                    elite = entry.elite,
                    motion = entry.motion,
                    placement = place,
                    dueAt = place.appearAt,
                });
            }
        }

        /// <summary>
        /// 이 줄이 씬 지점에서 나오는가. 자동 배치 줄이면 그냥 거짓이다.
        ///
        /// 지점을 부르는데 못 찾으면 <b>자동 배치로 떨어뜨리고 경고한다.</b> 소환을 건너뛰면
        /// 그 웨이브는 영영 전멸하지 않고, 원점에 세우면 방 한가운데서 튀어나온다.
        /// 둘 다 화면에 단서가 없다 — 제자리는 아니어도 나오기는 하는 쪽이 낫다.
        /// </summary>
        private bool ResolvePoint(in WaveSpawnEntry entry, WaveAsset wave, out Vector3 ground)
        {
            ground = Vector3.zero;
            if (entry.origin != SpawnOrigin.Point) return false;

            if (!entry.UsesPoint)
            {
                BattleLog.Warn(LogCategory.State,
                    $"{name}: '{wave.label}'에 지점 이름이 빈 줄이 있다 — 자동 배치로 내보낸다.", this);
                return false;
            }

            if (board != null && board.TryResolve(entry.pointId, out ground)) return true;

            BattleLog.Warn(LogCategory.State,
                $"{name}: '{wave.label}'이(가) 부르는 스폰 지점 '{entry.pointId}'을(를) 못 찾았다 — " +
                "자동 배치로 내보낸다.", this);

            return false;
        }

        /// <summary>
        /// 때가 된 예고 표식을 띄운다. <b>몸보다 먼저 도는 단계라 따로 있다.</b>
        ///
        /// 발밑에서 솟는 적은 표식이 없으면 반응할 <i>정보</i>가 아예 없다.
        /// 그래서 예고는 저작 항목이 아니라 <b>배치가 정해서 들고 온다</b>
        /// (<see cref="WaveSpawnPlanner.TelegraphAtFor"/>) — 저작으로 켜고 끄게 두면
        /// 언젠가 꺼진 채로 나가는 웨이브가 생기고, 디렉터가 따로 계산하면
        /// 방과 아레나가 서로 다른 예고 규칙을 갖게 된다.
        /// </summary>
        private void ReleaseTelegraphs()
        {
            for (int i = 0; i < pending.Count; i++)
            {
                Pending p = pending[i];
                if (p.telegraphed || !p.placement.HasTelegraph) continue;
                if (p.placement.telegraphAt > waveClock) continue;

                SpawnTelegraph.Show(p.placement.telegraphPoint,
                                    Mathf.Max(0.1f, p.dueAt - waveClock),
                                    MarkSize(p.placement.wall));

                p.telegraphed = true;
                pending[i] = p;   // 구조체라 다시 넣어야 표시가 남는다
            }
        }

        /// <summary>
        /// 예고 표식의 크기. 좌우 벽은 깊이 방향으로 길고, 앞뒤 벽은 가로로 길다 —
        /// 어느 벽인지가 모양만 봐도 읽혀야 한다. 벽이 아니면 발밑에 깔리는 납작한 자국이다.
        /// </summary>
        private static Vector2 MarkSize(SpawnWall wall)
        {
            if (wall == SpawnWall.None) return BurrowRules.TelegraphSize;

            return ArenaSpawnPlanner.IsHorizontal(wall) ? new Vector2(0.7f, 2.0f)
                                                        : new Vector2(2.2f, 0.7f);
        }

        /// <summary>때가 된 예약을 실제 적으로 바꾼다.</summary>
        private void ReleasePending()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].dueAt > waveClock) continue;

                Pending p = pending[i];
                pending.RemoveAt(i);

                Spawn(p.role, p.elite, p.motion, in p.placement);
            }
        }

        /// <summary>
        /// 지속 리젠. 최종 웨이브에서 "다 잡고 한숨 돌리는" 구간을 없앤다.
        /// <b>상한(<c>reinforceCap</c>)이 없으면 이길 수 없는 방이 된다</b> — 그래서 상한이 먼저다.
        /// </summary>
        private void TickReinforcements(float dt)
        {
            WaveAsset wave = CurrentWave;
            if (wave == null || !wave.HasReinforcements) return;
            if (reinforcedSoFar >= wave.reinforceCap) return;

            // 예약이 다 풀리기 전에는 세지 않는다. 첫 등장과 증원이 겹쳐 쏟아진다.
            if (pending.Count > 0) return;

            reinforceClock += dt;
            if (reinforceClock < wave.reinforceInterval) return;

            reinforceClock = 0f;
            reinforcedSoFar++;

            // 증원은 첫 등장과 순번을 나눠 쓰지 않는다. 예약이 전부 풀린 뒤에만 도는 구간이라
            // 겹칠 상대가 없고, 증원끼리는 자기 번호로 좌우 · 깊이가 갈린다.
            WaveSpawnEntry entry = WaveSpawnEntry.Auto(wave.reinforceRole, SpawnSide.Both);
            SpawnPlacement place = WaveSpawnPlanner.PlanAuto(in entry, reinforcedSoFar, PlayerDepth());

            Debug.Log($"[StageDirector] 증원 {reinforcedSoFar}/{wave.reinforceCap}");
            Spawn(wave.reinforceRole, false, SpawnMotion.FlyIn, in place);
        }

        /// <summary>
        /// 이 웨이브가 끝났는가. <b>판단은 <see cref="EncounterClearRules"/>가 한다</b> —
        /// 여기는 세기만 한다. 조건이 늘어날 자리가 그쪽 한 군데뿐이어야 테스트가 따라온다.
        /// </summary>
        private bool WaveCleared()
        {
            WaveAsset wave = CurrentWave;

            return EncounterClearRules.IsCleared(Census(wave),
                                                 wave != null ? wave.advance : WaveAdvance.AllCleared);
        }

        /// <summary>지금 이 웨이브의 인구조사. 몸을 세는 절반은 <see cref="SpawnCensus"/>가 한다.</summary>
        private EncounterCensus Census(WaveAsset wave)
        {
            var census = new EncounterCensus
            {
                pending = pending.Count,
                spawnFailures = spawnFailures,
                reinforcementsLeft = wave != null && wave.HasReinforcements
                    ? wave.reinforceCap - reinforcedSoFar
                    : 0,
            };

            SpawnCensus.CountBodies(spawned, ref census);

            return census;
        }

        private void CompleteWave()
        {
            Debug.Log($"[StageDirector] 웨이브 {CurrentWaveNumber} 소탕");

            // 입구는 열지 않는다. 되돌아갈 수 있으면 자리를 나눈 의미가 없다.
            Current.site?.OpenExit();

            // 카메라를 먼저 푼다. 안 풀면 문이 열려도 다음 자리로 못 걸어간다.
            running = false;

            // 아레나 방과 같은 자리다 — 한 묶음을 다 잡은 직후, 다음 묶음이 나오기 전.
            // 마지막 웨이브에서도 연다. 승리 판정은 세션이 닫힐 때까지 기다린다.
            LevelUpSession.RequestOpen();

            if (waveIndex + 1 >= WaveCount)
            {
                finished = true;
                Debug.Log("[StageDirector] 모든 웨이브 소탕 완료");
                return;
            }

            // 간격은 <b>끝난 조우</b>가 들고 있다. 지금은 전부 같은 값이지만,
            // 아레나가 합류하면 정비 시간이 자리마다 달라진다.
            float gap = Mathf.Max(0.01f, Current.GapSeconds);

            OpenOrWait(waveIndex + 1, gap);
        }

        /// <summary>
        /// 다음 조우를 <b>열거나, 열릴 때까지 기다린다.</b>
        ///
        /// 무엇이 조우를 여는지는 조우가 들고 있다(<see cref="StageEncounter.Trigger"/>).
        /// 지금 웨이브 방은 첫 조우가 즉시, 나머지가 앞 조우 뒤라 예전과 똑같이 돈다.
        /// 진입선 갈래는 아레나가 합류하는 6단계에서 채운다.
        /// </summary>
        private void OpenOrWait(int index, float gap = 0f)
        {
            if (index >= WaveCount)
            {
                finished = true;
                running = false;
                Debug.Log("[StageDirector] 모든 웨이브 소탕 완료");
                return;
            }

            waitingIndex = index;

            switch (encounters[index].Trigger)
            {
                case EncounterTrigger.Immediate:
                    BeginWave(index);
                    return;

                case EncounterTrigger.CrossLine:
                    // 플레이어가 자리의 진입선을 넘을 때까지 기다린다.
                    // 자리 없는 조우의 진입선 조건은 Trigger 가 이미 접어서 여기 안 온다.
                    gapLeft = 0f;
                    waitingForLine = true;
                    return;

                default:
                    waitingForLine = false;
                    gapLeft = Mathf.Max(0.01f, gap);
                    return;
            }
        }

        // ── 소환 ────────────────────────────────────────

        private void Spawn(EnemyRole role, bool elite, SpawnMotion motion, in SpawnPlacement place)
        {
            if (spawner == null)
            {
                spawnFailures++;
                BattleLog.Warn(LogCategory.State, $"{name}: EnemySpawnService가 없다. 소환을 건너뛴다.", this);
                return;
            }

            Enemy enemy = spawner.Spawn(role, elite, motion, in place);

            if (enemy == null) { spawnFailures++; return; }

            spawned.Add(enemy);
            spawnedAny = true;
        }

        /// <summary>
        /// 지금 조작 중인 몸의 깊이. 돌진전사와 마법사가 이 값을 기준으로 줄을 고른다.
        /// 아무도 없으면 방 한가운데로 본다.
        /// </summary>
        /// <summary>
        /// 조작 중인 몸의 X. 진입선 판정이 읽는다.
        /// 깊이와 같은 자리에서 같은 몸을 집어야 한다 — 두 값이 다른 몸에서 오면
        /// "카메라는 잠겼는데 적이 엉뚱한 줄에 선다"가 된다.
        /// </summary>
        private float PlayerX()
        {
            if (swap == null) swap = FindAnyObjectByType<TagSwapController>();
            if (swap != null && swap.CurrentIndex >= 0) return swap.ControlledGround.x;

            foreach (Entity e in BattleRegistry.Allies)
            {
                if (e == null || !e.isActiveAndEnabled || e.Combat.IsDead) continue;
                return e.transform.position.x;
            }

            return 0f;
        }

        private float PlayerDepth()
        {
            // 태그 시스템이 있으면 그쪽에 묻는다. <b>"조작 중인 몸"을 아는 것은 그쪽뿐이다.</b>
            //
            // 아래 폴백은 등록 목록의 첫 활성 아군을 집는데, 한 번에 한 명만 서 있던 시절엔
            // 그게 곧 조작 캐릭터였다. 지금은 등퇴장 연출로 <b>두 몸이 동시에 활성인 창</b>이
            // 생겨서(교대 릴레이 · 슬롯 전환) 목록 순서에 따라 화면 밖으로 나가는 몸의
            // 깊이를 집을 수 있다. 그러면 그때 뜬 웨이브의 돌진전사가 플레이어가 아닌
            // 엉뚱한 줄에 정렬되고, 증상은 "가끔 돌진이 안 맞는다"로만 보인다.
            if (swap == null) swap = FindAnyObjectByType<TagSwapController>();

            // CurrentIndex를 함께 본다. 로스터를 못 만들었거나(Player 미배선) 전멸한 상태면
            // 그쪽 좌표는 원점이라, 묻는 것보다 아래 폴백이 낫다.
            if (swap != null && swap.CurrentIndex >= 0) return swap.ControlledGround.z;

            // 태그 컨트롤러가 없는 씬(스킬 시험장 · 훈련장)을 위한 폴백. 예전 동작 그대로다.
            foreach (Entity e in BattleRegistry.Allies)
            {
                if (e == null || !e.isActiveAndEnabled || e.Combat.IsDead) continue;
                return e.transform.position.z;
            }

            return 0f;
        }

        /// <summary>조작 중인 몸을 아는 유일한 자리. 비어 있으면 폴백으로 간다.</summary>
        private TagSwapController swap;
    }
}
