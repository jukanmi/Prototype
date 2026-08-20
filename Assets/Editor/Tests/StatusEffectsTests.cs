using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 지속시간 · 원복 규칙. 유니티 객체가 필요 없는 부분은 <see cref="StatusEffects"/>를
    /// 직접 굴리고, Combat과 물리는 실제 타격 경로로 만든다(EnemyStateTintTests와 같은 방식).
    /// </summary>
    public class StatusEffectsTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        // ── 모델 ────────────────────────────────────────

        [Test]
        public void Applied_StatusIsListedWithItsRemainingTime()
        {
            var s = new StatusEffects();

            s.Apply(StatusKind.Shield, 5f);

            Assert.That(s.Has(StatusKind.Shield), Is.True);
            Assert.That(s.Remaining(StatusKind.Shield), Is.EqualTo(5f).Within(0.0001f));
            Assert.That(s.Active[0].Ratio, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Tick_DrainsRemainingTime()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.DamageCut, 4f);

            s.Tick(1f);

            Assert.That(s.Remaining(StatusKind.DamageCut), Is.EqualTo(3f).Within(0.0001f));
            Assert.That(s.Active[0].Ratio, Is.EqualTo(0.75f).Within(0.0001f),
                        "게이지는 걸릴 때의 전체 길이를 분모로 그린다");
        }

        [Test]
        public void WhenTimeRunsOut_RevertRunsOnceAndTheStatusIsGone()
        {
            int reverted = 0;
            var s = new StatusEffects();
            s.Apply(StatusKind.Lifesteal, 1f, () => reverted++);

            s.Tick(1.5f);
            s.Tick(1.5f);

            Assert.That(reverted, Is.EqualTo(1), "원복은 딱 한 번이어야 한다");
            Assert.That(s.Has(StatusKind.Lifesteal), Is.False);
            Assert.That(s.Count, Is.Zero);
        }

        /// <summary>짧은 재시전이 이미 걸린 긴 버프를 잘라 내면 안 된다.</summary>
        [Test]
        public void Reapplying_KeepsTheLongerDuration()
        {
            int reverted = 0;
            var s = new StatusEffects();

            s.Apply(StatusKind.Shield, 8f, () => reverted++);
            s.Apply(StatusKind.Shield, 2f, () => reverted++);

            Assert.That(s.Count, Is.EqualTo(1), "같은 종류가 두 줄로 늘어나면 안 된다");
            Assert.That(s.Remaining(StatusKind.Shield), Is.EqualTo(8f).Within(0.0001f));
            Assert.That(reverted, Is.Zero, "덧건 것이지 한 번 푼 게 아니다");
        }

        [Test]
        public void Reapplying_ExtendsAndKeepsTheGaugeWithinRange()
        {
            var s = new StatusEffects();

            s.Apply(StatusKind.Shield, 3f);
            s.Tick(2f);
            s.Apply(StatusKind.Shield, 5f);

            Assert.That(s.Remaining(StatusKind.Shield), Is.EqualTo(5f).Within(0.0001f));
            Assert.That(s.Active[0].Ratio, Is.EqualTo(1f).Within(0.0001f),
                        "늘어난 만큼 분모도 커져야 게이지가 100%를 넘지 않는다");
        }

        [Test]
        public void ZeroDuration_RevertsImmediatelyWithoutListing()
        {
            int reverted = 0;
            var s = new StatusEffects();

            s.Apply(StatusKind.DamageCut, 0f, () => reverted++);

            Assert.That(reverted, Is.EqualTo(1));
            Assert.That(s.Count, Is.Zero);
        }

        [Test]
        public void Cancel_EndsItEarlyAndReverts()
        {
            int reverted = 0;
            var s = new StatusEffects();
            s.Apply(StatusKind.Shield, 10f, () => reverted++);

            s.Cancel(StatusKind.Shield);

            Assert.That(reverted, Is.EqualTo(1));
            Assert.That(s.Has(StatusKind.Shield), Is.False);
        }

        [Test]
        public void CancelAll_RevertsEveryOne()
        {
            int reverted = 0;
            var s = new StatusEffects();
            s.Apply(StatusKind.Shield, 10f, () => reverted++);
            s.Apply(StatusKind.DamageCut, 10f, () => reverted++);

            s.CancelAll();

            Assert.That(reverted, Is.EqualTo(2));
            Assert.That(s.Count, Is.Zero);
        }

        /// <summary>원복이 다시 상태를 거는 경우. 방금 지운 칸을 덮어쓰면 목록이 깨진다.</summary>
        [Test]
        public void RevertMayApplyAnotherStatus_WithoutCorruptingTheList()
        {
            var s = new StatusEffects();
            s.Apply(StatusKind.Shield, 1f, () => s.Apply(StatusKind.DamageCut, 3f));

            s.Tick(1f);

            Assert.That(s.Has(StatusKind.Shield), Is.False);
            Assert.That(s.Remaining(StatusKind.DamageCut), Is.EqualTo(3f).Within(0.0001f));
        }

        // ── Combat 연결 ─────────────────────────────────

        [Test]
        public void CombatTick_DrivesTheStatusClock()
        {
            Combat c = NewAlly();
            bool reverted = false;
            c.Statuses.Apply(StatusKind.DamageCut, 0.5f, () => reverted = true);

            c.Tick(0.2f);
            Assert.That(reverted, Is.False, "선행 조건: 아직 남아 있어야 한다");

            c.Tick(0.4f);

            Assert.That(reverted, Is.True);
            Assert.That(c.Statuses.Count, Is.Zero);
        }

        /// <summary>경직 중에도 버프 시간은 흘러야 한다. Tick의 early return 뒤에 두면 멈춘다.</summary>
        [Test]
        public void StatusClockRuns_EvenWhileStunned()
        {
            Combat attacker = NewAlly();
            Combat victim = NewAlly();
            victim.Statuses.Apply(StatusKind.Shield, 2f);

            attacker.Attack(victim, in Poke);
            Assert.That(victim.CombatState, Is.EqualTo(CombatState.LightHit), "선행 조건: 경직이어야 한다");

            victim.Tick(0.1f);

            Assert.That(victim.Statuses.Remaining(StatusKind.Shield), Is.EqualTo(1.9f).Within(0.0001f));
        }

        [Test]
        public void ShieldStatus_EndsWhenTheShieldIsUsedUp()
        {
            Combat c = NewAlly();
            c.AddShield(10f);
            c.Statuses.Apply(StatusKind.Shield, 30f, () => c.ClearShield());

            c.TakeDamage(new DamageData(10f));

            Assert.That(c.Shield, Is.Zero);
            Assert.That(c.Statuses.Has(StatusKind.Shield), Is.False,
                        "다 닳은 보호막이 게이지에 계속 떠 있으면 안 된다");
        }

        [Test]
        public void ShieldStatus_SurvivesAPartialHit()
        {
            Combat c = NewAlly();
            c.AddShield(10f);
            c.Statuses.Apply(StatusKind.Shield, 30f, () => c.ClearShield());

            c.TakeDamage(new DamageData(4f));

            Assert.That(c.Shield, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(c.Statuses.Has(StatusKind.Shield), Is.True);
        }

        [Test]
        public void OnDeath_EveryStatusIsCleared()
        {
            Combat c = NewAlly();
            c.Statuses.Apply(StatusKind.Lifesteal, 30f, () => c.SetLifesteal(0f));

            c.TakeDamage(new DamageData(9999f));

            Assert.That(c.IsDead, Is.True);
            Assert.That(c.Statuses.Count, Is.Zero, "시체에 버프가 남아 있을 이유가 없다");
        }

        // ── 헬퍼 ────────────────────────────────────────

        /// <summary>죽지 않을 만큼 약한 한 대. 경직만 일으킨다.</summary>
        private static readonly HitData Poke = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            hitStunDuration = 0.3f,
        };

        private Combat NewAlly()
        {
            var go = new GameObject("Ally");
            spawned.Add(go);
            go.AddComponent<Ally>();
            return go.GetComponent<Combat>();
        }
    }
}
