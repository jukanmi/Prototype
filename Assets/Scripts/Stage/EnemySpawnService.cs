using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적 한 기를 실제로 세상에 내놓는 일. 웨이브(<see cref="StageDirector"/>)와
    /// 라운드(<see cref="ArenaDirector"/>)가 같은 창구를 쓴다 —
    /// 소환 순서가 두 벌이 되면 한쪽만 레이어 배선이 빠지는 식으로 조용히 갈라진다.
    /// </summary>
    public class EnemySpawnService : MonoBehaviour
    {
        [Header("적 데이터")]
        [Tooltip("전사. EnemyData.prefab을 소환한다.")]
        [SerializeField] private EnemyData meleeData;
        [Tooltip("돌진전사.")]
        [SerializeField] private EnemyData chargerData;
        [Tooltip("마법사(원거리).")]
        [SerializeField] private EnemyData rangedData;
        [Tooltip("보스. 보스 아레나에서만 쓴다.")]
        [SerializeField] private EnemyData bossData;

        [Header("강화 개체")]
        [Tooltip("elite 표시가 붙은 적의 체력 배율.")]
        [SerializeField] private float eliteHealthScale = 2.5f;
        [Tooltip("elite 표시가 붙은 적의 공격력 배율.")]
        [SerializeField] private float eliteAttackScale = 1.4f;

        /// <summary>
        /// 소환 대기실. <b>비활성</b> 오브젝트다.
        ///
        /// 여기를 거치는 이유는 <see cref="Attack"/>이 Awake 에서 자기 레이어로 충돌 마스크를
        /// 굳혀 버리기 때문이다. 프리팹은 레이어가 Default 인 채로 저장돼 있어서, 활성 상태로
        /// 그냥 Instantiate 하면 Awake 가 먼저 돌아 <b>Default 기준 마스크</b>가 박힌다 —
        /// 그 뒤에 레이어를 고쳐 봐야 이미 늦었고, 결과는 서로를 통과하는 적이다.
        /// 비활성 부모 밑에서 만들면 Awake 가 미뤄지므로, 레이어를 맞춘 뒤 꺼내면서 깨운다.
        /// </summary>
        private Transform nursery;

        private void Awake()
        {
            var holder = new GameObject("SpawnNursery");
            holder.transform.SetParent(transform, false);
            holder.SetActive(false);
            nursery = holder.transform;
        }

        /// <summary>
        /// 적 데이터를 한 번에 꽂는다. 씬 빌더가 부른다.
        ///
        /// <b>SerializedObject를 쓰지 않는 이유</b>: 그쪽은 스크립트가 방금 바뀐 직후에
        /// <c>FindProperty</c>가 새 필드를 못 찾거나, 찾아도 쓴 값이 안 남는 일이 있다.
        /// 증상이 "그 칸만 비어 있다"라 원인을 짚기가 대단히 어렵다 —
        /// 필드에 직접 넣고 <c>SetDirty</c>로 저장시키는 편이 짧고 확실하다.
        /// </summary>
        public void Configure(EnemyData melee, EnemyData charger, EnemyData ranged, EnemyData boss)
        {
            meleeData = melee;
            chargerData = charger;
            rangedData = ranged;
            bossData = boss;
        }

        /// <summary>이 역할의 데이터가 꽂혀 있고 프리팹까지 달려 있는가. 빌더의 검증이 읽는다.</summary>
        public bool CanSpawn(EnemyRole role)
        {
            EnemyData data = DataFor(role);
            return data != null && data.prefab != null;
        }

        public EnemyData DataFor(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Charger: return chargerData;
                case EnemyRole.Ranged:  return rangedData;
                case EnemyRole.Boss:    return bossData;
                default:                return meleeData;
            }
        }

        /// <summary>
        /// 벽 뒤에서 걸어 나오는 소환(아레나). 판정을 끈 채 시작해
        /// 목표 셀에 닿으면 스스로 원상복구한다.
        /// </summary>
        public Enemy SpawnFromWall(EnemyRole role, bool elite, in ArenaSpawnPlan plan)
        {
            Enemy enemy = Create(role, elite, plan.spawnPoint);
            if (enemy == null) return null;

            // Arm 이 BeginSpawnEntry 보다 먼저다. 순서가 뒤집히면 첫 프레임에 판정이 켜진 채로
            // 벽 안쪽에 서 있게 되어, 그 한 프레임 동안 광역기에 맞는다.
            enemy.gameObject.AddComponent<EnemySpawnGuard>().Arm(plan.wall, plan.entryLine);

            Enter(enemy, plan.entryPoint, plan.holdSeconds);
            return enemy;
        }

        /// <summary>
        /// 방 가장자리에서 걸어 들어오는 소환(웨이브). 벽 연출이 없으므로 판정은 처음부터 켜져 있다.
        /// </summary>
        public Enemy SpawnAtEdge(EnemyRole role, bool elite, in SpawnPlacement place)
        {
            Enemy enemy = Create(role, elite, place.spawnPoint);
            if (enemy == null) return null;

            Enter(enemy, place.entryPoint, place.holdSeconds);
            return enemy;
        }

        // ── 공통 ────────────────────────────────────────

        private Enemy Create(EnemyRole role, bool elite, Vector3 spawnPoint)
        {
            EnemyData data = DataFor(role);
            if (data == null)
            {
                // 경고가 아니라 에러다. 이건 저작 실수가 아니라 <b>배선이 빠진 씬</b>이고,
                // 증상은 "그 적이 영영 안 나온다"라 화면에 단서가 하나도 없다.
                Debug.LogError($"[EnemySpawnService] {name}: {role} 의 EnemyData가 안 꽂혀 있다. " +
                               "씬을 다시 구울 것.", this);
                return null;
            }

            if (data.prefab == null)
            {
                Debug.LogError($"[EnemySpawnService] {name}: EnemyData '{data.name}'에 prefab이 없다. " +
                               "프리팹 빌더를 먼저 돌릴 것.", this);
                return null;
            }

            // 대기실(비활성)에서 만든다 — 레이어를 맞추기 전에 Awake 가 돌면 안 된다.
            GameObject go = Instantiate(data.prefab, nursery);
            go.name = elite ? $"{data.enemyId}_강화" : data.enemyId;
            go.transform.position = spawnPoint;
            go.transform.rotation = Quaternion.identity;

            EnemyLayers.Apply(go);

            // 부모에서 꺼내는 순간 활성화되고 Awake · Start 가 돈다. 위치는 그대로 둔다.
            go.transform.SetParent(null, worldPositionStays: true);

            var enemy = go.GetComponent<Enemy>();
            if (enemy == null)
            {
                BattleLog.Warn(LogCategory.State, $"{name}: '{data.prefab.name}'에 Enemy가 없다.", this);
                Destroy(go);
                return null;
            }

            enemy.ApplyData(data);
            if (elite) enemy.ApplyEliteScale(eliteHealthScale, eliteAttackScale);

            return enemy;
        }

        private static void Enter(Enemy enemy, Vector3 entryPoint, float hold)
        {
            var control = enemy.GetComponent<EnemyControl>();
            if (control != null) control.BeginSpawnEntry(entryPoint, hold);
        }
    }
}
