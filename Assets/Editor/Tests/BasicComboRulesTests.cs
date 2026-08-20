using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 평타 연타 판정. 전부 순수 함수라 씬도 <c>Awake</c>도 필요 없다 —
    /// EditMode에서는 <see cref="Entity.Awake"/>가 돌지 않아 상태머신이 없으므로,
    /// 판정을 상태 안에 뒀다면 여기서 아무것도 검증하지 못한다.
    /// </summary>
    public class BasicComboRulesTests
    {
        // 1타 기준: 선딜 0.10 · 판정끝 0.20 · 캔슬 0.20 · 전체 0.32
        private static readonly BasicAttackTiming Stage = new BasicAttackTiming(0.10f, 0.20f, 0.20f, 0.32f);

        // ── 타이밍 폴백 ──────────────────────────────────

        [Test]
        public void ResolveTiming_ZeroFallsBackToEntityDefaults()
        {
            var empty = new BasicAttackStage();

            BasicAttackTiming t = BasicComboRules.ResolveTiming(in empty, 0.12f, 0.24f, 0.45f);

            Assert.That(t.windup, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(t.activeEnd, Is.EqualTo(0.24f).Within(0.0001f));
            Assert.That(t.total, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(t.cancelStart, Is.EqualTo(0.24f).Within(0.0001f),
                        "캔슬 시점을 안 적으면 판정이 닫히는 순간이다");
        }

        [Test]
        public void ResolveTiming_AuthoredValuesWin()
        {
            var stage = new BasicAttackStage { windup = 0.18f, activeEnd = 0.34f, total = 0.7f, cancelStart = 0.5f };

            BasicAttackTiming t = BasicComboRules.ResolveTiming(in stage, 0.12f, 0.24f, 0.45f);

            Assert.That(t.windup, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(t.cancelStart, Is.EqualTo(0.5f).Within(0.0001f));
        }

        // ── 전이표 ───────────────────────────────────────

        [Test]
        public void NoBuffer_RunsToEndThenFinishes()
        {
            Assert.That(Step(0.25f, 0, 3, buffered: false), Is.EqualTo(BasicComboStep.Continue));
            Assert.That(Step(0.32f, 0, 3, buffered: false), Is.EqualTo(BasicComboStep.Finish));
        }

        [Test]
        public void Buffered_BeforeCancelWindow_DoesNotAdvance()
        {
            Assert.That(Step(0.15f, 0, 3, buffered: true), Is.EqualTo(BasicComboStep.Continue),
                        "히트박스가 아직 열려 있는 동안 넘어가면 그 타가 맞기도 전에 사라진다");
        }

        [Test]
        public void Buffered_AfterCancelWindow_Advances()
        {
            Assert.That(Step(0.20f, 0, 3, buffered: true), Is.EqualTo(BasicComboStep.Advance));
            Assert.That(Step(0.28f, 1, 3, buffered: true), Is.EqualTo(BasicComboStep.Advance));
        }

        [Test]
        public void Advance_BeatsFinish_OnTheSameFrame()
        {
            // 캔슬 시점과 전체 길이를 동시에 넘긴 프레임. Finish가 이기면 콤보가 한 타에서 멈춘다.
            Assert.That(Step(0.40f, 0, 3, buffered: true), Is.EqualTo(BasicComboStep.Advance));
        }

        [Test]
        public void LastStage_DoesNotCancel_ButRestartsAfterRecovery()
        {
            // 마무리는 캔슬 불가 — 긴 후딜을 지워 버리면 "강타는 무겁다"가 거짓말이 된다.
            Assert.That(Step(0.25f, 2, 3, buffered: true), Is.EqualTo(BasicComboStep.Continue));

            // 다 기다린 뒤에 선입력이 살아 있으면 1타부터 다시 돈다.
            Assert.That(Step(0.32f, 2, 3, buffered: true), Is.EqualTo(BasicComboStep.Restart));
            Assert.That(Step(0.32f, 2, 3, buffered: false), Is.EqualTo(BasicComboStep.Finish));
        }

        [Test]
        public void SingleStage_NeverAdvancesOrRestarts()
        {
            // 콤보를 저작하지 않은 몸(적 · 자율 동료)은 예전과 완전히 같은 경로를 탄다.
            Assert.That(Step(0.25f, 0, 1, buffered: true), Is.EqualTo(BasicComboStep.Continue));
            Assert.That(Step(0.32f, 0, 1, buffered: true), Is.EqualTo(BasicComboStep.Finish));
        }

        // ── 단계별 타격 ──────────────────────────────────

        [Test]
        public void DamageMultiplier_AppliesOnTopOfAttackPower()
        {
            HitData basic = Basic();
            var stage = new BasicAttackStage { damageMultiplier = 1.8f };

            HitData h = BasicComboRules.BuildStageHit(in basic, in stage);

            Assert.That(h.damageData.damage, Is.EqualTo(18f).Within(0.0001f));
        }

        [Test]
        public void ZeroMultiplier_ReadsAsOne()
        {
            HitData basic = Basic();
            var stage = new BasicAttackStage();

            HitData h = BasicComboRules.BuildStageHit(in basic, in stage);

            Assert.That(h.damageData.damage, Is.EqualTo(10f).Within(0.0001f),
                        "배율을 안 적은 단계는 기본 데미지 그대로다");
        }

        [Test]
        public void WithoutOverride_ReactionIsUntouched()
        {
            HitData basic = Basic();
            var stage = new BasicAttackStage { damageMultiplier = 1.1f };

            HitData h = BasicComboRules.BuildStageHit(in basic, in stage);

            Assert.That(h.nextState, Is.EqualTo(basic.nextState));
            Assert.That(h.mode, Is.EqualTo(basic.mode));
            Assert.That(h.knockbackForce, Is.EqualTo(basic.knockbackForce));
            Assert.That(h.launchForce, Is.Zero);
        }

        [Test]
        public void Override_ChangesReaction_ButKeepsHitIdentity()
        {
            HitData basic = Basic();
            basic.canOtg = true;

            var stage = new BasicAttackStage
            {
                damageMultiplier = 1.8f,
                overrideReaction = true,
                nextState = CombatState.AerialHit,
                mode = KnockbackMode.AwayFromCaster,
                knockbackForce = 4.5f,
                launchForce = 6f,
                hitStunDuration = 0.5f,
            };

            HitData h = BasicComboRules.BuildStageHit(in basic, in stage);

            Assert.That(h.nextState, Is.EqualTo(CombatState.AerialHit));
            Assert.That(h.launchForce, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(h.hitStunDuration, Is.EqualTo(0.5f).Within(0.0001f));

            // 타격의 정체성(OTG · 선행 조건)은 단계가 건드리지 않는다 —
            // 평타 3타가 서로 다른 OTG 판정을 갖는 건 기능이 아니라 버그다.
            Assert.That(h.canOtg, Is.True);
            Assert.That(h.targetState, Is.EqualTo(basic.targetState));
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private static BasicComboStep Step(float timer, int stage, int count, bool buffered)
            => BasicComboRules.Decide(timer, in Stage, stage, count, buffered);

        private static HitData Basic() => new HitData
        {
            damageData = new DamageData(10f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            knockbackForce = 3f,
            hitStunDuration = 0.3f,
        };
    }
}
