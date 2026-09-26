using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 원거리. 사거리 안에서 쏘되 너무 붙으면 물러난다.
    /// 무상태 — 타이머와 타겟은 EnemyControl이 들고 컨텍스트로 넘어온다.
    /// </summary>
    [CreateAssetMenu(menuName = "Prototype/Enemy Brain/Ranged", fileName = "Brain_Ranged")]
    public class RangedBrainAsset : EnemyBrainAsset
    {
        public override EnemyIntent Decide(in EnemyBrainContext ctx)
        {
            if (ctx.target == null) return EnemyIntent.None;

            if (ctx.p.leashRange > 0f && ctx.distance > ctx.p.leashRange)
                return EnemyIntent.None;

            Vector3 dir = ctx.distance > 0.0001f ? ctx.toTarget / ctx.distance : Vector3.zero;

            // 카이팅이 먼저다. 붙은 상태에서 쏘면 근접에게 그대로 물린다.
            if (ctx.p.preferredMinRange > 0f && ctx.distance < ctx.p.preferredMinRange)
                return EnemyIntent.Move(-dir);

            if (ctx.distance <= ctx.p.attackRange)
                return ctx.attackReady ? EnemyIntent.Attack(dir) : EnemyIntent.None;

            return EnemyIntent.Move(dir);
        }
    }
}
