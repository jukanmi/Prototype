using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Prototype
{
    /// <summary>
    /// 무엇을 바꿀 수 있고, 무엇끼리 충돌인지.
    ///
    /// 충돌 판정이 이 파일의 핵심이다. "같이 켜져 있는 맵끼리 겹치면 충돌"로 잡으면
    /// <b>기본 바인딩부터 충돌로 뜬다</b> — Gameplay/Move · BulletTime/Navigate ·
    /// SkillShot/Aim이 셋 다 WASD다. 실제로는 이렇게 갈린다:
    ///
    /// <list type="bullet">
    /// <item>BulletTime과 SkillShot은 <see cref="InputMapSwitcher"/>가 배타로 잡는다 — 절대 안 겹친다.</item>
    /// <item>Gameplay의 이동 · 평타는 정지 중 <c>IsFrozen</c> 게이트에 막힌다 — 카드 조작과 안 겹친다.</item>
    /// <item>단 <b>지휘키(E · Space · U)는 정지 중에도 산다</b> — 이것만 불릿타임 맵과 진짜로 겹친다.</item>
    /// </list>
    /// </summary>
    public static class InputRebindRules
    {
        /// <summary>정지 중에도 먹는 Gameplay 액션. 나머지는 조종사(PlayerPilot)의 게이트에 막힌다.</summary>
        private static readonly HashSet<string> CommanderActions = new HashSet<string>
        {
            InputActionNames.Gameplay.BulletTime,
            InputActionNames.Gameplay.CardUse,
        };

        /// <summary>키로 바꿀 수 있는 값이 아닌 액션. 포인터 위치 · 이동량.</summary>
        private static readonly HashSet<string> PointerActions = new HashSet<string>
        {
            InputActionNames.BulletTimeSkillShot.AimPoint,
            InputActionNames.BulletTimeSkillShot.AimDelta,
        };

        /// <summary>
        /// UI 맵은 목록에서 뺀다. <c>InputSystemUIInputModule</c>이 이름으로 찾아 쓰는 규약이고,
        /// <c>Cancel</c>은 <c>*/{Cancel}</c> 용도 바인딩이라 특정 키로 바꿀 자리가 아니다.
        /// </summary>
        public static bool IsRebindableMap(string mapName) => mapName != InputActionNames.UI.Map;

        public static bool IsRebindableAction(InputAction action)
            => action != null
               && IsRebindableMap(action.actionMap.name)
               && !PointerActions.Contains(action.name);

        /// <summary>
        /// 마우스 바인딩은 고정한다. 조준 확정의 좌클릭까지 바꾸게 하면
        /// 키보드 자리를 잘못 잡았을 때 빠져나올 길이 없어진다.
        /// </summary>
        public static bool IsRebindableBinding(InputAction action, int bindingIndex)
        {
            if (!IsRebindableAction(action)) return false;
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) return false;

            InputBinding binding = action.bindings[bindingIndex];
            if (binding.isComposite) return false;

            return !IsPointerPath(binding.path);
        }

        private static bool IsPointerPath(string path)
            => !string.IsNullOrEmpty(path)
               && (path.StartsWith("<Mouse>") || path.StartsWith("<Pointer>"));

        /// <summary>설정 화면에 줄로 뜨는 것 전부. 맵 순서 · 바인딩 순서를 그대로 따른다.</summary>
        public static IEnumerable<(InputAction action, int bindingIndex)> Enumerate(InputActionAsset actions)
        {
            if (actions == null) yield break;

            foreach (InputActionMap map in actions.actionMaps)
            {
                if (!IsRebindableMap(map.name)) continue;

                foreach (InputAction action in map.actions)
                {
                    if (!IsRebindableAction(action)) continue;

                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        if (IsRebindableBinding(action, i)) yield return (action, i);
                    }
                }
            }
        }

        /// <summary>두 액션이 한 프레임에 함께 발동할 수 있는가. 충돌 판정의 근거다.</summary>
        public static bool CanFireTogether(InputAction a, InputAction b)
        {
            if (a == null || b == null) return false;

            string mapA = a.actionMap.name;
            string mapB = b.actionMap.name;

            if (mapA == mapB) return true;

            bool bulletA = IsBulletTimeSide(mapA);
            bool bulletB = IsBulletTimeSide(mapB);

            // 카드 조작과 시전 위치 지정은 InputMapSwitcher가 배타로 잡는다.
            if (bulletA && bulletB) return false;

            // 남은 조합은 Gameplay ↔ 불릿타임 맵. 정지 중에도 사는 건 지휘키뿐이다.
            if (mapA == InputActionNames.Gameplay.Map && bulletB) return CommanderActions.Contains(a.name);
            if (mapB == InputActionNames.Gameplay.Map && bulletA) return CommanderActions.Contains(b.name);

            // 모르는 조합은 겹친다고 본다 — 놓치는 것보다 한 번 더 묻는 편이 낫다.
            return true;
        }

        private static bool IsBulletTimeSide(string mapName)
            => mapName == InputActionNames.BulletTime.Map
               || mapName == InputActionNames.BulletTimeSkillShot.Map;

        /// <summary>
        /// <paramref name="path"/>를 그 자리에 넣으면 이미 그 키를 쓰는 자리와 부딪히는가.
        /// Input System은 이 검사를 해 주지 않는다 — 그냥 둘 다 발동한다.
        /// </summary>
        public static bool TryFindConflict(InputActionAsset actions,
            InputAction target, int bindingIndex, string path,
            out InputAction blocker, out int blockerBindingIndex)
        {
            blocker = null;
            blockerBindingIndex = -1;

            if (actions == null || target == null || string.IsNullOrEmpty(path)) return false;

            foreach ((InputAction action, int index) in Enumerate(actions))
            {
                // 자기 자신은 건너뛴다. 같은 액션의 다른 바인딩은 검사 대상이다 —
                // 컴포지트의 위와 왼쪽에 같은 키가 들어가면 방향이 통째로 망가진다.
                if (action == target && index == bindingIndex) continue;

                if (action.bindings[index].effectivePath != path) continue;
                if (!CanFireTogether(target, action)) continue;

                blocker = action;
                blockerBindingIndex = index;
                return true;
            }

            return false;
        }
    }
}
