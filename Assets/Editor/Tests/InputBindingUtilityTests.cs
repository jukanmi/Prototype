using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype.Tests
{
    /// <summary>
    /// 바인딩 인덱스 찾기. 프리셋과 런타임 리바인드가 같은 규칙으로 같은 자리를 가리켜야
    /// "프리셋으로 바꾼 키"와 "직접 바꾼 키"가 어긋나지 않는다.
    /// </summary>
    public class InputBindingUtilityTests
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

        private InputAction Move => InputBindingUtility.FindAction(actions, "Gameplay", "Move");
        private InputAction Attack => InputBindingUtility.FindAction(actions, "Gameplay", "Attack");

        [Test]
        public void FindAction_NeedsTheRightMap()
        {
            // Move · Navigate · Aim이 셋 다 같은 WASD를 쓴다. 맵을 안 따지면 엉뚱한 쪽을 고친다.
            Assert.That(InputBindingUtility.FindAction(actions, "Gameplay", "Aim"), Is.Null);
            Assert.That(InputBindingUtility.FindAction(actions, "BulletTime", "Aim"), Is.Null);
            Assert.That(InputBindingUtility.FindAction(actions, "BulletTimeSkillShot", "Aim"), Is.Not.Null);
            Assert.That(InputBindingUtility.FindAction(actions, "BulletTime", "Navigate"), Is.Not.Null);
        }

        [Test]
        public void FindBindingIndex_SingleBinding()
        {
            int i = InputBindingUtility.FindBindingIndex(Attack, "");

            Assert.That(i, Is.GreaterThanOrEqualTo(0));
            Assert.That(Attack.bindings[i].path, Is.EqualTo("<Keyboard>/j"));
        }

        [TestCase("up", "<Keyboard>/w")]
        [TestCase("down", "<Keyboard>/s")]
        [TestCase("left", "<Keyboard>/a")]
        [TestCase("right", "<Keyboard>/d")]
        public void FindBindingIndex_CompositePart(string part, string expected)
        {
            int i = InputBindingUtility.FindBindingIndex(Move, part);

            Assert.That(i, Is.GreaterThanOrEqualTo(0));
            Assert.That(Move.bindings[i].path, Is.EqualTo(expected));
        }

        [Test]
        public void FindBindingIndex_IgnoresPartCase()
        {
            // 인스펙터에서 손으로 고치면 대소문자가 흔들린다.
            Assert.That(InputBindingUtility.FindBindingIndex(Move, "UP"),
                Is.EqualTo(InputBindingUtility.FindBindingIndex(Move, "up")));
        }

        [Test]
        public void FindBindingIndex_WithoutPart_SkipsCompositeParts()
        {
            // Move는 컴포지트뿐이라 "파트 없는 바인딩"이 아예 없다.
            // 여기서 0을 돌려주면 컴포지트 머리를 덮어써 액션이 통째로 깨진다.
            Assert.That(InputBindingUtility.FindBindingIndex(Move, ""), Is.EqualTo(-1));
        }

        [Test]
        public void FindBindingIndex_UnknownPart_ReturnsMinusOne()
        {
            Assert.That(InputBindingUtility.FindBindingIndex(Move, "diagonal"), Is.EqualTo(-1));
        }

        [Test]
        public void RebindableIndices_ExcludeCompositeHead()
        {
            var indices = InputBindingUtility.RebindableIndices(Move).ToList();

            // 리바인드 UI가 컴포지트 머리("2DVector")를 줄로 뽑으면
            // 사용자가 키를 넣을 수 없는 항목이 하나 뜬다.
            Assert.That(indices.Count, Is.EqualTo(4));
            Assert.That(indices.All(i => Move.bindings[i].isPartOfComposite), Is.True);
        }

        [Test]
        public void RebindableIndices_HandlesSingleBinding()
        {
            Assert.That(InputBindingUtility.RebindableIndices(Attack).ToList(), Has.Count.EqualTo(1));
        }
    }
}
