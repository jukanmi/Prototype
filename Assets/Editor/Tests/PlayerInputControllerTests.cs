using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Prototype.Tests
{
    /// <summary>
    /// PlayerInputController의 배선을 본다 — 액션 이름이 전부 풀리는가, 맵이 켜지는가,
    /// Instance 수명이 맞는가.
    ///
    /// 실제 키를 눌러 보는 테스트는 없다. 그러려면 Input System의 <c>InputTestFixture</c>가
    /// 필요한데, 그건 별도 어셈블리(Unity.InputSystem.TestFramework)라 asmdef 없이는 참조를 못 건다.
    /// 게임 코드가 asmdef로 쪼개지기 전까지는 여기까지가 한계다.
    /// </summary>
    public class PlayerInputControllerTests
    {
        private const string AssetPath = "Assets/Settings/InputSystem_Actions.inputactions";

        private GameObject rig;
        private InputActionAsset actions;
        private PlayerInputController controller;

        [SetUp]
        public void SetUp()
        {
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(source, Is.Not.Null, $"{AssetPath} 를 못 찾았다.");

            // 프로젝트 자산을 직접 쓰면 테스트가 맵을 켜고 끈 흔적이 에셋에 남는다. 복제해서 쓴다.
            actions = Object.Instantiate(source);
            actions.name = source.name;

            // 비활성으로 만들어 두고 조립한다 — 활성 상태면 AddComponent 하는 순간 Awake가 돌아
            // PlayerInput에 자산을 넣기도 전에 PlayerInputController가 먼저 깨어난다.
            rig = new GameObject("InputRig");
            rig.SetActive(false);

            var playerInput = rig.AddComponent<PlayerInput>();
            playerInput.actions = actions;
            playerInput.defaultActionMap = InputActionNames.Gameplay.Map;

            controller = rig.AddComponent<PlayerInputController>();

            // PlayerInput은 디바이스 짝짓기에 실패하면 로그를 남긴다. 테스트 러너가 그걸
            // 실패로 세지 않도록 조립 구간에만 잠깐 막는다.
            LogAssert.ignoreFailingMessages = true;
            rig.SetActive(true);
            LogAssert.ignoreFailingMessages = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (rig != null) Object.DestroyImmediate(rig);
            if (actions != null) Object.DestroyImmediate(actions);
        }

        // ── 수명 ────────────────────────────────────────────

        [Test]
        public void Awake_SetsInstance()
        {
            Assert.That(PlayerInputController.Instance, Is.SameAs(controller));
        }

        [Test]
        public void Destroy_ClearsInstance()
        {
            Object.DestroyImmediate(rig);
            rig = null;

            // 남아 있으면 씬을 다시 로드한 뒤 UI가 죽은 컨트롤러를 붙들고 입력을 못 읽는다.
            Assert.That(PlayerInputController.Instance, Is.Null);
        }

        // ── 배선 ────────────────────────────────────────────

        [Test]
        public void Actions_IsThePlayerInputAsset()
        {
            // 직렬화한 자산을 따로 들고 있으면 런타임 리바인드가 다른 인스턴스에 얹혀
            // 화면에 보이는 키와 실제로 먹는 키가 갈린다.
            Assert.That(controller.Actions, Is.SameAs(rig.GetComponent<PlayerInput>().actions));
        }

        [Test]
        public void Enable_EnablesGameplayAndUiMaps()
        {
            Assert.That(controller.Actions.FindActionMap(InputActionNames.Gameplay.Map).enabled, Is.True);
            Assert.That(controller.Actions.FindActionMap(InputActionNames.UI.Map).enabled, Is.True);
        }

        [Test]
        public void Enable_LeavesBulletTimeMapsClosed()
        {
            // 불릿타임 두 맵은 InputMapSwitcher가 페이즈를 보고 연다.
            // 여기서 켜 두면 실시간 전투 중 J가 평타이면서 카드 집기로도 먹는다.
            Assert.That(controller.Actions.FindActionMap(InputActionNames.BulletTime.Map).enabled, Is.False);
            Assert.That(controller.Actions.FindActionMap(InputActionNames.BulletTimeSkillShot.Map).enabled, Is.False);
        }

        [Test]
        public void Disable_LeavesUiMapAlone()
        {
            controller.enabled = false;

            // UI 맵은 InputSystemUIInputModule과 공유한다. 같이 끄면 화면 버튼이 전부 죽는다.
            Assert.That(controller.Actions.FindActionMap(InputActionNames.UI.Map).enabled, Is.True);
            Assert.That(controller.Actions.FindActionMap(InputActionNames.Gameplay.Map).enabled, Is.False);
        }

        // ── 맵 개폐 ─────────────────────────────────────────

        [Test]
        public void BulletTimeMapEnabled_TogglesTheMap()
        {
            controller.BulletTimeMapEnabled = true;
            Assert.That(controller.Actions.FindActionMap(InputActionNames.BulletTime.Map).enabled, Is.True);
            Assert.That(controller.BulletTimeMapEnabled, Is.True);

            controller.BulletTimeMapEnabled = false;
            Assert.That(controller.Actions.FindActionMap(InputActionNames.BulletTime.Map).enabled, Is.False);
            Assert.That(controller.BulletTimeMapEnabled, Is.False);
        }

        [Test]
        public void SkillShotMapEnabled_TogglesTheMap()
        {
            controller.SkillShotMapEnabled = true;
            Assert.That(controller.Actions.FindActionMap(InputActionNames.BulletTimeSkillShot.Map).enabled, Is.True);
            Assert.That(controller.SkillShotMapEnabled, Is.True);

            controller.SkillShotMapEnabled = false;
            Assert.That(controller.Actions.FindActionMap(InputActionNames.BulletTimeSkillShot.Map).enabled, Is.False);
            Assert.That(controller.SkillShotMapEnabled, Is.False);
        }

        [Test]
        public void FreshlyEnabledAimMap_ReportsNoMouseMove()
        {
            // 켠 직후 마우스를 안 건드렸으면 움직이지 않은 것이다.
            // position 차분으로 판단하던 시절엔 여기서 화면 절반짜리 이동이 잡혀
            // 카드 위에서 시작한 조준점이 곧바로 마우스로 끌려갔다.
            controller.SkillShotMapEnabled = true;

            Assert.That(controller.AimPointMovedThisFrame, Is.False);
        }

        [Test]
        public void ReEnablingCardMap_DoesNotReportPhantomStep()
        {
            // 맵을 다시 켠 첫 프레임은 기준값을 새로 잡고 스텝을 내지 않는다.
            // 안 그러면 조준을 마치고 돌아오는 순간 W를 쥐고 있던 손이 곧바로
            // 조준으로 되튀거나, 카드 한 칸이 공짜로 넘어간다.
            controller.BulletTimeMapEnabled = true;
            controller.BulletTimeMapEnabled = false;
            controller.BulletTimeMapEnabled = true;

            Assert.That(controller.NavigateStep, Is.EqualTo(Vector2Int.zero));
        }

        // ── 무입력 기본값 ───────────────────────────────────

        [Test]
        public void NoInput_SkillPressedIsMinusOne()
        {
            Assert.That(controller.SkillPressed, Is.EqualTo(-1));
        }

        [Test]
        public void NoInput_MoveIsZero()
        {
            Assert.That(controller.Move, Is.EqualTo(Vector2.zero));
            Assert.That(controller.Aim, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void NoInput_NavigateStepIsZero()
        {
            Assert.That(controller.NavigateStep, Is.EqualTo(Vector2Int.zero));
        }

        [Test]
        public void NoInput_ButtonsAreNotPressed()
        {
            Assert.That(controller.AttackPressed, Is.False);
            Assert.That(controller.JumpPressed, Is.False);
            Assert.That(controller.DashPressed, Is.False);
            Assert.That(controller.BulletTimePressed, Is.False);
            Assert.That(controller.CardUsePressed, Is.False);
            Assert.That(controller.AimConfirmPressed, Is.False);
            Assert.That(controller.AimCancelPressed, Is.False);
            Assert.That(controller.CancelPressed, Is.False);
        }
    }
}
