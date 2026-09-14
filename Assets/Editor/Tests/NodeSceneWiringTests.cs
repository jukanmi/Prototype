using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.Tests
{
    /// <summary>
    /// 비전투 칸 씬 셋이 <b>이름 · 빌드 목록 · 종류</b>가 서로 맞는지 본다.
    ///
    /// 씬 전환은 이름 문자열로만 이뤄져서 컴파일에 안 걸린다. 빌드 목록에서 빠지면
    /// 에디터에서는 멀쩡하고 빌드에서만 "그 칸에 들어가면 멈춘다"로 보인다.
    /// 종류가 어긋나면 휴식방에 상점이 뜬다 — 둘 다 원인에서 먼 증상이다.
    /// </summary>
    public class NodeSceneWiringTests
    {
        private static readonly (string scene, NodeSceneKind kind)[] Nodes =
        {
            (SceneNames.NodeRest,  NodeSceneKind.Rest),
            (SceneNames.NodeShop,  NodeSceneKind.Shop),
            (SceneNames.NodeEvent, NodeSceneKind.Event),
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

        /// <summary>후보가 비면 이벤트 칸이 [계속] 하나짜리 빈 창이 된다.</summary>
        [Test]
        public void EventScene_HasPlayableEvents()
        {
            EditorSceneManager.OpenScene(PathOf(SceneNames.NodeEvent), OpenSceneMode.Single);
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
