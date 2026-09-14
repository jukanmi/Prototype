using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Prototype.Tests
{
    /// <summary>
    /// 레시피의 저작 실수. <b>틀린 레시피 하나가 문제를 정확히 하나씩</b> 내는지 본다 —
    /// 하나의 실수가 경고 셋으로 번지면 무엇을 고쳐야 할지 안 읽힌다.
    /// </summary>
    public class RunMapRecipeTests
    {
        private static NodeCandidate N(string scene, MapNodeKind kind, int weight = 1)
            => NodeCandidate.Of(scene, kind, weight);

        private static FloorRule Boss() => FloorRule.Of(1, 1, N("Z", MapNodeKind.Boss));

        private static List<RunMapProblem> Problems(params FloorRule[] floors)
            => RunMapRules.RecipeIssues(floors).Select(i => i.problem).ToList();

        [Test]
        public void ValidRecipe_IsClean()
        {
            Assert.That(Problems(FloorRule.Of(1, 2, N("A", MapNodeKind.Battle), N("", MapNodeKind.Rest)), Boss()),
                        Is.Empty);
        }

        [Test]
        public void NoFloors()
        {
            Assert.That(Problems(), Is.EqualTo(new[] { RunMapProblem.NoFloors }));
            Assert.That(RunMapRules.RecipeIssues(null).Single().problem, Is.EqualTo(RunMapProblem.NoFloors));
        }

        [Test]
        public void EmptyFloor()
        {
            Assert.That(Problems(FloorRule.Of(1, 1), Boss()), Is.EqualTo(new[] { RunMapProblem.EmptyFloor }));
        }

        [TestCase(0, 1)]
        [TestCase(3, 2)]
        public void BadNodeRange(int min, int max)
        {
            Assert.That(Problems(FloorRule.Of(min, max, N("A", MapNodeKind.Battle)), Boss()),
                        Is.EqualTo(new[] { RunMapProblem.BadNodeRange }));
        }

        [Test]
        public void ZeroWeight()
        {
            Assert.That(Problems(FloorRule.Of(1, 1, N("A", MapNodeKind.Battle, 0)), Boss()),
                        Is.EqualTo(new[] { RunMapProblem.ZeroWeight }));
        }

        /// <summary>씬이 빈 칸은 휴식만 된다(계획서 2.1). 씬 달린 휴식은 정상이다.</summary>
        [TestCase(MapNodeKind.Battle)]
        [TestCase(MapNodeKind.Elite)]
        [TestCase(MapNodeKind.Shop)]
        [TestCase(MapNodeKind.Event)]
        public void MissingScene_OnlyRestMaySkipIt(MapNodeKind kind)
        {
            Assert.That(Problems(FloorRule.Of(1, 1, N("  ", kind)), Boss()),
                        Is.EqualTo(new[] { RunMapProblem.MissingScene }));

            Assert.That(Problems(FloorRule.Of(1, 1, N("", MapNodeKind.Rest)), Boss()), Is.Empty);
            Assert.That(Problems(FloorRule.Of(1, 1, N("Node_Rest", MapNodeKind.Rest)), Boss()), Is.Empty);
        }

        [Test]
        public void BossBeforeLastFloor()
        {
            Assert.That(Problems(FloorRule.Of(1, 1, N("B", MapNodeKind.Boss)), Boss()),
                        Is.EqualTo(new[] { RunMapProblem.BossBeforeLastFloor }));
        }

        [Test]
        public void LastFloorNotSingleBoss()
        {
            FloorRule start = FloorRule.Of(1, 1, N("A", MapNodeKind.Battle));

            Assert.That(Problems(start, FloorRule.Of(1, 2, N("Z", MapNodeKind.Boss))),
                        Is.EqualTo(new[] { RunMapProblem.LastFloorNotSingleBoss }), "칸이 둘일 수 있다");

            Assert.That(Problems(start, FloorRule.Of(1, 1, N("Z", MapNodeKind.Boss), N("Y", MapNodeKind.Battle))),
                        Is.EqualTo(new[] { RunMapProblem.LastFloorNotSingleBoss }), "보스가 아닌 후보");
        }

        [Test]
        public void CandidateScene_IsTrimmed()
        {
            Assert.That(N("  Stage_01 ", MapNodeKind.Battle).Scene, Is.EqualTo("Stage_01"));
            Assert.That(new NodeCandidate().Scene, Is.EqualTo(""));
        }
    }
}
