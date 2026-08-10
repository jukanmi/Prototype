using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Prototype
{
    /// <summary>
    /// 설정 화면에 뜨는 이름. 액션 이름을 그대로 보여 주면 "Skill3"이나 "Navigate/up" 같은 게
    /// 줄줄이 뜬다 — 그건 코드가 쓰는 이름이지 사람이 읽을 이름이 아니다.
    /// </summary>
    public static class InputDisplayNames
    {
        private static readonly Dictionary<string, string> Maps = new Dictionary<string, string>
        {
            { InputActionNames.Gameplay.Map, "전투" },
            { InputActionNames.BulletTime.Map, "불릿타임 — 카드 조작" },
            { InputActionNames.BulletTimeSkillShot.Map, "불릿타임 — 시전 위치" },
        };

        private static readonly Dictionary<string, string> Actions = new Dictionary<string, string>
        {
            { InputActionNames.Gameplay.Move, "이동" },
            { InputActionNames.Gameplay.Attack, "평타" },
            { InputActionNames.Gameplay.Jump, "점프" },
            { InputActionNames.Gameplay.Dash, "대쉬" },
            { "Skill1", "동료 고유기 1" },
            { "Skill2", "동료 고유기 2" },
            { "Skill3", "동료 고유기 3" },
            { "Skill4", "동료 고유기 4" },
            { InputActionNames.Gameplay.BulletTime, "불릿타임 진입 · 실행" },
            { InputActionNames.Gameplay.CardUse, "카드 즉시 사용" },
            { InputActionNames.BulletTime.Navigate, "카드 선택 · 순서" },
            { InputActionNames.BulletTimeSkillShot.Aim, "조준점 이동" },
            { InputActionNames.BulletTimeSkillShot.Confirm, "확정" },
            { InputActionNames.BulletTimeSkillShot.Cancel, "취소" },
        };

        private static readonly Dictionary<string, string> Parts = new Dictionary<string, string>
        {
            { "up", "위" },
            { "down", "아래" },
            { "left", "왼쪽" },
            { "right", "오른쪽" },
        };

        public static string Map(string mapName)
            => Maps.TryGetValue(mapName, out string label) ? label : mapName;

        public static string Action(string actionName)
            => Actions.TryGetValue(actionName, out string label) ? label : actionName;

        /// <summary>한 줄에 뜰 이름. 컴포지트면 "이동 · 위"처럼 파트를 붙인다.</summary>
        public static string Binding(InputAction action, int bindingIndex)
        {
            string label = Action(action.name);

            InputBinding binding = action.bindings[bindingIndex];
            if (!binding.isPartOfComposite) return label;

            string part = binding.name ?? string.Empty;
            if (Parts.TryGetValue(part.ToLowerInvariant(), out string partLabel)) part = partLabel;

            return $"{label} · {part}";
        }

        /// <summary>지금 눌러야 하는 키. 오버라이드가 걸려 있으면 바뀐 쪽이 나온다.</summary>
        public static string Key(InputAction action, int bindingIndex)
        {
            string display = action.GetBindingDisplayString(bindingIndex);
            return string.IsNullOrEmpty(display) ? "—" : display;
        }
    }
}
