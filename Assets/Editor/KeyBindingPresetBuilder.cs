using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 예시 키 세팅 두 벌을 찍어낸다. 프리셋 표를 어떻게 채우는지 보여 주는 게 목적이다.
    ///
    /// 몇 번을 돌려도 같은 경로를 갱신할 뿐이지만, 손으로 고친 항목은 덮어쓴다.
    /// </summary>
    public static class KeyBindingPresetBuilder
    {
        private const string Folder = "Assets/Settings/InputPresets";

        [MenuItem("Prototype/입력 - 예시 키 프리셋 만들기")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);

            // 항목이 비면 액션 자산의 기본값 그대로다. "되돌리기" 버튼이 쓸 프리셋.
            Write("Preset_Default", "기본", System.Array.Empty<KeyBindingPreset.Entry>());

            var arrows = new List<KeyBindingPreset.Entry>();

            // 이동과 조준을 둘 다 옮겨야 한다. 한쪽만 바꾸면 불릿타임에 들어간 순간
            // 조준이 옛 키로 남아 손가락이 두 군데를 오간다.
            arrows.AddRange(MoveParts("Gameplay", "Move",
                "<Keyboard>/upArrow", "<Keyboard>/downArrow",
                "<Keyboard>/leftArrow", "<Keyboard>/rightArrow"));
            arrows.AddRange(MoveParts("BulletTime", "Navigate",
                "<Keyboard>/upArrow", "<Keyboard>/downArrow",
                "<Keyboard>/leftArrow", "<Keyboard>/rightArrow"));
            arrows.AddRange(MoveParts("BulletTimeSkillShot", "Aim",
                "<Keyboard>/upArrow", "<Keyboard>/downArrow",
                "<Keyboard>/leftArrow", "<Keyboard>/rightArrow"));

            // WASD가 비었으니 고유기를 숫자열로 뺀다. 기획서 원안에 가까운 배치다.
            arrows.Add(Single("Gameplay", "Skill1", "<Keyboard>/1"));
            arrows.Add(Single("Gameplay", "Skill2", "<Keyboard>/2"));
            arrows.Add(Single("Gameplay", "Skill3", "<Keyboard>/3"));
            arrows.Add(Single("Gameplay", "Skill4", "<Keyboard>/4"));

            // 손이 화살표로 옮겨 가면 오른손 JKU는 너무 멀다. 행동키를 전부 왼손으로 당긴다 —
            // 이동만 옮기고 나머지를 두면 양손이 키보드 양 끝으로 벌어진다.
            arrows.Add(Single("Gameplay", "Attack", "<Keyboard>/z"));
            arrows.Add(Single("Gameplay", "Jump", "<Keyboard>/x"));
            arrows.Add(Single("Gameplay", "Dash", "<Keyboard>/c"));
            arrows.Add(Single("Gameplay", "BulletTime", "<Keyboard>/space"));
            arrows.Add(Single("Gameplay", "CardUse", "<Keyboard>/a"));

            // 조준 확정 · 취소도 같이 당긴다. 여기만 JK로 남으면 조준할 때만 오른손이 간다.
            // 평타(Z) · 점프(X)와 키가 겹치는 건 기본 세팅에서 평타와 조준 확정이
            // 둘 다 J인 것과 같다 — 정지 중 평타는 막혀 있어 부딪히지 않는다.
            // part를 비워 두면 키보드 바인딩(0번)만 바뀌고 마우스 클릭은 그대로 남는다.
            arrows.Add(Single("BulletTimeSkillShot", "Confirm", "<Keyboard>/z"));
            arrows.Add(Single("BulletTimeSkillShot", "Cancel", "<Keyboard>/x"));

            Write("Preset_Arrows", "방향키 프리셋", arrows.ToArray());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[KeyBindingPresetBuilder] {Folder} 에 프리셋 2개 생성 완료");
        }

        private static KeyBindingPreset.Entry Single(string map, string action, string path)
            => new KeyBindingPreset.Entry { map = map, action = action, part = "", path = path };

        /// <summary>2DVector 파트 4개를 한 번에 적는다.</summary>
        private static KeyBindingPreset.Entry[] MoveParts(
            string map, string action, string up, string down, string left, string right)
            => new[]
            {
                new KeyBindingPreset.Entry { map = map, action = action, part = "up", path = up },
                new KeyBindingPreset.Entry { map = map, action = action, part = "down", path = down },
                new KeyBindingPreset.Entry { map = map, action = action, part = "left", path = left },
                new KeyBindingPreset.Entry { map = map, action = action, part = "right", path = right },
            };

        private static void Write(string fileName, string displayName, KeyBindingPreset.Entry[] entries)
        {
            string path = $"{Folder}/{fileName}.asset";

            var preset = AssetDatabase.LoadAssetAtPath<KeyBindingPreset>(path);
            bool isNew = preset == null;
            if (isNew) preset = ScriptableObject.CreateInstance<KeyBindingPreset>();

            // 필드가 private이라 SerializedObject로 넣는다. 프리셋을 읽는 쪽은
            // 런타임 코드라 public 세터를 뚫어 줄 이유가 없다.
            var so = new SerializedObject(preset);
            so.FindProperty("displayName").stringValue = displayName;

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

            if (isNew) AssetDatabase.CreateAsset(preset, path);
            else EditorUtility.SetDirty(preset);
        }
    }
}
