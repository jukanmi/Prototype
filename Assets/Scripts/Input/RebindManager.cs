// 런타임 키 변경 — 대기 · 충돌 검사 · 저장, 그리고 무엇을 바꿀 수 있는지 정하는 규칙.
// 화면은 RebindUI가 그린다.

using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;
using UnityEngine;

namespace Prototype
{
    // ══ RebindManager ═══════════════════════════════════════════

    /// <summary>
    /// 런타임 키 변경. 대기 · 충돌 검사 · 저장을 맡는다. 화면은 <c>RebindUI</c>가 그린다.
    ///
    /// MonoBehaviour가 아니다 — 코루틴이 필요 없고, 대기 중인 조작 하나만 들고 있으면 된다.
    /// </summary>
    public class RebindManager
    {
        /// <summary>바뀐 키를 담는 PlayerPrefs 키. 값은 Input System의 오버라이드 JSON.</summary>
        public const string PrefsKey = "input.bindings";

        public enum Result
        {
            /// <summary>새 키가 들어갔다.</summary>
            Applied,

            /// <summary>사용자가 ESC로 물렸다.</summary>
            Canceled,

            /// <summary>이미 그 키를 쓰는 자리가 있어 되돌렸다.</summary>
            Conflict,
        }

        private readonly InputActionAsset actions;
        private InputActionRebindingExtensions.RebindingOperation operation;

        /// <summary>대기 중 잠가 둔 맵들. 끝나면 이 상태로 되돌린다.</summary>
        private bool[] mapWasEnabled;

        public RebindManager(InputActionAsset actions)
        {
            this.actions = actions;
        }

        public bool IsListening => operation != null;

        /// <summary>충돌로 막혔을 때 어느 자리와 부딪혔는지. UI가 문구를 만들 때 쓴다.</summary>
        public string LastConflictLabel { get; private set; }

        // ── 키 받기 ─────────────────────────────────────────

        /// <summary>
        /// 다음에 눌리는 키를 그 자리에 넣는다. 콜백은 성공 · 취소 · 충돌 어느 쪽으로든 한 번 온다.
        /// </summary>
        public void Begin(InputAction action, int bindingIndex, Action<Result> onFinish)
        {
            if (IsListening) return;
            if (!InputRebindRules.IsRebindableBinding(action, bindingIndex)) return;

            LastConflictLabel = null;

            string before = action.bindings[bindingIndex].overridePath;

            // 대기 중에는 게임이 그 키에 반응하면 안 된다. W를 누르면 카드가 집히고
            // 그 W가 새 키로도 들어가는 꼴이 된다. ESC 취소는 액션이 아니라
            // 디바이스에서 직접 듣기 때문에 전부 잠가도 동작한다.
            SuspendAllMaps();

            operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ => Finish(action, bindingIndex, before, Result.Canceled, onFinish))
                .OnComplete(_ => Complete(action, bindingIndex, before, onFinish))
                .Start();
        }

        /// <summary>화면을 닫는 등 밖에서 대기를 접을 때.</summary>
        public void Abort()
        {
            operation?.Cancel();
        }

        private void Complete(InputAction action, int bindingIndex, string before,
            Action<Result> onFinish)
        {
            string path = action.bindings[bindingIndex].effectivePath;

            if (InputRebindRules.TryFindConflict(actions, action, bindingIndex, path,
                    out InputAction blocker, out int blockerIndex))
            {
                LastConflictLabel =
                    $"{InputDisplayNames.Map(blocker.actionMap.name)} · {InputDisplayNames.Binding(blocker, blockerIndex)}";

                // 되돌린다. 그냥 두면 한 키가 두 자리를 동시에 발동한다.
                Restore(action, bindingIndex, before);
                Finish(action, bindingIndex, before, Result.Conflict, onFinish);
                return;
            }

            Finish(action, bindingIndex, before, Result.Applied, onFinish);
        }

        private void Finish(InputAction action, int bindingIndex, string before,
            Result result, Action<Result> onFinish)
        {
            if (result == Result.Canceled) Restore(action, bindingIndex, before);

            operation?.Dispose();
            operation = null;

            RestoreMaps();

            if (result == Result.Applied) Save();

            onFinish?.Invoke(result);
        }

        private static void Restore(InputAction action, int bindingIndex, string before)
        {
            if (string.IsNullOrEmpty(before)) action.RemoveBindingOverride(bindingIndex);
            else action.ApplyBindingOverride(bindingIndex, before);
        }

        // ── 맵 잠금 ─────────────────────────────────────────

        private void SuspendAllMaps()
        {
            mapWasEnabled = new bool[actions.actionMaps.Count];

            for (int i = 0; i < actions.actionMaps.Count; i++)
                mapWasEnabled[i] = actions.actionMaps[i].enabled;

            actions.Disable();
        }

        private void RestoreMaps()
        {
            if (mapWasEnabled == null) return;

            for (int i = 0; i < actions.actionMaps.Count && i < mapWasEnabled.Length; i++)
            {
                if (mapWasEnabled[i]) actions.actionMaps[i].Enable();
            }

            mapWasEnabled = null;
        }

        // ── 프리셋 · 되돌리기 ───────────────────────────────

        /// <summary>프리셋을 얹고 저장한다. 기존에 직접 바꾼 키는 전부 지워진다.</summary>
        public int ApplyPreset(KeyBindingPreset preset)
        {
            if (preset == null) return 0;

            int applied = preset.ApplyTo(actions);
            Save();
            return applied;
        }

        /// <summary>액션 자산의 기본 키로 되돌린다.</summary>
        public void ResetAll()
        {
            actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        // ── 저장 · 불러오기 ─────────────────────────────────

        public void Save()
        {
            PlayerPrefs.SetString(PrefsKey, actions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 저장해 둔 키를 얹는다. <see cref="PlayerInputController"/>가 액션을 잡은 직후 부른다 —
        /// 그 전에 부르면 얹을 자산이 아직 없다.
        /// </summary>
        public static void Load(InputActionAsset actions)
        {
            if (actions == null) return;

            // 먼저 비운다. 에디터에서는 자산이 ScriptableObject 인스턴스 하나뿐이라
            // 지난 플레이에서 얹은 오버라이드가 메모리에 남는다. 저장본이 없을 때
            // 그냥 돌아가면 지운 적 없는 키가 되살아난 것처럼 보인다.
            actions.RemoveAllBindingOverrides();

            string json = PlayerPrefs.GetString(PrefsKey, null);
            if (string.IsNullOrEmpty(json)) return;

            actions.LoadBindingOverridesFromJson(json);
        }
    }

    // ══ InputRebindRules ═══════════════════════════════════════════

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
