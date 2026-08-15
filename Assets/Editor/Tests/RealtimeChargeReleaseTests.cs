using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 실시간(U키 단발) 차징의 자동 발동.
    ///
    /// 불릿타임에서는 <c>ComboExecutor</c>가 큐를 다 비운 뒤 <see cref="IChargeState.Release"/>를
    /// 불러 준다. 실시간에는 그 주체가 없어 게이지만 가득 찬 채로 서 있다가
    /// <c>Ally.realtimeSkillTimeout</c>에 잘렸다 — 그래서 머리 위 게이지가 차면 스스로 터진다.
    /// </summary>
    public class RealtimeChargeReleaseTests
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
        public void Realtime_ReleasesItself_WhenGaugeFills()
        {
            ChargeSkillState charge = NewRealtimeCharge(maxChargeTime: 1f);

            charge.Tick(0.6f);
            Assert.That(charge.IsCharging, Is.True, "아직 게이지가 안 찼다");
            Assert.That(charge.ChargeRatio, Is.EqualTo(0.6f).Within(0.001f));

            charge.Tick(0.6f);

            Assert.That(charge.IsCharging, Is.False, "게이지가 가득 차면 스스로 터져야 한다");
        }

        [Test]
        public void Realtime_ReleasesAtFullPower()
        {
            ChargeSkillState charge = NewRealtimeCharge(maxChargeTime: 1f);

            charge.Tick(5f);   // 한 번에 넘겨도 상한에서 잠긴다

            Assert.That(charge.IsCharging, Is.False);
            Assert.That(charge.ChargeRatio, Is.EqualTo(1f).Within(0.001f),
                        "실시간 차징은 항상 최대 위력으로 터진다");
        }

        /// <summary>
        /// 점프키 중도 해제(<c>PlayerControl.TryReleaseCharge</c>)가 부르는 경로.
        /// 입력 자체는 씬 없이 못 돌리므로 상태 쪽 계약만 본다 — 모은 만큼에서 잠긴다.
        /// </summary>
        [Test]
        public void ReleasedMidCharge_LocksThePartialRatio()
        {
            ChargeSkillState charge = NewRealtimeCharge(maxChargeTime: 1f);

            charge.Tick(0.4f);
            charge.Release();

            Assert.That(charge.IsCharging, Is.False);
            Assert.That(charge.ChargeRatio, Is.EqualTo(0.4f).Within(0.001f),
                        "중도 해제는 모은 양 그대로 잠긴다 — 일찍 끊을수록 약하다");

            // 해제 뒤에는 더 모이지 않는다. 시전 타임라인만 돈다.
            charge.Tick(1f);
            Assert.That(charge.ChargeRatio, Is.EqualTo(0.4f).Within(0.001f));
        }

        [Test]
        public void ReleaseTwice_IsIgnored()
        {
            ChargeSkillState charge = NewRealtimeCharge(maxChargeTime: 1f);

            charge.Tick(0.3f);
            charge.Release();
            charge.Release();   // 자동 발동과 점프키가 같은 프레임에 겹쳐도 안전해야 한다

            Assert.That(charge.ChargeRatio, Is.EqualTo(0.3f).Within(0.001f));
        }

        [Test]
        public void BulletTime_KeepsCharging_PastFullGauge()
        {
            ChargeSkillState charge = NewCharge(maxChargeTime: 1f, bulletTime: true);

            charge.Tick(5f);

            Assert.That(charge.IsCharging, Is.True,
                        "불릿타임은 큐가 끝날 때까지 붙잡아 둔다 — Executor가 Release를 부른다");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private ChargeSkillState NewRealtimeCharge(float maxChargeTime)
            => NewCharge(maxChargeTime, bulletTime: false);

        /// <summary>
        /// <c>Ally.CastCard</c>를 거치지 않는다 — 그쪽은 코루틴을 띄우므로 에디트모드에서 못 돈다.
        /// 검증 대상은 상태 자체의 해제 판단이라 상태만 직접 만든다.
        /// </summary>
        private ChargeSkillState NewCharge(float maxChargeTime, bool bulletTime)
        {
            SkillData data = NewChargeSkill(maxChargeTime);
            Ally caster = NewObject("Wizard").AddComponent<Ally>();

            var ctx = new SkillContext
            {
                data = data,
                caster = caster,
                targetInfo = TargetInfo.None,
                isBulletTime = bulletTime,
                comboIndex = bulletTime ? 0 : -1,
            };

            IState state = data.CreateState(in ctx);
            Assert.That(state, Is.InstanceOf<ChargeSkillState>(), "선행 조건: 차징 상태여야 한다");

            state.Enter();
            return (ChargeSkillState)state;
        }

        /// <summary>타격이 없는 최소 차징 스킬. 검증에 필요한 건 모으기 판단뿐이다.</summary>
        private SkillData NewChargeSkill(float maxChargeTime)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            data.skillName = "충전탄";
            data.role = Role.Wizard;
            data.attackType = AttackType.Charge;
            data.maxChargeTime = maxChargeTime;
            data.castTime = 0.2f;
            data.hitDataList = new List<HitData>();

            assets.Add(data);
            return data;
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }
    }
}
