using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.Tests
{
    /// <summary>
    /// 비전투 칸 씬들의 <b>이름 · 빌드 목록 · 종류 · 배선</b>이 서로 맞는지 본다.
    ///
    /// 씬 전환은 이름 문자열로만 이뤄져서 컴파일에 안 걸린다. 빌드 목록에서 빠지면
    /// 에디터에서는 멀쩡하고 빌드에서만 "그 칸에 들어가면 멈춘다"로 보인다.
    /// 종류가 어긋나면 휴식방에 상점이 뜬다 — 둘 다 원인에서 먼 증상이다.
    /// </summary>
    public class NodeSceneWiringTests
    {
        private static readonly (string scene, NodeSceneKind kind)[] Nodes =
        {
            (SceneNames.NodeShop,       NodeSceneKind.Shop),
            (SceneNames.NodeEvent,      NodeSceneKind.Event),
            (SceneNames.NodeEventFight, NodeSceneKind.Event),
            (SceneNames.NodeEventCamp,  NodeSceneKind.Event),
        };

        private static string PathOf(string scene) => $"Assets/Scenes/Node/{scene}.unity";

        private string reopen;

        [SetUp]
        public void SetUp() => reopen = SceneManager.GetActiveScene().path;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(reopen) && reopen != SceneManager.GetActiveScene().path)
                EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);
        }

        [Test]
        public void NodeScenes_AreInBuildSettings()
        {
            foreach ((string scene, _) in Nodes)
            {
                string path = PathOf(scene);
                EditorBuildSettingsScene entry = EditorBuildSettings.scenes.FirstOrDefault(s => s.path == path);

                Assert.That(entry, Is.Not.Null, $"{path} 가 Build Settings 에 없다.");
                Assert.That(entry.enabled, Is.True, $"{path} 가 Build Settings 에서 꺼져 있다.");
            }
        }

        [Test]
        public void NodeScenes_HaveOneControllerOfTheirKind()
        {
            foreach ((string scene, NodeSceneKind kind) in Nodes)
            {
                string path = PathOf(scene);
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, $"{path} 가 없다.");

                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                NodeSceneController[] found = Object.FindObjectsByType<NodeSceneController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                Assert.That(found.Length, Is.EqualTo(1), $"{path} 의 NodeSceneController 수");
                Assert.That(found[0].Kind, Is.EqualTo(kind), $"{path} 의 칸 종류");

                // 소리가 안 들리는 씬이 된다. Boot 씬에는 리스너가 없다.
                Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length,
                            Is.EqualTo(1), $"{path} 의 AudioListener 수");
            }
        }

        /// <summary>
        /// 상점 · 이벤트 칸은 걸어다니는 방이고, NPC가 창을 연다. 인스펙터 배선은 메서드 이름 문자열이라
        /// 컴파일에 안 걸린다 — 끊기면 그 칸에 들어가 걸어 다니기만 하고 창도 출구도 없다.
        /// </summary>
        [TestCase(SceneNames.NodeShop, nameof(NodeSceneController.OpenShop))]
        [TestCase(SceneNames.NodeEvent, nameof(NodeSceneController.OpenEvent))]
        [TestCase(SceneNames.NodeEventFight, nameof(NodeSceneController.OpenEvent))]
        [TestCase(SceneNames.NodeEventCamp, nameof(NodeSceneController.Rest))]
        public void WalkableNode_HasPartyAndNpcThatOpensWindow(string scene, string method)
        {
            EditorSceneManager.OpenScene(PathOf(scene), OpenSceneMode.Single);
            NodeSceneController ctrl = Object.FindAnyObjectByType<NodeSceneController>();

            Assert.That(Object.FindAnyObjectByType<PartyAssembler>(), Is.Not.Null,
                        $"{scene} 에 BattleInput 이 없다 — 파티가 안 선다.");

            bool wired = Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).Any(i =>
                Enumerable.Range(0, i.OnInteract.GetPersistentEventCount()).Any(k =>
                    i.OnInteract.GetPersistentTarget(k) == ctrl && i.OnInteract.GetPersistentMethodName(k) == method));

            Assert.That(wired, Is.True, $"{scene}: {method} 를 부르는 Interactable 이 없다 — 창을 열 길이 없다.");
        }

        /// <summary>
        /// 싸움 이벤트는 NPC가 적으로 돌아선다. 배선이 빠지면 "싸운다"를 골라도 아무 일 없이 지나간다.
        /// </summary>
        [Test]
        public void FightScene_HasFoeAndFightChoice()
        {
            EditorSceneManager.OpenScene(PathOf(SceneNames.NodeEventFight), OpenSceneMode.Single);
            NodeSceneController ctrl = Object.FindAnyObjectByType<NodeSceneController>();

            Assert.That(ctrl.FightNpc, Is.Not.Null, "적으로 돌아설 NPC 가 비어 있다.");
            Assert.That(ctrl.FightNpc.GetComponent<Interactable>(), Is.Not.Null, "fightNpc 가 말을 걸 수 있는 NPC 가 아니다.");
            Assert.That(ctrl.FightEnemy, Is.Not.Null, "NPC 대신 설 적이 비어 있다.");
            Assert.That(ctrl.FightEnemy.prefab, Is.Not.Null, $"{ctrl.FightEnemy.name} 에 프리팹이 없다.");
            Assert.That(ctrl.Events.Any(e => e != null && e.choices.Any(c => c.fight)), Is.True, "싸우는 선택지가 없다.");
        }

        /// <summary>후보가 비면 이벤트 칸이 [계속] 하나짜리 빈 창이 된다.</summary>
        [TestCase(SceneNames.NodeEvent)]
        [TestCase(SceneNames.NodeEventFight)]
        public void EventScene_HasPlayableEvents(string scene)
        {
            EditorSceneManager.OpenScene(PathOf(scene), OpenSceneMode.Single);
            NodeSceneController ctrl = Object.FindAnyObjectByType<NodeSceneController>();

            Assert.That(ctrl, Is.Not.Null);
            Assert.That(ctrl.Events.Count, Is.GreaterThan(0), "이벤트 후보가 비어 있다.");

            foreach (RunEventAsset e in ctrl.Events)
            {
                Assert.That(e, Is.Not.Null, "이벤트 후보에 빈 칸이 있다.");
                Assert.That(e.ChoiceCount, Is.GreaterThan(0), $"{e.name} 에 선택지가 없다.");

                // 전부 골드를 요구하면 빈털터리는 아무것도 못 골라 [계속]도 안 뜬 채 갇힌다.
                Assert.That(e.choices.Any(c => RunEventRules.CanChoose(in c, 0)), Is.True,
                            $"{e.name}: 골드 0으로 고를 수 있는 선택지가 하나는 있어야 한다.");
            }
        }
    }
}
