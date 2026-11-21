// 창 안의 칸을 키보드로 고르는 커서 — 상점 카드 · 이벤트 선택지가 같이 쓴다.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Prototype
{
    // ══ KeyMenu ═══════════════════════════════════════════

    /// <summary>
    /// 창 안의 칸을 키보드로 고른다. 방향키 · WASD로 옮기고 F · Enter로 고른다
    /// (<see cref="PlayerInputController.MenuStep"/> · <see cref="PlayerInputController.MenuConfirmPressed"/>).
    /// 지금 칸 뒤에 금색 테두리를 깔고 숨 쉬듯 깜빡인다 — 어디를 보고 있는지 한눈에 보여야 한다.
    ///
    /// 칸은 줄(row)로 묶는다. 좌우는 같은 줄 안에서, 위아래는 줄을 건너 x가 가장 가까운 칸으로 간다 —
    /// 상점(카드 한 줄 + [닫기])과 선택지(한 칸이 한 줄)가 같은 규칙으로 돈다. 잠긴 칸은 건너뛴다(<see cref="MenuCursor"/>).
    ///
    /// <b>EventSystem의 선택은 쓰지 않는다</b> — 마우스로 누른 버튼이 선택된 채 남으면
    /// Enter 한 번이 그 버튼과 커서 칸을 둘 다 누른다. 마우스 클릭은 버튼의 onClick이 따로 받는다.
    /// </summary>
    public sealed class KeyMenu
    {
        private sealed class Item
        {
            public RectTransform rect;
            public int row;
            public Func<bool> enabled;
            public Action pick;
        }

        private static readonly Color FrameColor = new Color(1f, 0.86f, 0.45f);

        /// <summary>테두리가 칸 밖으로 내미는 두께.</summary>
        private const float Pad = 7f;

        private readonly List<Item> items = new List<Item>();
        private readonly Image frame;

        private int cursor = MenuCursor.None;

        /// <summary>창을 연 프레임. 이 프레임의 결정 키는 창을 연 키다 — 칸을 고르지 않는다.</summary>
        private int openedFrame = -1;

        /// <summary>커서가 옮겨 갔다(이전 칸, 지금 칸). 칸 모양(글자색 · 크기)을 바꾸려는 쪽이 받는다.</summary>
        public event Action<int, int> Moved;

        public int Cursor => cursor;

        public KeyMenu(Transform frameParent)
        {
            frame = UiKit.NewImage(frameParent, "Focus", FrameColor);
            frame.gameObject.SetActive(false);
        }

        /// <summary>칸 하나를 더한다. <paramref name="enabled"/>가 null이면 늘 고를 수 있다.</summary>
        public void Add(RectTransform rect, int row, Func<bool> enabled, Action pick)
            => items.Add(new Item { rect = rect, row = row, enabled = enabled, pick = pick });

        /// <summary>
        /// 창이 (다시) 떴다. 지금 칸이 아직 쓸 수 있으면 그대로, 아니면 쓸 수 있는 첫 칸에 둔다.
        /// 쥐고 있던 방향키와 이 프레임의 결정 키는 세지 않는다.
        /// </summary>
        public void Open()
        {
            openedFrame = Time.frameCount;

            PlayerInputController input = PlayerInputController.Instance;
            if (input != null) input.ResyncMenu();

            Refresh();
        }

        /// <summary>칸이 잠기거나 풀렸다(구매 등). 커서가 잠긴 칸에 남지 않게 한다.</summary>
        public void Refresh() => MoveTo(MenuCursor.Clamp(cursor, items.Count, IsEnabled));

        /// <summary>매 프레임 부른다 — 입력을 읽고 테두리를 깜빡인다.</summary>
        public void Tick()
        {
            ClearEventSystemSelection();
            Pulse();

            PlayerInputController input = PlayerInputController.Instance;
            if (input == null) return;

            // 위아래를 먼저 본다. 대각선으로 눌리면 줄을 옮기는 쪽이 같은 줄 안의 이동보다 우선이다.
            Vector2Int step = input.MenuStep;
            if (step.y != 0) MoveTo(Vertical(step.y > 0 ? -1 : 1));
            else if (step.x != 0) MoveTo(Horizontal(step.x));

            if (input.MenuConfirmPressed && Time.frameCount != openedFrame) Pick();
        }

        /// <summary>지금 칸을 고른다. 잠겨 있으면 아무 일도 없다.</summary>
        public void Pick()
        {
            if (IsEnabled(cursor)) items[cursor].pick?.Invoke();
        }

        // ── 이동 ────────────────────────────────────────

        private bool IsEnabled(int i)
            => i >= 0 && i < items.Count && (items[i].enabled == null || items[i].enabled());

        private int Horizontal(int dir)
        {
            if (cursor < 0) return MenuCursor.First(items.Count, IsEnabled);

            int row = items[cursor].row;
            return MenuCursor.Step(cursor, items.Count, dir, i => IsEnabled(i) && items[i].row == row);
        }

        /// <summary>
        /// 다음(+1) · 이전(−1) 줄에서 x가 가장 가까운 칸. 그 줄이 통째로 잠겼으면 한 줄 더 간다.
        /// 갈 줄이 없으면 제자리다(감싸지 않는다 — <see cref="MenuCursor"/>와 같다).
        /// </summary>
        private int Vertical(int dir)
        {
            if (cursor < 0) return MenuCursor.First(items.Count, IsEnabled);

            int row = items[cursor].row;
            float x = items[cursor].rect.anchoredPosition.x;

            int bestRow = int.MaxValue;
            int best = cursor;
            float bestDx = float.MaxValue;

            for (int i = 0; i < items.Count; i++)
            {
                if (!IsEnabled(i)) continue;

                int r = items[i].row;
                if (dir > 0 ? r <= row : r >= row) continue;

                int distance = Math.Abs(r - row);
                float dx = Mathf.Abs(items[i].rect.anchoredPosition.x - x);
                if (distance > bestRow || (distance == bestRow && dx >= bestDx)) continue;

                bestRow = distance;
                bestDx = dx;
                best = i;
            }

            return best;
        }

        private void MoveTo(int next)
        {
            int previous = cursor;
            cursor = next;

            PlaceFrame();
            if (previous != next) Moved?.Invoke(previous, next);
        }

        // ── 그리기 ──────────────────────────────────────

        /// <summary>테두리를 지금 칸 <b>바로 뒤</b>에 깐다 — 칸과 같은 부모, 한 칸 앞 형제라 칸보다 먼저 그려진다.</summary>
        private void PlaceFrame()
        {
            if (cursor < 0 || cursor >= items.Count || items[cursor].rect == null)
            {
                frame.gameObject.SetActive(false);
                return;
            }

            RectTransform target = items[cursor].rect;
            var rect = frame.rectTransform;

            rect.SetParent(target.parent, false);

            // 테두리가 이미 앞쪽 형제면 한 칸 당겨 꽂아야 칸 바로 앞에 선다(뒤로 꽂히면 칸을 덮는다).
            int at = target.GetSiblingIndex();
            rect.SetSiblingIndex(rect.GetSiblingIndex() < at ? at - 1 : at);

            rect.anchorMin = target.anchorMin;
            rect.anchorMax = target.anchorMax;
            rect.pivot = target.pivot;
            rect.anchoredPosition = target.anchoredPosition;
            rect.sizeDelta = target.sizeDelta + new Vector2(Pad * 2f, Pad * 2f);
            rect.localScale = target.localScale;

            frame.gameObject.SetActive(true);
        }

        private void Pulse()
        {
            if (!frame.gameObject.activeSelf) return;

            // 일시정지(timeScale 0)에도 깜빡여야 "여기"가 보인다.
            float a = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);
            frame.color = new Color(FrameColor.r, FrameColor.g, FrameColor.b, a);

            // 칸이 커지면(포커스 확대) 테두리도 따라간다.
            RectTransform target = cursor >= 0 && cursor < items.Count ? items[cursor].rect : null;
            if (target != null) frame.rectTransform.localScale = target.localScale;
        }

        private static void ClearEventSystemSelection()
        {
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null) events.SetSelectedGameObject(null);
        }
    }
}
