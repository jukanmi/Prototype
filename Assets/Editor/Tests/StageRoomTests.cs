using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 방 비율을 씬의 트랜스폼에 쓰는 자리. 계산은 <see cref="RoomRules"/>가 이미 테스트로 덮여 있으므로
    /// 여기서는 <b>무엇을 어떻게 옮기는가</b>만 본다 — 벽은 자리와 콜라이더 길이, 그림은 바닥과 나란한 축만,
    /// 지점은 위치만, 높이는 어느 것도 안 바뀐다. 그리고 100%면 아무것도 안 건드린다.
    ///
    /// 벽 규격은 Stage_01 그대로다(옆벽 x ±6.25 · (0.5, 4, 6), 앞뒤 벽 z ±3.25 · (13, 4, 0.5)).
    ///
    /// 계획서: docs/Room_Size_Plan.md (4항 · 3단계)
    /// </summary>
    public class StageRoomTests
    {
        private const float Eps = 0.001f;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        private StageRoom room;
        private Transform left, right, near, far;
        private Transform floor, backdrop, anchor;

        [SetUp]
        public void SetUp()
        {
            left = Wall("Wall_Left", new Vector3(-6.25f, 2f, 0f), new Vector3(0.5f, 4f, 6f));
            right = Wall("Wall_Right", new Vector3(6.25f, 2f, 0f), new Vector3(0.5f, 4f, 6f));
            near = Wall("Wall_Front", new Vector3(0f, 2f, -3.25f), new Vector3(13f, 4f, 0.5f));
            far = Wall("Wall_Back", new Vector3(0f, 2f, 3.25f), new Vector3(13f, 4f, 0.5f));

            // 눕힌 바닥: 로컬 x · y가 월드 x · z
            floor = Node("Floor", Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(12f, 6f, 1f));

            // 세운 뒷벽 그림: 로컬 y가 월드 높이
            backdrop = Node("Backdrop", new Vector3(0f, 2f, 3f), Quaternion.identity, new Vector3(12f, 4f, 1f));

            anchor = Node("SpawnPoint", new Vector3(3f, 0.5f, 1.5f), Quaternion.identity, Vector3.one);

            room = Node("StageRoom", Vector3.zero, Quaternion.identity, Vector3.one).gameObject.AddComponent<StageRoom>();
            room.Configure(RoomRect.Default, 0.5f, left, right, near, far,
                           new[] { floor, backdrop }, new[] { anchor });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                if (go != null) Object.DestroyImmediate(go);

            foreach (Object asset in assets)
                if (asset != null) Object.DestroyImmediate(asset);

            spawned.Clear();
            assets.Clear();
        }

        // ── 적용 전 ─────────────────────────────────────

        /// <summary>에디트 모드(보드 검사 · 기즈모)는 적용 전이다. 그때 방은 기준 방이어야 한다.</summary>
        [Test]
        public void BeforeApply_RoomIsBaseRoom()
        {
            Assert.That(room.IsApplied, Is.False);
            Assert.That(room.Percent, Is.EqualTo(100));
            AssertRoom(room.Room, -6f, 6f, -3f, 3f);
        }

        // ── 100% ────────────────────────────────────────

        /// <summary>100%는 저작한 씬 그대로다. 한 치라도 건드리면 방 크기 기능이 모든 씬을 흔든다.</summary>
        [TestCase(100)]
        [TestCase(0)]
        public void Full_TouchesNothing(int percent)
        {
            room.Apply(percent);

            Assert.That(room.IsApplied, Is.True);
            Assert.That(room.Percent, Is.EqualTo(100));
            AssertRoom(room.Room, -6f, 6f, -3f, 3f);

            AssertVector(left.position, -6.25f, 2f, 0f, "옆벽");
            AssertVector(near.GetComponent<BoxCollider>().size, 13f, 4f, 0.5f, "앞벽 콜라이더");
            AssertVector(floor.localScale, 12f, 6f, 1f, "바닥");
            AssertVector(backdrop.localScale, 12f, 4f, 1f, "뒷벽 그림");
            AssertVector(anchor.position, 3f, 0.5f, 1.5f, "스폰 지점");
        }

        // ── 80% ─────────────────────────────────────────

        [Test]
        public void Small_RoomIsScaled()
        {
            room.Apply(80);

            Assert.That(room.Percent, Is.EqualTo(80));
            AssertRoom(room.Room, -4.8f, 4.8f, -2.4f, 2.4f);
        }

        /// <summary>벽 안쪽 면이 새 방 변에 오고, 콜라이더가 새 방 길이만큼이다. 높이는 그대로.</summary>
        [Test]
        public void Small_WallsFollowTheRoom()
        {
            room.Apply(80);

            AssertVector(left.position, -5.05f, 2f, 0f, "Left");
            AssertVector(right.position, 5.05f, 2f, 0f, "Right");
            AssertVector(near.position, 0f, 2f, -2.65f, "Near");
            AssertVector(far.position, 0f, 2f, 2.65f, "Far");

            AssertVector(left.GetComponent<BoxCollider>().size, 0.5f, 4f, 4.8f, "옆벽 콜라이더");
            AssertVector(near.GetComponent<BoxCollider>().size, 10.6f, 4f, 0.5f, "앞벽 콜라이더(모서리 덮음)");
        }

        /// <summary>바닥은 두 축이 줄고, 세운 뒷벽 그림은 폭만 준다 — 벽 그림 높이가 방 따라 줄면 안 된다.</summary>
        [Test]
        public void Small_StretchShrinksOnlyFloorAxes()
        {
            room.Apply(80);

            AssertVector(floor.localScale, 9.6f, 4.8f, 1f, "바닥");
            AssertVector(backdrop.localScale, 9.6f, 4f, 0.8f, "뒷벽 그림");
            AssertVector(backdrop.position, 0f, 2f, 2.4f, "뒷벽 그림은 새 뒷변으로 온다");
        }

        /// <summary>지점은 방 안 상대 위치를 지키고, 크기와 높이는 그대로다.</summary>
        [Test]
        public void Small_AnchorsKeepRelativePosition()
        {
            room.Apply(80);

            AssertVector(anchor.position, 2.4f, 0.5f, 1.2f, "스폰 지점");
            Assert.That(anchor.localScale, Is.EqualTo(Vector3.one));
        }

        /// <summary>콜라이더 크기는 로컬 값이다. 벽이 늘려져 있으면 그만큼 나눠야 월드 길이가 맞는다.</summary>
        [Test]
        public void ScaledWall_ColliderSizeIsLocal()
        {
            left.localScale = new Vector3(1f, 1f, 2f);
            room.Apply(80);

            AssertVector(left.GetComponent<BoxCollider>().size, 0.5f, 4f, 2.4f, "스케일 2인 옆벽");
        }

        // ── 방어 ────────────────────────────────────────

        /// <summary>범위 밖 비율은 물린다. 조용히가 아니라 경고와 함께다(로그는 검사 대상이 아님).</summary>
        [TestCase(50, 80)]
        [TestCase(200, 125)]
        [TestCase(92, 90)]
        public void OutOfRangePercent_IsClamped(int requested, int expected)
        {
            room.Apply(requested);
            Assert.That(room.Percent, Is.EqualTo(expected));
        }

        /// <summary>판 도중에 방이 바뀌면 이미 선 적과 계산한 스폰 자리가 옛 방에 남는다. 두 번째 적용은 무시한다.</summary>
        [Test]
        public void SecondApply_IsIgnored()
        {
            room.Apply(80);
            room.Apply(125);

            Assert.That(room.Percent, Is.EqualTo(80));
            AssertVector(left.position, -5.05f, 2f, 0f, "두 번 옮겨졌다");
        }

        /// <summary>안 꽂힌 벽이 있어도 나머지는 옮긴다. 빠진 벽은 4단계 배선 테스트가 잡는다.</summary>
        [Test]
        public void MissingWall_DoesNotStopTheRest()
        {
            room.Configure(RoomRect.Default, 0.5f, null, right, near, far, new[] { floor }, new[] { anchor });
            room.Apply(80);

            Assert.That(left.position.x, Is.EqualTo(-6.25f).Within(Eps), "안 꽂힌 벽은 그대로다");
            Assert.That(right.position.x, Is.EqualTo(5.05f).Within(Eps));
            AssertVector(anchor.position, 2.4f, 0.5f, 1.2f, "스폰 지점");
        }

        /// <summary>원점이 아닌 방은 원점이 아니라 방 중심으로 줄어든다.</summary>
        [Test]
        public void OffOriginBase_ScalesAroundRoomCenter()
        {
            var centered = Node("Center", new Vector3(20f, 0f, 1f), Quaternion.identity, Vector3.one);
            room.Configure(new RoomRect(14f, 26f, -2f, 4f), 0.5f, anchorTargets: new[] { centered, anchor });

            room.Apply(80);

            AssertRoom(room.Room, 15.2f, 24.8f, -1.4f, 3.4f);
            AssertVector(centered.position, 20f, 0f, 1f, "방 중심은 그대로");
        }

        // ── 비율은 어디서 오는가 ────────────────────────
        // 계획서: docs/Room_Size_Plan.md (7단계)

        /// <summary>GameManager 없음(씬 단독 재생) · 디버그 값 없음 → 저작 그대로.</summary>
        [Test]
        public void Request_NoRunNoDebug_IsFull()
        {
            Assert.That(StageRoom.ResolveRequest(null, 0, out StageRoom.RoomRequestSource source), Is.EqualTo(100));
            Assert.That(source, Is.EqualTo(StageRoom.RoomRequestSource.None));
        }

        [Test]
        public void Request_NoRun_UsesDebugValue()
        {
            Assert.That(StageRoom.ResolveRequest(null, 85, out StageRoom.RoomRequestSource source), Is.EqualTo(85));
            Assert.That(source, Is.EqualTo(StageRoom.RoomRequestSource.DebugValue));
        }

        /// <summary>
        /// Boot가 떠 있어도(GameManager 있음) 들어가 있는 칸이 없으면 디버그 값이 먹는다.
        /// Boot를 겹쳐 연 채 씬을 재생하는 흔한 작업에서 디버그 값이 안 먹으면 확인할 방법이 없다.
        /// </summary>
        [Test]
        public void Request_ManagerWithoutNode_UsesDebugValue()
        {
            GameManager gm = NewManager(roomMin: 90, roomMax: 90);

            Assert.That(gm.CurrentNode, Is.Null);
            Assert.That(StageRoom.ResolveRequest(gm, 110, out StageRoom.RoomRequestSource source), Is.EqualTo(110));
            Assert.That(source, Is.EqualTo(StageRoom.RoomRequestSource.DebugValue));
        }

        /// <summary>들어가 있는 지도 칸의 비율이 곧 이 방의 비율이다. 그리고 그 값으로 실제로 벽이 옮겨진다.</summary>
        [Test]
        public void Request_RunNode_DrivesTheRoom()
        {
            GameManager gm = NewManager(roomMin: 90, roomMax: 90);
            EnterFirstNode(gm);

            int requested = StageRoom.ResolveRequest(gm, 0, out StageRoom.RoomRequestSource source);
            Assert.That(requested, Is.EqualTo(90));
            Assert.That(source, Is.EqualTo(StageRoom.RoomRequestSource.RunNode));

            room.Apply(requested);

            Assert.That(room.Percent, Is.EqualTo(90));
            AssertVector(left.position, -5.65f, 2f, 0f, "90% 방의 옆벽");
        }

        /// <summary>
        /// 런 안에서는 칸이 이긴다 — <b>칸이 100%여도</b>. 디버그 값이 이기면, 누가 그 값을 저장한 채 커밋한 순간
        /// 그 스테이지가 모든 런에서 그 크기로 돈다.
        /// </summary>
        [TestCase(90, 90, 110, 90)]
        [TestCase(0, 0, 110, 100)]
        public void Request_RunNode_BeatsDebugValue(int roomMin, int roomMax, int debug, int expected)
        {
            GameManager gm = NewManager(roomMin, roomMax);
            EnterFirstNode(gm);

            Assert.That(StageRoom.ResolveRequest(gm, debug, out StageRoom.RoomRequestSource source), Is.EqualTo(expected));
            Assert.That(source, Is.EqualTo(StageRoom.RoomRequestSource.RunNodeOverDebug));
        }

        // ── 실행 순서 ───────────────────────────────────

        /// <summary>
        /// 파티가 서기 전에 방이 옮겨져야 한다. 늦으면 파티가 옛 시작 자리에 선 뒤 그 자리가 옮겨져,
        /// 좁은 방에서 파티가 벽 밖에 서거나 한 프레임 튄다.
        /// </summary>
        [Test]
        public void RunsBeforePartyAssembler()
        {
            int roomOrder = typeof(StageRoom).GetCustomAttribute<DefaultExecutionOrder>().order;
            int partyOrder = typeof(PartyAssembler).GetCustomAttribute<DefaultExecutionOrder>().order;

            Assert.That(roomOrder, Is.LessThan(partyOrder));
        }

        // ── 도우미 ──────────────────────────────────────

        /// <summary>1층 전투 한 칸(방 범위 지정) → 보스. 런을 열어 둔 GameManager.</summary>
        private GameManager NewManager(int roomMin, int roomMax)
        {
            FloorRule start = FloorRule.Of(1, 1, NodeCandidate.Of("Stage_A", MapNodeKind.Battle));
            start.roomPercentMin = roomMin;
            start.roomPercentMax = roomMax;

            var recipe = ScriptableObject.CreateInstance<RunMapRecipe>();
            recipe.floors = new[] { start, FloorRule.Of(1, 1, NodeCandidate.Of("Stage_Boss", MapNodeKind.Boss)) };
            assets.Add(recipe);

            var go = new GameObject("GameManager");
            spawned.Add(go);

            GameManager gm = go.AddComponent<GameManager>();
            gm.Configure(recipe, 4242);
            Assert.That(gm.StartNewRun(), Is.True);

            return gm;
        }

        private static void EnterFirstNode(GameManager gm)
        {
            MapNode first = gm.Map.NodesOn(0)[0];
            Assert.That(gm.Progress.TrySelect(first.Id), Is.True);
        }

        private Transform Node(string name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = new GameObject(name);
            spawned.Add(go);

            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            return go.transform;
        }

        private Transform Wall(string name, Vector3 position, Vector3 size)
        {
            Transform t = Node(name, position, Quaternion.identity, Vector3.one);
            t.gameObject.AddComponent<BoxCollider>().size = size;
            return t;
        }

        private static void AssertVector(Vector3 v, float x, float y, float z, string what)
        {
            Assert.That(v.x, Is.EqualTo(x).Within(Eps), $"{what} x");
            Assert.That(v.y, Is.EqualTo(y).Within(Eps), $"{what} y");
            Assert.That(v.z, Is.EqualTo(z).Within(Eps), $"{what} z");
        }

        private static void AssertRoom(in RoomRect r, float minX, float maxX, float minZ, float maxZ)
        {
            Assert.That(r.MinX, Is.EqualTo(minX).Within(Eps), "MinX");
            Assert.That(r.MaxX, Is.EqualTo(maxX).Within(Eps), "MaxX");
            Assert.That(r.MinZ, Is.EqualTo(minZ).Within(Eps), "MinZ");
            Assert.That(r.MaxZ, Is.EqualTo(maxZ).Within(Eps), "MaxZ");
        }
    }
}
