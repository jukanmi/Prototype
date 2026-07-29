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
        private const string ShadowChild = "Shadow";
        private const string RoomName = "Room";
        private const string WallLayerName = "Wall";
        private const int WallLayer = 9;                 // 3D전환_TODO.md §2

        /// <summary>방 크기(중심 기준 반경). 카메라 한 화면에 들어오는 값.</summary>
        private const float RoomHalfX = 6f;
        private const float RoomHalfZ = 3f;
        private const float WallHeight = 4f;
        private const float WallThickness = 0.5f;

        /// <summary>깊이를 화면 세로로 접는 비율. 아이작 쪽으로 강하게.</summary>
        private const float DepthToScreen = 0.9f;

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

            foreach (Entity entity in Object.FindObjectsByType<Entity>(FindObjectsInactive.Include))
            {
                Reposition(entity.gameObject);
                if (RigBeltScrollView(entity.gameObject)) rigged++;
                RigAttackBox(entity);
            }

            EnsureWallLayer();
            BuildRoom();
            SetupCamera();
            EnsureDebugHud();

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[SceneLayoutBuilder] 완료 — BeltScrollView 배선 {rigged}개, 카메라 정리 1개");
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
        /// 루트에 붙어 있던 SpriteRenderer를 자식 Sprite로 옮기고 Shadow를 만든 뒤
        /// BeltScrollView에 물린다. 루트는 논리 좌표만 유지한다.
        /// </summary>
        private static bool RigBeltScrollView(GameObject root)
        {
            Transform sprite = root.transform.Find(SpriteChild);
            SpriteRenderer rootRenderer = root.GetComponent<SpriteRenderer>();

            if (sprite == null)
            {
                var spriteGo = new GameObject(SpriteChild);
                Undo.RegisterCreatedObjectUndo(spriteGo, "sprite child");
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
                Undo.DestroyObjectImmediate(rootRenderer);

            Transform shadow = root.transform.Find(ShadowChild);
            if (shadow == null)
            {
                var shadowGo = new GameObject(ShadowChild);
                Undo.RegisterCreatedObjectUndo(shadowGo, "shadow child");
                shadow = shadowGo.transform;
                shadow.SetParent(root.transform, false);

                var sr = shadowGo.AddComponent<SpriteRenderer>();
                sr.sprite = FindCircleSprite();
                sr.color = new Color(0f, 0f, 0f, 0.35f);

                var spriteSr = sprite.GetComponent<SpriteRenderer>();
                if (spriteSr != null) sr.sharedMaterial = spriteSr.sharedMaterial;
            }

            var view = root.GetComponent<BeltScrollView>();
            if (view == null) view = Undo.AddComponent<BeltScrollView>(root);

            var so = new SerializedObject(view);
            so.FindProperty("sprite").objectReferenceValue = sprite;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            // 모든 인스턴스가 같은 값이어야 한다 — BeltScroll.DepthToScreen이 한 벌이다.
            so.FindProperty("depthToScreen").floatValue = DepthToScreen;

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

        /// <summary>Wall 레이어를 만든다. 이게 없으면 wallMask로 벽만 골라낼 수 없다.</summary>
        private static void EnsureWallLayer()
        {
            if (LayerMask.LayerToName(WallLayer) == WallLayerName) return;

            Object asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var so = new SerializedObject(asset);
            SerializedProperty layers = so.FindProperty("layers");

            layers.GetArrayElementAtIndex(WallLayer).stringValue = WallLayerName;
            so.ApplyModifiedProperties();

            Debug.Log($"[SceneLayoutBuilder] 레이어 {WallLayer} = {WallLayerName} 등록");
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
        /// 방 바닥 표시. 논리 좌표계의 사각형은 화면에서 평행사변형이 되지만,
        /// 프로토타입에선 접힌 범위를 덮는 사각형 하나로 충분하다.
        /// </summary>
        private static void BuildRoomVisual(GameObject room)
        {
            Transform t = room.transform.Find("Floor");
            if (t == null)
            {
                var go = new GameObject("Floor");
                Undo.RegisterCreatedObjectUndo(go, "floor");
                t = go.transform;
                t.SetParent(room.transform, false);
            }

            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) sr = Undo.AddComponent<SpriteRenderer>(t.gameObject);

            sr.sprite = FindSprite("Square");
            sr.color = new Color(0.16f, 0.17f, 0.20f, 1f);
            sr.drawMode = SpriteDrawMode.Simple;
            // 캐릭터 정렬은 -z*100이라 최저 z(-3)에서도 -300이다. 그보다 더 뒤로 보낸다.
            sr.sortingOrder = -10000;

            t.localPosition = Vector3.zero;
            t.localScale = new Vector3(RoomHalfX * 2f, RoomHalfZ * 2f * DepthToScreen, 1f);

            EditorUtility.SetDirty(t.gameObject);
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
            cam.transform.position = new Vector3(0f, 0.8f, -10f);

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
