// 웨이브 하나 = 애셋 하나.
//
// 웨이브 배치의 <b>원본</b>이다. 애셋이라 이름과 GUID 를 갖고, 씬의 보드가 그걸 목록으로 든다.
// 스폰 지점만은 애셋이 들 수 없어서(ScriptableObject 는 씬 오브젝트 참조를 저장할 때
// 유니티가 null 로 지운다) 이름으로만 부른다.
//
// 계획서: docs/Wave_Authoring_Refactor_Plan.md (2.1)

using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 웨이브 하나의 저작물.
    ///
    /// <b>디렉터는 이 애셋을 읽기만 한다.</b> 씬 보드가 참조로 들고 있어서 런타임에 값을 고치면
    /// 디스크의 애셋이 그대로 더러워지고, 다음 판에도 그 값으로 돈다.
    ///
    /// <see cref="attackTokens"/>가 이 프로젝트의 다구리 방지책이다. 한 화면에 적이 여섯이어도
    /// 동시에 공격을 <b>시도</b>할 수 있는 적은 이 수까지고, 나머지는 사거리 안에서 기다린다.
    /// </summary>
    [CreateAssetMenu(fileName = "Wave", menuName = "Prototype/웨이브", order = 20)]
    public class WaveAsset : ScriptableObject
    {
        [Tooltip("로그에 찍히는 이름. 배치 의도를 한 줄로 적어 둔다.")]
        public string label = "";

        [Tooltip("다음 웨이브가 열리는 조건.")]
        public WaveAdvance advance = WaveAdvance.AllCleared;

        [Tooltip("동시에 공격을 시도할 수 있는 적의 수. 2~3을 권장한다.")]
        [Range(1, 6)] public int attackTokens = 2;

        [Tooltip("한 줄이 적 한 기다.")]
        public WaveSpawnEntry[] spawns = new WaveSpawnEntry[0];

        [Header("지속 리젠 (최종 웨이브용)")]
        [Tooltip("0보다 크면 이 간격마다 증원이 한 기씩 들어온다.")]
        [Min(0f)] public float reinforceInterval;

        public EnemyRole reinforceRole = EnemyRole.Melee;

        [Tooltip("증원 총량 상한. 0이면 증원하지 않는다.")]
        [Min(0)] public int reinforceCap;

        // ── 읽는 쪽이 보는 것 ───────────────────────────

        /// <summary>
        /// 실제로 적용할 조건. 미구현 조건이 애셋에 박혀 있어도 전멸로 접어서
        /// <b>스테이지가 안 끝나는 상태를 만들지 않는다.</b>
        /// </summary>
        public WaveAdvance Advance => WaveAdvanceRules.Resolve(advance);

        /// <summary>웨이브 시작 시 예약되는 총 마릿수. 증원은 세지 않는다.</summary>
        public int TotalSpawnCount => spawns != null ? spawns.Length : 0;

        /// <summary>이 웨이브에 지속 리젠이 붙어 있는가.</summary>
        public bool HasReinforcements => reinforceInterval > 0f && reinforceCap > 0;

        /// <summary>동시 공격 허용치를 1~6으로 물린다. 저작이 0이어도 아무도 못 때리면 안 된다.</summary>
        public int AttackTokens => Mathf.Clamp(attackTokens, 1, 6);

        /// <summary>역할별 마릿수. 배치 검사가 애셋을 그대로 읽을 수 있게 연다.</summary>
        public int CountOf(EnemyRole role)
        {
            int n = 0;
            if (spawns != null)
                for (int i = 0; i < spawns.Length; i++)
                    if (spawns[i].role == role) n++;
            return n;
        }

        /// <summary>
        /// 쓰이는 벽의 가짓수. <b>아레나는 난이도를 마릿수가 아니라 방향으로 올린다</b> —
        /// 몹을 늘리면 그냥 오래 걸리지만, 방향을 늘리면 "재배치 개입이 의미가 있는가"라는
        /// 질문이 라운드마다 강해진다.
        /// </summary>
        public int WallCount
        {
            get
            {
                if (spawns == null) return 0;

                int mask = 0;
                for (int i = 0; i < spawns.Length; i++)
                    if (spawns[i].motion == SpawnMotion.FromWall) mask |= 1 << (int)spawns[i].wall;

                int n = 0;
                while (mask != 0) { n += mask & 1; mask >>= 1; }
                return n;
            }
        }

        /// <summary>이 벽을 쓰는 줄이 있는가.</summary>
        public bool UsesWall(SpawnWall wall)
        {
            if (spawns == null) return false;

            for (int i = 0; i < spawns.Length; i++)
                if (spawns[i].motion == SpawnMotion.FromWall && spawns[i].wall == wall) return true;

            return false;
        }

        /// <summary>
        /// 이 웨이브가 부르는 스폰 지점 이름들. 중복은 뺀다.
        /// 씬 보드에 그 이름이 다 있는지 검사하는 쪽이 읽는다 —
        /// 이름 대조는 컴파일에 안 걸려서, 누가 훑어 주지 않으면 실행할 때까지 모른다.
        /// </summary>
        public List<string> PointIds()
        {
            var ids = new List<string>();
            if (spawns == null) return ids;

            for (int i = 0; i < spawns.Length; i++)
            {
                if (!spawns[i].UsesPoint) continue;

                string id = spawns[i].pointId.Trim();
                if (!ids.Contains(id)) ids.Add(id);
            }

            return ids;
        }
    }
}
