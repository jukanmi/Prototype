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

            // 그림자는 바닥 평면에 눕는다 — 납작해 보이는 건 카메라 기울기가 만들므로
            // XY 모두 실제 지름이어야 한다. Y를 눌러 두면 이중으로 눌린다.
            Assert.That(so.FindProperty("shadowBaseScale").vector3Value.y,
                        Is.EqualTo(0.9f).Within(0.0001f));
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

            // 셋 다 SpriteRenderer다. 바닥은 눕힌 판이라 메시가 필요 없다.
            int wallOrder = wall.GetComponent<Renderer>().sortingOrder;
            int floorOrder = floor.GetComponent<Renderer>().sortingOrder;
            int horizonOrder = horizon.GetComponent<Renderer>().sortingOrder;

            Assert.That(wallOrder, Is.LessThan(floorOrder), "벽이 바닥보다 뒤여야 한다");
            Assert.That(floorOrder, Is.LessThan(horizonOrder), "경계선이 바닥보다 앞이어야 한다");

            // 캐릭터는 최대 z=3에서도 -300이다. 배경 셋 다 그보다 뒤여야 한다.
            Assert.That(horizonOrder, Is.LessThan(-300));
        }

        /// <summary>
        /// <b>이 테스트가 "발판이 발판 노릇을 한다"의 정의다.</b>
        ///
        /// 판정은 모든 z에서 x ∈ [-6, 6]인 직사각형이다. 바닥 판도 XZ 평면에 눕힌
        /// 같은 크기 사각형이어야 걷는 자리와 그림이 어긋나지 않는다.
        /// 예전엔 화면 가로 밀림 때문에 평행사변형 메시였고, 그래서 뒤쪽으로 걸어가면
        /// 바닥 그림 밖으로 걸어 나갔다.
        /// </summary>
        [Test]
        public void BuildRoomVisual_FloorLiesFlatOnTheWalkableRectangle()
        {
            var room = new GameObject("Room");
            spawned.Add(room);

            SceneLayoutBuilder.BuildRoomVisual(room);

            Transform floor = room.transform.Find("Floor");

            Assert.That(floor.GetComponent<MeshFilter>(), Is.Null, "평행사변형 메시 잔해가 남았다");
            Assert.That(Quaternion.Angle(floor.localRotation, BeltScrollView.LieOnGround),
                        Is.LessThan(0.01f), "바닥이 XZ 평면에 눕지 않았다");

            Assert.That(floor.localPosition, Is.EqualTo(Vector3.zero), "바닥은 방 중심에 있다");
            Assert.That(floor.localScale.x, Is.EqualTo(12f).Within(0.001f), "폭 = RoomHalfX × 2");
            Assert.That(floor.localScale.y, Is.EqualTo(6f).Within(0.001f), "깊이 = RoomHalfZ × 2");
        }

        /// <summary>벽 아랫변과 바닥 뒷변이 어긋나면 그 틈으로 배경이 뚫려 보인다.</summary>
        [Test]
        public void BuildRoomVisual_WallStandsOnTheFloorBackEdge()
        {
            var room = new GameObject("Room");
            spawned.Add(room);

            SceneLayoutBuilder.BuildRoomVisual(room);

            Transform wall = room.transform.Find("BackWall");
            Transform horizon = room.transform.Find("Horizon");

            // 뒷변은 z = RoomHalfZ, 바닥면은 y = 0이다. 둘 다 논리 좌표 그대로다.
            Assert.That(wall.localPosition.z, Is.EqualTo(3f).Within(0.0001f), "뒷벽은 z = RoomHalfZ에 선다");
            Assert.That(wall.localPosition.y - wall.localScale.y * 0.5f,
                        Is.EqualTo(0f).Within(0.0001f), "벽 아랫변이 바닥면에 닿아야 한다");
            Assert.That(horizon.localPosition.z, Is.EqualTo(3f).Within(0.0001f));

            // 밀림이 사라졌으므로 벽도 경계선도 옆으로 안 밀린다.
            Assert.That(wall.localPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(horizon.localPosition.x, Is.EqualTo(0f).Within(0.001f));
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
