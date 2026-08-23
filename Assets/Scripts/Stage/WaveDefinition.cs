using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적의 <b>역할</b>. 종류가 아니라 역할이다 — 배치를 짤 때 우리가 실제로 고르는 축이고,
    /// <see cref="WaveSpawnPlanner"/>가 등장 자리를 이 값 하나로 정한다.
    ///
    /// <list type="bullet">
    /// <item><see cref="Melee"/> 전사 — 전방에서 이동과 타격을 받아내며 난전을 만든다.</item>
    /// <item><see cref="Charger"/> 돌진전사 — X축 직선 돌진으로 콤보를 끊고 깊이(Z) 회피를 강제한다.</item>
    /// <item><see cref="Ranged"/> 마법사 — 상하단 구석에서 투사체로 안전지대를 깎는다.</item>
    /// <item><see cref="Boss"/> 보스 — 패턴을 여럿 든 단일 개체. 방 하나를 통째로 차지한다.</item>
    /// </list>
    /// </summary>
    public enum EnemyRole
    {
        Melee,
        Charger,
        Ranged,

        /// <summary>
        /// 보스. 웨이브 표에는 쓰지 않는다 — 잡몹 자리에 섞어 놓으면 동시 등장 규칙이
        /// 그대로 적용되어 보스가 둘씩 나오는 표를 실수로 만들 수 있다.
        /// 아레나 라운드에서 한 기만 쓴다.
        /// </summary>
        Boss,
    }

    /// <summary>
    /// 어느 쪽 벽에서 들어오는가. <see cref="Both"/>는 <b>번갈아</b>다 —
    /// 같은 묶음의 짝수 번째는 오른쪽, 홀수 번째는 왼쪽으로 갈라진다(양방향 포위 · 교차 돌진).
    /// </summary>
    public enum SpawnSide
    {
        Right,
        Left,
        Both,
    }

    /// <summary>
    /// 웨이브 안의 한 묶음. "돌진전사 2기를 1.5초 시차로 왼쪽에서" 같은 한 줄이다.
    /// </summary>
    [Serializable]
    public struct WaveSpawn
    {
        public EnemyRole role;

        [Tooltip("이 묶음의 마릿수.")]
        [Min(1)] public int count;

        public SpawnSide side;

        [Tooltip("웨이브가 시작되고 첫 기가 나타나기까지의 시간.")]
        [Min(0f)] public float delay;

        [Tooltip("한 기씩 벌리는 간격. 0이면 전부 동시에 나온다.")]
        [Min(0f)] public float interval;

        [Tooltip("강화 개체. 체력·공격력이 배로 오르고 이름에 (강화)가 붙는다.")]
        public bool elite;

        /// <summary>0이나 음수로 저작된 마릿수는 1로 본다 — 아무도 안 나오는 묶음은 실수다.</summary>
        public int Count => Mathf.Max(1, count);

        /// <summary>이 묶음의 <paramref name="index"/>번째가 나타나는 시각(웨이브 시작 기준).</summary>
        public float AppearAt(int index) => Mathf.Max(0f, delay) + Mathf.Max(0f, interval) * Mathf.Max(0, index);

        /// <summary>마지막 한 기가 나타나는 시각. 디렉터가 "다 나왔는가"를 여기서 안다.</summary>
        public float LastAppearAt => AppearAt(Count - 1);

        public static WaveSpawn Of(EnemyRole role, int count,
                                   SpawnSide side = SpawnSide.Right,
                                   float delay = 0f, float interval = 0f, bool elite = false)
            => new WaveSpawn
            {
                role = role,
                count = count,
                side = side,
                delay = delay,
                interval = interval,
                elite = elite,
            };
    }

    /// <summary>
    /// 웨이브 하나. <b>전멸시켜야 다음으로 넘어간다</b> — 판정은 <see cref="StageDirector"/>가 한다.
    ///
    /// <see cref="attackTokens"/>가 이 프로젝트의 다구리 방지책이다. 한 화면에 적이 여섯이어도
    /// 동시에 공격을 <b>시도</b>할 수 있는 적은 이 수까지고, 나머지는 사거리 안에서 기다린다.
    /// 토큰이 없으면 6기가 동시에 휘둘러 회피가 성립하지 않는 구간이 생긴다.
    /// </summary>
    [Serializable]
    public class WaveDefinition
    {
        [Tooltip("로그에 찍히는 이름. 배치 의도를 한 줄로 적어 둔다.")]
        public string label = "";

        public WaveSpawn[] spawns = new WaveSpawn[0];

        [Tooltip("동시에 공격을 시도할 수 있는 적의 수. 2~3을 권장한다.")]
        [Range(1, 6)] public int attackTokens = 2;

        [Header("지속 리젠 (최종 웨이브용)")]
        [Tooltip("0보다 크면 이 간격마다 증원이 한 기씩 들어온다.")]
        [Min(0f)] public float reinforceInterval;

        public EnemyRole reinforceRole = EnemyRole.Melee;

        [Tooltip("증원 총량 상한. 0이면 증원하지 않는다.")]
        [Min(0)] public int reinforceCap;

        /// <summary>웨이브 시작 시 예약되는 총 마릿수. 증원은 세지 않는다 — 그건 나중에 결정된다.</summary>
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

        /// <summary>이 웨이브에 지속 리젠이 붙어 있는가.</summary>
        public bool HasReinforcements => reinforceInterval > 0f && reinforceCap > 0;

        /// <summary>역할별 마릿수. 배치 검사가 표를 그대로 읽을 수 있게 연다.</summary>
        public int CountOf(EnemyRole role)
        {
            int n = 0;
            if (spawns != null)
                for (int i = 0; i < spawns.Length; i++)
                    if (spawns[i].role == role) n += spawns[i].Count;
            return n;
        }
    }
}
