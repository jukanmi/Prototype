using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 아레나 스테이지의 진행. 구간 목록을 들고, 플레이어가 지금 어느 토막에 있는지 보고,
    /// <b>경계 소유자</b>(<see cref="StageBounds"/>)에 그 구간의 경계를 넘긴다.
    ///
    /// <code>
    ///   [아레나1 / 1R] → [통로] → [아레나2 / 2R]
    /// </code>
    ///
    /// 카메라를 직접 만지지 않는다는 점이 이 구조의 요점이다. 여기가 하는 일은
    /// "지금 구간이 어디까지인가"를 갱신하는 것뿐이고, 락도 스크롤도 그 경계의 결과다.
    /// </summary>
    public class StageRunner : StageProgressSource
    {
        [Tooltip("왼쪽부터 순서대로. 빌더가 채운다.")]
        [SerializeField] private StageSection[] sections = new StageSection[0];

        [Tooltip("아레나 구간과 같은 순서일 필요는 없다 — 경계로 짝을 찾는다.")]
        [SerializeField] private ArenaDirector[] arenas = new ArenaDirector[0];

        [SerializeField] private StageBounds bounds;

        /// <summary>지금 플레이어가 있는 구간. 로그·디버그용.</summary>
        public int CurrentSection { get; private set; } = -1;

        /// <summary>짝 없는 아레나를 이미 외친 구간. 매 프레임 같은 에러를 쏟지 않는다.</summary>
        private readonly HashSet<int> warnedMissingArena = new HashSet<int>();

        public IReadOnlyList<StageSection> Sections => sections;

        // ── 승패 판정이 물어보는 것 ──────────────────────

        /// <summary>아레나가 하나라도 안 끝났으면 아직 남았다.</summary>
        public override bool ThreatsRemaining
        {
            get
            {
                for (int i = 0; i < arenas.Length; i++)
                    if (arenas[i] != null && !arenas[i].IsDone) return true;

                return false;
            }
        }

        public override bool HasSpawnedAny
        {
            get
            {
                for (int i = 0; i < arenas.Length; i++)
                    if (arenas[i] != null && arenas[i].HasSpawnedAny) return true;

                return false;
            }
        }

        private void Awake()
        {
            if (bounds == null) bounds = StageBounds.Instance;
            if (bounds == null) bounds = FindAnyObjectByType<StageBounds>();

            EnsureArenas();
        }

        /// <summary>
        /// 아레나 목록이 비었으면 씬에서 주워 담는다.
        ///
        /// <b>이 컴포넌트가 프리팹이 되면 이 배열은 반드시 끊긴다.</b> 아레나는 씬 오브젝트이고
        /// 프리팹 에셋은 씬 오브젝트를 참조할 수 없어서, 호스트를 프리팹으로 만드는 순간
        /// <c>arenas</c>가 <c>[null, null]</c>이 된다. 그러면 <see cref="ArenaAt"/>가 언제나
        /// null을 돌려주고 <c>Begin()</c>이 한 번도 안 불려 <b>적이 하나도 안 나온다</b> —
        /// 그런데 구간 경계는 프리팹에 남아 있어서 카메라 락은 정상으로 걸린다.
        /// 방은 멀쩡해 보이고 문도 안 닫히고 적만 없다.
        ///
        /// 짝은 인덱스가 아니라 <b>왼쪽 경계</b>로 맞추므로(<see cref="ArenaAt"/>)
        /// 주워 담는 순서는 상관없다.
        /// </summary>
        private void EnsureArenas()
        {
            if (HasAnyArena()) return;

            arenas = FindObjectsByType<ArenaDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (arenas.Length > 0)
                BattleLog.Log(LogCategory.State,
                    $"{name}: 아레나 배선이 비어 있어 씬에서 {arenas.Length}개를 찾아 채웠다.", this);
        }

        private bool HasAnyArena()
        {
            if (arenas == null) return false;

            for (int i = 0; i < arenas.Length; i++)
                if (arenas[i] != null) return true;

            return false;
        }

        private void Start()
        {
            if (sections == null || sections.Length == 0)
            {
                BattleLog.Warn(LogCategory.State, $"{name}: 구간이 하나도 없다. 이 스테이지는 그냥 열린 방이다.", this);
                return;
            }

            // 첫 프레임에 보간이 돌면 스테이지가 카메라 이동으로 시작한다. 시작은 붙여 놓는다.
            int start = StageLayoutRules.SectionAt(sections, PlayerX());
            CurrentSection = start;

            bounds?.Snap(sections[start].minX, sections[start].maxX);
            ApplySection(start, snap: true);

            Debug.Log($"[StageRunner] 구간 {sections.Length}개 · 아레나 {arenas.Length}개 — " +
                      $"{sections[start].kind}에서 시작");
        }

        private void Update()
        {
            if (sections == null || sections.Length == 0) return;

            // 라운드가 도는 동안에는 위치를 안 본다. 넉백으로 경계에 걸치는 한 프레임에
            // 카메라 락이 풀리면 전투 도중 화면이 통째로 흔들린다.
            ArenaDirector busy = RunningArena();
            if (busy != null)
            {
                bounds?.SwitchTo(busy.Section.minX, busy.Section.maxX);
                return;
            }

            int index = StageLayoutRules.SectionAt(sections, PlayerX());
            if (index < 0) return;

            if (index != CurrentSection)
            {
                CurrentSection = index;
                Debug.Log($"[StageRunner] 구간 {index} 진입 — {sections[index].kind}");
            }

            ApplySection(index, snap: false);
        }

        /// <summary>구간의 경계를 넘기고, 아레나라면 라운드를 연다.</summary>
        private void ApplySection(int index, bool snap)
        {
            StageSection section = sections[index];

            if (bounds != null)
            {
                if (snap) bounds.Snap(section.minX, section.maxX);
                else bounds.SwitchTo(section.minX, section.maxX);
            }

            if (section.kind != SectionKind.Arena) return;

            ArenaDirector arena = ArenaAt(section.minX);

            if (arena == null)
            {
                // 여기서 조용히 넘어가면 "방은 멀쩡한데 적만 없다"가 되고, 화면에 단서가 없다.
                // 구간마다 매 프레임 불리는 자리라 한 번만 외친다.
                if (warnedMissingArena.Add(index))
                    Debug.LogError(
                        $"[StageRunner] 구간 {index}(아레나, 왼쪽 경계 {section.minX:0.##})에 맞는 " +
                        "ArenaDirector 가 없다 — 이 방은 라운드가 열리지 않아 적이 안 나온다. " +
                        "아레나의 minX 가 구간의 minX 와 같은지 확인할 것.", this);

                return;
            }

            arena.Begin();   // 이미 시작했거나 끝난 아레나는 스스로 무시한다
        }

        /// <summary>지금 라운드가 도는 아레나. 없으면 null.</summary>
        private ArenaDirector RunningArena()
        {
            for (int i = 0; i < arenas.Length; i++)
                if (arenas[i] != null && arenas[i].IsRunning) return arenas[i];

            return null;
        }

        /// <summary>왼쪽 경계로 아레나를 찾는다. 구간과 아레나를 인덱스로 묶으면 순서가 어긋난다.</summary>
        private ArenaDirector ArenaAt(float sectionMinX)
        {
            for (int i = 0; i < arenas.Length; i++)
            {
                ArenaDirector a = arenas[i];
                if (a != null && Mathf.Approximately(a.TriggerLine, sectionMinX)) return a;
            }

            return null;
        }

        /// <summary>
        /// 지금 조작 중인 몸의 X.
        ///
        /// 교대로 내려간 동료는 <c>SetActive(false)</c> 상태라 좌표가 벤치에 있던 자리 그대로다.
        /// 활성 여부를 같이 보지 않으면 내려간 동료의 옛 좌표로 구간이 바뀐다.
        /// </summary>
        private static float PlayerX()
        {
            foreach (Entity e in BattleRegistry.Allies)
            {
                if (e == null || !e.isActiveAndEnabled || e.Combat.IsDead) continue;
                return e.transform.position.x;
            }

            return 0f;
        }

        /// <summary>빌더가 구간과 아레나를 물려 준다.</summary>
        public void Configure(StageSection[] stageSections, ArenaDirector[] stageArenas, StageBounds owner)
        {
            sections = stageSections;
            arenas = stageArenas;
            bounds = owner;
        }
    }
}
