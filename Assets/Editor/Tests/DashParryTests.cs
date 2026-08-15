using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 대시 패링. 대시가 <see cref="Combat.BeginParryWindow"/>로 짧은 창을 열고,
    /// 그 창 안에 <b>전방</b>에서 들어온 타격을 흘린다.
    ///
    /// 대시는 상태가 아니라 <c>Physics.Dash</c> 한 번으로 끝나므로, 여기서도 대시를 재현하지 않고
    /// 창을 직접 연다 — 검증 대상은 창이 열린 뒤의 판정이다.
    /// </summary>
    public class DashParryTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        // ── 전방 판정 (순수 규칙) ────────────────────────

        [Test]
        public void IsFrontal_StraightAhead_Passes()
        {
            Assert.That(CombatStateRules.IsFrontal(Vector3.right, Vector3.right, 120f), Is.True);
        }

        [Test]
        public void IsFrontal_Behind_Fails()
        {
            Assert.That(CombatStateRules.IsFrontal(Vector3.right, Vector3.left, 120f), Is.False);
        }

        [Test]
        public void IsFrontal_SideIsInsideA120DegreeFan()
        {
            // 120도 창이면 정면 기준 좌우 60도까지다. 정확히 옆(90도)은 밖.
            Assert.That(CombatStateRules.IsFrontal(Vector3.right, Vector3.forward, 120f), Is.False);
            Assert.That(CombatStateRules.IsFrontal(Vector3.right, Vector3.forward, 200f), Is.True);
        }

        [Test]
        public void IsFrontal_EdgeOfTheFan()
        {
            Vector3 justInside = Quaternion.Euler(0f, 59f, 0f) * Vector3.right;
            Vector3 justOutside = Quaternion.Euler(0f, 61f, 0f) * Vector3.right;

            Assert.That(CombatStateRules.IsFrontal(Vector3.right, justInside, 120f), Is.True);
            Assert.That(CombatStateRules.IsFrontal(Vector3.right, justOutside, 120f), Is.False);
        }

        [Test]
        public void IsFrontal_IgnoresHeight()
        {
            // 머리 위에서 내려찍어도 XZ가 정면이면 정면이다.
            Vector3 fromAbove = new Vector3(1f, 8f, 0f);

            Assert.That(CombatStateRules.IsFrontal(Vector3.right, fromAbove, 120f), Is.True);
        }

        [Test]
        public void IsFrontal_ZeroVector_Fails()
        {
            Assert.That(CombatStateRules.IsFrontal(Vector3.zero, Vector3.right, 120f), Is.False);
            Assert.That(CombatStateRules.IsFrontal(Vector3.right, Vector3.zero, 120f), Is.False);

            // 바로 위에서 정확히 수직으로 떨어지는 타격 — XZ 성분이 없어 방향을 못 정한다.
            Assert.That(CombatStateRules.IsFrontal(Vector3.right, Vector3.up, 120f), Is.False);
        }

        // ── 패링 판정 ────────────────────────────────────

        [Test]
        public void FrontalHit_InsideWindow_IsNullified()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.BeginParryWindow();
            bool landed = victim.Hit(in Poke, attacker);

            Assert.That(landed, Is.False, "패링한 타격은 적중으로 치지 않는다");
            Assert.That(victim.Health.CurValue, Is.EqualTo(victim.Health.MaxValue), "데미지가 들어가면 안 된다");
            Assert.That(victim.CombatState, Is.EqualTo(CombatState.Neutral), "경직도 없다");
        }

        [Test]
        public void HitFromBehind_InsideWindow_LandsNormally()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", Behind);

            victim.BeginParryWindow();
            bool landed = victim.Hit(in Poke, attacker);

            Assert.That(landed, Is.True);
            Assert.That(victim.CombatState, Is.EqualTo(CombatState.LightHit), "등 뒤는 못 막는다");
        }

        [Test]
        public void AfterWindowExpires_FrontalHitLands()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.BeginParryWindow();
            victim.Tick(0.5f);   // 기본 창 0.15초를 넉넉히 넘긴다
            Assert.That(victim.IsParrying, Is.False, "선행 조건: 창이 닫혔다");

            Assert.That(victim.Hit(in Poke, attacker), Is.True);
            Assert.That(victim.CombatState, Is.EqualTo(CombatState.LightHit));
        }

        [Test]
        public void WithoutDash_FrontalHitLands()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            Assert.That(victim.IsParrying, Is.False);
            Assert.That(victim.Hit(in Poke, attacker), Is.True);
        }

        [Test]
        public void ParrySuccess_TurnsIntoInvulnerability()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.BeginParryWindow();

            Assert.That(victim.Hit(in Poke, attacker), Is.False, "첫 타를 막는다");
            Assert.That(victim.IsParrying, Is.False, "창은 그 자리에서 닫히고");
            Assert.That(victim.IsParryInvulnerable, Is.True, "무적으로 갈아탄다");
        }

        /// <summary>
        /// 이 시스템의 요점. 개별 타격을 하나씩 지우면 다대일에서 한 명분만 막고
        /// 나머지를 그대로 맞아 패링이 무의미해진다.
        /// </summary>
        [Test]
        public void AfterParry_EveryIncomingHitIsShrugged_RegardlessOfDirection()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat front = NewCombat("Front", InFront);
            Combat back = NewCombat("Back", Behind);
            Combat second = NewCombat("Second", InFront);

            victim.BeginParryWindow();
            victim.Hit(in Poke, front);   // 여기서 무적 시작

            Assert.That(victim.Hit(in Poke, back), Is.False, "무적 구간은 등 뒤도 흘린다");
            Assert.That(victim.Hit(in Poke, second), Is.False, "동시에 들어온 다른 적의 타격도 흘린다");
            Assert.That(victim.Health.CurValue, Is.EqualTo(victim.Health.MaxValue));
        }

        [Test]
        public void EveryShruggedAttacker_GetsCountered()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat front = NewCombat("Front", InFront);
            Combat back = NewCombat("Back", Behind);

            victim.BeginParryWindow();
            victim.Hit(in Poke, front);
            victim.Hit(in Poke, back);

            Assert.That(front.CombatState, Is.EqualTo(CombatState.LightHit));
            Assert.That(back.CombatState, Is.EqualTo(CombatState.LightHit),
                        "무적으로 흘린 타격도 받아친다 — 한 번의 패링으로 여러 적을 끊는다");
        }

        [Test]
        public void InvulnerabilityExpires()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.BeginParryWindow();
            victim.Hit(in Poke, attacker);

            victim.Tick(1f);   // 기본 무적 0.35초를 넉넉히 넘긴다
            Assert.That(victim.IsParryInvulnerable, Is.False);

            Assert.That(victim.Hit(in Poke, attacker), Is.True, "무적이 끝나면 다시 맞는다");
            Assert.That(victim.CombatState, Is.EqualTo(CombatState.LightHit));
        }

        [Test]
        public void ParryReward_FiresOncePerParry_NotPerShruggedHit()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat front = NewCombat("Front", InFront);
            Combat back = NewCombat("Back", Behind);

            int calls = 0;
            void Handler(Combat d, Combat a) => calls++;

            Combat.OnParried += Handler;
            try
            {
                victim.BeginParryWindow();
                victim.Hit(in Poke, front);
                victim.Hit(in Poke, back);
            }
            finally
            {
                Combat.OnParried -= Handler;
            }

            Assert.That(calls, Is.EqualTo(1), "게이지 보상은 패링 한 번당 한 번이다");
        }

        [Test]
        public void ParrySucceeded_StunsTheAttacker_WithoutDamage()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.BeginParryWindow();
            victim.Hit(in Poke, attacker);

            Assert.That(attacker.CombatState, Is.EqualTo(CombatState.LightHit), "패링당한 쪽이 경직에 걸린다");
            Assert.That(attacker.Health.CurValue, Is.EqualTo(attacker.Health.MaxValue),
                        "패링의 보상은 딜이 아니라 기회다 — 반격 데미지는 0");
        }

        [Test]
        public void ParryFires_OnParried_Once()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            var calls = new List<(Combat defender, Combat attacker)>();
            void Handler(Combat d, Combat a) => calls.Add((d, a));

            Combat.OnParried += Handler;
            try
            {
                victim.BeginParryWindow();
                victim.Hit(in Poke, attacker);
            }
            finally
            {
                // static 이벤트라 해제하지 않으면 파괴된 구독자가 다음 테스트까지 따라온다.
                Combat.OnParried -= Handler;
            }

            Assert.That(calls.Count, Is.EqualTo(1));
            Assert.That(calls[0].defender, Is.SameAs(victim));
            Assert.That(calls[0].attacker, Is.SameAs(attacker));
        }

        [Test]
        public void AreaHit_UsesItsOwnOrigin_NotTheCasterPosition()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            // 시전자는 등 뒤에 있지만 장판은 앞에서 터진다 — 판정은 터진 자리를 따라야 한다.
            Combat caster = NewCombat("Caster", Behind);

            victim.BeginParryWindow();
            bool landed = victim.Hit(Poke.WithOrigin(InFront), caster);

            Assert.That(landed, Is.False, "장판은 터진 좌표를 기준으로 막는다");
        }

        [Test]
        public void AreaHitFromBehind_IsNotParried()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat caster = NewCombat("Caster", InFront);

            victim.BeginParryWindow();
            bool landed = victim.Hit(Poke.WithOrigin(Behind), caster);

            Assert.That(landed, Is.True, "시전자가 앞에 있어도 터진 자리가 뒤면 못 막는다");
        }

        [Test]
        public void BenchingClosesTheWindow()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);

            Combat attacker = NewCombat("Attacker", InFront);

            victim.BeginParryWindow();
            victim.Hit(in Poke, attacker);
            victim.ClearHitStun();   // 태그로 내려가는 몸의 뒷정리

            Assert.That(victim.IsParrying, Is.False, "필드에서 뺀 몸이 창을 물고 돌아오면 안 된다");
            Assert.That(victim.IsParryInvulnerable, Is.False, "무적도 함께 닫힌다");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>Physics.Facing의 기본값이 Vector3.right이므로 +X가 정면이다.</summary>
        private static readonly Vector3 InFront = new Vector3(3f, 0f, 0f);
        private static readonly Vector3 Behind = new Vector3(-3f, 0f, 0f);

        /// <summary>죽지 않을 만큼 약한 한 대. 경직만 일으킨다.</summary>
        private static readonly HitData Poke = new HitData
        {
            damageData = new DamageData(5f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.AwayFromCaster,
            hitStunDuration = 0.3f,
        };

        private Combat NewCombat(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            spawned.Add(go);

            go.AddComponent<Ally>();
            return go.GetComponent<Combat>();
        }
    }
}
