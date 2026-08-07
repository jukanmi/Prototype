using System.Collections.Generic;
using NUnit.Framework;
using Prototype.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 빌더는 여러 번 돌린다. 두 번째 실행에서 노드가 겹치거나 Sprite가 제자리로
    /// 안 돌아오면 씬이 조용히 망가진다 — 컴파일로는 안 잡힌다.
    /// </summary>
    public class SceneLayoutRoomTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();

            // 빌더가 Undo에 기록을 남긴다. 다음 테스트로 새지 않게 지운다.
            Undo.ClearAll();
        }

        private GameObject NewRoot(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.AddComponent<Physics>();
            return go;
        }

        // ── View 노드 배선 ──────────────────────────────

        [Test]
        public void RigBeltScrollView_MovesSpriteUnderViewNode()
        {
            GameObject root = NewRoot("Rig");

            SceneLayoutBuilder.RigBeltScrollView(root);

            Assert.That(root.transform.Find("View"), Is.Not.Null, "View 노드가 없다");
            Assert.That(root.transform.Find("View/Sprite"), Is.Not.Null, "Sprite가 View 아래에 없다");
            Assert.That(root.transform.Find("Sprite"), Is.Null, "Sprite가 루트에 남아 있다");
        }

        [Test]
        public void RigBeltScrollView_WiresDepthRoot()
        {
            GameObject root = NewRoot("Rig");

            SceneLayoutBuilder.RigBeltScrollView(root);

            var so = new SerializedObject(root.GetComponent<BeltScrollView>());
            Assert.That(so.FindProperty("depthRoot").objectReferenceValue,
                        Is.SameAs(root.transform.Find("View")));
            Assert.That(so.FindProperty("depthScalePerUnit").floatValue,
                        Is.EqualTo(0.06f).Within(0.0001f));
            Assert.That(so.FindProperty("depthToScreenX").floatValue,
                        Is.EqualTo(0.45f).Within(0.0001f));
        }

        [Test]
        public void RigBeltScrollView_TwiceKeepsOneViewNode()
        {
            GameObject root = NewRoot("Rig");

            SceneLayoutBuilder.RigBeltScrollView(root);
            SceneLayoutBuilder.RigBeltScrollView(root);

            int views = 0;
            foreach (Transform child in root.transform)
                if (child.name == "View") views++;

            Assert.That(views, Is.EqualTo(1), "View 노드가 중복 생성됐다");
            Assert.That(root.transform.Find("View/Sprite"), Is.Not.Null);
        }

        /// <summary>이미 루트에 Sprite가 있던 예전 프리팹도 옮겨져야 한다.</summary>
        [Test]
        public void RigBeltScrollView_AdoptsExistingRootSprite()
        {
            GameObject root = NewRoot("Rig");
            var legacy = new GameObject("Sprite");
            legacy.transform.SetParent(root.transform, false);
            legacy.AddComponent<SpriteRenderer>();

            SceneLayoutBuilder.RigBeltScrollView(root);

            Assert.That(root.transform.Find("View/Sprite"), Is.SameAs(legacy.transform));
        }

        // ── 방 배경 ─────────────────────────────────────

        [Test]
        public void BuildRoomVisual_CreatesWallFloorAndHorizon()
        {
            var room = new GameObject("Room");
            spawned.Add(room);

            SceneLayoutBuilder.BuildRoomVisual(room);

            Transform wall = room.transform.Find("BackWall");
            Transform floor = room.transform.Find("Floor");
            Transform horizon = room.transform.Find("Horizon");

            Assert.That(wall, Is.Not.Null, "BackWall 없음");
            Assert.That(floor, Is.Not.Null, "Floor 없음");
            Assert.That(horizon, Is.Not.Null, "Horizon 없음");

            // 바닥은 평행사변형이라 MeshRenderer, 나머지는 SpriteRenderer다.
            int wallOrder = wall.GetComponent<Renderer>().sortingOrder;
            int floorOrder = floor.GetComponent<Renderer>().sortingOrder;
            int horizonOrder = horizon.GetComponent<Renderer>().sortingOrder;

            Assert.That(wallOrder, Is.LessThan(floorOrder), "벽이 바닥보다 뒤여야 한다");
            Assert.That(floorOrder, Is.LessThan(horizonOrder), "경계선이 바닥보다 앞이어야 한다");

            // 캐릭터는 최대 z=3에서도 -300이다. 배경 셋 다 그보다 뒤여야 한다.
            Assert.That(horizonOrder, Is.LessThan(-300));
        }

        /// <summary>
        /// 바닥이 평행사변형이어야 기울기가 보인다. 뒷변이 앞변보다 오른쪽으로 밀려 있어야 한다.
        /// </summary>
        [Test]
        public void BuildRoomVisual_FloorIsAParallelogram()
        {
            var room = new GameObject("Room");
            spawned.Add(room);

            SceneLayoutBuilder.BuildRoomVisual(room);

            Mesh mesh = room.transform.Find("Floor").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh, Is.Not.Null, "바닥 메시가 없다");

            Vector3[] v = mesh.vertices;
            Assert.That(v.Length, Is.EqualTo(4));

            // 0 앞왼, 1 앞오, 2 뒤오, 3 뒤왼 — DepthToScreenX 0.45, RoomHalfZ 3 → ±1.35
            Assert.That(v[0].x, Is.EqualTo(-6f - 1.35f).Within(0.001f));
            Assert.That(v[3].x, Is.EqualTo(-6f + 1.35f).Within(0.001f));
            Assert.That(v[3].x - v[0].x, Is.EqualTo(2f * 1.35f).Within(0.001f), "뒷변이 오른쪽으로 밀려야 한다");

            // 밀려도 폭은 그대로다 — 판정이 모든 z에서 같은 폭이라 좁히면 어긋난다.
            Assert.That(v[1].x - v[0].x, Is.EqualTo(12f).Within(0.001f), "앞변 폭");
            Assert.That(v[2].x - v[3].x, Is.EqualTo(12f).Within(0.001f), "뒷변 폭");
        }

        /// <summary>벽 아랫변과 바닥 뒷변이 어긋나면 그 틈으로 배경이 뚫려 보인다.</summary>
        [Test]
        public void BuildRoomVisual_WallSitsExactlyOnFloorBackEdge()
        {
            var room = new GameObject("Room");
            spawned.Add(room);

            SceneLayoutBuilder.BuildRoomVisual(room);

            Transform wall = room.transform.Find("BackWall");
            Transform horizon = room.transform.Find("Horizon");
            Mesh mesh = room.transform.Find("Floor").GetComponent<MeshFilter>().sharedMesh;

            // 뒤왼 · 뒤오 꼭짓점의 높이가 곧 바닥 뒷변이다.
            float floorBack = mesh.vertices[3].y;
            float wallBottom = wall.localPosition.y - wall.localScale.y * 0.5f;

            Assert.That(floorBack, Is.EqualTo(2.7f).Within(0.0001f), "바닥 뒷변 = RoomHalfZ × DepthToScreen");
            Assert.That(wallBottom, Is.EqualTo(floorBack).Within(0.0001f));
            Assert.That(horizon.localPosition.y, Is.EqualTo(floorBack).Within(0.0001f));

            // 벽과 경계선도 뒷변만큼 옆으로 밀려야 바닥 뒷변 위에 정확히 얹힌다.
            Assert.That(wall.localPosition.x, Is.EqualTo(1.35f).Within(0.001f));
            Assert.That(horizon.localPosition.x, Is.EqualTo(1.35f).Within(0.001f));
        }

        [Test]
        public void BuildRoomVisual_TwiceMakesNoDuplicates()
        {
            var room = new GameObject("Room");
            spawned.Add(room);

            SceneLayoutBuilder.BuildRoomVisual(room);
            SceneLayoutBuilder.BuildRoomVisual(room);

            Assert.That(room.transform.childCount, Is.EqualTo(3));
        }
    }
}
