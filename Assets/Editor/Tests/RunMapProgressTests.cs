using System.Linq;
using NUnit.Framework;

namespace Prototype.Tests
{
    /// <summary>
    /// 지도 위 진행 — current · pending 두 값. 패배 후 재시작이 "pending을 다시 연다"로 성립하는지가 핵심이다.
    /// </summary>
    public class RunMapProgressTests
    {
        /// <summary>
        /// <code>
        ///        [0 A]
        ///       /     \
        ///    [1 B]   [2 C]
        ///      |    /
        ///    [3 D]  ← 2는 3으로만 간다
        ///      |
        ///    [4 Boss]
        /// </code>
        /// </summary>
        private static RunMap Map() => new RunMap(9, new[]
        {
            new[] { new MapNode(0, 0, 0, MapNodeKind.Battle, "A", new[] { 1, 2 }) },
            new[]
            {
                new MapNode(1, 1, 0, MapNodeKind.Battle, "B", new[] { 3 }),
                new MapNode(2, 1, 1, MapNodeKind.Shop, "C", new[] { 3 }),
            },
            new[] { new MapNode(3, 2, 0, MapNodeKind.Battle, "D", new[] { 4 }) },
            new[] { new MapNode(4, 3, 0, MapNodeKind.Boss, "Z", null) },
        });

        private static int[] Ids(RunMapProgress p) => p.Choices().Select(n => n.Id).ToArray();

        [Test]
        public void BeforeStart_ChoicesAreFloorZero()
        {
            var p = new RunMapProgress(Map());

            Assert.That(p.Current, Is.EqualTo(RunMapProgress.None));
            Assert.That(p.Pending, Is.EqualTo(RunMapProgress.None));
            Assert.That(Ids(p), Is.EqualTo(new[] { 0 }));
            Assert.That(p.FloorNumber, Is.EqualTo(1));
            Assert.That(p.IsFinished, Is.False);
        }

        [Test]
        public void Select_OutsideChoices_IsRefused()
        {
            var p = new RunMapProgress(Map());

            Assert.That(p.TrySelect(1), Is.False, "1층 칸은 아직 못 고른다");
            Assert.That(p.TrySelect(99), Is.False, "없는 칸");
            Assert.That(p.Pending, Is.EqualTo(RunMapProgress.None));
        }

        [Test]
        public void Select_WhilePending_IsRefused()
        {
            var p = new RunMapProgress(Map());
            Assert.That(p.TrySelect(0), Is.True);
            p.CompletePending();

            Assert.That(p.TrySelect(1), Is.True);
            Assert.That(p.TrySelect(2), Is.False, "들어가 있는 동안 다른 칸으로 못 간다");
            Assert.That(p.Pending, Is.EqualTo(1));
        }

        [Test]
        public void Complete_MovesChoicesToThatNodesNext()
        {
            var p = new RunMapProgress(Map());
            p.TrySelect(0);

            Assert.That(p.CompletePending(), Is.True);
            Assert.That(p.Current, Is.EqualTo(0));
            Assert.That(Ids(p), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(p.FloorNumber, Is.EqualTo(2));

            p.TrySelect(2);
            p.CompletePending();

            Assert.That(Ids(p), Is.EqualTo(new[] { 3 }), "상점(2)은 3으로만 이어진다");
            Assert.That(p.Visited, Is.EqualTo(new[] { 0, 2 }));
            Assert.That(p.HasVisited(1), Is.False);
        }

        /// <summary>진 뒤 재시작은 같은 pending을 다시 연다 — 끝내지 않았으니 되돌릴 게 없다.</summary>
        [Test]
        public void Pending_SurvivesUntilCompleted()
        {
            var p = new RunMapProgress(Map());
            p.TrySelect(0);

            Assert.That(p.PendingNode.Scene, Is.EqualTo("A"));
            Assert.That(p.FloorNumber, Is.EqualTo(1), "들어가 있는 칸의 층");
            Assert.That(p.Visited, Is.Empty);
        }

        [Test]
        public void Complete_WithoutPending_IsRefused()
        {
            var p = new RunMapProgress(Map());
            Assert.That(p.CompletePending(), Is.False);
        }

        [Test]
        public void FinishingBoss_EndsTheRun()
        {
            var p = new RunMapProgress(Map());
            foreach (int id in new[] { 0, 1, 3, 4 })
            {
                Assert.That(p.TrySelect(id), Is.True, $"칸 {id}");
                p.CompletePending();
            }

            Assert.That(p.IsFinished, Is.True);
            Assert.That(p.Choices(), Is.Empty);
            Assert.That(p.FloorNumber, Is.EqualTo(4), "마지막 층 번호에 머문다");
        }
    }
}
