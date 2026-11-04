// 발판 하나의 계약. 높이를 묻는 GroundRegistry는 Actor 층(Physics가 씀)에 있고,
// 실제 발판(GroundPlate · Stage 층)이 이 인터페이스로 자신을 등록한다.

namespace Prototype
{
    public interface IGroundPlate
    {
        GroundRect Rect { get; }
    }
}
