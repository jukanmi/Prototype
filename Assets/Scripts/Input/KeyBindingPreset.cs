using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype
{
    /// <summary>
    /// 통째로 갈아끼우는 키 세팅 한 벌.
    ///
    /// 바인딩 오버라이드 JSON을 통으로 들고 있지 않고 <b>항목 표</b>로 적는 이유는
    /// 인스펙터에서 사람이 읽고 고칠 수 있어야 하기 때문이다. JSON 뭉치는 바인딩 순서가
    /// 바뀌는 순간 어디를 가리키는지 알 수 없게 된다.
    ///
    /// 사용자가 직접 바꾼 키는 프리셋 위에 얹힌다 — 프리셋을 적용하면 기존 오버라이드가
    /// 전부 지워지므로, 순서는 <b>프리셋 먼저, 개인 설정 나중</b>이다.
    /// </summary>
    [CreateAssetMenu(fileName = "KeyBindingPreset", menuName = "Prototype/키 바인딩 프리셋")]
    public class KeyBindingPreset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Gameplay · BulletTime · UI")]
            public string map;

            [Tooltip("Move · Attack · Skill1 …")]
            public string action;

            [Tooltip("2DVector 컴포지트의 파트(up · down · left · right). 단일 바인딩이면 비운다.")]
            public string part;

            [Tooltip("<Keyboard>/upArrow 같은 컨트롤 경로.")]
            public string path;
        }

        [Tooltip("설정 화면에 뜰 이름. 비우면 애셋 이름을 쓴다.")]
        [SerializeField] private string displayName;

        [Tooltip("비어 있으면 액션 자산의 기본 키 그대로라는 뜻이다.")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public Entry[] Entries => entries;

        /// <summary>
        /// 기존 오버라이드를 <b>전부 지우고</b> 이 프리셋을 얹는다.
        /// 지우지 않으면 이전 프리셋의 키가 이 프리셋이 안 건드리는 액션에 남는다.
        /// </summary>
        /// <returns>실제로 적용된 항목 수. 표의 길이와 다르면 자산과 어긋난 항목이 있다는 뜻이다.</returns>
        public int ApplyTo(InputActionAsset actions)
        {
            if (actions == null) return 0;

            actions.RemoveAllBindingOverrides();

            int applied = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (ApplyEntry(actions, entries[i])) applied++;
            }

            return applied;
        }

        private static bool ApplyEntry(InputActionAsset actions, Entry entry)
        {
            InputAction action = InputBindingUtility.FindAction(actions, entry.map, entry.action);
            if (action == null)
            {
                Debug.LogWarning($"[KeyBindingPreset] 액션을 못 찾았다: {entry.map}/{entry.action}");
                return false;
            }

            int index = InputBindingUtility.FindBindingIndex(action, entry.part);
            if (index < 0)
            {
                Debug.LogWarning(
                    $"[KeyBindingPreset] 바인딩을 못 찾았다: {entry.map}/{entry.action}" +
                    (string.IsNullOrEmpty(entry.part) ? "" : $" [{entry.part}]"));
                return false;
            }

            action.ApplyBindingOverride(index, entry.path);
            return true;
        }
    }
}
