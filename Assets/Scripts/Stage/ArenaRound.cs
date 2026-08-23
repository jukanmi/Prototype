using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적이 튀어나오는 벽. 아레나는 사방이 막혀 있으므로 방향은 넷뿐이다.
    ///
    /// <b>난이도는 마릿수가 아니라 방향으로 올린다.</b> 몹을 늘리면 그냥 오래 걸리지만,
    /// 방향을 늘리면 "재배치 개입이 의미가 있는가"라는 질문이 라운드마다 강해진다.
    /// </summary>
    public enum SpawnWall
    {
        /// <summary>정면(깊이 +Z). 화면 위쪽 뒷벽이다.</summary>
        Front,

        /// <summary>왼쪽(-X).</summary>
        Left,

        /// <summary>오른쪽(+X).</summary>
        Right,

        /// <summary>후방(깊이 -Z). 화면 아래, 플레이어 등 뒤다.</summary>
        Back,
    }

    /// <summary>라운드 안의 한 묶음. "돌진전사 2기를 왼쪽 벽에서" 같은 한 줄이다.</summary>
    [Serializable]
    public struct RoundSpawn
    {
        public EnemyRole role;

        [Min(1)] public int count;

        public SpawnWall wall;

        [Tooltip("라운드가 시작되고 이 묶음의 예고가 뜨기까지의 시간.")]
        [Min(0f)] public float delay;

        [Tooltip("강화 개체.")]
        public bool elite;

        public int Count => Mathf.Max(1, count);

        public static RoundSpawn Of(EnemyRole role, int count, SpawnWall wall,
                                    float delay = 0f, bool elite = false)
            => new RoundSpawn { role = role, count = count, wall = wall, delay = delay, elite = elite };
    }

    /// <summary>
    /// 아레나 하나에서 도는 라운드.
    ///
    /// 사이클은 <b>진입 → 락 → 예고 → 스폰 → 전투 → 클리어 판정 → 언락</b>이다.
    /// (정비 구간은 아직 비어 있다 — 동료 소환·재배치가 들어갈 자리다.)
    /// </summary>
    [Serializable]
    public class ArenaRound
    {
        [Tooltip("로그에 찍히는 이름. 이 라운드로 무엇을 검증하는지 한 줄로 적어 둔다.")]
        public string label = "";

        public RoundSpawn[] spawns = new RoundSpawn[0];

        [Tooltip("동시에 공격을 시도할 수 있는 적의 수. 2~3을 권장한다.")]
        [Range(1, 6)] public int attackTokens = 2;

        /// <summary>이 라운드에 나오는 총 마릿수.</summary>
        public int TotalSpawnCount
        {
            get
            {
                int n = 0;
                if (spawns != null)
                    for (int i = 0; i < spawns.Length; i++) n += spawns[i].Count;
                return n;
            }
        }

        public int CountOf(EnemyRole role)
        {
            int n = 0;
            if (spawns != null)
                for (int i = 0; i < spawns.Length; i++)
                    if (spawns[i].role == role) n += spawns[i].Count;
            return n;
        }

        /// <summary>쓰이는 벽의 가짓수. 라운드가 진행될수록 늘어나야 한다.</summary>
        public int WallCount
        {
            get
            {
                if (spawns == null) return 0;

                int mask = 0;
                for (int i = 0; i < spawns.Length; i++) mask |= 1 << (int)spawns[i].wall;

                int n = 0;
                while (mask != 0) { n += mask & 1; mask >>= 1; }
                return n;
            }
        }

        public bool UsesWall(SpawnWall wall)
        {
            if (spawns == null) return false;
            for (int i = 0; i < spawns.Length; i++)
                if (spawns[i].wall == wall) return true;
            return false;
        }
    }
}
