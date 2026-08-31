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

        /// <summary>
        /// 카메라 피치. 깊이를 화면 세로로 보여 주는 일을 <b>카메라가</b> 한다.
        /// X축 하나만 돌린다 — 요를 섞으면 깊이가 화면 가로로도 새어 대각선 이동이 된다.
        /// </summary>
        private const float TiltDegrees = BeltScroll.DefaultTiltDegrees;

        /// <summary>바닥 중심에서 물러나는 거리. 직교라 그림 크기와 무관하고 클리핑 여유만 정한다.</summary>
        private const float CameraDistance = 10f;

        /// <summary>화면 위로 프레임을 올리는 양. 지면이 화면 한가운데 오지 않게 한다.</summary>
        private const float CameraLift = 0.5f;

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
                if (IsInsideBattleInput(entity)) continue;

                Reposition(entity.gameObject);
                if (RigBeltScrollView(entity.gameObject)) rigged++;
                RigAttackBox(entity);
            }

            EnsureWallLayer();

            // 히트박스가 만들어진 뒤에 레이어를 붙여야 한다.
            foreach (Entity entity in Object.FindObjectsByType<Entity>(FindObjectsInactive.Include))
            {
                if (IsInsideBattleInput(entity)) continue;
                AssignLayers(entity);
            }

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
            so.FindProperty("depthScalePerUnit").floatValue = DepthScalePerUnit;
            // 그림자는 바닥 평면에 눕는다. 납작해 보이는 건 카메라 기울기가 만들므로
            // XY 모두 실제 지름을 준다 — 예전처럼 Y를 0.35로 눌러 두면 이중으로 눌린다.
            so.FindProperty("shadowBaseScale").vector3Value = new Vector3(0.9f, 0.9f, 1f);

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
        /// 셋 다 <b>논리 좌표 그대로</b> 놓는다. 카메라가 기울어 깊이를 보여 주므로
        /// 예전처럼 화면 좌표로 손수 접을 이유가 없다 —
        /// 바닥은 XZ 평면에 눕힌 판, 뒷벽은 z = +<see cref="RoomHalfZ"/>에 세운 판이다.
        ///
        /// 바닥이 평행사변형 메시였던 것도 그래서 없앴다. 그 기울기는 화면 가로 밀림
        /// 때문에 필요했던 것이고, 밀림이 사라진 지금은 판정 영역과 모양이 정확히 같다.
        ///
        /// 테스트가 씬을 건드리지 않고 임시 오브젝트로 부를 수 있게 public이다.
        /// </summary>
        public static void BuildRoomVisual(GameObject room)
        {
            float width = RoomHalfX * 2f;

            // 캐릭터 정렬은 -z*100이라 최저 z(-3)에서도 -300이다. 배경은 전부 그보다 뒤로 보낸다.
            MakePanel(room, "BackWall",
                      new Vector3(0f, WallVisualHeight * 0.5f, RoomHalfZ),
                      Quaternion.identity,
                      new Vector3(width, WallVisualHeight, 1f),
                      new Color(0.10f, 0.11f, 0.14f, 1f), -10001);

            MakeFloor(room, width, new Color(0.16f, 0.17f, 0.20f, 1f), -10000);

            // 바닥과 벽이 꺾이는 선. 점프 높이가 이 선 대비로 읽힌다.
            // 바닥 판과 같은 평면에 두면 깜빡이므로 아주 살짝 띄운다.
            MakePanel(room, "Horizon",
                      new Vector3(0f, 0.03f, RoomHalfZ),
                      Quaternion.identity,
                      new Vector3(width, 0.06f, 1f),
                      new Color(0.32f, 0.34f, 0.40f, 1f), -9999);
        }

        /// <summary>
        /// 바닥 판. XZ 평면에 눕힌 사각형이라 <b>캐릭터가 밟는 자리와 정의상 같다</b> —
        /// 판정이 모든 z에서 x ∈ [-RoomHalfX, RoomHalfX]인 직사각형이고, 이 판도 그렇다.
        /// </summary>
        private static void MakeFloor(GameObject room, float width, Color color, int order)
        {
            SpriteRenderer sr = MakePanel(room, "Floor",
                                          Vector3.zero,
                                          BeltScrollView.LieOnGround,
                                          new Vector3(width, RoomHalfZ * 2f, 1f),
                                          color, order);

            // 예전엔 평행사변형 메시였다. 남겨 두면 눕힌 판 위에 겹쳐 그려진다.
            var mf = sr.GetComponent<MeshFilter>();
            if (mf != null) Object.DestroyImmediate(mf);

            var mr = sr.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);

            // 밟히는 범위를 그림에서 그대로 얻는다(GroundPlate는 크기를 안 적으면 Renderer에서 잰다).
            // 그림과 판정을 따로 적으면 언젠가 한쪽만 어긋난다 — 그게 이 버그의 원인이었다.
            if (sr.GetComponent<GroundPlate>() == null)
                sr.gameObject.AddComponent<GroundPlate>();
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
            => MakePanel(room, name, localPos, Quaternion.identity, scale, color, order);

        private static SpriteRenderer MakePanel(GameObject room, string name, Vector3 localPos,
                                                Quaternion localRot, Vector3 scale, Color color, int order)
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
            t.localRotation = localRot;
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

            BasicAttackProfiles.SetHitbox(entity.gameObject, atk);

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

            // 깊이를 보여 주는 일은 카메라가 한다. X축 피치만 — 요·롤은 0이어야
            // right가 (1,0,0)으로 남아 screenX = x가 되고 깊이가 가로로 새지 않는다.
            Quaternion rot = Quaternion.Euler(TiltDegrees, 0f, 0f);
            cam.transform.rotation = rot;
            cam.transform.position = -(rot * Vector3.forward) * CameraDistance
                                   +  (rot * Vector3.up) * CameraLift;

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
        /// <summary>
        /// 이 몸이 <c>BattleInput</c> 프리팹 안에 있는가.
        ///
        /// <b>파티는 이 빌더가 손대면 안 된다.</b> 프리팹 인스턴스의 자식을 여기서 옮기거나
        /// 레이어를 칠하면 씬마다 오버라이드가 되살아난다 — 이 리팩터링이 없앤 바로 그것이다.
        /// 파티의 자리는 <see cref="PartySpawnPoint"/>가, 레이어는 런타임의
        /// <see cref="AllyLayers"/>가 맡는다.
        /// </summary>
        private static bool IsInsideBattleInput(Component c)
            => c != null && c.GetComponentInParent<PartyAssembler>(true) != null;

        private static void EnsureDebugHud()
        {
            BulletTimeController btc = Object.FindAnyObjectByType<BulletTimeController>();
            if (btc == null)
            {
                Debug.LogError("[SceneLayoutBuilder] BulletTimeController가 씬에 없다. HUD를 붙일 곳이 없다.");
                return;
            }

            // 덱 · HUD 가 BattleInput 안으로 들어갔다. 그쪽은 BattleInputBuilder 가 배선하므로
            // 여기서 또 만지면 프리팹 인스턴스에 씬 오버라이드가 생긴다.
            if (IsInsideBattleInput(btc))
            {
                Debug.Log("[SceneLayoutBuilder] HUD 는 BattleInput 프리팹 소유다 — 건너뛴다.");
                return;
            }

            var hud = btc.GetComponent<DebugComboHUD>();
            if (hud == null) hud = Undo.AddComponent<DebugComboHUD>(btc.gameObject);

            if (btc.GetComponent<RecentHitEnemyHUD>() == null)
                Undo.AddComponent<RecentHitEnemyHUD>(btc.gameObject);

            // 콤보 타수 · 누적 피해를 화면 오른쪽에 띄운다. 훈련장 콘솔 합계는 끝난 뒤에야
            // 보이는 숫자라, 굴리는 도중 어디서 끊겼는지는 이게 없으면 눈으로 못 잡는다.
            if (btc.GetComponent<ComboDamageHUD>() == null)
                Undo.AddComponent<ComboDamageHUD>(btc.gameObject);

            // 라운드가 끝나면 여기가 레벨업 화면을 연다. 화면은 스스로 짓지만
            // 얻은 카드를 지금 판의 덱에 들이려면 BulletTimeController를 알아야 한다.
            if (btc.GetComponent<LevelUpSession>() == null)
            {
                var session = Undo.AddComponent<LevelUpSession>(btc.gameObject);

                var sessionSo = new SerializedObject(session);
                sessionSo.FindProperty("bulletTime").objectReferenceValue = btc;
                sessionSo.ApplyModifiedProperties();
            }

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
