// 웨이브 저작 어휘 — 스폰 한 줄 · 위치 소스 · 등장 모션 · 다음 웨이브 조건.
//
// 저작 단위는 <b>적 한 기</b>다. 예전에는 "돌진전사 2기를 1.2초 간격으로" 같은 묶음이었지만,
// 등장 위치를 씬의 실제 지점으로 찍으려면 묶음 단위로는 지정할 자리가 없다.
//
// 이 파일은 순수 데이터다. 씬도 애셋도 시간도 모른다.
// 계획서: docs/Wave_Authoring_Refactor_Plan.md

using System;
using UnityEngine;

namespace Prototype
{
    // ══ SpawnOrigin ═══════════════════════════════════════════

    /// <summary>
    /// 등장 자리를 <b>누가 정하는가</b>.
    ///
    /// <see cref="Auto"/>가 기본값이고, 그래야 하는 이유가 있다.
    /// <see cref="WaveSpawnPlanner"/>의 자동 배치는 단순한 좌표 계산이 아니라
    /// <b>레벨 디자인 규칙 그 자체</b>다 — 돌진전사는 플레이어와 같은 깊이 줄에 서서
    /// 깊이 회피를 강제하고, 마법사는 반대로 그 줄을 피해 축을 옮기는 동선을 만든다.
    /// 모든 줄을 <see cref="Point"/>로 못 박으면 그 규칙이 통째로 사라지고,
    /// 증상은 "돌진이 잘 안 맞는다" 정도로만 보여 원인에서 아주 멀다.
    ///
    /// 그래서 <see cref="Point"/>는 <b>의도가 있을 때만</b> 쓴다.
    /// </summary>
    public enum SpawnOrigin
    {
        /// <summary>역할별 규칙이 자리를 정한다. <see cref="WaveSpawnPlanner"/>가 계산한다.</summary>
        Auto = 0,

        /// <summary>씬에 찍어 둔 지점에서 나온다. <c>pointId</c>로 찾는다.</summary>
        Point = 1,
    }

    // ══ SpawnMotion ═══════════════════════════════════════════

    /// <summary>
    /// <b>어떻게</b> 등장하는가. 판정을 끄고 몸을 옮기는 구간의 모양이다.
    ///
    /// 앞의 둘은 이미 있는 경로에 이름만 붙인 것이다 —
    /// <see cref="FlyIn"/>은 <c>EnemySpawnService.SpawnAtEdge</c>,
    /// <see cref="FromWall"/>은 <c>EnemySpawnService.SpawnFromWall</c>.
    /// <see cref="Burrow"/>만 새로 만든다.
    ///
    /// <b>모션은 시간을 먹는다.</b> <see cref="WaveSpawnEntry.appearAt"/>은 연출이
    /// <i>시작</i>되는 시각이지 적이 <i>싸우기 시작</i>하는 시각이 아니다.
    /// 이 둘을 섞으면 저작자가 표를 못 믿게 된다(계획서 3.5).
    /// </summary>
    public enum SpawnMotion
    {
        /// <summary>화면 밖에서 방 가장자리로 날아 들어온다. 지금 웨이브의 동작.</summary>
        FlyIn = 0,

        /// <summary>벽 뒤에서 걸어 나온다. 지금 아레나의 동작.</summary>
        FromWall = 1,

        /// <summary>
        /// 땅속에서 솟아오른다.
        ///
        /// <b>예고 없이는 쓸 수 없다.</b> 발밑에서 튀어나오는 적은 반응할 <i>정보</i>가 없어
        /// 그냥 부당하게 읽힌다. <see cref="SpawnTelegraph"/>를 발밑에 띄우는 것이
        /// 이 모션의 일부지 선택지가 아니다.
        /// </summary>
        Burrow = 2,
    }

    // ══ WaveAdvance ═══════════════════════════════════════════

