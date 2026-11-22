using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 전투 클리어 보상(금화 · 상자 · 두루마리)이 떨어지는 자리. 출구선을 밟지 않고 주울 수 있는가,
    /// 방 밖으로 안 나가는가, 두루마리끼리 한 번에 둘이 안 잡히는가.
    /// </summary>
    public class ClearLootRulesTests
    {
        private static readonly RoomRect Room = RoomRect.Default;   // x[-9..9] z[-3..3]
        private static float ExitLine => StageOutcomeRules.ExitLine(Room.MaxX);

        [Test]
        public void Center_StaysWhereAsked_WhenItFits()
        {
            // 방 한가운데를 주면 그 자리 그대로다 — 몸 앞으로 밀던 예전 규칙은 없앴다.
            Vector3 c = ClearLootRules.Center(new Vector3(Room.CenterX, 0f, Room.CenterZ), Room, ExitLine);

            Assert.That(c.x, Is.EqualTo(Room.CenterX).Within(0.001f));
            Assert.That(c.z, Is.EqualTo(Room.CenterZ).Within(0.001f));
        }

        [Test]
        public void BodyAtTheExit_LootStaysClearOfTheExitLine()
        {
            // 출구에 서서 이겼다. 상자가 출구선 너머에 떨어지면 주우러 가는 길이 곧 나가는 길이다.
            Vector3 c = ClearLootRules.Center(new Vector3(ExitLine + 0.5f, 0f, 0f), Room, ExitLine);

            Assert.That(ClearLootRules.ChestAt(c).x, Is.LessThanOrEqualTo(ExitLine - ClearLootRules.ExitClearance + 0.001f));
        }

        [Test]
        public void BodyAgainstTheLeftWall_GoldStaysInsideTheRoom()
        {
            Vector3 c = ClearLootRules.Center(new Vector3(-20f, 0f, 9f), Room, ExitLine);

            Assert.That(ClearLootRules.GoldAt(c).x, Is.GreaterThanOrEqualTo(Room.MinX + ClearLootRules.WallClearance - 0.001f));
            Assert.That(c.z, Is.LessThanOrEqualTo(Room.MaxZ - ClearLootRules.WallClearance + 0.001f));
        }

        [Test]
        public void SkillRow_IsInsideTheRoom_AndClearOfTheExit()
        {
            // 상자가 오른쪽 끝에 있어도 줄 전체를 안으로 민다.
            Vector3 chest = new Vector3(ExitLine - 1f, 0f, 0f);
            Vector3[] row = ClearLootRules.SkillRow(chest, Room, ExitLine, 3);

            Assert.That(row.Length, Is.EqualTo(3));
            foreach (Vector3 p in row)
            {
                Assert.That(p.x, Is.LessThanOrEqualTo(ExitLine - ClearLootRules.ExitClearance + 0.001f));
                Assert.That(p.x, Is.GreaterThanOrEqualTo(Room.MinX + ClearLootRules.WallClearance - 0.001f));
                Assert.That(p.z, Is.InRange(Room.MinZ, Room.MaxZ));
            }
        }

        [Test]
        public void SkillRow_SitsInFrontOfTheChest_OrBehindAtTheFrontWall()
        {
            Vector3[] middle = ClearLootRules.SkillRow(Vector3.zero, Room, ExitLine, 3);
            Assert.That(middle[0].z, Is.LessThan(0f), "보통은 상자 앞(카메라 쪽)에 선다.");

            Vector3[] atWall = ClearLootRules.SkillRow(new Vector3(0f, 0f, Room.MinZ + 0.5f), Room, ExitLine, 3);
            Assert.That(atWall[0].z, Is.GreaterThan(Room.MinZ + 0.5f), "앞이 벽이면 뒤로 놓는다.");
        }

        [Test]
        public void FinalRoomFarRight_LootLandsInThatRoom()
        {
            // 통로 너머 마지막 아레나(x 35.7~53.7)에서 이겼다. 기준 방(x −9~9)으로 계산하면 보상이
            // 첫 방 좌표로 끌려가 눈앞에서 사라진다 — 고친 버그다.
            var finalRoom = new RoomRect(35.73f, 53.73f, -ArenaSpawnPlanner.ArenaHalfZ, ArenaSpawnPlanner.ArenaHalfZ);
            float exit = StageOutcomeRules.ExitLine(finalRoom.MaxX);

            Vector3 c = ClearLootRules.Center(new Vector3(44f, 0f, 0f), finalRoom, exit);

            foreach (Vector3 p in new[] { ClearLootRules.GoldAt(c), ClearLootRules.ChestAt(c) })
            {
                Assert.That(p.x, Is.InRange(finalRoom.MinX, exit), "금화 · 상자는 마지막 방 안, 출구선 안쪽이다.");
                Assert.That(p.z, Is.InRange(finalRoom.MinZ, finalRoom.MaxZ));
            }

            foreach (Vector3 p in ClearLootRules.SkillRow(ClearLootRules.ChestAt(c), finalRoom, exit, 3))
                Assert.That(p.x, Is.InRange(finalRoom.MinX, exit), "두루마리도 마지막 방 안이다.");
        }

        [Test]
        public void SkillGap_KeepsOnlyOneScrollInReach()
        {
            // 두 두루마리 사이 한가운데에 서도 둘 다 반경 안에 들면 안 된다 — 키 안내가 둘 사이에서 깜빡인다.
            Assert.That(ClearLootRules.SkillGap, Is.GreaterThan(ClearLootRules.SkillRadius * 2f));
        }

        [Test]
        public void LootArt_ResourceHasEverySprite()
        {
            // 그림이 빠지면 보상은 돌지만 바닥에 아무것도 안 보인다 — 이름표만 떠 있다.
            var art = Resources.Load<LootArt>(LootArt.ResourcePath);

            Assert.That(art, Is.Not.Null, $"Resources/{LootArt.ResourcePath} 가 없다.");
            Assert.That(art.coin, Is.Not.Null, "금화(원) 그림");
            Assert.That(art.ChestOf(false, false), Is.Not.Null, "닫힌 상자");
            Assert.That(art.ChestOf(false, true), Is.Not.Null, "열린 상자");
            Assert.That(art.ChestOf(true, false), Is.Not.Null, "정예 닫힌 상자");
            Assert.That(art.ChestOf(true, true), Is.Not.Null, "정예 열린 상자");

            foreach (Role role in System.Enum.GetValues(typeof(Role)))
                Assert.That(art.ScrollOf(role), Is.Not.Null, $"{role} 두루마리");
        }

        [Test]
        public void NarrowRoom_DoesNotThrow_AndCenters()
        {
            var narrow = new RoomRect(0f, 2f, -0.5f, 0.5f);
            Vector3 c = ClearLootRules.Center(Vector3.zero, narrow, StageOutcomeRules.ExitLine(narrow.MaxX));

            Assert.That(float.IsNaN(c.x), Is.False);
            Assert.That(ClearLootRules.SkillRow(c, narrow, 1f, 3).Length, Is.EqualTo(3));
        }
    }
}
