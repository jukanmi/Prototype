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

        [Tooltip("원거리 전용. 이보다 가까우면 물러난다. 0이면 물러나지 않는다.")]
        public float preferredMinRange;

        [Tooltip("특수 행동(돌진)이 닿는 최대 거리. 0이면 특수 행동을 쓰지 않는다.")]
        public float specialRange;
    }

    /// <summary>브레인이 판단에 쓰는 입력. EnemyControl이 매 프레임 채운다.</summary>
    public struct EnemyBrainContext
    {
        /// <summary>특수 행동 인덱스의 상한. 비트마스크가 int라 여기서 막힌다.</summary>
        public const int MaxSpecials = 32;

        public Entity self;

        /// <summary>없으면 null. 브레인이 먼저 검사해야 한다.</summary>
        public Entity target;

        /// <summary>타겟까지의 벡터. XZ 평면이고 정규화 전.</summary>
        public Vector3 toTarget;

        /// <summary>toTarget.magnitude. 브레인이 다시 sqrt를 돌리지 않도록 미리 넣어 준다.</summary>
        public float distance;

        /// <summary>공격 쿨이 끝났는지. 쿨 관리는 EnemyControl이 한다.</summary>
        public bool attackReady;

        /// <summary>
        /// 0번 특수 행동의 쿨이 끝났는지. 평타 쿨과 따로 돈다.
        /// 특수가 하나뿐인 적(돌진 멧돼지)을 위한 지름길이며,
        /// <see cref="specialReadyMask"/>의 0번 비트와 항상 같은 값이다.
        /// </summary>
        public bool specialReady;

        /// <summary>
        /// 특수 행동 인덱스별 쿨 완료 여부. 비트 i가 서 있으면 i번을 지금 쓸 수 있다.
        ///
        /// bool 배열이 아니라 비트마스크인 이유: 컨텍스트는 매 프레임 만들어지는 struct라
        /// 배열을 실으면 프레임마다 할당이 생기고, 브레인은 무상태여야 해서
        /// 자기 쪽에 캐시를 둘 수도 없다.
        /// </summary>
        public int specialReadyMask;

        /// <summary>남은 체력 비율(0~1). 보스 페이즈 판정이 읽는다.</summary>
        public float healthRatio;

        public EnemyBrainParams p;
        public float dt;

        /// <summary>인덱스 하나의 쿨 완료 여부. 브레인이 비트 연산을 직접 쓰지 않게 감싼다.</summary>
        public bool IsSpecialReady(int index)
            => index >= 0 && index < MaxSpecials && (specialReadyMask & (1 << index)) != 0;
    }

    /// <summary>
    /// 브레인이 고른 행동의 종류.
    /// <see cref="Command"/>와 나눠 둔 이유: 특수 행동은 상태머신이 아는 명령이 아니라
    /// EnemyControl이 붙잡고 돌리는 <b>실행기</b>(<see cref="IEnemySpecialAction"/>)가 처리한다.
    /// </summary>
    public enum EnemyActionKind
    {
        None,
        Move,
        Attack,

        /// <summary>실행기가 가진 패턴 하나. 어느 패턴인지는 <see cref="EnemyIntent.specialIndex"/>가 정한다.</summary>
        Special,
    }

    /// <summary>브레인이 내는 결론. EnemyControl이 그대로 Control 프로퍼티로 옮긴다.</summary>
    public struct EnemyIntent
    {
        public EnemyActionKind kind;
        public Command command;
        public Vector3 moveDirection;

        /// <summary>실행기 안에서 몇 번 패턴인가. kind가 Special일 때만 의미가 있다.</summary>
        public int specialIndex;

        /// <summary>
        /// 이 패턴을 쓴 뒤 걸리는 쿨. 0 이하면 EnemyControl의 specialInterval로 떨어진다.
        ///
        /// 쿨 <b>길이</b>를 브레인이 정하고 쿨 <b>타이머</b>는 EnemyControl이 굴린다.
        /// 브레인은 무상태여야 해서 타이머를 못 들고, 실행기는 패턴을 언제 다시 쓸지에
        /// 관여하지 않는다(그건 판단이다).
        /// </summary>
        public float specialCooldown;

        public static EnemyIntent None => default;

        public static EnemyIntent Move(Vector3 dir)
            => new EnemyIntent { kind = EnemyActionKind.Move, command = Command.Move, moveDirection = dir };

        /// <summary>
        /// face는 AttackState.Enter가 Physics.Face에 쓴다.
        /// 비우면 Facing이 갱신되지 않아 마지막 이동 방향으로 헛휘두른다.
        /// </summary>
        public static EnemyIntent Attack(Vector3 face)
            => new EnemyIntent { kind = EnemyActionKind.Attack, command = Command.Attack, moveDirection = face };

        /// <summary>
        /// 특수 행동. command는 None으로 남긴다 — 평타 명령으로 새면 상태머신이 AttackState로 끌고 간다.
        /// </summary>
        public static EnemyIntent Special(int index, Vector3 dir, float cooldown = 0f)
            => new EnemyIntent
            {
                kind = EnemyActionKind.Special,
                command = Command.None,
                moveDirection = dir,
                specialIndex = index,
                specialCooldown = cooldown,
            };
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
