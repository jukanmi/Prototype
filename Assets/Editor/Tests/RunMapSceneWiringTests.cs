using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.Tests
{
    /// <summary>
    /// 지도 씬이 이름 · 빌드 목록 · 화면 컴포넌트와 맞는지. 씬 전환은 이름 문자열이라 컴파일에 안 걸린다.
    /// </summary>
    public class RunMapSceneWiringTests
    {
        private const string ScenePath = "Assets/Scenes/RunMap.unity";

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
        public void RunMapScene_FileNameMatchesSceneNames()
        {
            Assert.That(System.IO.Path.GetFileNameWithoutExtension(ScenePath), Is.EqualTo(SceneNames.RunMap));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, $"{ScenePath} 가 없다.");
        }

        [Test]
        public void RunMapScene_IsInBuildSettings()
        {
            EditorBuildSettingsScene entry = EditorBuildSettings.scenes.FirstOrDefault(s => s.path == ScenePath);

            Assert.That(entry, Is.Not.Null, $"{ScenePath} 가 Build Settings 에 없다.");
            Assert.That(entry.enabled, Is.True);
        }

        /// <summary>
        /// Boot 의 GameManager 에 레시피가 꽂혀 있는가. 비어 있으면 [시작]이 에러만 남기고 안 눌린다 —
        /// 옛 스테이지 목록처럼 기본값으로 채우는 폴백이 없으므로 배선이 곧 동작이다.
        /// </summary>
        [Test]
        public void BootGameManager_HasUsableRecipe()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity", OpenSceneMode.Single);
            GameManager gm = Object.FindAnyObjectByType<GameManager>();

            Assert.That(gm, Is.Not.Null, "Boot 씬에 GameManager 가 없다.");
            Assert.That(gm.Recipe, Is.Not.Null, "Boot 의 GameManager 에 런 지도 레시피가 비어 있다.");
            Assert.That(gm.Recipe.Issues(), Is.Empty);
        }

        /// <summary>
        /// 레시피가 부르는 씬 이름이 전부 빌드 목록에 있는가. 이름은 문자열이라 컴파일에 안 걸리고,
        /// 빠지면 에디터에서는 멀쩡하다가 빌드에서만 "그 칸에 들어가면 멈춘다".
        /// </summary>
        [TestCase("Assets/Data/Map/RunMap_Main.asset")]
        [TestCase("Assets/Data/Map/RunMap_Debug.asset")]
        public void RecipeScenes_AreInBuildSettings(string recipePath)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<RunMapRecipe>(recipePath);
            Assert.That(recipe, Is.Not.Null, $"{recipePath} 가 없다.");

            var enabled = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
                .ToList();

            foreach (FloorRule floor in recipe.floors)
                foreach (NodeCandidate c in floor.candidates)
                    if (c.HasScene)
                        Assert.That(enabled, Has.Member(c.Scene), $"{recipe.name}: '{c.Scene}' 가 Build Settings 에 없거나 꺼져 있다.");
        }

        [Test]
        public void RunMapScene_HasOneScreen_WithUsablePreviewRecipe()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            RunMapScreen[] screens = Object.FindObjectsByType<RunMapScreen>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.That(screens.Length, Is.EqualTo(1));
            Assert.That(screens[0].PreviewRecipe, Is.Not.Null, "미리보기 레시피가 비어 있다.");
            Assert.That(screens[0].PreviewRecipe.Issues(), Is.Empty);

            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1),
                        "Boot 씬에는 리스너가 없다 — 없으면 지도 화면에서 소리가 끊긴다.");
        }
    }
}
