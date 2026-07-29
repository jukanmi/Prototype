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
            ApplyData();
        }

        protected override void Start()
        {
            base.Start();
            BattleRegistry.RegisterEnemy(this);
        }

        private void ApplyData()
        {
            if (data == null) return;

            Stats.Set(StatType.AttackPower, data.atk);
            Combat.SetMaxHealth(data.hp);
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
