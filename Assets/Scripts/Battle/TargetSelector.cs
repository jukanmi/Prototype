using UnityEngine;

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
        [Tooltip("조준을 시작할 때 커서가 놓이는 기준. 비우면 Player를 찾는다.")]
        [SerializeField] private Transform cursorOrigin;

        /// <summary>
        /// 태그로 조작 대상이 바뀌면 조준 기준도 따라가야 한다.
        /// 안 그러면 벤치에 앉은 몸의 좌표에서 커서가 시작하고,
        /// <c>Direction</c> 스킬은 화면에 없는 몸을 기준으로 방향을 잡는다.
        /// </summary>
        public void SetCursorOrigin(Transform t)
        {
            if (t != null) cursorOrigin = t;
        }

        private SkillData current;

        /// <summary>조준 기준이 되는 몸. 근접 스킬 프리뷰가 "어디서 시전하는가"를 이걸로 잡는다.</summary>
        public Transform CursorOrigin => cursorOrigin;

        public bool IsSelecting => current != null;
        public SkillData Current => current;

        /// <summary>커서가 가리키는 <b>논리</b> 바닥 좌표(XZ). 스킬이 실제로 쓰는 값.</summary>
        public Vector3 CursorPoint { get; private set; }

        /// <summary>
        /// 커서를 <b>화면에 그릴</b> 위치. 카메라가 기울어 있지 않아 Z를 세로로 접어야 보인다.
        /// 캐릭터와 같은 <see cref="BeltScroll"/> 변환을 거친다.
        /// </summary>
        public Vector3 CursorViewPoint => BeltScroll.ToView(CursorPoint);
        /// <summary>반경 안에 걸리는 적 수. 0이면 헛침 경고를 띄운다.</summary>
        public int EnemiesInRange { get; private set; }
        /// <summary>
        /// 헛침 경고. radius로 때리는 스킬에만 뜬다 —
        /// 근접은 시전자 히트박스로 때리므로 반경 안이 비어도 멀쩡히 맞는다.
        /// </summary>
        public bool WillWhiff => IsSelecting && current.UsesRadius && EnemiesInRange == 0;

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

        /// <summary>
        /// 카드를 집었을 때 시작한다. targeting == None이면 곧바로 확정 가능하다.
        ///
        /// 시작점을 안 주면 가까운 적에서 시작한다. 다만 벨트스크롤 투영이 깊은 z를
        /// 화면 위 · 오른쪽으로 밀어 올리므로(<see cref="BeltScroll.ToView"/>),
        /// 방 안쪽 적에서 시작하면 커서가 늘 우측 상단에 뜬 것처럼 보인다.
        /// 화면 기준으로 잡고 싶으면 <see cref="ScreenToGround"/>를 거쳐 넘겨라.
        /// </summary>
        public void Begin(SkillData data) => Begin(data, PickStart(data));

        /// <summary>시작점을 지정해 조준을 연다.</summary>
        public void Begin(SkillData data, Vector3 startGround)
        {
            current = data;
            EnemiesInRange = 0;

            startGround.y = groundY;
            CursorPoint = startGround;

            BattleLog.Log(LogCategory.Predict,
                $"조준 시작: {(data != null ? data.skillName : "(null)")} | 방식 {(data != null ? data.targeting.ToString() : "?")} | 시작 {CursorPoint:F1}", this);
        }

        /// <summary>
        /// 커서를 매번 원점에서 시작하면 멀리서부터 끌고 와야 한다. 스킬이 자동으로 고를 적 위에 올려 둔다 —
        /// 그냥 확정하면 자동 조준과 <b>같은 결과</b>가 나오도록(<see cref="SkillData.targetPick"/>).
        /// </summary>
        private Vector3 PickStart(SkillData data)
        {
            if (cursorOrigin == null) return Vector3.zero;

            TargetPick pick = data != null ? data.targetPick : TargetPick.Nearest;
            Entity picked = BattleRegistry.PickEnemy(cursorOrigin.position, pick);

            return picked != null ? picked.transform.position : cursorOrigin.position;
        }

        /// <summary>
        /// 화면 좌표 → 논리 바닥 좌표. 마우스 피킹과 조준 시작점이 같은 길을 타야
        /// 시작 위치와 마우스로 집은 위치가 어긋나지 않는다.
        ///
        /// 바닥 평면과의 <b>레이 교차</b>로 푼다. 예전에는 <c>ScreenToWorldPoint</c> 한 점을
        /// <see cref="BeltScroll.ToGround"/>로 되돌렸는데, 그건 카메라가 기울지 않은 직교
        /// 투영이라 화면 한 점이 곧 월드 한 점이던 시절의 방법이다. 기울어진 원근
        /// 카메라에서는 화면 한 점이 <b>광선</b>이라 그 식이 성립하지 않는다.
        /// </summary>
        public Vector3 ScreenToGround(Vector2 screenPos)
        {
            if (cam == null) return Vector3.zero;

            Ray ray = cam.ScreenPointToRay(screenPos);
            var ground = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));

            // 지면과 평행하게 쏘면(카메라가 눕는 경우) 교차가 없다. 그때는 조준을 포기한다.
            return ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;
        }

        public void Cancel()
        {
            current = null;
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
                    // 실제 상대는 시전 순간 이 좌표에서 다시 뽑힌다(SkillState.ResolveTarget).
                    info = TargetInfo.Ground(CursorPoint);
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
                $"조준 확정: {current.skillName} | {info.type} | point {info.point}", this);

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

            if (current.UsesRadius && predictor != null)
                EnemiesInRange = predictor.CountEnemiesInRadius(CursorPoint, current.radius);
        }

        private void UpdateCursor()
        {
            // 마우스를 실제로 움직인 프레임에만 마우스가 우선한다. 그 외에는 키보드.
            if (!MoveByMouse())
                MoveByKeyboard();
        }

        /// <summary>
        /// 마우스가 움직였으면 그 화면 위치를 논리 바닥 좌표로 되돌린다.
        /// 카메라가 기울어 있지 않아 바닥 평면이 화면에서 선으로 눌리므로,
        /// 평면 레이캐스트가 아니라 <see cref="BeltScroll.ToGround"/> 역변환을 쓴다.
        /// </summary>
        private bool MoveByMouse()
        {
            PlayerInputController input = PlayerInputController.Instance;
            if (cam == null || input == null) return false;
            if (!input.AimPointMovedThisFrame) return false;

            CursorPoint = ScreenToGround(input.AimPoint);
            return true;
        }

        /// <summary>Aim(기본 WASD)으로 조준점을 민다. 시간이 멈춰 있으므로 unscaled로 돌린다.</summary>
        private void MoveByKeyboard()
        {
            PlayerInputController input = PlayerInputController.Instance;
            if (input == null) return;

            Vector2 aim = input.Aim;

            var dir = new Vector3(aim.x, 0f, aim.y);
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
