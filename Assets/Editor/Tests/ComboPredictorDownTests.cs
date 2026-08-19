using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 예측기가 <b>착지 → 다운</b>을 시뮬레이션하는지.
    ///
    /// 다운은 1.2s + 기상 0.4s = 1.6초 완전 무적인데 슬롯 간격은 0.05초다.
    /// 콤보 도중 대상이 눕는 순간 뒤따르던 슬롯이 전부 증발한다 —
    /// 그런데 예측 UI는 계속 <c>AerialHit [강화]</c>로 거짓말을 했다.
    /// 다운을 "콤보 설계 미스"로 남기기로 했으므로(결정), 미스라는 걸 놓기 전에 보여 줘야 한다.
    /// </summary>
    public class ComboPredictorDownTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++) Object.DestroyImmediate(spawned[i]);
            for (int i = 0; i < assets.Count; i++) Object.DestroyImmediate(assets[i]);

            spawned.Clear();
            assets.Clear();
        }

        [Test]
        public void ShortFollowUps_StayAirborne()
        {
            ComboPredictor predictor = NewPredictor();

            // 띄우기 12 / 중력 30 → 체공 0.8초. 짧은 스킬 두 장은 그 안에 들어간다.
            var slots = new List<ComboSlot>
            {
                Slot(Launcher(12f)),
                Slot(Follow(0.2f)),
                Slot(Follow(0.2f)),
            };

            List<CombatState> p = predictor.Simulate(slots);

            Assert.That(p[1], Is.EqualTo(CombatState.AerialHit), "체공 안에 들어온 후속타는 공중 유지");
            Assert.That(p[2], Is.EqualTo(CombatState.AerialHit));
            Assert.That(predictor.IsBlockedByDown(1), Is.False);
            Assert.That(predictor.IsBlockedByDown(2), Is.False);
        }

        [Test]
        public void LongFollowUp_LandsAndGoesDown()
        {
            ComboPredictor predictor = NewPredictor();

            // 체공 0.8초인데 두 번째 슬롯이 1.5초짜리다 — 그 안에 대상이 바닥에 닿는다.
            var slots = new List<ComboSlot>
            {
                Slot(Launcher(12f)),
                Slot(Follow(1.5f)),
                Slot(Follow(0.2f)),
            };

            List<CombatState> p = predictor.Simulate(slots);

            Assert.That(p[0], Is.EqualTo(CombatState.AerialHit), "선행 조건: 첫 장은 띄운다");
            Assert.That(p[1], Is.EqualTo(CombatState.Down), "착지했으면 다운이다");
            Assert.That(predictor.IsBlockedByDown(2), Is.True,
                        "다운 뒤에 놓인 슬롯은 무적에 통째로 흘린다 — UI가 미리 말해야 한다");
        }

        [Test]
        public void OtgSkill_StillConnectsAfterDown()
        {
            ComboPredictor predictor = NewPredictor();

            var slots = new List<ComboSlot>
            {
                Slot(Launcher(12f)),
                Slot(Follow(1.5f)),
                Slot(Otg()),
            };

            predictor.Simulate(slots);

            Assert.That(predictor.IsBlockedByDown(2), Is.False,
                        "바닥쓸기(canOtg)는 다운을 관통한다");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private static ComboSlot Slot(SkillData data) => new ComboSlot { card = new ComboCard(data) };

        private ComboPredictor NewPredictor()
        {
            var go = new GameObject("Predictor");
            spawned.Add(go);
            return go.AddComponent<ComboPredictor>();
        }

        /// <summary>띄우는 시동기. 전체 소요는 짧게 둔다.</summary>
        private SkillData Launcher(float launchForce)
        {
            SkillData data = NewSkill("띄우기", castTime: 0.1f, recovery: 0.1f);
            data.hitDataList = new List<HitData>
            {
                new HitData
                {
                    damageData = new DamageData(5f),
                    nextState = CombatState.AerialHit,
                    mode = KnockbackMode.Up,
                    launchForce = launchForce,
                    hitStunDuration = 0.4f,
                },
            };
            return data;
        }

        /// <summary>후속타 한 장. <paramref name="duration"/>이 곧 슬롯이 잡아먹는 시간이다.</summary>
        private SkillData Follow(float duration)
        {
            SkillData data = NewSkill("후속", castTime: duration * 0.5f, recovery: duration * 0.5f);
            data.requireState = CombatState.AerialHit;
            data.hitDataList = new List<HitData>
            {
                new HitData
                {
                    damageData = new DamageData(5f),
                    nextState = CombatState.AerialHit,
                    mode = KnockbackMode.Up,
                    launchForce = 2f,
                    hitStunDuration = 0.3f,
                },
            };
            return data;
        }

        /// <summary>바닥쓸기. 다운을 관통한다.</summary>
        private SkillData Otg()
        {
            SkillData data = NewSkill("바닥쓸기", castTime: 0.1f, recovery: 0.1f);
            data.hitDataList = new List<HitData>
            {
                new HitData
                {
                    damageData = new DamageData(5f),
                    nextState = CombatState.LightHit,
                    mode = KnockbackMode.AwayFromCaster,
                    knockbackForce = 4f,
                    canOtg = true,
                    hitStunDuration = 0.3f,
                },
            };
            return data;
        }

        private SkillData NewSkill(string name, float castTime, float recovery)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            assets.Add(data);

            data.skillName = name;
            data.castTime = castTime;
            data.hitInterval = 0f;
            data.recoveryTime = recovery;
            data.attackType = AttackType.Strike;
            return data;
        }
    }
}
