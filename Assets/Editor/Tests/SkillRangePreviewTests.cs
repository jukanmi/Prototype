using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// <see cref="SkillState.TryGetRangePreview"/> — 선딜부터 후딜까지 스킬이 때릴 자리를
    /// <see cref="AttackRangePreview"/>로 얼마나 정확히 옮기는지.
    ///
    /// 근접 스킬은 targeting: None이라 조준 구간이 없다(<see cref="ComboBoardUI"/>의
    /// BeginAiming이 곧바로 거절한다) — 그래서 이 미리보기가 실행 중에 계속 떠 있지 않으면
    /// 근접 스킬은 범위를 볼 방법이 아예 없다.
    /// </summary>
    public class SkillRangePreviewTests
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
        public void RangedSkill_NeverShowsPreview()
        {
            SkillData data = NewSkill(Role.Archer, hit: Melee(0.3f));
            data.projectile = NewObject("Bolt").AddComponent<Projectile>();

            SkillState state = Enter(data);

            Assert.That(state.TryGetRangePreview(out _), Is.False,
                        "착탄 지점이 시전 위치와 다르므로 틀린 자리에 그리느니 안 그린다");
        }

        [Test]
        public void MeleeBox_MatchesHurtboxTimesScale()
        {
            SkillData data = NewSkill(Role.Warrior, hit: Melee());
            data.castRangeScale = new Vector3(2f, 0.6f, 1.3f);

            SkillState state = Enter(data);

            Assert.That(state.TryGetRangePreview(out AttackRangePreview r), Is.True);
            Assert.That(r.IsCone, Is.False);
            Assert.That(r.IsCircle, Is.False);
            // HurtboxSize는 콜라이더 없는 테스트 오브젝트라 (1,1,1)로 떨어진다.
            Assert.That(r.halfWidth, Is.EqualTo(1f).Within(0.0001f), "가로 배수 2의 절반");
            Assert.That(r.halfLength, Is.EqualTo(0.65f).Within(0.0001f), "세로 배수 1.3의 절반");
        }

        [Test]
        public void MeleeBox_CenterSitsAtFrontFace()
        {
            SkillData data = NewSkill(Role.Warrior, hit: Melee());
            data.castRangeScale = new Vector3(2f, 0.6f, 1.3f);

            SkillState state = Enter(data);
            state.TryGetRangePreview(out AttackRangePreview r);

            // 기본 Facing은 +X. Attack.Resize와 같은 규약 — 몸 앞면(size.z*0.5)에서 시작한다.
            Assert.That(r.center.x, Is.EqualTo(1.3f * 0.5f).Within(0.0001f));
            Assert.That(r.center.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void MeleeBox_ZeroScale_IsUnset_NoPreview()
        {
            SkillData data = NewSkill(Role.Warrior, hit: Melee());
            // castRangeScale을 안 채운 스킬 — 프리팹 기본 히트박스라 미리보기 크기를 모른다.

            SkillState state = Enter(data);

            Assert.That(state.TryGetRangePreview(out _), Is.False);
        }

        [Test]
        public void Cone_UsesHurtboxXAsRadius()
        {
            SkillData data = NewSkill(Role.Warrior, hit: Melee());
            data.castRangeScale = new Vector3(4f, 1f, 1f);
            data.castConeAngle = 60f;

            SkillState state = Enter(data);

            Assert.That(state.TryGetRangePreview(out AttackRangePreview r), Is.True);
            Assert.That(r.IsCone, Is.True);
            Assert.That(r.radius, Is.EqualTo(4f).Within(0.0001f), "HurtboxSize.x(1) * scale.x(4)");
            Assert.That(r.coneAngle, Is.EqualTo(60f).Within(0.0001f));
        }

        [Test]
        public void AreaSkill_MatchesRadiusAndOrigin()
        {
            SkillData data = NewSkill(Role.Wizard, hit: Melee());
            data.radius = 3.5f;
            data.targeting = TargetingType.GroundPoint;

            SkillState state = Enter(data, targetInfo: TargetInfo.Ground(new Vector3(5f, 0f, 2f)));

            Assert.That(state.TryGetRangePreview(out AttackRangePreview r), Is.True);
            Assert.That(r.IsCircle, Is.True);
            Assert.That(r.IsCone, Is.False);
            Assert.That(r.radius, Is.EqualTo(3.5f).Within(0.0001f));
            Assert.That(r.center.x, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(r.center.z, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void Finished_NoLongerShowsPreview()
        {
            SkillData data = NewSkill(Role.Warrior, hit: Melee(hitStun: 0.3f));
            data.castRangeScale = new Vector3(2f, 0.6f, 1.3f);
            data.castTime = 0.1f;
            data.recoveryTime = 0.1f;

            SkillState state = Enter(data);
            Assert.That(state.TryGetRangePreview(out _), Is.True, "선딜 중엔 보여야 한다");

            state.Tick(1f);   // 선딜 + 후딜을 넉넉히 넘긴다
            Assert.That(state.IsFinished, Is.True, "선행 조건: 스킬이 끝나 있어야 한다");

            Assert.That(state.TryGetRangePreview(out _), Is.False, "끝난 스킬은 더 이상 보이면 안 된다");
        }

        [Test]
        public void Progress_TracksTimerOverEndTime()
        {
            SkillData data = NewSkill(Role.Warrior, hit: Melee());
            data.castRangeScale = new Vector3(2f, 0.6f, 1.3f);
            data.castTime = 1f;
            data.recoveryTime = 1f;

            SkillState state = Enter(data);

            state.Tick(0.5f);
            state.TryGetRangePreview(out AttackRangePreview r);

            Assert.That(r.progress, Is.EqualTo(0.5f / data.TotalDuration).Within(0.01f));
        }

        [Test]
        public void EmptyHitList_NoPreview()
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            data.skillName = "빈 스킬";
            data.role = Role.Warrior;
            data.hitDataList = new List<HitData>();
            assets.Add(data);

            SkillState state = Enter(data);

            Assert.That(state.TryGetRangePreview(out _), Is.False);
        }

        // ── 차징: 모으는 동안 실시간으로 커지는지 ─────────

        [Test]
        public void Charging_GrowsRadiusLiveWithChargeRatio()
        {
            SkillData data = NewSkill(Role.Wizard, hit: Melee());
            data.attackType = AttackType.Charge;
            data.radius = 2f;
            data.maxChargeTime = 1f;
            data.maxChargeRadiusMul = 2f;
            data.targeting = TargetingType.GroundPoint;

            SkillContext ctx = BuildContext(data, TargetInfo.Ground(Vector3.zero));
            IState raw = data.CreateState(in ctx);
            Assert.That(raw, Is.InstanceOf<ChargeSkillState>(), "선행 조건: 차징 상태여야 한다");
            var charge = (ChargeSkillState)raw;
            charge.Enter();

            charge.Tick(0.5f);   // 50% 모음
            Assert.That(charge.TryGetRangePreview(out AttackRangePreview r), Is.True,
                        "모으는 동안에도 범위가 보여야 한다 — 발동 전이라도 예고는 필요하다");
            Assert.That(r.radius, Is.EqualTo(2f * Mathf.Lerp(1f, 2f, 0.5f)).Within(0.001f),
                        "Release()와 같은 lerp를 실시간으로 반영한다");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private static HitData Melee(float hitStun = 0.3f) => new HitData
        {
            damageData = new DamageData(10f),
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            hitStunDuration = hitStun,
        };

        private SkillData NewSkill(Role role, HitData hit)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            data.skillName = "테스트";
            data.role = role;
            data.castTime = 0.2f;
            data.recoveryTime = 0.2f;
            data.hitDataList = new List<HitData> { hit };

            assets.Add(data);
            return data;
        }

        private SkillContext BuildContext(SkillData data, TargetInfo targetInfo)
        {
            Ally caster = NewObject("Caster").AddComponent<Ally>();

            return new SkillContext
            {
                data = data,
                caster = caster,
                targetInfo = targetInfo,
                isBulletTime = false,
                comboIndex = -1,
            };
        }

        private SkillState Enter(SkillData data, TargetInfo targetInfo = default)
        {
            SkillContext ctx = BuildContext(data, targetInfo.type == TargetingType.None ? TargetInfo.None : targetInfo);
            IState state = data.CreateState(in ctx);
            state.Enter();
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
