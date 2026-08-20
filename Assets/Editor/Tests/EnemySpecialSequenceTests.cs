using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 특수 행동의 단계 전이만 검증한다. MonoBehaviour·물리는 끼지 않는다 —
    /// 타이밍 버그를 씬 없이 잡으려고 순수 객체로 떼어 놨다.
    ///
    /// 돌진 · 내려찍기 · 연타가 전부 이 한 벌을 쓴다.
    /// </summary>
    public class EnemySpecialSequenceTests
    {
        private const float Telegraph = 0.6f;
        private const float Active = 0.55f;
        private const float Recovery = 0.8f;

        private EnemySpecialSequence seq;

        [SetUp]
        public void SetUp()
        {
            seq = new EnemySpecialSequence(Telegraph, Active, Recovery);
        }

        [Test]
        public void Fresh_IsIdle()
        {
            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Idle));
            Assert.That(seq.IsRunning, Is.False);
        }

        [Test]
        public void Begin_EntersTelegraph()
        {
            seq.Begin();

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Telegraph));
            Assert.That(seq.IsRunning, Is.True);
        }

        [Test]
        public void BeforeTelegraphEnds_StaysInTelegraph()
        {
            seq.Begin();
            seq.Tick(Telegraph - 0.05f, Vector3.right);

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Telegraph));
        }

        [Test]
        public void AtTelegraphBoundary_LocksDirectionAndActivates()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right);

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Active));
            Assert.That(Vector3.Distance(seq.LockedDirection, Vector3.right), Is.LessThan(0.001f));
        }

        /// <summary>발동에 들어간 뒤엔 유도되면 안 된다. 피할 수 없는 공격이 된다.</summary>
        [Test]
        public void AfterLock_DirectionDoesNotFollowTarget()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right);
            seq.Tick(0.1f, Vector3.forward);

            Assert.That(Vector3.Distance(seq.LockedDirection, Vector3.right), Is.LessThan(0.001f));
        }

        [Test]
        public void AfterActiveDuration_EntersRecovery()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right);
            seq.Tick(Active, Vector3.right);

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Recovery));
        }

        [Test]
        public void HitOrWall_CutsActiveShort()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right);
            seq.Tick(0.1f, Vector3.right);
            seq.HitOrWall();

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Recovery));
        }

        /// <summary>예고 중 충돌은 취소시키지 않는다 — 아직 몸통이 나가지 않았다.</summary>
        [Test]
        public void HitOrWall_DuringTelegraph_Ignored()
        {
            seq.Begin();
            seq.Tick(0.1f, Vector3.right);
            seq.HitOrWall();

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Telegraph));
        }

        [Test]
        public void AfterRecoveryDuration_ReturnsToIdle()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right);
            seq.Tick(Active, Vector3.right);
            seq.Tick(Recovery, Vector3.right);

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Idle));
            Assert.That(seq.IsRunning, Is.False);
        }

        [Test]
        public void Cancel_ReturnsToIdleImmediately()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right);
            seq.Cancel();

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Idle));
        }

        [Test]
        public void TickWhileIdle_DoesNothing()
        {
            seq.Tick(10f, Vector3.right);

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Idle));
        }

        /// <summary>정규화되지 않은 방향을 그대로 물면 대쉬 속도가 거리에 비례해 터진다.</summary>
        [Test]
        public void LockedDirection_IsNormalized()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right * 7f);

            Assert.That(seq.LockedDirection.magnitude, Is.EqualTo(1f).Within(0.001f));
        }

        // ── 패턴마다 길이를 갈아끼우는 경로 ──────────────

        /// <summary>보스는 패턴마다 타이밍이 다르다. 시작할 때 실은 길이를 따라야 한다.</summary>
        [Test]
        public void BeginWithDurations_UsesNewTimings()
        {
            seq.Begin(0.2f, 0.1f, 0.3f);

            Assert.That(seq.TelegraphDuration, Is.EqualTo(0.2f).Within(0.0001f));

            seq.Tick(0.2f, Vector3.right);
            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Active), "새 예고 길이로 끝나야 한다");

            seq.Tick(0.1f, Vector3.right);
            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Recovery));
        }

        /// <summary>다단히트 간격 계산이 이 값을 나눈다. 0으로 새면 0 나누기가 된다.</summary>
        [Test]
        public void NegativeDurations_ClampToZero()
        {
            seq.Begin(-1f, -1f, -1f);

            Assert.That(seq.TelegraphDuration, Is.EqualTo(0f));
            Assert.That(seq.ActiveDuration, Is.EqualTo(0f));
            Assert.That(seq.RecoveryDuration, Is.EqualTo(0f));
        }

        /// <summary>길이가 0인 단계는 진행도를 나눌 수 없다. 1로 떨어져야 한다.</summary>
        [Test]
        public void PhaseProgress_ZeroLengthPhase_IsFull()
        {
            seq.Begin(0f, 0.5f, 0.5f);

            Assert.That(seq.PhaseProgress, Is.EqualTo(1f));
        }

        [Test]
        public void PhaseProgress_TracksTelegraph()
        {
            seq.Begin();
            seq.Tick(Telegraph * 0.5f, Vector3.right);

            Assert.That(seq.PhaseProgress, Is.EqualTo(0.5f).Within(0.01f));
        }

        // ── 차징 ────────────────────────────────────────
        // 보스 차징기(혼신베기)의 뼈대다. "때리면 늦어지고, 계속 때리면 안 터진다"가
        // 이 패턴의 전부라 그 규칙을 여기서 고정한다.

        private const float Charge = 2.4f;

        private void BeginCharging()
        {
            seq.Begin(Charge, Telegraph, Active, Recovery);
        }

        [Test]
        public void BeginWithCharge_EntersChargeFirst()
        {
            BeginCharging();

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Charge));
            Assert.That(seq.ChargeDuration, Is.EqualTo(Charge).Within(0.0001f));
        }

        /// <summary>차징을 안 쓰는 패턴은 이 클래스가 생기기 전과 같은 경로를 타야 한다.</summary>
        [Test]
        public void BeginWithZeroCharge_SkipsStraightToTelegraph()
        {
            seq.Begin(0f, Telegraph, Active, Recovery);

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Telegraph));
        }

        [Test]
        public void AfterChargeDuration_EntersTelegraph()
        {
            BeginCharging();
            seq.Tick(Charge, Vector3.right);

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Telegraph));
        }

        /// <summary>차징 중에는 "!"를 켜지 않는다. 예고는 발동 직전이라는 뜻으로 남겨 둔다.</summary>
        [Test]
        public void DuringCharge_TelegraphSignIsOff()
        {
            BeginCharging();
            seq.Tick(Charge * 0.5f, Vector3.right);

            Assert.That(seq.ShouldShowTelegraph, Is.False);

            seq.Tick(Charge * 0.5f, Vector3.right);
            Assert.That(seq.ShouldShowTelegraph, Is.True, "차징이 끝나면 예고가 켜져야 한다");
        }

        [Test]
        public void ChargeProgress_TracksCharge()
        {
            BeginCharging();
            seq.Tick(Charge * 0.5f, Vector3.right);

            Assert.That(seq.ChargeProgress, Is.EqualTo(0.5f).Within(0.01f));
        }

        /// <summary>예고로 넘어가면 게이지가 사라져야 한다 — 모으는 중이 아니다.</summary>
        [Test]
        public void ChargeProgress_IsZeroOutsideChargePhase()
        {
            BeginCharging();
            seq.Tick(Charge, Vector3.right);

            Assert.That(seq.ChargeProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Delay_PushesChargeBack()
        {
            BeginCharging();
            seq.Tick(1.2f, Vector3.right);
            seq.Delay(0.4f);

            Assert.That(seq.PhaseTime, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Charge));
        }

        /// <summary>되감기는 0까지다. 음수로 새면 차징이 정상보다 길어진다.</summary>
        [Test]
        public void Delay_ClampsAtZero()
        {
            BeginCharging();
            seq.Tick(0.3f, Vector3.right);
            seq.Delay(99f);

            Assert.That(seq.PhaseTime, Is.EqualTo(0f));
        }

        /// <summary>몰아치는 쪽이 이긴다 — 지연이 진행보다 빠르면 차징은 끝나지 않는다.</summary>
        [Test]
        public void RepeatedDelay_NeverFinishesCharge()
        {
            BeginCharging();

            for (int i = 0; i < 200; i++)
            {
                seq.Tick(0.1f, Vector3.right);
                seq.Delay(0.35f);
            }

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Charge));
        }

        /// <summary>
        /// 예고 · 발동은 밀 수 없다. 밀리면 "!"를 보고 맞춘 회피 · 패링 타이밍이 매번 달라진다.
        /// </summary>
        [Test]
        public void Delay_OutsideCharge_IsIgnored()
        {
            BeginCharging();
            seq.Tick(Charge, Vector3.right);
            seq.Tick(0.2f, Vector3.right);
            seq.Delay(0.2f);

            Assert.That(seq.PhaseTime, Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void ReleaseCharge_JumpsToTelegraph()
        {
            BeginCharging();
            seq.Tick(0.3f, Vector3.right);
            seq.ReleaseCharge();

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Telegraph));
            Assert.That(seq.PhaseTime, Is.EqualTo(0f));
        }

        /// <summary>끊기면 아무것도 터지지 않는다 — 모은 만큼 약하게 나가는 경로는 없다.</summary>
        [Test]
        public void Cancel_DuringCharge_LeavesNothingRunning()
        {
            BeginCharging();
            seq.Tick(Charge * 0.9f, Vector3.right);
            seq.Cancel();

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Idle));
            Assert.That(seq.IsRunning, Is.False);
            Assert.That(seq.ChargeProgress, Is.EqualTo(0f));
        }

        [Test]
        public void NegativeCharge_ClampsToZero()
        {
            seq.Begin(-1f, Telegraph, Active, Recovery);

            Assert.That(seq.ChargeDuration, Is.EqualTo(0f));
            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Telegraph));
        }

        /// <summary>차징을 붙여도 그 뒤 단계의 길이와 방향 고정은 그대로여야 한다.</summary>
        [Test]
        public void ChargeDoesNotDisturbLaterPhases()
        {
            BeginCharging();
            seq.Tick(Charge, Vector3.right);
            seq.Tick(Telegraph, Vector3.right);

            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Active));
            Assert.That(Vector3.Distance(seq.LockedDirection, Vector3.right), Is.LessThan(0.001f));

            seq.Tick(Active, Vector3.right);
            Assert.That(seq.Phase, Is.EqualTo(EnemySpecialPhase.Recovery));
        }
    }
}