    /// <summary>
    /// 다음 웨이브가 <b>언제 열리는가</b>.
    ///
    /// <see cref="TargetKilled"/>는 <b>아직 배선되지 않았다.</b> 지정 개체를 가리키려면
    /// 웨이브가 스폰 줄을 트리로 들어야 하는데, 그건 이번 리팩터링에서 건너뛴 부분이다.
    /// 이름만 먼저 두는 이유는 <see cref="AllCleared"/> 하나짜리 enum 을 나중에 늘리면
    /// 이미 구운 애셋의 직렬화 값이 흔들리기 때문이다.
    /// 지원 여부는 <see cref="WaveAdvanceRules.IsSupported"/>로 물어본다.
    /// </summary>
    public enum WaveAdvance
    {
        /// <summary>이 웨이브가 낳은 적이 전부 죽으면. 지금까지의 유일한 조건.</summary>
        AllCleared = 0,

        /// <summary>지정한 적이 죽으면. <b>미구현</b> — 만나면 <see cref="AllCleared"/>로 돈다.</summary>
        TargetKilled = 1,
    }

    /// <summary>
    /// <see cref="WaveAdvance"/>의 지원 범위. 디렉터와 저작 검증이 <b>같은 답</b>을 봐야 해서
    /// 판단을 여기 한 군데 둔다 — 두 벌이 되면 검증은 통과하는데 런타임이 다르게 도는
    /// 조합이 반드시 생긴다.
    /// </summary>
    public static class WaveAdvanceRules
    {
        /// <summary>이 조건이 실제로 돌아가는가. 거짓이면 디렉터가 경고하고 전멸 조건으로 떨어뜨린다.</summary>
        public static bool IsSupported(WaveAdvance advance) => advance == WaveAdvance.AllCleared;

        /// <summary>실제로 적용할 조건. 미구현 조건은 <see cref="WaveAdvance.AllCleared"/>로 접는다.</summary>
        public static WaveAdvance Resolve(WaveAdvance advance)
            => IsSupported(advance) ? advance : WaveAdvance.AllCleared;
    }

    // ══ WaveSpawnEntry ═══════════════════════════════════════════

    /// <summary>
    /// 웨이브 안의 <b>적 한 기</b>. "돌진전사 하나가 3.5초에 왼쪽 굴에서 솟는다"가 한 줄이다.
    ///
    /// 한 줄이 한 기라 <b>줄 자체는 자기가 몇 번째인지 모른다.</b> 좌우 교대와 깊이 줄
    /// 로테이션이 순번을 입력으로 쓰므로, 자동 배치를 쓰는 줄은 밖에서
    /// <b>역할별 통산 번호</b>를 받는다(<see cref="WaveLayout.AutoLaneIndices"/>).
    /// 안 받으면 전사 다섯이 전부 같은 줄에 겹쳐 선다.
    /// </summary>
    [Serializable]
    public struct WaveSpawnEntry
    {
        public EnemyRole role;

        [Tooltip("강화 개체. 체력·공격력이 배로 오르고 이름에 (강화)가 붙는다.")]
        public bool elite;

        [Header("등장 위치")]
        [Tooltip("Auto 는 역할별 배치 규칙이 자리를 정한다. Point 는 씬에 찍어 둔 지점을 쓴다.")]
        public SpawnOrigin origin;

        [Tooltip("Point 일 때 찾을 스폰 지점 이름. 씬 보드의 지점 표에 있어야 한다.")]
        public string pointId;

        [Tooltip("Auto 일 때 어느 쪽 벽에서 들어오는가. 방 조우만 읽는다.")]
        public SpawnSide side;

        [Header("아레나 조우 전용")]
        [Tooltip("사방 중 어느 벽에서 나오는가. 방 조우는 읽지 않는다.")]
        public SpawnWall wall;

        [Tooltip("그 벽면 위 어디인가. 0이 한쪽 끝, 1이 반대쪽 끝이다.")]
        [Range(0f, 1f)] public float alongWall;

