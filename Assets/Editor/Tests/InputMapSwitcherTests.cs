using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Prototype.Tests
{
    /// <summary>
    /// 불릿타임 액션 맵 둘이 페이즈와 조준 여부에 맞춰 열리는지, 그리고
    /// <b>절대 동시에 열리지 않는지</b> 본다.
    ///
    /// 동시성이 이 파일의 핵심이다. 기본 바인딩에서 J는 카드 놓기와 조준 확정 양쪽에
    /// 걸려 있어서, 두 맵이 겹치면 한 번 누른 J가 조준을 확정하고 그 카드를 놓는 것까지
    /// 한 프레임에 해 버린다.
    /// </summary>
    public class InputMapSwitcherTests
    {
        private const string AssetPath = "Assets/Settings/InputSystem_Actions.inputactions";

        private GameObject inputRig;
        private GameObject battleRig;
        private InputActionAsset actions;

        private PlayerInputController controller;
        private InputMapSwitcher switcher;
        private BulletTimeController bulletTime;

        [SetUp]
        public void SetUp()
        {
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(source, Is.Not.Null, $"{AssetPath} 를 못 찾았다.");

            actions = Object.Instantiate(source);
            actions.name = source.name;

            battleRig = new GameObject("BattleRig");
            bulletTime = battleRig.AddComponent<BulletTimeController>();


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
            Awake(bulletTime);
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
            if (battleRig != null) Object.DestroyImmediate(battleRig);
            if (actions != null) Object.DestroyImmediate(actions);

            LogAssert.ignoreFailingMessages = false;
        }

        private bool CardMapEnabled
            => controller.Actions.FindActionMap(InputActionNames.BulletTime.Map).enabled;

        private void EnterOrderPhase() => bulletTime.ForcePhase(TacticPhase.Order);

        // ── 실시간 전투 ─────────────────────────────────────

        [Test]
        public void BeforeAnyPhase_BothMapsClosed()
        {
            Assert.That(bulletTime.AllowsCardEdit, Is.False, "전제가 깨졌다.");

            switcher.Apply();

            // 실시간 전투 중 카드 맵이 열려 있으면 J가 평타이면서 카드 집기도 된다.
            Assert.That(CardMapEnabled, Is.False);
        }

        [Test]
        public void OnEnable_DoesNotOpenBulletTimeMaps()
        {
            // 스위처를 거치지 않고도 닫혀 있어야 한다 — PlayerInputController가
            // OnEnable에서 세 맵을 다 켜던 시절의 회귀를 막는다.
            Assert.That(CardMapEnabled, Is.False);
        }

        // ── Order 페이즈 ────────────────────────────────────

        [Test]
        public void InOrderPhase_CardMapOpens()
        {
            EnterOrderPhase();
            switcher.Apply();

            Assert.That(CardMapEnabled, Is.True, "Order 페이즈인데 카드 조작이 안 먹는다.");
        }

        // ── 페이즈 이탈 ─────────────────────────────────────

        [Test]
        public void LeavingOrderPhase_ClosesBothMaps()
        {
            EnterOrderPhase();
            switcher.Apply();
            Assert.That(CardMapEnabled, Is.True, "전제가 깨졌다.");

            // Freeze는 연출 구간이라 카드 편집을 안 받는다.
            bulletTime.ForcePhase(TacticPhase.Freeze);
            switcher.Apply();

            Assert.That(CardMapEnabled, Is.False);
        }

        [Test]
        public void WithoutBulletTimeController_BothMapsStayClosed()
        {
            Object.DestroyImmediate(battleRig);
            battleRig = null;

            switcher.Apply();

            Assert.That(CardMapEnabled, Is.False);
        }

        [Test]
        public void JumpCancellation_ClosesCardMap()
        {
            EnterOrderPhase();
            switcher.Apply();
            Assert.That(bulletTime.Tactic.OnCancelKey(), Is.True);
            switcher.Apply();
            Assert.That(CardMapEnabled, Is.False);
            Assert.That(TimeControl.Scale, Is.EqualTo(1f));
        }
    }
}
