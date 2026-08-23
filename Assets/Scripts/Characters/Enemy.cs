using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적. 아군을 추적하고 공격한다.
    /// 스테이지가 끝나면 AI가 정지한다 — 종료 후에도 계속 도는 문제 차단.
    /// </summary>
    public class Enemy : Entity
    {
        [SerializeField] private EnemyData data;

        private EnemyControl enemyControl;

        public EnemyData Data => data;

        public override Faction Faction => Faction.Enemy;

        protected override void Awake()
        {
            base.Awake();
            enemyControl = Control as EnemyControl;
            ApplyData(data);

            // 상태 색은 모든 적에게 붙어야 한다. 프리팹이 두 형태로 갈려 있어
            // 배선으로 보장하면 한쪽이 조용히 빠진다 — 코드로 붙인다.
            if (GetComponent<EnemyStateTint>() == null)
                gameObject.AddComponent<EnemyStateTint>();
        }

        protected override void Start()
        {
            base.Start();
            BattleRegistry.RegisterEnemy(this);
        }

        /// <summary>
        /// 수치 테이블을 실제 컴포넌트에 밀어 넣는다. Awake가 부르고,
        /// 에디터 생성기도 같은 경로를 쓴다 — 주입 규칙이 두 벌이 되지 않게.
        /// </summary>
        public void ApplyData(EnemyData source)
        {
            data = source;
            if (data == null) return;

            Stats.Set(StatType.AttackPower, data.atk);
            Stats.Set(StatType.MoveSpeed, data.moveSpeed);   // MoveState가 읽는다
            Combat.SetMaxHealth(data.hp);

            // 투사체가 없는 데이터는 근접 그대로 둔다. null로 덮으면 프리팹 설정이 지워진다.
            if (data.basicProjectile != null)
                ConfigureBasicProjectile(data.basicProjectile, data.projectileSpeed,
                                         data.projectileRange, data.projectilePierce);

            if (enemyControl != null) enemyControl.ApplyData(data);
        }

        /// <summary>
        /// 강화 개체로 만든다. <see cref="ApplyData"/> <b>다음에</b> 불러야 한다 —
        /// ApplyData 가 표의 원본값으로 덮어쓰기 때문이다.
        ///
        /// 몸집은 건드리지 않는다. 깊이 배율(<see cref="BeltScrollView"/>)이 매 프레임
        /// localScale 을 다시 쓰므로 여기서 키우면 그대로 지워지고, 히트박스만 어긋난다.
        /// 강화 개체는 이름과 체력 · 공격력으로 구분한다.
        /// </summary>
        public void ApplyEliteScale(float healthScale, float attackScale)
        {
            float hp = Mathf.Max(1f, healthScale);
            float atk = Mathf.Max(1f, attackScale);

            float baseHp = data != null ? data.hp : Combat.Health.MaxValue;
            float baseAtk = data != null ? data.atk : Stats.GetValue(StatType.AttackPower, 10f);

            Combat.SetMaxHealth(baseHp * hp);
            Stats.Set(StatType.AttackPower, baseAtk * atk);

            BattleLog.Log(LogCategory.Combat,
                $"{name} 강화 개체 — 체력 x{hp:0.##}, 공격력 x{atk:0.##}", this);
        }

        /// <summary>스테이지 종료 시 호출. AI와 전투 입력을 모두 멈춘다.</summary>
        public void StopAI()
        {
            BattleLog.Log(LogCategory.Combat, $"{name} AI 정지 (스테이지 종료)", this);
            if (enemyControl != null) enemyControl.SetActive(false);
        }

        private void OnDestroy()
        {
            BattleRegistry.Unregister(this);
        }
    }
}
