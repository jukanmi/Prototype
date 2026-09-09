using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Prototype.EditorTools;

namespace Prototype.Tests
{
    /// <summary>
    /// 입력 배선을 본다. 손으로 붙이는 부분이라 컴파일러도 다른 테스트도 안 잡아 준다 —
    /// 틀리면 씬을 켜 봐야 안다.
    ///
    /// 예전에는 이 배선이 Player 프리팹에 있었다. 태그 교대가 몸을
    /// <c>SetActive(false)</c>로 내리면서 거기 있으면 안 되게 됐다 —
    /// 교대하는 순간 되돌아올 키까지 함께 죽는다.
    /// </summary>
    public class BattleInputPrefabTests
    {
        // 경로가 아니라 컴포넌트로 찾는다 — 프리팹을 옮겨도 안 끊긴다.
        private static string HostPath => PrefabLocator.BattleInputPath;
        private static string PlayerPath => PrefabLocator.PlayerPath;
        private const string ActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

        private static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(go, Is.Not.Null,
                $"{path} 를 못 찾았다.");
            return go;
        }

        // ── 파티 체력 HUD ───────────────────────────────────

        /// <summary>
        /// 줄은 <c>RowTemplate</c>을 복제해 만들고 조각을 <b>이름으로</b> 찾는다
        /// (<c>PartyHealthHUD.NewRow</c>). 이름이 어긋나면 복제한 줄에서
        /// <c>NullReferenceException</c>이 나는데, 그때는 이미 씬을 켠 뒤다.
        /// </summary>
        [Test]
        public void PartyHealthHUD_IsWired()
        {
            var hud = Load(HostPath).GetComponentInChildren<PartyHealthHUD>(true);
            Assert.That(hud, Is.Not.Null, "BattleInput 에 PartyHealthHUD 가 없다.");

            var so = new SerializedObject(hud);

            foreach (string field in new[] { "panel", "rowTemplate" })
                Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null,
                    $"PartyHealthHUD.{field} 배선이 비었다 — 파티 체력 줄이 안 뜬다.");
        }

        [TestCase("Band")]
        [TestCase("Name")]
        [TestCase("BarBack")]
        [TestCase("BarBack/Fill")]
        [TestCase("Numbers")]
        public void PartyHealthRowTemplate_HasEveryPart(string path)
        {
            var hud = Load(HostPath).GetComponentInChildren<PartyHealthHUD>(true);
            var template = (RectTransform)new SerializedObject(hud)
                .FindProperty("rowTemplate").objectReferenceValue;

            Assert.That(template, Is.Not.Null, "rowTemplate 이 비었다.");
            Assert.That(template.Find(path), Is.Not.Null,
                $"RowTemplate/{path} 가 없다 — NewRow 가 이 이름으로 찾는다.");
        }

        /// <summary>원본은 꺼진 채로 둔다. 켜 두면 아무도 없는 빈 줄이 하나 남는다.</summary>
        [Test]
        public void PartyHealthRowTemplate_StartsHidden()
        {
            var hud = Load(HostPath).GetComponentInChildren<PartyHealthHUD>(true);
            var template = (RectTransform)new SerializedObject(hud)
                .FindProperty("rowTemplate").objectReferenceValue;

            Assert.That(template.gameObject.activeSelf, Is.False,
                "RowTemplate 이 켜진 채로 저장됐다 — 빈 줄이 하나 남는다.");
        }

        // ── 입력 호스트 ─────────────────────────────────────

        [Test]
        public void Host_HasPlayerInput()
        {
            // 자식에 붙이면 PlayerInputController 가 같은 오브젝트에서
            // PlayerInput 을 못 찾아 Awake 에서 죽는다.
            Assert.That(Load(HostPath).GetComponent<PlayerInput>(), Is.Not.Null,
                "입력 호스트 최상단에 PlayerInput 이 없다.");
        }

        [Test]
        public void Host_HasPlayerInputController()
        {
            Assert.That(Load(HostPath).GetComponent<PlayerInputController>(), Is.Not.Null,
                "입력 호스트에 PlayerInputController 가 없다.");
        }

        [Test]
        public void Host_HasInputMapSwitcher()
        {
            // 없으면 조준 맵이 켜진 채로 굳어 이동과 조준이 같은 WASD를 계속 물고 있다.
            Assert.That(Load(HostPath).GetComponent<InputMapSwitcher>(), Is.Not.Null,
                "입력 호스트에 InputMapSwitcher 가 없다.");
        }

        [Test]
        public void Host_HasCommanderAndTagSwap()
        {
            GameObject host = Load(HostPath);

            // 지휘키(E · U)와 교대키(F)를 읽는 주체. 몸에 두면 교대와 함께 죽는다.
            Assert.That(host.GetComponent<BattleCommander>(), Is.Not.Null,
                "입력 호스트에 BattleCommander 가 없다 — E · U · F 가 아무 데서도 안 읽힌다.");

            Assert.That(host.GetComponent<TagSwapController>(), Is.Not.Null,
                "입력 호스트에 TagSwapController 가 없다 — 교대가 일어나지 않는다.");
        }

        [Test]
        public void PlayerInput_UsesProjectActionsAsset()
        {
            var playerInput = Load(HostPath).GetComponent<PlayerInput>();
            Assert.That(playerInput, Is.Not.Null, "PlayerInput 이 없다.");
            Assert.That(playerInput.actions, Is.Not.Null, "PlayerInput 의 Actions 칸이 비어 있다.");

            var expected = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            Assert.That(playerInput.actions, Is.SameAs(expected),
                $"PlayerInput 이 {ActionsPath} 가 아닌 다른 자산을 물고 있다.");
        }

        [Test]
        public void PlayerInput_DefaultMapIsGameplay()
        {
            var playerInput = Load(HostPath).GetComponent<PlayerInput>();

            // 비어 있으면 아무 맵도 자동으로 안 켜진다. PlayerInputController 가
            // OnEnable 에서 직접 켜기는 하지만, 그 전에 도는 코드가 입력을 못 읽는다.
            Assert.That(playerInput.defaultActionMap, Is.EqualTo(InputActionNames.Gameplay.Map),
                "Default Map 이 Gameplay 가 아니다.");
        }

        [Test]
        public void PlayerInput_UsesCSharpEvents()
        {
            var playerInput = Load(HostPath).GetComponent<PlayerInput>();

            // 폴링으로 읽으므로 콜백은 아무도 안 받는다. SendMessage 로 두면
            // 매 입력마다 리플렉션만 돌고 얻는 게 없다.
            Assert.That(playerInput.notificationBehavior,
                Is.EqualTo(PlayerNotifications.InvokeCSharpEvents),
                "Behavior 가 Invoke C Sharp Events 가 아니다.");
        }

        // ── 몸에는 입력이 없어야 한다 ───────────────────────

        [Test]
        public void PlayerBody_HasNoInputComponents()
        {
            GameObject body = Load(PlayerPath);

            Assert.That(body.GetComponent<PlayerInput>(), Is.Null,
                "Player 프리팹에 PlayerInput 이 남아 있다 — 태그로 내려가면 입력이 통째로 죽는다.");
            Assert.That(body.GetComponent<PlayerInputController>(), Is.Null,
                "Player 프리팹에 PlayerInputController 가 남아 있다.");
            Assert.That(body.GetComponent<InputMapSwitcher>(), Is.Null,
                "Player 프리팹에 InputMapSwitcher 가 남아 있다.");
        }

        [Test]
        public void PlayerBody_IsPilotable()
        {
            // 조종사는 몸 밖에 하나뿐이다. 몸에 남는 건 "몰 수 있다"는 표식과
            // 그 캐릭터의 조작 수치(대시 쿨 · 선입력 창)뿐이다.
            Assert.That(Load(PlayerPath).GetComponent<Pilotable>(), Is.Not.Null,
                "Player 프리팹에 Pilotable 이 없다 — 조종사가 대시 쿨 · 선입력 창을 기본값으로 돌린다.");
        }

        [Test]
        public void AllyBodies_ArePilotable()
        {
            // 동료도 태그로 조작 대상이 된다.
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponent<Ally>() == null) continue;

                Assert.That(go.GetComponent<Pilotable>(), Is.Not.Null,
                    $"{path} 에 Pilotable 이 없다 — 교대해도 조작 수치가 기본값으로 돈다.");
            }
        }

        /// <summary>
        /// 빙의 모델의 잔해가 남아 있으면 안 된다. 몸에 조종사가 붙어 있던 시절에는
        /// 프리팹과 씬이 조용히 어긋났다 — 어떤 몸에는 붙고 어떤 몸에는 안 붙어도
        /// 게임이 그냥 돌아가 버렸다.
        /// </summary>
        [Test]
        public void Bodies_CarryNoDriverExceptEnemyAi()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponent<Entity>() == null) continue;

                foreach (Control c in go.GetComponents<Control>())
                    Assert.That(c, Is.TypeOf<EnemyControl>(),
                        $"{path} 에 {c.GetType().Name} 이 붙어 있다 — 몸에 붙는 드라이버는 EnemyControl 뿐이다.");
            }
        }
    }
}
