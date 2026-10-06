using System.Collections.Generic;
using NUnit.Framework;
using Prototype;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.Tests
{
    /// <summary>
    /// 애셋과 씬을 잇는 다리. 웨이브 애셋은 스폰 지점을 <b>이름으로만</b> 부르고,
    /// 그 이름을 실제 좌표로 바꾸는 곳이 보드다.
    ///
    /// 이름 대조라서 실수가 컴파일에 안 걸린다. 여기서 안 잡으면 증상은
    /// "그 적만 엉뚱한 데서 나온다"로 나타나고, 그건 원인에서 아주 멀다.
    ///
    /// 계획서: docs/Wave_Authoring_Refactor_Plan.md (2.2, 3.3)
    /// </summary>
    public class StageWaveBoardTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();
        private StageWaveBoard board;

        /// <summary>
        /// 씬의 방. 보드는 방 크기를 들지 않고 이걸 찾아 읽는다. 기본은 기준 방이라,
        /// 방과 무관한 검사들은 방이 없던 시절과 같은 답을 낸다. 없는 경우는 "방" 절이 따로 본다.
        /// </summary>
        private StageRoom stageRoom;

        /// <summary>
        /// 테스트 전용 미리보기 씬. 보드는 <b>자기 씬</b>의 방을 찾으므로, 스테이지 씬을 열어 둔 채로 돌려도
        /// 그 씬의 방이 섞이지 않게 여기에 따로 세운다.
        /// 일반 씬을 겹쳐 열지 않는 이유 — 제목 없는 씬이 열려 있으면 에디터가 새 씬 추가를 거절한다.
        /// </summary>
        private Scene scene;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();

            board = NewBoard();
            stageRoom = NewRoom(RoomRect.Default);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);

            // 디스크에 없는 ScriptableObject 도 치워야 한다. 안 지우면 에디터가 켜져 있는 동안
            // 계속 살아남아 다음 테스트의 FindObjectsByType 결과까지 흔든다.
            for (int i = 0; i < assets.Count; i++)
                Object.DestroyImmediate(assets[i]);

            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);

            spawned.Clear();
            assets.Clear();
            board = null;
            stageRoom = null;
        }

        // ── 조회 ────────────────────────────────────────

        /// <summary>높이는 접지가 다시 잡으므로 0으로 눌러 넘긴다.</summary>
        [Test]
        public void TryResolve_FlattensHeight()
        {
            board.Configure(new[] { Bind("굴_좌", new Vector3(-3f, 5f, 1.5f)) });

            Assert.That(board.TryResolve("굴_좌", out Vector3 ground), Is.True);
            Assert.That(ground.x, Is.EqualTo(-3f).Within(0.0001f));
            Assert.That(ground.y, Is.EqualTo(0f));
            Assert.That(ground.z, Is.EqualTo(1.5f).Within(0.0001f));
        }

        /// <summary>
        /// 앞뒤 공백은 양쪽 다 무시한다. 인스펙터에서 이름을 붙여 넣다 딸려 온 공백 하나로
        /// 적이 안 나오면, 화면만 봐서는 절대 못 찾는다.
        /// </summary>
        [Test]
        public void TryResolve_IgnoresSurroundingWhitespace()
        {
            board.Configure(new[] { Bind("  굴_좌 ", Vector3.zero) });

            Assert.That(board.TryResolve("굴_좌", out _), Is.True, "표 쪽 공백이 안 걸러졌다");
            Assert.That(board.TryResolve(" 굴_좌", out _), Is.True, "질의 쪽 공백이 안 걸러졌다");
        }

        [Test]
        public void TryResolve_FailsOnUnknownOrEmptyId()
        {
            board.Configure(new[] { Bind("굴_좌", Vector3.zero) });

            Assert.That(board.TryResolve("굴_우", out _), Is.False);
            Assert.That(board.TryResolve("", out _), Is.False);
            Assert.That(board.TryResolve(null, out _), Is.False);
            Assert.That(board.TryResolve("   ", out _), Is.False);
        }

        /// <summary>같은 이름이 여럿이면 먼저 붙은 것을 쓴다. 규칙이 흔들리면 재현이 안 된다.</summary>
        [Test]
        public void TryResolve_FirstBindingWins()
        {
            board.Configure(new[]
            {
                Bind("굴", new Vector3(1f, 0f, 0f)),
                Bind("굴", new Vector3(-1f, 0f, 0f)),
            });

            Assert.That(board.TryResolve("굴", out Vector3 ground), Is.True);
            Assert.That(ground.x, Is.EqualTo(1f).Within(0.0001f));
        }

        /// <summary>
        /// 자리가 안 꽂힌 줄이 뒤의 멀쩡한 줄을 <b>가리면 안 된다.</b>
        /// 가리면 표에는 자리가 보이는데 적은 자동 배치로 나오는 상태가 된다.
        /// </summary>
        [Test]
        public void TryResolve_SkipsUnboundRowWithTheSameId()
        {
            board.Configure(new[]
            {
                new SpawnPointBinding { id = "굴", point = null },
                Bind("굴", new Vector3(2f, 0f, 0f)),
            });

            Assert.That(board.TryResolve("굴", out Vector3 ground), Is.True,
                        "빈 줄이 뒤의 멀쩡한 줄을 가렸다");
            Assert.That(ground.x, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void CanResolve_MatchesTryResolve()
        {
            board.Configure(new[] { Bind("굴_좌", Vector3.zero) });

            Assert.That(board.CanResolve("굴_좌"), Is.True);
            Assert.That(board.CanResolve("굴_우"), Is.False);
        }

        [Test]
        public void EmptyBoard_ResolvesNothing()
        {
            Assert.That(board.SpawnPointCount, Is.EqualTo(0));
            Assert.That(board.TryResolve("굴", out _), Is.False);
            Assert.That(board.Issues(), Is.Empty);
        }

        // ── 검증 ────────────────────────────────────────

        [Test]
        public void CleanTable_HasNoIssues()
        {
            board.Configure(new[]
            {
                Bind("굴_좌", new Vector3(-4f, 0f, 1f)),
                Bind("굴_우", new Vector3(4f, 0f, -1f)),
            });

            Assert.That(board.Issues(), Is.Empty);
        }

        [Test]
        public void EmptyId_IsReported()
        {
            board.Configure(new[] { Bind("   ", Vector3.zero) });

            Assert.That(ProblemsOf(board), Is.EqualTo(new[] { BoardProblem.EmptyId }));
        }

        /// <summary>뒤에 붙은 줄이 잡혀야 한다. 먼저 붙은 줄은 실제로 쓰이는 줄이다.</summary>
        [Test]
        public void DuplicateId_ReportsTheLaterRow()
        {
            board.Configure(new[]
            {
                Bind("굴", new Vector3(1f, 0f, 0f)),
                Bind("굴", new Vector3(-1f, 0f, 0f)),
            });

            List<BoardIssue> issues = board.Issues();

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].problem, Is.EqualTo(BoardProblem.DuplicateId));
            Assert.That(issues[0].row, Is.EqualTo(1));
        }

        [Test]
        public void MissingTransform_IsReported()
        {
            board.Configure(new[] { new SpawnPointBinding { id = "굴", point = null } });

            Assert.That(ProblemsOf(board), Is.EqualTo(new[] { BoardProblem.MissingTransform }));
        }

        /// <summary>
        /// 방 밖 좌표는 소환할 때 안으로 당겨진다. 그래도 말해 줘야 한다 —
        /// 조용히 당기기만 하면 저작자가 자기 실수를 영영 모른다.
        /// </summary>
        [Test]
        public void OutsideRoom_IsReported()
        {
            board.Configure(new[] { Bind("먼_굴", new Vector3(99f, 0f, 0f)) });

            Assert.That(ProblemsOf(board), Is.EqualTo(new[] { BoardProblem.OutsideRoom }));
        }

        /// <summary>자리가 없는 줄은 방 안팎을 따질 것도 없다. 한 줄에 문제 하나만 붙어야 읽힌다.</summary>
        [Test]
        public void MissingTransform_DoesNotAlsoReportOutsideRoom()
        {
            board.Configure(new[] { new SpawnPointBinding { id = "굴", point = null } });

            Assert.That(board.Issues().Count, Is.EqualTo(1));
        }

        /// <summary>어느 줄인지 못 짚으면 씬에서 찾아갈 수가 없다.</summary>
        [Test]
        public void Issues_CarryTheRowAndId()
        {
            board.Configure(new[]
            {
                Bind("굴_좌", Vector3.zero),
                Bind("먼_굴", new Vector3(0f, 0f, 99f)),
            });

            List<BoardIssue> issues = board.Issues();

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].row, Is.EqualTo(1));
            Assert.That(issues[0].id, Is.EqualTo("먼_굴"));
            Assert.That(issues[0].Describe(), Does.Contain("먼_굴"));
        }

        // ── 웨이브 목록 ─────────────────────────────────

        /// <summary>범위 밖은 null이다. 조용히 감싸 돌면 마지막 웨이브가 무한히 반복된다.</summary>
        [Test]
        public void WaveAt_ReturnsNullOutsideRange()
        {
            board.Configure(Encounters(NewWave()));

            Assert.That(board.WaveCount, Is.EqualTo(1));
            Assert.That(board.WaveAt(0), Is.Not.Null);
            Assert.That(board.WaveAt(1), Is.Null);
            Assert.That(board.WaveAt(-1), Is.Null);
        }

        /// <summary>빈 칸을 조용히 건너뛰면 빠뜨린 것과 일부러 비운 것이 구분되지 않는다.</summary>
        [Test]
        public void EmptyWaveSlot_IsReported()
        {
            board.Configure(Encounters((WaveAsset)null));

            Assert.That(ProblemsOf(board), Is.EqualTo(new[] { BoardProblem.EmptyWaveSlot }));
        }

        /// <summary>
        /// 웨이브가 부르는 이름이 표에 없으면 잡아야 한다.
        /// <b>이 검사가 이 보드의 존재 이유 절반이다</b> — 이름 대조는 컴파일에 안 걸린다.
        /// </summary>
        [Test]
        public void UnknownPointId_IsReported()
        {
            board.Configure(new[] { Bind("굴_좌", Vector3.zero) });
            board.Configure(Encounters(NewWave(WaveSpawnEntry.At(EnemyRole.Melee, "굴_우"))));

            List<BoardIssue> issues = board.Issues();

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].problem, Is.EqualTo(BoardProblem.UnknownPointId));
            Assert.That(issues[0].id, Is.EqualTo("굴_우"));
        }

        [Test]
        public void KnownPointId_IsClean()
        {
            board.Configure(new[] { Bind("굴_좌", Vector3.zero) });
            board.Configure(Encounters(NewWave(WaveSpawnEntry.At(EnemyRole.Melee, " 굴_좌 "))));

            Assert.That(board.Issues(), Is.Empty, "공백만 다른 이름이 못 찾는 것으로 잡혔다");
        }

        /// <summary>
        /// 웨이브가 열리자마자 솟는 적은 아무리 표식을 띄워도 읽을 틈이 없다.
        /// 예고를 낼 시간이 없는 땅속 등장은 저작 실수다.
        /// </summary>
        [Test]
        public void BurrowWithNoRoomForTelegraph_IsReported()
        {
            board.Configure(new[] { Bind("굴", Vector3.zero) });
            board.Configure(Encounters(NewWave(WaveSpawnEntry.At(EnemyRole.Melee, "굴", 0f, SpawnMotion.Burrow))));

            List<BoardIssue> issues = board.Issues();

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].problem, Is.EqualTo(BoardProblem.BurrowTooEarly));
        }

        [Test]
        public void BurrowWithEnoughLead_IsClean()
        {
            board.Configure(new[] { Bind("굴", Vector3.zero) });
            board.Configure(Encounters(NewWave(WaveSpawnEntry.At(EnemyRole.Melee, "굴", ArenaSpawnPlanner.TelegraphLead, SpawnMotion.Burrow))));

            Assert.That(board.Issues(), Is.Empty);
        }

        /// <summary>
        /// 런타임은 미구현 조건을 전멸로 접어서 멈추지는 않는다. 그래도 말해 줘야 한다 —
        /// 저작자는 자기가 지정한 조건이 걸린 줄 알고 그 위에 배치를 쌓는다.
        /// </summary>
        [Test]
        public void UnsupportedAdvance_IsReported()
        {
            WaveAsset wave = NewWave(WaveSpawnEntry.Auto(EnemyRole.Melee));
            wave.advance = WaveAdvance.TargetKilled;

            board.Configure(Encounters(wave));

            List<BoardIssue> issues = board.Issues();

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].problem, Is.EqualTo(BoardProblem.UnsupportedAdvance));
            Assert.That(issues[0].Describe(), Does.Contain("TargetKilled"));
        }

        [Test]
        public void AllClearedAdvance_IsClean()
        {
            WaveAsset wave = NewWave(WaveSpawnEntry.Auto(EnemyRole.Melee));
            wave.advance = WaveAdvance.AllCleared;

            board.Configure(Encounters(wave));

            Assert.That(board.Issues(), Is.Empty);
        }

        /// <summary>날아 들어오는 적은 오는 모습 자체가 예고다. 0초 등장도 실수가 아니다.</summary>
        [Test]
        public void FlyInAtZero_IsNotAnIssue()
        {
            board.Configure(Encounters(NewWave(WaveSpawnEntry.Auto(EnemyRole.Melee))));

            Assert.That(board.Issues(), Is.Empty);
        }

        /// <summary>자동 배치 줄은 지점을 안 부른다. 표에 없다고 잡으면 안 된다.</summary>
        [Test]
        public void AutoRows_DoNotNeedAPoint()
        {
            board.Configure(Encounters(NewWave(WaveSpawnEntry.Auto(EnemyRole.Melee))));

            Assert.That(board.Issues(), Is.Empty);
        }

        // ── 방 ──────────────────────────────────────────
        // 계획서: docs/Room_Size_Plan.md (4항 · 3단계)

        /// <summary>
        /// 방에서 도는 조우가 있는데 방이 없으면 잡는다. 디렉터는 이 상태로 조우를 하나도 안 돌리므로,
        /// 여기서 못 잡으면 증상은 "그 스테이지가 열리자마자 끝난다"뿐이다.
        /// </summary>
        [Test]
        public void NoStageRoom_WithRoomEncounter_IsReported()
        {
            RemoveRoom();
            board.Configure(Encounters(NewWave(WaveSpawnEntry.Auto(EnemyRole.Melee))));

            Assert.That(board.NeedsRoom, Is.True);
            Assert.That(ProblemsOf(board), Is.EqualTo(new[] { BoardProblem.NoStageRoom }));
        }

        /// <summary>방이 없으면 방 밖 판정을 줄마다 또 적지 않는다. 원인 하나에 경고 하나여야 읽힌다.</summary>
        [Test]
        public void NoStageRoom_WithPoints_ReportsOnlyOnce()
        {
            RemoveRoom();
            board.Configure(new[]
            {
                Bind("굴_좌", new Vector3(-4f, 0f, 1f)),
                Bind("먼_굴", new Vector3(99f, 0f, 0f)),
            });

            Assert.That(ProblemsOf(board), Is.EqualTo(new[] { BoardProblem.NoStageRoom }));
        }

        /// <summary>아레나만 도는 보드(자리 있는 조우뿐, 지점 없음)는 방을 안 쓴다. 방이 없어도 멀쩡하다.</summary>
        [Test]
        public void NoStageRoom_SiteOnlyBoard_IsClean()
        {
            RemoveRoom();

            var site = Track(new GameObject("Arena_1")).AddComponent<EncounterSite>();

            WaveAsset wave = NewWave(WaveSpawnEntry.AtWall(EnemyRole.Melee, SpawnWall.Front, 0.5f));
            board.Configure(new[] { new StageEncounter { content = wave, site = site } });

            Assert.That(board.NeedsRoom, Is.False);
            Assert.That(board.Issues(), Is.Empty);
        }

        [Test]
        public void EmptyBoard_WithoutRoom_IsClean()
        {
            RemoveRoom();
            Assert.That(board.NeedsRoom, Is.False);
            Assert.That(board.Issues(), Is.Empty);
        }

        /// <summary>
        /// 지점이 방 안인지는 <b>씬의 방</b>으로 판정한다. 원점 기준으로 보면 옮겨진 방에서
        /// 멀쩡한 지점이 방 밖으로, 방 밖 지점이 방 안으로 잡힌다.
        /// </summary>
        [Test]
        public void Points_AreJudgedAgainstTheStageRoom()
        {
            stageRoom.Configure(new RoomRect(14f, 26f, -2f, 4f));

            board.Configure(new[]
            {
                Bind("방_안", new Vector3(20f, 0f, 1f)),
                Bind("원점", Vector3.zero),
            });

            List<BoardIssue> issues = board.Issues();

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].problem, Is.EqualTo(BoardProblem.OutsideRoom));
            Assert.That(issues[0].id, Is.EqualTo("원점"), "기준 방 안인 원점이 옮겨진 방에서도 방 안으로 잡혔다");
        }

        /// <summary>반깊이 1이면 소환 가능 깊이 ±0.4 — 마법사가 플레이어 줄을 피할 자리가 없다.</summary>
        [Test]
        public void ShallowRoom_IsReported()
        {
            stageRoom.Configure(new RoomRect(-6f, 6f, -1f, 1f));
            board.Configure(Encounters(NewWave(WaveSpawnEntry.Auto(EnemyRole.Melee))));

            Assert.That(ProblemsOf(board), Is.EqualTo(new[] { BoardProblem.RoomTooSmall }));
        }

        // ── 도우미 ──────────────────────────────────────

        /// <summary>테스트 씬으로 옮기고 치울 목록에 올린다.</summary>
        private GameObject Track(GameObject go)
        {
            SceneManager.MoveGameObjectToScene(go, scene);
            spawned.Add(go);
            return go;
        }

        private StageRoom NewRoom(RoomRect baseRoom)
        {
            var room = Track(new GameObject("StageRoom")).AddComponent<StageRoom>();
            room.Configure(baseRoom);
            return room;
        }

        /// <summary>방이 없는 씬을 만든다. 보드가 씬을 훑어 찾으므로 지우는 것 말고는 방법이 없다.</summary>
        private void RemoveRoom()
        {
            if (stageRoom == null) return;

            spawned.Remove(stageRoom.gameObject);
            Object.DestroyImmediate(stageRoom.gameObject);
            stageRoom = null;
        }

        /// <summary>
        /// 보드는 <b>자기 씬</b>의 방만 본다. 다른 씬(열어 둔 스테이지 씬)에 방이 있어도 짝이 아니다.
        /// </summary>
        [Test]
        public void RoomInAnotherScene_IsNotThisBoardsRoom()
        {
            RemoveRoom();

            var elsewhere = new GameObject("다른 씬의 StageRoom");
            spawned.Add(elsewhere);   // 테스트 씬으로 안 옮긴다 — 지금 열린 씬에 남는다
            elsewhere.AddComponent<StageRoom>().Configure(RoomRect.Default);

            board.Configure(Encounters(NewWave(WaveSpawnEntry.Auto(EnemyRole.Melee))));

            Assert.That(ProblemsOf(board), Is.EqualTo(new[] { BoardProblem.NoStageRoom }));
        }

        /// <summary>웨이브 몇 개를 자리 없는 조우 목록으로 편다. 웨이브 방의 모양이다.</summary>
        private static StageEncounter[] Encounters(params WaveAsset[] waves)
        {
            var list = new StageEncounter[waves.Length];

            for (int i = 0; i < waves.Length; i++)
                list[i] = new StageEncounter { content = waves[i], gapSeconds = 2f };

            return list;
        }

        private WaveAsset NewWave(params WaveSpawnEntry[] spawns)
        {
            var wave = ScriptableObject.CreateInstance<WaveAsset>();
            wave.label = "테스트 웨이브";
            wave.spawns = spawns ?? new WaveSpawnEntry[0];

            assets.Add(wave);
            return wave;
        }

        private StageWaveBoard NewBoard()
            => Track(new GameObject("StageWaveBoard")).AddComponent<StageWaveBoard>();

        private SpawnPointBinding Bind(string id, Vector3 at)
        {
            GameObject go = Track(new GameObject($"SpawnPoint {id}"));
            go.transform.position = at;

            return new SpawnPointBinding { id = id, point = go.transform };
        }

        private static BoardProblem[] ProblemsOf(StageWaveBoard board)
        {
            List<BoardIssue> issues = board.Issues();
            var problems = new BoardProblem[issues.Count];

            for (int i = 0; i < issues.Count; i++) problems[i] = issues[i].problem;

            return problems;
        }
    }
}
