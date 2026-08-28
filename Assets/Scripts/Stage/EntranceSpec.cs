using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 등장 · 퇴장 한 번의 주문서. <see cref="EntranceDirector.Play"/>가 받는 유일한 인자다.
    ///
    /// <b>방향을 구분하지 않는다.</b> 등장은 화면 밖 → 착지점, 퇴장은 착지점 → 화면 밖일 뿐
    /// 하는 일이 같다. 두 메서드로 쪼개면 잠금 · 억제 · 뒷정리가 두 벌이 되고,
    /// 반드시 한쪽만 고쳐지는 날이 온다.
    /// </summary>
    public struct EntranceSpec
    {
        /// <summary>출발 지상 좌표. 등장이면 화면 밖이다.</summary>
        public Vector3 start;

        /// <summary>도착 지상 좌표. 퇴장이면 이쪽이 화면 밖이다.</summary>
        public Vector3 landing;

        /// <summary>도착했을 때 볼 방향. <see cref="Vector3.zero"/>면 진행 방향을 그대로 쓴다.</summary>
        public Vector3 facing;

        /// <summary>
        /// 바닥에서 뜬 높이. 양 끝점 모두 이 높이로 잡히므로 <b>수평으로</b> 날아온다.
        ///
        /// 공중에서 태그 교대를 하면 새 몸도 같은 높이에서 이어받아야 한다 —
        /// 지상 좌표만 넘기면 새 몸만 바닥에 서서 콤보가 그 자리에서 끊긴다.
        /// </summary>
        public float height;

        /// <summary>소요 시간. 0이면 <see cref="EntranceRules.DefaultSeconds"/>.</summary>
        public float seconds;

        /// <summary>
        /// 스케일 안 된 시간으로 굴릴 것인가.
        ///
        /// <b>컷인과 겹치는 등장은 반드시 true다.</b> 컷인이 <c>TimeControl.Scale</c>을
        /// 0.15로 내리므로, 게임 시간으로 굴리면 6.7배 느려져 컷인이 끝나도 아직 날아오는 중이다.
        /// </summary>
        public bool unscaled;

        /// <summary>
        /// 오는 동안 판정 · 조준을 끄고 배경 뒤로 숨길 것인가(<see cref="EntranceGuard"/>).
        ///
        /// 끄지 않으면 양쪽이 다 샌다 — 화면 밖의 몸이 맞거나, 화면 밖으로 광역기가 나간다.
        /// </summary>
        public bool guard;

        /// <summary>
        /// 오는 동안 AI · 조종 입력을 잠글 것인가(<see cref="Entity.IsEntering"/>).
        /// 끄면 날아오는 도중에 몸이 제 판단으로 걸어 나간다.
        /// </summary>
        public bool lockControl;

        /// <summary>
        /// 도착한 <b>뒤에</b> 할 일. 카메라 인계 · 진입 걷기 시작 · <c>SetActive(false)</c>가 여기 실린다.
        ///
        /// 도착 처리(텔레포트 · 방향 · 잠금 해제 · 억제 복구)가 <b>전부 끝난 다음</b> 불린다 —
        /// 그래야 콜백이 그 자리에서 다음 연출을 이어 걸어도 안전하다.
        /// </summary>
        public Action onArrive;

        /// <summary>
        /// 흔히 쓰는 조합. 판정을 끄고 조종을 잠근 채, <b>게임 시간</b>으로 들어온다.
        ///
        /// 기본이 게임 시간인 이유는 몸이 세상의 일부이기 때문이다 — 시간이 멈춘 화면에서
        /// 적 하나만 움직이면 정지가 통째로 깨진다. 컷인과 겹치는 시전자 등장만
        /// <see cref="unscaled"/>를 직접 켠다.
        /// </summary>
        public static EntranceSpec Default(Vector3 start, Vector3 landing, float seconds = 0f)
            => new EntranceSpec
            {
                start = start,
                landing = landing,
                facing = Vector3.zero,
                seconds = seconds,
                unscaled = false,
                guard = true,
                lockControl = true,
            };
    }
}
