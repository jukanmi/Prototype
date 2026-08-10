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
        private SkillData skill;

        private PlayerInputController controller;
        private InputMapSwitcher switcher;
        private BulletTimeController bulletTime;
        private TargetSelector targetSelector;

        [SetUp]
        public void SetUp()
        {
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(source, Is.Not.Null, $"{AssetPath} 를 못 찾았다.");

            actions = Object.Instantiate(source);
            actions.name = source.name;

            battleRig = new GameObject("BattleRig");
            bulletTime = battleRig.AddComponent<BulletTimeController>();
            targetSelector = battleRig.AddComponent<TargetSelector>();

            // 조준 대상이 될 스킬. Begin에 null을 넣으면 IsSelecting이 서지 않는다.
            skill = ScriptableObject.CreateInstance<SkillData>();

            inputRig = new GameObject("InputRig");
            inputRig.SetActive(false);

            var playerInput = inputRig.AddComponent<PlayerInput>();
            playerInput.actions = actions;
            playerInput.defaultActionMap = InputActionNames.Gameplay.Map;

            controller = inputRig.AddComponent<PlayerInputController>();
            switcher = inputRig.AddComponent<InputMapSwitcher>();

            LogAssert.ignoreFailingMessages = true;
            inputRig.SetActive(true);
            LogAssert.ignoreFailingMessages = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (inputRig != null) Object.DestroyImmediate(inputRig);
            if (battleRig != null) Object.DestroyImmediate(battleRig);
            if (actions != null) Object.DestroyImmediate(actions);
            if (skill != null) Object.DestroyImmediate(skill);

            LogAssert.ignoreFailingMessages = false;
        }

        private bool CardMapEnabled
            => controller.Actions.FindActionMap(InputActionNames.BulletTime.Map).enabled;

        private bool AimMapEnabled
            => controller.Actions.FindActionMap(InputActionNames.BulletTimeSkillShot.Map).enabled;

        private void EnterOrderPhase() => bulletTime.Tactic.ChangeTo(bulletTime.Tactic.Order);

        // ── 실시간 전투 ─────────────────────────────────────

        [Test]
        public void BeforeAnyPhase_BothMapsClosed()
        {
            Assert.That(bulletTime.AllowsCardEdit, Is.False, "전제가 깨졌다.");

            switcher.Apply();

            // 실시간 전투 중 카드 맵이 열려 있으면 J가 평타이면서 카드 집기도 된다.
            Assert.That(CardMapEnabled, Is.False);
            Assert.That(AimMapEnabled, Is.False);
        }

        [Test]
        public void OnEnable_DoesNotOpenBulletTimeMaps()
        {
            // 스위처를 거치지 않고도 닫혀 있어야 한다 — PlayerInputController가
            // OnEnable에서 세 맵을 다 켜던 시절의 회귀를 막는다.
            Assert.That(CardMapEnabled, Is.False);
            Assert.That(AimMapEnabled, Is.False);
        }

        // ── Order 페이즈 ────────────────────────────────────

        [Test]
        public void InOrderPhase_CardMapOpens()
        {
            EnterOrderPhase();
            switcher.Apply();

            Assert.That(CardMapEnabled, Is.True, "Order 페이즈인데 카드 조작이 안 먹는다.");
            Assert.That(AimMapEnabled, Is.False, "조준 중이 아닌데 조준 맵이 열렸다.");
        }

        [Test]
        public void WhileAiming_AimMapOpensAndCardMapCloses()
        {
            EnterOrderPhase();
            targetSelector.Begin(skill);
            Assert.That(targetSelector.IsSelecting, Is.True, "전제가 깨졌다.");

            switcher.Apply();

            Assert.That(AimMapEnabled, Is.True);
            Assert.That(CardMapEnabled, Is.False,
                "조준 중에 카드 맵이 함께 열려 있다. J 한 번이 조준 확정과 카드 놓기를 둘 다 한다.");
        }

        [Test]
        public void AfterAiming_ReturnsToCardMap()
        {
            EnterOrderPhase();
            targetSelector.Begin(skill);
            switcher.Apply();

            targetSelector.Cancel();
            switcher.Apply();

            Assert.That(CardMapEnabled, Is.True, "조준을 접었는데 카드 조작으로 안 돌아왔다.");
            Assert.That(AimMapEnabled, Is.False);
        }

        [Test]
        public void TwoMaps_AreNeverBothOpen()
        {
            // 페이즈 · 조준 조합을 전부 훑는다.
            foreach (bool order in new[] { false, true })
            {
                bulletTime.Tactic.ChangeTo(order ? bulletTime.Tactic.Order : bulletTime.Tactic.Freeze);

                foreach (bool aiming in new[] { false, true })
                {
                    if (aiming) targetSelector.Begin(skill);
                    else targetSelector.Cancel();

                    switcher.Apply();

                    Assert.That(CardMapEnabled && AimMapEnabled, Is.False,
                        $"두 맵이 함께 열렸다 (order={order}, aiming={aiming}).");
                }
            }
        }

        // ── 페이즈 이탈 ─────────────────────────────────────

        [Test]
        public void LeavingOrderPhase_ClosesBothMaps()
        {
            EnterOrderPhase();
            switcher.Apply();
            Assert.That(CardMapEnabled, Is.True, "전제가 깨졌다.");

            // Freeze는 연출 구간이라 카드 편집을 안 받는다.
            bulletTime.Tactic.ChangeTo(bulletTime.Tactic.Freeze);
            switcher.Apply();

            Assert.That(CardMapEnabled, Is.False);
            Assert.That(AimMapEnabled, Is.False);
        }

        [Test]
        public void AimingOutsideOrderPhase_DoesNotOpenAimMap()
        {
            // 조준 상태가 남은 채 페이즈가 넘어갔을 때 조준 맵이 살아 있으면
            // 실시간 전투 중 WASD가 이동과 조준을 동시에 먹는다.
            targetSelector.Begin(skill);
            switcher.Apply();

            Assert.That(AimMapEnabled, Is.False);
        }

        [Test]
        public void WithoutBulletTimeController_BothMapsStayClosed()
        {
            Object.DestroyImmediate(battleRig);
            battleRig = null;

            switcher.Apply();

            Assert.That(CardMapEnabled, Is.False);
            Assert.That(AimMapEnabled, Is.False);
        }
    }
}
