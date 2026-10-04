using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 보통 적의 판단. 근접 · 원거리 · 돌진이 <b>이 하나</b>를 쓰고, 차이는 적 데이터의 수치가 만든다.
    /// <list type="bullet">
    /// <item><c>preferredMinRange</c> &gt; 0 — 너무 붙으면 물러난다(원거리).</item>
    /// <item><c>specialRange</c> &gt; 0 — 그 거리 안이면 특수 행동을 쓴다(돌진).</item>
    /// <item>둘 다 0 — 붙어서 평타만 친다(근접).</item>
    /// </list>
    /// 예전에는 이 셋이 브레인 클래스 셋이었는데, 차이가 위 두 수치의 0 여부뿐이라 합쳤다.
    /// 무상태 — 타이머와 타겟은 EnemyControl이 들고 컨텍스트로 넘어온다.
    /// </summary>
    [CreateAssetMenu(menuName = "Prototype/Enemy Brain/Standard", fileName = "Brain_Standard")]
    public class StandardBrainAsset : EnemyBrainAsset
    {
        public override EnemyIntent Decide(in EnemyBrainContext ctx)
        {
            if (!TryAim(in ctx, out Vector3 dir)) return EnemyIntent.None;

            // 카이팅이 먼저다. 붙은 상태에서 쏘면 근접에게 그대로 물린다.
            if (ctx.p.preferredMinRange > 0f && ctx.distance < ctx.p.preferredMinRange)
                return EnemyIntent.Move(-dir);

            // 붙어 있으면 돌진할 거리가 없다. 평타가 맞다.
            if (ctx.distance <= ctx.p.attackRange)
            {
                // 원거리는 Z 줄이 맞아야 쏜다. 사거리만 보면 옆 줄에서 허공에 쏜다 — 줄부터 맞춘다.
                if (ctx.IsRanged && !ctx.LinedUp)
                    return EnemyIntent.Move(new Vector3(0f, 0f, Mathf.Sign(ctx.toTarget.z)));

                return ctx.attackReady ? EnemyIntent.Attack(dir) : EnemyIntent.None;
            }

            // 특수 행동은 실행기(EnemyPatternAction)가 가진 0번 하나다.
            // 쿨은 안 실어 보낸다 — EnemyControl의 specialInterval을 그대로 쓴다.
            if (ctx.specialReady && ctx.p.specialRange > 0f && ctx.distance <= ctx.p.specialRange)
                return EnemyIntent.Special(0, dir);

            return EnemyIntent.Move(dir);
        }
    }
}
