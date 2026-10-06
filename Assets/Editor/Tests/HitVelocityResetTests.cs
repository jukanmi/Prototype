using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 히트 시 대상의 속력을 0으로 만든 다음 넉백·띄우기가 얹힌다.
    ///
    /// 남은 관성 위에 힘을 더하면 같은 타격 데이터인데도 대상이 달려오던 중이냐,
    /// 떨어지던 중이냐에 따라 밀리는 거리와 뜨는 높이가 달라진다. 공중 연계는
    /// 높이가 재현되지 않으면 후속타 히트박스가 그냥 빗나간다.
    ///
    /// 단 수직은 <b>낙하 성분만</b> 끊는다. 상승 중까지 0으로 맞추면 띄워 놓은 몸이
    /// 후속타를 맞는 순간 정점에서 뚝 끊겨 떨어진다.
    ///
    /// 띄우기 높이는 세 층이 겹친다 — 스킬 데이터(<c>airborneHeight</c> · <c>aerialAirborneHeight</c>),
    /// 공중 최소 부양(<c>airHitLift</c>), 캐릭터별 배율(<c>airLaunchScale</c>).
    /// 아래 테스트는 층을 하나씩 <see cref="SetField"/>로 꺼 두고 본다 —
    /// 프리팹 튜닝값이 바뀔 때마다 테스트가 깨지면 안 된다.
    /// </summary>
    public class HitVelocityResetTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        // ── 속력 리셋 ────────────────────────────────────

        [Test]
        public void Hit_WhileFalling_StopsFallBeforeApplyingForce()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);

            victim.Physics.AddLaunch(20f);
            SetVerticalVelocity(victim.Physics, -18f);   // 정점을 지나 떨어지는 중

            attacker.Attack(victim, in Poke);

            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(0f).Within(0.0001f),
                        "낙하 속력은 끊는다 — 남으면 이어지는 띄우기 높이가 줄어든다");
            Assert.That(victim.Physics.PhysicsState, Is.EqualTo(PhysicsState.Aerial),
                        "속력만 0이다 — 공중 상태는 유지되어 그 자리에서 떨어진다");
        }

        [Test]
        public void Hit_WhileRising_KeepsVerticalSpeed()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);

            victim.Physics.AddLaunch(20f);   // 올라가는 중

            attacker.Attack(victim, in Poke);

            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(20f).Within(0.0001f),
                        "상승 중에 끊으면 띄워 둔 몸이 정점에서 뚝 떨어진다");
        }

        [Test]
        public void Hit_ClearsHorizontalSpeed()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);

            victim.Physics.AddImpulse(Vector3.right, 15f);
            Assert.That(victim.Physics.HorizontalVelocity.magnitude, Is.GreaterThan(1f),
                        "선행 조건: 수평 속력이 실려 있다");

            attacker.Attack(victim, in Poke);

            Assert.That(victim.Physics.HorizontalVelocity.magnitude, Is.EqualTo(0f).Within(0.0001f),
                        "넉백이 없는 타격은 대상을 그 자리에 세운다");
        }

        // ── 띄우기 힘 선택 ───────────────────────────────

        [Test]
        public void Launch_FromGround_UsesAuthoredHeight()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);

            attacker.Attack(victim, in DualLauncher);

            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(12f).Within(0.0001f),
                        "지상 첫 타는 airborneHeight를 그대로 환산한 속도 — 시작 높이가 변하면 안 된다");
        }

        [Test]
        public void AerialHeight_ReplacesAirborneHeight_WhenTargetIsAerial()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);

            Airborne(victim);

            attacker.Attack(victim, in DualLauncher);

            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(22f).Within(0.0001f),
                        "공중 대상에는 aerialAirborneHeight가 쓰여야 한다");
        }

        [Test]
        public void AerialHeight_Zero_FallsBackToAirborneHeight()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);

            Airborne(victim);

            // Launcher는 aerialAirborneHeight가 비어 있다 — 공중 전용 값을 안 채운 스킬이다.
            attacker.Attack(victim, in Launcher);

            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(12f).Within(0.0001f),
                        "값을 안 채운 스킬은 airborneHeight로 뜬다");
        }

        [Test]
        public void Launch_WhileRising_NeverSlowsTheBody()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);

            // 이미 크게 떠 있는 상태에서 더 약한 띄우기를 맞는다.
            victim.Physics.AddLaunch(20f);

            attacker.Attack(victim, in Launcher);

            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(20f).Within(0.0001f),
                        "약한 후속타가 상승속도를 깎으면 몸이 올라가다 주저앉는다");
        }

        // ── 공중 최소 부양(airHitLift) ────────────────────

        [Test]
        public void AirHitLift_LiftsEvenHitsWithoutLaunchForce()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);
            SetField(victim.Physics, "airHitLift", 4f);

            Airborne(victim);

            // 띄우기 값이 전혀 없는 평타.
            attacker.Attack(victim, in Poke);

            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(4f).Within(0.0001f),
                        "공중에서는 평타도 최소 부양만큼 올라가야 체공이 늘어난다");
        }

        [Test]
        public void AirHitLift_NeverLiftsAGroundedTarget()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);
            SetField(victim.Physics, "airHitLift", 4f);

            attacker.Attack(victim, in Poke);

            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(0f).Within(0.0001f),
                        "지상 평타가 띄우면 지상 콤보가 통째로 사라진다");
            Assert.That(victim.Physics.PhysicsState, Is.EqualTo(PhysicsState.Ground),
                        "지상 대상은 지상에 남는다");
        }

        [Test]
        public void AirHitLift_IsAFloor_NotABonus()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);
            SetField(victim.Physics, "airHitLift", 4f);

            Airborne(victim);

            attacker.Attack(victim, in DualLauncher);

            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(22f).Within(0.0001f),
                        "부양은 바닥값이다 — aerialAirborneHeight에 더해지면 공중 연계가 천장을 뚫는다");
        }

        // ── 캐릭터별 배율(airLaunchScale) ─────────────────

        [Test]
        public void AirLaunchScale_AppliesOnlyInTheAir()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);
            SetField(victim.Physics, "airLaunchScale", 1.5f);

            attacker.Attack(victim, in Launcher);
            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(12f).Within(0.0001f),
                        "지상 첫 타에는 배율이 안 걸린다 — 띄우기 시작 높이가 흔들리면 안 된다");

            Airborne(victim);
            attacker.Attack(victim, in Launcher);

            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(18f).Within(0.0001f),
                        "공중 타격에는 배율이 걸린다 (12 x 1.5)");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>제대로 띄우는 타격. 높이 2.4 → 속도 12(√(2·30·2.4)). 공중 전용 값은 비어 있다.</summary>
        private static readonly HitData Launcher = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.AerialHit,
            mode = KnockbackMode.Up,
            airborneHeight = 2.4f,
            hitStunDuration = 0.5f,
        };

        /// <summary>지상과 공중에 서로 다른 띄우기 높이를 싣는 타격. 8.0666667 → 속도 22.</summary>
        private static readonly HitData DualLauncher = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.AerialHit,
            mode = KnockbackMode.Up,
            airborneHeight = 2.4f,
            aerialAirborneHeight = 8.0666667f,
            hitStunDuration = 0.5f,
        };

        /// <summary>넉백도 띄우기도 없는 약경직 한 대.</summary>
        private static readonly HitData Poke = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            hitStunDuration = 0.3f,
        };

        /// <summary>
        /// 프리팹 튜닝값(부양 · 배율)을 꺼서 스킬 데이터만 남긴다.
        /// 이 둘의 기본값이 바뀌었다고 다른 테스트가 깨지면 안 된다.
        /// </summary>
        private static void Plain(Combat combat)
        {
            SetField(combat.Physics, "airHitLift", 0f);
            SetField(combat.Physics, "airLaunchScale", 1f);
        }

        /// <summary>
        /// 정점을 지나 떨어지는 공중 상태로 만든다. <c>StopFall</c>이 낙하 속도를 0으로
        /// 끊으므로 이어지는 띄우기 힘이 그대로 최종 속도가 된다 — 값을 딱 떨어지게 볼 수 있다.
        /// </summary>
        private static void Airborne(Combat combat)
        {
            combat.Physics.AddLaunch(4f);
            SetVerticalVelocity(combat.Physics, -1f);
        }

        /// <summary>낙하 상태를 만든다. EditMode에는 FixedUpdate가 없어 중력을 굴릴 수 없다.</summary>
        private static void SetVerticalVelocity(Prototype.Physics physics, float value)
            => SetField(physics, "verticalVelocity", value);

        /// <summary>
        /// 타입을 <c>Prototype.Physics</c>로 전부 적어 준다 — <c>using UnityEngine;</c>이
        /// 같이 들어와 있어 짧게 쓰면 <c>UnityEngine.Physics</c>와 겹쳐 CS0104가 난다.
        /// </summary>
        private static void SetField(Prototype.Physics physics, string field, float value)
        {
            FieldInfo f = typeof(Prototype.Physics)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(f, Is.Not.Null, $"Physics.{field}가 사라졌다 — 테스트를 같이 고칠 것");
            f.SetValue(physics, value);
        }

        private Combat NewCombat(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.AddComponent<Ally>();
            return go.GetComponent<Combat>();
        }
    }
}
