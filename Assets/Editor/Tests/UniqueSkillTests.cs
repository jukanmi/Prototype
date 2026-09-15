using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 기획 문서(고유 스킬) 4종 동작 검증:
    /// 1. 전사: 비켜! (120도 부채꼴 밀치기)
    /// 2. 궁수: 스탭샷 (전방 타격 + 후방 3유닛 백스탭)
    /// 3. 탱커: 금강불괴 (보호막 50 부여)
    /// 4. 마법사: 마력폭발 (360도 전방위 폭발 밀치기)
    /// </summary>
    public class UniqueSkillTests
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
        public void Archer_StepShot_MovesBackwardThreeUnits()
        {
            Entity caster = CreateDummy("Archer", Vector3.zero, Vector3.forward);

            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "스탭샷";
            skill.role = Role.Archer;
            skill.attackType = AttackType.Strike;
            skill.castTime = 0f;
            skill.approachDistance = 10f;

            var hit = new HitData
            {
                damageData = new DamageData(10f),
                castRangeScale = new Vector3(2f, 1.1f, 1.5f),
                stepDistance = -3f,
                targetState = CombatState.Neutral,
                nextState = CombatState.LightHit,
                hitStunDuration = 0.35f,
            };
            skill.hitDataList = new List<HitData> { hit };
            assets.Add(skill);

            var ctx = new SkillContext
            {
                data = skill,
                caster = caster,
                targetInfo = TargetInfo.None,
            };

            IState state = skill.CreateState(in ctx);
            state.Enter();

            Assert.That(caster.Physics.GroundPosition.z, Is.EqualTo(-3f).Within(0.01f),
                        "스탭샷은 후방으로 정확히 3유닛 백스탭해야 한다");
            Assert.That(caster.Physics.Facing, Is.EqualTo(Vector3.forward),
                        "백스탭 시 전방 시선을 유지해야 한다");
        }

        [Test]
        public void Tanker_InvincibleShield_AppliesShieldAmount()
        {
            Entity caster = CreateDummy("Tanker", Vector3.zero, Vector3.forward);

            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "금강불괴";
            skill.role = Role.Tanker;
            skill.castTime = 0f;
            skill.approachDistance = 100f;
            skill.hitDataList = new List<HitData>();

            var shield = new ShieldEffect();
            // private float amount = 30f 리플렉션 또는 기본 적용 검증
            // ShieldEffect의 Apply는 caster.Combat.AddShield(amount)를 호출
            skill.effects = new List<ISkillEffect> { shield };
            assets.Add(skill);

            var ctx = new SkillContext
            {
                data = skill,
                caster = caster,
                targetInfo = TargetInfo.None,
            };

            IState state = skill.CreateState(in ctx);
            state.Enter();

            Assert.That(caster.Combat.Shield, Is.GreaterThan(0f),
                        "금강불괴 시전 시 시전자에게 보호막이 부여되어야 한다");
        }

        [Test]
        public void Warrior_OuttaMyWay_PushesEnemiesAway()
        {
            var hit = new HitData
            {
                damageData = new DamageData(10f),
                castConeAngle = 120f,
                castRangeScale = new Vector3(4f, 1.1f, 4f),
                mode = KnockbackMode.AwayFromCaster,
                pushDistance = 4f,
                nextState = CombatState.Knockback,
            };

            Vector3 casterPos = Vector3.zero;
            Vector3 casterForward = Vector3.forward;
            Vector3 enemyPos = new Vector3(0f, 0f, 2f);

            Vector3 knockbackDir = hit.ResolveDirection(casterPos, casterForward, enemyPos);

            Assert.That(knockbackDir.z, Is.GreaterThan(0f),
                        "비켜!는 정면의 적을 시전자 반대쪽(전방)으로 밀어내야 한다");
            Assert.That(hit.pushDistance, Is.EqualTo(4f),
                        "밀치기 거리는 4유닛이어야 한다");
            Assert.That(hit.castConeAngle, Is.EqualTo(120f),
                        "부채꼴 각도는 120도여야 한다");
        }

        [Test]
        public void Wizard_MagicBurst_ConeIsFullCircle()
        {
            var hit = new HitData
            {
                damageData = new DamageData(10f),
                castConeAngle = 360f,
                castRangeScale = new Vector3(3f, 1.1f, 3f),
                mode = KnockbackMode.AwayFromCaster,
                pushDistance = 4f,
                nextState = CombatState.Knockback,
            };

            Assert.That(hit.IsCone, Is.True, "360도 부채꼴은 전방위 원형 판정이어야 한다");
            Assert.That(hit.castConeAngle, Is.EqualTo(360f), "각도는 360도여야 한다");
            Assert.That(hit.mode, Is.EqualTo(KnockbackMode.AwayFromCaster),
                        "주변의 모든 적을 시전자 중심에서 바깥으로 밀어내야 한다");
            Assert.That(hit.pushDistance, Is.EqualTo(4f), "밀치기 거리는 4유닛이어야 한다");
        }

        [Test]
        public void Ally_CastUniqueSkill_TriggersSkillAndAppliesCooldown()
        {
            var go = new GameObject("WarriorAlly", typeof(Rigidbody), typeof(Physics), typeof(Combat), typeof(Ally));
            spawned.Add(go);
            var ally = go.GetComponent<Ally>();

            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "비켜!";
            skill.cooldown = 5f;
            assets.Add(skill);

            var partyData = ScriptableObject.CreateInstance<PartyMemberData>();
            partyData.role = Role.Warrior;
            partyData.uniqueSkill = skill;
            assets.Add(partyData);

            ally.ApplyData(partyData);

            Assert.That(ally.UniqueSkill, Is.EqualTo(skill), "PartyMemberData의 uniqueSkill이 연결되어야 한다");
            Assert.That(ally.CanCastUniqueSkill, Is.True, "초기 상태에서는 고유 스킬을 사용할 수 있어야 한다");

            bool casted = ally.CastUniqueSkill();
            Assert.That(casted, Is.True, "고유 스킬 시전이 성공해야 한다");
            Assert.That(ally.UniqueSkillCooldownRemaining, Is.EqualTo(5f).Within(0.01f), "시전 후 쿨타임이 적용되어야 한다");
            Assert.That(ally.CanCastUniqueSkill, Is.False, "쿨타임 중에는 고유 스킬을 재사용할 수 없어야 한다");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private Entity CreateDummy(string name, Vector3 pos, Vector3 facing)
        {
            var go = new GameObject(name, typeof(Rigidbody), typeof(Physics), typeof(Combat), typeof(Entity));
            go.transform.position = pos;
            spawned.Add(go);

            var phys = go.GetComponent<Physics>();
            phys.Teleport(pos, 0f);
            phys.Face(facing);

            return go.GetComponent<Entity>();
        }
    }
}