        [Header("등장 시각 · 모션")]
        [Tooltip("웨이브 시작 기준. 적이 싸우기 시작하는 시각이 아니라 연출이 시작되는 시각이다.")]
        [Min(0f)] public float appearAt;

        public SpawnMotion motion;

        /// <summary>음수로 저작된 시각은 0으로 본다. 조우가 시작되기 전은 없다.</summary>
        public float AppearAt => Mathf.Max(0f, appearAt);

        /// <summary>
        /// 벽면 위 자리. 0~1 밖은 물린다 — 벽을 벗어나면 모서리에 끼어 못 나온다.
        ///
        /// 방 조우는 이 값을 안 읽는다. 방은 역할별 규칙이 자리를 정하기 때문이다
        /// (<see cref="WaveSpawnPlanner.PlanAuto"/>).
        /// </summary>
        public float AlongWall => Mathf.Clamp01(alongWall);

        /// <summary>
        /// 씬 지점을 실제로 찾아야 하는 줄인가.
        ///
        /// <see cref="SpawnOrigin.Point"/>인데 <see cref="pointId"/>가 비어 있으면 <b>거짓</b>이다.
        /// 저작 실수를 조용히 원점 소환으로 바꾸지 않고, 자동 배치로 떨어뜨린 뒤 경고하기 위한 갈래다.
        ///
        /// 공백만 든 이름도 비어 있는 것으로 본다 — 인스펙터에서 지우다 남은 한 칸은
        /// 눈으로 구분이 안 되는데, 이름 대조에서는 절대 안 맞는 이름이 된다.
        /// </summary>
        public bool UsesPoint => origin == SpawnOrigin.Point && !string.IsNullOrWhiteSpace(pointId);

        /// <summary>저작이 온전한가. 거짓이면 그 줄은 자동 배치로 돈다.</summary>
        public bool IsWellFormed => origin != SpawnOrigin.Point || UsesPoint;

        /// <summary>자동 배치 한 줄.</summary>
        public static WaveSpawnEntry Auto(EnemyRole role,
                                          SpawnSide side = SpawnSide.Right,
                                          float appearAt = 0f,
                                          bool elite = false,
                                          SpawnMotion motion = SpawnMotion.FlyIn)
            => new WaveSpawnEntry
            {
                role = role,
                elite = elite,
                origin = SpawnOrigin.Auto,
                // null 이 아니라 빈 문자열이다. 유니티가 직렬화하면서 null 을 ""로 바꾸므로,
                // 여기서 null 을 쓰면 메모리에서 만든 줄과 애셋에서 읽은 줄이 안 같아진다.
                pointId = "",
                side = side,
                appearAt = appearAt,
                motion = motion,
            };

        /// <summary>
        /// 아레나 벽에서 나오는 한 줄.
        ///
        /// 위치 소스는 <see cref="SpawnOrigin.Auto"/>다 — 자리를 정하는 것이 규칙이라는 뜻은 같고,
        /// 그 규칙이 방에서는 역할별 줄, 아레나에서는 벽면 분산일 뿐이다.
        /// </summary>
        public static WaveSpawnEntry AtWall(EnemyRole role, SpawnWall wall, float alongWall,
                                            float appearAt = 0f, bool elite = false)
            => new WaveSpawnEntry
            {
                role = role,
                elite = elite,
                origin = SpawnOrigin.Auto,
                pointId = "",
                side = SpawnSide.Right,
                wall = wall,
                alongWall = alongWall,
                appearAt = appearAt,
                motion = SpawnMotion.FromWall,
            };

        /// <summary>씬 지점을 찍은 한 줄.</summary>
        public static WaveSpawnEntry At(EnemyRole role, string pointId,
                                        float appearAt = 0f,
                                        SpawnMotion motion = SpawnMotion.FlyIn,
                                        bool elite = false)
            => new WaveSpawnEntry
            {
                role = role,
                elite = elite,
                origin = SpawnOrigin.Point,
                pointId = pointId,
                side = SpawnSide.Right,
                appearAt = appearAt,
                motion = motion,
            };
    }

