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
            arena?.Begin();   // 이미 시작했거나 끝난 아레나는 스스로 무시한다
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
