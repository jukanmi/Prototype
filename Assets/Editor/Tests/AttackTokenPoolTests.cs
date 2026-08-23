using NUnit.Framework;
using Prototype;

namespace Prototype.Tests
{
    /// <summary>
    /// 동시 공격 제한. 이 규칙이 없으면 여섯이 둘러싼 순간 <b>피할 수 있는 틈이 아예 없어진다</b> —
    /// 난이도가 아니라 설계 실패라, 여기서 순수 함수로 못 박아 둔다.
    ///
    /// 가장 중요한 항목은 <b>만료</b>다. 반납을 놓치는 경로(공격 중 사망 · 씬 언로드 ·
    /// 상태머신이 공격을 거절)는 반드시 생기고, 한 번 새면 그 자리는 영영 안 돌아온다.
    /// 증상은 "어느 순간부터 적이 아무도 안 때린다"라 원인을 짚기가 대단히 어렵다.
    /// </summary>
    public class AttackTokenPoolTests
    {
        private AttackTokenPool pool;

        [SetUp]
        public void SetUp()
        {
            pool = new AttackTokenPool();
            pool.SetCapacity(2);
        }

        // ── 정원 ────────────────────────────────────────

        [Test]
        public void UpToCapacity_Succeeds()
        {
            Assert.That(pool.TryAcquire(1, 0f, 1f), Is.True);
            Assert.That(pool.TryAcquire(2, 0f, 1f), Is.True);
        }

        [Test]
        public void BeyondCapacity_IsRefused()
        {
            pool.TryAcquire(1, 0f, 1f);
            pool.TryAcquire(2, 0f, 1f);

            Assert.That(pool.TryAcquire(3, 0f, 1f), Is.False, "세 번째가 동시에 휘두른다");
            Assert.That(pool.ActiveCount(0f), Is.EqualTo(2));
        }

        [Test]
        public void ReleasedSlot_GoesToTheNextInLine()
        {
            pool.TryAcquire(1, 0f, 1f);
            pool.TryAcquire(2, 0f, 1f);
            pool.Release(1);

            Assert.That(pool.TryAcquire(3, 0f, 1f), Is.True);
        }

        /// <summary>
        /// 이미 쥔 쪽은 언제나 성공한다. 연타 도중에 정원이 줄었다고 휘두르던 팔이 멈추면
        /// 그게 더 이상하다 — 갱신은 새 자리를 요구하지 않는다.
        /// </summary>
        [Test]
        public void Holder_CanAlwaysRenew_EvenWhenFull()
        {
            pool.TryAcquire(1, 0f, 1f);
            pool.TryAcquire(2, 0f, 1f);

            Assert.That(pool.TryAcquire(1, 0.5f, 1f), Is.True);
            Assert.That(pool.ActiveCount(0.5f), Is.EqualTo(2), "갱신이 자리를 하나 더 먹었다");
        }

        [Test]
        public void ReleasingSomethingWeNeverHeld_IsHarmless()
        {
            Assert.DoesNotThrow(() => pool.Release(42));
            Assert.That(pool.ActiveCount(0f), Is.Zero);
        }

        // ── 만료 ────────────────────────────────────────

        /// <summary>공격 도중에 죽은 적의 자리. 반납이 안 와도 임대가 끝나면 열린다.</summary>
        [Test]
        public void ExpiredLease_FreesTheSlot()
        {
            pool.SetCapacity(1);
            pool.TryAcquire(1, 0f, 2f);

            Assert.That(pool.TryAcquire(2, 1f, 2f), Is.False, "아직 임대 중인데 자리가 열렸다");
            Assert.That(pool.TryAcquire(2, 2.5f, 2f), Is.True, "임대가 끝났는데 자리가 안 열린다");
        }

        [Test]
        public void Holds_FollowsTheLeaseClock()
        {
            pool.TryAcquire(1, 0f, 2f);

            Assert.That(pool.Holds(1, 1f), Is.True);
            Assert.That(pool.Holds(1, 3f), Is.False);
            Assert.That(pool.Holds(99, 1f), Is.False);
        }

        [Test]
        public void ActiveCount_DropsAsLeasesExpire()
        {
            pool.SetCapacity(3);
            pool.TryAcquire(1, 0f, 1f);
            pool.TryAcquire(2, 0f, 3f);

            Assert.That(pool.ActiveCount(0f), Is.EqualTo(2));
            Assert.That(pool.ActiveCount(2f), Is.EqualTo(1));
            Assert.That(pool.ActiveCount(4f), Is.Zero);
        }

        // ── 정원 변경 ───────────────────────────────────

        /// <summary>0으로 두면 아무도 공격하지 못하는 방이 조용히 만들어진다.</summary>
        [Test]
        public void CapacityNeverDropsBelowOne()
        {
            pool.SetCapacity(0);
            Assert.That(pool.Capacity, Is.EqualTo(1));

            pool.SetCapacity(-5);
            Assert.That(pool.Capacity, Is.EqualTo(1));
        }

        [Test]
        public void ResetAll_ClearsLeasesAndCapacity()
        {
            pool.SetCapacity(5);
            pool.TryAcquire(1, 0f, 10f);

            pool.ResetAll();

            Assert.That(pool.ActiveCount(0f), Is.Zero);
            Assert.That(pool.Capacity, Is.EqualTo(AttackTokenPool.DefaultCapacity));
        }

        /// <summary>웨이브가 바뀔 때 부른다. 지난 웨이브의 임대가 새 정원을 차지하면 안 된다.</summary>
        [Test]
        public void Clear_EmptiesEveryLease()
        {
            pool.TryAcquire(1, 0f, 10f);
            pool.TryAcquire(2, 0f, 10f);

            pool.Clear();

            Assert.That(pool.ActiveCount(0f), Is.Zero);
            Assert.That(pool.TryAcquire(3, 0f, 1f), Is.True);
        }

        // ── 임대 길이 ───────────────────────────────────

        /// <summary>돌진은 예고+발동+후딜로 평타보다 훨씬 길다. 같은 임대를 쓰면 중간에 잘린다.</summary>
        [Test]
        public void SpecialLease_OutlastsBasicLease()
        {
            Assert.That(AttackTokenPool.SpecialLease, Is.GreaterThan(AttackTokenPool.BasicLease));
        }
    }
}
