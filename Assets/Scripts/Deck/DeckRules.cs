using System.Collections.Generic;

namespace Prototype
{
    /// <summary>
    /// 이 파티가 짜야 할 덱의 크기. <b>순수 함수</b>다.
    ///
    /// <b>왜 상수가 아닌가.</b> <see cref="Deck.Size"/>는 16이고, 그건 "동료 4명 × 장착 4장"이
    /// 만석일 때의 값이다. 3인 파티는 12장이 <b>정상</b>인데 상수와 비교하면 매 스테이지
    /// 거짓 경고가 뜬다 — 그러면 경고를 무시하는 습관이 붙고, 진짜 저작 실수
    /// (장착이 3장인 동료)까지 같이 묻힌다.
    ///
    /// <b>덱이 작아도 기능은 멀쩡하다.</b> <see cref="Deck.Draw"/>가 덱이 비면 버린 더미를
    /// 회수해 다시 섞으므로 12장 덱도 손패 4장을 끊김 없이 유지한다. 달라지는 것은
    /// 순환 속도뿐이고(<see cref="CycleHands"/>), 그건 알려 줄 정보이지 경고할 오류가 아니다.
    /// </summary>
    public static class DeckRules
    {
        /// <summary>동료 <paramref name="memberCount"/>명이 짜는 덱의 목표 장수.</summary>
        public static int TargetSize(int memberCount)
            => memberCount <= 0 ? 0 : memberCount * Ally.EquipSlots;

        /// <summary>편성 화면이 쓴다. 빈 칸은 세지 않는다.</summary>
        public static int TargetSize(IReadOnlyList<PartyMemberData> party)
            => TargetSize(CountFilled(party));

        /// <summary>
        /// 런타임이 쓴다. <see cref="Player.Party"/>는 빈 칸에 <c>null</c>이 들어 있고,
        /// 영구 사망한 동료의 슬롯도 <see cref="PartyAssembler"/>가 지워 <c>null</c>이 된다.
        /// 그래서 이 값은 <b>지금 실제로 카드를 낼 수 있는 인원</b>과 같다.
        /// </summary>
        public static int TargetSize(IReadOnlyList<Ally> party)
            => TargetSize(CountFilled(party));

        public static int CountFilled(IReadOnlyList<PartyMemberData> party)
        {
            if (party == null) return 0;

            int n = 0;
            for (int i = 0; i < party.Count; i++)
                if (party[i] != null) n++;

            return n;
        }

        public static int CountFilled(IReadOnlyList<Ally> party)
        {
            if (party == null) return 0;

            int n = 0;
            for (int i = 0; i < party.Count; i++)
                if (party[i] != null) n++;

            return n;
        }

        /// <summary>
        /// 덱 한 바퀴가 몇 번의 손패인가. 4인이면 4핸드, 3인이면 3핸드다.
        ///
        /// 인원이 줄면 <b>같은 카드가 더 자주 돌아온다</b>. 편성 화면이 이 숫자를 보여 줘야
        /// "동료 하나가 빠지면 콤보 다양성이 준다"는 사실이 고르는 자리에서 읽힌다 —
        /// 전투에 들어가서야 체감하는 값이 아니다.
        /// </summary>
        public static int CycleHands(int deckSize)
            => deckSize <= 0 ? 0 : deckSize / Hand.Size;

        /// <summary>
        /// 장수가 목표와 맞는가. <b>목표보다 많아도 어긋난 것이다</b> —
        /// 인원 × 4를 넘는다는 건 어떤 동료가 5장을 들고 있다는 뜻이다.
        /// </summary>
        public static bool Matches(int actual, int target) => actual == target;

        /// <summary>
        /// 저작 실수의 설명. 맞으면 <c>null</c>.
        ///
        /// 인원이 적어서 덱이 작은 것은 <b>실수가 아니다</b> — 그건 목표 자체가 줄어드는
        /// 경우라 여기 걸리지 않는다. 여기 걸리는 것은 "4인인데 15장" 같은,
        /// 장착 칸이 빈 동료가 있다는 신호뿐이다.
        /// </summary>
        public static string Explain(int actual, int memberCount)
        {
            int target = TargetSize(memberCount);
            if (Matches(actual, target)) return null;

            return actual < target
                ? $"장착 카드가 {actual}장이다(동료 {memberCount}명 × {Ally.EquipSlots}장 = {target}장). " +
                  "빈 장착 칸이 있는 동료를 확인할 것."
                : $"장착 카드가 {actual}장이다(동료 {memberCount}명 기준 {target}장). " +
                  $"{Ally.EquipSlots}장을 넘게 든 동료가 있다.";
        }
    }
}
