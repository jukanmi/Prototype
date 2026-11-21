using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 창 안의 키보드 커서(<see cref="KeyMenu"/>) — 처음 어디에 서는가, 잠긴 칸을 건너뛰는가,
    /// 칸이 잠기면(상점에서 산 카드) 커서가 비켜나는가, 고르면 그 칸이 불리는가.
    /// 키 입력 자체는 <c>PlayerInputControllerTests</c>와 같은 이유로 여기서 누르지 못한다.
    /// </summary>
    public class KeyMenuTests
    {
        private GameObject root;
        private KeyMenu menu;
        private readonly List<int> picked = new List<int>();
        private readonly bool[] locked = new bool[3];

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Root", typeof(RectTransform));
            menu = new KeyMenu(root.transform);
            picked.Clear();

            for (int i = 0; i < locked.Length; i++)
            {
                int captured = i;
                locked[i] = false;

                var rect = new GameObject($"Item_{i}", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(root.transform, false);
                rect.anchoredPosition = new Vector2(i * 100f, 0f);

                menu.Add(rect, 0, () => !locked[captured], () => picked.Add(captured));
            }
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Open_StandsOnTheFirstUsableItem()
        {
            locked[0] = true;
            menu.Open();

            Assert.AreEqual(1, menu.Cursor);
        }

        [Test]
        public void Pick_CallsTheFocusedItem()
        {
            menu.Open();
            menu.Pick();

            CollectionAssert.AreEqual(new[] { 0 }, picked);
        }

        [Test]
        public void LockingTheFocusedItem_MovesTheCursorOff()
        {
            // 상점에서 산 카드는 잠긴다. 커서가 그 위에 남으면 F가 아무것도 안 하는 칸을 누른다.
            menu.Open();
            locked[0] = true;
            menu.Refresh();

            Assert.AreEqual(1, menu.Cursor);
        }

        [Test]
        public void EverythingLocked_PickDoesNothing()
        {
            for (int i = 0; i < locked.Length; i++) locked[i] = true;
            menu.Open();
            menu.Pick();

            Assert.AreEqual(MenuCursor.None, menu.Cursor);
            Assert.IsEmpty(picked);
        }

        [Test]
        public void Moved_ReportsPreviousAndNext()
        {
            // 선택지 글자색 · 카드 확대가 이 알림으로 바뀐다. 이전 칸을 안 알려 주면 확대가 남는다.
            var moves = new List<(int, int)>();
            menu.Moved += (from, to) => moves.Add((from, to));

            menu.Open();
            locked[0] = true;
            menu.Refresh();

            CollectionAssert.AreEqual(new[] { (MenuCursor.None, 0), (0, 1) }, moves);
        }
    }
}
