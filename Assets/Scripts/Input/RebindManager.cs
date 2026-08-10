using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype
{
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
}
