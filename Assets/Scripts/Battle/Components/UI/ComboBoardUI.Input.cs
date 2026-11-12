using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 손패 판의 <b>조작</b> — 키보드 두 상태(커서 · 집기)와 마우스 드래그.
    /// 그리기 · 배치 · 애니메이션은 ComboBoardUI.cs 에 있다.
    ///
    /// <code>
    /// Browse ─↑→ Grabbed ─↓→ Browse
    /// </code>
    /// </summary>
    public partial class ComboBoardUI
    {
        /// <summary>키보드 카드 조작. 집은 카드가 있으면 집기 상태, 없으면 커서 상태.</summary>
        private void HandleInput()
        {
            // 불릿타임이 풀렸으면 집기도 같이 접는다.
            if (!_bulletTime.AllowsCardEdit)
            {
                if (_grabbedIndex >= 0)
                {
                    ReleaseGrab();
                    RefreshUI();
                }
                return;
            }

            PlayerInputController input = PlayerInputController.Instance;
            if (input == null) return;

            if (_grabbedIndex >= 0) HandleGrabbedInput(input);
            else HandleBrowseInput(input);
        }

        // ── 상태 1. 커서 이동 ────────────────────────────

        private void HandleBrowseInput(PlayerInputController input)
        {
            Vector2Int step = input.NavigateStep;

            // 위를 먼저 본다. 대각선으로 눌리면 두 축이 함께 선다.
            if (step.y > 0)
            {
                Grab(_cursorIndex);
                return;
            }

            if (step.x == 0) return;

            int next = NextOccupied(_cursorIndex, step.x);
            if (next == _cursorIndex) return;

            _cursorIndex = next;
            RefreshUI();
        }

        // ── 상태 2. 카드를 집은 상태 ─────────────────────

        private void HandleGrabbedInput(PlayerInputController input)
        {
            Vector2Int step = input.NavigateStep;

            // 아래를 먼저 본다. 대각선으로 눌리면 두 축이 함께 서는데,
            // 상태를 옮기는 쪽이 순서 변경보다 우선이다.
            if (step.y < 0)
            {
                ReleaseGrab();
                RefreshUI();
                return;
            }

            if (step.x != 0) MoveGrabbed(step.x);
        }

        /// <summary>집은 카드를 한 칸 민다. 손패 밖이나 빈자리로는 못 민다.</summary>
        private void MoveGrabbed(int dx)
        {
            int to = _grabbedIndex + dx;
            if (to < 0 || to >= Hand.Size) return;
            if (_bulletTime.Hand.Get(to).IsEmpty) return;

            // HandleSwap이 아니라 직접 부른다 — CanEditNow가 집은 상태를 막고 있다.
            if (!_bulletTime.SwapHand(_grabbedIndex, to)) return;

            _grabbedIndex = to;
            _cursorIndex = to;
            RefreshUI();
        }

        // ── 집기 · 놓기 ──────────────────────────────────

        private void Grab(int index)
        {
            if (index < 0 || index >= Hand.Size) return;
            if (_bulletTime.Hand.Get(index).IsEmpty) return;

            _grabbedIndex = index;
            RefreshUI();
        }

        /// <summary>
        /// 집기를 푼다. 바꾼 순서는 그대로 확정된다.
        /// 되돌리기는 없다 — 잘못 옮겼으면 다시 집어서 되밀면 된다.
        /// </summary>
        private void ReleaseGrab()
        {
            _grabbedIndex = -1;
        }

        /// <summary>커서에서 dx 방향으로 가장 가까운 카드 자리. 없으면 제자리를 돌려준다.</summary>
        private int NextOccupied(int from, int dx)
        {
            for (int i = from + dx; i >= 0 && i < Hand.Size; i += dx)
            {
                if (!_bulletTime.Hand.Get(i).IsEmpty) return i;
            }
            return from;
        }

        /// <summary>손패가 바뀌어 커서가 빈자리를 짚고 있으면 가장 왼쪽 카드로 되돌린다.</summary>
        private void EnsureCursorValid()
        {
            if (_cursorIndex >= 0 && _cursorIndex < Hand.Size &&
                !_bulletTime.Hand.Get(_cursorIndex).IsEmpty)
                return;

            _cursorIndex = -1;
            for (int i = 0; i < Hand.Size; i++)
            {
                if (_bulletTime.Hand.Get(i).IsEmpty) continue;
                _cursorIndex = i;
                return;
            }
        }

        private void HandleEnter()
        {
            // 불릿타임에 들어올 때마다 커서를 맨 왼쪽 카드에서 시작한다.
            _cursorIndex = -1;
            EnsureCursorValid();
            RefreshUI();
        }

        private void HandleExit()
        {
            ReleaseGrab();
            RefreshUI();
        }

        // ── 조작 ─────────────────────────────────────────

        private void HandleSwap(int from, int to)
        {
            if (!CanEditNow) return;
            _bulletTime.SwapHand(from, to);
        }
    }
}
