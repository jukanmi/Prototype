using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype.Tests
{
    /// <summary>
    /// 바꾼 키가 저장되고 다음 실행에 되살아나는지.
    ///
    /// 실제 키 입력을 흘려보내는 경로는 없다 — <c>PerformInteractiveRebinding</c> 은
    /// 디바이스 이벤트가 있어야 하고, 그건 <c>InputTestFixture</c>(별도 어셈블리) 없이는 못 만든다.
    /// 그래서 여기서는 저장 · 불러오기 · 되돌리기의 왕복만 본다.
    /// </summary>
    public class RebindPersistenceTests
    {
        private const string AssetPath = "Assets/Settings/InputSystem_Actions.inputactions";

        private InputActionAsset actions;
        private RebindManager manager;
        private string savedPrefs;
        private bool hadPrefs;

        [SetUp]
        public void SetUp()
        {
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(source, Is.Not.Null, $"{AssetPath} 를 못 찾았다.");

            actions = Object.Instantiate(source);
            manager = new RebindManager(actions);

            // 테스트가 사용자의 실제 설정을 날리면 안 된다. 원래 값을 들고 있다가 되돌린다.
            hadPrefs = PlayerPrefs.HasKey(RebindManager.PrefsKey);
            savedPrefs = hadPrefs ? PlayerPrefs.GetString(RebindManager.PrefsKey) : null;
            PlayerPrefs.DeleteKey(RebindManager.PrefsKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (actions != null) Object.DestroyImmediate(actions);

            if (hadPrefs) PlayerPrefs.SetString(RebindManager.PrefsKey, savedPrefs);
            else PlayerPrefs.DeleteKey(RebindManager.PrefsKey);

            PlayerPrefs.Save();
        }

        private InputAction Attack
            => InputBindingUtility.FindAction(actions, InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Attack);

        private InputAction Move
            => InputBindingUtility.FindAction(actions, InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Move);

        private static string EffectivePath(InputAction action, string part)
        {
            int i = InputBindingUtility.FindBindingIndex(action, part);
            return action.bindings[i].effectivePath;
        }

        [Test]
        public void Save_ThenLoadIntoAFreshAsset_RestoresTheKey()
        {
            Attack.ApplyBindingOverride(InputBindingUtility.FindBindingIndex(Attack, ""), "<Keyboard>/1");
            manager.Save();

            // 새로 켠 게임을 흉내 낸다.
            var fresh = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath));
            try
            {
                RebindManager.Load(fresh);

                InputAction attack = InputBindingUtility.FindAction(
                    fresh, InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Attack);

                Assert.That(EffectivePath(attack, ""), Is.EqualTo("<Keyboard>/1"));
            }
            finally
            {
                Object.DestroyImmediate(fresh);
            }
        }

        [Test]
        public void Save_KeepsCompositeParts()
        {
            // 컴포지트 파트는 인덱스로만 구분된다. 저장 포맷이 그걸 잃으면
            // 이동 방향이 통째로 기본값으로 돌아간다.
            Move.ApplyBindingOverride(InputBindingUtility.FindBindingIndex(Move, "up"), "<Keyboard>/upArrow");
            manager.Save();

            var fresh = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath));
            try
            {
                RebindManager.Load(fresh);

                InputAction move = InputBindingUtility.FindAction(
                    fresh, InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Move);

                Assert.That(EffectivePath(move, "up"), Is.EqualTo("<Keyboard>/upArrow"));
                Assert.That(EffectivePath(move, "down"), Is.EqualTo("<Keyboard>/s"));
            }
            finally
            {
                Object.DestroyImmediate(fresh);
            }
        }

        [Test]
        public void ResetAll_ClearsTheAssetAndTheSavedCopy()
        {
            Attack.ApplyBindingOverride(InputBindingUtility.FindBindingIndex(Attack, ""), "<Keyboard>/1");
            manager.Save();

            manager.ResetAll();

            Assert.That(EffectivePath(Attack, ""), Is.EqualTo("<Keyboard>/j"));

            // 저장본이 남으면 다음 실행에 바꾼 키가 되살아난다 — 되돌린 게 아니게 된다.
            Assert.That(PlayerPrefs.HasKey(RebindManager.PrefsKey), Is.False);
        }

        [Test]
        public void ApplyPreset_SavesImmediately()
        {
            var preset = ScriptableObject.CreateInstance<KeyBindingPreset>();
            try
            {
                var so = new SerializedObject(preset);
                SerializedProperty list = so.FindProperty("entries");
                list.arraySize = 1;

                SerializedProperty e = list.GetArrayElementAtIndex(0);
                e.FindPropertyRelative("map").stringValue = InputActionNames.Gameplay.Map;
                e.FindPropertyRelative("action").stringValue = InputActionNames.Gameplay.Attack;
                e.FindPropertyRelative("part").stringValue = "";
                e.FindPropertyRelative("path").stringValue = "<Keyboard>/2";
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(manager.ApplyPreset(preset), Is.EqualTo(1));
                Assert.That(EffectivePath(Attack, ""), Is.EqualTo("<Keyboard>/2"));

                // 프리셋을 고르고 게임을 껐다 켜면 그대로 남아 있어야 한다.
                Assert.That(PlayerPrefs.GetString(RebindManager.PrefsKey, ""), Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void NoSavedKeys_LeavesDefaultsAlone()
        {
            RebindManager.Load(actions);

            Assert.That(EffectivePath(Attack, ""), Is.EqualTo("<Keyboard>/j"));
        }

        [Test]
        public void Load_ClearsStaleOverridesFirst()
        {
            // 에디터에서는 자산이 인스턴스 하나뿐이라 지난 플레이의 오버라이드가 메모리에 남는다.
            // 저장본이 없는데 그게 살아 있으면 지운 적 없는 키가 되살아난 것처럼 보인다.
            Attack.ApplyBindingOverride(InputBindingUtility.FindBindingIndex(Attack, ""), "<Keyboard>/1");

            RebindManager.Load(actions);

            Assert.That(EffectivePath(Attack, ""), Is.EqualTo("<Keyboard>/j"));
        }
    }
}
