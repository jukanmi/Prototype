using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype
{
    /// <summary>
    /// 입력을 읽는 <b>유일한</b> 곳. 다른 어떤 스크립트도 <c>Keyboard.current</c>·<c>Mouse.current</c>를
    /// 직접 만지지 않는다.
    ///
    /// 콜백이 아니라 <b>폴링</b>이다. <see cref="PlayerControl.Tick"/>은 Entity.Update가 부르고
    /// 지휘 입력은 <see cref="Player"/>가 그 뒤에 읽는데, 콜백으로 받으면 이 순서에 맞춰
    /// "이번 프레임에 눌렸다"를 직접 래치해야 한다. <c>WasPressedThisFrame</c>은 프레임 내내
    /// 같은 답을 주므로 누가 몇 번을 읽든 상관없다.
    ///
    /// <see cref="PlayerInput"/>은 자산 보유·디바이스·컨트롤 스킴 관리용으로만 쓴다.
    /// 액션은 반드시 <c>playerInput.actions</c>를 거쳐 잡는다 — 직렬화한 자산을 따로 들고 있으면
    /// 런타임 리바인드가 다른 인스턴스에 얹혀 화면과 실제 키가 어긋난다.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputController : MonoBehaviour
    {
        /// <summary>마우스가 "실제로 움직였다"고 볼 픽셀 이동량의 제곱.</summary>
        private const float AimPointMoveThresholdSqr = 0.01f;

        /// <summary>
        /// 플레이어 밖 스크립트(<see cref="TargetSelector"/>, 전투 UI)가 입력을 읽는 통로.
        /// </summary>
        public static PlayerInputController Instance { get; private set; }

        private PlayerInput playerInput;

        private InputActionMap gameplayMap;
        private InputActionMap bulletTimeMap;
        private InputActionMap uiMap;

        private InputAction moveAction;
        private InputAction attackAction;
        private InputAction jumpAction;
        private InputAction dashAction;
        private InputAction[] skillActions;
        private InputAction bulletTimeAction;
        private InputAction cardUseAction;

        private InputActionMap skillShotMap;

        private InputAction navigateAction;

        private InputAction aimAction;
        private InputAction aimPointAction;
        private InputAction aimDeltaAction;
        private InputAction aimConfirmAction;
        private InputAction aimCancelAction;

        private InputAction cancelAction;

        // Navigate 이산 스텝 — AimPoint와 같은 이유로 읽을 때 계산한다.
        private Vector2Int lastNavigate;
        private int navigateFrame = -1;
        private Vector2Int navigateStep;
        private bool navigateNeedsResync = true;

        /// <summary>리바인드·프리셋이 만지는 자산. 항상 PlayerInput이 들고 있는 그 인스턴스다.</summary>
        public InputActionAsset Actions => playerInput != null ? playerInput.actions : null;

        // ── Gameplay ────────────────────────────────────────

        /// <summary>이동 입력. x는 좌우, y는 깊이(월드 Z)로 읽는다.</summary>
        public Vector2 Move => moveAction.ReadValue<Vector2>();

        public bool AttackPressed => attackAction.WasPressedThisFrame();
        public bool JumpPressed => jumpAction.WasPressedThisFrame();
        public bool DashPressed => dashAction.WasPressedThisFrame();

        /// <summary>
        /// E · Space — 불릿타임 진입 · 실행 요청. 무엇을 할지는 전술 페이즈가 정한다.
        /// </summary>
        public bool BulletTimePressed => bulletTimeAction.WasPressedThisFrame();
        /// <summary>U — 손패 맨 왼쪽 카드 즉시 사용.</summary>
        public bool CardUsePressed => cardUseAction.WasPressedThisFrame();

        /// <summary>동료 고유기. 눌린 슬롯 0~3, 없으면 -1. 동시에 눌리면 낮은 번호가 이긴다.</summary>
        public int SkillPressed
        {
            get
            {
                for (int i = 0; i < skillActions.Length; i++)
                {
                    if (skillActions[i].WasPressedThisFrame()) return i;
                }
                return -1;
            }
        }

        // ── BulletTime — 손패 카드 조작 ──────────────────────

        /// <summary>
        /// 이번 프레임에 <b>새로 </b>눌린 방향. 안 눌렸으면 (0, 0).
        ///
        /// 카드 선택은 한 번 누르면 한 칸이어야 한다. 값을 그대로 읽으면 누르고 있는 동안
        /// 매 프레임 넘어가 손패 끝까지 순식간에 지나간다. 그래서 0에서 ±1로 <b>넘어가는
        /// 순간</b>만 잡는다.
        /// </summary>
        public Vector2Int NavigateStep
        {
            get
            {
                RefreshNavigate();
                return navigateStep;
            }
        }

        // ── BulletTimeSkillShot — 시전 위치 지정 ────────────

        /// <summary>키보드 조준 입력. x는 좌우, y는 깊이(월드 Z).</summary>
        public Vector2 Aim => aimAction.ReadValue<Vector2>();

        /// <summary>마우스 커서의 화면 좌표.</summary>
        public Vector2 AimPoint => aimPointAction.ReadValue<Vector2>();

        /// <summary>
        /// 이번 프레임에 마우스가 실제로 움직였는가. 키보드 조준보다 마우스를 우선할지 가른다.
        /// 안 움직였는데 마우스가 이기면 커서가 화면 한 점에 못 박힌다.
        ///
        /// <see cref="AimPoint"/>의 프레임 차분이 아니라 delta를 직접 읽는다 —
        /// 맵을 켠 첫 프레임에 position이 0을 뱉어서, 실제 좌표가 들어오는 다음 프레임이
        /// 화면 절반만큼의 이동으로 읽힌다. 그러면 카드 위에서 시작한 조준점이
        /// 손도 안 댄 마우스로 끌려간다.
        /// </summary>
        public bool AimPointMovedThisFrame
            => aimDeltaAction.ReadValue<Vector2>().sqrMagnitude > AimPointMoveThresholdSqr;

        public bool AimConfirmPressed => aimConfirmAction.WasPressedThisFrame();
        public bool AimCancelPressed => aimCancelAction.WasPressedThisFrame();

        /// <summary>
        /// 카드 조작 맵이 살아 있는가. Order 페이즈이면서 조준 중이 아닐 때만 켠다.
        /// <c>InputMapSwitcher</c>가 여닫는다.
        /// </summary>
        public bool BulletTimeMapEnabled
        {
            get => bulletTimeMap != null && bulletTimeMap.enabled;
            set
            {
                if (bulletTimeMap == null || bulletTimeMap.enabled == value) return;

                if (value) bulletTimeMap.Enable();
                else bulletTimeMap.Disable();

                // 다시 켠 첫 프레임은 기준값을 새로 잡고 넘어간다. 자세한 건 RefreshNavigate 참고.
                navigateNeedsResync = true;
                navigateFrame = -1;
                navigateStep = Vector2Int.zero;
            }
        }

        /// <summary>
        /// 시전 위치 지정 맵이 살아 있는가. <see cref="BulletTimeMapEnabled"/> 와
        /// <b>동시에 켜지면 안 된다</b> — J가 조준 확정과 카드 놓기를 한 프레임에 둘 다 한다.
        /// </summary>
        public bool SkillShotMapEnabled
        {
            get => skillShotMap != null && skillShotMap.enabled;
            set
            {
                if (skillShotMap == null || skillShotMap.enabled == value) return;

                if (value) skillShotMap.Enable();
                else skillShotMap.Disable();

                // 여기서 되돌릴 상태가 없다. AimPointMovedThisFrame이 delta를 그대로 읽으므로
                // 맵이 꺼져 있던 동안의 값이 다음 판단에 끼어들지 않는다.
            }
        }

        // ── UI ──────────────────────────────────────────────

        /// <summary>ESC — 창 닫기 · 뒤로.</summary>
        public bool CancelPressed => cancelAction.WasPressedThisFrame();

        /// <summary>
        /// 설정 화면 같은 모달이 떠 있는 동안 게임 조작을 잠근다.
        /// 안 잠그면 키 설정을 보는 내내 뒤에서 캐릭터가 움직이고 카드가 집힌다.
        /// UI 맵은 잠그지 않는다 — 창을 닫을 길까지 막힌다.
        /// </summary>
        public bool GameplaySuspended
        {
            get => gameplaySuspended;
            set
            {
                if (gameplaySuspended == value) return;
                gameplaySuspended = value;

                if (!isActiveAndEnabled) return;

                if (gameplaySuspended) gameplayMap?.Disable();
                else gameplayMap?.Enable();

                // 불릿타임 두 맵은 InputMapSwitcher가 다음 프레임에 맞춘다.
            }
        }

        private bool gameplaySuspended;

        // ── 수명 ────────────────────────────────────────────

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();

            if (playerInput.actions == null)
            {
                Debug.LogError($"{nameof(PlayerInputController)}: PlayerInput에 액션 자산이 비어 있다.", this);
                enabled = false;
                return;
            }

            ResolveActions();

            // 저장해 둔 키를 얹는다. 맵을 켜기 전에 해야 첫 프레임부터 바뀐 키가 먹는다.
            RebindManager.Load(playerInput.actions);

            if (Instance != null && Instance != this)
                Debug.LogWarning($"{nameof(PlayerInputController)}가 둘 이상이다. 마지막 것이 Instance가 된다.", this);

            Instance = this;
        }

        private void OnEnable()
        {
            // PlayerInput은 defaultActionMap 하나만 켜 준다. 나머지는 직접 켠다.
            // 불릿타임 두 맵은 InputMapSwitcher가 페이즈를 보고 열어 준다 —
            // 여기서 켜 두면 실시간 전투 중 J가 카드 집기로도 먹는다.
            if (!gameplaySuspended) gameplayMap?.Enable();
            uiMap?.Enable();
        }

        private void OnDisable()
        {
            // UI 맵은 건드리지 않는다 — InputSystemUIInputModule이 같은 맵을 쓰고 있어서
            // 여기서 끄면 플레이어가 죽는 순간 화면의 버튼이 전부 먹통이 된다.
            gameplayMap?.Disable();
            bulletTimeMap?.Disable();
            skillShotMap?.Disable();

            navigateNeedsResync = true;
            navigateFrame = -1;
            navigateStep = Vector2Int.zero;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 이름으로 액션을 잡아 캐싱한다. 자산에서 이름이 바뀌었으면 여기서 예외로 터진다 —
        /// 조용히 입력이 안 먹는 것보다 낫다.
        /// </summary>
        private void ResolveActions()
        {
            InputActionAsset actions = playerInput.actions;

            gameplayMap = actions.FindActionMap(InputActionNames.Gameplay.Map, throwIfNotFound: true);
            bulletTimeMap = actions.FindActionMap(InputActionNames.BulletTime.Map, throwIfNotFound: true);
            skillShotMap = actions.FindActionMap(InputActionNames.BulletTimeSkillShot.Map, throwIfNotFound: true);
            uiMap = actions.FindActionMap(InputActionNames.UI.Map, throwIfNotFound: true);

            moveAction = gameplayMap.FindAction(InputActionNames.Gameplay.Move, throwIfNotFound: true);
            attackAction = gameplayMap.FindAction(InputActionNames.Gameplay.Attack, throwIfNotFound: true);
            jumpAction = gameplayMap.FindAction(InputActionNames.Gameplay.Jump, throwIfNotFound: true);
            dashAction = gameplayMap.FindAction(InputActionNames.Gameplay.Dash, throwIfNotFound: true);
            bulletTimeAction = gameplayMap.FindAction(InputActionNames.Gameplay.BulletTime, throwIfNotFound: true);
            cardUseAction = gameplayMap.FindAction(InputActionNames.Gameplay.CardUse, throwIfNotFound: true);

            string[] names = InputActionNames.Gameplay.Skills;
            skillActions = new InputAction[names.Length];
            for (int i = 0; i < names.Length; i++)
                skillActions[i] = gameplayMap.FindAction(names[i], throwIfNotFound: true);

            navigateAction = bulletTimeMap.FindAction(InputActionNames.BulletTime.Navigate, throwIfNotFound: true);

            aimAction = skillShotMap.FindAction(InputActionNames.BulletTimeSkillShot.Aim, throwIfNotFound: true);
            aimPointAction = skillShotMap.FindAction(InputActionNames.BulletTimeSkillShot.AimPoint, throwIfNotFound: true);
            aimDeltaAction = skillShotMap.FindAction(InputActionNames.BulletTimeSkillShot.AimDelta, throwIfNotFound: true);
            aimConfirmAction = skillShotMap.FindAction(InputActionNames.BulletTimeSkillShot.Confirm, throwIfNotFound: true);
            aimCancelAction = skillShotMap.FindAction(InputActionNames.BulletTimeSkillShot.Cancel, throwIfNotFound: true);

            cancelAction = uiMap.FindAction(InputActionNames.UI.Cancel, throwIfNotFound: true);
        }

        /// <summary>
        /// 0에서 ±1로 넘어가는 순간만 잡는다. 대각선은 두 축이 함께 서므로
        /// 어느 쪽을 먼저 볼지는 읽는 쪽이 정한다.
        /// </summary>
        private void RefreshNavigate()
        {
            if (navigateFrame == Time.frameCount) return;
            navigateFrame = Time.frameCount;

            Vector2 raw = navigateAction.ReadValue<Vector2>();
            var now = new Vector2Int(Digital(raw.x), Digital(raw.y));

            if (navigateNeedsResync)
            {
                // 맵이 막 켜진 프레임. 이미 눌려 있던 키는 "새로 눌렸다"로 치지 않는다 —
                // 조준을 마치고 돌아오는 순간 W를 쥐고 있으면 곧바로 조준으로 되튄다.
                navigateNeedsResync = false;
                navigateStep = Vector2Int.zero;
                lastNavigate = now;
                return;
            }

            navigateStep = new Vector2Int(
                now.x != 0 && now.x != lastNavigate.x ? now.x : 0,
                now.y != 0 && now.y != lastNavigate.y ? now.y : 0);

            lastNavigate = now;
        }

        /// <summary>
        /// 아날로그 값을 -1 · 0 · 1로 접는다. 문턱을 0.5로 두는 이유는
        /// 정규화된 대각선(≈0.707)이 두 축 모두 서야 하기 때문이다.
        /// </summary>
        private static int Digital(float value)
        {
            const float Threshold = 0.5f;
            if (value > Threshold) return 1;
            if (value < -Threshold) return -1;
            return 0;
        }

    }
}
