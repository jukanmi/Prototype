namespace Prototype
{
    /// <summary>QTE 판정 등급.</summary>
    public enum BulletTimeQTETier
    {
        Miss,
        Good,
        Perfect,
    }

    /// <summary>
    /// QTE 한 판의 결과. 실패해도 페널티는 없다 — <see cref="damageMultiplier"/>는 항상 1 이상이며,
    /// 성공했을 때만 보너스가 붙는다.
    /// </summary>
    public readonly struct BulletTimeQTEResult
    {
        public readonly BulletTimeQTETier tier;
        public readonly float damageMultiplier;

        public BulletTimeQTEResult(BulletTimeQTETier tier, float damageMultiplier)
        {
            this.tier = tier;
            this.damageMultiplier = damageMultiplier;
        }
    }
}
