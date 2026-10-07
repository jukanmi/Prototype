#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    public class AssistSystemTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private Player hero;
        private Ally ally;
        private AssistManager manager;
        private TestSkillData skill;

        [SetUp]
        public void SetUp()
        {
            TimeControl.Reset();
            BattleRegistry.Clear();
            GroundRegistry.Clear();
            hero = NewBody<Player>("Hero");
            ally = NewBody<Ally>("Assist");
            hero.SetParty(new[] { ally });
            var host = new GameObject("Assists");
            objects.Add(host);
            manager = host.AddComponent<AssistManager>();
            manager.Initialize(hero);
            skill = ScriptableObject.CreateInstance<TestSkillData>();
        }

        [TearDown]
        public void TearDown()
        {
            manager.RecallAll();
            foreach (GameObject go in objects) if (go != null) Object.DestroyImmediate(go);
            objects.Clear();
            Object.DestroyImmediate(skill);
            BattleRegistry.Clear();
            TimeControl.Reset();
        }

        private T NewBody<T>(string name) where T : Entity
        {
            var go = new GameObject(name);
            objects.Add(go);
            T body = go.AddComponent<T>();
            InvokeAwake(body.Physics, typeof(Physics));
            InvokeAwake(body.Combat, typeof(Combat));
            InvokeAwake(body, typeof(Entity));
            return body;
        }

        private static void InvokeAwake(object instance, System.Type type)
            => type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);

        private BulletTimeController CreateTacticController()
        {
            manager.gameObject.AddComponent<ComboExecutor>();
            var controller = manager.gameObject.AddComponent<BulletTimeController>();
            controller.SetHero(hero);
            InvokeAwake(controller, typeof(BulletTimeController));
            typeof(BulletTimeController).GetField("manaCost", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, 10f);
            var director = manager.gameObject.AddComponent<CastDirector>();
            director.Configure(hero, controller);
            controller.SetCastDirector(director);
            controller.Tactic.Begin();
            controller.Gauge.Recover(controller.Gauge.MaxValue);
            return controller;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void JumpCancel_ThawsSelectionWithoutConsumingCardsOrResources(bool order)
        {
            var controller = CreateTacticController();
            var card = new ComboCard(skill);
            controller.Hand.Add(card);
            float mana = hero.Energies.GetEnergy(EnergyType.Mana).CurValue;
            float gauge = controller.Gauge.CurValue;
            Assert.That(controller.Tactic.OnBulletTimeKey(), Is.True);
            if (order) controller.Tactic.Tick(0.01f);
            Assert.That(TimeControl.Scale, Is.Zero);
            Assert.That(controller.Tactic.OnCancelKey(), Is.True);
            Assert.That(controller.Phase, Is.EqualTo(TacticPhase.RealTime));
            Assert.That(TimeControl.Scale, Is.EqualTo(1f));
            Assert.That(controller.Hand.GetCard(0), Is.SameAs(card));
            Assert.That(controller.Gauge.CurValue, Is.EqualTo(gauge));
            Assert.That(hero.Energies.GetEnergy(EnergyType.Mana).CurValue, Is.EqualTo(mana));
            Assert.That(manager.BulletMode, Is.False);
            Assert.That(controller.Tactic.OnCancelKey(), Is.False, "Realtime jump is a normal jump");
        }

        [Test]
        public void OriginalBulletTimeKey_ConfirmsAndResumesTimeImmediatelyAndPaysCostOnce()
        {
            var controller = CreateTacticController();
            float mana = hero.Energies.GetEnergy(EnergyType.Mana).CurValue;
            Assert.That(controller.Tactic.OnExecuteKey(), Is.False, "Space must not enter selection");
            Assert.That(controller.Tactic.OnBulletTimeKey(), Is.True);
            controller.Tactic.Tick(0.01f);
            Assert.That(controller.Phase, Is.EqualTo(TacticPhase.Order));
            Assert.That(controller.Tactic.OnBulletTimeKey(), Is.True, "The original bullet-time key still executes the cards");
            Assert.That(controller.Phase, Is.EqualTo(TacticPhase.Resolve));
            Assert.That(TimeControl.Scale, Is.EqualTo(1f));
            Assert.That(controller.Gauge.CurValue, Is.Zero);
            Assert.That(hero.Energies.GetEnergy(EnergyType.Mana).CurValue, Is.EqualTo(mana - 10f));
            Assert.That(controller.Tactic.OnExecuteKey(), Is.False);
            Assert.That(controller.Tactic.OnCancelKey(), Is.False, "Jump during execution is a normal jump");
            controller.Tactic.Tick(0.01f);
            Assert.That(controller.Phase, Is.EqualTo(TacticPhase.RealTime));
        }

        private void Pump(float seconds)
        {
            var control = ally.GetComponent<AssistControlModule>();
            for (float t = 0f; t < seconds; t += 0.05f)
            {
                if (!ally.gameObject.activeSelf) continue;
                control.Tick(0.05f);
                ally.StateMachine.Tick(0.05f);
            }
        }

        [Test]
        public void FrozenBattle_CastTimeContinues()
        {
            TimeControl.Scale = 0f;
            Assert.That(hero.LocalTimeScale, Is.Zero);
            Assert.That(ally.LocalTimeScale, Is.EqualTo(1f));
        }

        [Test]
        public void ReactionClock_OnlyRunsForHitEnemyInCurrentResolution()
        {
            var enemy = NewBody<Enemy>("ReactionEnemy");
            TimeControl.Scale = 0f;
            enemy.BeginAssistReaction();
            Assert.That(enemy.LocalTimeScale, Is.Zero, "Selection does not run reactions");
            TimeControl.BeginAssistResolution();
            Assert.That(enemy.LocalTimeScale, Is.Zero);
            hero.BeginAssistReaction();
            Assert.That(hero.LocalTimeScale, Is.Zero);
            enemy.BeginAssistReaction();
            Assert.That(enemy.LocalTimeScale, Is.EqualTo(1f));
            TimeControl.EndAssistResolution();
            Assert.That(enemy.LocalTimeScale, Is.Zero);
            TimeControl.BeginAssistResolution();
            Assert.That(enemy.LocalTimeScale, Is.Zero, "Earlier impacts must not unfreeze a new selection");
        }

        [Test]
        public void ChainTarget_FollowsMovedEnemy_AndReplacesDeadTarget()
        {
            var enemy = NewBody<Enemy>("ChainEnemy");
            var replacement = NewBody<Enemy>("Replacement");
            BattleRegistry.RegisterEnemy(enemy);
            BattleRegistry.RegisterEnemy(replacement);
            enemy.Physics.Teleport(new Vector3(4f, 0f, 0f));
            replacement.Physics.Teleport(new Vector3(20f, 0f, 0f));
            skill.targeting = TargetingType.GroundPoint;
            var prepared = CastDirector.RefreshChainRequest(new CastRequest(ally, skill,
                TargetInfo.Ground(enemy.transform.position), 1.5f, true));
            enemy.Physics.Teleport(new Vector3(8f, 0f, 2f));
            var refreshed = CastDirector.RefreshChainRequest(prepared);
            Assert.That(refreshed.TargetEntity, Is.SameAs(enemy));
            Assert.That(refreshed.Target.point, Is.EqualTo(enemy.Physics.GroundPosition));
            Assert.That(refreshed.DamageScale, Is.EqualTo(1.5f));
            enemy.Combat.TakeDamage(new DamageData(10000f));
            refreshed = CastDirector.RefreshChainRequest(refreshed);
            Assert.That(refreshed.TargetEntity, Is.SameAs(replacement));
            Assert.That(refreshed.Target.point, Is.EqualTo(replacement.Physics.GroundPosition));
        }

        [Test]
        public void AutomaticCards_SameCompanionQueuesAndReusesBody()
        {
            var completed = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                manager.Queue(new CastRequest(ally, skill, TargetInfo.None), _ => completed.Add(index));
            }
            Pump(4.2f);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, completed);
            Assert.That(hero.gameObject.activeSelf, Is.True);
            Pump(4f);
            Assert.That(ally.gameObject.activeSelf, Is.False);
            CastTicket again = manager.Queue(new CastRequest(ally, skill, TargetInfo.None));
            Pump(4f);
            Assert.That(again.IsDone, Is.True);
        }

        [Test]
        public void ActiveAutomaticCaster_WalksToFormationWithoutDisabling()
        {
            manager.Queue(new CastRequest(ally, skill, TargetInfo.None));
            Pump(3.1f);
            manager.BeginSelection();
            manager.PrepareFormation(new[] { new CastRequest(ally, skill, TargetInfo.None, 1f, true) });
            Assert.That(ally.gameObject.activeSelf, Is.True);
            Pump(4f);
            Assert.That(manager.FormationReady, Is.True);
            Assert.That(ally.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void Formation_OnlyCardParticipantsAppear()
        {
            Ally withoutCard = NewBody<Ally>("NoCard");
            hero.SetParty(new[] { ally, withoutCard });
            manager.Initialize(hero);
            manager.BeginSelection();
            manager.PrepareFormation(new[] { new CastRequest(ally, skill, TargetInfo.None, 1f, true) });
            Assert.That(ally.gameObject.activeSelf, Is.True);
            Assert.That(withoutCard.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Recall_CancelsEachQueuedCardExactlyOnce()
        {
            int count = 0;
            CastTicket first = manager.Queue(new CastRequest(ally, skill, TargetInfo.None), _ => count++);
            CastTicket second = manager.Queue(new CastRequest(ally, skill, TargetInfo.None), _ => count++);
            manager.BeginSelection();
            manager.RecallAll();
            manager.RecallAll();
            Assert.That(count, Is.EqualTo(2));
            Assert.That(first.WasCancelled && second.WasCancelled, Is.True);
            Assert.That(ally.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void EmptySelection_ResumesDeferredAutomaticCards()
        {
            manager.BeginSelection();
            CastTicket ticket = manager.Queue(new CastRequest(ally, skill, TargetInfo.None));
            Assert.That(ally.gameObject.activeSelf, Is.False);
            manager.EndSelection();
            Pump(4f);
            Assert.That(ticket.IsDone, Is.True);
            Assert.That(ticket.WasCancelled, Is.False);
        }

        [Test]
        public void SelectionDuringRetreat_KeepsVisibleCompanionAvailable()
        {
            manager.Queue(new CastRequest(ally, skill, TargetInfo.None));
            Pump(3.7f);
            Assert.That(ally.GetComponent<AssistControlModule>().Phase, Is.EqualTo(AssistPhase.Departing));
            manager.BeginSelection();
            Pump(4f);
            Assert.That(ally.gameObject.activeSelf, Is.True);
            Assert.That(ally.GetComponent<AssistControlModule>().Phase, Is.EqualTo(AssistPhase.Ready));
        }

        [Test]
        public void Assist_IsInvulnerableAndCannotBePiloted()
        {
            var pilot = hero.gameObject.AddComponent<PlayerPilot>();
            pilot.Take(hero);
            pilot.Take(ally);
            Assert.That(pilot.Body, Is.SameAs(hero));
            var hit = new HitData { damageData = new DamageData(10f) };
            Assert.That(ally.Combat.Hit(in hit, hero.Combat), Is.False);
        }

        public class TestSkillData : SkillData
        {
            public override IState CreateState(in SkillContext context) => new TestSkill(this, in context);
        }
        private class TestSkill : SkillState
        {
            private float elapsed;
            public TestSkill(SkillData data, in SkillContext context) : base(data, in context) { }
            public override bool IsFinished => elapsed >= 0.15f;
            public override void Enter() => elapsed = 0f;
            public override void Tick(float dt) => elapsed += dt;
            public override void Exit() { }
        }
    }
}
#endif
