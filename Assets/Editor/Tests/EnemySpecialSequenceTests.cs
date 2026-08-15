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
    }
}
