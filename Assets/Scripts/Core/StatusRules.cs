namespace Prototype
{
    /// <summary>
    /// <see cref="StatusKind"/>와 <see cref="Debuff"/> 사이의 표를 <b>한 벌만</b> 유지한다.
    /// <see cref="CombatStateRules"/>와 같은 성격이다 — 유니티 객체를 만지지 않으므로
    /// EditMode에서 그대로 검증되고, 표가 코드 여기저기로 흩어지지 않는다.
    ///
    /// 두 enum을 둔 이유: 목록(<see cref="StatusEffects"/>)은 종류별로 하나씩 들고 있어야 하고
    /// (남은 시간 · 게이지 분모), 판정하는 쪽은 "행동을 막는 것이 하나라도 걸렸나"만 알면 된다.
    /// 비트마스크는 뒤쪽 질문에 한 번의 AND로 답한다.
    /// </summary>
    public static class StatusRules
    {
        /// <summary>이 상태가 차지하는 디버프 비트. 버프는 비트를 갖지 않는다.</summary>
        public static Debuff DebuffOf(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Stun:   return Debuff.Stun;
                case StatusKind.Freeze: return Debuff.Freeze;
                default:                return Debuff.None;
            }
        }

        public static bool IsDebuff(StatusKind kind) => DebuffOf(kind) != Debuff.None;

        /// <summary>
        /// 비트 하나를 상태 종류로 되돌린다. <see cref="HitData.debuff"/>가 마스크로 저작되므로
        /// (인스펙터에서 스턴 · 빙결을 함께 고를 수 있다) 목록에 넣을 때 풀어야 한다.
        ///
        /// 여러 비트가 켜진 마스크를 넘기면 false다 — 한 칸씩 물어보라는 뜻이다.
        /// </summary>
        public static bool TryStatusOf(Debuff bit, out StatusKind kind)
        {
            switch (bit)
            {
                case Debuff.Stun:   kind = StatusKind.Stun;   return true;
                case Debuff.Freeze: kind = StatusKind.Freeze; return true;
                default:            kind = default;           return false;
            }
        }

        /// <summary>이 마스크가 걸려 있으면 제 의지로 움직일 수 없다.</summary>
        public static bool BlocksAction(Debuff mask) => (mask & Debuff.ActionBlocking) != 0;

        /// <summary>
        /// 마스크에 켜진 비트를 하나씩 훑는다. 비트 수가 둘뿐이라 배열로 두는 편이
        /// 시프트 루프보다 읽기 쉽고, 새 디버프를 추가할 때 여기 한 줄만 늘면 된다.
        /// </summary>
        public static readonly Debuff[] Bits = { Debuff.Stun, Debuff.Freeze };
    }
}
