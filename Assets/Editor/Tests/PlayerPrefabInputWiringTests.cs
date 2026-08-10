using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype.Tests
{
    /// <summary>
    /// Player 프리팹의 입력 배선을 본다. 손으로 붙이는 부분이라
    /// 컴파일러도 다른 테스트도 안 잡아 준다 — 틀리면 씬을 켜 봐야 안다.
    /// </summary>
    public class PlayerPrefabInputWiringTests
    {
        private const string PrefabPath = "Assets/Prefabs/Player.prefab";
        private const string ActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

        private static GameObject LoadPrefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(go, Is.Not.Null, $"{PrefabPath} 를 못 찾았다.");
            return go;
        }

        [Test]
        public void Root_HasPlayerInput()
        {
            GameObject root = LoadPrefab();

            // 자식(Sprite · Shadow · View)에 붙이면 PlayerInputController 가
            // 같은 오브젝트에서 PlayerInput 을 못 찾아 Awake 에서 죽는다.
            Assert.That(root.GetComponent<PlayerInput>(), Is.Not.Null,
                "최상단 Player 오브젝트에 PlayerInput 이 없다.");
        }

        [Test]
        public void Root_HasPlayerInputController()
        {
            GameObject root = LoadPrefab();

            Assert.That(root.GetComponent<PlayerInputController>(), Is.Not.Null,
                "최상단 Player 오브젝트에 PlayerInputController 가 없다.");
        }

        [Test]
        public void PlayerInput_UsesProjectActionsAsset()
        {
            var playerInput = LoadPrefab().GetComponent<PlayerInput>();
            Assert.That(playerInput, Is.Not.Null, "PlayerInput 이 없다.");

            Assert.That(playerInput.actions, Is.Not.Null, "PlayerInput 의 Actions 칸이 비어 있다.");

            var expected = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            Assert.That(playerInput.actions, Is.SameAs(expected),
                $"PlayerInput 이 {ActionsPath} 가 아닌 다른 자산을 물고 있다.");
        }

        [Test]
        public void PlayerInput_DefaultMapIsGameplay()
        {
            var playerInput = LoadPrefab().GetComponent<PlayerInput>();
            Assert.That(playerInput, Is.Not.Null, "PlayerInput 이 없다.");

            // 비어 있으면 아무 맵도 자동으로 안 켜진다. PlayerInputController 가
            // OnEnable 에서 직접 켜기는 하지만, 그 전에 도는 코드가 입력을 못 읽는다.
            Assert.That(playerInput.defaultActionMap, Is.EqualTo(InputActionNames.Gameplay.Map),
                "Default Map 이 Gameplay 가 아니다.");
        }

        [Test]
        public void PlayerInput_UsesCSharpEvents()
        {
            var playerInput = LoadPrefab().GetComponent<PlayerInput>();
            Assert.That(playerInput, Is.Not.Null, "PlayerInput 이 없다.");

            // 폴링으로 읽으므로 콜백은 아무도 안 받는다. SendMessage · BroadcastMessage 로
            // 두면 매 입력마다 리플렉션만 돌고 얻는 게 없다.
            Assert.That(playerInput.notificationBehavior,
                Is.EqualTo(PlayerNotifications.InvokeCSharpEvents),
                "Behavior 가 Invoke C Sharp Events 가 아니다.");
        }

        [Test]
        public void Root_HasInputMapSwitcher()
        {
            GameObject root = LoadPrefab();

            // 없으면 조준 맵이 켜진 채로 굳는다. 지금 구조에선 곧바로 티가 안 나지만
            // 이동과 조준이 같은 WASD를 물고 있는 상태가 계속된다.
            Assert.That(root.GetComponent<InputMapSwitcher>(), Is.Not.Null,
                "최상단 Player 오브젝트에 InputMapSwitcher 가 없다.");
        }

        [Test]
        public void Root_StillHasPlayerControl()
        {
            GameObject root = LoadPrefab();

            // PlayerInputController 는 PlayerControl 을 대체하지 않는다.
            // 상태머신은 여전히 Control 의 Command / MoveDirection 을 읽는다.
            Assert.That(root.GetComponent<PlayerControl>(), Is.Not.Null,
                "PlayerControl 이 사라졌다. Entity 상태머신이 명령을 못 받는다.");
        }
    }
}
