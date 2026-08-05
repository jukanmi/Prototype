using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype
{
    /// <summary>
    /// UI 가 붙기 전까지 쓰는 임시 조작기 + 화면 표시.
    /// OnGUI 로만 그리므로 프리팹이나 캔버스가 필요 없다.
    ///
    /// 불릿타임 중 흐름:
    ///   A / D       → 손패 커서 좌우 이동
    ///   J           → 커서 위 카드 결정 → 빈 슬롯에 배치
    ///                 (조준이 필요한 스킬이면 J를 한 번 더 눌러 확정)
    ///   마우스       → 조준 좌표 지정. 좌클릭도 확정, 우클릭은 취소
    ///   Backspace   → 마지막 슬롯 회수
    ///   Space       → 실행
    /// </summary>
    [RequireComponent(typeof(BulletTimeController))]
    public class DebugComboHUD : MonoBehaviour
    {
        [SerializeField] private BulletTimeController bulletTime;
        [SerializeField] private TargetSelector targetSelector;
        [SerializeField] private Player player;
        [SerializeField] private bool show = true;

        /// <summary>true면 손패/슬롯 입력(커서·확정·회수)만 건너뛴다. 정보 패널(조작법·게이지·파티 등)은 계속 그려진다.
        /// 다른 카드 배치 UI(ComboBoardUI 등)가 같은 손패/슬롯을 조작할 때 입력이 겹치지 않게 하는 용도.</summary>
        public bool SuppressCardInput;

        private int pickedHandIndex = -1;
        private int handCursor;
        private GUIStyle box;
        private GUIStyle label;
        private readonly StringBuilder sb = new StringBuilder();

        private void Awake()
        {
            if (bulletTime == null) bulletTime = GetComponent<BulletTimeController>();
            if (targetSelector == null) targetSelector = GetComponentInChildren<TargetSelector>();
            if (player == null) player = FindAnyObjectByType<Player>();
        }

        private void Update()
        {
            // Order 페이즈에서만 손패를 만진다. Freeze · Resolve 중 입력은 버린다.
            if (bulletTime.Tactic == null || !bulletTime.Tactic.AllowsCardEdit)
            {
                pickedHandIndex = -1;
                handCursor = 0;
                return;
            }

            if (SuppressCardInput)
            {
                pickedHandIndex = -1;
                handCursor = 0;
                return;
            }

            HandleCursor();
            HandleConfirm();
            HandleRecall();
        }

        private static string PhaseTag(TacticPhase p)
        {
            switch (p)
            {
                case TacticPhase.Freeze: return "<color=#00E5FF>Freeze</color>";
                case TacticPhase.Order: return "<color=#00E5FF>Order</color>";
                case TacticPhase.Resolve: return "<color=#8AFF80>Resolve</color>";
                default: return "RealTime";
            }
        }

        private PlayerControl Input => player != null ? player.GetComponent<PlayerControl>() : null;

        /// <summary>A / D — 손패 커서 이동. 카드를 집은 뒤에는 조준 중이므로 움직이지 않는다.</summary>
        private void HandleCursor()
        {
            int count = bulletTime.Hand.Count;
            if (count == 0) { handCursor = 0; return; }

            handCursor = Mathf.Clamp(handCursor, 0, count - 1);

            PlayerControl pc = Input;
            if (pc == null || pickedHandIndex >= 0) return;

            int delta = pc.HandCursorDelta;
            if (delta == 0) return;

            // 양끝에서 반대편으로 감는다. 손패가 짧아 왕복이 잦다.
            handCursor = (handCursor + delta + count) % count;
        }

        /// <summary>J — 결정. 집기 → (조준) → 배치 순으로 한 키가 이어진다.</summary>
        private void HandleConfirm()
        {
            PlayerControl pc = Input;
            bool confirm = pc != null && pc.ConfirmPressed;

            // 우클릭 취소는 조준 중에만 의미가 있다.
            if (pickedHandIndex >= 0 && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                targetSelector?.Cancel();
                pickedHandIndex = -1;
                return;
            }

            bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

            if (pickedHandIndex < 0)
            {
                if (!confirm) return;
                PickAtCursor();
                return;
            }

            if (confirm || click)
                PlacePicked();
        }

        /// <summary>커서 위 카드를 집는다. 조준이 필요 없으면 곧바로 배치한다.</summary>
        private void PickAtCursor()
        {
            ComboCard card = bulletTime.Hand.Get(handCursor);
            if (card == null || card.Data == null) return;

            pickedHandIndex = handCursor;
            targetSelector?.Begin(card.Data);

            if (card.Data.targeting == TargetingType.None)
                PlacePicked();
        }

        private void PlacePicked()
        {
            if (pickedHandIndex < 0) return;

            int slot = bulletTime.Board != null ? bulletTime.Board.FirstEmptyIndex() : -1;
            if (slot < 0)
            {
                BattleLog.Warn(LogCategory.Combo, "슬롯이 가득 찼다. Backspace로 회수할 것.", this);
                targetSelector?.Cancel();
                pickedHandIndex = -1;
                return;
            }

            TargetInfo info = targetSelector != null ? targetSelector.Confirm() : TargetInfo.None;
            bulletTime.PlaceFromHand(pickedHandIndex, slot, in info);
            pickedHandIndex = -1;

            // 배치하면 손패가 한 장 줄어든다. 커서가 범위를 벗어나지 않게 당긴다.
            handCursor = Mathf.Clamp(handCursor, 0, Mathf.Max(0, bulletTime.Hand.Count - 1));
        }

        private void HandleRecall()
        {
            if (Keyboard.current == null || !Keyboard.current.backspaceKey.wasPressedThisFrame) return;
            if (bulletTime.Board == null) return;

            for (int i = bulletTime.Board.SlotCount - 1; i >= 0; i--)
            {
                if (bulletTime.Board.Get(i).IsEmpty) continue;
                bulletTime.RecallToHand(i);
                return;
            }
        }

        private void OnGUI()
        {
            if (!show) return;

            EnsureStyles();

            DrawAimMarker();

            GUILayout.BeginArea(new Rect(10, 10, 460, 580), BuildText(), box);
            GUILayout.EndArea();
        }

        /// <summary>
        /// 조준점을 화면에 찍는다. 키보드로 조준하면 마우스 커서가 없어
        /// 어디를 겨누는지 볼 방법이 이것뿐이다.
        /// </summary>
        private void DrawAimMarker()
        {
            if (targetSelector == null || !targetSelector.IsSelecting) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // 논리 좌표를 그대로 찍으면 카메라가 안 기울어 있어 Z가 화면에서 사라진다.
            // 캐릭터와 같은 BeltScroll 변환을 거친 위치에 그린다.
            Vector3 sp = cam.WorldToScreenPoint(targetSelector.CursorViewPoint);
            if (sp.z < 0f) return;

            float y = Screen.height - sp.y;   // GUI는 좌상단 원점
            bool whiff = targetSelector.WillWhiff;
            Color c = whiff ? new Color(1f, 0.42f, 0.42f) : new Color(1f, 0.82f, 0.4f);

            // 십자선
            DrawRect(new Rect(sp.x - 12f, y - 1f, 24f, 2f), c);
            DrawRect(new Rect(sp.x - 1f, y - 12f, 2f, 24f), c);

            if (targetSelector.Current.targeting != TargetingType.GroundPoint) return;

            // 바닥의 원은 화면에서 타원이 된다 — 세로가 depthToScreen만큼 눌린다.
            float radius = targetSelector.Current.radius;
            Vector3 edge = cam.WorldToScreenPoint(
                targetSelector.CursorViewPoint + Vector3.right * radius);

            float rx = Mathf.Abs(edge.x - sp.x);
            float ry = rx * BeltScroll.DepthToScreen;

            DrawEllipse(sp.x, y, rx, ry, c);
        }

        /// <summary>점을 둘러 타원 테두리를 낸다. IMGUI에는 선 그리기 API가 없다.</summary>
        private static void DrawEllipse(float cx, float cy, float rx, float ry, Color c)
        {
            const int Dots = 40;

            for (int i = 0; i < Dots; i++)
            {
                float a = i / (float)Dots * Mathf.PI * 2f;
                float x = cx + Mathf.Cos(a) * rx;
                float y = cy + Mathf.Sin(a) * ry;

                DrawRect(new Rect(x - 1f, y - 1f, 2f, 2f), c);
            }
        }

        private static void DrawRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void EnsureStyles()
        {
            if (box != null) return;

            box = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                richText = true,
                padding = new RectOffset(10, 10, 10, 10),
                wordWrap = false,
            };
            label = new GUIStyle(GUI.skin.label) { richText = true };
        }

        private string BuildText()
        {
            sb.Clear();

            sb.AppendLine("<b>── 조작 ──</b>");
            sb.AppendLine("실시간: WASD 이동 / J 평타 / K 점프 / Shift 대쉬 / YUIOP 동료 고유기 / E 불릿타임");
            sb.AppendLine("불릿타임: A D 손패 커서 / J 결정 / WASD 조준 / J 조준확정 / 우클릭 취소 / Backspace 회수 / Space 실행");
            sb.AppendLine();

            float g = bulletTime.Gauge != null ? bulletTime.Gauge.Ratio : 0f;
            sb.AppendLine($"<b>게이지</b> {Bar(g)} {g * 100f:0}%   " +
                          $"<b>페이즈</b> {PhaseTag(bulletTime.Phase)}   " +
                          $"TimeScale {TimeControl.Scale:0.0}");
            sb.AppendLine($"<b>덱</b> {bulletTime.Deck.Count}   <b>손패</b> {bulletTime.Hand.Count}   <b>Discard</b> {bulletTime.Discard.Count}");
            sb.AppendLine();

            AppendHand();
            AppendSlots();
            AppendTargeting();
            AppendParty();
            AppendEnemies();

            return sb.ToString();
        }

        private void AppendHand()
        {
            sb.AppendLine("<b>── 손패 ──</b>  <color=#808080>A D 이동 · J 결정</color>");

            if (bulletTime.Hand.Count == 0)
            {
                sb.AppendLine("  (비어 있음 — E로 불릿타임 진입)");
                sb.AppendLine();
                return;
            }

            for (int i = 0; i < bulletTime.Hand.Count; i++)
            {
                ComboCard c = bulletTime.Hand.Get(i);
                string n = c?.Data != null ? c.Data.skillName : "?";
                string role = c?.Data != null ? c.Data.role.ToString() : "-";
                string type = c?.Data != null ? c.Data.attackType.ToString() : "-";

                bool onCursor = i == handCursor;
                bool aiming = i == pickedHandIndex;

                string cursor = onCursor ? "<color=#00E5FF>▶</color>" : " ";
                string line = $"{n}  <color=#808080>({role} · {type})</color>";
                if (onCursor) line = $"<b>{line}</b>";
                if (aiming) line += " <color=#FFD166>← 조준 중</color>";

                sb.AppendLine($"  {cursor} {line}");
            }

            sb.AppendLine();
        }

        private void AppendSlots()
        {
            if (bulletTime.Board == null) return;

            sb.AppendLine("<b>── 콤보 슬롯 ──</b>");
            var predicted = bulletTime.Predictor != null ? bulletTime.Predictor.Predicted : null;

            for (int i = 0; i < bulletTime.Board.SlotCount; i++)
            {
                ComboSlot s = bulletTime.Board.Get(i);
                if (s.IsEmpty)
                {
                    sb.AppendLine($"  {i}: <color=#606060>비어 있음</color>");
                    continue;
                }

                string pred = predicted != null && i < predicted.Count ? predicted[i].ToString() : "?";
                bool chained = bulletTime.Predictor != null && bulletTime.Predictor.IsChained(bulletTime.Board.Slots, i);
                string tag = chained ? "<color=#8AFF80>강화</color>" : "<color=#808080>기본</color>";

                sb.AppendLine($"  {i}: {s.Data.skillName} → <b>{pred}</b> [{tag}]  <color=#808080>{BattleLog.Name(s.caster)}</color>");
            }

            sb.AppendLine();
        }

        private void AppendTargeting()
        {
            if (targetSelector == null || !targetSelector.IsSelecting) return;

            sb.AppendLine("<b>── 조준 중 ──</b>  <color=#808080>WASD 이동 · J 확정 · 우클릭 취소</color>");
            sb.AppendLine($"  {targetSelector.Current.skillName} / {targetSelector.Current.targeting}");
            sb.AppendLine($"  좌표 {targetSelector.CursorPoint:F1}  반경 내 적 {targetSelector.EnemiesInRange}마리");

            if (targetSelector.Current.targeting == TargetingType.EnemyUnit)
                sb.AppendLine($"  대상 <b>{BattleLog.Name(targetSelector.HoveredUnit)}</b>" +
                              (targetSelector.HoveredUnit == null ? " <color=#FF6B6B>(조준점 근처에 적 없음)</color>" : ""));

            if (targetSelector.WillWhiff)
                sb.AppendLine("  <color=#FF6B6B>헛침 경고 — 반경 안에 적이 없다</color>");

            sb.AppendLine();
        }

        private void AppendParty()
        {
            if (player == null) return;

            sb.AppendLine("<b>── 파티 ──</b>");
            sb.AppendLine($"  {player.name}  HP {player.Combat.Health.CurValue:0}/{player.Combat.Health.MaxValue:0}  {player.Combat.CombatState}");

            for (int i = 0; i < player.Party.Length; i++)
            {
                Ally a = player.Party[i];
                if (a == null)
                {
                    sb.AppendLine($"  [{i + 1}] (없음)");
                    continue;
                }

                string cmd = a.IsCommanded ? " <color=#8AFF80>지휘중</color>" : "";
                sb.AppendLine($"  [{i + 1}] {a.name} <color=#808080>{a.Role}</color>  " +
                              $"HP {a.Combat.Health.CurValue:0}  {a.Combat.CombatState}{cmd}");
            }

            sb.AppendLine();
        }

        private void AppendEnemies()
        {
            sb.AppendLine($"<b>── 적 {BattleRegistry.AliveEnemyCount()}마리 ──</b>");

            var enemies = BattleRegistry.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Entity e = enemies[i];
                if (e == null) continue;

                var combat = e.Combat;
                string air = combat.AirHitCount > 0 ? $"  공중히트 {combat.AirHitCount}" : "";
                sb.AppendLine($"  {e.name}  HP {combat.Health.CurValue:0}  " +
                              $"<b>{combat.CombatState}</b> / {e.Physics.PhysicsState}  h={e.Physics.Height:0.0}{air}");
            }
        }

        private static string Bar(float ratio)
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(ratio) * 10f);
            return "<color=#00E5FF>" + new string('█', filled) + "</color>" +
                   "<color=#404040>" + new string('█', 10 - filled) + "</color>";
        }
    }
}
