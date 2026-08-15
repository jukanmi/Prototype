using NUnit.Framework;
using Prototype.YG;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// GameManager 가 들고 있는 스테이지 진행. 씬을 실제로 로드하지는 않고
    /// <b>어디로 가야 하는지</b>를 아는지만 본다.
    ///
    /// 전환 메서드는 SceneLoader 가 없으면 거절한다. 그 거절이 상태를 건드리지 않는지가
    /// 여기서 가장 중요한 항목이다 — 스테이지 번호만 올려 놓고 씬은 안 바뀌면
    /// 미니 스테이지에 서 있는데 다음이 보스가 된다.
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

        /// <summary>미니 → SampleScene → 보스. 이 순서가 곧 게임의 흐름이다.</summary>
        [Test]
        public void DefaultStages_AreMiniThenSampleThenBoss()
        {
            Assert.That(GameManager.DefaultStages, Is.EqualTo(new[]
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
            Assert.That(gm.CurrentStageScene, Is.EqualTo(SceneNames.StageMini));
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
            Assert.That(gm.CurrentStageScene, Is.EqualTo(SceneNames.StageMini));
            Assert.That(gm.IsRunActive, Is.True);
        }

        [Test]
        public void AdvanceStage_WalksTheWholeRoute()
        {
            gm.StartNewRun();
            Assert.That(gm.CurrentStageScene, Is.EqualTo(SceneNames.StageMini));

            gm.AdvanceStage();
            Assert.That(gm.CurrentStageScene, Is.EqualTo(SceneNames.Battle));

            gm.AdvanceStage();
            Assert.That(gm.CurrentStageScene, Is.EqualTo(SceneNames.StageBoss));
        }

        [Test]
        public void NextStageScene_LooksOneAhead()
        {
            gm.StartNewRun();

            Assert.That(gm.HasNextStage, Is.True);
            Assert.That(gm.NextStageScene, Is.EqualTo(SceneNames.Battle));
        }

        [Test]
        public void OnLastStage_HasNoNext()
        {
            gm.StartNewRun();
            gm.AdvanceStage();
            gm.AdvanceStage();

            Assert.That(gm.HasNextStage, Is.False);
            Assert.That(gm.NextStageScene, Is.Null);
        }

        /// <summary>보스를 잡은 뒤 한 칸 더 가면 목록 밖이다. 거기서 멈춰야 한다.</summary>
        [Test]
        public void AdvancePastLastStage_StaysPut()
        {
            gm.StartNewRun();
            for (int i = 0; i < 10; i++) gm.AdvanceStage();

            Assert.That(gm.CurrentStageIndex, Is.EqualTo(gm.StageCount - 1));
            Assert.That(gm.CurrentStageScene, Is.EqualTo(SceneNames.StageBoss));
        }

        [Test]
        public void StageAt_ClampsBothEnds()
        {
            Assert.That(gm.StageAt(-5), Is.EqualTo(SceneNames.StageMini));
            Assert.That(gm.StageAt(99), Is.EqualTo(SceneNames.StageBoss));
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

            Assert.That(gm.GoToNextStage("Stage_Mini"), Is.False);
            Assert.That(gm.CurrentStageIndex, Is.EqualTo(0), "씬은 안 바뀌었는데 번호만 올랐다");
        }

        [Test]
        public void RestartCurrentStage_WithoutSceneLoader_IsRefused()
        {
            gm.StartNewRun();
            gm.AdvanceStage();

            Assert.That(gm.RestartCurrentStage("SampleScene"), Is.False);
            Assert.That(gm.CurrentStageIndex, Is.EqualTo(1), "재시작은 스테이지 번호를 유지한다");
        }

        [Test]
        public void RestartRun_WithoutSceneLoader_IsRefused_AndKeepsIndex()
        {
            gm.StartNewRun();
            gm.AdvanceStage();

            Assert.That(gm.RestartRun("SampleScene"), Is.False);
            Assert.That(gm.CurrentStageIndex, Is.EqualTo(1), "거절됐는데 런이 초기화됐다");
        }

        [Test]
        public void ReturnToMainMenu_WithoutSceneLoader_IsRefused_AndKeepsRun()
        {
            gm.StartNewRun();

            Assert.That(gm.ReturnToMainMenu("Stage_Mini"), Is.False);
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
