using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 조우 목록을 <b>방</b>으로 읽는 규칙(<see cref="EncounterRoomRules"/>).
    ///
    /// 통로로 이어진 아레나 스테이지에서 났던 버그의 그물이다 — 스테이지가 "방 하나"를 가정해
    /// 보상이 첫 방 좌표에 떨어지고, 출구선이 첫 방 안이라 마지막 방에서 출구가 안 열렸다.
    /// 중간 방은 문만 열고, 보상 · 출구는 마지막 방에서만 나와야 한다. 방이 셋 이상이어도 같다.
    /// </summary>
    public class EncounterRoomRulesTests
    {
        private readonly List<GameObject> made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in made) Object.DestroyImmediate(go);
            made.Clear();
        }

        private EncounterSite Site(float minX, float maxX)
        {
            var go = new GameObject($"Site_{minX}");
            made.Add(go);

            EncounterSite site = go.AddComponent<EncounterSite>();
            site.Configure(minX, maxX, null, null);
            return site;
        }

        private static StageEncounter At(EncounterSite site)
            => new StageEncounter { trigger = EncounterTrigger.CrossLine, site = site };

        private static readonly StageEncounter InRoom = new StageEncounter { trigger = EncounterTrigger.AfterPrevious };

        // ── 방 나누기 ───────────────────────────────────

        [Test]
        public void TwoArenas_AreTwoRooms_AndEachEndsItsOwnRoom()
        {
            var list = new[] { At(Site(-6f, 6f)), At(Site(32f, 44f)) };

            Assert.That(EncounterRoomRules.RoomCount(list), Is.EqualTo(2));
            Assert.That(EncounterRoomRules.EndsRoom(list, 0), Is.True, "첫 방을 깨면 나가는 문이 열려야 다음 방으로 간다.");
            Assert.That(EncounterRoomRules.EndsRoom(list, 1), Is.True);
            Assert.That(EncounterRoomRules.RoomIndexOf(list, 1), Is.EqualTo(1));
        }

        [Test]
        public void RoundsInOneArena_OnlyTheLastRoundEndsTheRoom()
        {
            // 같은 아레나의 라운드 사이에 문이 열리면 그 틈에 걸어 나가 방 밖에 갇힌다.
            EncounterSite arena = Site(-6f, 6f);
            var list = new[] { At(arena), At(arena), At(Site(32f, 44f)) };

            Assert.That(EncounterRoomRules.EndsRoom(list, 0), Is.False);
            Assert.That(EncounterRoomRules.EndsRoom(list, 1), Is.True);
            Assert.That(EncounterRoomRules.RoomCount(list), Is.EqualTo(2));
            Assert.That(EncounterRoomRules.RoomIndexOf(list, 1), Is.EqualTo(0));
        }

        [Test]
        public void WavesWithoutASite_AreOneRoom()
        {
            var list = new[] { InRoom, InRoom, InRoom };

            Assert.That(EncounterRoomRules.RoomCount(list), Is.EqualTo(1));
            Assert.That(EncounterRoomRules.EndsRoom(list, 0), Is.False);
            Assert.That(EncounterRoomRules.EndsRoom(list, 2), Is.True);
        }

        [Test]
        public void OutOfRangeOrEmpty_IsHarmless()
        {
            var list = new[] { InRoom };

            Assert.That(EncounterRoomRules.EndsRoom(list, -1), Is.False);
            Assert.That(EncounterRoomRules.EndsRoom(list, 1), Is.False);
            Assert.That(EncounterRoomRules.RoomIndexOf(list, 5), Is.EqualTo(-1));
            Assert.That(EncounterRoomRules.RoomCount(new StageEncounter[0]), Is.EqualTo(0));
            Assert.That(EncounterRoomRules.RoomCount(null), Is.EqualTo(0));
        }

        // ── 끝나는 방 ───────────────────────────────────

        [Test]
        public void FinalArea_IsTheLastArena_NotTheFirst()
        {
            EncounterSite last = Site(32.73f, 44.73f);
            var list = new[] { At(Site(-6f, 6f)), At(last) };

            Assert.That(EncounterRoomRules.TryFinalArea(list, RoomRect.Default, out RoomRect area), Is.True);

            // 씬에 방이 있어도 마지막 조우에 자리가 있으면 자리가 이긴다.
            Assert.That(area.MinX, Is.EqualTo(32.73f).Within(0.001f));
            Assert.That(area.MaxX, Is.EqualTo(44.73f).Within(0.001f));

            float exit = StageOutcomeRules.ExitLine(area.MaxX);
            Assert.That(exit, Is.GreaterThan(32.73f), "출구선은 마지막 방 안에 있어야 한다 — 첫 방 안이면 마지막 방에서 영영 안 열린다.");
        }

        [Test]
        public void FinalArea_ThreeRooms_IsTheThird()
        {
            var list = new[] { At(Site(-6f, 6f)), At(Site(20f, 32f)), At(Site(46f, 58f)) };

            Assert.That(EncounterRoomRules.TryFinalArea(list, null, out RoomRect area), Is.True);
            Assert.That(area.MinX, Is.EqualTo(46f).Within(0.001f));
            Assert.That(EncounterRoomRules.RoomCount(list), Is.EqualTo(3));
        }

        [Test]
        public void FinalArea_WaveRoom_IsTheSceneRoom()
        {
            var list = new[] { InRoom, InRoom };
            var room = new RoomRect(-7.5f, 7.5f, -3f, 3f);   // 125% 방

            Assert.That(EncounterRoomRules.TryFinalArea(list, room, out RoomRect area), Is.True);
            Assert.That(area.MaxX, Is.EqualTo(7.5f).Within(0.001f));
        }

        [Test]
        public void FinalArea_Unknown_IsFalse()
        {
            // 방도 자리도 없으면 모른다 — 부르는 쪽이 저작값으로 떨어진다.
            Assert.That(EncounterRoomRules.TryFinalArea(new[] { InRoom }, null, out _), Is.False);
            Assert.That(EncounterRoomRules.TryFinalArea(new StageEncounter[0], RoomRect.Default, out _), Is.False);
            Assert.That(EncounterRoomRules.TryFinalArea(null, RoomRect.Default, out _), Is.False);
        }

        [Test]
        public void SiteArea_UsesAuthoredBounds_AndArenaDepth()
        {
            RoomRect area = Site(32f, 44f).Area;

            Assert.That(area.MinX, Is.EqualTo(32f).Within(0.001f));
            Assert.That(area.MaxX, Is.EqualTo(44f).Within(0.001f));
            Assert.That(area.HalfZ, Is.EqualTo(ArenaSpawnPlanner.ArenaHalfZ).Within(0.001f));
        }
    }
}
