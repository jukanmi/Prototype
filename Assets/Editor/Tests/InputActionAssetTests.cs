using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace Prototype.Tests
{
    /// <summary>
    /// 액션 자산이 <see cref="InputActionNames"/>가 기대하는 모양 그대로인지 본다.
    ///
    /// 입력은 코드가 아니라 자산에 들어 있어서 컴파일러가 안 잡아 준다.
    /// 인스펙터에서 액션 이름 하나 고치면 런타임에 조용히 죽는 걸
    /// 여기서 잡는 게 이 파일의 전부다.
    /// </summary>
    public class InputActionAssetTests
    {
        private const string AssetPath = "Assets/Settings/InputSystem_Actions.inputactions";

        private InputActionAsset asset;

        [SetUp]
        public void SetUp()
        {
            asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null, $"{AssetPath} 를 못 찾았다.");
        }

        private InputActionMap Map(string name)
        {
            InputActionMap map = asset.FindActionMap(name);
            Assert.That(map, Is.Not.Null, $"맵 '{name}' 이 자산에 없다.");
            return map;
        }

        private InputAction Action(string mapName, string actionName)
        {
            InputAction action = Map(mapName).FindAction(actionName);
            Assert.That(action, Is.Not.Null, $"액션 '{mapName}/{actionName}' 이 자산에 없다.");
            return action;
        }

        // ── 맵 · 액션이 전부 있는가 ─────────────────────────

        [Test]
        public void AllExpectedMaps_Exist()
        {
            Assert.That(asset.actionMaps.Select(m => m.name),
                Is.EquivalentTo(new[]
                {
                    InputActionNames.Gameplay.Map,
                    InputActionNames.BulletTime.Map,
                    InputActionNames.BulletTimeSkillShot.Map,
                    InputActionNames.UI.Map,
                }));
        }

        [Test]
        public void GameplayMap_HasAllActions()
        {
            var expected = new List<string>
            {
                InputActionNames.Gameplay.Move,
                InputActionNames.Gameplay.Attack,
                InputActionNames.Gameplay.Jump,
                InputActionNames.Gameplay.Dash,
                InputActionNames.Gameplay.BulletTime,
                InputActionNames.Gameplay.CardUse,
            };
            expected.AddRange(InputActionNames.Gameplay.Skills);

            Assert.That(Map(InputActionNames.Gameplay.Map).actions.Select(a => a.name),
                Is.EquivalentTo(expected));
        }

        [Test]
        public void BulletTimeMap_HasOnlyNavigate()
        {
            // 집기(W) · 놓기(S)까지 방향에 실려 있다. 액션을 따로 두면 그 키가
            // Navigate와 겹쳐 한 번 누른 W가 집기와 조준 진입을 함께 터뜨린다.
            Assert.That(Map(InputActionNames.BulletTime.Map).actions.Select(a => a.name),
                Is.EquivalentTo(new[] { InputActionNames.BulletTime.Navigate }));
        }

        [Test]
        public void SkillShotMap_HasAllActions()
        {
            Assert.That(Map(InputActionNames.BulletTimeSkillShot.Map).actions.Select(a => a.name),
                Is.EquivalentTo(new[]
                {
                    InputActionNames.BulletTimeSkillShot.Aim,
                    InputActionNames.BulletTimeSkillShot.AimPoint,
                    InputActionNames.BulletTimeSkillShot.AimDelta,
                    InputActionNames.BulletTimeSkillShot.Confirm,
                    InputActionNames.BulletTimeSkillShot.Cancel,
                }));
        }

        [Test]
        public void AimDelta_IsBoundToMouseDelta()
        {
            // position의 프레임 차분으로 대신하면 안 된다. 맵을 켠 첫 프레임에 position이
            // 0을 뱉어서, 실제 좌표가 들어오는 다음 프레임이 큰 이동으로 읽힌다 —
            // 카드 위에서 시작한 조준점이 손도 안 댄 마우스로 끌려간다.
            InputAction delta = Action(InputActionNames.BulletTimeSkillShot.Map,
                InputActionNames.BulletTimeSkillShot.AimDelta);

            Assert.That(delta.bindings.Select(b => b.path), Is.EquivalentTo(new[] { "<Mouse>/delta" }));
        }

        [Test]
        public void CardNavigate_IsNotBoundToMouse()
        {
            // 카드 조작 중 마우스는 드래그 앤 드롭이 쓴다. 액션에 묶으면
            // 아무 데나 클릭해도 카드가 집히거나 놓인다.
            InputAction navigate = Action(InputActionNames.BulletTime.Map, InputActionNames.BulletTime.Navigate);

            Assert.That(navigate.bindings.Select(b => b.path), Has.None.StartsWith("<Mouse>"));
        }

        [Test]
        public void SkillShotConfirm_TakesKeyboardAndMouse()
        {
            // 조준 확정만은 마우스도 받는다. 기존 좌클릭 조준 흐름을 그대로 살린다.
            InputAction confirm = Action(InputActionNames.BulletTimeSkillShot.Map,
                InputActionNames.BulletTimeSkillShot.Confirm);

            Assert.That(confirm.bindings.Select(b => b.path),
                Is.EquivalentTo(new[] { "<Keyboard>/j", "<Mouse>/leftButton" }));
        }

        [Test]
        public void UIMap_HasCancel()
        {
            Assert.That(Action(InputActionNames.UI.Map, InputActionNames.UI.Cancel), Is.Not.Null);
        }

        [Test]
        public void BulletTime_HasASingleKey()
        {
            // 진입과 실행이 한 액션 · 한 키다. 자리가 둘이면 리바인드 화면에
            // 같은 이름이 두 줄로 떠서 서로 다른 기능처럼 보인다.
            InputAction action = Action(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.BulletTime);

            Assert.That(action.bindings.Select(b => b.path),
                Is.EquivalentTo(new[] { "<Keyboard>/e" }));
        }

        [Test]
        public void SelfSkills_AreFourSeparateActions()
        {
            // 액션 하나로는 몇 번째 고유기인지 알 수 없어 4개로 쪼갰다.
            // Player.Party 가 4칸이라 이 숫자가 어긋나면 배열 밖을 짚는다.
            Assert.That(InputActionNames.Gameplay.Skills.Length, Is.EqualTo(4));
        }

        // ── 컴포지트 모양 ───────────────────────────────────

        [TestCase(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Move)]
        [TestCase(InputActionNames.BulletTime.Map, InputActionNames.BulletTime.Navigate)]
        [TestCase(InputActionNames.BulletTimeSkillShot.Map, InputActionNames.BulletTimeSkillShot.Aim)]
        public void Vector2Action_IsCompositeWithFourParts(string mapName, string actionName)
        {
            InputAction action = Action(mapName, actionName);

            Assert.That(action.expectedControlType, Is.EqualTo("Vector2"));

            // 파트가 4개여야 리바인드 UI가 상하좌우를 따로 바꿀 수 있다.
            // 컴포지트를 통째로 하나의 바인딩으로 두면 WASD 를 영영 못 고친다.
            // 파트 이름의 대소문자는 유니티 UI에서 손으로 고치면 흔들린다. 런타임 조회도
            // 대소문자를 안 가리므로 여기서도 맞춰 준다.
            var parts = action.bindings
                .Where(b => b.isPartOfComposite)
                .Select(b => b.name.ToLowerInvariant())
                .ToList();
            Assert.That(parts, Is.EquivalentTo(new[] { "up", "down", "left", "right" }),
                $"{mapName}/{actionName} 컴포지트 파트가 상하좌우 4개가 아니다.");

            Assert.That(action.bindings.Count(b => b.isComposite), Is.EqualTo(1));
        }

        // ── 기본 키가 README 조작표와 맞는가 ────────────────

        [TestCase(InputActionNames.Gameplay.Attack, "<Keyboard>/j")]
        [TestCase(InputActionNames.Gameplay.Jump, "<Keyboard>/k")]
        [TestCase(InputActionNames.Gameplay.Dash, "<Keyboard>/leftShift")]
        [TestCase(InputActionNames.Gameplay.BulletTime, "<Keyboard>/e")]
        [TestCase(InputActionNames.Gameplay.CardUse, "<Keyboard>/u")]
        [TestCase("Skill1", "<Keyboard>/z")]
        [TestCase("Skill2", "<Keyboard>/x")]
        [TestCase("Skill3", "<Keyboard>/c")]
        [TestCase("Skill4", "<Keyboard>/v")]
        public void GameplayDefaultBinding_MatchesDocumentedKey(string actionName, string path)
        {
            InputAction action = Action(InputActionNames.Gameplay.Map, actionName);
            Assert.That(action.bindings.Select(b => b.path), Does.Contain(path));
        }

        [Test]
        public void MoveDefault_IsWasd()
        {
            InputAction move = Action(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Move);

            Assert.That(move.bindings.Where(b => b.isPartOfComposite).Select(b => b.path),
                Is.EquivalentTo(new[]
                {
                    "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d",
                }));
        }

        // ── UI 맵이 게임플레이를 침범하지 않는가 ────────────

        [Test]
        public void UINavigate_DoesNotUseWasd()
        {
            // 이 자산을 InputSystemUIInputModule 에 물리면 UI/Navigate 가 항상 살아 있다.
            // 유니티 템플릿 기본값이 WASD 라, 그대로 두면 전투 중 이동키가 UI 포커스까지 움직인다.
            InputAction navigate = Map(InputActionNames.UI.Map).FindAction("Navigate");
            Assert.That(navigate, Is.Not.Null);

            var wasd = new[] { "<Keyboard>/w", "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d" };
            var overlap = navigate.bindings.Select(b => b.path).Intersect(wasd).ToList();

            Assert.That(overlap, Is.Empty, $"UI/Navigate 가 이동키를 물고 있다: {string.Join(", ", overlap)}");
        }
    }
}
