using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 설치기 — 시전자는 설치만 하고 풀려나고, 후속타는 <see cref="Installation"/>이
    /// <see cref="SkillData.HitTime"/> 타임라인대로 설치 지점에서 낸다.
    /// </summary>
    public class InstallationTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            // 상태가 만든 [설치기] 오브젝트까지 치운다.
            foreach (Installation inst in Object.FindObjectsByType<Installation>(FindObjectsSortMode.None))
                Object.DestroyImmediate(inst.gameObject);

            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            for (int i = 0; i < assets.Count; i++)
                Object.DestroyImmediate(assets[i]);

            spawned.Clear();
            assets.Clear();
        }

        [Test]
        public void Install_TotalDuration_IgnoresHits()
        {
            SkillData data = NewInstallSkill(hits: 3);

            Assert.That(data.TotalDuration, Is.EqualTo(0.5f).Within(0.0001f),
                        "설치기는 castTime + recoveryTime에 풀려난다 — 후속타를 기다리지 않는다");
            Assert.That(data.HitTime(2), Is.EqualTo(0.2f + 0.5f + 0.5f).Within(0.0001f),
                        "후속타 타임라인 자체는 그대로다");
        }

        [Test]
        public void Install_CasterFreed_BeforeHitsLand()
        {
            SkillData data = NewInstallSkill(hits: 3);
            SkillState state = NewState(data);

            state.Enter();
            state.Tick(0.5f);

            Assert.That(state.IsFinished, Is.True, "시전자는 후속타와 무관하게 castTime + recoveryTime에 끝난다");

            Installation inst = Object.FindFirstObjectByType<Installation>();
            Assert.That(inst, Is.Not.Null, "설치 지점에 Installation이 서 있어야 한다");
            Assert.That(inst.Fired, Is.EqualTo(0), "상태의 Tick은 설치기 시계를 돌리지 않는다");
        }

        [Test]
        public void Installation_FiresOnHitTimeline()
        {
            SkillData data = NewInstallSkill(hits: 3);
            var ctx = new SkillContext { data = data, targetInfo = TargetInfo.Ground(Vector3.zero) };

            Installation inst = Installation.Place(data, in ctx, Vector3.zero, data.radius);

            inst.Tick(0.19f);
            Assert.That(inst.Fired, Is.EqualTo(0), "castTime 전에는 안 나간다");

            inst.Tick(0.02f);
            Assert.That(inst.Fired, Is.EqualTo(1), "castTime에 1타");

            inst.Tick(0.5f);
            Assert.That(inst.Fired, Is.EqualTo(2), "castDelay 0.5 뒤 2타");

            inst.Tick(0.5f);
            Assert.That(inst.Fired, Is.EqualTo(3));
            Assert.That(inst.IsDone, Is.True, "다 쏘면 끝");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>castTime 0.2 · 후속 castDelay 0.5 · recovery 0.3. 데미지만 있는 최소 타격.</summary>
        private SkillData NewInstallSkill(int hits)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            data.skillName = "마력 지뢰";
            data.role = Role.Wizard;
            data.attackType = AttackType.Strike;
            data.targeting = TargetingType.GroundPoint;
            data.install = true;
            data.radius = 2f;
            data.castTime = 0.2f;
            data.recoveryTime = 0.3f;
            data.hitDataList = new List<HitData>();

            for (int i = 0; i < hits; i++)
                data.hitDataList.Add(new HitData
                {
                    damageData = new DamageData(10f),
                    castDelay = i == 0 ? 0f : 0.5f,
                });

            assets.Add(data);
            return data;
        }

        /// <summary><c>Ally.CastCard</c>는 코루틴을 띄우므로 상태만 직접 만든다.</summary>
        private SkillState NewState(SkillData data)
        {
            Ally caster = NewObject("Wizard").AddComponent<Ally>();

            var ctx = new SkillContext
            {
                data = data,
                caster = caster,
                targetInfo = TargetInfo.Ground(Vector3.zero),
                isBulletTime = true,
            };

            IState state = data.CreateState(in ctx);
            Assert.That(state, Is.InstanceOf<SkillState>());
            return (SkillState)state;
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }
    }
}
