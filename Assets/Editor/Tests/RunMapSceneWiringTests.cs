using System.Collections.Generic;
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

        /// <summary>
        /// 방 크기 범위가 있는 층의 전투 · 정예 후보 씬은 <b>방을 바꿀 수 있어야 한다</b> — <see cref="StageRoom"/>이 있고,
        /// 그 기준 방에 범위 양끝을 걸어도 규칙(<see cref="RoomRules.Fits"/>)을 지킨다.
        ///
        /// 아레나 스테이지(02 · 05)는 <c>StageRoom</c>이 없다. 그런 씬이 범위 있는 층에 들어가면 지도는 "방 크기 90%"라고
        /// 말하는데 전투는 원래 크기로 돈다(디렉터가 에러만 남긴다). 레시피는 문자열이라 이 대조가 컴파일에 안 걸린다.
        /// 계획서: docs/Room_Size_Plan.md (2.4 · 8단계)
        /// </summary>
        [TestCase("Assets/Data/Map/RunMap_Main.asset", true)]
        [TestCase("Assets/Data/Map/RunMap_Debug.asset", false)]
        public void ResizableFloors_OnlyOfferScenesWithStageRoom(string recipePath, bool expectRanges)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<RunMapRecipe>(recipePath);
            Assert.That(recipe, Is.Not.Null, $"{recipePath} 가 없다.");

            var checkedScenes = new Dictionary<string, StageRoom>();
            int rangedFloors = 0;

            for (int f = 0; f < recipe.FloorCount; f++)
            {
                FloorRule floor = recipe.floors[f];
                if (!floor.HasRoomRange) continue;

                rangedFloors++;

                foreach (NodeCandidate c in floor.candidates)
                {
                    if (!RunMapRules.GetsRoomModifier(c.kind) || !c.HasScene) continue;

                    string path = ScenePathOf(c.Scene);
                    Assert.That(path, Is.Not.Null, $"{recipe.name} {f}층: '{c.Scene}' 를 Build Settings 에서 못 찾는다.");

                    StageRoom room = StageRoom.FindIn(EditorSceneManager.OpenScene(path, OpenSceneMode.Single));
                    Assert.That(room, Is.Not.Null,
                        $"{recipe.name} {f}층: 방 크기 범위({floor.roomPercentMin}~{floor.roomPercentMax}%)가 있는데 " +
                        $"후보 '{c.Scene}' 에 StageRoom 이 없다 — 이 층의 범위를 0으로 두거나 씬을 다른 층으로 옮길 것.");

                    foreach (int percent in new[] { floor.roomPercentMin, floor.roomPercentMax })
                    {
                        RoomRect sized = RoomRules.Scale(room.BaseRoom, percent);
                        Assert.That(RoomRules.Fits(sized), Is.True,
                            $"{recipe.name} {f}층 '{c.Scene}': 기준 방 {room.BaseRoom} 을 {percent}%로 바꾸면 {sized} — 규칙을 못 지킨다.");
                    }

                    checkedScenes[c.Scene] = room;
                }
            }

            // 조건이 잘못 좁혀져 전부 건너뛰면 이 테스트는 아무것도 안 보면서 통과한다.
            if (expectRanges)
            {
                Assert.That(rangedFloors, Is.GreaterThan(0), $"{recipe.name}: 방 크기 범위가 있는 층이 하나도 없다");
                Assert.That(checkedScenes.Count, Is.GreaterThan(0), $"{recipe.name}: 확인한 씬이 없다");
            }
            else
            {
                Assert.That(rangedFloors, Is.Zero, $"{recipe.name}: 디버그 경로는 방 크기를 흔들지 않는다");
            }
        }

        private static string ScenePathOf(string sceneName)
            => EditorBuildSettings.scenes
                .Select(s => s.path)
                .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == sceneName);

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
