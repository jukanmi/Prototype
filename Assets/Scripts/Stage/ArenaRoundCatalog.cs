using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 아레나 스테이지의 라운드 배치표. 스테이지마다 아레나가 여럿이라 <b>(스테이지, 아레나)</b>로 찾는다.
    ///
    /// <b>라운드마다 스폰 방향을 늘린다.</b> 마릿수를 늘리면 같은 싸움이 길어질 뿐이지만,
    /// 방향을 늘리면 "재배치 개입이 의미가 있는가"라는 검증 질문이 라운드가 진행될수록
    /// 강하게 압박받는다. 같은 난이도 상승에서 이쪽이 훨씬 재밌다.
    ///
    /// <list type="bullet">
    /// <item><b>2스테이지</b> — 1R 정면 하나 → 2R 좌우 양쪽. 시너지 붕괴 검증을 마지막에 몰아 둔다.</item>
    /// <item><b>5스테이지(보스)</b> — 미니 룸에서 몸을 풀고 통로를 지나 보스방에 들어간다.
    /// 구성은 기존 <c>Stage_Mini</c> · <c>Stage_Boss</c>를 그대로 옮긴 것이다.</item>
    /// </list>
    /// </summary>
    public static class ArenaRoundCatalog
    {
        public const int EarlyTokens = StageWaveCatalog.EarlyTokens;
        public const int LateTokens = StageWaveCatalog.LateTokens;

        /// <summary>이 스테이지에 아레나가 몇 개인가. 아레나 스테이지가 아니면 0.</summary>
        public static int ArenaCountOf(int stageNumber)
            => StageWaveCatalog.IsArenaStage(stageNumber) ? 2 : 0;

        /// <summary>아레나 번호(1부터). 범위 밖은 양끝으로 물린다.</summary>
        public static ArenaRound For(int stageNumber, int arenaNumber)
        {
            int arena = Clamp(arenaNumber);

            if (stageNumber == StageWaveCatalog.BossStageNumber)
                return arena == 1 ? BossWarmup() : BossRound();

            return arena == 1 ? Round1() : Round2();
        }

        /// <summary>아레나 주제. 로그가 읽는다.</summary>
        public static string ThemeOf(int stageNumber, int arenaNumber)
        {
            int arena = Clamp(arenaNumber);

            if (stageNumber == StageWaveCatalog.BossStageNumber)
                return arena == 1 ? "몸풀기 — 미니 룸" : "보스전";

            return arena == 1
                ? "돌진 대응 — 정면 한 방향"
                : "원거리 회피 & 시너지 붕괴 — 좌우 양방향";
        }

        private static int Clamp(int arenaNumber) => Mathf.Clamp(arenaNumber, 1, 2);

        // ── 2스테이지 1라운드: 정면 벽 하나 ──────────────
        // 방향이 하나뿐이라 "어디서 나오는가"를 배우는 데 온전히 집중할 수 있다.
        // 돌진전사가 정면에서 곧장 내려오므로 깊이(Z) 회피가 그대로 답이 된다.

        private static ArenaRound Round1() => new ArenaRound
        {
            label = "1R 정면 벽 — 전사 3 + 돌진전사 2",
            attackTokens = EarlyTokens,
            spawns = new[]
            {
                RoundSpawn.Of(EnemyRole.Melee,   3, SpawnWall.Front),
                RoundSpawn.Of(EnemyRole.Charger, 2, SpawnWall.Front, delay: 3.0f),
            },
        };

        // ── 2스테이지 2라운드: 좌우 양쪽 ─────────────────
        // 마법사를 좌우 벽에 하나씩 붙여 두면 어느 쪽을 보든 반대쪽에서 견제받는다.
        // 여기가 시너지 붕괴 검증 자리다 — 동료를 잃은 채로 이 구성을 상대할 수 있는가.

        private static ArenaRound Round2() => new ArenaRound
        {
            label = "2R 좌우 양방향 — 마법사 2 + 전사 3 + 돌진전사 1",
            attackTokens = LateTokens,
            spawns = new[]
            {
                RoundSpawn.Of(EnemyRole.Ranged,  1, SpawnWall.Left),
                RoundSpawn.Of(EnemyRole.Ranged,  1, SpawnWall.Right),
                RoundSpawn.Of(EnemyRole.Melee,   2, SpawnWall.Left,  delay: 1.2f),
                RoundSpawn.Of(EnemyRole.Melee,   1, SpawnWall.Right, delay: 1.2f),
                RoundSpawn.Of(EnemyRole.Charger, 1, SpawnWall.Right, delay: 4.0f),
            },
        };

        // ── 보스 스테이지 1라운드: 미니 룸 ───────────────
        // 기존 Stage_Mini 를 그대로 옮겼다 — 적 하나. 보스 앞에서 손을 푸는 자리이지
        // 소모전을 시키는 자리가 아니다. 여기서 체력을 깎아 두면 보스전이 운이 된다.

        private static ArenaRound BossWarmup() => new ArenaRound
        {
            label = "미니 룸 — 전사 1기",
            attackTokens = EarlyTokens,
            spawns = new[] { RoundSpawn.Of(EnemyRole.Melee, 1, SpawnWall.Front) },
        };

        // ── 보스 스테이지 2라운드: 보스방 ────────────────
        // 기존 Stage_Boss 그대로 — 보스 한 기뿐이다. 호위를 붙이지 않는 것은 의도다.
        // 보스는 패턴을 여럿 들고 있어서, 잡몹이 섞이면 어느 예고가 누구 것인지 안 읽힌다.

        private static ArenaRound BossRound() => new ArenaRound
        {
            label = "보스전 — 전사 보스 1기",
            attackTokens = EarlyTokens,
            spawns = new[] { RoundSpawn.Of(EnemyRole.Boss, 1, SpawnWall.Front, delay: 0.6f) },
        };
    }
}
