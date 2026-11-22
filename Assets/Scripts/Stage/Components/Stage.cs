// 스테이지 구간 · 진행도 · 카메라 프레이밍 규칙.
// 셋 다 순수 규칙이라 씬을 켜지 않고 검증된다.

using System.Collections.Generic;
using System;
using UnityEngine;

namespace Prototype
{
    // ══ StageProgressSource ═══════════════════════════════════════════

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

        /// <summary>
        /// 스테이지가 끝나는 방 — 마지막 조우가 벌어지는 바닥. <b>클리어 보상이 여기 떨어지고 출구선이 이 방에서 풀린다.</b>
        ///
        /// 방이 여럿인 스테이지(아레나 → 통로 → 아레나)에서 이 값 없이 "씬의 방 하나"를 가정하면
        /// 보상이 첫 방 좌표에 떨어지고, 출구선이 첫 방 안이라 마지막 방에서는 영영 출구가 안 열린다.
        /// 모르면 거짓 — 부르는 쪽이 씬의 방이나 저작값으로 떨어진다.
        /// </summary>
        public virtual bool TryGetFinalArea(out RoomRect area)
        {
            area = default;
            return false;
        }

        /// <summary>
        /// 다음 방에 걸어 들어가기를 기다리는 중인가 — 방 사이 통로다. 앞 방은 깼고 다음 방은 아직 안 열렸다.
        /// 싸움 밖이라 총공격 편성이 열린다. 모르면 거짓.
        /// </summary>
        public virtual bool AwaitingEntry => false;
    }
}
