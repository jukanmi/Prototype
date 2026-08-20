using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Prototype.YG;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 훈련장을 통째로 굽는다 — 허수아비 애셋 · 프리팹 · <c>Stage_Training</c> 씬.
    ///
    /// <b>여기가 "시스템을 설명하는 셋업"이다.</b> 전투 시스템은 다섯 조각이 맞물려 돌아가는데
    /// 실전 씬에서는 적 여럿이 동시에 움직여 무엇이 무엇의 결과인지 안 보인다. 훈련장은
    /// 변수를 하나만 남긴다 — 적은 하나, 반격하지 않고, 죽지 않고, 손패는 매번 같은 4장이다.
    ///
    /// 그래서 이 씬에서 보이는 건 오직 <b>콤보 체인 한 싸이클</b>이다:
    /// <code>
    ///   모으기 → 띄우기 → 공격기 → 밀치기(벽) → 벽바운드 → 다운
    /// </code>
    ///
    /// 허수아비는 <b>보스 프리팹에서 떠 온다</b>. 무게(impulseDamping) · 중력 · 체공 ·
    /// 몸통 크기가 보스와 같아야 여기서 잰 넉백 거리와 체공 시간이 보스전에서 재현된다.
    /// 잡몹 몸으로 재면 훈련장이 거짓말을 한다.
    ///
    /// <see cref="StageSceneBuilder"/>와 같은 규약이다 — SampleScene을 복제해서 쓰므로
    /// 입력 · HUD · 방 · 카메라 · 충돌 설정이 전부 딸려 온다. <b>매번 다시 굽는다</b>:
    /// 이 씬을 손으로 고쳐 뒀다면 사라진다.
    /// </summary>
    public static class TrainingSceneBuilder
    {
        private const string SourceScene = "Assets/Scenes/SampleScene.unity";
        private const string LevelFolder = "Assets/Scenes/Level";
        private const string TrainingScene = LevelFolder + "/" + SceneNames.StageTraining + ".unity";

        private const string BossPrefabPath = "Assets/Prefabs/Enemy_Boss.prefab";
        private const string DummyPrefabPath = "Assets/Prefabs/Enemy_Dummy.prefab";

        private const string DataFolder = "Assets/Data/Enemy";
        private const string DummyBrainPath = DataFolder + "/Brain_Dummy.asset";
        private const string DummyDataPath = DataFolder + "/Enemy_Dummy.asset";

        private const string SkillFolder = "Assets/Data/Skills";

        /// <summary>
        /// 허수아비가 서는 자리. 플레이어가 방 왼쪽(-3)에서 시작하므로 오른쪽에 세운다.
        ///
        /// 오른쪽 벽(x = 6.25)까지 3.75를 비워 둔다 — 밀치기가 <b>날아가는 거리</b>까지 보여야 하기
        /// 때문이다. 벽에 바짝 붙여 세우면 밀자마자 닿아 벽바운드만 보이고 밀치기는 안 보인다.
        /// </summary>
        private static readonly Vector3 DummySpot = new Vector3(2.5f, 0f, 0f);

        /// <summary>
        /// 견본 콤보. 정석 4슬롯 체인을 <b>한 직업으로</b> 채운다 —
        /// 여러 직업을 섞으면 시전자가 매번 바뀌면서 이동 거리까지 변수로 들어온다.
        /// 마법사는 넷 다 갖췄고 전부 원거리라 카메라 밖으로 나가지 않는다.
        /// </summary>
        private static readonly string[] ShowcaseChain =
        {
            "SK_WZ중력장",       // Gather   — 모아서 시작
            "SK_WZ융기",         // Launcher — 띄운다
            "SK_WZ마력탄연사",   // Strike   — 공중에서 유지타
            "SK_WZ충격파",       // Push     — 벽으로 보낸다
        };

        [MenuItem("Prototype/훈련장 - 허수아비 + 견본 콤보 씬 만들기")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog(
                    "훈련장 만들기",
                    "SampleScene을 복제해 아래를 덮어쓴다.\n\n" +
                    $"· {TrainingScene}\n· {DummyPrefabPath}\n· {DummyDataPath}\n· {DummyBrainPath}\n\n" +
                    "이 씬·프리팹에 손으로 고친 내용이 있다면 사라진다. 계속할까?",
                    "만들기", "취소"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            BuildWithoutPrompt();
        }

        /// <summary>
        /// 확인 창 없이 곧바로 굽는다. 자동화(스크립트 실행 · 배치)가 부르는 자리다.
        ///
        /// <b>열려 있던 씬이 훈련장으로 바뀐다.</b> 부르는 쪽이 저장 여부를 이미 책임진 뒤라고 본다.
        /// </summary>
        public static bool BuildWithoutPrompt()
        {
            EnsureFolder(LevelFolder);
            EnsureFolder(DataFolder);

            GameObject dummyPrefab = BuildDummyPrefab();
            if (dummyPrefab == null) return false;

            // 지울 대상이 열려 있으면 삭제가 꼬인다. 먼저 원본으로 옮겨 둔다.
            EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);

            if (!BuildScene(dummyPrefab)) return false;

            RegisterInBuildSettings(TrainingScene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TrainingSceneBuilder] 완료 — {TrainingScene}");
            return true;
        }

        // ── 허수아비 애셋 ───────────────────────────────

        /// <summary>판단을 비운 브레인. 허수아비가 반격하지 않는 이유의 전부다.</summary>
        private static DummyBrainAsset EnsureBrain()
        {
            var brain = AssetDatabase.LoadAssetAtPath<DummyBrainAsset>(DummyBrainPath);
            if (brain != null) return brain;

            // 타입이 다른 애셋이 그 자리에 있으면 갈아끼운다.
            if (AssetDatabase.LoadAssetAtPath<Object>(DummyBrainPath) != null)
                AssetDatabase.DeleteAsset(DummyBrainPath);

            brain = ScriptableObject.CreateInstance<DummyBrainAsset>();
            AssetDatabase.CreateAsset(brain, DummyBrainPath);
            return brain;
        }

        /// <summary>
        /// 허수아비 수치. 체력은 크게, 공격력은 0이다 —
        /// <see cref="TrainingDummy"/>가 체력을 되돌리긴 하지만 최대치가 작으면
        /// 체력바가 매 타격마다 바닥을 쳐서 "얼마나 들어갔나"가 안 읽힌다.
        /// </summary>
        private static EnemyData EnsureData(EnemyBrainAsset brain)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DummyDataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, DummyDataPath);
            }

            data.enemyId = "Enemy_Dummy";
            data.enemyName = "허수아비";
            data.hp = 5000f;
            data.atk = 0f;
            data.brain = brain;

            // 움직이지도 때리지도 않는다. 브레인이 이미 None을 내지만 수치도 같이 잠가 둔다 —
            // 브레인을 갈아끼워도 허수아비가 갑자기 달려들지 않게.
            data.moveSpeed = 0f;
            data.attackRange = 0f;
            data.attackInterval = 999f;
            data.specialRange = 0f;
            data.leashRange = 0f;
            data.preferredMinRange = 0f;
            data.basicProjectile = null;

            data.exp = 0;
            data.gold = 0;

            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>
        /// 보스 프리팹을 떠서 허수아비로 만든다. <b>무게를 물려받는 게 목적</b>이라
        /// Physics · Combat 수치는 한 줄도 건드리지 않는다.
        /// </summary>
        private static GameObject BuildDummyPrefab()
        {
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (bossPrefab == null)
            {
                Debug.LogError(
                    $"[TrainingSceneBuilder] {BossPrefabPath}가 없다. " +
                    "'Prototype ▸ 보스 - 프리팹 + 데이터 만들기'를 먼저 돌릴 것.");
                return null;
            }

            EnemyBrainAsset brain = EnsureBrain();
            EnemyData data = EnsureData(brain);

            // 프리팹 애셋을 직접 편집하지 않는다. 사본을 조립해 통째로 저장한다(EnemyPrefabBuilder와 같은 규약).
            var root = Object.Instantiate(bossPrefab);
            root.name = "Enemy_Dummy";
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;

            WireDummy(root, data, brain);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, DummyPrefabPath);
            Object.DestroyImmediate(root);

            data.prefab = saved;
            EditorUtility.SetDirty(data);

            Debug.Log($"[TrainingSceneBuilder] {DummyPrefabPath} 저장 — 보스 무게 그대로", saved);
            return saved;
        }

        /// <summary>데이터 · 브레인을 갈아끼우고 <see cref="TrainingDummy"/>를 붙인다.</summary>
        private static void WireDummy(GameObject root, EnemyData data, EnemyBrainAsset brain)
        {
            var enemy = root.GetComponent<Enemy>();
            if (enemy != null)
            {
                // private [SerializeField]는 SerializedObject로만 안전하게 건드린다.
                var so = new SerializedObject(enemy);
                so.FindProperty("data").objectReferenceValue = data;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var control = root.GetComponent<EnemyControl>();
            if (control != null)
            {
                var so = new SerializedObject(control);
                so.FindProperty("brain").objectReferenceValue = brain;

                SerializedProperty p = so.FindProperty("parameters");
                p.FindPropertyRelative("attackRange").floatValue = 0f;
                p.FindPropertyRelative("leashRange").floatValue = 0f;
                p.FindPropertyRelative("preferredMinRange").floatValue = 0f;
                p.FindPropertyRelative("specialRange").floatValue = 0f;

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 보스 패턴 실행기가 붙어 있으면 브레인이 침묵해도 자기 차례를 돌린다. 떼어 낸다.
            foreach (var special in root.GetComponents<MonoBehaviour>())
                if (special is IEnemySpecialAction)
                    Object.DestroyImmediate(special);

            if (root.GetComponent<TrainingDummy>() == null)
                root.AddComponent<TrainingDummy>();
        }

        // ── 씬 ─────────────────────────────────────────

        private static bool BuildScene(GameObject dummyPrefab)
        {
            Scene scene = OpenCopy(TrainingScene);
            if (!scene.IsValid()) return false;

            foreach (Enemy e in FindEnemies(scene))
                Object.DestroyImmediate(e.gameObject);

            var dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab, scene);
            dummy.name = "Dummy";
            dummy.transform.position = DummySpot;
            dummy.transform.rotation = Quaternion.identity;

            NormalizeSceneEntity(dummy);
            WireFixedHand(scene);

            int count = FindEnemies(scene).Count;
            if (count != 1)
            {
                Debug.LogError($"[TrainingSceneBuilder] 훈련장에 적이 {count}명이다. 1명이어야 한다.");
                return false;
            }

            if (!EditorSceneManager.SaveScene(scene, TrainingScene))
            {
                Debug.LogError($"[TrainingSceneBuilder] 씬 저장 실패: {TrainingScene}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 손패를 견본 체인으로 고정한다. 이걸 안 하면 훈련장에서도 매번 다른 4장이 와서
        /// "같은 콤보를 다시 굴린다"가 불가능하다.
        /// </summary>
        private static void WireFixedHand(Scene scene)
        {
            BulletTimeController bt = FindInScene<BulletTimeController>(scene);
            if (bt == null)
            {
                Debug.LogWarning("[TrainingSceneBuilder] 씬에 BulletTimeController가 없어 견본 손패를 못 꽂았다.");
                return;
            }

            var so = new SerializedObject(bt);
            so.FindProperty("useFixedHand").boolValue = true;

            SerializedProperty list = so.FindProperty("fixedHand");
            list.ClearArray();

            int filled = 0;
            foreach (string assetName in ShowcaseChain)
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>($"{SkillFolder}/{assetName}.asset");
                if (skill == null)
                {
                    Debug.LogWarning($"[TrainingSceneBuilder] 견본 스킬을 못 찾았다: {assetName}");
                    continue;
                }

                list.InsertArrayElementAtIndex(filled);
                list.GetArrayElementAtIndex(filled).objectReferenceValue = skill;
                filled++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bt);

            Debug.Log($"[TrainingSceneBuilder] 견본 손패 {filled}장 고정 — {string.Join(" → ", ShowcaseChain)}", bt);
        }

        // ── 씬 조작 (StageSceneBuilder와 같은 규약) ──────

        private static Scene OpenCopy(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                AssetDatabase.DeleteAsset(path);

            if (!AssetDatabase.CopyAsset(SourceScene, path))
            {
                Debug.LogError($"[TrainingSceneBuilder] 씬 복제 실패: {SourceScene} → {path}");
                return default;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        /// <summary>비활성 오브젝트까지 센다. 꺼 둔 적이 남아 있으면 훈련장이 아니다.</summary>
        private static List<Enemy> FindEnemies(Scene scene)
        {
            var found = new List<Enemy>();

            foreach (GameObject root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<Enemy>(true));

            return found;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>씬에 새로 꽂은 캐릭터를 SampleScene의 다른 캐릭터와 같은 규약으로 맞춘다.</summary>
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
            Debug.Log($"[TrainingSceneBuilder] Build Settings에 씬 {added}개 등록");
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
