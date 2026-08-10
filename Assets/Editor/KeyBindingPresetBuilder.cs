using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 키 세팅 세 벌을 찍어낸다 — 기본 · 방향키 · 마우스 + 키보드.
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

            BuildMouseKeyboard();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[KeyBindingPresetBuilder] {Folder} 에 프리셋 3개 생성 완료");
        }

        /// <summary>
        /// 왼손은 WASD에 두고 공격 · 대쉬를 마우스로 넘긴 배치.
        ///
        /// 이동 계열을 기본값과 같은 WASD로 <b>명시해서</b> 적는다. 안 적으면 액션 자산의
        /// 기본값을 그대로 따라가는데, 나중에 그 기본값이 화살표로 바뀌면 이 프리셋이
        /// 이름과 다른 배치가 된다.
        /// </summary>
        private static void BuildMouseKeyboard()
        {
            var mouse = new List<KeyBindingPreset.Entry>();

            mouse.AddRange(MoveParts("Gameplay", "Move",
                "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d"));
            mouse.AddRange(MoveParts("BulletTime", "Navigate",
                "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d"));
            mouse.AddRange(MoveParts("BulletTimeSkillShot", "Aim",
                "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d"));

            // 평타와 대쉬를 마우스로 넘긴다. 조준 확정 · 취소가 이미 좌 · 우클릭을 쓰고 있지만
            // 그쪽은 정지 중에만 살아 있고 평타 · 대쉬는 그때 IsFrozen에 막히므로 부딪히지 않는다.
            mouse.Add(Single("Gameplay", "Attack", "<Mouse>/leftButton"));
            mouse.Add(Single("Gameplay", "Dash", "<Mouse>/rightButton"));

            mouse.Add(Single("Gameplay", "Jump", "<Keyboard>/space"));
            mouse.Add(Single("Gameplay", "BulletTime", "<Keyboard>/e"));
            mouse.Add(Single("Gameplay", "CardUse", "<Keyboard>/q"));

            // 조준 확정 · 취소의 키보드 자리(J · K)도 클릭으로 덮는다.
            // 좌 · 우클릭은 원래 이 액션의 두 번째 바인딩으로 늘 붙어 있어서 기능은 이미 되지만,
            // 리바인드 목록에는 키보드 자리만 뜬다 — 덮지 않으면 화면에 "확정 J"라고 남아
            // 마우스로 조준하는 배치인데 설명이 딴소리를 한다.
            mouse.Add(Single("BulletTimeSkillShot", "Confirm", "<Mouse>/leftButton"));
            mouse.Add(Single("BulletTimeSkillShot", "Cancel", "<Mouse>/rightButton"));

            // 동료 고유기(ZXCV)는 손대지 않는다 — 이 배치가 정하려는 건 이동과 행동키다.
            Write("Preset_MouseKeyboard", "마우스 + 키보드", mouse.ToArray());
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
