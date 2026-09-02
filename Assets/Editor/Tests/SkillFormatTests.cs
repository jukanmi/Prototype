using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 스킬 데이터의 <b>저작 어휘</b>가 실제로 화면에서 재어지는지.
    ///
    /// HitData는 힘(초기 속도 · 충격량)이 아니라 기획서와 같은 말로 적는다 —
    /// <c>airborneHeight</c>(유닛) · <c>pushDistance</c>(유닛) · <c>castDelay</c>(초).
    /// 그래서 이 파일이 지키는 건 하나다: <b>적은 수가 그대로 나오는가.</b>
    /// 여기가 깨지면 기획서를 옮겨 적어도 다른 값이 나온다 — 포맷의 존재 이유가 사라진다.
    /// </summary>
    public class SkillFormatTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);
            for (int i = 0; i < assets.Count; i++)
                Object.DestroyImmediate(assets[i]);

            spawned.Clear();
            assets.Clear();
        }

        // ── 높이 · 거리 환산 ─────────────────────────────

        [Test]
        public void LaunchForHeight_AndApexHeight_RoundTrip()
        {
            Prototype.Physics p = NewPhysics("RoundTrip", Vector3.zero);

            foreach (float height in new[] { 0.267f, 0.6f, 1.3f, 2.4f, 8.0666667f })
            {
                float v = p.LaunchForHeight(height);
                float back = KnockbackPreview.ApexHeight(v, p.Gravity);

                Assert.That(back, Is.EqualTo(height).Within(0.001f),
                            $"높이 {height}를 속도로 폈다 되돌리면 원값이어야 한다");
            }
        }

        [Test]
        public void AuthoredHeight_BecomesThatApex_OnGround()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);

            attacker.Attack(victim, in Launcher);

            float apex = KnockbackPreview.ApexHeight(victim.Physics.VerticalVelocity, victim.Physics.Gravity);

            Assert.That(apex, Is.EqualTo(1.3f).Within(0.001f),
                        "지상 대상은 저작한 높이 그대로 뜬다 — 기획서 수가 곧 화면의 높이다");
        }

        [Test]
        public void PushDistance_IsTheTravelDistance()
        {
            Prototype.Physics victim = NewPhysics("Victim", new Vector3(3f, 0f, 0f));

            SkillData data = NewSkill(new HitData
            {
                damageData = new DamageData(1f),
                nextState = CombatState.Knockback,
                mode = KnockbackMode.AwayFromCaster,
                pushDistance = 1.25f,
                hitStunDuration = 0.3f,
            });

            Assert.That(KnockbackPreview.TryPredict(data, Vector3.zero, Vector3.right, victim, out var r), Is.True);

            Assert.That(r.to.x - r.from.x, Is.EqualTo(1.25f).Within(0.001f),
                        "저작한 거리가 곧 이동 거리다");

            // 실전(Combat)은 이 거리를 충격량으로 편다. 왕복이 무손실이어야 화살표가 진실이다.
            Assert.That(victim.TravelForImpulse(victim.ImpulseToTravel(1.25f)), Is.EqualTo(1.25f).Within(0.001f));
        }

        [Test]
        public void HurtboxSize_ReadsTheRootCollider()
        {
            var go = new GameObject("Body");
            spawned.Add(go);

            CapsuleCollider cap = go.AddComponent<CapsuleCollider>();
            cap.radius = 0.5f;
            cap.height = 1f;

            Entity e = go.AddComponent<Ally>();

            Assert.That(e.HurtboxSize, Is.EqualTo(new Vector3(1f, 1f, 1f)),
                        "피격 범위는 루트 콜라이더에서 읽는다 — 기획서 배수의 기준점이다");
        }

        // ── 에어본 상한 (기획서 비고 행) ─────────────────

        [Test]
        public void CapAirborne_DoesNotRaiseApex_OnRepeatHit()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);

            attacker.Attack(victim, in Launcher);
            float first = ApexOf(victim);

            // 뜬 채로 같은 스킬을 다시 맞는다.
            attacker.Attack(victim, in Launcher);

            Assert.That(ApexOf(victim), Is.LessThanOrEqualTo(first + 0.001f),
                        "상한이 걸린 스킬은 이미 뜬 대상을 자기 높이 위로 더 올리지 않는다");
        }

        [Test]
        public void WithoutCap_AirLaunchScale_RaisesSpeed()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            SetField(victim.Physics, "airHitLift", 0f);
            SetField(victim.Physics, "airLaunchScale", 1.5f);

            HitData uncapped = Launcher;
            uncapped.capAirborne = false;

            attacker.Attack(victim, in uncapped);
            float first = victim.Physics.VerticalVelocity;

            attacker.Attack(victim, in uncapped);

            Assert.That(victim.Physics.VerticalVelocity, Is.GreaterThan(first),
                        "상한을 끄면 공중 배율이 그대로 걸려 맞을수록 더 뜬다 — 기존 동작");
        }

        // ── 타별 선딜 (연격) ─────────────────────────────

        [Test]
        public void NoCastDelay_FallsBackToHitInterval()
        {
            SkillData d = NewTimed(castTime: 0.15f, interval: 0.3f, hits: 3);

            Assert.That(d.HitTime(0), Is.EqualTo(0.15f).Within(0.0001f), "첫 타는 castTime 그대로");
            Assert.That(d.HitTime(1), Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(d.HitTime(2), Is.EqualTo(0.75f).Within(0.0001f),
                        "castDelay를 안 채운 스킬은 예전처럼 hitInterval로 균등하다");
        }

        [Test]
        public void PerHitCastDelay_MatchesTheDesignTable()
        {
            // 연격 — 선딜 0.2 / 0.2 / 0.3
            SkillData d = NewTimed(castTime: 0.2f, interval: 0.3f, hits: 0);
            d.hitDataList = new List<HitData>
            {
                Beat(10f, delay: 0f),
                Beat(12f, delay: 0.2f),
                Beat(13f, delay: 0.3f),
            };

            Assert.That(d.HitTime(0), Is.EqualTo(0.2f).Within(0.0001f), "1타 선딜 0.2");
            Assert.That(d.HitTime(1), Is.EqualTo(0.4f).Within(0.0001f), "2타는 0.2 뒤");
            Assert.That(d.HitTime(2), Is.EqualTo(0.7f).Within(0.0001f), "3타는 다시 0.3 뒤");

            d.recoveryTime = 0.2f;
            Assert.That(d.TotalDuration, Is.EqualTo(0.9f).Within(0.0001f),
                        "마지막 타(0.7) + 후딜(0.2). 콤보 큐와 SkillState가 같은 함수를 봐야 한다");
        }

        [Test]
        public void FirstHitDelay_StacksOnCastTime()
        {
            // 내려찍기 — 0.2s 점프/이동 + 0.3s 그 뒤 선딜.
            SkillData d = NewTimed(castTime: 0.2f, interval: 0.3f, hits: 0);
            d.hitDataList = new List<HitData> { Beat(25f, delay: 0.3f) };

            Assert.That(d.HitTime(0), Is.EqualTo(0.5f).Within(0.0001f),
                        "첫 타의 castDelay는 간격이 아니라 castTime 위에 얹는 2단 선딜이다");
        }

        // ── 타별 시전 범위 (연격) ────────────────────────

        [Test]
        public void PerHitRange_OverridesSkillDefault()
        {
            SkillData d = NewTimed(castTime: 0.2f, interval: 0.2f, hits: 0);
            d.castRangeScale = new Vector3(2f, 0.65f, 1.5f);
            d.hitDataList = new List<HitData>
            {
                Beat(10f),                                              // 타별 값 없음
                Beat(12f, range: new Vector3(2f, 0.8f, 1.5f)),
                Beat(13f, range: new Vector3(1.8f, 1.1f, 1.5f)),
            };

            Assert.That(d.RangeScaleFor(d.hitDataList[0]), Is.EqualTo(new Vector3(2f, 0.65f, 1.5f)),
                        "타별 값이 없으면 스킬 공통값으로 떨어진다 — 단타 스킬은 예전과 같다");
            Assert.That(d.RangeScaleFor(d.hitDataList[1]), Is.EqualTo(new Vector3(2f, 0.8f, 1.5f)));
            Assert.That(d.RangeScaleFor(d.hitDataList[2]), Is.EqualTo(new Vector3(1.8f, 1.1f, 1.5f)));
        }

        [Test]
        public void PartialRange_IsTreatedAsUnset()
        {
            SkillData d = NewTimed(castTime: 0.2f, interval: 0.2f, hits: 0);
            d.castRangeScale = new Vector3(2f, 0.6f, 1.3f);
            d.hitDataList = new List<HitData> { Beat(10f, range: new Vector3(2f, 0f, 1.5f)) };

            Assert.That(d.RangeScaleFor(d.hitDataList[0]), Is.EqualTo(d.castRangeScale),
                        "한 축이라도 0이면 미지정이다 — 두께 0짜리 히트박스를 만들지 않는다");
        }

        // ── 마무리기: 내리꽂기 (내려찍기) ──────────────────

        [Test]
        public void Slam_DealsDamageToTarget()
        {
            Combat attacker = NewCombat("Attacker");
            Combat airborne = NewCombat("Airborne");
            Plain(airborne);
            Airborne(airborne);

            float airborneBefore = airborne.Health.CurValue;
            attacker.Attack(airborne, in Slam);

            Assert.That(airborneBefore - airborne.Health.CurValue, Is.EqualTo(40f).Within(0.001f),
                        "내려찍기 마무리 피해 40");
        }

        [Test]
        public void Slam_DrivesAirborneTargetDown()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);
            Airborne(victim);

            attacker.Attack(victim, in Slam);

            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(-20f).Within(0.001f),
                        "떠 있던 적은 아래로 꽂힌다 — 음수 높이가 곧 내리꽂기다");
        }

        [Test]
        public void Slam_IgnoresGroundedTarget()
        {
            Combat attacker = NewCombat("Attacker");
            Combat victim = NewCombat("Victim");
            Plain(victim);

            attacker.Attack(victim, in Slam);

            Assert.That(victim.Physics.PhysicsState, Is.EqualTo(PhysicsState.Ground),
                        "지상 적에게는 꽂을 높이가 없다 — 걸면 서 있던 적이 이유 없이 다운된다");
            Assert.That(victim.Physics.VerticalVelocity, Is.EqualTo(0f).Within(0.001f));
        }

        // ── 모으기 기준점 (사슬견인) ─────────────────────

        [Test]
        public void PullAnchor_ConvergesEveryoneOnTheSamePoint()
        {
            // 반지름 4의 1/3 지점(1.33)으로 전부 모은다.
            // Combat.PushClamped가 기준점까지로 거리를 잘라 주므로 멀든 가깝든 같은 자리에 선다.
            Vector3 anchor = new Vector3(1.3333f, 0f, 0f);

            foreach (float startX in new[] { 2f, 3f, 4f })
            {
                Prototype.Physics victim = NewPhysics("Victim", new Vector3(startX, 0f, 0f));

                SkillData data = NewSkill(new HitData
                {
                    damageData = new DamageData(15f),
                    nextState = CombatState.LightHit,
                    mode = KnockbackMode.TowardCaster,
                    pushDistance = 4f,
                    hitStunDuration = 0.3f,
                }.WithOrigin(anchor));

                Assert.That(KnockbackPreview.TryPredict(data, anchor, Vector3.right, victim, out var r), Is.True);
                Assert.That(r.to.x, Is.EqualTo(anchor.x).Within(0.001f),
                            $"{startX}에서 시작해도 기준점에 선다 — 지나쳐 반대편으로 튀지 않는다");
            }
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>올려베기 한 타. 높이 1.3 · 상한 켜짐 · 밀치기 없음.</summary>
        private static readonly HitData Launcher = new HitData
        {
            damageData = new DamageData(15f),
            targetState = CombatState.Neutral,
            nextState = CombatState.AerialHit,
            mode = KnockbackMode.Up,
            airborneHeight = 1.3f,
            capAirborne = true,
            hitStunDuration = 0.3f,
        };

        /// <summary>내려찍기 한 타. 40 · 아래로 6.667(속도 20) · 경직 없음.</summary>
        private static readonly HitData Slam = new HitData
        {
            damageData = new DamageData(40f),
            targetState = CombatState.Neutral,
            nextState = CombatState.AerialHit,
            mode = KnockbackMode.Up,
            airborneHeight = -6.6666667f,
            hitStunDuration = 0f,
        };

        private static HitData Beat(float dmg, float delay = 0f, Vector3 range = default) => new HitData
        {
            damageData = new DamageData(dmg),
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            castDelay = delay,
            castRangeScale = range,
            hitStunDuration = 0.3f,
        };

        /// <summary>지금 속도가 만들어 낼 정점을 발판 기준 높이로 돌려준다.</summary>
        private static float ApexOf(Combat c)
            => c.Physics.Height + KnockbackPreview.ApexHeight(c.Physics.VerticalVelocity, c.Physics.Gravity);

        /// <summary>프리팹 튜닝값(부양 · 배율)을 꺼서 저작값만 보이게 한다.</summary>
        private static void Plain(Combat combat)
        {
            SetField(combat.Physics, "airHitLift", 0f);
            SetField(combat.Physics, "airLaunchScale", 1f);
        }

        /// <summary>공중 상태로 올린다. 내리꽂기 · 에어본 보너스가 볼 조건이다.</summary>
        private static void Airborne(Combat combat) => combat.Physics.AddLaunch(6f);

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

        private Prototype.Physics NewPhysics(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.transform.position = pos;
            return go.AddComponent<Prototype.Physics>();
        }

        private SkillData NewSkill(params HitData[] hits)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            assets.Add(data);

            data.skillName = "테스트";
            data.hitDataList = new List<HitData>(hits);
            return data;
        }

        private SkillData NewTimed(float castTime, float interval, int hits)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            assets.Add(data);

            data.skillName = "테스트";
            data.castTime = castTime;
            data.hitInterval = interval;
            data.recoveryTime = 0f;

            var list = new List<HitData>();
            for (int i = 0; i < hits; i++) list.Add(Beat(10f));
            data.hitDataList = list;

            return data;
        }
    }
}
