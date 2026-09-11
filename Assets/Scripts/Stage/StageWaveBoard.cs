// 방이 아는 것 — 스폰 지점 표.
//
// 웨이브 <b>내용</b>은 프로젝트 애셋에 있고 스폰 <b>지점</b>은 씬에 있다.
// ScriptableObject 는 씬 오브젝트 참조를 들 수 없어서(저장할 때 유니티가 null 로 지운다)
// 애셋은 지점을 이름으로만 부르고, 그 이름을 실제 Transform 으로 바꾸는 자리가 여기다.
//
// 조우 목록도 여기 있다. 개수는 목록의 길이지 따로 적는 정수가 아니다.
// 계획서: docs/Wave_Authoring_Refactor_Plan.md (2.2) · docs/Stage_Encounter_Unification_Plan.md (2.2)

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ SpawnPointBinding ═══════════════════════════════════════════

    /// <summary>지점 표의 한 줄. 이름과 씬 오브젝트를 짝짓는다.</summary>
    [Serializable]
    public struct SpawnPointBinding
    {
        [Tooltip("웨이브 애셋이 부르는 이름. 앞뒤 공백은 무시된다.")]
        public string id;

        [Tooltip("실제 자리. 빈 게임오브젝트면 충분하다.")]
        public Transform point;

        /// <summary>이름이 붙어 있는가. 공백만 든 이름은 없는 것으로 본다.</summary>
        public bool HasId => !string.IsNullOrWhiteSpace(id);

        /// <summary>이 줄로 좌표를 낼 수 있는가.</summary>
        public bool IsBound => HasId && point != null;

        /// <summary>바닥 좌표. 높이는 접지가 다시 잡으므로 0으로 눌러 넘긴다.</summary>
        public Vector3 Ground
            => point != null ? new Vector3(point.position.x, 0f, point.position.z) : Vector3.zero;
    }

    // ══ BoardIssue ═══════════════════════════════════════════

    /// <summary>보드에서 잡히는 저작 실수. 앞의 넷은 지점 표, 뒤의 둘은 웨이브 목록이다.</summary>
    public enum BoardProblem
    {
        /// <summary>이름이 비었다. 아무도 이 줄을 부를 수 없다.</summary>
        EmptyId,

        /// <summary>같은 이름이 앞에 이미 있다. 뒤에 붙은 줄은 영영 안 쓰인다.</summary>
        DuplicateId,

        /// <summary>이름만 있고 자리가 없다. 그 이름을 부르는 줄이 자동 배치로 떨어진다.</summary>
        MissingTransform,

        /// <summary>
        /// 자리가 방 밖이다. 방은 사방이 콜라이더라 밖에서 소환하면 벽에 걸려 못 들어온다.
        /// 소환 자체는 안으로 당겨서 하지만(<see cref="WaveSpawnPlanner.ClampIntoRoom"/>),
        /// 저작자가 의도한 자리는 아니므로 말해 준다.
        /// </summary>
        OutsideRoom,

        /// <summary>웨이브 칸이 비었다. 빠뜨린 것인지 일부러 비운 것인지 구분할 방법이 없다.</summary>
        EmptyWaveSlot,

        /// <summary>
        /// 웨이브가 부르는 지점 이름이 표에 없다.
        ///
        /// <b>이 검사가 이 보드의 존재 이유 절반이다.</b> 애셋과 씬을 이름으로 잇는 순간
        /// 오타가 컴파일에 안 걸리고, 증상은 "그 적만 엉뚱한 데서 나온다"로만 보인다.
        /// </summary>
        UnknownPointId,

        /// <summary>
        /// 땅속에서 솟는 적이 <b>예고를 낼 시간도 없이</b> 이르게 나온다.
        ///
        /// 발밑에서 솟는 적은 표식이 없으면 반응할 정보가 아예 없다. 웨이브가 열리자마자
        /// 솟으면 아무리 표식을 띄워도 읽을 틈이 없으므로, 등장 시각을 예고 길이만큼 미뤄야 한다.
        /// </summary>
        BurrowTooEarly,

        /// <summary>
        /// 다음 웨이브 조건이 아직 안 돌아간다.
        ///
        /// 런타임은 전멸로 접어서 스테이지가 멈추지는 않는다. 그래도 말해 줘야 한다 —
        /// 저작자는 자기가 지정한 조건이 걸린 줄 알고 그 위에 배치를 쌓는다.
        /// </summary>
        UnsupportedAdvance,

        /// <summary>
        /// 진입선 조건인데 자리가 없다. <b>넘을 선이 없어 영영 안 열린다.</b>
        /// 런타임은 앞 조우 뒤로 접어서 멈추지는 않지만, 저작자가 의도한 동작은 아니다.
        /// </summary>
        CrossLineWithoutSite,
    }

    /// <summary>잡힌 실수 하나. 어느 줄의 무엇인지까지 들고 있어야 씬에서 찾아갈 수 있다.</summary>
    public struct BoardIssue
    {
        public int row;
        public string id;
        public BoardProblem problem;

        public string Describe()
        {
            string name = string.IsNullOrWhiteSpace(id) ? "(이름 없음)" : $"'{id}'";

            switch (problem)
            {
                case BoardProblem.EmptyId:
                    return $"지점 {row}번 줄: 이름이 비었다. 이 줄은 아무도 못 부른다.";
                case BoardProblem.DuplicateId:
                    return $"지점 {row}번 줄: {name} 이(가) 앞에 이미 있다. 이 줄은 안 쓰인다.";
                case BoardProblem.MissingTransform:
                    return $"지점 {row}번 줄: {name} 에 자리가 안 꽂혀 있다. 이 이름을 부르는 적은 자동 배치로 나온다.";
                case BoardProblem.OutsideRoom:
                    return $"지점 {row}번 줄: {name} 이(가) 방 밖이다. 소환은 방 안으로 당겨진다.";
                case BoardProblem.EmptyWaveSlot:
                    return $"웨이브 {row}번 칸이 비었다.";
                case BoardProblem.BurrowTooEarly:
                    return $"웨이브 {row}번: 땅속 등장이 너무 이르다({name}초). " +
                           $"예고에 {ArenaSpawnPlanner.TelegraphLead}초가 필요하다.";
                case BoardProblem.UnsupportedAdvance:
                    return $"웨이브 {row}번: 다음 웨이브 조건 {name} 은(는) 아직 안 돌아간다. 전멸로 돈다.";
                case BoardProblem.CrossLineWithoutSite:
                    return $"조우 {row}번: 진입선 조건인데 자리가 없다. 앞 조우 뒤로 연다.";
                default:
                    return $"웨이브 {row}번: {name} 이(가) 지점 표에 없다. 그 적은 자동 배치로 나온다.";
            }
        }
    }

    // ══ StageWaveBoard ═══════════════════════════════════════════

    /// <summary>
    /// 방 하나에 하나. 웨이브 애셋이 부르는 <b>지점 이름을 실제 자리로 바꾼다.</b>
    ///
    /// 디렉터 프리팹에 배열로 얹지 않고 씬에 따로 세우는 이유는 프리팹 오버라이드다.
    /// 유니티는 배열을 크기와 원소로 나눠 오버라이드에 기록해서, 프리팹 원본을 나중에
    /// 건드리면 인스턴스와 병합되는 방식이 직관과 어긋난다. 실수로 Apply 를 누르면
    /// 한 씬의 목록이 프리팹을 타고 나머지 씬으로 번진다. 별개 오브젝트는 그 함정이 아예 없다.
    ///
    /// <see cref="PartySpawnPoint"/>와 같은 규약이다 — 방마다 다른 값이고, 방이 아는 값이다.
    /// </summary>
    public class StageWaveBoard : MonoBehaviour
    {
        [Tooltip("이 방이 순서대로 돌 조우. 위에서 아래로 실행된다. 개수는 이 목록의 길이다.")]
        [SerializeField] private StageEncounter[] encounters = new StageEncounter[0];

        [Tooltip("웨이브 애셋이 이름으로 부를 자리들. 빈 게임오브젝트를 꽂으면 된다.")]
        [SerializeField] private SpawnPointBinding[] spawnPoints = new SpawnPointBinding[0];

        /// <summary>이 방의 조우 목록. 디렉터가 순서대로 읽는다.</summary>
        public IReadOnlyList<StageEncounter> Encounters => encounters;

        public int WaveCount => encounters != null ? encounters.Length : 0;

        /// <summary>
        /// <paramref name="index"/>번째 웨이브. 범위 밖이거나 칸이 비었으면 null이다 —
        /// <b>부르는 쪽이 그걸 보고 멈춰야 한다.</b> 빈 칸을 조용히 건너뛰면
        /// 저작자가 웨이브 하나를 빠뜨린 것과 일부러 비운 것이 구분되지 않는다.
        /// </summary>
        public WaveAsset WaveAt(int index) => EncounterAt(index).content;

        /// <summary>
        /// <paramref name="index"/>번째 조우. 범위 밖이면 빈 칸이다 —
        /// <b>부르는 쪽이 그걸 보고 멈춰야 한다.</b>
        /// </summary>
        public StageEncounter EncounterAt(int index)
            => encounters != null && index >= 0 && index < encounters.Length
                ? encounters[index]
                : default;

        /// <summary>지점 표. 저작 검증과 기즈모가 읽는다.</summary>
        public IReadOnlyList<SpawnPointBinding> SpawnPoints => spawnPoints;

        public int SpawnPointCount => spawnPoints != null ? spawnPoints.Length : 0;

        /// <summary>
        /// 표를 한 번에 꽂는다. 씬 빌더와 테스트가 부른다.
        /// <see cref="EnemySpawnService.Configure"/>와 같은 이유로 <c>SerializedObject</c>를 안 쓴다 —
        /// 그쪽은 스크립트가 방금 바뀐 직후에 값이 안 남는 일이 있고, 증상이 "그 칸만 비어 있다"라
        /// 원인을 짚기가 대단히 어렵다.
        /// </summary>
        public void Configure(SpawnPointBinding[] points)
            => spawnPoints = points ?? new SpawnPointBinding[0];

        /// <summary>조우 목록을 한 번에 꽂는다. 씬 빌더와 테스트가 부른다.</summary>
        public void Configure(StageEncounter[] list)
            => encounters = list ?? new StageEncounter[0];

        // ── 조회 ────────────────────────────────────────

        /// <summary>
        /// 이름으로 자리를 찾는다.
        ///
        /// <b>같은 이름이 여럿이면 먼저 붙은 것을 쓴다.</b> 자리가 안 꽂힌 줄은 건너뛴다 —
        /// 비어 있는 줄이 뒤의 멀쩡한 줄을 가리면, 표에는 자리가 보이는데 적은 엉뚱한 데서
        /// 나오는 상태가 된다. 원인에서 가장 먼 종류의 증상이다.
        /// </summary>
        public bool TryResolve(string id, out Vector3 ground)
        {
            ground = Vector3.zero;

            string wanted = Normalize(id);
            if (string.IsNullOrEmpty(wanted) || spawnPoints == null) return false;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (!spawnPoints[i].IsBound) continue;
                if (!string.Equals(Normalize(spawnPoints[i].id), wanted, StringComparison.Ordinal)) continue;

                ground = spawnPoints[i].Ground;
                return true;
            }

            return false;
        }

        /// <summary>이 이름이 표에 있고 쓸 수 있는가. 저작 검증이 웨이브 애셋을 훑을 때 읽는다.</summary>
        public bool CanResolve(string id) => TryResolve(id, out _);

        // ── 검증 ────────────────────────────────────────

        /// <summary>
        /// 보드에서 잡히는 저작 실수 전부. <b>순수 판단이라 씬 없이도 테스트가 부를 수 있다.</b>
        /// 로그로 흘리지 않고 목록으로 돌려주는 이유이기도 하다 — 로그는 검사할 수가 없다.
        ///
        /// 지점 표를 먼저 보고 웨이브 목록을 나중에 본다. 순서가 곧 읽는 순서다 —
        /// 표가 망가져 있으면 웨이브 쪽 경고는 전부 그 여파라, 표를 먼저 보여 줘야 한다.
        /// </summary>
        public List<BoardIssue> Issues()
        {
            var issues = new List<BoardIssue>();

            CollectPointIssues(issues);
            CollectWaveIssues(issues);

            return issues;
        }

        private void CollectPointIssues(List<BoardIssue> issues)
        {
            if (spawnPoints == null) return;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPointBinding row = spawnPoints[i];
                string id = Normalize(row.id);

                if (!row.HasId)
                {
                    issues.Add(Issue(i, row.id, BoardProblem.EmptyId));
                    continue;
                }

                if (!seen.Add(id))
                    issues.Add(Issue(i, row.id, BoardProblem.DuplicateId));

                if (row.point == null)
                {
                    issues.Add(Issue(i, row.id, BoardProblem.MissingTransform));
                    continue;
                }

                if (!WaveSpawnPlanner.IsInsideRoom(row.Ground))
                    issues.Add(Issue(i, row.id, BoardProblem.OutsideRoom));
            }
        }

        /// <summary>
        /// 웨이브가 부르는 이름이 표에 다 있는가. <b>이 검사가 이 보드의 존재 이유 절반이다.</b>
        /// 애셋과 씬을 이름으로 이으면 오타가 컴파일에 안 걸리고, 실행해 봐야만 드러난다.
        /// </summary>
        private void CollectWaveIssues(List<BoardIssue> issues)
        {
            if (encounters == null) return;

            for (int i = 0; i < encounters.Length; i++)
            {
                WaveAsset wave = encounters[i].content;

                if (wave == null)
                {
                    issues.Add(Issue(i, null, BoardProblem.EmptyWaveSlot));
                    continue;
                }

                if (!WaveAdvanceRules.IsSupported(wave.advance))
                    issues.Add(Issue(i, wave.advance.ToString(), BoardProblem.UnsupportedAdvance));

                if (encounters[i].trigger == EncounterTrigger.CrossLine && !encounters[i].HasSite)
                    issues.Add(Issue(i, null, BoardProblem.CrossLineWithoutSite));

                foreach (string id in wave.PointIds())
                    if (!CanResolve(id))
                        issues.Add(Issue(i, id, BoardProblem.UnknownPointId));

                WaveSpawnEntry[] spawns = wave.spawns;
                if (spawns == null) continue;

                for (int s = 0; s < spawns.Length; s++)
                {
                    if (spawns[s].motion != SpawnMotion.Burrow) continue;
                    if (BurrowRules.HasRoomForTelegraph(spawns[s].AppearAt)) continue;

                    issues.Add(Issue(i, spawns[s].AppearAt.ToString("0.##"), BoardProblem.BurrowTooEarly));
                }
            }
        }

        /// <summary>잡힌 실수를 콘솔로 흘린다. 디렉터가 시작할 때 한 번 부른다.</summary>
        public int ReportIssues()
        {
            List<BoardIssue> issues = Issues();

            for (int i = 0; i < issues.Count; i++)
                BattleLog.Warn(LogCategory.State, $"{name} — {issues[i].Describe()}", this);

            return issues.Count;
        }

        // ── 찾기 ────────────────────────────────────────

        /// <summary>
        /// 씬에서 보드를 찾는다. 없으면 null — <b>부르는 쪽이 에러를 내고 멈춰야 한다.</b>
        /// 조용한 폴백은 만들지 않는다. 3 · 4스테이지가 1번 표로 돌던 버그(<c>c325639f</c>)가
        /// 정확히 그 조용한 폴백에서 나왔다.
        /// </summary>
        public static StageWaveBoard Find()
        {
            StageWaveBoard[] boards = FindObjectsByType<StageWaveBoard>(FindObjectsInactive.Exclude);

            if (boards == null || boards.Length == 0) return null;

            if (boards.Length > 1)
                BattleLog.Warn(LogCategory.State,
                    $"StageWaveBoard 가 {boards.Length}개다 — '{boards[0].name}'을(를) 쓴다.", boards[0]);

            return boards[0];
        }

        // ── 공통 ────────────────────────────────────────

        private static string Normalize(string id) => id == null ? null : id.Trim();

        private static BoardIssue Issue(int row, string id, BoardProblem problem)
            => new BoardIssue { row = row, id = id, problem = problem };

