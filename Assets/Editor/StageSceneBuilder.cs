using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Prototype.YG;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 미니 스테이지(적 1명)와 보스 스테이지 씬을 굽는다.
    ///
    /// 씬을 새로 조립하지 않고 <c>SampleScene</c>을 <b>복제</b>한다. 그 씬에는 플레이어 ·
    /// 동료 5명 · 입력 프리팹 · 디버그 HUD · 방 · 카메라 · 충돌 설정이 이미 전부 배선돼 있어서,
    /// 손으로 다시 만들면 하나씩 빠뜨린다. 복제본은 <see cref="Prototype.YG.BattleSceneController"/>도
    /// 물려받으므로 씬 단독 실행(ESC · 재시작)이 그대로 동작한다.
    ///
    /// <b>매번 SampleScene에서 다시 굽는다.</b> 스테이지 씬을 손으로 고쳐 뒀다면 사라진다 —
    /// 공통 배치는 SampleScene에서 고치고 이 메뉴를 다시 누르는 것이 이 빌더의 사용법이다.
    /// </summary>
    public static class StageSceneBuilder
    {
        private const string SourceScene = "Assets/Scenes/SampleScene.unity";
        private const string LevelFolder = "Assets/Scenes/Level";

        private const string MiniScene = LevelFolder + "/" + SceneNames.StageMini + ".unity";
        private const string BossScene = LevelFolder + "/" + SceneNames.StageBoss + ".unity";

        private const string BossPrefabPath = "Assets/Prefabs/Enemy_Boss.prefab";

        /// <summary>적을 놓는 자리. 플레이어가 방 왼쪽(-3)에 서므로 오른쪽에 벌려 둔다.</summary>
        private static readonly Vector3 MiniSpot = new Vector3(2.5f, 0f, 0f);
        private static readonly Vector3 BossSpot = new Vector3(3.5f, 0f, 0f);

        [MenuItem("Prototype/스테이지 - 미니·보스 씬 만들기")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog(
                    "스테이지 씬 만들기",
                    "SampleScene을 복제해 아래 두 씬을 덮어쓴다.\n\n" +
                    $"· {MiniScene}\n· {BossScene}\n\n" +
                    "두 씬에 손으로 고친 내용이 있다면 사라진다. 계속할까?",
                    "만들기", "취소"))
                return;

            // 지금 열려 있는 씬을 잃지 않게 먼저 묻는다. 아래에서 씬을 통째로 갈아 끼운다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
            {
                Debug.LogError($"[StageSceneBuilder] 원본 씬이 없다: {SourceScene}");
                return;
            }

            EnsureFolder(LevelFolder);

            // 지울 대상이 열려 있으면 삭제가 꼬인다. 먼저 원본으로 옮겨 두 씬 다 닫아 놓는다.
            EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);

            if (!BuildMini()) return;
            if (!BuildBoss()) return;

            RegisterInBuildSettings(MiniScene, BossScene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StageSceneBuilder] 완료 — {MiniScene}, {BossScene}");
        }

        /// <summary>
        /// 미니 스테이지. 씬에 이미 있는 적 <b>하나만 남기고</b> 나머지를 지운다.
        ///
        /// 프리팹을 새로 꽂지 않는 이유: SampleScene의 적들은 레이어 · wallMask · 깊이 배율이
        /// <see cref="SceneLayoutBuilder"/>로 이미 맞춰져 있다. 새 인스턴스를 넣으면 그걸 다시 맞춰야 하고,
        /// 하나라도 빠지면 "안 맞는 적"이 조용히 생긴다.
        /// </summary>
        private static bool BuildMini()
        {
            Scene scene = OpenCopy(MiniScene);
            if (!scene.IsValid()) return false;

            List<Enemy> enemies = FindEnemies(scene);
            if (enemies.Count == 0)
            {
                Debug.LogError("[StageSceneBuilder] SampleScene에 적이 하나도 없다. 미니 스테이지를 만들 수 없다.");
                return false;
            }

            Enemy keep = enemies[0];
            for (int i = 1; i < enemies.Count; i++)
                Object.DestroyImmediate(enemies[i].gameObject);

            keep.name = "Enemy_Mini";
            keep.transform.position = MiniSpot;

            return Save(scene, MiniScene, 1);
        }

        /// <summary>보스 스테이지. 기존 적을 전부 지우고 보스 프리팹 하나를 꽂는다.</summary>
        private static bool BuildBoss()
        {
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (bossPrefab == null)
            {
                Debug.LogError(
                    $"[StageSceneBuilder] {BossPrefabPath}가 없다. " +
                    "'Prototype ▸ 보스 - 프리팹 + 데이터 만들기'를 먼저 돌릴 것.");
                return false;
            }

            Scene scene = OpenCopy(BossScene);
            if (!scene.IsValid()) return false;

            foreach (Enemy e in FindEnemies(scene))
                Object.DestroyImmediate(e.gameObject);

            var boss = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab, scene);
            boss.name = "Boss";
            boss.transform.position = BossSpot;
            boss.transform.rotation = Quaternion.identity;

            NormalizeSceneEntity(boss);

            return Save(scene, BossScene, 1);
        }

        // ── 씬 조작 ────────────────────────────────────

        /// <summary>원본을 복제해서 연다. 기존 파일은 지운다 — 매번 같은 결과가 나와야 한다.</summary>
        private static Scene OpenCopy(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                AssetDatabase.DeleteAsset(path);

            if (!AssetDatabase.CopyAsset(SourceScene, path))
            {
                Debug.LogError($"[StageSceneBuilder] 씬 복제 실패: {SourceScene} → {path}");
                return default;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        /// <summary>비활성 오브젝트까지 센다. 꺼 둔 적이 남아 있으면 스테이지 수가 안 맞는다.</summary>
        private static List<Enemy> FindEnemies(Scene scene)
        {
            var found = new List<Enemy>();

            foreach (GameObject root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<Enemy>(true));

            return found;
        }

        /// <summary>
        /// 씬에 새로 꽂은 캐릭터를 SampleScene의 다른 캐릭터와 같은 규약으로 맞춘다.
        /// 깊이 배율은 <see cref="BeltScroll"/>의 static이 한 벌이라 인스턴스마다 값이 다르면 안 된다.
        /// </summary>
        private static void NormalizeSceneEntity(GameObject go)
        {
            SceneLayoutBuilder.RigBeltScrollView(go);

            var physics = go.GetComponent<Prototype.Physics>();
            int wall = LayerMask.NameToLayer("Wall");

            if (physics != null && wall >= 0)
            {
                var so = new SerializedObject(physics);
                so.FindProperty("wallMask").intValue = 1 << wall;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(go);
        }

        private static bool Save(Scene scene, string path, int expectedEnemies)
        {
            int actual = FindEnemies(scene).Count;
            if (actual != expectedEnemies)
            {
                Debug.LogError($"[StageSceneBuilder] {path}에 적이 {actual}명이다. {expectedEnemies}명이어야 한다.");
                return false;
            }

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                Debug.LogError($"[StageSceneBuilder] 씬 저장 실패: {path}");
                return false;
            }

            return true;
        }

        // ── Build Settings ─────────────────────────────

        /// <summary>
        /// 등록하지 않으면 <see cref="Prototype.YG.BattleSceneController.RestartStage"/>의
        /// 씬 단독 실행 경로(<c>SceneManager.LoadScene(scene.name)</c>)가 씬을 못 찾는다 —
        /// 재시작 버튼이 조용히 죽는다.
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
            Debug.Log($"[StageSceneBuilder] Build Settings에 씬 {added}개 등록");
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
