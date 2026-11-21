using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Prototype.Tests
{
    /// <summary>
    /// 카드 조작 맵(<c>BulletTime</c>)이 총공격 편집창이 닫혀 있는 동안 <b>닫혀 있는지</b> 본다.
    ///
    /// 실시간 전투 중 이 맵이 열려 있으면 이동키(WASD)가 편집창 칸 이동으로도 먹는다.
    /// </summary>
    public class InputMapSwitcherTests
    {
        private const string AssetPath = "Assets/Settings/InputSystem_Actions.inputactions";

        private GameObject inputRig;
        private InputActionAsset actions;

        private PlayerInputController controller;
        private InputMapSwitcher switcher;

        [SetUp]
        public void SetUp()
        {
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(source, Is.Not.Null, $"{AssetPath} 를 못 찾았다.");

            actions = Object.Instantiate(source);
            actions.name = source.name;

            inputRig = new GameObject("InputRig");
            inputRig.SetActive(false);

            var playerInput = inputRig.AddComponent<PlayerInput>();
            playerInput.actions = actions;
            playerInput.defaultActionMap = InputActionNames.Gameplay.Map;

            controller = inputRig.AddComponent<PlayerInputController>();
            switcher = inputRig.AddComponent<InputMapSwitcher>();

            LogAssert.ignoreFailingMessages = true;
            inputRig.SetActive(true);
            // Explicitly initialize Edit Mode fixtures; runtime Awake is not guaranteed here.
            Awake(controller);
            Awake(switcher);
            LogAssert.ignoreFailingMessages = false;
        }

        private static void Awake(object component)
            => component.GetType().GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(component, null);

        [TearDown]
        public void TearDown()
        {
            if (inputRig != null) Object.DestroyImmediate(inputRig);
            if (actions != null) Object.DestroyImmediate(actions);

            LogAssert.ignoreFailingMessages = false;
        }

        private bool CardMapEnabled
            => controller.Actions.FindActionMap(InputActionNames.BulletTime.Map).enabled;

        [Test]
        public void EditorClosed_CardMapStaysClosed()
        {
            Assert.That(AssaultEditorUI.IsOpen, Is.False, "전제가 깨졌다.");

            switcher.Apply();

            Assert.That(CardMapEnabled, Is.False);
        }

        [Test]
        public void OnEnable_DoesNotOpenCardMap()
        {
            // 스위처를 거치지 않고도 닫혀 있어야 한다 — PlayerInputController가
            // OnEnable에서 맵을 다 켜던 시절의 회귀를 막는다.
            Assert.That(CardMapEnabled, Is.False);
        }

        [Test]
        public void ClosingTheEditor_ClosesAnOpenCardMap()
        {
            // 편집창이 열려 있던 상태 — 맵이 켜져 있다.
            controller.BulletTimeMapEnabled = true;
            Assert.That(CardMapEnabled, Is.True, "전제가 깨졌다.");

            // 편집창이 닫혔으니 다음 Apply에서 맵도 닫혀야 한다.
            switcher.Apply();

            Assert.That(CardMapEnabled, Is.False);
        }

        [Test]
        public void GameplaySuspended_OpensCardMap_ForMenuCursor()
        {
            // 창(대화 선택지 · 상점)이 떠 조작이 잠기면 WASD로 커서를 옮긴다 — 맵이 열려야 한다.
            controller.GameplaySuspended = true;
            switcher.Apply();
            Assert.That(CardMapEnabled, Is.True);

            // 창이 닫히면 다시 닫힌다. 안 닫히면 걷는 키가 칸 이동으로도 먹는다.
            controller.GameplaySuspended = false;
            switcher.Apply();
            Assert.That(CardMapEnabled, Is.False);
        }
    }
}
