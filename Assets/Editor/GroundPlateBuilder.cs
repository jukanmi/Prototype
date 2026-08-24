using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 이미 만들어진 씬·프리팹의 바닥 그림에 <see cref="GroundPlate"/>를 붙인다.
    ///
    /// 새로 짓는 방은 <see cref="SceneLayoutBuilder"/>가 알아서 붙인다. 이건 <b>기존 씬용</b>이다 —
    /// 웨이브 구성처럼 손으로 짜 넣은 것을 날리지 않고 컴포넌트만 얹는다.
    ///
    /// 발판을 안 붙여도 게임은 예전대로 돈다(<see cref="GroundRegistry"/>가 비면 무한 평면).
    /// 낙차를 켜려면 그 전에 이걸 한 번 돌려야 한다.
    /// </summary>
    public static class GroundPlateBuilder
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string RoutePrefabPath = "Assets/Prefabs/Route.prefab";

        /// <summary>발판으로 볼 오브젝트 이름. 통로 타일은 Route 프리팹이 물려 준다.</summary>
        private static readonly string[] FloorNames = { "Floor" };

        [MenuItem("Prototype/발판 - 모든 씬 바닥에 GroundPlate 배선")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[GroundPlateBuilder] 취소됐다.");
                return;
            }

            int prefabs = RigRoutePrefab();
            int scenes = 0;
            int plates = 0;

            string opened = SceneManager.GetActiveScene().path;

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { SceneFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                int added = RigScene(scene);
                if (added <= 0) continue;

                plates += added;
                scenes++;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!string.IsNullOrEmpty(opened))
                EditorSceneManager.OpenScene(opened, OpenSceneMode.Single);

            Debug.Log($"[GroundPlateBuilder] 완료 — 씬 {scenes}개에 발판 {plates}장, 프리팹 {prefabs}개");
        }

        /// <summary>
        /// 통로 타일. 프리팹 원본에 붙여야 씬의 Path_N 인스턴스가 전부 물려받는다 —
        /// 인스턴스마다 붙이면 타일 수가 바뀔 때마다 다시 해야 한다.
        /// </summary>
        private static int RigRoutePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoutePrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[GroundPlateBuilder] {RoutePrefabPath} 이 없다. 통로는 건너뛴다.");
                return 0;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(RoutePrefabPath);
            try
            {
                if (contents.GetComponent<GroundPlate>() != null) return 0;

                contents.AddComponent<GroundPlate>();
                PrefabUtility.SaveAsPrefabAsset(contents, RoutePrefabPath);
                return 1;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static int RigScene(Scene scene)
        {
            int added = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!IsFloor(t.gameObject)) continue;
                    if (t.GetComponent<GroundPlate>() != null) continue;

                    // 프리팹이 소유한 오브젝트에는 인스턴스에서 컴포넌트를 얹지 않는다.
                    // Route 원본에 이미 붙였으므로 여기서 또 붙이면 오버라이드만 쌓인다.
                    if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) continue;

                    Undo.AddComponent<GroundPlate>(t.gameObject);
                    added++;
                }
            }

            return added;
        }

        private static bool IsFloor(GameObject go)
        {
            if (go.GetComponent<Renderer>() == null) return false;

            for (int i = 0; i < FloorNames.Length; i++)
                if (go.name == FloorNames[i]) return true;

            return false;
        }
    }
}
