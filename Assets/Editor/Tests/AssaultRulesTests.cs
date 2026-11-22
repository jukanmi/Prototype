using System;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 총공격의 두 규칙(<see cref="AssaultRules"/>)과 편성 문(<see cref="AssaultEditGate"/>).
    ///
    /// 팀에서 올라온 버그 다섯 개의 그물이다.
    /// <list type="number">
    /// <item>전투 중에 편성이 됐다 → 전투 칸은 이긴 뒤 · 방 사이 통로에서만, 비전투 칸은 싸움이 없을 때만, 마을은 늘 열린다.</item>
    /// <item>게이지가 덜 찼는데 편성이 됐다 → 편성은 게이지와 무관하게 "칸 안 · 싸움 밖"으로만 갈린다.</item>
    /// <item>편성을 닫자 총공격이 나갔다 · <b>4</b> 평타 치다 나갔다 · <b>5</b> 다음 방에 들어서자 나갔다 →
    /// 총공격은 키를 눌렀을 때만 이 규칙을 지나 나간다. 저절로 검사하는 길이 없다.</item>
    /// </list>
    /// </summary>
    public class AssaultRulesTests
    {
        private FakeAssaultEditGate registered;

        [TearDown]
        public void TearDown()
        {
            if (registered != null) AssaultEditGate.Unregister(registered);
            registered = null;
        }

        private FakeAssaultEditGate RegisterFake(bool editable)
        {
            registered = new FakeAssaultEditGate { Editable = editable };
            AssaultEditGate.Register(registered);
            return registered;
        }

        // ── 발동 ─────────────────────────────────────────

        [Test]
        public void Fire_GoesOnlyWhenEverythingIsReady()
        {
            Assert.That(AssaultRules.CheckFire(running: false, heroDown: false, resourcesReady: true, aliveEnemies: 3),
                Is.EqualTo(AssaultBlock.None));
        }

        [Test]
        public void Fire_GaugeNotFull_DoesNotGo()
        {
            // 게이지가 100이 아니면 키를 눌러도 안 나간다.
            Assert.That(AssaultRules.CheckFire(false, false, resourcesReady: false, aliveEnemies: 3),
                Is.EqualTo(AssaultBlock.Charging));
        }

        [Test]
        public void Fire_NoEnemy_DoesNotWasteTheGauge()
        {
            // 방 사이 통로 · 이긴 뒤 · 상점에서 눌렀다. 나가면 게이지만 날아간다.
            Assert.That(AssaultRules.CheckFire(false, false, true, aliveEnemies: 0), Is.EqualTo(AssaultBlock.NoEnemy));
        }

        [Test]
        public void Fire_WhileAnAssaultRuns_IsRefused()
        {
            Assert.That(AssaultRules.CheckFire(running: true, false, true, 3), Is.EqualTo(AssaultBlock.Busy));
        }

        [Test]
        public void Fire_HeroDown_IsRefused()
        {
            Assert.That(AssaultRules.CheckFire(false, heroDown: true, true, 3), Is.EqualTo(AssaultBlock.HeroDown));
        }

        [Test]
        public void Fire_ReportsTheFirstReasonInOrder()
        {
            // 전부 걸리면 맨 앞(진행 중)이 이유다. 하나씩 풀면 다음 이유가 나온다.
            Assert.That(AssaultRules.CheckFire(true, true, false, 0), Is.EqualTo(AssaultBlock.Busy));
            Assert.That(AssaultRules.CheckFire(false, true, false, 0), Is.EqualTo(AssaultBlock.HeroDown));
            Assert.That(AssaultRules.CheckFire(false, false, false, 0), Is.EqualTo(AssaultBlock.Charging));
            Assert.That(AssaultRules.CheckFire(false, false, true, 0), Is.EqualTo(AssaultBlock.NoEnemy));
        }

        [Test]
        public void Queue_WithoutSlots_IsRefused()
        {
            Assert.That(AssaultRules.CheckQueue(0), Is.EqualTo(AssaultBlock.NoSlots));
            Assert.That(AssaultRules.CheckQueue(1), Is.EqualTo(AssaultBlock.None));
            Assert.That(AssaultRules.CheckQueue(RunProgression.AssaultSlotCount), Is.EqualTo(AssaultBlock.None));
        }

        [Test]
        public void EveryBlock_HasItsOwnLine()
        {
            foreach (AssaultBlock block in Enum.GetValues(typeof(AssaultBlock)))
            {
                string line = AssaultRules.Describe(block);
                Assert.That(line, Is.Not.Empty.And.Not.EqualTo(block.ToString()), $"{block} 의 안내가 없다.");
            }
        }

        // ── 편성 — 전투 칸 ───────────────────────────────

        [TestCase(StageOutcome.Undecided, false, false, false)]   // 방 안에서 싸우는 중
        [TestCase(StageOutcome.Undecided, true, false, true)]     // 방 사이 통로
        [TestCase(StageOutcome.Defeat, true, false, false)]       // 진 판
        [TestCase(StageOutcome.Victory, false, false, true)]      // 이긴 뒤
        [TestCase(StageOutcome.Victory, false, true, false)]      // 이겼어도 칸을 떠나는 중
        [TestCase(StageOutcome.Undecided, true, true, false)]     // 통로여도 떠나는 중
        public void BattleNode_AllowsEditOnlyOutsideAFight(StageOutcome outcome, bool betweenRooms, bool leaving, bool expected)
        {
            Assert.That(AssaultRules.BattleNodeAllowsEdit(outcome, betweenRooms, leaving), Is.EqualTo(expected));
        }

        // ── 편성 — 비전투 칸 ─────────────────────────────

        [TestCase(false, false, false, true)]
        [TestCase(true, false, false, false)]
        [TestCase(false, true, false, false)]
        [TestCase(false, false, true, false)]
        public void PeacefulNode_AllowsEditOutsideAFight(bool fighting, bool defeated, bool leaving, bool expected)
        {
            Assert.That(AssaultRules.PeacefulNodeAllowsEdit(fighting, defeated, leaving), Is.EqualTo(expected));
        }

        // ── 편성 문 ──────────────────────────────────────

        [Test]
        public void Gate_IsClosed_WhenNoNodeRegistered()
        {
            // 지도 · 마을처럼 칸 지휘자가 없는 씬이다.
            Assume.That(AssaultEditGate.CanEdit, Is.False, "앞 테스트의 편성 문이 남아 있다.");
            Assert.That(AssaultEditGate.CanEdit, Is.False);
        }

        [Test]
        public void Gate_FollowsTheRegisteredNode()
        {
            FakeAssaultEditGate gate = RegisterFake(editable: false);
            Assert.That(AssaultEditGate.CanEdit, Is.False);

            gate.Editable = true;
            Assert.That(AssaultEditGate.CanEdit, Is.True);
        }

        [Test]
        public void Gate_UnregisterByAnotherNode_KeepsTheCurrentOne()
        {
            // 다음 칸이 먼저 문을 걸고, 그 뒤에 옛 칸이 내려가며 뗀다 — 새 칸의 문이 남아야 한다.
            RegisterFake(editable: true);
            AssaultEditGate.Unregister(new FakeAssaultEditGate());

            Assert.That(AssaultEditGate.CanEdit, Is.True);
        }

        [Test]
        public void Gate_UnregisterBySelf_Closes()
        {
            FakeAssaultEditGate gate = RegisterFake(editable: true);
            AssaultEditGate.Unregister(gate);
            registered = null;

            Assert.That(AssaultEditGate.CanEdit, Is.False);
        }

        [Test]
        public void Gate_DestroyedNode_ReadsAsClosed()
        {
            // 떼지 못하고 파괴된 지휘자(에디트 모드에선 OnDisable이 안 돈다)는 닫힌 문이다.
            var go = new GameObject("Node");
            var node = go.AddComponent<NodeSceneController>();
            try
            {
                AssaultEditGate.Register(node);
                Assert.That(AssaultEditGate.CanEdit, Is.True, "싸움이 없는 비전투 칸인데 닫혀 있다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            Assert.That(AssaultEditGate.CanEdit, Is.False, "파괴된 칸의 문이 열려 있다.");
        }

        [Test]
        public void Editor_CannotOpen_WhereTheGateIsClosed()
        {
            RegisterFake(editable: false);
            Assert.That(AssaultEditorUI.CanOpen, Is.False, "싸우는 중인데 편집창이 열린다.");

            AssaultEditorUI.RequestOpen();
            Assert.That(AssaultEditorUI.IsOpen, Is.False, "닫힌 문을 지나 편집창이 열렸다.");
        }

        // ── 안내 글 ──────────────────────────────────────

        [Test]
        public void ReadyLabels_NameTheKey()
        {
            // "다 차면 저절로 나간다"로 읽히면 안 된다 — 눌러야 하는 키가 글에 있어야 한다.
            Assert.That(BulletTimeGaugeWidget.ReadyLabel("Space"), Does.Contain("Space"));
            Assert.That(AssaultEditorUI.SlotTitle("Space"), Does.Contain("Space"));
            Assert.That(BulletTimeGaugeWidget.ReadyLabel("Space"), Does.Not.Contain("자동"));
            Assert.That(AssaultEditorUI.SlotTitle("Space"), Does.Not.Contain("자동"));
        }
    }

    /// <summary>테스트용 편성 문. 칸 지휘자 대신 열림을 손으로 정한다.</summary>
    internal sealed class FakeAssaultEditGate : IAssaultEditGate
    {
        public bool Editable;

        public bool AssaultEditable => Editable;
    }
}
