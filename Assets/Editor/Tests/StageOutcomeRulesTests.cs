using NUnit.Framework;
using Prototype.YG;

namespace Prototype.Tests
{
    /// <summary>
    /// 승패 판정 규칙. 씬 없이 도는 순수 함수라 여기서 전부 덮는다.
    ///
    /// 가장 중요한 항목은 <see cref="StageOutcomeRules.CanJudge"/>의 첫 프레임 보호다 —
    /// 이게 빠지면 스테이지가 뜨자마자 결과 화면이 나오는데, 원인이 "등록 순서"라
    /// 화면만 봐서는 절대 짐작할 수 없다.
    /// </summary>
    public class StageOutcomeRulesTests
    {
        // ── 판정 개시 ───────────────────────────────────

        /// <summary>Entity 는 Start 에서 등록한다. 그 전에는 양쪽 목록이 비어 있다.</summary>
        [Test]
        public void BeforeAnyoneRegisters_CannotJudge()
        {
            Assert.That(StageOutcomeRules.CanJudge(0, 0), Is.False);
        }

        [Test]
        public void OnlyOneSideRegistered_CannotJudge()
        {
            Assert.That(StageOutcomeRules.CanJudge(3, 0), Is.False, "아군이 아직 안 올라왔다");
            Assert.That(StageOutcomeRules.CanJudge(0, 5), Is.False, "적이 아직 안 올라왔다");
        }

        [Test]
        public void BothSidesRegistered_CanJudge()
        {
            Assert.That(StageOutcomeRules.CanJudge(1, 1), Is.True);
        }

        // ── 결말 ────────────────────────────────────────

        [Test]
        public void EnemiesAlive_AndAlliesAlive_IsUndecided()
        {
            Assert.That(StageOutcomeRules.Evaluate(2, false), Is.EqualTo(StageOutcome.Undecided));
        }

        [Test]
        public void NoEnemiesLeft_IsVictory()
        {
            Assert.That(StageOutcomeRules.Evaluate(0, false), Is.EqualTo(StageOutcome.Victory));
        }

        [Test]
        public void AllAlliesDead_IsDefeat()
        {
            Assert.That(StageOutcomeRules.Evaluate(2, true), Is.EqualTo(StageOutcome.Defeat));
        }

        /// <summary>
        /// 마지막 적과 마지막 아군이 같은 프레임에 쓰러졌다면 이긴 것으로 쳐 주지 않는다.
        /// 서로를 죽이고 끝났는데 다음 스테이지로 넘어가면 이상하다.
        /// </summary>
        [Test]
        public void MutualWipe_CountsAsDefeat()
        {
            Assert.That(StageOutcomeRules.Evaluate(0, true), Is.EqualTo(StageOutcome.Defeat));
        }

        /// <summary>살아 있는 적 수가 음수로 새어도 승리로 읽혀야 한다.</summary>
        [Test]
        public void NegativeEnemyCount_IsVictory()
        {
            Assert.That(StageOutcomeRules.Evaluate(-1, false), Is.EqualTo(StageOutcome.Victory));
        }

        // ── 출구 ────────────────────────────────────────

        private const float ExitX = 5f;

        [Test]
        public void ShortOfExit_DoesNotTrigger()
        {
            Assert.That(StageOutcomeRules.ReachedExit(4.9f, ExitX), Is.False);
        }

        /// <summary>경계값은 통과 쪽에 넣는다. 딱 닿았는데 안 열리면 버그로 보인다.</summary>
        [Test]
        public void ExactlyAtExit_Triggers()
        {
            Assert.That(StageOutcomeRules.ReachedExit(ExitX, ExitX), Is.True);
        }

        [Test]
        public void PastExit_Triggers()
        {
            Assert.That(StageOutcomeRules.ReachedExit(5.5f, ExitX), Is.True);
        }

        /// <summary>왼쪽 끝에 있어도 절대 열리면 안 된다.</summary>
        [Test]
        public void FarLeft_DoesNotTrigger()
        {
            Assert.That(StageOutcomeRules.ReachedExit(-6f, ExitX), Is.False);
        }

        /// <summary>
        /// 문턱은 벽(x = 6)보다 안쪽이어야 한다. 몸통 반지름 때문에 캐릭터는 5.5 근처에서
        /// 막히므로, 문턱을 벽에 맞추면 영영 닿지 않는다.
        /// </summary>
        [Test]
        public void DefaultExit_IsReachableBeforeTheWall()
        {
            const float wallX = 6f;
            const float bodyRadius = 0.5f;

            Assert.That(ExitX, Is.LessThan(wallX - bodyRadius));
        }
    
        // ── 웨이브 방 ───────────────────────────────────
        // 웨이브 사이에는 살아 있는 적이 0명인 구간이 반드시 생긴다. 그 한 프레임이
        // 승리로 잡히면 첫 웨이브만 잡고 스테이지가 끝난다 — 화면만 봐서는
        // "왜 벌써 클리어지?" 말고는 아무 단서가 없는 종류의 버그다.

        [Test]
        public void BetweenWaves_NoEnemiesAlive_IsStillUndecided()
        {
            Assert.That(StageOutcomeRules.Evaluate(0, false, wavesRemaining: true),
                        Is.EqualTo(StageOutcome.Undecided));
        }

        [Test]
        public void LastWaveCleared_IsVictory()
        {
            Assert.That(StageOutcomeRules.Evaluate(0, false, wavesRemaining: false),
                        Is.EqualTo(StageOutcome.Victory));
        }

        /// <summary>뒤에 웨이브가 몇 개 남았든 아군이 전멸하면 진 것이다.</summary>
        [Test]
        public void AllAlliesDead_LosesEvenWithWavesLeft()
        {
            Assert.That(StageOutcomeRules.Evaluate(3, true, wavesRemaining: true),
                        Is.EqualTo(StageOutcome.Defeat));
        }

        /// <summary>웨이브가 없는 방(씬에 적을 직접 놓은 방)은 지금까지와 똑같이 돈다.</summary>
        [Test]
        public void TwoArgumentForm_MeansNoWavesLeft()
        {
            Assert.That(StageOutcomeRules.Evaluate(0, false),
                        Is.EqualTo(StageOutcomeRules.Evaluate(0, false, wavesRemaining: false)));
        }
    }
}
