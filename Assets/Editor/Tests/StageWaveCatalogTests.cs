using NUnit.Framework;
using Prototype;

namespace Prototype.Tests
{
    /// <summary>
    /// 스테이지 <b>지형</b>. 몇 개인가, 어디가 아레나인가, 주제가 무엇인가.
    ///
    /// <b>웨이브 내용은 여기 없다.</b> 예전에는 이 표가 배치까지 들고 있었고 이 파일이
    /// 기획서의 사본이었지만, 웨이브가 애셋으로 옮겨 가면서 그 역할은
    /// <see cref="WaveAssetContentTests"/>로 넘어갔다. 여기 남은 것은
    /// <c>GameManager</c> · <c>StageDirector</c> · <c>BattleSceneController</c>가
    /// 함께 읽는 스테이지 번호 규약뿐이다.
    /// </summary>
    public class StageWaveCatalogTests
    {
        [Test]
        public void FiveStages_WithArenasAtTwoAndFive()
        {
            Assert.That(StageWaveCatalog.StageCount, Is.EqualTo(5));
            Assert.That(StageWaveCatalog.ArenaStageNumber, Is.EqualTo(2));
            Assert.That(StageWaveCatalog.BossStageNumber, Is.EqualTo(5));
        }

        [Test]
        public void ArenaStages_AreNotWaveStages()
        {
            Assert.That(StageWaveCatalog.IsWaveStage(1), Is.True);
            Assert.That(StageWaveCatalog.IsWaveStage(2), Is.False, "아레나 방이 웨이브로 잡혔다");
            Assert.That(StageWaveCatalog.IsWaveStage(3), Is.True);
            Assert.That(StageWaveCatalog.IsWaveStage(4), Is.True);
            Assert.That(StageWaveCatalog.IsWaveStage(5), Is.False, "보스 방이 웨이브로 잡혔다");

            Assert.That(StageWaveCatalog.IsArenaStage(2), Is.True);
            Assert.That(StageWaveCatalog.IsArenaStage(5), Is.True);
        }

        /// <summary>범위 밖 번호는 웨이브 방도 아레나 방도 아니다. 조용히 1번으로 접으면 안 된다.</summary>
        [Test]
        public void OutOfRangeStage_IsNeitherKind()
        {
            Assert.That(StageWaveCatalog.IsWaveStage(0), Is.False);
            Assert.That(StageWaveCatalog.IsWaveStage(99), Is.False);
        }

        /// <summary>디렉터 로그와 화면 문구가 읽는다. 비어 있으면 "스테이지 3 — " 로 끝난다.</summary>
        [Test]
        public void EveryStage_HasATheme()
        {
            for (int stage = 1; stage <= StageWaveCatalog.StageCount; stage++)
                Assert.That(StageWaveCatalog.ThemeOf(stage), Is.Not.Empty, $"스테이지 {stage}");
        }

        /// <summary>주제는 범위 밖 번호에서도 답이 있어야 한다. 로그가 예외로 끊기면 안 된다.</summary>
        [Test]
        public void ThemeOf_ClampsToBothEnds()
        {
            Assert.That(StageWaveCatalog.ThemeOf(0), Is.EqualTo(StageWaveCatalog.ThemeOf(1)));
            Assert.That(StageWaveCatalog.ThemeOf(99),
                        Is.EqualTo(StageWaveCatalog.ThemeOf(StageWaveCatalog.StageCount)));
        }

        /// <summary>
        /// 동시 공격 허용치. 앞 스테이지는 2, 뒤는 3이다.
        /// 3을 넘기면 회피가 성립하지 않고, 2 아래면 적이 서서 구경만 한다.
        /// </summary>
        [Test]
        public void AttackTokenConstants_StayInTheDesignedRange()
        {
            Assert.That(StageWaveCatalog.EarlyTokens, Is.EqualTo(2));
            Assert.That(StageWaveCatalog.LateTokens, Is.EqualTo(3));
        }
    }
}
