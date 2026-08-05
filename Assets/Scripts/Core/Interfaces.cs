namespace Prototype
{
    /// <summary>피격 판정을 받을 수 있는 대상. 상태 전이 · 넉백까지 포함한다.</summary>
    public interface IHittable
    {
        void Hit(in HitData hitData, Combat attacker);
    }

    /// <summary>데미지만 받는 대상. 파괴 가능한 오브젝트 등.</summary>
    public interface IDamageable
    {
        void TakeDamage(in DamageData damageData);
    }
}
