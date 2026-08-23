using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// "이 스테이지에 아직 나올 적이 남았는가"를 아는 것. 웨이브 방과 아레나 방이 각각 구현한다.
    ///
    /// <b>승리 판정이 이걸 물어본다.</b> 살아 있는 적 수만 보면 웨이브 사이 · 라운드 사이의
    /// 빈 구간이 그대로 승리로 잡혀서, 첫 웨이브만 잡고 스테이지가 끝난다.
    ///
    /// 인터페이스가 아니라 추상 클래스인 이유는 <c>FindAnyObjectByType</c> 때문이다 —
    /// 유니티의 오브젝트 검색은 인터페이스로 찾지 못한다.
    /// </summary>
    public abstract class StageProgressSource : MonoBehaviour
    {
        /// <summary>아직 나올 적(예약분 · 남은 웨이브 · 남은 라운드)이 있는가.</summary>
        public abstract bool ThreatsRemaining { get; }

        /// <summary>한 기라도 소환한 적이 있는가. 첫 프레임 오판정을 막는 데 쓴다.</summary>
        public abstract bool HasSpawnedAny { get; }
    }
}
