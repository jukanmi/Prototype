// 씬에서 방 크기의 유일한 주인 — 기준 방을 들고, 비율대로 벽 · 바닥 · 지점을 옮긴다.
// 계산은 RoomRules(Wave.cs)가 하고, 여기는 그 답을 트랜스폼에 쓰기만 한다.
// 비율은 들어가 있는 지도 칸의 보정(GameManager.CurrentNodeModifier)에서 온다. 칸이 없을 때만 디버그 값.
// 계획서: docs/Room_Size_Plan.md (4항 · 3단계 · 7단계)

using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 이 방이 몇 %인가, 그래서 실제 방이 어디까지인가를 아는 단 하나의 자리.
    ///
    /// 스폰 계산(<see cref="StageDirector"/>)과 지점 검사(<see cref="StageWaveBoard"/>)는 <see cref="Room"/>을 읽기만 한다.
    /// 보드에 크기를 따로 두지 않는다 — 기준 방과 실제 방이 둘 다 보드에 있으면 무엇을 읽을지 고르는 분기가 생긴다.
    ///
    /// <b>Awake에서 한 번 적용하고 판 내내 안 바꾼다.</b> 실행 순서 -300은 <see cref="PartyAssembler"/>(-200)보다 앞이다 —
    /// 파티가 옮겨진 시작 자리에 서야 하고, <see cref="GroundPlate"/>가 프레임 캐시에 옛 바닥 크기를 담기 전이어야 한다.
    ///
    /// <b>100%면 아무것도 건드리지 않는다.</b> 저작한 씬이 그대로 기준 방이다.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public class StageRoom : MonoBehaviour
    {
        /// <summary>플레이 중 씬에 하나. 에디트 모드에서는 null이라 <see cref="Find"/>를 쓴다.</summary>
        public static StageRoom Instance { get; private set; }

        [Header("기준 방")]
        [Tooltip("100%일 때 적을 세울 수 있는 구역. 벽 콜라이더의 안쪽 면이 이 네 변에 서 있어야 한다.")]
        [SerializeField] private RoomRect baseRoom = RoomRect.Default;

        [Tooltip("벽 두께. 안쪽 면을 방 변에 맞추려고 벽 중심을 반 두께만큼 밖으로 뺀다.")]
        [SerializeField] private float wallThickness = 0.5f;

        [Header("벽 — 이름이 아니라 자리로 꽂는다")]
        [Tooltip("−X 벽.")]
        [SerializeField] private Transform wallLeft;

        [Tooltip("+X 벽.")]
        [SerializeField] private Transform wallRight;

        [Tooltip("−Z 벽. 카메라 쪽, 화면 아래. 씬에서는 이름이 Wall_Front 다 — 이름과 자리가 반대이니 주의.")]
        [SerializeField] private Transform wallNear;

        [Tooltip("+Z 벽. 뒷벽, 화면 위. 씬에서는 이름이 Wall_Back 이다.")]
        [SerializeField] private Transform wallFar;

        [Header("방을 따라 움직이는 것")]
        [Tooltip("바닥 · 뒷벽 그림 · 테두리 선. 방 중심 기준으로 옮기고, 바닥과 나란한 축만 늘이고 줄인다(높이는 그대로).\n\n" +
                 "서로의 자식이거나 anchors 와 겹치면 두 번 옮겨진다.")]
        [SerializeField] private Transform[] stretch = new Transform[0];

        [Tooltip("스폰 지점 · 파티 시작 자리 · 소품. 방 안 상대 위치를 지키도록 옮기기만 한다(크기는 그대로).")]
        [SerializeField] private Transform[] anchors = new Transform[0];

        [Header("디버그")]
        [Tooltip("지도 칸 없이 씬만 재생할 때 쓸 비율. 0이면 100. 80~125, 5 단위.\n\n" +
                 "런 안에서 지도 칸으로 들어오면 칸의 비율이 이긴다 — 이 값은 무시하고 경고만 남긴다.")]
        [SerializeField, Range(0, RoomRules.MaxPercent)] private int debugRoomPercent;

        private bool applied;
        private RoomRect room;
        private int percent = RoomRules.FullPercent;

        /// <summary>100%일 때의 방. 에디트 모드의 검사 · 기즈모와 테스트가 읽는다.</summary>
        public RoomRect BaseRoom => baseRoom;

        /// <summary>실제 방. 적용 전(에디트 모드)이면 기준 방이다.</summary>
        public RoomRect Room => applied ? room : baseRoom;

        /// <summary>적용된 비율. 적용 전이면 100.</summary>
        public int Percent => applied ? percent : RoomRules.FullPercent;

        public bool IsApplied => applied;

        /// <summary>
        /// Enter Play Mode Options가 Domain Reload를 끄고 있어 static이 살아남는다(<see cref="GroundRegistry"/>와 같은 이유).
        /// 안 비우면 지난 세션의 파괴된 방을 다음 세션의 디렉터가 읽는다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[StageRoom] 씬에 StageRoom 이 둘이다 — '{Instance.name}'을(를) 쓰고 '{name}'은(는) 끈다.", this);
                enabled = false;
                return;
            }

            Instance = this;

            GameManager gm = GameManager.Instance;
            int requested = ResolveRequest(gm, debugRoomPercent, out RoomRequestSource source);

            if (source == RoomRequestSource.RunNodeOverDebug)
                BattleLog.Warn(LogCategory.State,
                    $"{name}: 디버그 비율 {debugRoomPercent}%는 무시한다 — 지도 칸({gm.CurrentNode})의 {requested}%가 이긴다. " +
                    "씬 단독 재생용 값이 런에 남아 있으니 0으로 되돌릴 것.", this);

            Apply(requested);

            if (percent != RoomRules.FullPercent)
                Debug.Log($"[StageRoom] 비율 출처: {Describe(source)}", this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>이번 판의 비율이 어디서 왔는가. 로그와 경고가 읽는다.</summary>
        public enum RoomRequestSource
        {
            /// <summary>지도 칸이 없고 디버그 값도 없다 — 저작 그대로(100%).</summary>
            None,

            /// <summary>런 안의 지도 칸. 방 크기 보정이 없는 칸이면 100이다.</summary>
            RunNode,

            /// <summary>지도 칸이 이겼고, 디버그 값이 0이 아니어서 무시됐다.</summary>
            RunNodeOverDebug,

            /// <summary>지도 칸이 없어 디버그 값을 썼다(씬 단독 재생).</summary>
            DebugValue,
        }

        /// <summary>
        /// 이번 판에 요청할 비율. <b>들어가 있는 지도 칸이 있으면 그 칸의 보정이 이긴다</b> — 보정이 없는 칸이면 100이다.
        /// 칸이 없을 때만(씬 단독 재생, 또는 Boot는 떠 있지만 런 밖) 디버그 값을 쓴다.
        ///
        /// 칸이 있는데 디버그 값을 쓰면, 누가 디버그 값을 저장한 채로 커밋한 순간 그 스테이지가 모든 런에서 그 크기로 돈다.
        /// 반대로 GameManager 가 있다는 것만으로 디버그를 끄면, Boot를 겹쳐 연 채 씬을 재생하는 흔한 작업에서 디버그 값이 안 먹는다.
        ///
        /// 순수 판단이라 테스트가 GameManager 를 직접 넘긴다.
        /// </summary>
        public static int ResolveRequest(GameManager gm, int debugPercent, out RoomRequestSource source)
        {
            MapNode node = gm != null ? gm.CurrentNode : null;

            if (node != null)
            {
                source = debugPercent != 0 ? RoomRequestSource.RunNodeOverDebug : RoomRequestSource.RunNode;
                return gm.CurrentNodeModifier.RoomPercent;
            }

            if (debugPercent != 0)
            {
                source = RoomRequestSource.DebugValue;
                return debugPercent;
            }

            source = RoomRequestSource.None;
            return RoomRules.FullPercent;
        }

        private static string Describe(RoomRequestSource source)
        {
            switch (source)
            {
                case RoomRequestSource.RunNode:
                case RoomRequestSource.RunNodeOverDebug: return "지도 칸";
                case RoomRequestSource.DebugValue:       return "디버그 값(씬 단독 재생)";
                default:                                 return "없음";
            }
        }

        /// <summary>
        /// 비율을 적용한다. <b>한 번만</b> 된다 — 두 번째 부름은 경고만 남긴다.
        /// 판 도중에 방이 바뀌면 이미 선 적 · 이미 계산한 스폰 자리가 옛 방에 남는다.
        ///
        /// 범위 밖 비율은 가까운 값으로 물리고 <b>경고한다</b>. 조용히 물리면 레시피 실수가 안 보인다.
        /// public 인 이유는 테스트다 — 에디트 모드에서는 Awake가 안 돈다. 요청값은 <see cref="ResolveRequest"/>가 정한다.
        /// </summary>
        public void Apply(int requested)
        {
            if (applied)
            {
                BattleLog.Warn(LogCategory.State,
                    $"{name}: 방 크기는 이미 {percent}%로 정해졌다 — {requested}% 요청은 무시한다.", this);
                return;
            }

            percent = RoomRules.ClampPercent(requested);
            room = RoomRules.Scale(baseRoom, percent);
            applied = true;

            if (percent != RoomRules.Normalize(requested))
                BattleLog.Warn(LogCategory.State,
                    $"{name}: 방 크기 {requested}%는 쓸 수 없다 — {percent}%로 연다 " +
                    $"({RoomRules.MinPercent}~{RoomRules.MaxPercent}, {RoomRules.Step} 단위).", this);

            if (!RoomRules.Fits(room))
                BattleLog.Warn(LogCategory.State,
                    $"{name}: {percent}% 방 {room} 이(가) 규칙을 못 지킨다 — 마법사 줄이 모자라거나 벽이 화면 밖이다. " +
                    "기준 방을 확인할 것.", this);

            if (percent == RoomRules.FullPercent) return;

            MoveWall(wallLeft, RoomSide.Left);
            MoveWall(wallRight, RoomSide.Right);
            MoveWall(wallNear, RoomSide.Near);
            MoveWall(wallFar, RoomSide.Far);

            if (stretch != null)
                foreach (Transform t in stretch) Stretch(t);

            if (anchors != null)
                foreach (Transform t in anchors) Anchor(t);

            Debug.Log($"[StageRoom] 방 {percent}% — {room}", this);
        }

        /// <summary>
        /// 벽을 새 자리에 세우고 콜라이더 길이를 맞춘다. 높이(위치 y · 콜라이더 높이)는 그대로다.
        ///
        /// 콜라이더 크기는 로컬 값이라 스케일로 나눈다. 벽은 회전이 없다고 본다 — 지금 씬이 그렇고, 4단계 배선 테스트가 본다.
        /// </summary>
        private void MoveWall(Transform wall, RoomSide side)
        {
            if (wall == null)
            {
                BattleLog.Warn(LogCategory.State,
                    $"{name}: {side} 벽이 안 꽂혀 있다 — 그 벽은 {percent}% 방을 안 따라가고 옛 자리에 남는다.", this);
                return;
            }

            WallPose pose = RoomRules.WallFor(side, room, wallThickness);

            Vector3 p = wall.position;
            wall.position = new Vector3(pose.Center.x, p.y, pose.Center.z);

            var box = wall.GetComponent<BoxCollider>();
            if (box == null) return;

            Vector3 world = pose.ColliderSize(0f);
            Vector3 scale = wall.lossyScale;

            box.size = new Vector3(Divide(world.x, scale.x), box.size.y, Divide(world.z, scale.z));
        }

        private void Stretch(Transform t)
        {
            if (t == null) return;

            t.position = RoomRules.MovePoint(t.position, baseRoom, room);
            t.localScale = RoomRules.StretchScale(t.localScale, t.rotation, percent);
        }

        private void Anchor(Transform t)
        {
            if (t == null) return;

            t.position = RoomRules.MovePoint(t.position, baseRoom, room);
        }

        private static float Divide(float value, float scale)
            => Mathf.Abs(scale) > 0.0001f ? value / Mathf.Abs(scale) : value;

        /// <summary>
        /// 플레이 중인 방. 없으면 null — <b>부르는 쪽이 에러를 내고 멈춰야 한다</b>(<see cref="StageWaveBoard.Find"/>와 같은 규약).
        /// 에디트 모드에서는 Awake가 안 돌아 <see cref="Instance"/>가 비므로 씬을 훑는다.
        /// </summary>
        public static StageRoom Find()
        {
            if (Instance != null) return Instance;

            StageRoom[] rooms = FindObjectsByType<StageRoom>(FindObjectsInactive.Exclude);
            if (rooms == null || rooms.Length == 0) return null;

            if (rooms.Length > 1)
                BattleLog.Warn(LogCategory.State, $"StageRoom 이 {rooms.Length}개다 — '{rooms[0].name}'을(를) 쓴다.", rooms[0]);

            return rooms[0];
        }

        /// <summary>
        /// <paramref name="scene"/> 안의 방. 보드 검사 · 기즈모가 쓴다 — <b>보드와 방은 같은 씬에 있어야 짝이다.</b>
        ///
        /// 씬 전체를 훑는 <see cref="Find"/>를 쓰면, 스테이지 씬을 열어 둔 채로 테스트를 돌릴 때
        /// 테스트가 만든 보드가 열린 씬의 방을 자기 방으로 읽는다. 여러 씬을 겹쳐 연 에디터에서도 같다.
        /// </summary>
        public static StageRoom FindIn(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;

            StageRoom found = null;
            int count = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (StageRoom r in root.GetComponentsInChildren<StageRoom>(false))
                {
                    if (found == null) found = r;
                    count++;
                }

            if (count > 1)
                BattleLog.Warn(LogCategory.State, $"{scene.name} 에 StageRoom 이 {count}개다 — '{found.name}'을(를) 쓴다.", found);

            return found;
        }

        /// <summary>
        /// 배선을 한 번에 꽂는다. 테스트가 부른다 — <see cref="StageWaveBoard.Configure(SpawnPointBinding[])"/>와 같은 이유로
        /// <c>SerializedObject</c>를 안 쓴다.
        /// </summary>
        public void Configure(RoomRect baseRect, float thickness = 0.5f,
                              Transform left = null, Transform right = null, Transform near = null, Transform far = null,
                              Transform[] stretchTargets = null, Transform[] anchorTargets = null)
        {
            baseRoom = baseRect;
            wallThickness = thickness;
            wallLeft = left;
            wallRight = right;
            wallNear = near;
            wallFar = far;
            stretch = stretchTargets ?? new Transform[0];
            anchors = anchorTargets ?? new Transform[0];
        }

#if UNITY_EDITOR
        /// <summary>실제 방(적용 전이면 기준 방)의 네 변. 벽 안쪽 면이 이 선 위에 있어야 한다.</summary>
        private void OnDrawGizmosSelected()
        {
            RoomRect r = Room;

            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(r.CenterX, 0f, r.CenterZ), new Vector3(r.HalfX * 2f, 0.02f, r.HalfZ * 2f));
        }
#endif
    }
}
