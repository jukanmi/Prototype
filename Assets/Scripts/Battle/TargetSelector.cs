using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype
{
    /// <summary>
    /// 불릿타임 중 카드를 슬롯에 놓을 때의 조준(결정 로그 ④).
    /// 시간이 멈춰 있으므로 조준할 여유가 있다.
    ///
    /// 기본은 <b>WASD 키보드 조준</b>이다. 마우스를 실제로 움직이면 그 프레임부터 마우스가 잡는다.
    /// </summary>
    public class TargetSelector : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private float groundY = 0f;
        [SerializeField] private ComboPredictor predictor;

        [Header("키보드 조준")]
        [Tooltip("WASD로 조준점이 움직이는 속도(units/s).")]
        [SerializeField] private float cursorSpeed = 9f;
        [Tooltip("EnemyUnit 조준에서 조준점이 적을 붙잡는 거리.")]
        [SerializeField] private float snapRadius = 1.5f;
        [Tooltip("조준을 시작할 때 커서가 놓이는 기준. 비우면 Player를 찾는다.")]
        [SerializeField] private Transform cursorOrigin;

        private SkillData current;

        public bool IsSelecting => current != null;
        public SkillData Current => current;

        /// <summary>커서가 가리키는 <b>논리</b> 바닥 좌표(XZ). 스킬이 실제로 쓰는 값.</summary>
        public Vector3 CursorPoint { get; private set; }

        /// <summary>
        /// 커서를 <b>화면에 그릴</b> 위치. 카메라가 기울어 있지 않아 Z를 세로로 접어야 보인다.
        /// 캐릭터와 같은 <see cref="BeltScroll"/> 변환을 거친다.
        /// </summary>
        public Vector3 CursorViewPoint => BeltScroll.ToView(CursorPoint);
        /// <summary>커서가 겹친 적. EnemyUnit 조준용.</summary>
        public Entity HoveredUnit { get; private set; }
        /// <summary>반경 안에 걸리는 적 수. 0이면 헛침 경고를 띄운다.</summary>
        public int EnemiesInRange { get; private set; }
        public bool WillWhiff => IsSelecting && current.targeting == TargetingType.GroundPoint && EnemiesInRange == 0;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (predictor == null) predictor = FindAnyObjectByType<ComboPredictor>();

            if (cursorOrigin == null)
            {
                Player p = FindAnyObjectByType<Player>();
                if (p != null) cursorOrigin = p.transform;
            }
        }

        /// <summary>카드를 집었을 때 시작한다. targeting == None이면 곧바로 확정 가능하다.</summary>
        public void Begin(SkillData data)
        {
            current = data;
            HoveredUnit = null;
            EnemiesInRange = 0;

            // 커서를 매번 원점에서 시작하면 멀리서부터 끌고 와야 한다.
            // 가까운 적이 있으면 거기서, 없으면 시전 기준점에서 시작한다.
            Entity near = cursorOrigin != null ? BattleRegistry.NearestEnemy(cursorOrigin.position) : null;
            Vector3 start = near != null ? near.transform.position
                          : (cursorOrigin != null ? cursorOrigin.position : Vector3.zero);
            start.y = groundY;
            CursorPoint = start;

            BattleLog.Log(LogCategory.Predict,
                $"조준 시작: {(data != null ? data.skillName : "(null)")} | 방식 {(data != null ? data.targeting.ToString() : "?")}", this);
        }

        public void Cancel()
        {
            current = null;
            HoveredUnit = null;
            EnemiesInRange = 0;
        }

        /// <summary>지금까지 찍은 값으로 TargetInfo를 만든다. 확정 후 선택 상태는 해제된다.</summary>
        public TargetInfo Confirm()
        {
            if (current == null) return TargetInfo.None;

            TargetInfo info;
            switch (current.targeting)
            {
                case TargetingType.GroundPoint:
                    // 적을 지정하지 않는다. 찍은 좌표 하나만 받는다.
                    // (탱커 모으기 = 그 지점으로 텔포 → 주변을 자기 쪽으로 흡입)
                    info = TargetInfo.Ground(CursorPoint);
                    break;

                case TargetingType.EnemyUnit:
                    info = TargetInfo.Unit(HoveredUnit);
                    break;

                case TargetingType.Direction:
                    // 방향은 시전 기준점에서 조준점을 향한다. CombatManager 원점이 아니다.
                    Vector3 from = cursorOrigin != null ? cursorOrigin.position : transform.position;
                    info = TargetInfo.Dir(CursorPoint - from);
                    break;

                default:
                    info = TargetInfo.None;
                    break;
            }

            BattleLog.Log(LogCategory.Predict,
                $"조준 확정: {current.skillName} | {info.type} | point {info.point} | unit {BattleLog.Name(info.unit)}", this);

            if (WillWhiff)
                BattleLog.Warn(LogCategory.Predict,
                    $"{current.skillName} 반경 {current.radius:0.#} 안에 적 0마리 — 헛침 예상", this);

            current = null;
            return info;
        }

        private void Update()
        {
            if (current == null) return;

            // 시간이 멈춰 있어도 조준은 돌아야 한다.
            UpdateCursor();

            if (current.targeting == TargetingType.GroundPoint && predictor != null)
                EnemiesInRange = predictor.CountEnemiesInRadius(CursorPoint, current.radius);
        }

        private void UpdateCursor()
        {
            // 마우스를 실제로 움직인 프레임에만 마우스가 우선한다. 그 외에는 키보드.
            if (!MoveByMouse())
                MoveByKeyboard();

            HoveredUnit = null;
            if (current.targeting != TargetingType.EnemyUnit) return;

            // 조준점에서 가장 가까운 적을 붙잡는다. 키보드로도 유닛 지정이 되게 하는 장치.
            Entity nearest = BattleRegistry.NearestEnemy(CursorPoint);
            if (nearest == null) return;

            Vector3 d = nearest.transform.position - CursorPoint;
            d.y = 0f;
            if (d.magnitude <= snapRadius) HoveredUnit = nearest;
        }

        /// <summary>
        /// 마우스가 움직였으면 그 화면 위치를 논리 바닥 좌표로 되돌린다.
        /// 카메라가 기울어 있지 않아 바닥 평면이 화면에서 선으로 눌리므로,
        /// 평면 레이캐스트가 아니라 <see cref="BeltScroll.ToGround"/> 역변환을 쓴다.
        /// </summary>
        private bool MoveByMouse()
        {
            if (cam == null || Mouse.current == null) return false;
            if (Mouse.current.delta.ReadValue().sqrMagnitude <= 0.01f) return false;

            Vector3 screen = Mouse.current.position.ReadValue();
            screen.z = Mathf.Abs(cam.transform.position.z);   // 직교 카메라라 깊이는 아무 값이나 무방

            Vector3 view = cam.ScreenToWorldPoint(screen);
            CursorPoint = BeltScroll.ToGround(view, groundY);
            return true;
        }

        /// <summary>WASD로 조준점을 민다. 시간이 멈춰 있으므로 unscaled로 돌린다.</summary>
        private void MoveByKeyboard()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);

            var dir = new Vector3(x, 0f, z);
            if (dir.sqrMagnitude <= 0.0001f) return;

            Vector3 p = CursorPoint + dir.normalized * cursorSpeed * TimeControl.UnscaledDeltaTime;
            p.y = groundY;
            CursorPoint = p;
        }

        private void OnDrawGizmosSelected()
        {
            if (current == null || current.targeting != TargetingType.GroundPoint) return;

            // 논리 위치(실제 판정)와 그리는 위치(화면)를 둘 다 보여 준다.
            Gizmos.color = WillWhiff ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(CursorPoint, current.radius);

            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireSphere(CursorViewPoint, 0.2f);
        }
    }
}
