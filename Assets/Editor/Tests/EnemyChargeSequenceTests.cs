using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 돌진의 단계 전이만 검증한다. MonoBehaviour·물리는 끼지 않는다 —
    /// 타이밍 버그를 씬 없이 잡으려고 순수 객체로 떼어 놨다.
    /// </summary>
    public class EnemyChargeSequenceTests
    {
        private const float Telegraph = 0.6f;
        private const float Charge = 0.55f;
        private const float Recovery = 0.8f;

        private EnemyChargeSequence seq;

        [SetUp]
        public void SetUp()
        {
            seq = new EnemyChargeSequence(Telegraph, Charge, Recovery);
        }

        [Test]
        public void Fresh_IsIdle()
        {
            Assert.That(seq.Phase, Is.EqualTo(EnemyChargePhase.Idle));
            Assert.That(seq.IsRunning, Is.False);
        }

        [Test]
        public void Begin_EntersTelegraph()
        {
            seq.Begin();

            Assert.That(seq.Phase, Is.EqualTo(EnemyChargePhase.Telegraph));
            Assert.That(seq.IsRunning, Is.True);
        }

        [Test]
        public void BeforeTelegraphEnds_StaysInTelegraph()
        {
            seq.Begin();
            seq.Tick(Telegraph - 0.05f, Vector3.right);

            Assert.That(seq.Phase, Is.EqualTo(EnemyChargePhase.Telegraph));
        }

        [Test]
        public void AtTelegraphBoundary_LocksDirectionAndCharges()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right);

            Assert.That(seq.Phase, Is.EqualTo(EnemyChargePhase.Charging));
            Assert.That(Vector3.Distance(seq.LockedDirection, Vector3.right), Is.LessThan(0.001f));
        }

        /// <summary>돌진에 들어간 뒤엔 유도되면 안 된다. 피할 수 없는 공격이 된다.</summary>
        [Test]
        public void AfterLock_DirectionDoesNotFollowTarget()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right);
            seq.Tick(0.1f, Vector3.forward);

            Assert.That(Vector3.Distance(seq.LockedDirection, Vector3.right), Is.LessThan(0.001f));
        }

        [Test]
        public void AfterChargeDuration_EntersRecovery()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right);
            seq.Tick(Charge, Vector3.right);

            Assert.That(seq.Phase, Is.EqualTo(EnemyChargePhase.Recovery));
        }

        [Test]
        public void HitOrWall_CutsChargeShort()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right);
            seq.Tick(0.1f, Vector3.right);
            seq.HitOrWall();

            Assert.That(seq.Phase, Is.EqualTo(EnemyChargePhase.Recovery));
        }

        /// <summary>예고 중 충돌은 돌진을 취소시키지 않는다 — 아직 몸통이 나가지 않았다.</summary>
        [Test]
        public void HitOrWall_DuringTelegraph_Ignored()
        {
            seq.Begin();
            seq.Tick(0.1f, Vector3.right);
            seq.HitOrWall();

            Assert.That(seq.Phase, Is.EqualTo(EnemyChargePhase.Telegraph));
        }

        [Test]
        public void AfterRecoveryDuration_ReturnsToIdle()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right);
            seq.Tick(Charge, Vector3.right);
            seq.Tick(Recovery, Vector3.right);

            Assert.That(seq.Phase, Is.EqualTo(EnemyChargePhase.Idle));
            Assert.That(seq.IsRunning, Is.False);
        }

        [Test]
        public void Cancel_ReturnsToIdleImmediately()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right);
            seq.Cancel();

            Assert.That(seq.Phase, Is.EqualTo(EnemyChargePhase.Idle));
        }

        [Test]
        public void TickWhileIdle_DoesNothing()
        {
            seq.Tick(10f, Vector3.right);

            Assert.That(seq.Phase, Is.EqualTo(EnemyChargePhase.Idle));
        }

        /// <summary>정규화되지 않은 방향을 그대로 물면 대쉬 속도가 거리에 비례해 터진다.</summary>
        [Test]
        public void LockedDirection_IsNormalized()
        {
            seq.Begin();
            seq.Tick(Telegraph, Vector3.right * 7f);

            Assert.That(seq.LockedDirection.magnitude, Is.EqualTo(1f).Within(0.001f));
        }
    }
}
