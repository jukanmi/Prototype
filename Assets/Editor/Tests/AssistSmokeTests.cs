using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Prototype.Tests
{
    /// <summary>Runs the actual Update/FixedUpdate lifecycle in an isolated verification project.</summary>
    public class AssistSmokeTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            TimeControl.Reset();
            BattleRegistry.Clear();
        }

        [UnityTest]
        public IEnumerator BulletSequence_ResumesBattle_ReusesActiveCaster_CastsInHandOrderAndRecycles()
        {
            yield return new EnterPlayMode();
            var hero = new GameObject("AssistSmokeHero").AddComponent<Player>();
            var a = new GameObject("AssistSmokeA").AddComponent<Ally>();
            var b = new GameObject("AssistSmokeB").AddComponent<Ally>();
            var noCard = new GameObject("AssistSmokeNoCard").AddComponent<Ally>();
            var enemy = new GameObject("AssistSmokeEnemy").AddComponent<Enemy>();
            enemy.gameObject.AddComponent<BoxCollider>();
            var pilot = new GameObject("AssistSmokePilot").AddComponent<PlayerPilot>();
            pilot.Take(hero);
            hero.SetParty(new[] { a, b, noCard });
            var director = new GameObject("AssistSmokeDirector").AddComponent<CastDirector>();
            director.Configure(hero, null);
            var data = ScriptableObject.CreateInstance<SmokeSkillData>();
            data.castTime = 1f;
            data.recoveryTime = 0f;

            var automatic = director.Cast(new CastRequest(a, data, TargetInfo.None));
            float entryDeadline = Time.realtimeSinceStartup + 5f;
            while (a.GetComponent<AssistControlModule>().Phase != AssistPhase.Casting)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(entryDeadline));
                yield return null;
            }
            Assert.That(a.gameObject.activeSelf, Is.True);
            Ally identity = a;
            int completed = 0;
            var castStarts = new List<float>();
            var castOrder = new List<Ally>();
            var consumedOrder = new List<int>();
            a.StateMachine.OnStateChanged += (_, next) => { if (next is SkillState) { castStarts.Add(Time.time); castOrder.Add(a); } };
            b.StateMachine.OnStateChanged += (_, next) => { if (next is SkillState) { castStarts.Add(Time.time); castOrder.Add(b); } };
            Vector3 heroPoint = hero.transform.position;
            Vector3 enemyPoint = enemy.transform.position;
            Coroutine run = director.StartCoroutine(director.Resolve(new[]
            {
                new CastRequest(b, data, TargetInfo.None, 1f, true),
                new CastRequest(a, data, TargetInfo.None, 1f, true),
                new CastRequest(a, data, TargetInfo.None, 1f, true),
            }, index => { completed++; consumedOrder.Add(index); }));
            yield return null;
            Assert.That(TimeControl.Scale, Is.EqualTo(1f));
            enemy.Physics.AddImpulse(Vector3.right, 4f);
            Assert.That(noCard.gameObject.activeSelf, Is.False);
            float deadline = Time.realtimeSinceStartup + 12f;
            while (director.IsResolving)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Assist sequence did not finish");
                Assert.That(hero.gameObject.activeSelf, Is.True);
                Assert.That(TimeControl.Scale, Is.EqualTo(1f));
                Assert.That(a, Is.SameAs(identity));
                yield return null;
            }
            yield return run;
            Assert.That(automatic.IsDone, Is.True);
            Assert.That(completed, Is.EqualTo(3));
            Assert.That(castStarts.Count, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { b, a, a }, castOrder);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, consumedOrder);
            for (int i = 1; i < castStarts.Count; i++)
                Assert.That(castStarts[i] - castStarts[i - 1], Is.GreaterThanOrEqualTo(0.99f),
                    "The next hand card must wait for the previous one-second skill to finish");
            Assert.That(a.gameObject.activeSelf || b.gameObject.activeSelf, Is.False);
            Assert.That(TimeControl.Scale, Is.EqualTo(1f));
            Assert.That(pilot.Body, Is.SameAs(hero));
            // Pump the resumed physics step explicitly: the Edit Mode runner controls
            // frame scheduling across EnterPlayMode and coroutine completion.
            var simulation = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;
            try
            {
                typeof(Physics).GetMethod("FixedUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(enemy.Physics, null);
                UnityEngine.Physics.Simulate(Time.fixedDeltaTime);
                Assert.That(enemy.Physics.Rigidbody.position.x, Is.GreaterThan(enemyPoint.x));
            }
            finally { UnityEngine.Physics.simulationMode = simulation; }
            Object.Destroy(data);
            Object.Destroy(director.gameObject);
            Object.Destroy(pilot.gameObject);
            Object.Destroy(hero.gameObject);
            Object.Destroy(a.gameObject);
            Object.Destroy(b.gameObject);
            Object.Destroy(noCard.gameObject);
            Object.Destroy(enemy.gameObject);
        }

        public class SmokeSkillData : SkillData
        {
            public override IState CreateState(in SkillContext ctx) => new SmokeSkill(this, in ctx);
        }

        [UnityTest]
        public IEnumerator BulletImpacts_MoveVictimBeforeNextCardReaims_DuringLiveCombat()
        {
            yield return new EnterPlayMode();
            var hero = new GameObject("ImpactHero").AddComponent<Player>();
            var a = new GameObject("ImpactCasterA").AddComponent<Ally>();
            var b = new GameObject("ImpactCasterB").AddComponent<Ally>();
            var enemy = new GameObject("ImpactVictim").AddComponent<Enemy>();
            var untouched = new GameObject("UntouchedEnemy").AddComponent<Enemy>();
            enemy.Physics.Teleport(new Vector3(4f, 0f, 0f));
            untouched.Physics.Teleport(new Vector3(20f, 0f, 0f));
            var control = enemy.gameObject.AddComponent<CountingControl>();
            enemy.SetControl(control);
            hero.SetParty(new[] { a, b });
            var director = new GameObject("ImpactDirector").AddComponent<CastDirector>();
            director.Configure(hero, null);
            var data = ScriptableObject.CreateInstance<ImpactSkillData>();
            data.targeting = TargetingType.GroundPoint;
            data.party = new[] { a, b };
            yield return null;
            int aiTicks = control.LiveTicks;
            Vector3 enemyStart = enemy.transform.position;
            Vector3 heroStart = hero.transform.position;
            Vector3 untouchedStart = untouched.transform.position;
            bool observedMovementDuringSequence = false;
            director.StartCoroutine(director.Resolve(new[]
            {
                new CastRequest(a, data, TargetInfo.Ground(enemyStart), 1f, true),
                new CastRequest(b, data, TargetInfo.Ground(enemyStart), 1f, true),
            }, _ => { }));
            float deadline = Time.realtimeSinceStartup + 12f;
            while (director.IsResolving)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                Assert.That(TimeControl.Scale, Is.EqualTo(1f));
                Assert.That(hero.transform.position, Is.EqualTo(heroStart));
                Assert.That(Vector3.Distance(untouched.transform.position, untouchedStart), Is.LessThan(0.001f));
                if (enemy.transform.position.x > enemyStart.x + 0.1f) observedMovementDuringSequence = true;
                yield return null;
            }
            Assert.That(observedMovementDuringSequence, Is.True);
            Assert.That(control.LiveTicks, Is.GreaterThan(aiTicks), "Enemy AI resumes when execution begins");
            Assert.That(data.hitTimes.Count, Is.EqualTo(2));
            Assert.That(data.hitTimes[1] - data.hitTimes[0], Is.GreaterThanOrEqualTo(0.49f));
            Assert.That(data.aimPoints[1].x, Is.GreaterThan(data.aimPoints[0].x + 0.1f));
            Assert.That(data.allPreparedOnFirstCast, Is.True);
            Assert.That(TimeControl.AssistResolutionActive, Is.False);
            Assert.That(a.gameObject.activeSelf || b.gameObject.activeSelf, Is.False);
            Object.Destroy(data);
            Object.Destroy(director.gameObject);
            Object.Destroy(hero.gameObject);
            Object.Destroy(a.gameObject);
            Object.Destroy(b.gameObject);
            Object.Destroy(enemy.gameObject);
            Object.Destroy(untouched.gameObject);
        }

        public class CountingControl : Control
        {
            public int LiveTicks;
            public override void Tick(float dt) { if (dt > 0f) LiveTicks++; base.Tick(dt); }
        }

        public class ImpactSkillData : SkillData
        {
            public Ally[] party;
            public bool allPreparedOnFirstCast;
            public readonly List<float> hitTimes = new List<float>();
            public readonly List<Vector3> aimPoints = new List<Vector3>();
            public override IState CreateState(in SkillContext ctx) => new ImpactSkill(this, in ctx);
        }

        private class ImpactSkill : SkillState
        {
            private readonly ImpactSkillData data;
            private float elapsed;
            private bool hit;
            public ImpactSkill(ImpactSkillData data, in SkillContext ctx) : base(data, in ctx) { this.data = data; }
            public override bool IsFinished => elapsed >= 0.2f;
            public override void Enter()
            {
                ResolveTarget();
                PlaceCaster();
                data.aimPoints.Add(Context.targetInfo.point);
                if (data.aimPoints.Count == 1)
                {
                    data.allPreparedOnFirstCast = true;
                    foreach (Ally ally in data.party)
                        if (ally != Context.caster && !ally.GetComponent<AssistControlModule>().InFormation)
                            data.allPreparedOnFirstCast = false;
                }
            }
            public override void Tick(float dt)
            {
                elapsed += dt;
                if (hit || elapsed < 0.1f) return;
                hit = true;
                var hitData = new HitData
                {
                    damageData = new DamageData(2f), pushDistance = 2f,
                    mode = KnockbackMode.Fixed, nextState = CombatState.LightHit, hitStunDuration = 0.2f,
                };
                if (Context.CasterCombat.Attack(Context.target.Combat, in hitData)) data.hitTimes.Add(Time.time);
            }
            public override void Exit() { }
        }

        [UnityTest]
        public IEnumerator HeroDeath_CancelsUnstartedCardsOnceAndThawsBattle()
        {
            yield return new EnterPlayMode();
            var hero = new GameObject("RecallHero").AddComponent<Player>();
            var ally = new GameObject("RecallAlly").AddComponent<Ally>();
            hero.SetParty(new[] { ally });
            var director = new GameObject("RecallDirector").AddComponent<CastDirector>();
            director.Configure(hero, null);
            var data = ScriptableObject.CreateInstance<SmokeSkillData>();
            var consumed = new List<int>();
            director.StartCoroutine(director.Resolve(new[]
            {
                new CastRequest(ally, data, TargetInfo.None, 1f, true),
                new CastRequest(ally, data, TargetInfo.None, 1f, true),
                new CastRequest(ally, data, TargetInfo.None, 1f, true),
            }, i => consumed.Add(i)));
            yield return null;
            hero.Combat.TakeDamage(new DamageData(1000f));
            yield return null;
            Assert.That(hero.Combat.IsDead, Is.True);
            Assert.That(director.IsResolving, Is.False);
            Assert.That(TimeControl.Scale, Is.EqualTo(1f));
            Assert.That(ally.gameObject.activeSelf, Is.False);
            director.Abort();
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, consumed);
            Object.Destroy(data);
            Object.Destroy(director.gameObject);
            Object.Destroy(hero.gameObject);
            Object.Destroy(ally.gameObject);
        }
        private class SmokeSkill : SkillState
        {
            private float elapsed;
            public SmokeSkill(SkillData data, in SkillContext ctx) : base(data, in ctx) { }
            public override bool IsFinished => elapsed >= 1f;
            public override void Enter() => elapsed = 0f;
            public override void Tick(float dt) => elapsed += dt;
            public override void Exit() { }
        }
    }
}
