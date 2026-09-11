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
            /// 불릿타임 진입 · 실행. 기본 E.
            ///
            /// 예전엔 Execute(Space)를 따로 뒀는데, Order 페이즈에서 둘 다 Resolve로 가는
            /// 같은 동작이라 리바인드 화면에서 서로 다른 기능처럼 보였다. 하나로 합쳤다.
            /// </summary>
            public const string BulletTime = "BulletTime";

            public const string CardUse = "CardUse";

            /// <summary>
            /// 동료 교대. 실시간 전투에는 동료가 한 명만 서 있고 이 키가 다음 생존자로 돌린다.
            ///
            /// 지휘키가 <b>아니다</b> — 정지 중에는 안 먹어야 한다. 그래서
            /// <c>InputRebindRules.CommanderActions</c>에 넣지 않는다. 넣으면 불릿타임 맵의
            /// WASD와 겹친다고 잡힌다.
            /// </summary>
            public const string Swap = "Swap";
        }

        /// <summary>
        /// 손패 카드 조작. Order 페이즈이면서 <b>조준 중이 아닐 때</b>만 켜진다.
        ///
        /// 액션이 <c>Navigate</c> 하나뿐이다. 집기 · 놓기까지 방향에 실어 두면
        /// 한 키가 두 액션을 동시에 발동하는 일이 원천적으로 없고,
        /// 리바인드도 컴포지트 하나만 바꾸면 네 방향이 전부 따라온다.
        ///
        /// 한 번 누르면 한 칸 가는 <b>이산</b> 입력이다 — 같은 WASD라도
        /// 조준의 연속 이동과 성격이 달라 맵을 나눴다.
        /// </summary>
        public static class BulletTime
        {
            public const string Map = "BulletTime";

            public const string Navigate = "Navigate";
        }

        /// <summary>
        /// 스킬 시전 위치 지정. 카드를 집은 상태에서 위(<c>Navigate.up</c>)로 들어온다.
        /// 확정 · 취소 후 다시 <see cref="BulletTime"/> 으로 돌아간다.
        /// </summary>
        public static class BulletTimeSkillShot
        {
            public const string Map = "BulletTimeSkillShot";

            public const string Aim = "Aim";

            /// <summary>마우스 커서의 화면 좌표.</summary>
            public const string AimPoint = "AimPoint";

            /// <summary>
            /// 마우스 이동량. <see cref="AimPoint"/>의 프레임 차분으로 대신하면 안 된다 —
            /// 맵을 켠 첫 프레임에 position이 0을 뱉어서, 실제 좌표가 들어오는 다음 프레임을
            /// "화면 절반만큼 움직였다"로 읽는다.
            /// </summary>
            public const string AimDelta = "AimDelta";

            public const string Confirm = "Confirm";
            public const string Cancel = "Cancel";
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
            { InputActionNames.BulletTime.Map, "불릿타임 — 카드 조작" },
            { InputActionNames.BulletTimeSkillShot.Map, "불릿타임 — 시전 위치" },
        };

        private static readonly Dictionary<string, string> Actions = new Dictionary<string, string>
        {
            { InputActionNames.Gameplay.Move, "이동" },
            { InputActionNames.Gameplay.Attack, "평타" },
            { InputActionNames.Gameplay.Jump, "점프" },
            { InputActionNames.Gameplay.Dash, "대쉬" },
            { InputActionNames.Gameplay.BulletTime, "불릿타임 진입 · 실행" },
            { InputActionNames.Gameplay.CardUse, "카드 즉시 사용" },
            { InputActionNames.Gameplay.Swap, "동료 교대" },
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
