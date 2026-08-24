using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Prototype.YG;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 아레나 스테이지를 굽는다. 이 프로젝트에서 <b>카메라가 실제로 스크롤되는</b> 씬들이다.
    ///
    /// <code>
    ///   [아레나1 / 1R] ── 통로 ── [아레나2 / 2R]
    /// </code>
    ///
    /// 두 개를 굽는다. 형태는 같고 <b>통로 배경과 라운드 표만 다르다</b>:
    /// <list type="bullet">
    /// <item><c>Stage_02</c> — <c>Path.prefab</c> 통로. 벽에서 나오는 규칙을 배우는 방.</item>
    /// <item><c>Stage_05</c> — <c>Route.prefab</c> 통로. 미니 룸에서 손을 풀고 통로를 지나
    /// <b>보스방</b>에 들어간다. 구성은 기존 <c>Stage_Mini</c> · <c>Stage_Boss</c>를 그대로
    /// 옮긴 것이라 그 두 씬은 손대지 않는다 — 단독 실행용으로 그대로 남는다.</item>
    /// </list>
    ///
    /// <b>배경이 하나뿐이어도 굽는다.</b> 통로 배경은 형태가 아니라 그림이라, 한쪽이 없다고
    /// 스테이지를 통째로 못 만드는 것은 과하다 — 남아 있는 쪽을 대신 쓰고 경고만 남긴다.
    /// 나중에 전용 배경을 만들어 두면 그때부터 자동으로 그쪽을 쓴다.
    ///
    /// <see cref="StageWaveSceneBuilder"/>와 같은 규약으로 <c>SampleScene</c>을 복제해
    /// 플레이어 · 동료 · 입력 · HUD를 물려받고, 그 위에 스테이지 형태를 다시 짓는다:
    ///
    /// <list type="bullet">
    /// <item>원본 방의 <b>좌우 벽을 걷어내고</b> 스테이지 전체 길이의 앞뒤 벽을 새로 세운다.
    /// 통로가 방 밖으로 이어져야 하므로 방 하나짜리 충돌은 그대로 쓸 수 없다.</item>
    /// <item>아레나2는 원본 방의 <b>그림</b>(바닥 · 뒷벽 · 지평선)을 복제해 오른쪽에 놓는다.</item>
    /// <item>통로 바닥은 <c>Path.prefab</c>을 이어 붙인다. 길이는 화면 폭의 1.5배를 목표로
    /// 타일 수를 계산한다 — 화면보다 짧으면 카메라 클램프가 한 점으로 접혀서
    /// <b>통로에서 스크롤이 아예 안 일어난다.</b></item>
    /// <item>아레나 경계마다 <see cref="ArenaGate"/>를 세운다.</item>
    /// <item>카메라의 <see cref="CameraFollow"/>를 <b>켠다</b>. 웨이브 씬은 꺼 둔 채로 둔다.</item>
    /// </list>
    ///
    /// <b>매번 SampleScene에서 다시 굽는다.</b> 이 씬을 손으로 고쳐 뒀다면 사라진다.
    /// </summary>
    public static class ArenaSceneBuilder
    {
        private const string SourceScene = "Assets/Scenes/SampleScene.unity";
        private const string LevelFolder = "Assets/Scenes/Level";
        private const string PathPrefabPath = "Assets/Prefabs/Path.prefab";
        private const string RoutePrefabPath = "Assets/Prefabs/Route.prefab";

        private const string DataFolder = "Assets/Data/Enemy";
        private const string MeleeDataPath = DataFolder + "/Enemy_Melee.asset";
        private const string ChargerDataPath = DataFolder + "/Enemy_Charger.asset";
        private const string RangedDataPath = DataFolder + "/Enemy_Ranged.asset";
        private const string BossDataPath = DataFolder + "/Enemy_Boss.asset";

        /// <summary>굽는 대상. 형태는 같고 통로 배경과 라운드 표만 다르다.</summary>
        private struct Plan
        {
            public int stageNumber;

            /// <summary>이 스테이지 전용 통로 배경.</summary>
            public string corridorPrefabPath;

            /// <summary>전용 배경이 없을 때 대신 쓸 배경. 통로 배경은 그림일 뿐이라 빌림이 허용된다.</summary>
            public string fallbackPrefabPath;
        }

        private static readonly Plan[] Plans =
        {
            new Plan
            {
                stageNumber = StageWaveCatalog.ArenaStageNumber,
                corridorPrefabPath = PathPrefabPath,
                fallbackPrefabPath = RoutePrefabPath,
            },
            new Plan
            {
                stageNumber = StageWaveCatalog.BossStageNumber,
                corridorPrefabPath = RoutePrefabPath,
                fallbackPrefabPath = PathPrefabPath,
            },
        };

        private static string ScenePath(int stageNumber) => $"{LevelFolder}/{SceneNames.Stage(stageNumber)}.unity";

        // ── 방 규격 (SceneLayoutBuilder와 같은 값) ──
        private const float ArenaHalfX = WaveSpawnPlanner.RoomHalfX;   // 6
        private const float ArenaHalfZ = WaveSpawnPlanner.RoomHalfZ;   // 3
        private const float WallHeight = 4f;
        private const float WallThickness = 0.5f;
        private const int WallLayer = 9;

        /// <summary>
        /// 통로 길이를 정할 때 기준으로 삼는 화면 폭. 직교 size 5 · 16:9 기준이다.
        ///
        /// 실제 종횡비는 게임 뷰 설정에 따라 달라지지만 <b>굽는 시점에는 알 수 없다</b>.
        /// 기준을 하나 정해 두지 않으면 에디터 창 크기에 따라 스테이지 길이가 바뀐다.
        /// </summary>
        private const float ReferenceScreenWidth = 2f * 5f * (16f / 9f);

        /// <summary>통로 길이 목표. 화면 폭의 1.5배 — 이보다 짧으면 스크롤이 안 생긴다.</summary>
        private const float CorridorTarget = ReferenceScreenWidth * 1.5f;

        [MenuItem("Prototype/스테이지 - 아레나 스테이지 만들기 (2 · 5보스)")]
        public static void Build()
        {
            var paths = new string[Plans.Length];
            for (int i = 0; i < paths.Length; i++) paths[i] = ScenePath(Plans[i].stageNumber);

            if (!EditorUtility.DisplayDialog(
                    "아레나 스테이지 만들기",
                    "SampleScene을 복제해 아래 씬을 덮어쓴다.\n\n" +
                    "· " + string.Join("\n· ", paths) + "\n\n" +
                    "이 씬들에 손으로 고친 내용이 있다면 사라진다. 계속할까?",
                    "만들기", "취소"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
            {
                Debug.LogError($"[ArenaSceneBuilder] 원본 씬이 없다: {SourceScene}");
                return;
            }

            EnemyData melee = Load(MeleeDataPath);
            EnemyData charger = Load(ChargerDataPath);
            EnemyData ranged = Load(RangedDataPath);
            EnemyData boss = Load(BossDataPath);

            if (melee == null || charger == null || ranged == null)
            {
                Debug.LogError(
                    "[ArenaSceneBuilder] 적 데이터가 없다. " +
                    "'Prototype ▸ 적 - 근접·원거리·돌진 프리팹 만들기'를 먼저 돌릴 것.");
                return;
            }

            // 보스는 다른 빌더가 만든다. 없으면 5스테이지가 빈 방이 되므로 여기서 끊는다.
            if (boss == null || boss.prefab == null)
            {
                Debug.LogError(
                    "[ArenaSceneBuilder] 보스 데이터가 없다. " +
                    "'Prototype ▸ 보스 - 프리팹 + 데이터 만들기'를 먼저 돌릴 것.");
                return;
            }

            EnsureFolder(LevelFolder);
            EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);

            foreach (Plan plan in Plans)
                if (!BuildStage(plan, melee, charger, ranged, boss)) return;

            RegisterInBuildSettings(paths);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ArenaSceneBuilder] 완료 — 아레나 스테이지 {Plans.Length}개");
        }

        private static bool BuildStage(Plan plan, EnemyData melee, EnemyData charger,
                                       EnemyData ranged, EnemyData boss)
        {
            GameObject corridorPrefab = LoadCorridor(in plan);
            if (corridorPrefab == null) return false;

            string path = ScenePath(plan.stageNumber);

            Scene scene = OpenCopy(path);
            if (!scene.IsValid()) return false;

            if (!Assemble(scene, plan.stageNumber, corridorPrefab, melee, charger, ranged, boss))
                return false;

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                Debug.LogError($"[ArenaSceneBuilder] 씬 저장 실패: {path}");
                return false;
            }

            Debug.Log($"[ArenaSceneBuilder] {path} — {StageWaveCatalog.ThemeOf(plan.stageNumber)}");
            return true;
        }

        /// <summary>
        /// 통로 배경을 집는다. 전용 배경이 없으면 다른 스테이지 것을 빌리고 경고만 남긴다 —
        /// 그림 하나 때문에 스테이지를 통째로 못 굽는 것은 과하다.
        /// 둘 다 없을 때만 끊는다. 그때는 통로가 바닥 없는 허공이 된다.
        /// </summary>
        private static GameObject LoadCorridor(in Plan plan)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(plan.corridorPrefabPath);
            if (prefab != null) return prefab;

            var fallback = AssetDatabase.LoadAssetAtPath<GameObject>(plan.fallbackPrefabPath);
            if (fallback != null)
            {
                Debug.LogWarning(
                    $"[ArenaSceneBuilder] 스테이지 {plan.stageNumber}: 통로 배경 {plan.corridorPrefabPath} 이 없다. " +
                    $"{plan.fallbackPrefabPath} 을 대신 쓴다.");
                return fallback;
            }

            Debug.LogError(
                $"[ArenaSceneBuilder] 통로 배경이 하나도 없다: {plan.corridorPrefabPath} · {plan.fallbackPrefabPath}");
            return null;
        }

        // ── 조립 ────────────────────────────────────────

        private static bool Assemble(Scene scene, int stageNumber, GameObject corridorPrefab,
                                     EnemyData melee, EnemyData charger, EnemyData ranged, EnemyData boss)
        {
            // 씬에 놓인 적은 전부 지운다. 남으면 1라운드가 열리기도 전에 싸움이 시작되고,
            // 디렉터가 세는 목록에도 안 들어가 라운드가 끝나지 않는다.
            foreach (Enemy e in FindEnemies(scene))
                Object.DestroyImmediate(e.gameObject);

            GameObject room = FindRoot(scene, "Room");
            if (room == null)
            {
                Debug.LogError("[ArenaSceneBuilder] SampleScene에 Room이 없다. " +
                               "'Prototype ▸ 씬 벨트스크롤 배치로 정리'를 먼저 돌릴 것.");
                return false;
            }

            // ── 구간 좌표 ──
            float a1Min = -ArenaHalfX, a1Max = ArenaHalfX;

            float tileWidth = MeasureWidth(corridorPrefab);
            if (tileWidth <= 0.01f)
            {
                Debug.LogError($"[ArenaSceneBuilder] {corridorPrefab.name}의 가로 폭을 읽지 못했다.");
                return false;
            }

            int tiles = StageLayoutRules.TileCount(CorridorTarget, tileWidth);
            float corridorMin = a1Max;
            float corridorMax = corridorMin + tileWidth * tiles;

            float a2Min = corridorMax, a2Max = a2Min + ArenaHalfX * 2f;

            // ── 형태 ──
            BuildOuterWalls(room, a1Min, a2Max);
            CloneArenaVisual(room, offsetX: (a2Min + a2Max) * 0.5f);
            BuildCorridor(scene, corridorPrefab, corridorMin, tileWidth, tiles);

            ArenaGate exitGate = BuildGate(scene, "Gate_Arena1_Exit", a1Max);
            ArenaGate entryGate = BuildGate(scene, "Gate_Arena2_Entry", a2Min);

            // ── 두뇌 ──
            var host = new GameObject("StageRunner");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.transform.position = Vector3.zero;

            EnemySpawnService spawner = host.AddComponent<EnemySpawnService>();

            // 애셋을 여기서 <b>다시</b> 읽는다. Build 에서 한 번 읽어 들고 오면
            // 그 사이의 DeleteAsset · ImportAsset · OpenScene 을 거치며 참조가 죽어
            // (유니티의 가짜 null) 조용히 null 이 꽂힌다.
            WireSpawner(spawner,
                        Load(MeleeDataPath), Load(ChargerDataPath),
                        Load(RangedDataPath), Load(BossDataPath));

            // 배선이 실제로 들어갔는지 확인하고 아니면 굽기를 거부한다.
            // 안 그러면 씬은 멀쩡해 보이는데 해당 역할의 적이 <b>영영 안 나오고</b>,
            // 라운드는 소환 실패를 클리어로 처리해 문만 열린다 — 화면에 단서가 하나도 없다.
            if (!VerifySpawner(spawner, stageNumber)) return false;

            StageBounds bounds = host.AddComponent<StageBounds>();

            ArenaDirector arena1 = NewArena(scene, stageNumber, 1, a1Min, a1Max, null, exitGate, spawner);
            ArenaDirector arena2 = NewArena(scene, stageNumber, 2, a2Min, a2Max, entryGate, null, spawner);

            var sections = new[]
            {
                StageSection.Of(SectionKind.Arena, a1Min, a1Max),
                StageSection.Of(SectionKind.Corridor, corridorMin, corridorMax),
                StageSection.Of(SectionKind.Arena, a2Min, a2Max),
            };

            host.AddComponent<StageRunner>().Configure(sections, new[] { arena1, arena2 }, bounds);

            // ── 카메라 · 출구 ──
            EnableScrollingCamera(scene);
            SetExitLine(scene, a2Max - 1f);

            Debug.Log($"[ArenaSceneBuilder] 스테이지 {stageNumber} 구간 — 아레나1 [{a1Min:0.##}, {a1Max:0.##}] · " +
                      $"통로 [{corridorMin:0.##}, {corridorMax:0.##}] (타일 {tiles}장) · " +
                      $"아레나2 [{a2Min:0.##}, {a2Max:0.##}]");

            return true;
        }

        /// <summary>
        /// 스테이지 전체를 감싸는 벽.
        ///
        /// 원본 방의 <b>좌우 벽은 지운다</b> — 통로가 방 밖으로 이어져야 하므로 x = ±6에
        /// 벽이 남아 있으면 아레나1에서 영영 못 나간다. 앞뒤 벽은 스테이지 길이만큼 늘린다.
        /// </summary>
        private static void BuildOuterWalls(GameObject room, float minX, float maxX)
        {
            DestroyChild(room, "Wall_Left");
            DestroyChild(room, "Wall_Right");

            float centerX = (minX + maxX) * 0.5f;
            float length = (maxX - minX) + WallThickness * 2f;

            MakeWall(room, "Wall_Back",
                     new Vector3(centerX, WallHeight * 0.5f, ArenaHalfZ + WallThickness * 0.5f),
                     new Vector3(length, WallHeight, WallThickness));

            MakeWall(room, "Wall_Front",
                     new Vector3(centerX, WallHeight * 0.5f, -ArenaHalfZ - WallThickness * 0.5f),
                     new Vector3(length, WallHeight, WallThickness));

            MakeWall(room, "Wall_StageLeft",
                     new Vector3(minX - WallThickness * 0.5f, WallHeight * 0.5f, 0f),
                     new Vector3(WallThickness, WallHeight, ArenaHalfZ * 2f));

            MakeWall(room, "Wall_StageRight",
                     new Vector3(maxX + WallThickness * 0.5f, WallHeight * 0.5f, 0f),
                     new Vector3(WallThickness, WallHeight, ArenaHalfZ * 2f));
        }

        /// <summary>
        /// 아레나2의 그림. 원본 방의 바닥 · 뒷벽 · 지평선을 통째로 복제해 오른쪽으로 옮긴다.
        /// 새로 그리지 않는 이유는 바닥이 평행사변형 메시라서다 — 좌표 규칙을 두 번 적으면
        /// 언젠가 한쪽만 어긋난다.
        /// </summary>
        private static void CloneArenaVisual(GameObject room, float offsetX)
        {
            var clone = new GameObject("Arena2_Visual");
            clone.transform.SetParent(room.transform, false);
            clone.transform.localPosition = new Vector3(offsetX, 0f, 0f);

            foreach (string name in new[] { "Floor", "BackWall", "Horizon" })
            {
                Transform src = room.transform.Find(name);
                if (src == null) continue;

                GameObject copy = Object.Instantiate(src.gameObject, clone.transform);
                copy.name = name;
                copy.transform.localPosition = src.localPosition;
                copy.transform.localRotation = src.localRotation;
                copy.transform.localScale = src.localScale;
            }
        }

        /// <summary>
        /// 통로 바닥. 배경 한 장이 통로보다 짧으면 바닥이 끊겨 보이므로 이어 붙인다.
        /// </summary>
        private static void BuildCorridor(Scene scene, GameObject prefab,
                                          float startX, float tileWidth, int tiles)
        {
            var root = new GameObject("Corridor");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = Vector3.zero;

            for (int i = 0; i < tiles; i++)
            {
                var tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                tile.name = $"Path_{i + 1}";
                tile.transform.SetParent(root.transform, false);

                // 프리팹이 자기 위치에 저작돼 있어도 여기서 덮어쓴다 — 이어 붙이는 자리는 우리가 정한다.
                Vector3 p = tile.transform.localPosition;
                p.x = startX + tileWidth * (i + 0.5f);
                tile.transform.localPosition = p;
            }
        }

        /// <summary>
        /// 아레나 경계를 막는 문. 콜라이더 하나와 문짝 그림 한 장이다.
        ///
        /// 문짝은 <b>논리 좌표 그대로</b> YZ 평면에 세운다 — 카메라가 기울어 깊이를 보여 주므로
        /// 콜라이더가 막는 자리와 그림이 정확히 같은 자리에 온다.
        /// </summary>
        private static ArenaGate BuildGate(Scene scene, string name, float x)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(x, 0f, 0f);
            go.layer = WallLayer;

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = new Vector3(0f, WallHeight * 0.5f, 0f);
            box.size = new Vector3(WallThickness, WallHeight, ArenaHalfZ * 2f);

            // 문짝. 깊이 방향으로 기운 방을 가로지르므로 세로로 넉넉히 잡는다.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(go.transform, false);
            panel.transform.localPosition = new Vector3(0f, WallHeight * 0.5f, 0f);
            // Y축 90° — 판이 깊이(Z) 방향을 가로지르게 눕힌다. 콜라이더와 같은 크기다.
            panel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            panel.transform.localScale = new Vector3(ArenaHalfZ * 2f, WallHeight, 1f);

            var sr = panel.AddComponent<SpriteRenderer>();
            sr.sprite = FindSquareSprite();
            sr.color = new Color(0.55f, 0.18f, 0.16f, 0.95f);
            sr.sortingOrder = -400;   // 캐릭터(-300 언저리)보다 뒤, 배경(-10000)보다 앞

            ArenaGate gate = go.AddComponent<ArenaGate>();
            gate.SetPanel(panel.transform);

            var so = new SerializedObject(gate);
            so.FindProperty("panel").objectReferenceValue = panel.transform;
            so.FindProperty("closedY").floatValue = 2f;
            so.FindProperty("openLift").floatValue = 9f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return gate;
        }

        private static ArenaDirector NewArena(Scene scene, int stageNumber, int number,
                                              float minX, float maxX,
                                              ArenaGate entry, ArenaGate exit, EnemySpawnService spawner)
        {
            var go = new GameObject($"Arena_{number}");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3((minX + maxX) * 0.5f, 0f, 0f);

            ArenaDirector arena = go.AddComponent<ArenaDirector>();
            arena.Configure(stageNumber, number, minX, maxX, entry, exit, spawner);

            // Configure 가 넣은 값을 직렬화에 굳힌다. 런타임 대입만으로는 씬에 저장되지 않는다.
            var so = new SerializedObject(arena);
            so.FindProperty("stageNumber").intValue = stageNumber;
            so.FindProperty("arenaNumber").intValue = number;
            so.FindProperty("minX").floatValue = minX;
            so.FindProperty("maxX").floatValue = maxX;
            so.FindProperty("entryGate").objectReferenceValue = entry;
            so.FindProperty("exitGate").objectReferenceValue = exit;
            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.ApplyModifiedPropertiesWithoutUndo();

            return arena;
        }

        /// <summary>
        /// 이 씬에서만 카메라가 따라간다. 웨이브 씬은 방 하나가 한 화면이라 꺼 둔 그대로다.
        /// </summary>
        private static void EnableScrollingCamera(Scene scene)
        {
            Camera cam = FindCamera(scene);
            if (cam == null)
            {
                Debug.LogWarning("[ArenaSceneBuilder] 씬에 카메라가 없다. 스크롤을 못 켠다.");
                return;
            }

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();

            follow.enabled = true;

            Player player = Object.FindAnyObjectByType<Player>();
            if (player != null) follow.SetTarget(player.transform);

            var so = new SerializedObject(follow);
            // SceneLayoutBuilder가 잡아 둔 구도를 그대로 유지한다.
            so.FindProperty("fixedY").floatValue = cam.transform.position.y;
            so.FindProperty("fixedZ").floatValue = cam.transform.position.z;
            so.FindProperty("deadZoneHalfWidth").floatValue = 1.5f;
            // 고정 경계는 안 쓴다 — StageBounds가 매 프레임 넘겨 준다.
            so.FindProperty("minX").floatValue = 0f;
            so.FindProperty("maxX").floatValue = 0f;
            if (player != null) so.FindProperty("target").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(follow);
        }

        /// <summary>
        /// 다음 스테이지로 넘어가는 선. 기본값(x = 5)은 방 하나짜리 씬 기준이라
        /// 그대로 두면 <b>아레나1을 지나자마자 스테이지가 넘어간다.</b>
        /// </summary>
        private static void SetExitLine(Scene scene, float exitX)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var controller = root.GetComponentInChildren<BattleSceneController>(true);
                if (controller == null) continue;

                var so = new SerializedObject(controller);
                so.FindProperty("exitX").floatValue = exitX;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
                return;
            }

            Debug.LogWarning("[ArenaSceneBuilder] BattleSceneController가 없다. 출구 선을 못 옮겼다.");
        }

        // ── 공용 ────────────────────────────────────────

        /// <summary>
        /// 이 스테이지의 라운드가 실제로 쓰는 역할이 전부 꽂혀 있는지 <b>읽어서</b> 확인한다.
        ///
        /// 표(<see cref="ArenaRoundCatalog"/>)와 배선을 여기서 한 번 맞물려 둔다 —
        /// 라운드에 새 역할을 추가하고 스포너에 데이터 꽂는 걸 잊는 실수가 이 한 줄에 걸린다.
        /// </summary>
        private static bool VerifySpawner(EnemySpawnService spawner, int stageNumber)
        {
            bool ok = true;

            for (int arena = 1; arena <= ArenaRoundCatalog.ArenaCountOf(stageNumber); arena++)
            {
                ArenaRound round = ArenaRoundCatalog.For(stageNumber, arena);
                if (round.spawns == null) continue;

                foreach (RoundSpawn spawn in round.spawns)
                {
                    if (spawner.CanSpawn(spawn.role)) continue;

                    string field = DataFieldOf(spawn.role);

                    Debug.LogError(
                        $"[ArenaSceneBuilder] 스테이지 {stageNumber} {arena}R 이 {spawn.role} 를 쓰는데 " +
                        $"{field} 가 비어 있다. 애셋 경로를 확인할 것: {DataPathOf(spawn.role)}");
                    ok = false;
                }
            }

            return ok;
        }

        private static string DataFieldOf(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Charger: return "chargerData";
                case EnemyRole.Ranged:  return "rangedData";
                case EnemyRole.Boss:    return "bossData";
                default:                return "meleeData";
            }
        }

        private static string DataPathOf(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Charger: return ChargerDataPath;
                case EnemyRole.Ranged:  return RangedDataPath;
                case EnemyRole.Boss:    return BossDataPath;
                default:                return MeleeDataPath;
            }
        }

        /// <summary>웨이브 빌더와 같은 배선을 쓴다 — 두 벌이 되면 한쪽만 갱신된다.</summary>
        internal static void WireSpawner(EnemySpawnService spawner,
                                         EnemyData melee, EnemyData charger, EnemyData ranged,
                                         EnemyData boss = null)
        {
            spawner.Configure(melee, charger, ranged, boss);
            EditorUtility.SetDirty(spawner);
        }

        /// <summary>프리팹을 잠깐 세워 렌더러 경계로 실제 가로 폭을 잰다.</summary>
        private static float MeasureWidth(GameObject prefab)
        {
            GameObject probe = Object.Instantiate(prefab);
            try
            {
                var sr = probe.GetComponentInChildren<SpriteRenderer>();
                return sr != null ? sr.bounds.size.x : 0f;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        private static void MakeWall(GameObject room, string name, Vector3 center, Vector3 size)
        {
            Transform t = room.transform.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                t = go.transform;
                t.SetParent(room.transform, false);
            }

            t.localPosition = center;
            t.gameObject.layer = WallLayer;

            var box = t.GetComponent<BoxCollider>();
            if (box == null) box = t.gameObject.AddComponent<BoxCollider>();

            box.isTrigger = false;
            box.center = Vector3.zero;
            box.size = size;

            EditorUtility.SetDirty(t.gameObject);
        }

        private static void DestroyChild(GameObject parent, string name)
        {
            Transform t = parent.transform.Find(name);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        private static Sprite FindSquareSprite()
        {
            // 유니티 내장 흰 사각형. 전용 애셋을 만들지 않는다.
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            return sprite != null ? sprite : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        }

        private static Camera FindCamera(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var cam = root.GetComponentInChildren<Camera>(true);
                if (cam != null) return cam;
            }

            return null;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;

            return null;
        }

        private static List<Enemy> FindEnemies(Scene scene)
        {
            var found = new List<Enemy>();

            foreach (GameObject root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<Enemy>(true));

            return found;
        }

        private static EnemyData Load(string path) => AssetDatabase.LoadAssetAtPath<EnemyData>(path);

        private static Scene OpenCopy(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                AssetDatabase.DeleteAsset(path);

            if (!AssetDatabase.CopyAsset(SourceScene, path))
            {
                Debug.LogError($"[ArenaSceneBuilder] 씬 복제 실패: {SourceScene} → {path}");
                return default;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
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
            Debug.Log($"[ArenaSceneBuilder] Build Settings에 씬 {added}개 등록");
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
