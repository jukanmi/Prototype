using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Prototype
{
    /// <summary>
    /// 바인딩 인덱스를 다루는 공용 조각. 프리셋 적용과 런타임 리바인드가 같은 규칙을 써야
    /// "프리셋으로 바꾼 키"와 "직접 바꾼 키"가 같은 자리를 가리킨다.
    /// </summary>
    public static class InputBindingUtility
    {
        /// <summary>
        /// 실제로 바꿀 수 있는 바인딩의 인덱스. <b>컴포지트 머리는 뺀다</b> —
        /// 그건 "2DVector"라는 타입 이름일 뿐이라 키를 넣을 자리가 아니다.
        /// 파트(up · down · left · right)는 각각 따로 나온다.
        /// </summary>
        public static IEnumerable<int> RebindableIndices(InputAction action)
        {
            if (action == null) yield break;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].isComposite) continue;
                yield return i;
            }
        }

        /// <summary>
        /// 바꿀 바인딩의 인덱스를 찾는다. 반환값은 <see cref="InputAction.bindings"/> 기준이라
        /// 그대로 <c>ApplyBindingOverride</c>에 넣을 수 있다. 없으면 -1.
        /// </summary>
        /// <param name="part">
        /// 컴포지트 파트 이름(up · down · left · right). 단일 바인딩이면 비운다.
        /// 대소문자는 가리지 않는다 — 인스펙터에서 손으로 고치면 흔들리는 값이다.
        /// </param>
        public static int FindBindingIndex(InputAction action, string part)
        {
            if (action == null) return -1;

            bool wantsPart = !string.IsNullOrEmpty(part);

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite) continue;

                if (wantsPart)
                {
                    if (binding.isPartOfComposite &&
                        string.Equals(binding.name, part, StringComparison.OrdinalIgnoreCase))
                        return i;

                    continue;
                }

                // 파트를 안 줬으면 컴포지트가 아닌 첫 바인딩을 고른다.
                if (!binding.isPartOfComposite) return i;
            }

            return -1;
        }

        /// <summary>
        /// 액션을 이름으로 찾는다. 맵 이름까지 맞아야 한다 —
        /// Gameplay/Move와 BulletTime/Aim처럼 같은 키를 쓰는 액션이 여러 맵에 있다.
        /// </summary>
        public static InputAction FindAction(InputActionAsset actions, string mapName, string actionName)
        {
            InputActionMap map = actions != null ? actions.FindActionMap(mapName) : null;
            return map != null ? map.FindAction(actionName) : null;
        }
    }
}
