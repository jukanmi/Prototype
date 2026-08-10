using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Prototype.Tests
{
    /// <summary>
    /// 프리셋이 실제로 키를 갈아끼우는지, 그리고 갈아끼운 뒤 되돌아오는지 본다.
    /// 되돌아오지 않으면 프리셋을 한 번 눌러 본 사용자가 기본값으로 못 돌아간다.
    /// </summary>
    public class KeyBindingPresetTests
    {
        private const string AssetPath = "Assets/Settings/InputSystem_Actions.inputactions";

        private InputActionAsset actions;
        private KeyBindingPreset preset;

        [SetUp]
        public void SetUp()
        {
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(source, Is.Not.Null, $"{AssetPath} 를 못 찾았다.");

            // 프로젝트 자산에 오버라이드 흔적을 남기지 않도록 복제해서 쓴다.
            actions = Object.Instantiate(source);
            preset = ScriptableObject.CreateInstance<KeyBindingPreset>();
        }

        [TearDown]
        public void TearDown()
        {
            if (actions != null) Object.DestroyImmediate(actions);
            if (preset != null) Object.DestroyImmediate(preset);

            LogAssert.ignoreFailingMessages = false;
        }

        private void SetEntries(params KeyBindingPreset.Entry[] entries)
        {
            var so = new SerializedObject(preset);
            SerializedProperty list = so.FindProperty("entries");

            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("map").stringValue = entries[i].map;
                e.FindPropertyRelative("action").stringValue = entries[i].action;
                e.FindPropertyRelative("part").stringValue = entries[i].part;
                e.FindPropertyRelative("path").stringValue = entries[i].path;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private InputAction Action(string map, string name)
            => InputBindingUtility.FindAction(actions, map, name);

        private string EffectivePath(string map, string action, string part)
        {
            InputAction a = Action(map, action);
            int i = InputBindingUtility.FindBindingIndex(a, part);
            return a.bindings[i].effectivePath;
        }

        // ── 단일 바인딩 ─────────────────────────────────────

        [Test]
        public void ApplyTo_OverridesSingleBinding()
        {
            SetEntries(new KeyBindingPreset.Entry
            {
                map = "Gameplay", action = "Attack", part = "", path = "<Keyboard>/1",
            });

            Assert.That(preset.ApplyTo(actions), Is.EqualTo(1));
            Assert.That(EffectivePath("Gameplay", "Attack", ""), Is.EqualTo("<Keyboard>/1"));
        }

        // ── 컴포지트 파트 ───────────────────────────────────

        [Test]
        public void ApplyTo_OverridesCompositePart()
        {
            SetEntries(new KeyBindingPreset.Entry
            {
                map = "Gameplay", action = "Move", part = "up", path = "<Keyboard>/upArrow",
            });

            Assert.That(preset.ApplyTo(actions), Is.EqualTo(1));

            // up만 바뀌고 나머지 파트는 그대로여야 한다.
            Assert.That(EffectivePath("Gameplay", "Move", "up"), Is.EqualTo("<Keyboard>/upArrow"));
            Assert.That(EffectivePath("Gameplay", "Move", "down"), Is.EqualTo("<Keyboard>/s"));
            Assert.That(EffectivePath("Gameplay", "Move", "left"), Is.EqualTo("<Keyboard>/a"));
            Assert.That(EffectivePath("Gameplay", "Move", "right"), Is.EqualTo("<Keyboard>/d"));
        }

        // ── 되돌리기 ────────────────────────────────────────

        [Test]
        public void EmptyPreset_RestoresDefaults()
        {
            SetEntries(new KeyBindingPreset.Entry
            {
                map = "Gameplay", action = "Attack", part = "", path = "<Keyboard>/1",
            });
            preset.ApplyTo(actions);

            // 항목이 빈 프리셋이 "기본으로 되돌리기"다. 앞선 오버라이드가 남으면 안 된다.
            SetEntries();
            Assert.That(preset.ApplyTo(actions), Is.EqualTo(0));

            Assert.That(EffectivePath("Gameplay", "Attack", ""), Is.EqualTo("<Keyboard>/j"));
        }

        [Test]
        public void ApplyTo_ClearsPreviousPresetsLeftovers()
        {
            SetEntries(new KeyBindingPreset.Entry
            {
                map = "Gameplay", action = "Jump", part = "", path = "<Keyboard>/1",
            });
            preset.ApplyTo(actions);

            // 다음 프리셋이 Jump를 안 건드린다. 이전 값이 남으면 두 프리셋이 섞인다.
            SetEntries(new KeyBindingPreset.Entry
            {
                map = "Gameplay", action = "Attack", part = "", path = "<Keyboard>/2",
            });
            preset.ApplyTo(actions);

            Assert.That(EffectivePath("Gameplay", "Jump", ""), Is.EqualTo("<Keyboard>/k"));
            Assert.That(EffectivePath("Gameplay", "Attack", ""), Is.EqualTo("<Keyboard>/2"));
        }

        // ── 자산과 어긋난 항목 ──────────────────────────────

        [Test]
        public void UnknownAction_IsReportedNotApplied()
        {
            SetEntries(new KeyBindingPreset.Entry
            {
                map = "Gameplay", action = "존재하지않는액션", part = "", path = "<Keyboard>/1",
            });

            // 경고 로그는 의도된 것이라 테스트 실패로 세지 않는다.
            LogAssert.ignoreFailingMessages = true;

            // 적용 수가 표 길이보다 적으면 프리셋이 자산과 어긋났다는 뜻이다.
            Assert.That(preset.ApplyTo(actions), Is.EqualTo(0));
        }

        [Test]
        public void UnknownCompositePart_IsReportedNotApplied()
        {
            SetEntries(new KeyBindingPreset.Entry
            {
                map = "Gameplay", action = "Move", part = "diagonal", path = "<Keyboard>/1",
            });

            LogAssert.ignoreFailingMessages = true;

            Assert.That(preset.ApplyTo(actions), Is.EqualTo(0));
        }

        // ── 예시 프리셋이 자산과 맞는가 ─────────────────────

        [Test]
        public void ShippedPresets_MatchTheActionAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:KeyBindingPreset");
            if (guids.Length == 0) Assert.Ignore("프리셋 애셋이 아직 없다.");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var shipped = AssetDatabase.LoadAssetAtPath<KeyBindingPreset>(path);
                if (shipped == null || shipped.Entries.Length == 0) continue;

                // 항목이 전부 붙어야 한다. 하나라도 못 붙으면 액션 이름이 바뀐 것이다.
                Assert.That(shipped.ApplyTo(actions), Is.EqualTo(shipped.Entries.Length),
                    $"{path} 의 항목 일부가 자산과 어긋난다: " +
                    string.Join(", ", shipped.Entries.Select(e => $"{e.map}/{e.action}")));
            }
        }

        [Test]
        public void ShippedPresets_ProduceNoConflicts()
        {
            // 프리셋은 항목을 직접 얹으므로 리바인드 UI의 충돌 검사를 거치지 않는다.
            // 프리셋 하나가 같은 키를 두 자리에 넣어도 아무도 안 잡는다 — 여기가 그 그물이다.
            string[] guids = AssetDatabase.FindAssets("t:KeyBindingPreset");
            if (guids.Length == 0) Assert.Ignore("프리셋 애셋이 아직 없다.");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var shipped = AssetDatabase.LoadAssetAtPath<KeyBindingPreset>(path);
                if (shipped == null) continue;

                shipped.ApplyTo(actions);

                foreach ((InputAction action, int index) in InputRebindRules.Enumerate(actions).ToList())
                {
                    string effective = action.bindings[index].effectivePath;

                    bool conflict = InputRebindRules.TryFindConflict(actions, action, index, effective,
                        out InputAction blocker, out int blockerIndex);

                    Assert.That(conflict, Is.False,
                        $"{path} 적용 후 충돌: {action.actionMap.name}/{action.name}[{index}] " +
                        $"↔ {blocker?.actionMap.name}/{blocker?.name}[{blockerIndex}] — {effective}");
                }
            }
        }
    }
}
