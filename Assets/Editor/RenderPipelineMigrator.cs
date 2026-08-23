using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Prototype.EditorTools
{
    /// <summary>
    /// URP 렌더러를 <b>Renderer2D → Universal Renderer(3D)</b>로 갈아 끼운다.
    ///
    /// 3D 맵 메시가 조명 · 그림자 · 뎁스를 받으려면 3D 렌더러가 있어야 한다. 이 프로젝트는
    /// <c>com.unity.template.universal-2d</c>로 만들어져 <c>UniversalRendererData</c> 에셋이
    /// 아예 없는 상태라, 여기서 처음 만든다.
    ///
    /// 렌더러 에셋 YAML을 손으로 쓰지 않는다 — 셰이더 참조가 수십 개라 하나만 빠져도
    /// 화면이 분홍이 된다. <see cref="UniversalRenderPipelineAsset.LoadBuiltinRendererData"/>가
    /// URP 내부의 <c>ResourceReloader</c>로 그 참조를 전부 채워 주므로 그쪽에 맡긴다.
    ///
    /// 여러 번 돌려도 같은 결과가 나온다.
    /// </summary>
    public static class RenderPipelineMigrator
    {
        private const string UrpAssetPath   = "Assets/Settings/UniversalRP.asset";
        private const string RendererPath   = "Assets/Settings/UniversalRenderer.asset";

        /// <summary>URP가 <see cref="UniversalRenderPipelineAsset.LoadBuiltinRendererData"/>에서 쓰는 고정 경로.</summary>
        private const string RendererTempPath = "Assets/UniversalRenderer.asset";

        [MenuItem("Prototype/렌더 - 3D 렌더러로 전환 (전부)")]
        public static void MigrateAll()
        {
            if (!EditorUtility.DisplayDialog(
                    "3D 렌더러로 전환",
                    "아래를 한 번에 바꾼다.\n\n" +
                    "· URP 렌더러: Renderer2D → Universal Renderer\n" +
                    "· 스프라이트 머티리얼: Sprite-Lit → Sprite-Unlit\n" +
                    "· 씬 라이트: Global Light 2D → Directional Light\n" +
                    "· 에디터 기본 동작: 2D 모드 → 3D 모드\n\n" +
                    "씬과 프리팹이 저장된다. 계속할까?",
                    "전환", "취소"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            SwitchRenderer();
            SpriteMaterialMigrator.MigrateAll();
            SwapSceneLights();
            Set3DBehaviorMode();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RenderPipelineMigrator] 3D 렌더러 전환 완료");
        }

        // ── 1. 렌더러 교체 ───────────────────────────────

        /// <summary>
        /// <see cref="UniversalRenderPipelineAsset"/>의 0번 렌더러를 Universal Renderer로.
        ///
        /// <c>Renderer2D.asset</c>은 지우지 않는다 — 참조만 끊긴 채 남겨 두면 되돌릴 때
        /// 다시 꽂기만 하면 된다.
        /// </summary>
        [MenuItem("Prototype/렌더 - URP 렌더러만 3D로")]
        public static void SwitchRenderer()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urp == null)
            {
                Debug.LogError($"[RenderPipelineMigrator] URP 에셋이 없다: {UrpAssetPath}");
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);

            // scriptableRenderer 로 확인하면 렌더러 인스턴스가 실제로 만들어진다. 목록만 본다.
            bool already = urp.rendererDataList.Length > 0 && urp.rendererDataList[0] is UniversalRendererData;
            if (existing != null && already)
            {
                Debug.Log("[RenderPipelineMigrator] 이미 Universal Renderer다. 넘어간다.");
                return;
            }

            // LoadBuiltinRendererData 는 경로가 고정이라 먼저 자리를 비워 둔다.
            if (AssetDatabase.LoadAssetAtPath<Object>(RendererTempPath) != null)
                AssetDatabase.DeleteAsset(RendererTempPath);

            urp.LoadBuiltinRendererData(RendererType.UniversalRenderer);
            EditorUtility.SetDirty(urp);
            AssetDatabase.SaveAssets();

            // Settings 아래로 모은다. GUID 기반이라 방금 걸린 참조는 따라온다.
            if (existing != null) AssetDatabase.DeleteAsset(RendererPath);

            string moveError = AssetDatabase.MoveAsset(RendererTempPath, RendererPath);
            if (!string.IsNullOrEmpty(moveError))
                Debug.LogWarning($"[RenderPipelineMigrator] 렌더러 에셋 이동 실패({moveError}). {RendererTempPath}에 그대로 둔다.");

            AssetDatabase.SaveAssets();
            Debug.Log("[RenderPipelineMigrator] URP 렌더러 → Universal Renderer");
        }

        // ── 2. 씬 라이트 교체 ────────────────────────────

        /// <summary>
        /// 모든 씬에서 <c>Light2D</c>를 걷어내고, 캐릭터가 있는 씬에는 Directional Light를 세운다.
        ///
        /// Light2D는 Renderer2D의 라이팅 패스 전용이라 3D 렌더러에서는 아무 일도 하지 않는다.
        /// 라이트를 세울 씬을 <see cref="Entity"/> 유무로 가르는 이유: Boot에는 카메라조차 없고
        /// MainMenu는 UI뿐이라 3D 라이트가 필요 없다.
        /// </summary>
        [MenuItem("Prototype/렌더 - 씬 라이트 3D로")]
        public static void SwapSceneLights()
        {
            int touched = 0;

            foreach (string path in AllScenePaths())
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool dirty = StripLight2D(scene);

                if (HasEntity(scene)) dirty |= EnsureDirectionalLight(scene);

                if (!dirty) continue;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                touched++;
            }

            Debug.Log($"[RenderPipelineMigrator] 씬 라이트 정리 — {touched}개 씬 변경");
        }

        private static bool StripLight2D(Scene scene)
        {
            var doomed = new List<GameObject>();

            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Light2D l in root.GetComponentsInChildren<Light2D>(true))
                {
                    // 라이트 하나만 달린 전용 오브젝트면 통째로 지운다. 다른 것이 얹혀 있으면
                    // 컴포넌트만 떼서 그 오브젝트의 역할은 살려 둔다.
                    if (l.gameObject.GetComponents<Component>().Length <= 2) doomed.Add(l.gameObject);
                    else Object.DestroyImmediate(l);
                }

            foreach (GameObject go in doomed) Object.DestroyImmediate(go);

            return doomed.Count > 0;
        }

        /// <summary>
        /// 3D 방향광 하나. 세워 두지 않으면 Lit 머티리얼을 쓴 맵 메시가 새까맣게 나온다.
        /// 각도는 <see cref="TestSceneBuilder"/>가 쓰던 값과 같다.
        /// </summary>
        public static bool EnsureDirectionalLight(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<Light>(true) != null) return false;

            var go = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(go, scene);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            return true;
        }

        private static bool HasEntity(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<Entity>(true) != null) return true;

            return false;
        }

        // ── 3. 에디터 기본 동작 ──────────────────────────

        /// <summary>
        /// 2D 모드로 두면 새로 넣는 텍스처가 자동으로 Sprite로 임포트되고 씬뷰도 2D로 열린다.
        /// 3D 맵 에셋을 다루기 시작하면 매번 되돌려야 하므로 여기서 바꾼다.
        /// </summary>
        public static void Set3DBehaviorMode()
        {
            if (EditorSettings.defaultBehaviorMode == EditorBehaviorMode.Mode3D) return;

            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode3D;
            Debug.Log("[RenderPipelineMigrator] 에디터 기본 동작 → 3D 모드");
        }

        // ── 공용 ─────────────────────────────────────────

        /// <summary>
        /// <c>Assets/Scenes</c> 아래 모든 씬. 빌드 세팅 목록을 쓰지 않는 이유는
        /// <c>SkillTestScene</c>처럼 미등록인 씬도 함께 고쳐야 하기 때문이다.
        /// </summary>
        internal static IEnumerable<string> AllScenePaths()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
                yield return AssetDatabase.GUIDToAssetPath(guid);
        }
    }
}
