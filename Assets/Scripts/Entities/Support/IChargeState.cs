namespace Prototype
{
    /// <summary>
    /// "지금 무언가를 모으고 있다"는 계약. 머리 위 게이지(<see cref="ChargeGauge"/>)가
    /// <see cref="Entity.ChargeState"/>로 읽어 그린다. 지금 구현은 보스 패턴(<see cref="BossPatternAction"/>)뿐이다.
    /// </summary>
    public interface IChargeState
    {
        /// <summary>아직 모으는 중인지.</summary>
        bool IsCharging { get; }
        /// <summary>0~1. 게이지가 얼마나 찼는지.</summary>
        float ChargeRatio { get; }
    }
}
