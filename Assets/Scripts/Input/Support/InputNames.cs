// 입력 이름표 — 액션 이름 상수 · 화면 표기 · 바인딩 유틸.
// 셋 다 InputActionAsset의 문자열을 다루는 잔손질이다.

using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;

namespace Prototype
{
    // ══ InputActionNames ═══════════════════════════════════════════

    /// <summary>
    /// 액션 자산(<c>Assets/Settings/InputSystem_Actions.inputactions</c>)의 맵 · 액션 이름.
    ///
    /// 문자열을 코드 여기저기에 흩뿌리면 자산에서 이름 하나 바꿨을 때 조용히 죽는다.
    /// 여기로 모아 두고 <c>InputActionAssetTests</c>가 자산과 대조한다.
    /// </summary>
    public static class InputActionNames
    {
        /// <summary>전투 중 항상 켜져 있는 맵. 지휘키도 여기 있다.</summary>
        public static class Gameplay
        {
            public const string Map = "Gameplay";

            public const string Move = "Move";
            public const string Attack = "Attack";
            public const string Jump = "Jump";
            public const string Dash = "Dash";

            /// <summary>
            /// 총공격 슬롯 편집창 열기. 기본 E, 프리셋/리바인딩에 따라 변경.
            /// </summary>
            public const string BulletTime = "BulletTime";

            /// <summary>
            /// 로비 오브젝트 상호작용. 기본 F. E는 불릿타임이라 쓸 수 없다 —
            /// 로비에도 허수아비가 있어 불릿타임이 살아 있다.
            /// </summary>
            public const string Interact = "Interact";
        }

        /// <summary>
        /// 총공격 편집창 조작. 편집창이 열려 있을 때만 켜진다(<c>InputMapSwitcher</c>).
        ///
        /// 액션이 <c>Navigate</c> 하나뿐이다. 리바인드도 컴포지트 하나만 바꾸면 네 방향이 전부 따라온다.
        /// 한 번 누르면 한 칸 가는 <b>이산</b> 입력이다.
        /// </summary>
        public static class BulletTime
        {
            public const string Map = "BulletTime";

            public const string Navigate = "Navigate";
        }

        /// <summary>UI 맵. <c>InputSystemUIInputModule</c>이 쓰는 이름이라 바꾸면 안 된다.</summary>
        public static class UI
        {
            public const string Map = "UI";

            /// <summary>
            /// 모달 안에서 칸을 옮기는 방향. 화살표 · 게임패드만 물려 있다 —
            /// WASD를 여기 넣으면 UI 맵이 늘 켜져 있으므로 전투 중 이동키가 UI 포커스까지 움직인다
            /// (<c>InputActionAssetTests.UINavigate_DoesNotUseWasd</c>가 지킨다).
            /// </summary>
            public const string Navigate = "Navigate";

            /// <summary>
            /// 모달 안에서 지금 칸을 고른다. 바인딩이 용도 태그라 Enter · 게임패드 남쪽이 걸린다 —
            /// 특정 키를 박아 두지 않았으므로 <see cref="Cancel"/>과 같이 리바인드 목록에서 뺀다.
            /// </summary>
            public const string Submit = "Submit";

            public const string Cancel = "Cancel";
        }
    }

    // ══ InputDisplayNames ═══════════════════════════════════════════

    /// <summary>
    /// 설정 화면에 뜨는 이름. 액션 이름을 그대로 보여 주면 "Skill3"이나 "Navigate/up" 같은 게
    /// 줄줄이 뜬다 — 그건 코드가 쓰는 이름이지 사람이 읽을 이름이 아니다.
    /// </summary>
    public static class InputDisplayNames
    {
        private static readonly Dictionary<string, string> Maps = new Dictionary<string, string>
        {
            { InputActionNames.Gameplay.Map, "전투" },
            { InputActionNames.BulletTime.Map, "총공격 편집창" },
        };

        private static readonly Dictionary<string, string> Actions = new Dictionary<string, string>
        {
            { InputActionNames.Gameplay.Move, "이동" },
            { InputActionNames.Gameplay.Attack, "평타" },
            { InputActionNames.Gameplay.Jump, "점프" },
            { InputActionNames.Gameplay.Dash, "대쉬" },
            { InputActionNames.Gameplay.BulletTime, "총공격 편집창 열기" },
            { InputActionNames.Gameplay.Interact, "상호작용 (로비)" },
            { InputActionNames.BulletTime.Navigate, "칸 이동" },
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

    // ══ InputHints ═══════════════════════════════════════════

    /// <summary>
    /// 화면 안내문에 넣을 <b>지금 실제로 눌러야 하는 키</b>. 글자로 박아 두지 않고 매번 묻는다 —
    /// 리바인드 · 프리셋으로 키가 바뀌어도 안내가 따라온다.
    /// </summary>
    public static class InputHints
    {
        /// <summary>그 액션의 키 이름. 컴포지트면 <paramref name="part"/>(up · down · left · right)를 준다. 못 찾으면 "?".</summary>
        public static string Key(string map, string action, string part = null)
        {
            InputActionAsset actions = PlayerInputController.Instance != null ? PlayerInputController.Instance.Actions : null;
            return Key(actions, map, action, part);
        }

        public static string Key(InputActionAsset actions, string map, string action, string part = null)
        {
            InputAction found = InputBindingUtility.FindAction(actions, map, action);
            if (found == null) return "?";

            int index = InputBindingUtility.FindBindingIndex(found, part);
            return index >= 0 ? InputDisplayNames.Key(found, index) : "?";
        }
    }

    // ══ InputBindingUtility ═══════════════════════════════════════════

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

        private static readonly string[] CompositeParts = { "up", "down", "left", "right" };

        /// <summary>
        /// 편집창 칸 이동(<c>BulletTime/Navigate</c>)을 이동(<c>Gameplay/Move</c>) 키에 그대로 맞춘다.
        /// 칸 이동 키는 따로 바꾸지 않는다 — 이동키를 바꾸면 같이 따라간다.
        /// 오버라이드를 적용 · 불러오는 모든 자리 뒤에 부른다.
        /// </summary>
        public static void SyncNavigateToMove(InputActionAsset actions)
        {
            InputAction move = FindAction(actions, InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Move);
            InputAction navigate = FindAction(actions, InputActionNames.BulletTime.Map, InputActionNames.BulletTime.Navigate);
            if (move == null || navigate == null) return;

            foreach (string part in CompositeParts)
            {
                int from = FindBindingIndex(move, part);
                int to = FindBindingIndex(navigate, part);
                if (from < 0 || to < 0) continue;

                navigate.ApplyBindingOverride(to, move.bindings[from].effectivePath);
            }
        }

        /// <summary>
        /// 액션을 이름으로 찾는다. 맵 이름까지 맞아야 한다 —
        /// Gameplay/Move와 BulletTime/Navigate처럼 같은 키를 쓰는 액션이 여러 맵에 있다.
        /// </summary>
        public static InputAction FindAction(InputActionAsset actions, string mapName, string actionName)
        {
            InputActionMap map = actions != null ? actions.FindActionMap(mapName) : null;
            return map != null ? map.FindAction(actionName) : null;
        }
    }
}
