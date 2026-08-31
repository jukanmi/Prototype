using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적의 평타. <b>단발이다.</b>
    ///
    /// 필드가 하나도 없는 게 이 클래스의 전부이자 요점이다 — 연타 단계를 <b>둘 자리가 없다</b>.
    /// 예전에는 같은 값이 <see cref="Entity"/>에 있어서 적 인스펙터에도 단계 배열이 떴고,
    /// 거기 값을 채워도 게임은 그냥 돌아갔다(적은 선입력 버퍼를 못 채우니 1타에서 멈춘다).
    /// 조용히 도는 저작 실수는 테스트로만 잡혔는데, 이제 타입이 막는다.
    ///
    /// 연타를 주고 싶어지면 그 몸은 적이 아니다 — <see cref="AllyBasicAttack"/>을 붙일 일이다.
    /// </summary>
    [AddComponentMenu("Prototype/평타 - 적 (단발)")]
    public sealed class EnemyBasicAttack : BasicAttackProfile
    {
    }
}
