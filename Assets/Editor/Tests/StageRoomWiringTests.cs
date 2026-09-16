using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype.Tests
{
    /// <summary>
    /// 스테이지 씬의 <see cref="StageRoom"/> 배선. 방 크기를 바꾸면 <b>꽂힌 것만 움직인다</b> —
    /// 빠진 벽은 옛 자리에 남아 방 안을 가르고, 빠진 스폰 자리는 좁은 방에서 벽 밖에 남는다.
    /// 둘 다 100% 방에서는 아무 증상이 없어서, 레시피가 비율을 굴리기 시작한 날에야 드러난다.
    ///
    /// 그리고 <b>기준 방 숫자와 벽이 같은 방을 말하는지</b> 본다. 어긋나면 적은 숫자의 방에, 벽은 씬의 방에 선다.
    ///
    /// 씬을 여는 테스트라 느리다. 계획서: docs/Room_Size_Plan.md (4단계)
    /// </summary>
    public class StageRoomWiringTests
    {
        private const float Tol = 0.05f;

        private static readonly string[] StageScenes =
        {
            "Assets/Scenes/Level/Stage_01.unity",
            "Assets/Scenes/Level/Stage_02.unity",
            "Assets/Scenes/Level/Stage_03.unity",
            "Assets/Scenes/Level/Stage_04.unity",
            "Assets/Scenes/Level/Stage_05.unity",
            "Assets/Scenes/Level/Stage_Boss.unity",
            "Assets/Scenes/Level/Stage_Mini.unity",
            "Assets/Scenes/Level/Stage_Training.unity",
            "Assets/Scenes/SampleScene.unity",
        };

        /// <summary>방이 필요한 스테이지. 늘거나 줄면 이 숫자와 아래 확인을 같이 고친다.</summary>
        private const int ExpectedRoomStages = 3;

        private string reopen;

        [SetUp]
        public void SetUp() => reopen = SceneManager.GetActiveScene().path;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(reopen) && reopen != SceneManager.GetActiveScene().path)
                EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);
        }

        // ── 있어야 할 곳에 하나 ─────────────────────────

        /// <summary>
        /// 방에서 도는 조우가 있는 스테이지는 방이 정확히 하나다. 없으면 디렉터가 조우를 안 돌리고,
        /// 둘이면 보드와 디렉터가 서로 다른 방을 읽을 수 있다.
        /// </summary>
        [Test]
        public void RoomStages_HaveExactlyOneStageRoom()
        {
            int checkedStages = 0;

            foreach (string path in Scenes())
            {
                Scene scene = Open(path);
                StageWaveBoard board = StageWaveBoard.Find();
                StageRoom[] rooms = Object.FindObjectsByType<StageRoom>(FindObjectsInactive.Include);

                if (board == null || !board.NeedsRoom)
                {
                    Assert.That(rooms.Length, Is.LessThanOrEqualTo(1), $"{path}: StageRoom 이 {rooms.Length}개다");
                    continue;
                }

                checkedStages++;

                Assert.That(rooms.Length, Is.EqualTo(1),
                    $"{path}: 방에서 도는 조우가 있는데 StageRoom 이 {rooms.Length}개다 — 정확히 1개여야 한다.");
                Assert.That(StageRoom.FindIn(scene), Is.SameAs(rooms[0]), $"{path}: 보드가 이 방을 못 찾는다");
            }

            // 조건이 잘못 좁혀져 전부 건너뛰면 이 테스트는 아무것도 안 보면서 통과한다.
            Assert.That(checkedStages, Is.EqualTo(ExpectedRoomStages), "방이 필요한 스테이지 수가 예상과 다르다");
        }

        // ── 기준 방 = 벽 ────────────────────────────────

        /// <summary>
        /// 벽 네 개가 전부 꽂혀 있고, 각 벽의 안쪽 면 · 길이가 기준 방에서 계산한 자리와 같다.
        /// <b>이름이 아니라 자리로 본다</b> — 씬의 <c>Wall_Front</c>는 −Z(가까운 벽)다.
        /// </summary>
        [Test]
        public void BaseRoom_MatchesTheWalls()
        {
            foreach (Wired w in WiredRooms())
            {
                foreach (RoomSide side in new[] { RoomSide.Left, RoomSide.Right, RoomSide.Near, RoomSide.Far })
                {
                    Transform wall = w.Wall(side);
                    Assert.That(wall, Is.Not.Null, $"{w.Path}: {side} 벽이 안 꽂혀 있다 — 방 크기를 바꿔도 이 벽은 안 움직인다");

                    var box = wall.GetComponent<BoxCollider>();
                    Assert.That(box, Is.Not.Null, $"{w.Path}: {side} 벽 '{wall.name}'에 BoxCollider 가 없다");

                    WallPose pose = RoomRules.WallFor(side, w.Room.BaseRoom, w.Thickness);
                    Vector3 want = pose.ColliderSize(box.size.y);

                    Assert.That(wall.position.x, Is.EqualTo(pose.Center.x).Within(Tol), $"{w.Path}: {side} 벽 '{wall.name}' x");
                    Assert.That(wall.position.z, Is.EqualTo(pose.Center.z).Within(Tol), $"{w.Path}: {side} 벽 '{wall.name}' z");
                    Assert.That(box.size.x, Is.EqualTo(want.x).Within(Tol), $"{w.Path}: {side} 벽 '{wall.name}' 콜라이더 x");
                    Assert.That(box.size.z, Is.EqualTo(want.z).Within(Tol), $"{w.Path}: {side} 벽 '{wall.name}' 콜라이더 z");
                }
            }
        }

        /// <summary>
        /// 벽 길이 계산은 <b>회전 없는 벽 · 가운데 콜라이더</b>를 전제한다. 돌아간 벽은 콜라이더 x · z가 월드 축과 어긋나,
        /// 방을 줄이면 엉뚱한 축이 줄어든다.
        /// </summary>
        [Test]
        public void Walls_HaveNoRotation_AndCenteredColliders()
        {
            foreach (Wired w in WiredRooms())
                foreach (RoomSide side in new[] { RoomSide.Left, RoomSide.Right, RoomSide.Near, RoomSide.Far })
                {
                    Transform wall = w.Wall(side);
                    if (wall == null) continue;   // 위 테스트가 잡는다

                    Assert.That(Quaternion.Angle(wall.rotation, Quaternion.identity), Is.LessThan(0.01f),
                        $"{w.Path}: {side} 벽 '{wall.name}'이 돌아가 있다");

                    var box = wall.GetComponent<BoxCollider>();
                    if (box != null)
                        Assert.That(box.center.magnitude, Is.LessThan(Tol), $"{w.Path}: {side} 벽 '{wall.name}' 콜라이더가 가운데가 아니다");
                }
        }

        /// <summary>벽 레이어 콜라이더는 전부 방에 꽂혀 있다. 빠진 벽은 좁아진 방 안에 옛 자리로 서서 방을 가른다.</summary>
        [Test]
        public void StageRoom_OwnsEveryWall()
        {
            int wallLayer = LayerMask.NameToLayer("Wall");
            Assert.That(wallLayer, Is.GreaterThanOrEqualTo(0), "Wall 레이어가 없다");

            foreach (Wired w in WiredRooms())
            {
                var owned = new HashSet<Transform> { w.Wall(RoomSide.Left), w.Wall(RoomSide.Right), w.Wall(RoomSide.Near), w.Wall(RoomSide.Far) };

                foreach (BoxCollider box in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include))
                {
                    if (box.gameObject.layer != wallLayer) continue;
                    if (box.GetComponentInParent<PartyAssembler>(true) != null) continue;   // 파티 호스트 프리팹은 방이 아니다

                    Assert.That(owned.Contains(box.transform), Is.True,
                        $"{w.Path}: 벽 레이어 콜라이더 '{box.name}'이 StageRoom 에 안 꽂혀 있다");
                }
            }
        }

        // ── 따라 움직이는 것 ────────────────────────────

        /// <summary>
        /// 파티 시작 자리와 보드의 스폰 지점은 전부 <c>anchors</c>에 있다.
        /// 빠지면 좁은 방에서 그 자리만 옛 좌표에 남아 벽 밖이 된다 — 파티가 벽 밖에서 시작한다.
        /// </summary>
        [Test]
        public void StageRoom_AnchorsEverySpawnPoint()
        {
            foreach (Wired w in WiredRooms())
            {
                var anchors = new HashSet<Transform>(w.Anchors);

                foreach (PartySpawnPoint sp in Object.FindObjectsByType<PartySpawnPoint>(FindObjectsInactive.Include))
                    Assert.That(anchors.Contains(sp.transform), Is.True,
                        $"{w.Path}: PartySpawnPoint '{sp.name}'이 anchors 에 없다");

                StageWaveBoard board = StageWaveBoard.Find();
                if (board == null) continue;

                foreach (SpawnPointBinding row in board.SpawnPoints)
                    if (row.point != null)
                        Assert.That(anchors.Contains(row.point), Is.True,
                            $"{w.Path}: 스폰 지점 '{row.id}'({row.point.name})이 anchors 에 없다");
            }
        }

        /// <summary>
        /// 옮기는 대상끼리 부모-자식이면 자식이 <b>두 번</b> 옮겨진다(부모를 따라 한 번, 자기 차례에 한 번).
        /// 방 오브젝트 자신도 대상이면 안 된다 — 벽이 그 자식이다.
        /// </summary>
        [Test]
        public void Targets_DoNotNest()
        {
            foreach (Wired w in WiredRooms())
            {
                var targets = new List<Transform>();
                foreach (RoomSide side in new[] { RoomSide.Left, RoomSide.Right, RoomSide.Near, RoomSide.Far })
                    if (w.Wall(side) != null) targets.Add(w.Wall(side));
                targets.AddRange(w.Stretch);
                targets.AddRange(w.Anchors);

                for (int i = 0; i < targets.Count; i++)
                {
                    Transform a = targets[i];
                    if (a == null) { Assert.Fail($"{w.Path}: stretch/anchors 에 빈 칸이 있다"); continue; }

                    Assert.That(a, Is.Not.SameAs(w.Room.transform), $"{w.Path}: StageRoom 자신이 옮길 대상에 들어 있다");

                    for (int j = 0; j < targets.Count; j++)
                    {
                        if (i == j || targets[j] == null) continue;

                        Assert.That(a, Is.Not.SameAs(targets[j]), $"{w.Path}: '{a.name}'이 두 번 꽂혀 있다");
                        Assert.That(a.IsChildOf(targets[j]), Is.False,
                            $"{w.Path}: '{a.name}'이 다른 대상 '{targets[j].name}'의 자식이다 — 두 번 옮겨진다");
                    }
                }
            }
        }

        // ── 도우미 ──────────────────────────────────────

        private static IEnumerable<string> Scenes()
        {
            foreach (string path in StageScenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                    yield return path;
        }

        private static Scene Open(string path) => EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        /// <summary>방이 있는 씬을 하나씩 열어 배선을 읽는다. 열린 동안만 유효하다.</summary>
        private static IEnumerable<Wired> WiredRooms()
        {
            int found = 0;

            foreach (string path in Scenes())
            {
                Scene scene = Open(path);
                StageRoom room = StageRoom.FindIn(scene);
                if (room == null) continue;

                found++;
                yield return new Wired(path, room);
            }

            Assert.That(found, Is.EqualTo(ExpectedRoomStages), "StageRoom 이 있는 스테이지 수가 예상과 다르다");
        }

        /// <summary>
        /// <see cref="StageRoom"/>의 배선 읽기. 컴포넌트에 읽기 전용 API를 늘리지 않고 직렬화 값으로 본다 —
        /// 씬 구성 테스트가 <c>CameraFollow</c>를 읽는 방식과 같다.
        /// </summary>
        private readonly struct Wired
        {
            public readonly string Path;
            public readonly StageRoom Room;
            private readonly SerializedObject so;

            public Wired(string path, StageRoom room)
            {
                Path = path;
                Room = room;
                so = new SerializedObject(room);
            }

            public float Thickness => so.FindProperty("wallThickness").floatValue;

            public Transform Wall(RoomSide side)
            {
                string field = side switch
                {
                    RoomSide.Left => "wallLeft",
                    RoomSide.Right => "wallRight",
                    RoomSide.Near => "wallNear",
                    _ => "wallFar",
                };

                return so.FindProperty(field).objectReferenceValue as Transform;
            }

            public List<Transform> Stretch => Array("stretch");

            public List<Transform> Anchors => Array("anchors");

            private List<Transform> Array(string field)
            {
                var list = new List<Transform>();
                SerializedProperty p = so.FindProperty(field);

                for (int i = 0; i < p.arraySize; i++)
                    list.Add(p.GetArrayElementAtIndex(i).objectReferenceValue as Transform);

                return list;
            }
        }
    }
}
