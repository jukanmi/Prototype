namespace Prototype
{
    /// <summary>
    /// 전투를 <b>어떤 덱으로 시작하는가</b>. 런의 첫 전투 씬에서 한 번만 정해진다
    /// (<see cref="RunProgression.Seeded"/>), 그 뒤로는 런 덱이 그대로 이어진다.
    ///
    /// <see cref="Party"/> 외의 둘은 <b>디버그용</b>이다. 지금 이 게임은 16장을 쥐고 시작하는데,
    /// 그러면 특정 카드 한 장이나 황금 카드의 감각을 따로 떼어 볼 방법이 없다 —
    /// 손패 4칸이 늘 다른 12장과 섞여 나오기 때문이다.
    /// </summary>
    public enum DeckStartupMode
    {
        /// <summary>파티 4명의 장착 카드 16장. 게임의 실제 시작이다.</summary>
        Party,

        /// <summary>
        /// <b>테스트 모드</b> — 0장으로 시작한다. 손패가 비므로 카드는 오직 레벨업으로만 들어온다.
        /// 성장 곡선과 레벨업 보상만 떼어 볼 때 쓴다.
        /// </summary>
        Empty,

        /// <summary>
        /// <b>디버그 모드</b> — 시작할 때 화면에서 직접 짠다(<see cref="DeckBuilderUI"/>).
        /// 보고 싶은 카드만, 원하는 장수만 넣을 수 있다.
        /// </summary>
        Pick,
    }
}
