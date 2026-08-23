using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// <b>웨이브 스테이지</b>의 배치표. 방 하나에서 웨이브가 연달아 도는 방들이다.
    ///
    /// 애셋이 아니라 코드인 이유는 이 프로젝트의 다른 표(<c>EnemyPrefabBuilder.Variants</c>,
    /// <c>GameManager.DefaultStages</c>)와 같다 — 씬 · 애셋에 흩어 두면 "지금 3-2가 몇 기인가"를
    /// 다섯 군데를 열어 봐야 알 수 있고, 테스트가 표를 직접 읽을 수도 없다.
    /// 씬별로 손을 대고 싶으면 <see cref="StageDirector"/>의 인스펙터 배열이 이 표를 덮는다.
    ///
    /// <b>2 · 5스테이지는 여기 없다.</b> 그 둘은 아레나와 통로로 이뤄진 스크롤 스테이지라
    /// 웨이브가 아니라 라운드로 돌고, 구성은 <see cref="ArenaRoundCatalog"/>에 있다.
    ///
    /// <b>역할군 도입 순서가 곧 학습 순서다.</b>
    /// <list type="number">
    /// <item>1 입문 — 전사만. 변수를 빼고 타격감과 콤보에 집중시킨다.</item>
    /// <item>2 <i>(아레나)</i> — 돌진전사와 마법사가 벽에서 나오는 법을 여기서 배운다.</item>
    /// <item>3 역할군 조합 — 셋을 섞어 맵 전체를 쓰게 만든다.</item>
    /// <item>4 총력전 — 사각지대를 찾아 무적 판정을 쓰게 만든다.</item>
    /// <item>5 <i>(아레나)</i> 보스 — 미니 룸에서 손을 풀고 통로를 지나 보스방에 들어간다.</item>
    /// </list>
    /// </summary>
    public static class StageWaveCatalog
    {
        /// <summary>본편 스테이지 수. 아레나 스테이지를 포함한다.</summary>
        public const int StageCount = 5;

        /// <summary>아레나 · 통로로 도는 스테이지 번호. 이 둘만 웨이브가 아니다.</summary>
        public const int ArenaStageNumber = 2;

        /// <summary>보스 스테이지. 아레나 구조를 그대로 쓰되 마지막 방이 보스방이다.</summary>
        public const int BossStageNumber = 5;

        /// <summary>아레나와 통로로 도는 방인가.</summary>
        public static bool IsArenaStage(int stageNumber)
            => stageNumber == ArenaStageNumber || stageNumber == BossStageNumber;

        /// <summary>이 번호가 웨이브로 도는 방인가.</summary>
        public static bool IsWaveStage(int stageNumber)
            => stageNumber >= 1 && stageNumber <= StageCount && !IsArenaStage(stageNumber);

        /// <summary>전사만 나오는 앞 스테이지의 동시 공격 허용치.</summary>
        public const int EarlyTokens = 2;

        /// <summary>역할군이 섞이는 뒤 스테이지의 허용치. 3을 넘기면 회피가 성립하지 않는다.</summary>
        public const int LateTokens = 3;

        /// <summary>스테이지 주제. 로그와 디렉터가 읽는다.</summary>
        public static string ThemeOf(int stageNumber)
        {
            switch (Clamp(stageNumber))
            {
                case 1:  return "입문 — 기본 조작 & 공격 연계";
                case 2:  return "아레나 — 벽 뒤에서 오는 적";
                case 3:  return "역할군 조합 — 공간 제어";
                case 4:  return "총력전 — 숙련도 시험";
                default: return "보스 — 미니 룸 → 통로 → 보스방";
            }
        }

        /// <summary>
        /// 스테이지 하나의 웨이브 목록. <b>매번 새로 만든다</b> —
        /// <see cref="WaveDefinition"/>은 클래스라 한 벌을 돌려주면 디렉터가 인스펙터에서
        /// 만진 값이 표 원본을 오염시킨다(<c>GameManager.DefaultStages</c>와 같은 이유).
        ///
        /// 아레나 스테이지 번호를 주면 <b>빈 배열</b>이다. 그 방들은 웨이브로 돌지 않는다 —
        /// 조용히 다른 스테이지의 표를 돌려주면 아레나에 웨이브가 겹쳐 나온다.
        /// </summary>
        public static WaveDefinition[] For(int stageNumber)
        {
            int stage = Clamp(stageNumber);
            if (IsArenaStage(stage)) return new WaveDefinition[0];

            switch (stage)
            {
                case 1:  return Stage1();
                case 3:  return Stage3();
                default: return Stage4();
            }
        }

        private static int Clamp(int stageNumber) => Mathf.Clamp(stageNumber, 1, StageCount);

        // ── 1 입문 ──────────────────────────────────────
        // 돌진도 원거리도 없다. 콤보 · 잡기 · 기본 피격만 남기고 변수를 전부 뺀다.

        private static WaveDefinition[] Stage1() => new[]
        {
            new WaveDefinition
            {
                label = "1-1 전사 3기 순차 — 한 명씩 상대하는 법",
                attackTokens = EarlyTokens,
                spawns = new[] { WaveSpawn.Of(EnemyRole.Melee, 3, SpawnSide.Right, 0f, 1.6f) },
            },
            new WaveDefinition
            {
                label = "1-2 전사 4기 양방향 포위 — 등 뒤를 보게 만든다",
                attackTokens = EarlyTokens,
                spawns = new[] { WaveSpawn.Of(EnemyRole.Melee, 4, SpawnSide.Both, 0f, 0.7f) },
            },
            new WaveDefinition
            {
                label = "1-B 강화 전사 1기 + 전사 2기 — 첫 체력 벽",
                attackTokens = EarlyTokens,
                spawns = new[]
                {
                    WaveSpawn.Of(EnemyRole.Melee, 1, SpawnSide.Right, 0f,   0f,   elite: true),
                    WaveSpawn.Of(EnemyRole.Melee, 2, SpawnSide.Both,  1.2f, 0.6f),
                },
            },
        };

        // ── 3 역할군 조합 ────────────────────────────────
        // 전사가 길을 막고 마법사가 구석에서 깎을 때 돌진전사가 라인을 민다.
        // 여기서부터 토큰 3 — 셋이 동시에 압박해야 맵 전체를 쓰게 된다.

        private static WaveDefinition[] Stage3() => new[]
        {
            new WaveDefinition
            {
                label = "3-1 돌진전사 2기 + 마법사 1기 — 피할 곳이 이미 견제당한다",
                attackTokens = LateTokens,
                spawns = new[]
                {
                    WaveSpawn.Of(EnemyRole.Charger, 2, SpawnSide.Both, 1.0f, 1.2f),
                    WaveSpawn.Of(EnemyRole.Ranged,  1, SpawnSide.Right),
                },
            },
            new WaveDefinition
            {
                label = "3-2 전사 3기 + 돌진전사 1기 + 마법사 2기 — 세 위협 동시 대응",
                attackTokens = LateTokens,
                spawns = new[]
                {
                    WaveSpawn.Of(EnemyRole.Melee,   3, SpawnSide.Right, 0f,   0.7f),
                    WaveSpawn.Of(EnemyRole.Ranged,  2, SpawnSide.Both,  0.5f, 0.4f),
                    WaveSpawn.Of(EnemyRole.Charger, 1, SpawnSide.Left,  3.0f),
                },
            },
            new WaveDefinition
            {
                label = "3-3 전사 2기(전방) + 돌진 2기(기습) + 마법사 2기(후방)",
                attackTokens = LateTokens,
                spawns = new[]
                {
                    WaveSpawn.Of(EnemyRole.Melee,   2, SpawnSide.Right, 0f,   0.6f),
                    WaveSpawn.Of(EnemyRole.Ranged,  2, SpawnSide.Both,  0.5f, 0.4f),
                    // 기습은 반대쪽 벽에서 온다. 전사와 붙어 있는 등 뒤가 열린다.
                    WaveSpawn.Of(EnemyRole.Charger, 2, SpawnSide.Left,  3.5f, 1.4f),
                },
            },
        };

        // ── 4 총력전 ─────────────────────────────────────
        // 적의 공격 판정이 겹치지 않는 사각지대를 찾아 메가크래시 · 잡기 무적을 쓰게 만든다.
        // 최종 웨이브의 지속 리젠이 "다 잡고 쉬는" 구간을 없앤다.

        private static WaveDefinition[] Stage4() => new[]
        {
            new WaveDefinition
            {
                label = "4-1 돌진전사 3기 교차 돌진 — 좌우에서 번갈아 들어온다",
                attackTokens = LateTokens,
                spawns = new[] { WaveSpawn.Of(EnemyRole.Charger, 3, SpawnSide.Both, 0.5f, 1.1f) },
            },
            new WaveDefinition
            {
                label = "4-2 마법사 3기 탄막 + 전사 3기 — 안전지대가 사라진다",
                attackTokens = LateTokens,
                spawns = new[]
                {
                    WaveSpawn.Of(EnemyRole.Ranged, 3, SpawnSide.Both,  0f,   0.4f),
                    WaveSpawn.Of(EnemyRole.Melee,  3, SpawnSide.Right, 1.0f, 0.6f),
                },
            },
            new WaveDefinition
            {
                label = "4-3 엘리트 돌진 2기 + 마법사 2기 + 지속 리젠 전사",
                attackTokens = LateTokens,
                spawns = new[]
                {
                    WaveSpawn.Of(EnemyRole.Charger, 2, SpawnSide.Both, 1.0f, 1.6f, elite: true),
                    WaveSpawn.Of(EnemyRole.Ranged,  2, SpawnSide.Both, 0f,   0.4f),
                },
                // 리젠은 "빨리 끝내라"는 압박이다. 상한이 없으면 이길 수 없는 방이 된다.
                reinforceInterval = 7f,
                reinforceRole = EnemyRole.Melee,
                reinforceCap = 4,
            },
        };
    }
}
