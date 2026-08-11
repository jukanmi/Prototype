using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype.EditorTools
{
    /// <summary>
    /// <c>InputSystem_Actions.inputactions</c>를 코드로 다시 찍어낸다.
    ///
    /// 왜 스크립트인가 — 이 파일을 에디터 밖에서 직접 고치면
    /// 유니티가 메모리에 들고 있던 예전 내용을 나중에 그대로 디스크에 덮어쓴다.
    /// 유니티 안에서 만들고 곧바로 임포트까지 시켜야 살아남는다.
    ///
    /// <b>테스트는 이걸 부르지 않는다.</b> 인스펙터에서 손으로 고친 바인딩이
    /// 테스트 돌릴 때마다 날아가면 안 되기 때문이다. 형태 검증은
    /// <c>InputActionAssetTests</c>가 자산을 읽어서 따로 한다.
    ///
    /// 몇 번을 돌려도 같은 경로를 갱신할 뿐이다. 다만 기존 바인딩 수정은 전부 사라진다.
    /// </summary>
    public static class InputActionsBuilder
    {
        private const string AssetPath = "Assets/Settings/InputSystem_Actions.inputactions";

        private const string KeyboardMouse = "Keyboard&Mouse";
        private const string Gamepad = "Gamepad";

        [MenuItem("Prototype/입력 - 액션 자산 다시 만들기")]
        public static void Build()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = Path.GetFileNameWithoutExtension(AssetPath);

            BuildGameplayMap(asset);
            BuildBulletTimeMap(asset);
            BuildSkillShotMap(asset);
            BuildUIMap(asset);

            asset.AddControlScheme(KeyboardMouse)
                .WithRequiredDevice("<Keyboard>")
                .WithRequiredDevice("<Mouse>")
                .Done();

            asset.AddControlScheme(Gamepad)
                .WithRequiredDevice("<Gamepad>")
                .Done();

            string json = asset.ToJson();
            Object.DestroyImmediate(asset);

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            File.WriteAllText(AssetPath, json);

            // 쓰자마자 임포트해서 유니티의 메모리 사본을 새 내용으로 맞춘다.
            // 이걸 빼면 다음 저장 때 예전 사본이 파일을 도로 덮는다.
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            Debug.Log($"[InputActionsBuilder] {AssetPath} 재생성 완료 — Gameplay · BulletTime · UI");
        }

        /// <summary>
        /// 전투 중 항상 켜져 있는 맵. 지휘키(BulletTime · Execute · CardUse)도 여기 둔다 —
        /// PlayerInput이 defaultActionMap 하나만 자동으로 켜기 때문이다.
        /// "정지 중 이동 · 평타 차단"은 맵 전환이 아니라 PlayerControl의 IsFrozen 게이트가 한다.
        /// </summary>
        private static void BuildGameplayMap(InputActionAsset asset)
        {
            InputActionMap map = asset.AddActionMap(InputActionNames.Gameplay.Map);

            // 벨트스크롤 — x는 좌우, y는 깊이(월드 Z)로 읽는다.
            InputAction move = map.AddAction(
                InputActionNames.Gameplay.Move, InputActionType.Value, expectedControlLayout: "Vector2");
            AddWasd(move);

            map.AddAction(InputActionNames.Gameplay.Attack, InputActionType.Button,
                "<Keyboard>/j", groups: KeyboardMouse);
            map.AddAction(InputActionNames.Gameplay.Jump, InputActionType.Button,
                "<Keyboard>/k", groups: KeyboardMouse);
            map.AddAction(InputActionNames.Gameplay.Dash, InputActionType.Button,
                "<Keyboard>/leftShift", groups: KeyboardMouse);

            // 진입과 실행이 한 액션 · 한 키다. Order 페이즈에서 이 키가 곧 실행이라
            // 예전의 Execute(Space)는 같은 일을 하는 두 번째 자리일 뿐이었다.
            map.AddAction(InputActionNames.Gameplay.BulletTime, InputActionType.Button,
                "<Keyboard>/e", groups: KeyboardMouse);

            map.AddAction(InputActionNames.Gameplay.CardUse, InputActionType.Button,
                "<Keyboard>/u", groups: KeyboardMouse);

            // 동료 교대. F인 이유는 세 프리셋 어디에서도 안 쓰는 자리이기 때문이다 —
            // Q는 마우스 프리셋이 CardUse로 가져가서 그 프리셋을 얹는 순간 충돌한다.
            map.AddAction(InputActionNames.Gameplay.Swap, InputActionType.Button,
                "<Keyboard>/f", groups: KeyboardMouse);
        }

        /// <summary>
        /// 손패 카드 조작. Order 페이즈이면서 조준 중이 아닐 때만 켠다.
        ///
        /// 액션이 Navigate 하나뿐이다. 집기(W) · 놓기(S)까지 방향에 실려 있어서
        /// 한 키가 두 액션을 동시에 발동하는 조합이 아예 없다.
        ///
        /// Navigate가 Gameplay/Move와 같은 WASD인 것은 의도된 것 — 정지 중에는
        /// PlayerControl이 이동을 막으므로 둘이 동시에 먹지 않는다.
        /// </summary>
        private static void BuildBulletTimeMap(InputActionAsset asset)
        {
            InputActionMap map = asset.AddActionMap(InputActionNames.BulletTime.Map);

            // 한 번 누르면 한 칸. 이산 스텝은 PlayerInputController가 프레임 차분으로 낸다.
            InputAction navigate = map.AddAction(
                InputActionNames.BulletTime.Navigate, InputActionType.Value, expectedControlLayout: "Vector2");
            AddWasd(navigate);
        }

        /// <summary>
        /// 스킬 시전 위치 지정. 카드를 집은 상태에서 위로 들어와 확정 · 취소로 나간다.
        ///
        /// Aim은 <b>연속</b> 이동이라 BulletTime의 이산 Navigate와 한 맵에 둘 수 없다.
        /// 같은 WASD라도 소비 방식이 근본적으로 다르다.
        /// </summary>
        private static void BuildSkillShotMap(InputActionAsset asset)
        {
            InputActionMap map = asset.AddActionMap(InputActionNames.BulletTimeSkillShot.Map);

            InputAction aim = map.AddAction(
                InputActionNames.BulletTimeSkillShot.Aim, InputActionType.Value, expectedControlLayout: "Vector2");
            AddWasd(aim);

            // position과 delta를 둘 다 둔다. 차분으로 이동량을 대신하면 맵을 켠 첫 프레임에
            // position이 0을 뱉어서, 실제 좌표가 들어오는 다음 프레임이 큰 이동으로 읽힌다.
            // 그러면 카드 위에서 시작한 조준점이 곧바로 마우스로 끌려간다.
            map.AddAction(InputActionNames.BulletTimeSkillShot.AimPoint, InputActionType.Value,
                "<Mouse>/position", groups: KeyboardMouse, expectedControlLayout: "Vector2");
            map.AddAction(InputActionNames.BulletTimeSkillShot.AimDelta, InputActionType.Value,
                "<Mouse>/delta", groups: KeyboardMouse, expectedControlLayout: "Vector2");

            // 키보드와 마우스를 같은 액션에 함께 묶는다. 조준 확정은 둘 다 허용한다.
            InputAction confirm = map.AddAction(InputActionNames.BulletTimeSkillShot.Confirm,
                InputActionType.Button, "<Keyboard>/j", groups: KeyboardMouse);
            confirm.AddBinding("<Mouse>/leftButton", groups: KeyboardMouse);

            InputAction cancel = map.AddAction(InputActionNames.BulletTimeSkillShot.Cancel,
                InputActionType.Button, "<Keyboard>/k", groups: KeyboardMouse);
            cancel.AddBinding("<Mouse>/rightButton", groups: KeyboardMouse);
        }

        /// <summary>
        /// <c>InputSystemUIInputModule</c>이 이름으로 찾아 쓰는 맵. 액션 이름을 바꾸면 안 된다.
        /// </summary>
        private static void BuildUIMap(InputActionAsset asset)
        {
            InputActionMap map = asset.AddActionMap(InputActionNames.UI.Map);

            InputAction navigate = map.AddAction(
                "Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");

            // 유니티 템플릿 기본값은 WASD도 물고 있다. 그대로 두면 이 자산을 UI 모듈에 물린 순간
            // 전투 중 이동키가 UI 포커스까지 움직인다. 화살표와 게임패드만 남긴다.
            navigate.AddCompositeBinding("2DVector")
                .With("up", "<Keyboard>/upArrow", KeyboardMouse)
                .With("down", "<Keyboard>/downArrow", KeyboardMouse)
                .With("left", "<Keyboard>/leftArrow", KeyboardMouse)
                .With("right", "<Keyboard>/rightArrow", KeyboardMouse);

            navigate.AddBinding("<Gamepad>/dpad", groups: Gamepad);
            navigate.AddBinding("<Gamepad>/leftStick", groups: Gamepad);

            // */{Submit} · */{Cancel}은 디바이스 공통 용도 바인딩이다.
            // 키보드에서는 각각 enter · escape로 풀린다.
            map.AddAction("Submit", InputActionType.Button, "*/{Submit}");
            map.AddAction(InputActionNames.UI.Cancel, InputActionType.Button, "*/{Cancel}");

            map.AddAction("Point", InputActionType.PassThrough,
                "<Mouse>/position", groups: KeyboardMouse, expectedControlLayout: "Vector2");
            map.AddAction("Click", InputActionType.PassThrough,
                "<Mouse>/leftButton", groups: KeyboardMouse, expectedControlLayout: "Button");
            map.AddAction("RightClick", InputActionType.PassThrough,
                "<Mouse>/rightButton", groups: KeyboardMouse, expectedControlLayout: "Button");
            map.AddAction("MiddleClick", InputActionType.PassThrough,
                "<Mouse>/middleButton", groups: KeyboardMouse, expectedControlLayout: "Button");
            map.AddAction("ScrollWheel", InputActionType.PassThrough,
                "<Mouse>/scroll", groups: KeyboardMouse, expectedControlLayout: "Vector2");
        }

        /// <summary>
        /// 상하좌우를 <b>파트 4개짜리</b> 2DVector 컴포지트로 붙인다.
        /// 컴포지트를 통째로 바인딩 하나로 두면 리바인드 UI가 WASD를 영영 못 고친다.
        /// </summary>
        private static void AddWasd(InputAction action)
        {
            action.AddCompositeBinding("2DVector")
                .With("up", "<Keyboard>/w", KeyboardMouse)
                .With("down", "<Keyboard>/s", KeyboardMouse)
                .With("left", "<Keyboard>/a", KeyboardMouse)
                .With("right", "<Keyboard>/d", KeyboardMouse);
        }
    }
}
