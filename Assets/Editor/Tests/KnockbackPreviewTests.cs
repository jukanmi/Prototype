using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 밀치기 프리뷰가 실전과 같은 자리를 찍는지.
    ///
    /// 프리뷰는 순수 계산이므로 씬 없이 돈다. 여기서 어긋나면 화면의 화살표가 곧 거짓말이 된다 —
    /// 유저는 그걸 믿고 콤보를 설계한다.
    /// </summary>
    public class KnockbackPreviewTests
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

        [Test]
        public void AwayFromCaster_TravelsForceOverDamping()
        {
            Physics victim = NewPhysics("Victim", new Vector3(3f, 0f, 0f));
            SkillData data = NewSkill(Push(8f));

            Assert.That(KnockbackPreview.TryPredict(data, Vector3.zero, Vector3.right, victim, out var r), Is.True);

            float expected = 8f / victim.ImpulseDamping;
            Assert.That(r.to.x - r.from.x, Is.EqualTo(expected).Within(0.001f),
                        "이동 거리는 force / impulseDamping이다 — Physics.AddImpulse의 지수감쇠 총량");
            Assert.That(r.to.z, Is.EqualTo(r.from.z).Within(0.001f), "밀치기는 깊이를 바꾸지 않는다");
        }

        [Test]
        public void TowardCaster_DoesNotOvershootCenter()
        {
            // 중심에서 1유닛. 힘은 넘치게 준다 — 자르지 않으면 반대편으로 튄다.
            Physics victim = NewPhysics("Victim", new Vector3(1f, 0f, 0f));
            SkillData data = NewSkill(Pull(32f));

            Assert.That(KnockbackPreview.TryPredict(data, Vector3.zero, Vector3.right, victim, out var r), Is.True);

            Assert.That(r.to.x, Is.EqualTo(0f).Within(0.001f), "중심에 멈춰야 한다");
            Assert.That(r.to.x, Is.GreaterThanOrEqualTo(-0.001f), "중심을 지나쳐 반대편으로 가면 안 된다");
        }

        [Test]
        public void Up_HasNoHorizontalTravel_ButHasApex()
        {
            Physics victim = NewPhysics("Victim", new Vector3(2f, 0f, 0f));
            SkillData data = NewSkill(Launch(12f));

            Assert.That(KnockbackPreview.TryPredict(data, Vector3.zero, Vector3.right, victim, out var r), Is.True);

            Assert.That((r.to - r.from).magnitude, Is.EqualTo(0f).Within(0.001f), "띄우기는 수평으로 밀지 않는다");
            Assert.That(r.apexHeight, Is.EqualTo(12f * 12f / (2f * victim.Gravity)).Within(0.001f));
        }

        [Test]
        public void MultiHit_AccumulatesEachHit()
        {
            Physics single = NewPhysics("Single", new Vector3(3f, 0f, 0f));
            Physics twice = NewPhysics("Twice", new Vector3(3f, 0f, 0f));

            SkillData one = NewSkill(Push(8f));
            SkillData two = NewSkill(Push(8f), Push(8f));

            KnockbackPreview.TryPredict(one, Vector3.zero, Vector3.right, single, out var a);
            KnockbackPreview.TryPredict(two, Vector3.zero, Vector3.right, twice, out var b);

            float oneTravel = a.to.x - a.from.x;
            float twoTravel = b.to.x - b.from.x;

            Assert.That(twoTravel, Is.EqualTo(oneTravel * 2f).Within(0.001f),
                        "다단히트는 타마다 누적된다 — 마지막 위치가 진짜 착지점");
        }

        [Test]
        public void PureDamage_DrawsNothing()
        {
            Physics victim = NewPhysics("Victim", new Vector3(3f, 0f, 0f));
            SkillData data = NewSkill(new HitData { damageData = new DamageData(10f), hitStunDuration = 0.3f });

            Assert.That(KnockbackPreview.MovesTarget(data), Is.False);
            Assert.That(KnockbackPreview.TryPredict(data, Vector3.zero, Vector3.right, victim, out _), Is.False,
                        "밀치기도 띄우기도 없으면 그릴 게 없다");
        }

        [Test]
        public void ApproachSpot_StaysOnApproachSideAndTargetLane()
        {
            Vector3 spot = KnockbackPreview.ApproachSpot(new Vector3(5f, 0f, 0f), new Vector3(2f, 0f, 1.5f), 1.1f);

            Assert.That(spot.x, Is.EqualTo(3.1f).Within(0.001f), "오던 쪽(오른쪽)에 붙는다 — 대상을 관통하지 않는다");
            Assert.That(spot.z, Is.EqualTo(1.5f).Within(0.001f), "깊이는 대상 레인에 맞춘다");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private static HitData Push(float force) => new HitData
        {
            damageData = new DamageData(1f),
            nextState = CombatState.Knockback,
            mode = KnockbackMode.AwayFromCaster,
            knockbackForce = force,
            hitStunDuration = 0.3f,
        };

        private static HitData Pull(float force) => new HitData
        {
            damageData = new DamageData(1f),
            nextState = CombatState.LightHit,
            mode = KnockbackMode.TowardCaster,
            knockbackForce = force,
            hitStunDuration = 0.3f,
        };

        private static HitData Launch(float force) => new HitData
        {
            damageData = new DamageData(1f),
            nextState = CombatState.AerialHit,
            mode = KnockbackMode.Up,
            launchForce = force,
            hitStunDuration = 0.3f,
        };

        private Physics NewPhysics(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.transform.position = pos;
            return go.AddComponent<Physics>();
        }

        private SkillData NewSkill(params HitData[] hits)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            assets.Add(data);

            data.skillName = "테스트";
            data.hitDataList = new List<HitData>(hits);
            return data;
        }
    }
}
