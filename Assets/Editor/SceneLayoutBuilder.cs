using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 2D 템플릿 그대로 남아 있던 씬을 XZ 벨트스크롤 배치로 바꾼다(3D전환_TODO.md §4).
    ///
    /// 카메라는 <b>기울이지 않는다</b>. 깊이 → 화면 세로 환산은 <see cref="BeltScrollView"/>가
    /// depthToScreen으로 이미 처리하므로, 카메라까지 기울이면 깊이가 두 번 적용된다.
    ///
    /// 여러 번 돌려도 같은 결과가 나온다.
    /// </summary>
    public static class SceneLayoutBuilder
    {
        private const string SpriteChild = "Sprite";
        private const string ViewChild = "View";
        private const string ShadowChild = "Shadow";
        private const string RoomName = "Room";
        private const string PrefabFolder = "Assets/Prefabs";

        // 3D전환_TODO.md §2 — 레이어 번호는 문서와 맞춘다.
        private const int WallLayer = 9;
        private const int AllyHurtLayer = 10;
        private const int EnemyHurtLayer = 11;
        private const int AllyHitLayer = 12;
        private const int EnemyHitLayer = 13;

        private static readonly (int index, string name)[] Layers =
        {
            (WallLayer,      "Wall"),
            (AllyHurtLayer,  "AllyHurtbox"),
            (EnemyHurtLayer, "EnemyHurtbox"),
            (AllyHitLayer,   "AllyHitbox"),
            (EnemyHitLayer,  "EnemyHitbox"),
        };

        /// <summary>방 크기(중심 기준 반경). 카메라 한 화면에 들어오는 값.</summary>
        private const float RoomHalfX = 6f;
        private const float RoomHalfZ = 3f;
        private const float WallHeight = 4f;
        private const float WallThickness = 0.5f;

        /// <summary>뒷벽을 그리는 높이. 위가 조금 잘리는 편이 방이 위로 이어져 보인다.</summary>
        private const float WallVisualHeight = 4f;

        /// <summary>깊이를 화면 세로로 접는 비율. 아이작 쪽으로 강하게.</summary>
        private const float DepthToScreen = 0.9f;

        /// <summary>깊이를 화면 가로로 미는 비율. 바닥이 평행사변형으로 기운다.</summary>
        private const float DepthToScreenX = 0.45f;

        /// <summary>깊이 1당 줄어드는 표시 배율. 방 깊이 ±3에서 앞뒤 1.44배.</summary>
        private const float DepthScalePerUnit = 0.06f;

        /// <summary>XZ 평면 배치. Y는 전부 0 — 높이는 점프로만 생긴다.</summary>
        private static readonly Dictionary<string, Vector3> Layout = new Dictionary<string, Vector3>
        {
            { "Player", new Vector3(-3.0f, 0f,  0.0f) },
            { "Tan",    new Vector3(-4.5f, 0f,  1.0f) },
            { "War",    new Vector3(-4.5f, 0f, -1.0f) },
            { "Arc",    new Vector3(-5.5f, 0f,  2.0f) },
            { "Wiz",    new Vector3(-5.5f, 0f, -2.0f) },
        };

        [MenuItem("Prototype/씬 벨트스크롤 배치로 정리")]
        public static void BuildLayout()
        {
            int rigged = 0;
            enemyIndex = 0;

            // 프리팹 원본이 먼저다. 씬 인스턴스에서는 프리팹이 소유한 자식을 다른 부모로
            // 못 옮긴다 — 유니티가 "Cannot restructure Prefab instance"로 막는다.
            int prefabs = RigPrefabAssets();

            foreach (Entity entity in Object.FindObjectsByType<Entity>(FindObjectsInactive.Include))
            {
                Reposition(entity.gameObject);
                if (RigBeltScrollView(entity.gameObject)) rigged++;
                RigAttackBox(entity);
            }

            EnsureWallLayer();

            // 히트박스가 만들어진 뒤에 레이어를 붙여야 한다.
            foreach (Entity entity in Object.FindObjectsByType<Entity>(FindObjectsInactive.Include))
                AssignLayers(entity);

            BuildRoom();
            SetupCamera();
            EnsureDebugHud();

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[SceneLayoutBuilder] 완료 — 프리팹 {prefabs}개, 씬 BeltScrollView 배선 {rigged}개, 카메라 정리 1개");
        }

        /// <summary>
        /// 프리팹 원본의 BeltScrollView 배선. 씬보다 먼저 돌아야 한다.
        ///
        /// 인스턴스에서는 프리팹이 소유한 <c>Sprite</c>를 새 <c>View</c> 아래로 못 옮긴다.
        /// 원본을 고쳐 두면 인스턴스가 View 노드를 물려받으므로 씬 쪽은 값만 덮어쓰면 된다.
        /// </summary>
        private static int RigPrefabAssets()
        {
            int rigged = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    // BeltScrollView로 그리는 캐릭터만 대상이다. 투사체는 자기 방식으로 그린다.
                    if (contents.GetComponent<Entity>() == null) continue;

                    RigBeltScrollView(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    rigged++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return rigged;
        }

        /// <summary>적은 방 오른쪽에 벌려 놓는다. 벽에 밀어붙일 여유를 남긴다.</summary>
        private static readonly Vector3[] EnemyFormation =
        {
            new Vector3(2.0f, 0f,  0.0f),
            new Vector3(3.5f, 0f,  1.6f),
            new Vector3(3.5f, 0f, -1.6f),
            new Vector3(5.0f, 0f,  0.0f),
            new Vector3(4.5f, 0f,  2.4f),
            new Vector3(4.5f, 0f, -2.4f),
        };

        private static int enemyIndex;

        private static void Reposition(GameObject go)
        {
            var entity = go.GetComponent<Entity>();

            // 적은 이름과 무관하게 대형으로 벌린다. 복제본이 한 점에 겹치는 걸 막는다.
            if (entity != null && entity.Faction == Faction.Enemy)
            {
                Undo.RecordObject(go.transform, "layout");
                go.transform.position = EnemyFormation[enemyIndex % EnemyFormation.Length];
                enemyIndex++;
                EditorUtility.SetDirty(go);
                return;
            }

            if (!Layout.TryGetValue(go.name, out Vector3 pos)) return;

            Undo.RecordObject(go.transform, "layout");
            go.transform.position = pos;
            EditorUtility.SetDirty(go);
        }

        /// <summary>
        /// 루트에 붙어 있던 SpriteRenderer를 자식 Sprite로 옮기고, 그 Sprite를 다시
        /// 깊이 배율 전용 노드 View 아래로 넣은 뒤 Shadow와 함께 BeltScrollView에 물린다.
        /// 루트는 논리 좌표만 유지한다.
        ///
        /// View를 한 겹 끼우는 이유: 애니 클립이 Sprite의 localScale을 쓰므로
        /// 깊이 배율을 같은 트랜스폼에 얹으면 매 프레임 서로 덮어쓴다.
        ///
        /// 테스트가 씬을 건드리지 않고 오브젝트 하나로 부를 수 있게 public이다.
        ///
        /// Undo를 안 쓴다 — <see cref="RigPrefabAssets"/>가 프리팹 프리뷰 씬 안에서도 이걸 부르는데
        /// 거기서는 Undo 등록이 오작동한다. 어차피 이 빌더는 마지막에 씬을 저장한다.
        /// </summary>
        public static bool RigBeltScrollView(GameObject root)
        {
            // 이미 옮겨 놓은 씬을 다시 돌릴 수 있어야 한다. 두 자리 다 본다.
            Transform sprite = root.transform.Find(ViewChild + "/" + SpriteChild);
            if (sprite == null) sprite = root.transform.Find(SpriteChild);

            SpriteRenderer rootRenderer = root.GetComponent<SpriteRenderer>();

            if (sprite == null)
            {
                var spriteGo = new GameObject(SpriteChild);
                sprite = spriteGo.transform;
                sprite.SetParent(root.transform, false);

                var sr = spriteGo.AddComponent<SpriteRenderer>();
                if (rootRenderer != null)
                {
                    sr.sprite = rootRenderer.sprite;
                    sr.color = rootRenderer.color;
                    sr.sharedMaterial = rootRenderer.sharedMaterial;
                }
            }

            // 루트 렌더러는 이제 필요 없다. 두면 자식과 겹쳐 그려진다.
            if (rootRenderer != null)
                Object.DestroyImmediate(rootRenderer);

            Transform depthRoot = EnsureDepthRoot(root, sprite);

            Transform shadow = root.transform.Find(ShadowChild);
            if (shadow == null)
            {
                var shadowGo = new GameObject(ShadowChild);
                shadow = shadowGo.transform;
                shadow.SetParent(root.transform, false);

                var sr = shadowGo.AddComponent<SpriteRenderer>();
                sr.sprite = FindCircleSprite();
                sr.color = new Color(0f, 0f, 0f, 0.35f);

                var spriteSr = sprite.GetComponent<SpriteRenderer>();
                if (spriteSr != null) sr.sharedMaterial = spriteSr.sharedMaterial;
            }

            var view = root.GetComponent<BeltScrollView>();
            if (view == null) view = root.AddComponent<BeltScrollView>();

            var so = new SerializedObject(view);
            so.FindProperty("sprite").objectReferenceValue = sprite;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.FindProperty("depthRoot").objectReferenceValue = depthRoot;
            // 모든 인스턴스가 같은 값이어야 한다 — BeltScroll의 static이 한 벌이다.
            so.FindProperty("depthToScreen").floatValue = DepthToScreen;
            so.FindProperty("depthToScreenX").floatValue = DepthToScreenX;
            so.FindProperty("depthScalePerUnit").floatValue = DepthScalePerUnit;

            // 그림자가 먼저(뒤에), 스프라이트가 나중(앞에) 그려져야 한다.
            SerializedProperty sorted = so.FindProperty("sortedRenderers");
            sorted.ClearArray();
            sorted.InsertArrayElementAtIndex(0);
            sorted.GetArrayElementAtIndex(0).objectReferenceValue = shadow.GetComponent<SpriteRenderer>();
            sorted.InsertArrayElementAtIndex(1);
            sorted.GetArrayElementAtIndex(1).objectReferenceValue = sprite.GetComponent<SpriteRenderer>();

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);

            // 히트박스는 루트를 따라 돌아야 하므로 Attack 자식은 건드리지 않는다.
            return true;
        }

        /// <summary>
        /// 깊이 배율 전용 노드. 있으면 그대로 쓰고, Sprite가 이미 그 아래면 아무것도 하지 않는다 —
        /// 빌더는 여러 번 돌린다.
        /// </summary>
        private static Transform EnsureDepthRoot(GameObject root, Transform sprite)
        {
            Transform view = root.transform.Find(ViewChild);
            if (view == null)
            {
                var go = new GameObject(ViewChild);
                view = go.transform;
                view.SetParent(root.transform, false);
            }

            view.localPosition = Vector3.zero;
            view.localRotation = Quaternion.identity;
            view.localScale = Vector3.one;   // 런타임에 BeltScrollView가 매 프레임 덮어쓴다

            if (sprite.parent != view)
            {
                // 프리팹이 소유한 자식은 인스턴스에서 못 옮긴다. 원본을 먼저 고쳐야 한다.
                // 여기서 조용히 넘어가면 깊이 배율이 안 걸린 채로 씬이 저장된다.
                if (PrefabUtility.IsPartOfPrefabInstance(sprite))
                {
                    Debug.LogError(
                        $"[SceneLayoutBuilder] {root.name}/{sprite.name}은 프리팹 인스턴스라 " +
                        "View 아래로 못 옮긴다. 프리팹 원본을 먼저 손봐야 한다(RigPrefabAssets).", root);
                    return view;
                }

                sprite.SetParent(view, false);
            }

            // 위치는 View가 잡는다. Sprite는 부모에 붙어만 있으면 된다.
            sprite.localPosition = Vector3.zero;
            sprite.localRotation = Quaternion.identity;

            return view;
        }

        /// <summary>레이어 등록. 없으면 충돌 매트릭스로 조합을 골라낼 수 없다.</summary>
        private static void EnsureWallLayer()
        {
            Object asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var so = new SerializedObject(asset);
            SerializedProperty layers = so.FindProperty("layers");

            int added = 0;
            foreach ((int index, string name) in Layers)
            {
                if (LayerMask.LayerToName(index) == name) continue;
                layers.GetArrayElementAtIndex(index).stringValue = name;
                added++;
            }

            if (added > 0)
            {
                so.ApplyModifiedProperties();
                Debug.Log($"[SceneLayoutBuilder] 레이어 {added}개 등록");
            }

            ConfigureCollisionMatrix();
        }

        /// <summary>
        /// 충돌 매트릭스. 캐릭터끼리는 <b>안 부딪히게</b> 해서 같은 자리에 겹칠 수 있게 한다.
        /// 위치는 코드가 정하지 유니티 물리가 밀어내면 안 된다(3D전환_TODO.md §3).
        ///
        /// 몸통을 트리거로 바꾸는 방법은 못 쓴다 — 벽 판정이 OnCollisionEnter라
        /// 트리거가 되면 월바운드가 통째로 죽는다.
        /// </summary>
        private static void ConfigureCollisionMatrix()
        {
            int[] hurt = { AllyHurtLayer, EnemyHurtLayer };
            int[] hit = { AllyHitLayer, EnemyHitLayer };

            // 1) 몸통끼리 전부 끈다 — 아군·적 구분 없이 겹칠 수 있다.
            foreach (int a in hurt)
                foreach (int b in hurt)
                    UnityEngine.Physics.IgnoreLayerCollision(a, b, true);

            // 2) 히트박스는 서로, 그리고 벽과 부딪힐 필요가 없다.
            foreach (int a in hit)
            {
                foreach (int b in hit) UnityEngine.Physics.IgnoreLayerCollision(a, b, true);
                UnityEngine.Physics.IgnoreLayerCollision(a, WallLayer, true);
            }

            // 3) 히트박스 ↔ 상대 몸통만 남긴다. 같은 진영은 끈다.
            UnityEngine.Physics.IgnoreLayerCollision(AllyHitLayer, EnemyHurtLayer, false);
            UnityEngine.Physics.IgnoreLayerCollision(EnemyHitLayer, AllyHurtLayer, false);
            UnityEngine.Physics.IgnoreLayerCollision(AllyHitLayer, AllyHurtLayer, true);
            UnityEngine.Physics.IgnoreLayerCollision(EnemyHitLayer, EnemyHurtLayer, true);

            // 4) 몸통 ↔ 벽은 반드시 켜져 있어야 한다. 월바운드가 여기서 나온다.
            foreach (int a in hurt)
                UnityEngine.Physics.IgnoreLayerCollision(a, WallLayer, false);

            AssetDatabase.SaveAssets();
            Debug.Log("[SceneLayoutBuilder] 충돌 매트릭스 설정 — 캐릭터끼리 통과, 몸통↔벽 유지");
        }

        /// <summary>진영에 맞는 레이어를 몸통과 히트박스에 나눠 붙인다.</summary>
        private static void AssignLayers(Entity entity)
        {
            bool ally = entity.Faction == Faction.Ally;

            Undo.RecordObject(entity.gameObject, "layer");
            entity.gameObject.layer = ally ? AllyHurtLayer : EnemyHurtLayer;

            Attack atk = entity.BasicAttack;
            if (atk != null)
            {
                Undo.RecordObject(atk.gameObject, "layer");
                atk.gameObject.layer = ally ? AllyHitLayer : EnemyHitLayer;
                EditorUtility.SetDirty(atk.gameObject);
            }

            EditorUtility.SetDirty(entity.gameObject);
        }

        /// <summary>
        /// 방 경계. 4면을 콜라이더로 두르고, 화면에는 접힌 위치에 판때기를 깐다.
        /// 밀치기(Push)가 벽에 닿아야 <see cref="CombatState.WallBound"/>가 나온다.
        /// </summary>
        private static void BuildRoom()
        {
            GameObject room = GameObject.Find(RoomName);
            if (room == null)
            {
                room = new GameObject(RoomName);
                Undo.RegisterCreatedObjectUndo(room, "room");
            }
            room.transform.position = Vector3.zero;

            // (이름, 콜라이더 중심, 콜라이더 크기)
            MakeWall(room, "Wall_Back",  new Vector3(0f, WallHeight * 0.5f,  RoomHalfZ + WallThickness * 0.5f), new Vector3(RoomHalfX * 2f + WallThickness * 2f, WallHeight, WallThickness));
            MakeWall(room, "Wall_Front", new Vector3(0f, WallHeight * 0.5f, -RoomHalfZ - WallThickness * 0.5f), new Vector3(RoomHalfX * 2f + WallThickness * 2f, WallHeight, WallThickness));
            MakeWall(room, "Wall_Right", new Vector3( RoomHalfX + WallThickness * 0.5f, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, RoomHalfZ * 2f));
            MakeWall(room, "Wall_Left",  new Vector3(-RoomHalfX - WallThickness * 0.5f, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, RoomHalfZ * 2f));

            BuildRoomVisual(room);

            // 모든 캐릭터가 Wall 레이어만 벽으로 인식하게 한다.
            // 기본값 ~0이면 캐릭터끼리 부딪혀도 벽 접촉으로 오인한다.
            foreach (Physics p in Object.FindObjectsByType<Physics>(FindObjectsInactive.Include))
            {
                var pso = new SerializedObject(p);
                pso.FindProperty("wallMask").intValue = 1 << WallLayer;
                pso.ApplyModifiedProperties();
                EditorUtility.SetDirty(p);
            }
        }

        private static void MakeWall(GameObject room, string name, Vector3 center, Vector3 size)
        {
            Transform t = room.transform.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "wall");
                t = go.transform;
                t.SetParent(room.transform, false);
            }

            t.localPosition = center;
            t.gameObject.layer = WallLayer;

            var box = t.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(t.gameObject);

            box.isTrigger = false;   // OnCollisionEnter로 받아야 한다
            box.center = Vector3.zero;
            box.size = size;

            EditorUtility.SetDirty(t.gameObject);
        }

        /// <summary>
        /// 방 배경. 바닥 판 하나로는 점프한 높이가 "화면에서 위로 갔다"로만 보인다.
        /// 뒷벽과 경계선을 세워 <b>높이를 잴 기준면</b>을 만든다.
        ///
        /// 깊이가 화면 가로로도 접히므로(<see cref="DepthToScreenX"/>) 논리 좌표의 사각형이
        /// 화면에서 <b>평행사변형</b>이 된다. 바닥은 그래서 4점 메시다 — Transform은 기울일 수 없다.
        /// 뒷벽과 경계선은 z가 한 값(RoomHalfZ)으로 고정이라 통째로 옆으로 밀린 직사각형이다.
        ///
        /// 폭은 안 좁힌다. 판정이 모든 z에서 x ∈ [-RoomHalfX, RoomHalfX]인 직사각형이라
        /// 그림만 좁히면 뒤쪽에서 캐릭터가 바닥 밖으로 걸어 나간 것처럼 보인다.
        ///
        /// 테스트가 씬을 건드리지 않고 임시 오브젝트로 부를 수 있게 public이다.
        /// </summary>
        public static void BuildRoomVisual(GameObject room)
        {
            // 방 뒷변이 화면에서 놓이는 자리. 벽과 경계선이 전부 여기 맞물린다.
            float backY = RoomHalfZ * DepthToScreen;
            float backX = RoomHalfZ * DepthToScreenX;
            float width = RoomHalfX * 2f;

            // 캐릭터 정렬은 -z*100이라 최저 z(-3)에서도 -300이다. 배경은 전부 그보다 뒤로 보낸다.
            SpriteRenderer wall = MakePanel(room, "BackWall",
                      new Vector3(backX, backY + WallVisualHeight * 0.5f, 0f),
                      new Vector3(width, WallVisualHeight, 1f),
                      new Color(0.10f, 0.11f, 0.14f, 1f), -10001);

            // 바닥과 벽이 꺾이는 선. 점프 높이가 이 선 대비로 읽힌다.
            MakePanel(room, "Horizon",
                      new Vector3(backX, backY, 0f),
                      new Vector3(width, 0.06f, 1f),
                      new Color(0.32f, 0.34f, 0.40f, 1f), -9999);

            MakeFloor(room, new Color(0.16f, 0.17f, 0.20f, 1f), -10000,
                      wall != null ? wall.sharedMaterial : null);
        }

        /// <summary>
        /// 평행사변형 바닥. 논리 좌표의 네 모서리를 그대로 투영해 꼭짓점으로 쓴다 —
        /// 캐릭터가 밟는 자리와 그림이 정의상 일치한다.
        ///
        /// 메시는 에셋으로 저장한다. 씬에만 들고 있으면 저장·재시작에서 참조가 끊긴다.
        /// </summary>
        private static void MakeFloor(GameObject room, Color color, int order, Material material)
        {
            Transform t = room.transform.Find("Floor");
            if (t == null)
            {
                var go = new GameObject("Floor");
                t = go.transform;
                t.SetParent(room.transform, false);
            }

            // 예전엔 사각형 스프라이트였다. 남겨 두면 평행사변형 위에 겹쳐 그려진다.
            var legacy = t.GetComponent<SpriteRenderer>();
            if (legacy != null) Object.DestroyImmediate(legacy);

            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;   // 크기는 메시 꼭짓점이 들고 있다

            var mf = t.GetComponent<MeshFilter>();
            if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>();

            var mr = t.GetComponent<MeshRenderer>();
            if (mr == null) mr = t.gameObject.AddComponent<MeshRenderer>();

            mf.sharedMesh = SaveFloorMesh(color);

            // 스프라이트 머티리얼을 그대로 쓴다 — 정점색을 곱해 주므로 색이 그대로 나오고,
            // 2D 렌더러가 같은 패스로 그린다. 비워 두면 URP 기본 머티리얼이 붙어 분홍이 된다.
            if (material == null)
                material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (material != null) mr.sharedMaterial = material;

            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            EditorUtility.SetDirty(t.gameObject);
        }

        private const string FloorMeshPath = "Assets/Data/Mesh/RoomFloor.asset";

        private static Mesh SaveFloorMesh(Color color)
        {
            // 논리 (x, z) → 화면 (x + z·kx, z·ky). 캐릭터가 거치는 변환과 같은 식이다.
            Vector3 Corner(float x, float z)
                => new Vector3(x + z * DepthToScreenX, z * DepthToScreen, 0f);

            var vertices = new[]
            {
                Corner(-RoomHalfX, -RoomHalfZ),   // 0 앞왼
                Corner( RoomHalfX, -RoomHalfZ),   // 1 앞오
                Corner( RoomHalfX,  RoomHalfZ),   // 2 뒤오
                Corner(-RoomHalfX,  RoomHalfZ),   // 3 뒤왼
            };

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(FloorMeshPath);
            bool created = mesh == null;
            if (created) mesh = new Mesh();

            mesh.name = "RoomFloor";
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            // 색은 정점에 싣는다. 스프라이트 머티리얼이 _MainTex(흰색)에 정점색을 곱하므로
            // 머티리얼을 인스턴스화하지 않고도 원하는 색이 나온다.
            mesh.colors = new[] { color, color, color, color };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (created)
            {
                EnsureFolder("Assets/Data/Mesh");
                AssetDatabase.CreateAsset(mesh, FloorMeshPath);
            }

            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int cut = path.LastIndexOf('/');
            string parent = path.Substring(0, cut);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(cut + 1));
        }

        private static SpriteRenderer MakePanel(GameObject room, string name, Vector3 localPos,
                                                Vector3 scale, Color color, int order)
        {
            Transform t = room.transform.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "room panel");
                t = go.transform;
                t.SetParent(room.transform, false);
            }

            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) sr = Undo.AddComponent<SpriteRenderer>(t.gameObject);

            sr.sprite = FindSprite("Square");
            sr.color = color;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sortingOrder = order;

            t.localPosition = localPos;
            t.localRotation = Quaternion.identity;
            t.localScale = scale;

            EditorUtility.SetDirty(t.gameObject);
            return sr;
        }

        /// <summary>
        /// 히트박스 배선. Attack 자식은 있는데 <see cref="Attack"/> 컴포넌트가 없고
        /// Entity.basicAttack도 비어 있어서 데미지가 아예 안 들어가고 있었다.
        /// </summary>
        private static void RigAttackBox(Entity entity)
        {
            Transform child = entity.transform.Find("Attack");
            if (child == null)
            {
                var go = new GameObject("Attack");
                Undo.RegisterCreatedObjectUndo(go, "attack box");
                child = go.transform;
                child.SetParent(entity.transform, false);
            }

            // Physics.Apply가 Quaternion.LookRotation(Facing)으로 루트를 돌린다.
            // 즉 정면은 로컬 +Z다. 기존 값은 +X라 히트박스가 늘 옆구리를 때리고 있었다.
            Undo.RecordObject(child, "attack offset");
            child.localPosition = new Vector3(0f, 0f, 1.0f);
            child.localRotation = Quaternion.identity;

            var col = child.GetComponent<Collider>();
            if (col == null)
            {
                var cap = Undo.AddComponent<CapsuleCollider>(child.gameObject);
                cap.radius = 0.6f;
                cap.height = 1f;
                col = cap;
            }
            col.isTrigger = true;

            var atk = child.GetComponent<Attack>();
            if (atk == null) atk = Undo.AddComponent<Attack>(child.gameObject);
            atk.Attacker = entity.Combat != null ? entity.Combat : entity.GetComponent<Combat>();

            var so = new SerializedObject(entity);
            so.FindProperty("basicAttack").objectReferenceValue = atk;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(atk);
            EditorUtility.SetDirty(entity);
        }

        private static void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Undo.RecordObject(cam.transform, "camera");
            Undo.RecordObject(cam, "camera");

            cam.orthographic = true;
            cam.orthographicSize = 5f;
            // 기울이지 않는다 — 깊이는 BeltScrollView가 담당한다.
            cam.transform.rotation = Quaternion.identity;
            // 아래 여백을 줄이고 뒷벽을 더 보여준다. size 5 기준 세로 -3.7 ~ 6.3 —
            // 바닥 아랫변(-2.7)과 벽 윗변(6.7) 사이가 화면에 거의 다 들어온다.
            cam.transform.position = new Vector3(0f, 1.3f, -10f);

            // 아이작 구도 — 방 하나가 한 화면. 카메라는 방 중심에 고정한다.
            // 방을 넘나드는 흐름이 붙으면 그때 다시 켠다.
            var follow = cam.GetComponent<CameraFollow>();
            if (follow != null) follow.enabled = false;

            EditorUtility.SetDirty(cam);
        }

        /// <summary>
        /// 임시 조작기 겸 HUD. 이게 없으면 손패가 화면에 안 그려지고
        /// 카드 집기 · 배치 입력도 아무도 처리하지 않는다.
        /// </summary>
        private static void EnsureDebugHud()
        {
            BulletTimeController btc = Object.FindAnyObjectByType<BulletTimeController>();
            if (btc == null)
            {
                Debug.LogError("[SceneLayoutBuilder] BulletTimeController가 씬에 없다. HUD를 붙일 곳이 없다.");
                return;
            }

            var hud = btc.GetComponent<DebugComboHUD>();
            if (hud == null) hud = Undo.AddComponent<DebugComboHUD>(btc.gameObject);

            if (btc.GetComponent<RecentHitEnemyHUD>() == null)
                Undo.AddComponent<RecentHitEnemyHUD>(btc.gameObject);

            var so = new SerializedObject(hud);
            so.FindProperty("bulletTime").objectReferenceValue = btc;
            so.FindProperty("targetSelector").objectReferenceValue = Object.FindAnyObjectByType<TargetSelector>();
            so.FindProperty("player").objectReferenceValue = Object.FindAnyObjectByType<Player>();
            so.FindProperty("show").boolValue = true;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(hud);
            Debug.Log("[SceneLayoutBuilder] DebugComboHUD 배선 완료", hud);
        }

        /// <summary>프로젝트에 이미 있는 기본 도형 스프라이트를 재사용한다.</summary>
        private static Sprite FindSprite(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite"))
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null) return s;
            }

            Debug.LogWarning($"[SceneLayoutBuilder] {name} 스프라이트를 못 찾았다.");
            return null;
        }

        private static Sprite FindCircleSprite() => FindSprite("Circle");

        /// <summary>3D전환_TODO.md §3 — 중력은 Physics 클래스가 직접 계산한다.</summary>
        [MenuItem("Prototype/프로젝트 중력 0으로 (3D전환 TODO §3)")]
        public static void ZeroGravity()
        {
            UnityEngine.Physics.gravity = Vector3.zero;
            Debug.Log("[SceneLayoutBuilder] Physics.gravity = 0. Project Settings > Physics 에도 저장됨.");
        }
    }
}
