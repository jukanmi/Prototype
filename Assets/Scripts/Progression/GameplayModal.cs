namespace Prototype
{
    /// <summary>
    /// 게임플레이를 <b>멈춰 세워야 하는</b> 전면 UI가 떠 있는가.
    ///
    /// 스테이지 진행(웨이브 시계 · 아레나 정비 시계)과 승패 판정이 각자 개별 UI를 알게 두면,
    /// 모달이 하나 늘 때마다 세 군데를 똑같이 고쳐야 하고 한 군데를 빠뜨리면
    /// "카드를 고르는 사이에 등 뒤에서 웨이브가 쏟아지는" 식으로만 드러난다.
    /// 그래서 물어보는 창구를 하나로 둔다.
    /// </summary>
    public static class GameplayModal
    {
        public static bool IsOpen => LevelUpSession.IsOpen || DeckBuilderUI.IsOpen;
    }
}
