using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 근접. 타겟에게 붙어 사거리 안에서 평타를 낸다.
    /// 무상태 — 타이머와 타겟은 EnemyControl이 들고 컨텍스트로 넘어온다.
    /// </summary>
    [CreateAssetMenu(menuName = "Prototype/Enemy Brain/Melee", fileName = "Brain_Melee")]
    public class MeleeBrainAsset : EnemyBrainAsset
    {
        public override EnemyIntent Decide(in EnemyBrainContext ctx)
        {
            if (ctx.target == null) return EnemyIntent.None;

            // leash 0은 무제한. 스포너·방 경계가 붙으면 여기서 추격을 끊는다.
            if (ctx.p.leashRange > 0f && ctx.distance > ctx.p.leashRange)
                return EnemyIntent.None;

            // distance를 이미 받았으므로 normalized(sqrt 재계산) 대신 나눈다.
            Vector3 dir = ctx.distance > 0.0001f ? ctx.toTarget / ctx.distance : Vector3.zero;

            if (ctx.distance <= ctx.p.attackRange)
                return ctx.attackReady ? EnemyIntent.Attack(dir) : EnemyIntent.None;

            return EnemyIntent.Move(dir);
        }
    }
}
