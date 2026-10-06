using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// HitData.stepDistance에 따른 시전자 이동(전방 돌진/파고들기 및 후방 백스탭) 검증.
    /// </summary>
    public class SkillStepTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            for (int i = 0; i < assets.Count; i++)
                if (assets[i] != null) Object.DestroyImmediate(assets[i]);

            spawned.Clear();
            assets.Clear();
        }

        [Test]
        public void ForwardStep_MovesCasterForward()
        {
            Entity caster = CreateDummy(Vector3.zero, Vector3.forward);
            SkillData skill = CreateSkill(stepDistance: 3f, fixedOrigin: false);

            ExecuteSkillFirstHit(skill, caster);

            Assert.That(caster.Physics.GroundPosition.z, Is.EqualTo(3f).Within(0.01f),
                        "양수 stepDistance는 전방으로 이동해야 한다");
            Assert.That(caster.Physics.Facing, Is.EqualTo(Vector3.forward),
                        "전방 이동 시 정면을 바라봐야 한다");
        }

        [Test]
        public void BackwardStep_MovesCasterBackward_KeepsFacing()
        {
            Entity caster = CreateDummy(Vector3.zero, Vector3.forward);
            SkillData skill = CreateSkill(stepDistance: -2.5f, fixedOrigin: false);

            ExecuteSkillFirstHit(skill, caster);

            Assert.That(caster.Physics.GroundPosition.z, Is.EqualTo(-2.5f).Within(0.01f),
                        "음수 stepDistance는 후방(백스탭)으로 이동해야 한다");
            Assert.That(caster.Physics.Facing, Is.EqualTo(Vector3.forward),
                        "백스탭 시 시선(Facing)은 뒤로 돌지 않고 전방을 유지해야 한다");
        }

        [Test]
        public void LegacyFixedOrigin_MovesCasterByHitboxDepth()
        {
            Entity caster = CreateDummy(Vector3.zero, Vector3.forward);
            // stepDistance가 0이지만 fixedOrigin이 켜져 있고 z배수가 2이면 2유닛 이동
            SkillData skill = CreateSkill(stepDistance: 0f, fixedOrigin: true, castRangeScaleZ: 2f);

            ExecuteSkillFirstHit(skill, caster);

            Assert.That(caster.Physics.GroundPosition.z, Is.EqualTo(2f).Within(0.01f),
                        "stepDistance가 0이어도 레거시 fixedOrigin 스킬은 판정 깊이만큼 파고들어야 한다");
        }

        [Test]
        public void ZeroStep_StaysInPlace()
        {
            Entity caster = CreateDummy(Vector3.zero, Vector3.forward);
            SkillData skill = CreateSkill(stepDistance: 0f, fixedOrigin: false);

            ExecuteSkillFirstHit(skill, caster);

            Assert.That(caster.Physics.GroundPosition.z, Is.EqualTo(0f).Within(0.01f),
                        "stepDistance가 0이면 이동하지 않아야 한다");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private void ExecuteSkillFirstHit(SkillData skill, Entity caster)
        {
            var ctx = new SkillContext
            {
                data = skill,
                caster = caster,
                targetInfo = TargetInfo.None,
            };

            IState state = skill.CreateState(in ctx);
            state.Enter(); // 선딜 0이므로 Enter()에서 즉시 첫 타 FireNextHit() 실행됨
        }

        private SkillData CreateSkill(float stepDistance, bool fixedOrigin, float castRangeScaleZ = 1f)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            data.skillName = "테스트스킬";
            data.role = Role.Warrior;
            data.castTime = 0f; // 즉시 발동
            data.approachDistance = 100f; // 시전 전 자동 이동 방지

            var hit = new HitData
            {
                damageData = new DamageData(10f),
                castRangeScale = new Vector3(1f, 1f, castRangeScaleZ),
                fixedOrigin = fixedOrigin,
                stepDistance = stepDistance,
            };
            data.hitDataList = new List<HitData> { hit };

            assets.Add(data);
            return data;
        }

        private Entity CreateDummy(Vector3 pos, Vector3 facing)
        {
            var go = new GameObject("Caster", typeof(Rigidbody), typeof(Physics), typeof(Combat), typeof(Entity));
            go.transform.position = pos;
            spawned.Add(go);

            var phys = go.GetComponent<Physics>();
            phys.Teleport(pos, 0f);
            phys.Face(facing);

            return go.GetComponent<Entity>();
        }
    }
}

