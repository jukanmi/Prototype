using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 스테이지 맵을 <b>프리팹</b>으로 굽는다.
    ///
    /// 전에는 <see cref="SceneLayoutBuilder"/>가 방을 씬에 직접 만들고, 그 결과를
    /// SampleScene에 구운 뒤 <see cref="StageSceneBuilder"/>가 씬 파일을 통째로 복사해
    /// 스테이지로 퍼뜨렸다. 그러면 <b>모든 스테이지의 맵이 영원히 같다</b> —
    /// 스테이지마다 다른 맵을 넣을 자리가 구조적으로 없었다.
    ///
    /// 프리팹으로 빼면 씬은 "어느 맵을 쓰는가"만 들고, 3D 에셋으로 만든 맵을
    /// 스테이지마다 갈아 끼울 수 있다.
    ///
    /// 여기서 굽는 <c>Room_Default</c>는 3D 에셋이 들어오기 전까지 쓰는 <b>회색 상자 맵</b>이다.
    /// 실제 맵은 이 구조(<c>Geometry</c> + <c>Walls</c> + <see cref="RoomBounds"/>)를 지키는
    /// 프리팹을 손으로 만들어 넣으면 된다.
    ///
    /// 여러 번 돌려도 같은 결과가 나온다.
    /// </summary>
    public static class RoomPrefabBuilder
    {
        internal const string RoomFolder = "Assets/Prefabs/Rooms";
        internal const string DefaultRoomPath = RoomFolder + "/Room_Default.prefab";

        private const string MaterialDir = "Assets/Prefabs/Materials";

        /// <summary>맵 프리팹이 반드시 갖춰야 하는 자식 이름. 테스트와 빌더가 함께 본다.</summary>
        internal const string GeometryChild = "Geometry";
        internal const string WallsChild = "Walls";

        internal static readonly string[] WallNames = { "Wall_Left", "Wall_Right", "Wall_Back", "Wall_Front" };

        private const float WallHeight = 4f;
        private const float WallThickness = 0.5f;

        /// <summary>오른쪽 벽에서 출구 문턱까지 비워 두는 여유. 몸통 반지름 때문에 벽에 딱 붙지 못한다.</summary>
        private const float ExitInset = 1f;

        [MenuItem("Prototype/맵 - 기본 방 프리팹 만들기")]
        public static void BuildDefault()
        {
            GameObject prefab = Build(DefaultRoomPath, halfX: 6f, halfZ: 3f);
            if (prefab == null) return;

            Debug.Log($"[RoomPrefabBuilder] 완료 — {DefaultRoomPath}");
            Selection.activeObject = prefab;
        }

        /// <summary>
        /// 회색 상자 맵 하나. <paramref name="halfX"/> · <paramref name="halfZ"/>는
        /// 플레이 가능 범위의 반지름이다(벽 <b>안쪽</b> 면 기준).
        /// </summary>
        internal static GameObject Build(string path, float halfX, float halfZ)
        {
            SceneLayoutBuilder.EnsureFolder(RoomFolder);
            SceneLayoutBuilder.EnsureFolder(MaterialDir);

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));

            try
            {
                var bounds = root.AddComponent<RoomBounds>();

                var so = new SerializedObject(bounds);
                so.FindProperty("minX").floatValue = -halfX;
                so.FindProperty("maxX").floatValue =  halfX;
                so.FindProperty("minZ").floatValue = -halfZ;
                so.FindProperty("maxZ").floatValue =  halfZ;
                so.FindProperty("exitX").floatValue = halfX - ExitInset;
                so.ApplyModifiedPropertiesWithoutUndo();

                BuildGeometry(root, halfX, halfZ);
                BuildWalls(root, halfX, halfZ);

                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 보이는 부분. <b>콜라이더를 붙이지 않는다</b> — 높이(Y)는 <c>Physics</c>가 코드로
        /// 계산하므로 유니티 물리가 바닥을 밀어내면 서로 싸운다.
        /// </summary>
        private static void BuildGeometry(GameObject root, float halfX, float halfZ)
        {
            var geometry = new GameObject(GeometryChild);
            geometry.transform.SetParent(root.transform, false);

            Material floorMat = EnsureMaterial("M_Floor", new Color(0.22f, 0.22f, 0.26f));
            Material wallMat  = EnsureMaterial("M_Wall",  new Color(0.35f, 0.32f, 0.30f));

            // 바닥은 살짝 아래로 내려 윗면이 정확히 y = 0에 오게 한다.
            // 캐릭터 발과 그림자가 놓이는 평면이 곧 y = 0이다.
            const float floorThickness = 0.2f;
            MakeBox(geometry, "Floor",
                    new Vector3(0f, -floorThickness * 0.5f, 0f),
                    new Vector3(halfX * 2f, floorThickness, halfZ * 2f),
                    floorMat);

            // 뒷벽이 있어야 점프 높이를 잴 기준면이 생긴다. 없으면 "위로 갔다"로만 보인다.
            MakeBox(geometry, "BackWall",
                    new Vector3(0f, WallHeight * 0.5f, halfZ + WallThickness * 0.5f),
                    new Vector3(halfX * 2f + WallThickness * 2f, WallHeight, WallThickness),
                    wallMat);
        }

        /// <summary>
        /// 실제로 캐릭터를 막는 콜라이더. <c>Wall</c> 레이어이고 트리거가 아니다 —
        /// <c>Physics.OnCollisionEnter</c>가 받아야 <see cref="CombatState.WallBound"/>가 난다.
        /// </summary>
        private static void BuildWalls(GameObject root, float halfX, float halfZ)
        {
            var walls = new GameObject(WallsChild);
            walls.transform.SetParent(root.transform, false);

            float y = WallHeight * 0.5f;
            float outX = halfX + WallThickness * 0.5f;
            float outZ = halfZ + WallThickness * 0.5f;
            float spanX = halfX * 2f + WallThickness * 2f;
            float spanZ = halfZ * 2f;

            MakeWall(walls, "Wall_Left",  new Vector3(-outX, y, 0f), new Vector3(WallThickness, WallHeight, spanZ));
            MakeWall(walls, "Wall_Right", new Vector3( outX, y, 0f), new Vector3(WallThickness, WallHeight, spanZ));
            MakeWall(walls, "Wall_Back",  new Vector3(0f, y,  outZ), new Vector3(spanX, WallHeight, WallThickness));
            MakeWall(walls, "Wall_Front", new Vector3(0f, y, -outZ), new Vector3(spanX, WallHeight, WallThickness));
        }

        private static void MakeWall(GameObject parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = center;
            go.layer = SceneLayoutBuilder.WallLayer;

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = Vector3.zero;
            box.size = size;
        }

        private static void MakeBox(GameObject parent, string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;

            Object.DestroyImmediate(go.GetComponent<Collider>());

            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return null;

            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
