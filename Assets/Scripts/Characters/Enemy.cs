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
