namespace Prototype
{
    /// <summary>
    /// 적이 죽었을 때 경험치를 누구에게 얼마나 주는지. 정책을 한 곳에 모아 둔다 —
    /// <see cref="Enemy"/>는 "죽었다"만 알리고 계산은 여기서 한다.
    /// </summary>
    public static class ExpRewards
    {
        /// <summary>
        /// 적 한 기의 사망 보상. <see cref="EnemyData.exp"/>가 0인 개체(훈련장 더미)는 그냥 넘어간다.
        /// </summary>
        public static void Award(Enemy enemy)
        {
            if (enemy == null || enemy.Data == null) return;

            RunProgression run = RunProgression.Current;

            int amount = enemy.Data.exp;
            if (amount <= 0) return;

            run.AddExp(amount);

            BattleLog.Log(LogCategory.State,
                $"{enemy.name} 처치 — 경험치 +{amount} (누적 {run.Exp}, Lv.{run.Level})", enemy);
        }
    }
}
