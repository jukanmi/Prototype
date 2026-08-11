using System.Text;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 개발용 정보 패널 + 조준 마커. OnGUI로만 그리므로 프리팹이나 캔버스가 필요 없다.
    ///
    /// 손패 조작은 <see cref="ComboBoardUI"/>가 전담한다. 여기서는 입력을 받지 않고
    /// 덱 · 손패 · 파티 · 적 상태를 읽어서 보여 주기만 한다.
    /// </summary>
    [RequireComponent(typeof(BulletTimeController))]
    public class DebugComboHUD : MonoBehaviour
    {
        [SerializeField] private BulletTimeController bulletTime;
        [SerializeField] private TargetSelector targetSelector;
        [SerializeField] private Player player;
        [SerializeField] private TagSwapController swap;
        [SerializeField] private bool show = true;

        private GUIStyle box;
        private readonly StringBuilder sb = new StringBuilder();

        private void Awake()
        {
            if (bulletTime == null) bulletTime = GetComponent<BulletTimeController>();
            if (targetSelector == null) targetSelector = GetComponentInChildren<TargetSelector>();
            if (player == null) player = FindAnyObjectByType<Player>();
            if (swap == null) swap = FindAnyObjectByType<TagSwapController>();
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
        }

        private string BuildText()
        {
            sb.Clear();

            sb.AppendLine("<b>── 조작 ──</b>");
            sb.AppendLine("실시간: WASD 이동 / J 평타 / K 점프 / Shift 대쉬 / ZXCV 동료 고유기");
            sb.AppendLine("        <color=#FFD166>U 손패 맨 왼쪽 카드 사용</color> / E 불릿타임");
            sb.AppendLine("불릿타임: 카드 드래그 순서 교환 / 카드 클릭 조준 / WASD·마우스 조준 / 우클릭 취소");
            sb.AppendLine("        <color=#FFD166>E 또는 Space</color> 해제 → 왼쪽부터 순서대로 발동");
            sb.AppendLine();

            float g = bulletTime.Gauge != null ? bulletTime.Gauge.Ratio : 0f;
            sb.AppendLine($"<b>게이지</b> {Bar(g)} {g * 100f:0}%   " +
                          $"<b>페이즈</b> {PhaseTag(bulletTime.Phase)}   " +
                          $"TimeScale {TimeControl.Scale:0.0}");
            sb.AppendLine($"<b>덱</b> {bulletTime.Deck.Count}   <b>손패</b> {bulletTime.Hand.Count}   <b>Discard</b> {bulletTime.Discard.Count}");
            sb.AppendLine();

            AppendHand();
            AppendTargeting();
            AppendParty();
            AppendEnemies();

            return sb.ToString();
        }

        private void AppendHand()
        {
            sb.AppendLine("<b>── 손패 ──</b>  <color=#808080>왼쪽이 다음에 나갈 카드</color>");

            Hand hand = bulletTime.Hand;
            if (hand.Count == 0)
            {
                sb.AppendLine("  <color=#FF6B6B>(비어 있음 — 덱과 Discard를 확인할 것)</color>");
                sb.AppendLine();
                return;
            }

            var predicted = bulletTime.Predictor != null ? bulletTime.Predictor.Predicted : null;
            bool editing = bulletTime.AllowsCardEdit;

            for (int i = 0; i < hand.Count; i++)
            {
                ComboSlot s = hand.Get(i);
                SkillData d = s.Data;

                string head = i == 0 ? "<color=#00E5FF>U ▶</color>" : $"{i + 1}. ";

                if (d == null)
                {
                    sb.AppendLine($"  {head} <color=#FF6B6B>(빈 카드 — SkillData 미지정)</color>");
                    continue;
                }

                string line = $"{d.skillName}  <color=#808080>({d.role} · {d.attackType})</color>";

                if (s.aimed)
                    line += " <color=#FFD166>◉조준됨</color>";

                if (editing && predicted != null && i < predicted.Count)
                {
                    bool chained = bulletTime.Predictor.IsChained(hand.Slots, i);
                    string tag = chained ? "<color=#8AFF80>강화</color>" : "<color=#808080>기본</color>";
                    line += $"  → <b>{predicted[i]}</b> [{tag}]";
                }

                sb.AppendLine($"  {head} {line}");
            }

            sb.AppendLine();
        }

        private void AppendTargeting()
        {
            if (targetSelector == null || !targetSelector.IsSelecting) return;

            sb.AppendLine("<b>── 조준 중 ──</b>  <color=#808080>WASD·마우스 이동 · 좌클릭 확정 · 우클릭 취소</color>");
            sb.AppendLine($"  {targetSelector.Current.skillName} / {targetSelector.Current.targeting}");
            sb.AppendLine($"  좌표 {targetSelector.CursorPoint:F1}  반경 내 적 {targetSelector.EnemiesInRange}마리");

            if (targetSelector.WillWhiff)
                sb.AppendLine("  <color=#FF6B6B>헛침 경고 — 반경 안에 적이 없다</color>");

            sb.AppendLine();
        }

        private void AppendParty()
        {
            if (player == null) return;

            string cool = swap != null && swap.CooldownRemaining > 0f
                ? $" <color=#FFD166>쿨 {swap.CooldownRemaining:0.0}s</color>"
                : "";

            sb.AppendLine($"<b>── 로스터 ──</b>  <color=#808080>F 교대</color>{cool}");

            // 로스터는 플레이어가 0번, 동료가 1~4번이다. 태그 컨트롤러가 없으면
            // 파티 배열만으로 대신 그린다 — 씬 배선이 덜 된 상태에서도 HUD는 떠야 한다.
            int count = swap != null ? swap.Roster.Count : player.Party.Length + 1;

            for (int i = 0; i < count; i++)
            {
                Entity e = swap != null
                    ? swap.Roster[i]
                    : (i == 0 ? (Entity)player : player.Party[i - 1]);

                if (e == null)
                {
                    sb.AppendLine($"  [{i}] (없음)");
                    continue;
                }

                // 조작 중 · 벤치 · 불려 나옴(불릿타임)을 갈라 준다. 벤치에 앉은 몸은 꺼져 있어
                // 하이어라키를 안 보면 왜 안 싸우는지 알 수 없다.
                string stand = swap != null && swap.CurrentIndex == i
                    ? "<color=#00E5FF>▶</color>"
                    : e.gameObject.activeSelf ? "<color=#8AFF80>·</color>" : "<color=#808080>×</color>";

                string role = e is Ally ally ? $" <color=#808080>{ally.Role}</color>" : "";
                string cmd = e.IsCommanded ? " <color=#8AFF80>지휘중</color>" : "";

                sb.AppendLine($"  {stand} [{i}] {e.name}{role}  " +
                              $"HP {e.Combat.Health.CurValue:0}  {e.Combat.CombatState}{cmd}");
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
