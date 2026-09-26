using UnityEngine;
using UnityEngine.EventSystems;

namespace Prototype
{
    /// <summary>
    /// 손패 판의 <b>조작</b> — 키보드 세 상태(커서 · 집기 · 조준)와 마우스 드래그.
    /// 그리기 · 배치 · 애니메이션은 ComboBoardUI.cs 에 있다.
    ///
    /// <code>
    /// Browse ─↑→ Grabbed ─↑→ Aiming ─확정→ Browse
    ///            Grabbed ─↓→ Browse          Aiming ─취소→ Grabbed(집고 있었으면) / Browse
    /// 드래그로 손패 밖에 놓기 ─────────────→ Aiming
    /// </code>
    /// </summary>
    public partial class ComboBoardUI
    {
        private enum BoardMode { Browse, Grabbed, Aiming }

        /// <summary>
        /// 지금 키보드 입력을 받는 상태. <b>따로 저장하지 않고 인덱스 둘에서 읽는다</b> —
        /// 조준을 취소하면 집고 있던 카드로 돌아가야 하므로, 조준 중에도 집은 인덱스가 살아 있다.
        /// 상태 하나로 접으면 그 "돌아갈 자리"를 따로 들고 다녀야 한다.
        /// </summary>
        private BoardMode Mode
            => _aimingIndex >= 0 ? BoardMode.Aiming
             : _grabbedIndex >= 0 ? BoardMode.Grabbed
             : BoardMode.Browse;

        /// <summary>
        /// 키보드 카드 조작. 세 상태가 <b>배타적</b>으로 하나만 돈다 —
        /// 조준 확정(J)과 카드 놓기(J)가 기본값이 같아서, 한 프레임에 두 갈래가 돌면
        /// 한 번 누른 J가 조준을 확정하고 그 카드를 놓는 것까지 해 버린다.
        /// </summary>
        private void HandleInput()
        {
            // 불릿타임이 풀렸으면 조준도 집기도 같이 접는다.
            if (!_bulletTime.AllowsCardEdit)
            {
                if (_aimingIndex >= 0 || _grabbedIndex >= 0)
                {
                    CancelAiming();
                    ReleaseGrab();
                    RefreshUI();
                }
                return;
            }

            PlayerInputController input = PlayerInputController.Instance;
            if (input == null) return;

            switch (Mode)
            {
                case BoardMode.Aiming: HandleAimingInput(input); break;
                case BoardMode.Grabbed: HandleGrabbedInput(input); break;
                default: HandleBrowseInput(input); break;
            }
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

            // 위 · 아래를 먼저 본다. 대각선으로 눌리면 두 축이 함께 서는데,
            // 상태를 옮기는 쪽이 순서 변경보다 우선이다.
            if (step.y > 0)
            {
                BeginAiming(_grabbedIndex);
                return;
            }

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

        // ── 상태 3. 시전 위치 지정 ───────────────────────

        private void HandleAimingInput(PlayerInputController input)
        {
            if (input.AimCancelPressed)
            {
                CancelAiming();
                RefreshUI();
                return;
            }

            // "UI 위 클릭은 확정으로 안 친다"는 판정은 PlayerInputController가 한다 —
            // 여기서 커서 위치만 보면 마우스를 손패 위에 올려 둔 채 키보드로 확정하는 것까지 막힌다.
            if (!input.AimConfirmPressed) return;

            TargetInfo info = targetSelector != null ? targetSelector.Confirm() : TargetInfo.None;

            // 인덱스를 먼저 비운다 — SetHandTarget이 OnChanged로 RefreshUI를 부르므로
            // 그 시점에 이미 조준이 끝난 상태로 보여야 한다.
            int idx = _aimingIndex;
            _aimingIndex = -1;

            // 위치까지 찍었으면 그 카드는 볼일이 끝났다. 집은 채로 돌아가면
            // 방금 확정한 카드를 다시 놓아 줘야 다음 카드로 넘어갈 수 있다.
            // 반면 취소(K)는 집은 상태를 남긴다 — 조준만 무르고 다시 겨냥할 수 있어야 한다.
            _cursorIndex = idx;
            ReleaseGrab();

            _bulletTime.SetHandTarget(idx, in info);
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
            CancelAiming();
            ReleaseGrab();
            RefreshUI();
        }

        // ── 조작 ─────────────────────────────────────────

        private void HandleSwap(int from, int to)
        {
            if (!CanEditNow) return;
            _bulletTime.SwapHand(from, to);
        }

        /// <summary>
        /// 뗀 자리가 손패 판 안인지. 화면좌표로 본다 —
        /// 끌려 나간 카드의 RectTransform이 아니라 <b>커서 위치</b>가 기준이다.
        /// </summary>
        private bool IsInsideHandPanel(PointerEventData eventData)
        {
            if (_handPanelRect == null) return true;

            return RectTransformUtility.RectangleContainsScreenPoint(
                _handPanelRect, eventData.position, eventData.pressEventCamera);
        }

        // 카드를 손패 밖으로 꺼냄 → 조준이 필요한 스킬이면 조준 모드로 들어간다.
        private void HandleCardPulledOut(int index)
        {
            if (!CanEditNow) return;
            BeginAiming(index);
        }

        /// <summary>
        /// 시전 위치 지정으로 들어간다. 드래그로 꺼냈을 때와 집은 카드에서 위를 눌렀을 때
        /// 같은 길을 타야 한다 — 두 경로가 갈리면 조준 상태가 반쪽만 서는 조합이 생긴다.
        /// </summary>
        private bool BeginAiming(int index)
        {
            if (index < 0 || index >= Hand.Size) return false;

            SkillData data = _bulletTime.Hand.GetData(index);
            if (data == null) return false;

            if (data.targeting == TargetingType.None)
            {
                BattleLog.Log(LogCategory.Predict, $"{data.skillName} — 조준이 필요 없는 스킬", this);
                return false;
            }

            if (targetSelector != null)
            {
                // 그 카드 바로 위에서 시작한다. 기본값(가장 가까운 적)은 벨트스크롤 투영 탓에
                // 방 안쪽 적이 화면 우측 상단으로 밀려 올라가 늘 같은 구석에서 시작하는 것처럼 보인다.
                if (TryGetAimStartScreen(index, out Vector2 screen))
                    targetSelector.Begin(data, targetSelector.ScreenToGround(screen));
                else
                    targetSelector.Begin(data);
            }

            _aimingIndex = index;
            RefreshUI();
            return true;
        }

        /// <summary>
        /// 조준 시작점의 화면 좌표 — 카드 위쪽 모서리 중앙에서 조금 더 위.
        /// 캔버스가 ScreenSpaceOverlay라 RectTransform의 월드 코너가 곧 화면 픽셀이다.
        /// </summary>
        private bool TryGetAimStartScreen(int index, out Vector2 screen)
        {
            screen = default;

            if (_cards == null || index < 0 || index >= _cards.Length) return false;

            CardWidgets w = _cards[index];
            if (w == null || w.root == null || !w.root.activeInHierarchy) return false;

            var rect = w.root.transform as RectTransform;
            if (rect == null) return false;

            rect.GetWorldCorners(_corners);

            Vector2 bottomLeft = _corners[0];
            Vector2 topLeft = _corners[1];
            Vector2 topRight = _corners[2];

            float height = topLeft.y - bottomLeft.y;
            screen = (topLeft + topRight) * 0.5f + Vector2.up * (height * AimStartGap);
            return true;
        }

        private void CancelAiming()
        {
            if (_aimingIndex < 0) return;

            targetSelector?.Cancel();
            _aimingIndex = -1;
        }
    }
}
