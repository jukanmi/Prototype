using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 땅속에서 솟아오르는 등장의 숫자와 판단.
    ///
    /// 이 등장만 <b>예고가 선택이 아니다.</b> 벽에서 걸어 나오거나 화면 밖에서 날아오는 적은
    /// 오는 모습 자체가 예고지만, 발밑에서 솟는 적은 표식이 없으면 반응할 정보가 아예 없다.
    ///
    /// 계획서: docs/Wave_Authoring_Refactor_Plan.md (3.4, 6단계)
    /// </summary>
    public class BurrowRulesTests
    {
        // ── 솟는 높이 ───────────────────────────────────

        /// <summary>
        /// 몸을 꺼내는 지점이 출발과 도착 <b>사이</b>에 있어야 한다.
        /// 0이면 처음부터 보여 땅속이 아니고, 깊이와 같으면 다 올라와서야 튀어나온다.
        /// </summary>
        [Test]
        public void RevealHappensPartwayUp()
        {
            Assert.That(BurrowRules.RiseBeforeReveal, Is.GreaterThan(0f), "처음부터 보인다");
            Assert.That(BurrowRules.RiseBeforeReveal, Is.LessThan(BurrowRules.Depth),
                        "다 올라와서야 튀어나온다");
        }

        /// <summary>
        /// 꺼내는 지점은 <b>지면 아래</b>다. 지면 위로 잡으면 몸이 다 올라와도 그 선에 못 닿아
        /// 영영 안 나타난다 — 화면에 없는 적이 살아 있어서 웨이브가 안 끝난다.
        /// </summary>
        [Test]
        public void RevealY_SitsBelowTheGround()
        {
            Assert.That(BurrowRules.RevealY(0f), Is.LessThan(0f), "지면 위에서 꺼내려 한다");
            Assert.That(BurrowRules.RevealY(0f), Is.GreaterThan(-BurrowRules.Depth),
                        "출발점보다 아래라 처음부터 보인다");
        }

        /// <summary>발판 위에서 솟는 적도 같게 돌아야 한다. 지면이 올라가면 기준선도 같이 올라간다.</summary>
        [Test]
        public void RevealY_FollowsTheGround()
        {
            Assert.That(BurrowRules.RevealY(5f) - BurrowRules.RevealY(0f),
                        Is.EqualTo(5f).Within(0.0001f));
        }

        // ── 예고 시각 ───────────────────────────────────

        /// <summary>표식은 몸보다 예고 길이만큼 먼저 뜬다.</summary>
        [Test]
        public void TelegraphLeadsTheBody()
        {
            float appearAt = 3f;

            Assert.That(BurrowRules.TelegraphAt(appearAt),
                        Is.EqualTo(appearAt - ArenaSpawnPlanner.TelegraphLead).Within(0.0001f));
            Assert.That(BurrowRules.TelegraphSeconds(appearAt),
                        Is.EqualTo(ArenaSpawnPlanner.TelegraphLead).Within(0.0001f));
        }

        /// <summary>
        /// 웨이브 시작 전은 없다. 앞이 모자라면 0으로 물리고, 표식은 남은 시간만큼만 뜬다.
        /// 짧아도 안 띄우는 것보다는 낫다 — 안 띄우면 아무 단서가 없다.
        /// </summary>
        [Test]
        public void TelegraphNeverStartsBeforeTheWave()
        {
            Assert.That(BurrowRules.TelegraphAt(0f), Is.EqualTo(0f));
            Assert.That(BurrowRules.TelegraphSeconds(0f), Is.EqualTo(0f));

            float early = ArenaSpawnPlanner.TelegraphLead * 0.5f;
            Assert.That(BurrowRules.TelegraphAt(early), Is.EqualTo(0f));
            Assert.That(BurrowRules.TelegraphSeconds(early), Is.EqualTo(early).Within(0.0001f));
        }

        /// <summary>음수로 저작된 시각도 0으로 본다.</summary>
        [Test]
        public void NegativeAppearTime_FoldsToZero()
        {
            Assert.That(BurrowRules.TelegraphAt(-5f), Is.EqualTo(0f));
            Assert.That(BurrowRules.TelegraphSeconds(-5f), Is.EqualTo(0f));
        }

        /// <summary>예고를 낼 시간이 있는지는 저작 검증이 읽는다.</summary>
        [Test]
        public void HasRoomForTelegraph_MarksTheBoundary()
        {
            float lead = ArenaSpawnPlanner.TelegraphLead;

            Assert.That(BurrowRules.HasRoomForTelegraph(0f), Is.False);
            Assert.That(BurrowRules.HasRoomForTelegraph(lead * 0.5f), Is.False);
            Assert.That(BurrowRules.HasRoomForTelegraph(lead), Is.True);
            Assert.That(BurrowRules.HasRoomForTelegraph(lead + 1f), Is.True);
        }

        // ── 주문서 ──────────────────────────────────────

        /// <summary>제자리에서 올라온다. 출발과 착지가 같고 출발만 아래다.</summary>
        [Test]
        public void PlanBurrow_RisesInPlace()
        {
            var landing = new Vector3(2f, 0f, 1f);

            EntranceSpec spec = EntranceDirector.PlanBurrow(landing);

            Assert.That(spec.start, Is.EqualTo(landing));
            Assert.That(spec.landing, Is.EqualTo(landing));
            Assert.That(spec.startDepth, Is.EqualTo(BurrowRules.Depth).Within(0.0001f));
        }

        /// <summary>솟는 동안은 판정도 조종도 꺼져 있어야 한다.</summary>
        [Test]
        public void PlanBurrow_GuardsAndLocks()
        {
            EntranceSpec spec = EntranceDirector.PlanBurrow(Vector3.zero);

            Assert.That(spec.guard, Is.True, "올라오는 몸이 맞는다");
            Assert.That(spec.lockControl, Is.True, "올라오는 도중에 제 판단으로 움직인다");
            Assert.That(spec.unscaled, Is.False, "시간이 멈춘 화면에서 적만 솟는다");
        }

        /// <summary>
        /// 보는 방향을 주문서가 정해 준다. 출발과 착지가 같아 진행 방향이 0이라,
        /// 안 정하면 프리팹에 저장된 방향 그대로 등을 보이고 솟는다.
        /// </summary>
        [Test]
        public void PlanBurrow_FacesTheRoomCentre()
        {
            Assert.That(EntranceDirector.PlanBurrow(new Vector3(4f, 0f, 0f)).facing.x,
                        Is.LessThan(0f), "오른쪽에서 솟는 적이 바깥을 본다");
            Assert.That(EntranceDirector.PlanBurrow(new Vector3(-4f, 0f, 0f)).facing.x,
                        Is.GreaterThan(0f), "왼쪽에서 솟는 적이 바깥을 본다");
        }

        [Test]
        public void PlanBurrow_UsesItsOwnDurationByDefault()
        {
            Assert.That(EntranceDirector.PlanBurrow(Vector3.zero).seconds,
                        Is.EqualTo(BurrowRules.Seconds).Within(0.0001f));
            Assert.That(EntranceDirector.PlanBurrow(Vector3.zero, 1.1f).seconds,
                        Is.EqualTo(1.1f).Within(0.0001f));
        }
    }
}
