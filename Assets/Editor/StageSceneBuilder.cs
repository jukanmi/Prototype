using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Prototype.YG;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 미니 · 원거리(Soft · Hard) · 보스 스테이지 씬을 굽는다.
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
        private const string SoftScene = LevelFolder + "/" + SceneNames.StageSoft + ".unity";
        private const string HardScene = LevelFolder + "/" + SceneNames.StageHard + ".unity";

        private const string BossPrefabPath = "Assets/Prefabs/Enemy_Boss.prefab";
        private const string MeleePrefabPath = "Assets/Prefabs/Enemy_Melee.prefab";

        /// <summary>적을 놓는 자리. 플레이어가 방 왼쪽(-3)에 서므로 오른쪽에 벌려 둔다.</summary>
        private static readonly Vector3 MiniSpot = new Vector3(2.5f, 0f, 0f);
        private static readonly Vector3 BossSpot = new Vector3(3.5f, 0f, 0f);

        /// <summary>
        /// Soft — 마법사 둘을 위아래로 갈라 놓는다.
        ///
        /// 한 명씩 떼어 잡을 수 있는 간격이다. 견습생의 카이팅 거리가 3이라 한쪽에 붙으면
        /// 그쪽만 물러나고 다른 쪽은 아직 쏘는 중이다 — "쏘는 놈부터 끊는다"를 배우는 배치.
        /// </summary>
        private static readonly Vector3[] SoftSpots =
        {
            new Vector3(2.2f, 0f,  1.4f),
            new Vector3(2.2f, 0f, -1.4f),
        };

        /// <summary>
        /// Hard — 대마법사 넷을 방 오른쪽까지 밀어 두고 근접 둘을 앞에 세운다.
        ///
        /// 대마법사는 사거리 8.8에 카이팅 5라 벽(x = 6)에 등을 붙인 뒤로는 더 못 물러난다.
        /// 그 벽까지 가는 길을 근접 둘이 막는 것이 이 스테이지의 문제다.
        /// </summary>
        private static readonly Vector3[] HardWizardSpots =
        {
            new Vector3(5.2f, 0f,  2.2f),
            new Vector3(5.2f, 0f, -2.2f),
            new Vector3(3.4f, 0f,  0.9f),
            new Vector3(3.4f, 0f, -0.9f),
        };

        private static readonly Vector3[] HardMeleeSpots =
        {
            new Vector3(1.0f, 0f,  1.8f),
            new Vector3(1.0f, 0f, -1.8f),
        };

        [MenuItem("Prototype/스테이지 - 미니·원거리·보스 씬 만들기")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog(
                    "스테이지 씬 만들기",
                    "SampleScene을 복제해 아래 네 씬을 덮어쓴다.\n\n" +
                    $"· {MiniScene}\n· {SoftScene}\n· {HardScene}\n· {BossScene}\n\n" +
                    "네 씬에 손으로 고친 내용이 있다면 사라진다. 계속할까?",
                    "만들기", "취소"))
                return;

            // 지금 열려 있는 씬을 잃지 않게 먼저 묻는다. 아래에서 씬을 통째로 갈아 끼운다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            BuildAll();
        }

        /// <summary>
        /// 대화상자 없이 전부 굽는다. 열려 있는 씬을 <b>저장하지 않고</b> 갈아 끼우므로,
        /// 부르는 쪽이 먼저 저장을 책임져야 한다.
        /// </summary>
        /// <returns>네 씬을 다 구웠으면 참. 실패는 이미 로그로 남겼다.</returns>
        public static bool BuildAll()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
            {
                Debug.LogError($"[StageSceneBuilder] 원본 씬이 없다: {SourceScene}");
                return false;
            }

            EnsureFolder(LevelFolder);

            // 마법사 프리팹 · 데이터가 없으면 Soft · Hard가 통째로 빈 방이 된다.
            // 메뉴를 누르는 순서를 사람이 외워야 하는 상황을 만들지 않는다 — 여기서 먼저 굽는다.
            if (!WizardEnemyBuilder.BuildSilently()) return false;

            // 지울 대상이 열려 있으면 삭제가 꼬인다. 먼저 원본으로 옮겨 네 씬 다 닫아 놓는다.
            EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);

            if (!BuildMini()) return false;
            if (!BuildSoft()) return false;
            if (!BuildHard()) return false;
            if (!BuildBoss()) return false;

            RegisterInBuildSettings(MiniScene, SoftScene, HardScene, BossScene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StageSceneBuilder] 완료 — {MiniScene}, {SoftScene}, {HardScene}, {BossScene}");
            return true;
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

        /// <summary>
        /// Soft — 마법사 견습생 둘.
        ///
        /// 미니 스테이지와 달리 씬에 있던 적을 <b>남기지 않는다.</b> 원거리만 있는 방이어야
        /// "거리를 좁히는 법"이라는 이 스테이지의 문제가 흐려지지 않는다.
        /// </summary>
        private static bool BuildSoft()
        {
            if (!Require(WizardEnemyBuilder.PrefabPath) || !Require(WizardEnemyBuilder.SoftDataPath))
                return false;

            Scene scene = OpenCopy(SoftScene);
            if (!scene.IsValid()) return false;

            var wizard = AssetDatabase.LoadAssetAtPath<GameObject>(WizardEnemyBuilder.PrefabPath);
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(WizardEnemyBuilder.SoftDataPath);

            ClearEnemies(scene);

            for (int i = 0; i < SoftSpots.Length; i++)
                Spawn(scene, wizard, $"Wizard_Soft_{i + 1}", SoftSpots[i], data);

            return Save(scene, SoftScene, SoftSpots.Length);
        }

        /// <summary>
        /// Hard — 대마법사 넷 + 근접 둘.
        ///
        /// 근접을 섞는 것이 핵심이다. 마법사만 여섯을 놓으면 전부 같은 방향으로 물러나
        /// 한 덩어리가 되고, 그러면 Soft와 같은 문제를 숫자만 늘려 낸 것이 된다.
        /// </summary>
        private static bool BuildHard()
        {
            if (!Require(WizardEnemyBuilder.PrefabPath)
                || !Require(WizardEnemyBuilder.HardDataPath)
                || !Require(MeleePrefabPath))
                return false;

            Scene scene = OpenCopy(HardScene);
            if (!scene.IsValid()) return false;

            var wizard = AssetDatabase.LoadAssetAtPath<GameObject>(WizardEnemyBuilder.PrefabPath);
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(WizardEnemyBuilder.HardDataPath);
            var melee = AssetDatabase.LoadAssetAtPath<GameObject>(MeleePrefabPath);

            ClearEnemies(scene);

            for (int i = 0; i < HardWizardSpots.Length; i++)
                Spawn(scene, wizard, $"Wizard_Hard_{i + 1}", HardWizardSpots[i], data);

            // 근접은 프리팹이 들고 있는 데이터 그대로다 — 마법사를 지키는 몸이지 주역이 아니다.
            for (int i = 0; i < HardMeleeSpots.Length; i++)
                Spawn(scene, melee, $"Melee_Hard_{i + 1}", HardMeleeSpots[i], null);

            return Save(scene, HardScene, HardWizardSpots.Length + HardMeleeSpots.Length);
        }

        /// <summary>
        /// 애셋이 있는지만 본다. <b>참조를 들고 나오지 않는다</b> —
        /// <see cref="OpenCopy"/>가 씬을 여는 순간 안 쓰이는 애셋이 언로드되므로,
        /// 그 전에 잡아 둔 참조는 파괴된 채로 살아남아 대입하면 조용히 null이 된다.
        /// 그 증상이 정확히 "Hard의 대마법사가 견습생 수치로 서 있는" 것이었다.
        /// </summary>
        private static bool Require(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) return true;

            Debug.LogError($"[StageSceneBuilder] {path}가 없다. 앞선 빌더가 실패했는지 확인할 것.");
            return false;
        }

        private static void ClearEnemies(Scene scene)
        {
            foreach (Enemy e in FindEnemies(scene))
                Object.DestroyImmediate(e.gameObject);
        }

        /// <summary>
        /// 적 하나를 꽂는다. <paramref name="data"/>를 주면 인스턴스의 수치 테이블을 갈아끼운다 —
        /// 난이도가 프리팹이 아니라 데이터로 갈리므로 마법사는 프리팹이 한 벌뿐이다.
        /// </summary>
        private static void Spawn(Scene scene, GameObject prefab, string name, Vector3 spot, EnemyData data)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = name;
            go.transform.position = spot;
            go.transform.rotation = Quaternion.identity;

            if (data != null)
            {
                Enemy enemy = go.GetComponent<Enemy>();

                // private [SerializeField]는 SerializedObject로만 안전하게 건드린다.
                var so = new SerializedObject(enemy);
                so.FindProperty("data").objectReferenceValue = data;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Undo를 안 거친 변경은 프리팹 인스턴스의 오버라이드 목록에 <b>자동으로 안 실린다.</b>
                // 이 줄이 없으면 저장할 때 값이 프리팹 기본값으로 되돌아간다 —
                // Hard의 대마법사가 조용히 견습생 수치로 서 있게 된다.
                PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
            }

            NormalizeSceneEntity(go);
        }

        /// <summary>보스 스테이지. 기존 적을 전부 지우고 보스 프리팹 하나를 꽂는다.</summary>
        private static bool BuildBoss()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath) == null)
            {
                Debug.LogError(
                    $"[StageSceneBuilder] {BossPrefabPath}가 없다. " +
                    "'Prototype ▸ 보스 - 프리팹 + 데이터 만들기'를 먼저 돌릴 것.");
                return false;
            }

            Scene scene = OpenCopy(BossScene);
            if (!scene.IsValid()) return false;

            // 참조는 씬을 연 뒤에 잡는다(<see cref="Require"/> 참고).
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);

            ClearEnemies(scene);
            Spawn(scene, bossPrefab, "Boss", BossSpot, null);

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

            // 히트박스 레이어. 프리팹에 따라 Default로 남아 있는 것이 있어서, 여기서 맞추지 않으면
            // 충돌 매트릭스가 아군 · 적을 못 가른다.
            var entity = go.GetComponent<Entity>();
            if (entity != null) SceneLayoutBuilder.AssignLayers(entity);

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