#if UNITY_EDITOR
        /// <summary>
        /// 자리를 눈으로 볼 수 있어야 한다. 좌표를 숫자로만 두면 "굴_좌가 어디였더라"를
        /// 인스펙터와 씬 뷰를 오가며 찾게 된다.
        ///
        /// 성한 줄은 초록, 문제 있는 줄은 빨강. 방 경계도 같이 그려서 지점이 밖으로
        /// 나갔는지 한눈에 보이게 한다.
        /// </summary>
        private void OnDrawGizmos()
        {
            DrawRoomBounds();

            if (spawnPoints == null) return;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPointBinding row = spawnPoints[i];
                if (row.point == null) continue;

                Vector3 g = row.Ground;
                bool ok = row.HasId && WaveSpawnPlanner.IsInsideRoom(g);

                Gizmos.color = ok ? new Color(0.3f, 0.9f, 0.4f, 0.9f) : new Color(1f, 0.35f, 0.3f, 0.9f);
                Gizmos.DrawWireSphere(g, 0.4f);
                Gizmos.DrawLine(g, g + Vector3.up * 1.2f);

                UnityEditor.Handles.Label(g + Vector3.up * 1.4f, row.HasId ? row.id : "(이름 없음)");
            }
        }

        private static void DrawRoomBounds()
        {
            float x = WaveSpawnPlanner.RoomHalfX - WaveSpawnPlanner.SpawnInset;
            float z = WaveSpawnPlanner.MaxDepth;

            Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.35f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(x * 2f, 0.02f, z * 2f));
        }
#endif
    }
}
