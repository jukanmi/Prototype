using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 표식 한 개를 그려 달라는 요청. 소스는 <b>논리 좌표와 모양만</b> 넘긴다 —
    /// 빌보드 · 깊이 배율 · 정렬 · 가장자리 물림은 <see cref="MarkerLayer"/>가 한 번만 처리한다.
    /// <see cref="BattleVfx"/>가 호출부에서 화면 접기를 걷어 내는 것과 같은 취지다.
    /// </summary>
    public struct MarkerRequest
    {
        /// <summary>가리킬 대상의 지상 좌표. 화면 밖이면 레이어가 가장자리로 물린다.</summary>
        public Vector3 ground;

        /// <summary>대상의 점프 높이. 표식이 머리를 따라 올라간다.</summary>
        public float height;

        /// <summary>머리 위로 띄우는 양. 깊이 배율이 곱해진다.</summary>
        public float headOffset;

        /// <summary>그릴 글자. <c>▼</c> 같은 한 글자를 상정한다.</summary>
        public string glyph;

        public Color color;

        /// <summary>깊이 정렬(<c>-z·100</c>) 위에 더하는 값.</summary>
        public int sortingOffset;

        /// <summary>월드 단위 글자 크기.</summary>
        public float size;

        /// <summary>상하 부유 오프셋. 소스가 직접 계산해 넣는다.</summary>
        public float bob;

        /// <summary>
        /// 화면 밖으로 물렸을 때 <b>눕힐 것인가</b>.
        ///
        /// 화살표(<c>▼</c>)처럼 방향이 곧 뜻인 글리프만 켠다. 직업 글자 같은 것을 눕히면
        /// 글자가 옆으로 쓰러져 읽히지 않고, 애초에 <b>어느 가장자리에 붙었는지가
        /// 이미 방향을 말해 준다</b> — 굳이 회전으로 한 번 더 말할 이유가 없다.
        /// </summary>
        public bool tilt;

        /// <summary>
        /// 눕히는 각도(도). <see cref="tilt"/>가 켜져 있으면
        /// <see cref="MarkerRules.PointerAngle"/>이 채운다.
        /// </summary>
        public float angle;
    }

    /// <summary>
    /// 표식을 요구하는 쪽. <see cref="MarkerLayer"/>와 같은 오브젝트에 붙이면
    /// 레이어가 알아서 찾아 매 프레임 물어본다 — 씬 배선이 없다.
    ///
    /// 리스트를 <b>받아서 채운다</b>. 매 프레임 도는 자리라 새 리스트를 만들면 그대로 GC 부담이 된다.
    /// </summary>
    public interface IMarkerSource
    {
        void Collect(List<MarkerRequest> into);
    }
}
