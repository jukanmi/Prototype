using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// GameManager 가 들고 있는 스테이지 진행. 씬을 실제로 로드하지는 않고
    /// <b>어디로 가야 하는지</b>를 아는지만 본다.
    ///
    /// 전환 메서드는 SceneLoader 가 없으면 거절한다. 그 거절이 상태를 건드리지 않는지가
    /// 여기서 가장 중요한 항목이다 — 스테이지 번호만 올려 놓고 씬은 안 바뀌면
    /// 1스테이지에 서 있는데 다음이 3스테이지가 된다.
    /// </summary>
    public class GameManagerStageTests
    {
        private GameObject host;
        private GameManager gm;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("GameManager");
            gm = host.AddComponent<GameManager>();   // Awake 가 여기서 돈다
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        // ── 기본 목록 ───────────────────────────────────

        /// <summary>
        /// 스테이지 1 → 5. 이 순서가 곧 게임의 흐름이자 학습 곡선이다.
        /// 2번과 5번(보스)만 성격이 다르다 — 아레나 2개와 통로로 이뤄진 스크롤 스테이지다.
        /// </summary>
        [Test]
        public void DefaultStages_AreTheFiveMainStages()
        {
            Assert.That(GameManager.DefaultStages, Is.EqualTo(new[]
            {
                SceneNames.Stage01,
                SceneNames.Stage02,
                SceneNames.Stage03,
                SceneNames.Stage04,
                SceneNames.Stage05,
            }));
        }

        /// <summary>런의 마지막은 보스방이다.</summary>
        [Test]
        public void LastStage_IsTheBossStage()
        {
            Assert.That(GameManager.DefaultStages[GameManager.DefaultStages.Length - 1],
                        Is.EqualTo(SceneNames.Stage(StageWaveCatalog.BossStageNumber)));
        }

        /// <summary>배치표와 씬 목록이 어긋나면 마지막 스테이지가 조용히 사라진다.</summary>
        [Test]
        public void DefaultStages_MatchTheCatalog()
        {
            Assert.That(GameManager.DefaultStages.Length, Is.EqualTo(StageWaveCatalog.StageCount));
        }

        /// <summary>씬 하나만 띄워 감각을 보는 짧은 경로. 게임의 흐름과는 다른 물건이다.</summary>
        [Test]
        public void DebugStages_KeepTheOldMiniBossRoute()
        {
            Assert.That(GameManager.DebugStages, Is.EqualTo(new[]
            {
                SceneNames.StageMini,
                SceneNames.Battle,
                SceneNames.StageBoss,
            }));
        }

        /// <summary>
        /// 인스펙터에서 비워 둔 채로 만들어진 GameManager 도 동작해야 한다.
        /// 이 필드가 생기기 전의 Boot 씬이 정확히 그 상태다.
        /// </summary>
        [Test]
        public void EmptyInspectorList_FallsBackToDefault()
        {
            Assert.That(gm.StageCount, Is.EqualTo(GameManager.DefaultStages.Length));
            Assert.That(gm.CurrentStageScene, Is.EqualTo(SceneNames.Stage01));
        }

        /// <summary>기본 배열을 그대로 물면 인스펙터 수정이 static 을 오염시킨다.</summary>
        [Test]
        public void FallbackList_IsACopy_NotTheStaticArray()
        {
            Assert.That(gm.Stages, Is.Not.SameAs(GameManager.DefaultStages));
        }

        // ── 진행 ────────────────────────────────────────

        [Test]
        public void NewRun_StartsAtFirstStage()
        {
            gm.AdvanceStage();
            gm.StartNewRun();

            Assert.That(gm.CurrentStageIndex, Is.EqualTo(0));
            Assert.That(gm.CurrentStageScene, Is.EqualTo(SceneNames.Stage01));
            Assert.That(gm.IsRunActive, Is.True);
        }

        [Test]
        public void AdvanceStage_WalksTheWholeRoute()
        {
            gm.StartNewRun();

            for (int number = 1; number <= StageWaveCatalog.StageCount; number++)
            {
                Assert.That(gm.CurrentStageScene, Is.EqualTo(SceneNames.Stage(number)));
                gm.AdvanceStage();
            }
        }

        [Test]
        public void NextStageScene_LooksOneAhead()
        {
            gm.StartNewRun();

            Assert.That(gm.HasNextStage, Is.True);
            Assert.That(gm.NextStageScene, Is.EqualTo(SceneNames.Stage02));
        }

        [Test]
        public void OnLastStage_HasNoNext()
        {
            gm.StartNewRun();
            for (int i = 1; i < StageWaveCatalog.StageCount; i++) gm.AdvanceStage();

            Assert.That(gm.HasNextStage, Is.False);
            Assert.That(gm.NextStageScene, Is.Null);
        }

        /// <summary>마지막 스테이지 뒤로 한 칸 더 가면 목록 밖이다. 거기서 멈춰야 한다.</summary>
        [Test]
        public void AdvancePastLastStage_StaysPut()
        {
            gm.StartNewRun();
            for (int i = 0; i < 10; i++) gm.AdvanceStage();

            Assert.That(gm.CurrentStageIndex, Is.EqualTo(gm.StageCount - 1));
            Assert.That(gm.CurrentStageScene, Is.EqualTo(SceneNames.Stage05));
        }

        [Test]
        public void StageAt_ClampsBothEnds()
        {
            Assert.That(gm.StageAt(-5), Is.EqualTo(SceneNames.Stage01));
            Assert.That(gm.StageAt(99), Is.EqualTo(SceneNames.Stage05));
        }

        /// <summary>사람에게 보여줄 번호는 1부터다.</summary>
        [Test]
        public void StageNumber_IsOneBased()
        {
            gm.StartNewRun();
            Assert.That(gm.CurrentStageNumber, Is.EqualTo(1));

            gm.AdvanceStage();
            Assert.That(gm.CurrentStageNumber, Is.EqualTo(2));
        }

        // ── 전환 거절 ───────────────────────────────────
        // 테스트에는 SceneLoader 가 없다. 전부 거절되어야 하고, 거절은 상태를 안 건드려야 한다.

        [Test]
        public void GoToNextStage_WithoutSceneLoader_IsRefused_AndKeepsIndex()
        {
            gm.StartNewRun();

            Assert.That(gm.GoToNextStage(SceneNames.Stage01), Is.False);
            Assert.That(gm.CurrentStageIndex, Is.EqualTo(0), "씬은 안 바뀌었는데 번호만 올랐다");
        }

        [Test]
        public void RestartCurrentStage_WithoutSceneLoader_IsRefused()
        {
            gm.StartNewRun();
            gm.AdvanceStage();

            Assert.That(gm.RestartCurrentStage(SceneNames.Stage02), Is.False);
            Assert.That(gm.CurrentStageIndex, Is.EqualTo(1), "재시작은 스테이지 번호를 유지한다");
        }

        [Test]
        public void RestartRun_WithoutSceneLoader_IsRefused_AndKeepsIndex()
        {
            gm.StartNewRun();
            gm.AdvanceStage();

            Assert.That(gm.RestartRun(SceneNames.Stage02), Is.False);
            Assert.That(gm.CurrentStageIndex, Is.EqualTo(1), "거절됐는데 런이 초기화됐다");
        }

        [Test]
        public void ReturnToMainMenu_WithoutSceneLoader_IsRefused_AndKeepsRun()
        {
            gm.StartNewRun();

            Assert.That(gm.ReturnToMainMenu(SceneNames.Stage01), Is.False);
            Assert.That(gm.IsRunActive, Is.True, "거절됐는데 런이 종료됐다");
        }

        // ── 런 수명 ─────────────────────────────────────

        [Test]
        public void EndRun_ClearsActiveFlag()
        {
            gm.StartNewRun();
            gm.EndRun();

            Assert.That(gm.IsRunActive, Is.False);
        }

        [Test]
        public void AddExp_Accumulates()
        {
            gm.StartNewRun();
            gm.AddExp(30);
            gm.AddExp(12);

            Assert.That(gm.TotalExp, Is.EqualTo(42));
        }
    }
}
