using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 돌진. 어중간한 거리에서 직선으로 밀고 들어오고, 붙으면 평타로 전환한다.
    /// 무상태 — 예고·이동·후딜 실행은 <see cref="EnemyChargeAction"/>이 맡는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Prototype/Enemy Brain/Charger", fileName = "Brain_Charger")]
    public class ChargerBrainAsset : EnemyBrainAsset
    {
        public override EnemyIntent Decide(in EnemyBrainContext ctx)
        {
            if (ctx.target == null) return EnemyIntent.None;

            if (ctx.p.leashRange > 0f && ctx.distance > ctx.p.leashRange)
                return EnemyIntent.None;

            Vector3 dir = ctx.distance > 0.0001f ? ctx.toTarget / ctx.distance : Vector3.zero;

            // 붙어 있으면 돌진할 거리가 없다. 평타가 맞다.
            if (ctx.distance <= ctx.p.attackRange)
                return ctx.attackReady ? EnemyIntent.Attack(dir) : EnemyIntent.None;

            // 돌진은 실행기가 가진 유일한 패턴이라 0번이다.
            // 쿨은 안 실어 보낸다 — EnemyControl의 specialInterval을 그대로 쓴다.
            if (ctx.specialReady && ctx.p.specialRange > 0f && ctx.distance <= ctx.p.specialRange)
                return EnemyIntent.Special(0, dir);

            return EnemyIntent.Move(dir);
        }
    }
}
