using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 브레인에 넘기는 수치. EnemyData에서 오거나, 없으면 EnemyControl의 인스펙터 폴백값.
    /// 브레인 애셋이 특정 EnemyData를 참조하면 여러 적이 공유할 수 없으므로 struct로 끊는다.
    /// </summary>
    [Serializable]
    public struct EnemyBrainParams
    {
        [Tooltip("이 거리 안이면 공격한다.")]
        public float attackRange;

        [Tooltip("타겟이 이보다 멀면 포기한다. 0이면 무제한.")]
        public float leashRange;
    }

    /// <summary>브레인이 판단에 쓰는 입력. EnemyControl이 매 프레임 채운다.</summary>
    public struct EnemyBrainContext
    {
        public Entity self;

        /// <summary>없으면 null. 브레인이 먼저 검사해야 한다.</summary>
        public Entity target;

        /// <summary>타겟까지의 벡터. XZ 평면이고 정규화 전.</summary>
        public Vector3 toTarget;

        /// <summary>toTarget.magnitude. 브레인이 다시 sqrt를 돌리지 않도록 미리 넣어 준다.</summary>
        public float distance;

        /// <summary>공격 쿨이 끝났는지. 쿨 관리는 EnemyControl이 한다.</summary>
        public bool attackReady;

        public EnemyBrainParams p;
        public float dt;
    }

    /// <summary>브레인이 내는 결론. EnemyControl이 그대로 Control 프로퍼티로 옮긴다.</summary>
    public struct EnemyIntent
    {
        public Command command;
        public Vector3 moveDirection;

        public static EnemyIntent None => default;

        public static EnemyIntent Move(Vector3 dir)
            => new EnemyIntent { command = Command.Move, moveDirection = dir };

        /// <summary>
        /// face는 AttackState.Enter가 Physics.Face에 쓴다.
        /// 비우면 Facing이 갱신되지 않아 마지막 이동 방향으로 헛휘두른다.
        /// </summary>
        public static EnemyIntent Attack(Vector3 face)
            => new EnemyIntent { command = Command.Attack, moveDirection = face };
    }

    /// <summary>
    /// 적 종류별 판단 로직. <b>무상태여야 한다</b> — 애셋 하나를 여러 적이 공유한다.
    /// 타이머·타겟이 필요하면 EnemyControl에 두고 컨텍스트로 받는다.
    /// </summary>
    public interface IEnemyBrain
    {
        EnemyIntent Decide(in EnemyBrainContext ctx);
    }

    /// <summary>
    /// ScriptableObject로 만드는 브레인의 공통 베이스.
    /// SerializeReference 대신 애셋을 쓰는 이유: GUID 참조라 클래스 이름을 바꿔도 안 끊기고,
    /// 인스펙터 드래그&드롭이 기본으로 되고, 수치만 다른 변종을 애셋으로 찍을 수 있다.
    /// </summary>
    public abstract class EnemyBrainAsset : ScriptableObject, IEnemyBrain
    {
        public abstract EnemyIntent Decide(in EnemyBrainContext ctx);
    }
}
