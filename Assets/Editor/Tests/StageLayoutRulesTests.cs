using NUnit.Framework;
using Prototype;

namespace Prototype.Tests
{
    /// <summary>
    /// 구간 목록 읽기. 구간이 딱 붙어 있어서 <b>경계에 선 좌표가 어느 쪽인지</b>가 애매하고,
    /// 그 한 칸이 틀리면 아레나 트리거가 한 프레임 일찍 걸리거나 영영 안 걸린다.
    /// 화면만 봐서는 절대 안 보이는 종류의 버그다.
    /// </summary>
    public class StageLayoutRulesTests
    {
        /// <summary>아레나1 → 통로 → 아레나2. 빌더가 굽는 것과 같은 형태다.</summary>
        private static readonly StageSection[] Stage =
        {
            StageSection.Of(SectionKind.Arena, -6f, 6f),
            StageSection.Of(SectionKind.Corridor, 6f, 33f),
            StageSection.Of(SectionKind.Arena, 33f, 45f),
        };

        [Test]
        public void InsideEachSection_FindsIt()
        {
            Assert.That(StageLayoutRules.SectionAt(Stage, -3f), Is.EqualTo(0));
            Assert.That(StageLayoutRules.SectionAt(Stage, 20f), Is.EqualTo(1));
            Assert.That(StageLayoutRules.SectionAt(Stage, 40f), Is.EqualTo(2));
        }

        /// <summary>
        /// 경계 좌표는 <b>다음 구간</b>의 것이다. 아레나의 왼쪽 경계가 곧 진입 트리거라,
        /// 이게 반대면 아레나 안에 서 있는데 통로로 판정되어 카메라 락이 안 걸린다.
        /// </summary>
        [Test]
        public void OnABoundary_BelongsToTheNextSection()
        {
            Assert.That(StageLayoutRules.SectionAt(Stage, 6f), Is.EqualTo(1), "통로 시작이 아레나로 잡혔다");
            Assert.That(StageLayoutRules.SectionAt(Stage, 33f), Is.EqualTo(2), "아레나2 진입선이 통로로 잡혔다");
        }

        /// <summary>
        /// 넉백으로 벽을 뚫거나 마지막 아레나 오른쪽 끝에 서는 일이 실제로 생긴다.
        /// 거기서 -1을 돌려주면 부르는 쪽이 전부 null 검사를 해야 한다.
        /// </summary>
        [Test]
        public void OutsideTheStage_ClampsToTheEnds()
        {
            Assert.That(StageLayoutRules.SectionAt(Stage, -50f), Is.EqualTo(0));
            Assert.That(StageLayoutRules.SectionAt(Stage, 999f), Is.EqualTo(2));
            Assert.That(StageLayoutRules.SectionAt(Stage, 45f), Is.EqualTo(2), "마지막 구간의 오른쪽 끝");
        }

        [Test]
        public void EmptyLayout_HasNoSection()
        {
            Assert.That(StageLayoutRules.SectionAt(new StageSection[0], 0f), Is.EqualTo(-1));
            Assert.That(StageLayoutRules.SectionAt(null, 0f), Is.EqualTo(-1));
        }

        [Test]
        public void EntryLine_IsTheArenaLeftEdge()
        {
            Assert.That(StageLayoutRules.EntryLineOf(Stage[2]), Is.EqualTo(33f));
        }

        [Test]
        public void SectionGeometry_IsCenterAndWidth()
        {
            Assert.That(Stage[2].Center, Is.EqualTo(39f).Within(0.001f));
            Assert.That(Stage[2].Width, Is.EqualTo(12f).Within(0.001f));
        }

        /// <summary>구간이 이어 붙어 있어야 한다. 틈이 있으면 그 좌표에서 카메라가 튄다.</summary>
        [Test]
        public void SectionsAreContiguous()
        {
            for (int i = 1; i < Stage.Length; i++)
                Assert.That(Stage[i].minX, Is.EqualTo(Stage[i - 1].maxX).Within(0.001f), $"{i}번 구간 앞에 틈이 있다");
        }

        // ── 통로 배경 타일 ──────────────────────────────

        /// <summary>배경 한 장이 통로보다 짧으면 바닥이 끊겨 보인다. 모자라면 올림이다.</summary>
        [Test]
        public void TileCount_RoundsUpToCoverTheSection()
        {
            Assert.That(StageLayoutRules.TileCount(27f, 13.36f), Is.EqualTo(3));
            Assert.That(StageLayoutRules.TileCount(26.72f, 13.36f), Is.EqualTo(2));
        }

        /// <summary>배경이 아무리 넓어도 한 장은 깐다 — 0장이면 통로 바닥이 통째로 없다.</summary>
        [Test]
        public void TileCount_IsNeverZero()
        {
            Assert.That(StageLayoutRules.TileCount(5f, 100f), Is.EqualTo(1));
            Assert.That(StageLayoutRules.TileCount(0f, 13f), Is.EqualTo(1));
        }

        [Test]
        public void TileCount_WithNoWidth_IsZero()
        {
            Assert.That(StageLayoutRules.TileCount(27f, 0f), Is.Zero);
        }
    }
}
