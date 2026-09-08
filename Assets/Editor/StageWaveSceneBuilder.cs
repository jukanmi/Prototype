using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.EditorTools
{
    /// <summary>
    /// <b>웨이브</b> 스테이지를 굽는다. 지금은 1 · 3 · 4번이다 —
    /// 2번은 아레나 스테이지라 <see cref="ArenaSceneBuilder"/>가 따로 굽는다.
    ///
    /// <see cref="StageSceneBuilder"/>와 같은 규약이다 — <c>SampleScene</c>을 <b>복제</b>해서
    /// 플레이어 · 동료 5명 · 입력 프리팹 · 디버그 HUD · 방 · 카메라 · 충돌 설정을 통째로 물려받는다.
    /// 손으로 다시 만들면 반드시 하나씩 빠뜨린다.
    ///
    /// 다른 점은 하나다: <b>적을 전부 지운다.</b> 웨이브 스테이지의 적은 씬에 놓이지 않고
    /// <see cref="StageDirector"/>가 <see cref="StageWaveCatalog"/>의 표를 읽어 소환한다.
    /// 그래서 배치를 고치는 자리는 씬이 아니라 그 표 하나다 — 다섯 씬을 열어 적을 옮기는 일이
    /// 생기지 않는다.
    ///
    /// <b>매번 SampleScene에서 다시 굽는다.</b> 이 씬들을 손으로 고쳐 뒀다면 사라진다.
    /// </summary>
    public static class StageWaveSceneBuilder
    {
        private const string SourceScene = "Assets/Scenes/SampleScene.unity";
        private const string LevelFolder = "Assets/Scenes/Level";

        private const string DataFolder = "Assets/Data/Enemy";
        private const string MeleeDataPath = DataFolder + "/Enemy_Melee.asset";
        private const string ChargerDataPath = DataFolder + "/Enemy_Charger.asset";
        private const string RangedDataPath = DataFolder + "/Enemy_Ranged.asset";

        private const string DirectorName = "StageDirector";

        /// <summary>웨이브로 도는 스테이지 번호. 2번은 아레나라 빠져 있다.</summary>
        private static readonly int[] WaveStages = { 1, 3, 4 };

        /// <summary>번호 → 씬 경로.</summary>
        private static string ScenePath(int number) => $"{LevelFolder}/{SceneNames.Stage(number)}.unity";

        [MenuItem("Prototype/스테이지 - 웨이브 스테이지 만들기 (1 · 3 · 4)")]
        public static void Build()
        {
            var paths = new string[WaveStages.Length];
            for (int i = 0; i < paths.Length; i++) paths[i] = ScenePath(WaveStages[i]);

            if (!EditorUtility.DisplayDialog(
                    "웨이브 스테이지 만들기",
                    "SampleScene을 복제해 아래 다섯 씬을 덮어쓴다.\n\n" +
                    "· " + string.Join("\n· ", paths) + "\n\n" +
                    "이 씬들에 손으로 고친 내용이 있다면 사라진다. 계속할까?",
                    "만들기", "취소"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
            {
                Debug.LogError($"[StageWaveSceneBuilder] 원본 씬이 없다: {SourceScene}");
                return;
            }

            EnemyData melee = Load(MeleeDataPath);
            EnemyData charger = Load(ChargerDataPath);
            EnemyData ranged = Load(RangedDataPath);

            if (melee == null || charger == null || ranged == null)
            {
                Debug.LogError(
                    "[StageWaveSceneBuilder] 적 데이터가 없다. " +
                    "'Prototype ▸ 적 - 근접·원거리·돌진 프리팹 만들기'를 먼저 돌릴 것.");
                return;
            }

            EnsureFolder(LevelFolder);

            // 지울 대상이 열려 있으면 삭제가 꼬인다. 먼저 원본으로 옮겨 전부 닫아 둔다.
            EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);

            foreach (int number in WaveStages)
                if (!BuildStage(number, melee, charger, ranged)) return;

            RegisterInBuildSettings(paths);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StageWaveSceneBuilder] 완료 — 웨이브 스테이지 {WaveStages.Length}개");
        }

        private static bool BuildStage(int number, EnemyData melee, EnemyData charger, EnemyData ranged)
        {
            string path = ScenePath(number);

            Scene scene = OpenCopy(path);
            if (!scene.IsValid()) return false;

            // 씬에 놓인 적은 전부 지운다. 여기 남으면 웨이브 1이 열리기도 전에 싸움이 시작되고,
            // 디렉터가 세는 목록에도 안 들어가 스테이지가 끝나지 않는다.
            foreach (Enemy e in FindEnemies(scene))
                Object.DestroyImmediate(e.gameObject);

            var host = new GameObject(DirectorName);
            SceneManager.MoveGameObjectToScene(host, scene);
            host.transform.position = Vector3.zero;

            EnemySpawnService spawner = host.AddComponent<EnemySpawnService>();
            ArenaSceneBuilder.WireSpawner(spawner, melee, charger, ranged);

            StageDirector director = host.AddComponent<StageDirector>();
            Wire(director, number, spawner);

            int left = FindEnemies(scene).Count;
            if (left != 0)
            {
                Debug.LogError($"[StageWaveSceneBuilder] {path}에 적이 {left}명 남았다. 0명이어야 한다.");
                return false;
            }

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                Debug.LogError($"[StageWaveSceneBuilder] 씬 저장 실패: {path}");
                return false;
            }

            WaveDefinition[] waves = StageWaveCatalog.For(number);
            Debug.Log($"[StageWaveSceneBuilder] {path} — {StageWaveCatalog.ThemeOf(number)} " +
                      $"(웨이브 {waves.Length}개)");
            return true;
        }

        /// <summary>
        /// 디렉터 배선. <c>waves</c>는 <b>비워 둔다</b> —
        /// 비어 있으면 <see cref="StageDirector"/>가 Awake 에서 <see cref="StageWaveCatalog"/>를 읽는다.
        /// 여기서 씬에 구워 넣으면 표를 고쳐도 씬을 다시 굽기 전까지 반영되지 않는다.
        /// </summary>
        private static void Wire(StageDirector director, int number, EnemySpawnService spawner)
        {
            var so = new SerializedObject(director);

            so.FindProperty("stageNumber").intValue = number;
            so.FindProperty("waves").ClearArray();
            so.FindProperty("spawner").objectReferenceValue = spawner;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        // ── 씬 조작 (StageSceneBuilder와 같은 규약) ─────

        private static Scene OpenCopy(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                AssetDatabase.DeleteAsset(path);

            if (!AssetDatabase.CopyAsset(SourceScene, path))
            {
                Debug.LogError($"[StageWaveSceneBuilder] 씬 복제 실패: {SourceScene} → {path}");
                return default;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        /// <summary>비활성 오브젝트까지 센다. 꺼 둔 적이 남아 있으면 웨이브 수가 안 맞는다.</summary>
        private static List<Enemy> FindEnemies(Scene scene)
        {
            var found = new List<Enemy>();

            foreach (GameObject root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<Enemy>(true));

            return found;
        }

        private static EnemyData Load(string path) => AssetDatabase.LoadAssetAtPath<EnemyData>(path);

        /// <summary>
        /// 등록하지 않으면 <c>SceneLoader</c>가 이름으로 씬을 못 찾아
        /// 스테이지 전환이 조용히 죽는다.
        /// </summary>
        private static void RegisterInBuildSettings(params string[] paths)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int added = 0;

            foreach (string path in paths)
            {
                if (scenes.Exists(s => s.path == path)) continue;

                scenes.Add(new EditorBuildSettingsScene(path, true));
                added++;
            }

            if (added == 0) return;

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[StageWaveSceneBuilder] Build Settings에 씬 {added}개 등록");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string cur = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