    // ══ WaveLayout ═══════════════════════════════════════════

    /// <summary>
    /// 웨이브 하나를 통째로 보고 <b>자동 배치의 순번</b>을 매기는 순수 함수.
    /// 저작 단위가 묶음에서 한 줄로 바뀌면서 생긴 자리다.
    ///
    /// 한 줄이 한 기라 줄 안에는 순번이 없다. 아무 대책 없이 전부 0번으로 두면
    /// <b>전사 다섯이 같은 줄에 겹쳐 선다.</b>
    /// </summary>
    public static class WaveLayout
    {
        /// <summary>
        /// 각 줄이 받을 배치 순번. 자동 배치가 아닌 줄은 <c>-1</c>이다.
        ///
        /// <b>순번은 역할별로 센다.</b> 웨이브 전체를 하나로 세면 안 된다 —
        /// 줄 로테이션 표가 역할마다 따로기 때문이다. 전사는 <c>MeleeLanes</c>를,
        /// 돌진전사는 <c>ChargerLaneOffsets</c>를, 마법사는 상 · 하 교대를 쓴다.
        /// 통짜 번호를 넘기면 마법사 둘의 상하가 뒤집히고, 무엇보다
        /// <b>돌진전사의 0번(플레이어와 같은 줄)이 앞 역할의 마릿수에 따라 밀린다.</b>
        /// 그러면 깊이 회피를 강제하려고 만든 규칙이 조용히 죽는다.
        ///
        /// <b>씬 지점을 쓰는 줄은 순번을 소비하지 않는다.</b> 소비하게 두면 지점 한 줄을
        /// 끼워 넣는 것만으로 나머지 자동 배치가 통째로 밀린다.
        /// </summary>
        public static int[] AutoLaneIndices(WaveSpawnEntry[] entries)
            => AutoLaneIndices(entries, null);

        /// <summary>
        /// 어느 줄이 실제로 지점에서 나오는지 <b>부르는 쪽이 알려 주는</b> 판.
        ///
        /// 애셋만 봐서는 지점을 찾을 수 있는지 알 수 없다. 씬 보드에 그 이름이 없으면 그 줄은
        /// 자동 배치로 떨어지고, <b>그러면 순번을 받아야 한다.</b> 안 주면 0번으로 몰려
        /// 멀쩡한 0번 줄과 같은 자리에 선다 — 이름 오타 하나가 적 두 기를 겹치게 만든다.
        /// </summary>
        /// <param name="atPoint">
        /// 줄 수와 길이가 같은 판정표. <c>true</c>면 지점에서 나오므로 순번을 안 먹는다.
        /// null 이면 저작값(<see cref="WaveSpawnEntry.UsesPoint"/>)을 그대로 믿는다.
        /// </param>
        public static int[] AutoLaneIndices(WaveSpawnEntry[] entries, bool[] atPoint)
        {
            if (entries == null || entries.Length == 0) return new int[0];

            bool useFlags = atPoint != null && atPoint.Length == entries.Length;

            var lanes = new int[entries.Length];

            // 역할 수가 넷뿐이라 배열이 사전보다 싸고, 순서도 보장된다.
            var used = new int[EnemyRoleCount];

            for (int i = 0; i < entries.Length; i++)
            {
                bool placed = useFlags ? atPoint[i] : entries[i].UsesPoint;
                if (placed) { lanes[i] = -1; continue; }

                int role = RoleSlot(entries[i].role);
                lanes[i] = used[role];
                used[role]++;
            }

            return lanes;
        }

        /// <summary><see cref="EnemyRole"/>의 항목 수. 새 역할을 넣으면 여기도 늘어야 한다.</summary>
        public const int EnemyRoleCount = 4;

        private static int RoleSlot(EnemyRole role)
        {
            int slot = (int)role;
            return slot >= 0 && slot < EnemyRoleCount ? slot : 0;
        }
    }
}
