using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 보스 가드 / 가드브레이크.
    ///
    /// 가드를 가진 개체는 <b>평소가 슈퍼아머</b>다 — 맞아도 밀리거나 멈추지 않고 데미지만 받는다.
    /// 타격마다 가드가 깎이고 0이 되면 정해진 시간 동안 완전 무방비가 된다.
    /// 그 구간에서만 경직 · 넉백 · 공중 콤보가 통한다.
    /// </summary>
    public class BossGuardTests
    {
        /// <summary>단위는 타격 횟수다. 보스 프리팹과 같은 값을 쓴다 — 열 대에 깨진다.</summary>
        private const float Guard = 10f;
        private const float BreakDuration = 4f;

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        // ── 슈퍼아머 (기본 상태) ─────────────────────────

        [Test]
        public void GuardedBody_IsSuperArmoredFromTheStart()
        {
            Combat boss = NewBoss();

            Assert.That(boss.HasGuard, Is.True);
            Assert.That(boss.IsSuperArmored, Is.True, "가드를 가진 개체는 평소가 아머다");
            Assert.That(boss.Guard.IsFull, Is.True);
        }

        [Test]
        public void Armored_TakesDamageButNoStagger()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            bool landed = boss.Hit(in Poke, attacker);

            Assert.That(landed, Is.True, "맞긴 맞았다 — 무적이 아니다");
            Assert.That(boss.Health.CurValue, Is.LessThan(boss.Health.MaxValue), "데미지는 들어간다");
            Assert.That(boss.CombatState, Is.EqualTo(CombatState.Neutral), "경직은 없다");
        }

        [Test]
        public void Armored_IsNotKnockedBack()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            boss.Hit(in HardKnockback, attacker);

            Assert.That(boss.Physics.HorizontalVelocity, Is.EqualTo(Vector3.zero),
                        "넉백은 ApplyKnockback 앞에서 막힌다");
        }

        [Test]
        public void Armored_IsNotLaunched()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            boss.Hit(in Launcher, attacker);

            Assert.That(boss.Physics.PhysicsState, Is.EqualTo(PhysicsState.Ground), "아머는 뜨지 않는다");
            Assert.That(boss.CombatState, Is.EqualTo(CombatState.Neutral));
        }

        [Test]
        public void Armored_StillDies()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            boss.Hit(Lethal, attacker);

            Assert.That(boss.IsDead, Is.True, "사망은 아머를 관통한다");
            Assert.That(boss.IsSuperArmored, Is.False, "시체는 아머가 아니다");
        }

        // ── 가드 소모 · 브레이크 ─────────────────────────

        /// <summary>
        /// 가드는 <b>타격 횟수</b>로 깎인다. 데미지에 비례시키면 큰 기술 한 방이 벽을 허물어
        /// "연타로 깨는" 맛이 사라진다.
        /// </summary>
        [Test]
        public void EachHit_DrainsOneGuard()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            float before = boss.Guard.CurValue;
            boss.Hit(in Poke, attacker);

            Assert.That(boss.Guard.CurValue, Is.EqualTo(before - DefaultGuardDamage).Within(0.001f));
        }

        [Test]
        public void BigHit_DrainsTheSameAsASmallOne()
        {
            Combat weak = NewBoss();
            Combat strong = NewBoss();
            Combat attacker = NewAttacker();

            weak.Hit(in Poke, attacker);
            strong.Hit(in Heavy, attacker);

            Assert.That(strong.Guard.CurValue, Is.EqualTo(weak.Guard.CurValue).Within(0.001f),
                        "한 방이 세다고 가드를 더 깎지 않는다 — 몰아치는 쪽이 벽을 허문다");
        }

        [Test]
        public void ExactlyTenHits_BreakTheGuard()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            for (int i = 0; i < 9; i++) boss.Hit(in Poke, attacker);
            Assert.That(boss.IsGuardBroken, Is.False, "아홉 대까지는 버틴다");

            boss.Hit(in Poke, attacker);
            Assert.That(boss.IsGuardBroken, Is.True, "열 대째에 깨진다");
        }

        [Test]
        public void ZeroDamageHit_DoesNotDrainGuard()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            float before = boss.Guard.CurValue;
            boss.Hit(in Harmless, attacker);

            Assert.That(boss.Guard.CurValue, Is.EqualTo(before).Within(0.001f),
                        "기회만 주는 판정(패링 반격 등)이 벽을 대신 허물면 안 된다");
        }

        [Test]
        public void HitWithGuardDamage_OverridesTheDefault()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            float before = boss.Guard.CurValue;
            boss.Hit(in GuardBreaker, attacker);

            Assert.That(boss.Guard.CurValue, Is.EqualTo(before - GuardBreaker.guardDamage).Within(0.001f));
        }

        [Test]
        public void GuardEmpty_OpensTheBreak()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            DrainToBreak(boss, attacker);

            Assert.That(boss.IsGuardBroken, Is.True);
            Assert.That(boss.IsSuperArmored, Is.False, "브레이크 중에는 아머가 아니다");
        }

        /// <summary>
        /// 가드를 깨뜨린 <b>그 타격</b>부터 경직이 걸린다 — 브레이크가 한 대 늦게 열리면
        /// "다 깎았는데 한 번 더 때려야 한다"가 되어 손맛이 끊긴다.
        /// </summary>
        [Test]
        public void TheBreakingHit_AlreadyStaggers()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            // 마지막 한 대만 남기고 깎는다.
            int hits = Mathf.CeilToInt(Guard / DefaultGuardDamage);
            for (int i = 0; i < hits - 1; i++) boss.Hit(in Poke, attacker);

            Assert.That(boss.IsGuardBroken, Is.False, "선행 조건: 아직 안 깨졌다");

            boss.Hit(in Poke, attacker);

            Assert.That(boss.IsGuardBroken, Is.True);
            Assert.That(boss.CombatState, Is.EqualTo(CombatState.LightHit),
                        "깨뜨린 타격이 곧 첫 경직이다");
        }

        [Test]
        public void Broken_TakesKnockbackAndLaunch()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();
            DrainToBreak(boss, attacker);

            boss.Hit(in Launcher, attacker);

            Assert.That(boss.CombatState, Is.EqualTo(CombatState.AerialHit), "브레이크 중엔 띄워진다");
            Assert.That(boss.Physics.PhysicsState, Is.EqualTo(PhysicsState.Aerial));
        }

        [Test]
        public void Broken_DoesNotDrainFurther()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();
            DrainToBreak(boss, attacker);

            Assert.That(boss.Guard.CurValue, Is.Zero, "선행 조건: 가드가 비었다");

            boss.Hit(in GuardBreaker, attacker);

            Assert.That(boss.Guard.CurValue, Is.Zero, "이미 바닥이다. 회복 시점은 타이머가 정한다");
        }

        [Test]
        public void BreakExpires_RefillsGuardAndRestoresArmor()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();
            DrainToBreak(boss, attacker);

            boss.Tick(BreakDuration + 0.1f);

            Assert.That(boss.IsGuardBroken, Is.False);
            Assert.That(boss.Guard.IsFull, Is.True, "브레이크가 끝나면 가드는 가득 찬 상태로 돌아온다");
            Assert.That(boss.IsSuperArmored, Is.True);
        }

        [Test]
        public void Unhit_RegeneratesGuardAfterTheDelay()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            boss.Hit(in Poke, attacker);
            float drained = boss.Guard.CurValue;

            // 회복 지연 안에서는 아직 차지 않는다.
            boss.Tick(RegenDelay * 0.5f);
            Assert.That(boss.Guard.CurValue, Is.EqualTo(drained).Within(0.001f));

            boss.Tick(RegenDelay);
            Assert.That(boss.Guard.CurValue, Is.GreaterThan(drained), "손 놓으면 다시 차오른다");
        }

        [Test]
        public void HittingAgain_RestartsTheRegenDelay()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            boss.Hit(in Poke, attacker);
            boss.Tick(RegenDelay * 0.9f);
            boss.Hit(in Poke, attacker);          // 지연이 다시 감긴다

            float drained = boss.Guard.CurValue;
            boss.Tick(RegenDelay * 0.5f);

            Assert.That(boss.Guard.CurValue, Is.EqualTo(drained).Within(0.001f),
                        "때리는 동안은 회복하지 않는다");
        }

        [Test]
        public void GuardBreak_ResetsAirHitCount()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            DrainToBreak(boss, attacker);
            boss.Hit(in Launcher, attacker);      // 브레이크 중이라 공중으로 뜬다
            Assert.That(boss.AirHitCount, Is.EqualTo(1), "선행 조건: 공중 히트가 쌓였다");

            // 브레이크가 끝나고 다시 깨지면 누적이 정리돼야 콤보를 처음부터 받는다.
            boss.Tick(BreakDuration + 0.1f);
            DrainToBreak(boss, attacker);

            Assert.That(boss.AirHitCount, Is.Zero);
        }

        [Test]
        public void GuardBreakChanged_FiresOnceEachWay()
        {
            Combat boss = NewBoss();
            Combat attacker = NewAttacker();

            var calls = new List<bool>();
            void Handler(bool on) => calls.Add(on);

            boss.OnGuardBreakChanged += Handler;
            try
            {
                DrainToBreak(boss, attacker);
                boss.Tick(BreakDuration + 0.1f);
            }
            finally
            {
                boss.OnGuardBreakChanged -= Handler;
            }

            Assert.That(calls, Is.EqualTo(new[] { true, false }));
        }

        // ── 잡몹 회귀 방지 ───────────────────────────────

        [Test]
        public void WithoutGuard_NothingChanges()
        {
            Combat mob = NewAttacker();          // maxGuard 기본값 0
            Combat attacker = NewAttacker();

            Assert.That(mob.HasGuard, Is.False);
            Assert.That(mob.IsSuperArmored, Is.False);

            bool landed = mob.Hit(in Poke, attacker);

            Assert.That(landed, Is.True);
            Assert.That(mob.CombatState, Is.EqualTo(CombatState.LightHit), "잡몹은 예전대로 경직에 걸린다");
        }

        // ── 패링과의 관계 ────────────────────────────────

        [Test]
        public void ParryingAnArmoredBoss_NullifiesDamageButNotTheBoss()
        {
            Combat boss = NewBoss();
            Combat player = NewAttacker();

            // 보스가 플레이어 정면에 있다. Physics.Facing 기본값이 +X다.
            boss.transform.position = new Vector3(3f, 0f, 0f);

            player.BeginParryWindow();
            bool landed = player.Hit(in Poke, boss);

            Assert.That(landed, Is.False, "패링은 그대로 성립한다");
            Assert.That(player.Health.CurValue, Is.EqualTo(player.Health.MaxValue));
            Assert.That(boss.CombatState, Is.EqualTo(CombatState.Neutral),
                        "아머라서 반격 경직은 무시된다 — 막을 수는 있어도 끊을 수는 없다");
        }

        [Test]
        public void ParryingABrokenBoss_AlsoStunsIt()
        {
            Combat boss = NewBoss();
            Combat player = NewAttacker();
            boss.transform.position = new Vector3(3f, 0f, 0f);

            DrainToBreak(boss, player);

            player.BeginParryWindow();
            player.Hit(in Poke, boss);

            Assert.That(boss.CombatState, Is.EqualTo(CombatState.LightHit),
                        "브레이크 중이면 반격 경직도 걸린다");
        }

        // ── 표시 우선순위 ────────────────────────────────

        [Test]
        public void Tint_ArmorBeatsTelegraph_AndStateBeatsBoth()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);

            Color armor = CombatStateVisuals.Tint(identity, CombatState.Neutral, CombatOverlay.SuperArmor);
            Color telegraph = CombatStateVisuals.Tint(identity, CombatState.Neutral, CombatOverlay.Telegraph);
            Color hit = CombatStateVisuals.Tint(identity, CombatState.Knockback, CombatOverlay.SuperArmor);

            Assert.That(armor, Is.Not.EqualTo(identity), "아머는 색이 붙는다");
            Assert.That(armor, Is.Not.EqualTo(telegraph), "아머와 예고는 다른 색이다");
            Assert.That(hit, Is.EqualTo(CombatStateVisuals.Tint(identity, CombatState.Knockback)),
                        "전투 상태가 겹침 표시를 이긴다");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>Combat의 인스펙터 기본값. 테스트가 계산에 쓴다.</summary>
        private const float DefaultGuardDamage = 1f;
        private const float RegenDelay = 3f;

        /// <summary>큰 기술. 데미지가 커도 가드는 한 대분만 깎는다.</summary>
        private static readonly HitData Heavy = new HitData
        {
            damageData = new DamageData(40f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.AwayFromCaster,
            hitStunDuration = 0.4f,
        };

        /// <summary>데미지가 없는 판정. 패링 반격이 이런 모양이다.</summary>
        private static readonly HitData Harmless = new HitData
        {
            damageData = new DamageData(0f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.AwayFromCaster,
            hitStunDuration = 0.6f,
        };

        private static readonly HitData Poke = new HitData
        {
            damageData = new DamageData(5f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.AwayFromCaster,
            hitStunDuration = 0.3f,
        };

        private static readonly HitData HardKnockback = new HitData
        {
            damageData = new DamageData(5f),
            targetState = CombatState.Neutral,
            nextState = CombatState.Knockback,
            mode = KnockbackMode.AwayFromCaster,
            knockbackForce = 20f,
            hitStunDuration = 0.5f,
        };

        private static readonly HitData Launcher = new HitData
        {
            damageData = new DamageData(5f),
            targetState = CombatState.Neutral,
            nextState = CombatState.AerialHit,
            mode = KnockbackMode.Up,
            launchForce = 12f,
            hitStunDuration = 0.5f,
        };

        /// <summary>가드 파괴가 특기인 타격. 한 대가 세 대분으로 들어간다.</summary>
        private static readonly HitData GuardBreaker = new HitData
        {
            damageData = new DamageData(5f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.AwayFromCaster,
            hitStunDuration = 0.3f,
            guardDamage = 3f,
        };

        private static HitData Lethal => new HitData
        {
            damageData = new DamageData(99999f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.AwayFromCaster,
            hitStunDuration = 0.3f,
        };

        /// <summary>가드가 0이 될 때까지 때린다. 브레이크를 여는 타격까지 포함한다.</summary>
        private static void DrainToBreak(Combat boss, Combat attacker)
        {
            for (int i = 0; i < 64 && !boss.IsGuardBroken; i++)
                boss.Hit(in GuardBreaker, attacker);
        }

        private Combat NewBoss()
        {
            Combat boss = NewAttacker();
            boss.SetMaxHealth(400f);
            boss.SetGuard(Guard, BreakDuration);
            return boss;
        }

        private Combat NewAttacker()
        {
            var go = new GameObject("Body");
            spawned.Add(go);

            go.AddComponent<Ally>();
            return go.GetComponent<Combat>();
        }
    }
}
