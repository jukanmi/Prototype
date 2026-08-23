using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 모든 <see cref="SpriteRenderer"/>의 머티리얼을 URP <b>Sprite-Lit-Default → Sprite-Unlit-Default</b>로 바꾼다.
    ///
    /// Sprite-Lit 셰이더는 Renderer2D의 2D 라이팅 패스에서만 그려진다. Universal Renderer(3D)로
    /// 갈아 끼우는 순간(<see cref="RenderPipelineMigrator"/>) 이 머티리얼을 쓴 스프라이트는
    /// 전부 까맣게 나온다 — 캐릭터가 통째로 사라지는 것처럼 보인다.
    ///
    /// 여러 번 돌려도 같은 결과가 나온다.
    /// </summary>
    public static class SpriteMaterialMigrator
    {
        /// <summary>URP 패키지의 고정 GUID. 경로는 패키지 해시가 섞여 버전마다 바뀐다.</summary>
        internal const string LitGuid   = "a97c105638bdf8b4a8650670310a4cd3";
        internal const string UnlitGuid = "9dfc825aed78fcd4ba02077103263b40";

        private const string PrefabFolder = "Assets/Prefabs";

        [MenuItem("Prototype/렌더 - 스프라이트 머티리얼 Unlit으로")]
        public static void MigrateAll()
        {
            Material unlit = Load(UnlitGuid);
            Material lit   = Load(LitGuid);

            if (unlit == null || lit == null)
            {
                Debug.LogError("[SpriteMaterialMigrator] URP 스프라이트 머티리얼을 찾지 못했다. URP 버전을 확인해라.");
                return;
            }

            int prefabs = MigratePrefabs(lit, unlit);
            int scenes  = MigrateScenes(lit, unlit);

            AssetDatabase.SaveAssets();
            Debug.Log($"[SpriteMaterialMigrator] 완료 — 프리팹 {prefabs}개, 씬 {scenes}개 변경");
        }

        private static int MigratePrefabs(Material lit, Material unlit)
        {
            int changed = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    if (!Swap(contents.GetComponentsInChildren<SpriteRenderer>(true), lit, unlit)) continue;

                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    changed++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return changed;
        }

        private static int MigrateScenes(Material lit, Material unlit)
        {
            int changed = 0;

            foreach (string path in RenderPipelineMigrator.AllScenePaths())
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                bool dirty = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                    dirty |= Swap(root.GetComponentsInChildren<SpriteRenderer>(true), lit, unlit);

                if (!dirty) continue;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changed++;
            }

            return changed;
        }

        /// <returns>하나라도 바꿨으면 참.</returns>
        private static bool Swap(SpriteRenderer[] renderers, Material lit, Material unlit)
        {
            bool changed = false;

            foreach (SpriteRenderer sr in renderers)
            {
                // 손으로 지정한 커스텀 머티리얼은 건드리지 않는다. Lit 기본값만 갈아 끼운다.
                if (sr.sharedMaterial != lit) continue;

                sr.sharedMaterial = unlit;
                EditorUtility.SetDirty(sr);

                // 프리팹 인스턴스에서는 이걸 빼먹으면 저장할 때 프리팹 기본값으로 되돌아간다.
                if (PrefabUtility.IsPartOfPrefabInstance(sr))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(sr);

                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// 아직 Sprite-Lit을 물고 있는 곳의 경로 목록. 테스트가 0건인지 확인하는 데 쓴다.
        /// 씬을 열어야 하므로 에디트 모드 테스트에서만 부를 수 있다.
        /// </summary>
        internal static List<string> FindLitReferences()
        {
            var found = new List<string>();
            Material lit = Load(LitGuid);
            if (lit == null) return found;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var contents = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (contents == null) continue;

                foreach (SpriteRenderer sr in contents.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.sharedMaterial == lit) found.Add($"{path} :: {sr.name}");
            }

            foreach (string path in RenderPipelineMigrator.AllScenePaths())
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                        if (sr.sharedMaterial == lit) found.Add($"{path} :: {sr.name}");
            }

            return found;
        }

        private static Material Load(string guid)
            => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
    }
}
