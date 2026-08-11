using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 입력 호스트 프리팹을 만든다.
    ///
    /// 태그 교대가 몸을 <c>SetActive(false)</c>로 내리기 때문에 입력이 몸에 붙어 있으면
    /// 교대하는 순간 되돌아올 키까지 죽는다. 그래서 <c>PlayerInput</c> ·
    /// <see cref="PlayerInputController"/> · <see cref="InputMapSwitcher"/> ·
    /// <see cref="BattleCommander"/> · <see cref="TagSwapController"/>를
    /// <b>절대 꺼지지 않는 오브젝트</b> 한 곳에 모은다.
    ///
    /// 손으로 붙이면 <c>defaultActionMap</c>이나 <c>notificationBehavior</c>를 빠뜨리기 쉬워
    /// 메뉴로 만든다. 검증은 <c>BattleInputPrefabTests</c>가 한다.
    /// </summary>
    public static class BattleInputBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/BattleInput.prefab";
        private const string ActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

        [MenuItem("Prototype/전투 - 입력 호스트 프리팹 만들기", priority = 30)]
        public static void Build()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null)
            {
                Debug.LogError($"[BattleInputBuilder] {ActionsPath} 를 못 찾았다. 액션 자산부터 만들 것.");
                return;
            }

            var root = new GameObject("BattleInput");

            var playerInput = root.AddComponent<PlayerInput>();
            playerInput.actions = actions;

            // 비어 있으면 아무 맵도 자동으로 안 켜진다. 컨트롤러가 OnEnable에서 직접 켜기는
            // 하지만 그 전에 도는 코드가 입력을 못 읽는다.
            playerInput.defaultActionMap = InputActionNames.Gameplay.Map;

            // 폴링으로 읽으므로 콜백은 아무도 안 받는다. SendMessage로 두면
            // 매 입력마다 리플렉션만 돌고 얻는 게 없다.
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            root.AddComponent<PlayerInputController>();
            root.AddComponent<InputMapSwitcher>();
            root.AddComponent<TagSwapController>();
            root.AddComponent<BattleCommander>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();

            Debug.Log($"[BattleInputBuilder] {PrefabPath} 생성 완료. " +
                      "씬에 넣고, Player 프리팹에서 PlayerInput · PlayerInputController · " +
                      "InputMapSwitcher 를 제거할 것.", prefab);
        }
    }
}
