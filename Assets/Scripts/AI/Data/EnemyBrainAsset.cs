using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// ScriptableObject로 만드는 브레인의 공통 베이스.
    /// SerializeReference 대신 애셋을 쓰는 이유: GUID 참조라 클래스 이름을 바꿔도 안 끊기고,
    /// 인스펙터 드래그&드롭이 기본으로 되고, 수치만 다른 변종을 애셋으로 찍을 수 있다.
    /// </summary>
    public abstract class EnemyBrainAsset : ScriptableObject, IEnemyBrain
    {
        public abstract EnemyIntent Decide(in EnemyBrainContext ctx);

        /// <summary>
        /// 모든 판단의 공통 서두. 대상이 없거나 목줄(<c>leashRange</c>, 0은 무제한) 밖이면 false —
        /// 아무것도 안 한다. 아니면 대상 쪽 단위 방향을 준다.
        /// </summary>
        protected static bool TryAim(in EnemyBrainContext ctx, out Vector3 dir)
        {
            dir = Vector3.zero;
            if (ctx.target == null) return false;
            if (ctx.p.leashRange > 0f && ctx.distance > ctx.p.leashRange) return false;

            // distance를 이미 받았으므로 normalized(sqrt 재계산) 대신 나눈다.
            if (ctx.distance > 0.0001f) dir = ctx.toTarget / ctx.distance;
            return true;
        }
    }
}
