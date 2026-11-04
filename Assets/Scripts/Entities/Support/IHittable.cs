// 타격을 받는 쪽의 계약. Combat(Actor 층)을 시그니처에 쓰므로 Actor 층에 둔다.
// HitData · DamageData 자체는 Kernel(Core/Support/HitData.cs)에 남는다.

namespace Prototype
{
    // ══ Interfaces ═══════════════════════════════════════════

    /// <summary>피격 판정을 받을 수 있는 대상. 상태 전이 · 넉백까지 포함한다.</summary>
    public interface IHittable
    {
        bool Hit(in HitData hitData, Combat attacker);
    }

    /// <summary>데미지만 받는 대상. 파괴 가능한 오브젝트 등.</summary>
    public interface IDamageable
    {
        void TakeDamage(in DamageData damageData);
    }
}
