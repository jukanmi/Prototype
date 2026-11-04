// 스테이지를 처음부터 다시 거는 쪽. [처음부터] 버튼(BattleRestartUI)은 이것만 알고,
// 구현(BattleSceneController · App 층)을 모른다.

namespace Prototype
{
    public interface IStageRestarter
    {
        void RestartStage();
    }
}
