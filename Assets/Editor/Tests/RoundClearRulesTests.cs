using NUnit.Framework;
using Prototype;

namespace Prototype.Tests
{
    /// <summary>
    /// 라운드 클리어 판정. <b>이 구조에서 가장 자주 터지는 부분</b>이라 순수 함수로 떼어 두고
    /// 여기서 전부 덮는다.
    ///
    /// 증상이 특히 고약하다 — 조기 종료는 "이겼는데 왜 적이 더 나오지"로, 지연 종료는
    /// "다 죽였는데 문이 안 열리네"로 나타나고, 둘 다 원인이 <b>세는 방식</b>이라
    /// 화면을 아무리 봐도 안 보인다.
    /// </summary>
    public class RoundClearRulesTests
    {
        private static RoundCensus Census(int pending = 0, int entering = 0, int fighting = 0, int dying = 0)
            => new RoundCensus { pending = pending, entering = entering, fighting = fighting, dying = dying };

        // ── 세는 방식 ───────────────────────────────────

        [Test]
        public void Threats_SumPendingEnteringAndFighting()
        {
            Assert.That(RoundClearRules.Threats(Census(pending: 2, entering: 1, fighting: 3)), Is.EqualTo(6));
        }

        /// <summary>
        /// 사망 연출 중인 개체는 <b>카운트에서 빼되 오브젝트는 남긴다</b>.
        /// 세어 버리면 시체가 사라질 때까지 문이 안 열려 몇 초씩 멈춘 것처럼 보인다.
        /// </summary>
        [Test]
        public void DyingBodies_AreNotThreats()
        {
            Assert.That(RoundClearRules.Threats(Census(dying: 5)), Is.Zero);
            Assert.That(RoundClearRules.IsCleared(Census(dying: 5), anySpawned: true), Is.True);
        }

        // ── 조기 종료 ───────────────────────────────────

        /// <summary>
        /// <b>이 테스트가 이 파일의 존재 이유다.</b> 마지막 몹이 죽는 순간 스폰 대기 중인 몹이
        /// 남아 있으면, 살아 있는 적만 세는 구현은 여기서 라운드를 끝내 버린다.
        /// 그러면 이미 열린 문 앞에서 예약분이 튀어나온다.
        /// </summary>
        [Test]
        public void PendingSpawns_KeepTheRoundOpen()
        {
            Assert.That(RoundClearRules.IsCleared(Census(pending: 1), anySpawned: true), Is.False);
        }

        /// <summary>
        /// 벽에서 걸어 나오는 중인 개체도 마찬가지다. 판정이 꺼져 있어 "살아 있는 적"으로
        /// 안 잡히기 쉬운데, 곧 싸울 몸이므로 위협이다.
        /// </summary>
        [Test]
        public void EnteringEnemies_KeepTheRoundOpen()
        {
            Assert.That(RoundClearRules.IsCleared(Census(entering: 1), anySpawned: true), Is.False);
        }

        [Test]
        public void FightingEnemies_KeepTheRoundOpen()
        {
            Assert.That(RoundClearRules.IsCleared(Census(fighting: 1), anySpawned: true), Is.False);
        }

        [Test]
        public void EverythingGone_IsCleared()
        {
            Assert.That(RoundClearRules.IsCleared(Census(), anySpawned: true), Is.True);
        }

        // ── 첫 프레임 보호 ───────────────────────────────

        /// <summary>
        /// 한 기도 안 나온 라운드는 클리어가 아니다. 라운드가 열린 직후는 예약이 잡히기 전이라
        /// 모든 칸이 0인 프레임이 있고, 그대로 판정하면 <b>아레나가 열리고 그냥 지나간다.</b>
        /// </summary>
        [Test]
        public void BeforeAnythingSpawns_NotCleared()
        {
            Assert.That(RoundClearRules.IsCleared(Census(), anySpawned: false), Is.False);
        }

        [Test]
        public void NegativeCounts_AreIgnored()
        {
            Assert.That(RoundClearRules.Threats(Census(pending: -3, fighting: 2)), Is.EqualTo(2));
        }
    }
}
