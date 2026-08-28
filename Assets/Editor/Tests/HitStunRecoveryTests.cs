using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 경직 고착 회귀 방지.
    ///
    /// 넉백 · 공중피격 계열은 <c>OnStunEnd</c>가 상태를 바꾸지 않는다 — 착지(<c>Physics.OnLand</c>)로만
    /// 풀리게 되어 있다. 그런데 지상에 서 있던 대상을 <c>airborneHeight</c> 없이 밀치면 Aerial로 가지 않아
    /// OnLand가 영영 오지 않고, 그대로 영구 경직이 된다(EnemyChargeAction · SK_AR2B 등이 그런 타격이다).
    /// </summary>
    public class HitStunRecoveryTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        [Test]
        public void GroundedKnockback_DoesNotStayStunnedForever()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");

            attacker.Attack(victim, in GroundKnockback);
            Assert.That(victim.CombatState, Is.EqualTo(CombatState.Knockback), "선행 조건: 넉백에 들어가야 한다");
            Assert.That(victim.Physics.PhysicsState, Is.EqualTo(PhysicsState.Ground),
                        "선행 조건: 띄우기가 없으므로 지상에 남아 있다");

            // 경직은 0.5초. 넉넉히 넘긴다.
            victim.Tick(1f);

            Assert.That(victim.CombatState, Is.EqualTo(CombatState.Down),
                        "착지 이벤트가 오지 않아도 넉백은 다운으로 넘어가야 한다");
        }

        [Test]
        public void GroundedKnockback_EventuallyReturnsToNeutral()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");

            attacker.Attack(victim, in GroundKnockback);

            victim.Tick(1f);    // 경직 종료 → 다운
            victim.Tick(2f);    // 다운 종료 → 기상
            victim.Tick(1f);    // 기상 완료 → 복귀

            Assert.That(victim.CombatState, Is.EqualTo(CombatState.Neutral));
            Assert.That(CombatStateRules.IsStunned(victim.CombatState), Is.False);
        }

        [Test]
        public void AerialState_WithoutLaunch_AlsoRecovers()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");

            attacker.Attack(victim, in GroundLauncherWithoutHeight);
            Assert.That(victim.CombatState, Is.EqualTo(CombatState.AerialHit), "선행 조건: 공중피격 상태다");

            victim.Tick(1f);

            Assert.That(victim.CombatState, Is.EqualTo(CombatState.Down),
                        "뜨지 못한 공중피격도 바닥에 있으므로 다운으로 넘어가야 한다");
        }

        /// <summary>
        /// 교대로 내려가는 몸은 경직을 물고 가지 않는다. 꺼진 몸은 Tick이 멈추고,
        /// 다시 설 때 Teleport가 착지 이벤트 없이 지면에 세우기 때문이다.
        /// </summary>
        [Test]
        public void Benching_ClearsHitStun()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");

            attacker.Attack(victim, in Launcher);
            Assert.That(CombatStateRules.IsStunned(victim.CombatState), Is.True, "선행 조건: 경직 상태다");

            victim.gameObject.SetActive(false);

            Assert.That(victim.CombatState, Is.EqualTo(CombatState.Neutral));
            Assert.That(victim.AirHitCount, Is.Zero, "공중 히트 누적도 함께 지운다");
        }

        /// <summary>
        /// 경직 타이머가 공중에서 이미 다 닳은 뒤 지면으로 옮겨지는 경우.
        /// Tick이 타이머 0에서 빠져나가면 복구 검사가 다시는 돌지 않는다.
        /// </summary>
        [Test]
        public void TeleportedToGround_AfterStunExpired_StillRecovers()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");

            attacker.Attack(victim, in Launcher);
            Assert.That(victim.Physics.PhysicsState, Is.EqualTo(PhysicsState.Aerial), "선행 조건: 떠 있다");

            // 공중에 뜬 채로 경직만 끝난다. 착지 이벤트는 아직 없다.
            victim.Tick(1f);
            Assert.That(victim.CombatState, Is.EqualTo(CombatState.AerialHit));

            // 교대 복귀 · 불릿타임 배치가 하는 일 — 착지 이벤트 없이 지면에 세운다.
            victim.Physics.Teleport(victim.Physics.GroundPosition);

            victim.Tick(0.1f);

            Assert.That(victim.CombatState, Is.EqualTo(CombatState.Down),
                        "타이머가 이미 0이어도 지면에 서면 다운으로 풀려야 한다");
        }

        [Test]
        public void LightHit_StillRecoversToNeutralDirectly()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");

            attacker.Attack(victim, in Poke);
            victim.Tick(1f);

            Assert.That(victim.CombatState, Is.EqualTo(CombatState.Neutral),
                        "약경직은 다운을 거치지 않는다");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>지상 넉백. airborneHeight가 없어 대상이 뜨지 않는다 — 문제의 타격이다.</summary>
        private static readonly HitData GroundKnockback = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.Knockback,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            pushDistance = 1f,
            hitStunDuration = 0.5f,
        };

        /// <summary>띄우기를 요청하지만 airborneHeight가 비어 있는 타격.</summary>
        private static readonly HitData GroundLauncherWithoutHeight = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.AerialHit,
            mode = KnockbackMode.Up,
            hitStunDuration = 0.5f,
        };

        /// <summary>제대로 띄우는 타격. 대상이 Aerial로 올라간다.</summary>
        private static readonly HitData Launcher = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.AerialHit,
            mode = KnockbackMode.Up,
            airborneHeight = 2.4f,
            hitStunDuration = 0.5f,
        };

        /// <summary>약경직 한 대.</summary>
        private static readonly HitData Poke = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            hitStunDuration = 0.3f,
        };

        private Combat NewCombat(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.AddComponent<Ally>();
            return go.GetComponent<Combat>();
        }
    }
}
