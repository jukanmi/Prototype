using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 불릿타임 게이지 보상 — 평타 적중과 대시 패링(저스트 회피).
    ///
    /// 게이지를 실제로 채우는 <see cref="BulletTimeController"/>는 Awake · OnEnable이 돌아야 살아나므로
    /// 여기서는 그 앞의 조각만 본다: 평타 번호가 찍히는가, 적중 신호가 제대로 울리는가,
    /// 진영 필터가 맞는가, 한 대당 한 번으로 세는가.
    /// </summary>
    public class BulletTimeGaugeRewardTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        // ── 진영 필터 ────────────────────────────────────

        [Test]
        public void Ally_EarnsGauge()
        {
            Assert.That(BulletTimeController.EarnsGauge(New<Ally>("Ally")), Is.True);
        }

        [Test]
        public void Enemy_DoesNotEarnGauge()
        {
            Assert.That(BulletTimeController.EarnsGauge(New<Enemy>("Enemy")), Is.False,
                "적이 패링할 때마다 플레이어 게이지가 차면 안 된다");
        }

        [Test]
        public void Null_DoesNotEarnGauge()
        {
            // 공격자를 모르는 타격(장판 등)이 흘러 들어오는 경로가 있다.
            Assert.That(BulletTimeController.EarnsGauge(null), Is.False);
        }

        [Test]
        public void AllyHittingEnemy_EarnsBasicHitGauge()
        {
            Assert.That(BulletTimeController.EarnsBasicHitGauge(New<Ally>("Ally"), New<Enemy>("Enemy")), Is.True);
        }

        [Test]
        public void EnemyHittingAlly_DoesNotEarnBasicHitGauge()
        {
            Assert.That(BulletTimeController.EarnsBasicHitGauge(New<Enemy>("Enemy"), New<Ally>("Ally")), Is.False,
                "적 평타에 맞았다고 플레이어 게이지가 차면 안 된다");
        }

        [Test]
        public void AllyHittingAlly_DoesNotEarnBasicHitGauge()
        {
            Assert.That(BulletTimeController.EarnsBasicHitGauge(New<Ally>("A"), New<Ally>("B")), Is.False,
                "적에게 맞혀야 보상이다");
        }

        // ── 평타 번호 ────────────────────────────────────

        [Test]
        public void BuildBasicHit_StampsFreshSwingEveryCall()
        {
            Ally ally = New<Ally>("Ally");
            ally.gameObject.AddComponent<AllyBasicAttack>();

            HitData first = ally.BuildBasicHit(0);
            HitData second = ally.BuildBasicHit(1);

            Assert.That(first.IsBasicAttack, Is.True);
            Assert.That(second.IsBasicAttack, Is.True);
            Assert.That(second.basicSwing, Is.Not.EqualTo(first.basicSwing), "연타는 타마다 다른 한 대다");
        }

        [Test]
        public void HandMadeHit_IsNotBasicAttack()
        {
            // 스킬 · 적 패턴 · 패링 반격은 BuildBasicHit을 거치지 않는다.
            Assert.That(Poke.IsBasicAttack, Is.False);
        }

        // ── 적중 신호 ────────────────────────────────────

        [Test]
        public void BasicHitLanding_FiresOnAnyBasicHitLanded()
        {
            Combat attacker = NewCombat("Attacker", Vector3.zero);
            Combat victim = NewCombat("Victim", InFront);
            HitData hit = Basic(7);

            var calls = new List<(Combat a, Combat v, int swing)>();
            void Handler(Combat a, Combat v, int s) => calls.Add((a, v, s));

            Combat.OnAnyBasicHitLanded += Handler;
            try
            {
                Assert.That(attacker.Attack(victim, in hit), Is.True, "선행 조건: 적중했다");
            }
            finally
            {
                // static 이벤트라 해제하지 않으면 파괴된 구독자가 다음 테스트까지 따라온다.
                Combat.OnAnyBasicHitLanded -= Handler;
            }

            Assert.That(calls.Count, Is.EqualTo(1));
            Assert.That(calls[0].a, Is.SameAs(attacker));
            Assert.That(calls[0].v, Is.SameAs(victim));
            Assert.That(calls[0].swing, Is.EqualTo(7));
        }

        [Test]
        public void NonBasicHit_DoesNotFire()
        {
            Combat attacker = NewCombat("Attacker", Vector3.zero);
            Combat victim = NewCombat("Victim", InFront);

            int calls = 0;
            void Handler(Combat a, Combat v, int s) => calls++;

            Combat.OnAnyBasicHitLanded += Handler;
            try
            {
                attacker.Attack(victim, in Poke);
            }
            finally
            {
                Combat.OnAnyBasicHitLanded -= Handler;
            }

            Assert.That(calls, Is.Zero, "스킬 적중은 평타 보상이 아니다");
        }

        [Test]
        public void ParriedBasicHit_DoesNotFire()
        {
            Combat attacker = NewCombat("Attacker", InFront);
            Combat victim = NewCombat("Victim", Vector3.zero);
            HitData hit = Basic(3);

            int calls = 0;
            void Handler(Combat a, Combat v, int s) => calls++;

            Combat.OnAnyBasicHitLanded += Handler;
            try
            {
                victim.BeginParryWindow();
                Assert.That(attacker.Attack(victim, in hit), Is.False, "선행 조건: 패링당했다");
            }
            finally
            {
                Combat.OnAnyBasicHitLanded -= Handler;
            }

            Assert.That(calls, Is.Zero, "흘려낸 평타는 적중이 아니다");
        }

        // ── 한 대당 한 번 ────────────────────────────────

        [Test]
        public void Ledger_OneSwingHittingManyEnemies_ClaimsOnce()
        {
            var ledger = new BasicHitLedger();
            var body = new object();

            Assert.That(ledger.TryClaim(body, 5), Is.True, "첫 적");
            Assert.That(ledger.TryClaim(body, 5), Is.False, "같은 대의 두 번째 적");
            Assert.That(ledger.TryClaim(body, 5), Is.False, "같은 대의 세 번째 적");
        }

        [Test]
        public void Ledger_NextSwing_ClaimsAgain()
        {
            var ledger = new BasicHitLedger();
            var body = new object();

            ledger.TryClaim(body, 5);

            Assert.That(ledger.TryClaim(body, 6), Is.True, "연타 다음 타는 따로 센다");
        }

        [Test]
        public void Ledger_InterleavedBodies_DoNotResetEachOther()
        {
            var ledger = new BasicHitLedger();
            var a = new object();
            var b = new object();

            Assert.That(ledger.TryClaim(a, 10), Is.True);
            Assert.That(ledger.TryClaim(b, 11), Is.True);
            Assert.That(ledger.TryClaim(a, 10), Is.False, "다른 동료의 평타가 끼어도 앞 대의 두 번째 적중은 거른다");
        }

        [Test]
        public void Ledger_NonBasicSwing_IsRejected()
        {
            Assert.That(new BasicHitLedger().TryClaim(new object(), 0), Is.False);
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>Physics.Facing의 기본값이 Vector3.right이므로 +X가 정면이다.</summary>
        private static readonly Vector3 InFront = new Vector3(3f, 0f, 0f);

        /// <summary>죽지 않을 만큼 약한 한 대. 평타 번호가 없다.</summary>
        private static readonly HitData Poke = new HitData
        {
            damageData = new DamageData(5f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.AwayFromCaster,
            hitStunDuration = 0.3f,
        };

        private static HitData Basic(int swing)
        {
            HitData h = Poke;
            h.basicSwing = swing;
            return h;
        }

        private T New<T>(string name) where T : Entity
        {
            var go = new GameObject(name);
            spawned.Add(go);

            return go.AddComponent<T>();
        }

        private Combat NewCombat(string name, Vector3 position)
        {
            Ally body = New<Ally>(name);
            body.transform.position = position;
            return body.Combat;
        }
    }
}
