using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype.Tests
{
    /// <summary>
    /// 리바인드 목록과 충돌 판정.
    ///
    /// 충돌 판정이 핵심이다. "같이 켜져 있는 맵끼리 겹치면 충돌"로 잡으면
    /// 기본 바인딩부터 빨갛게 뜬다 — Move · Navigate · Aim이 셋 다 WASD다.
    /// </summary>
    public class InputRebindRulesTests
    {
        private const string AssetPath = "Assets/Settings/InputSystem_Actions.inputactions";

        private InputActionAsset actions;

        [SetUp]
        public void SetUp()
        {
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(source, Is.Not.Null, $"{AssetPath} 를 못 찾았다.");

            actions = Object.Instantiate(source);
        }

        [TearDown]
        public void TearDown()
        {
            if (actions != null) Object.DestroyImmediate(actions);
        }

        private InputAction Action(string map, string name)
            => InputBindingUtility.FindAction(actions, map, name);

        // ── 목록에 무엇이 뜨는가 ────────────────────────────

        [Test]
        public void Enumerate_SkipsUIMap()
        {
            // UI 맵은 InputSystemUIInputModule이 이름으로 찾아 쓰는 규약이고
            // Cancel은 */{Cancel} 용도 바인딩이라 특정 키로 바꿀 자리가 아니다.
            Assert.That(InputRebindRules.Enumerate(actions).Select(e => e.action.actionMap.name),
                Has.None.EqualTo(InputActionNames.UI.Map));
        }

        [Test]
        public void Enumerate_SkipsPointerActions()
        {
            var names = InputRebindRules.Enumerate(actions).Select(e => e.action.name).ToList();

            // 마우스 위치·이동량은 키로 바꿀 수 있는 값이 아니다.
            Assert.That(names, Has.None.EqualTo(InputActionNames.BulletTimeSkillShot.AimPoint));
            Assert.That(names, Has.None.EqualTo(InputActionNames.BulletTimeSkillShot.AimDelta));
        }

        [Test]
        public void Enumerate_SkipsMouseBindings()
        {
            // 조준 확정의 좌클릭까지 바꾸게 하면 키보드 자리를 잘못 잡았을 때 빠져나올 길이 없다.
            foreach ((InputAction action, int index) in InputRebindRules.Enumerate(actions))
            {
                Assert.That(action.bindings[index].path, Does.Not.StartWith("<Mouse>"),
                    $"{action.actionMap.name}/{action.name} 의 마우스 바인딩이 목록에 있다.");
            }
        }

        [Test]
        public void Enumerate_SkipsCompositeHeads()
        {
            // 컴포지트 머리는 "2DVector"라는 타입 이름일 뿐이라 키를 넣을 자리가 아니다.
            foreach ((InputAction action, int index) in InputRebindRules.Enumerate(actions))
                Assert.That(action.bindings[index].isComposite, Is.False);
        }

        [Test]
        public void Enumerate_IncludesEveryMoveDirection()
        {
            var move = InputRebindRules.Enumerate(actions)
                .Where(e => e.action.name == InputActionNames.Gameplay.Move)
                .ToList();

            Assert.That(move, Has.Count.EqualTo(4), "WASD 네 방향이 각각 줄로 떠야 한다.");
        }

        // ── 무엇이 충돌인가 ─────────────────────────────────

        [Test]
        public void SameMap_AlwaysConflicts()
        {
            InputAction attack = Action(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Attack);
            InputAction jump = Action(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Jump);

            Assert.That(InputRebindRules.CanFireTogether(attack, jump), Is.True);
        }

        [Test]
        public void CardAndAimMaps_NeverConflict()
        {
            // InputMapSwitcher가 배타로 잡는다. 둘 다 WASD인 게 정상이다.
            InputAction navigate = Action(InputActionNames.BulletTime.Map, InputActionNames.BulletTime.Navigate);
            InputAction aim = Action(InputActionNames.BulletTimeSkillShot.Map, InputActionNames.BulletTimeSkillShot.Aim);

            Assert.That(InputRebindRules.CanFireTogether(navigate, aim), Is.False);
        }

        [Test]
        public void GameplayMovement_DoesNotConflictWithCardMap()
        {
            // 정지 중에는 PlayerControl이 이동을 막는다. 이게 충돌이면 기본 WASD가 곧바로 빨개진다.
            InputAction move = Action(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Move);
            InputAction navigate = Action(InputActionNames.BulletTime.Map, InputActionNames.BulletTime.Navigate);

            Assert.That(InputRebindRules.CanFireTogether(move, navigate), Is.False);
        }

        [TestCase(InputActionNames.Gameplay.BulletTime)]
        [TestCase(InputActionNames.Gameplay.CardUse)]
        public void CommanderKeys_ConflictWithCardMap(string commander)
        {
            // 지휘키는 정지 중에도 산다 — 카드 조작과 진짜로 같은 프레임에 발동한다.
            InputAction action = Action(InputActionNames.Gameplay.Map, commander);
            InputAction navigate = Action(InputActionNames.BulletTime.Map, InputActionNames.BulletTime.Navigate);

            Assert.That(InputRebindRules.CanFireTogether(action, navigate), Is.True);
        }

        // ── 기본 바인딩은 깨끗한가 ──────────────────────────

        [Test]
        public void ShippedBindings_HaveNoConflicts()
        {
            foreach ((InputAction action, int index) in InputRebindRules.Enumerate(actions).ToList())
            {
                string path = action.bindings[index].effectivePath;

                bool conflict = InputRebindRules.TryFindConflict(actions, action, index, path,
                    out InputAction blocker, out int blockerIndex);

                Assert.That(conflict, Is.False,
                    $"기본 바인딩끼리 충돌한다: {action.actionMap.name}/{action.name}[{index}] " +
                    $"↔ {blocker?.actionMap.name}/{blocker?.name}[{blockerIndex}] — {path}");
            }
        }

        // ── 충돌 검출 ───────────────────────────────────────

        [Test]
        public void TakingAKeyUsedInTheSameMap_IsAConflict()
        {
            InputAction jump = Action(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Jump);
            int index = InputBindingUtility.FindBindingIndex(jump, "");

            // J는 평타가 쓰고 있다.
            bool conflict = InputRebindRules.TryFindConflict(actions, jump, index, "<Keyboard>/j",
                out InputAction blocker, out _);

            Assert.That(conflict, Is.True);
            Assert.That(blocker.name, Is.EqualTo(InputActionNames.Gameplay.Attack));
        }

        [Test]
        public void TakingAKeyFromAnotherPartOfTheSameComposite_IsAConflict()
        {
            InputAction move = Action(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Move);
            int up = InputBindingUtility.FindBindingIndex(move, "up");

            // 위와 왼쪽에 같은 키가 들어가면 방향이 통째로 망가진다.
            bool conflict = InputRebindRules.TryFindConflict(actions, move, up, "<Keyboard>/a", out _, out _);

            Assert.That(conflict, Is.True);
        }

        [Test]
        public void KeepingItsOwnKey_IsNotAConflict()
        {
            InputAction attack = Action(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Attack);
            int index = InputBindingUtility.FindBindingIndex(attack, "");

            // 자기 자신과 부딪힌다고 하면 아무 키도 다시 지정할 수 없다.
            Assert.That(InputRebindRules.TryFindConflict(actions, attack, index, "<Keyboard>/j", out _, out _),
                Is.False);
        }

        [Test]
        public void TakingTheCardMapKeyForMovement_IsNotAConflict()
        {
            // Gameplay/Attack 을 W 로 옮겨도 카드 조작(W = 집기)과는 안 부딪힌다 —
            // 정지 중 평타는 막혀 있다.
            InputAction attack = Action(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Attack);
            int index = InputBindingUtility.FindBindingIndex(attack, "");

            Assert.That(InputRebindRules.TryFindConflict(actions, attack, index, "<Keyboard>/w", out _, out _),
                Is.False);
        }

        [Test]
        public void TakingTheCardMapKeyForACommander_IsAConflict()
        {
            // 반면 지휘키를 W 로 옮기면 카드를 집으면서 불릿타임이 함께 걸린다.
            InputAction commander = Action(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.CardUse);
            int index = InputBindingUtility.FindBindingIndex(commander, "");

            Assert.That(InputRebindRules.TryFindConflict(actions, commander, index, "<Keyboard>/w", out _, out _),
                Is.True);
        }
    }
}
