using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>구간의 성격. 카메라가 흐르는 곳과 멈추는 곳, 둘뿐이다.</summary>
    public enum SectionKind
    {
        /// <summary>통로. 카메라가 스크롤되고 라운드가 발생하지 않는다.</summary>
        Corridor,

        /// <summary>아레나. 카메라가 고정되고 라운드가 돈다.</summary>
        Arena,
    }

    /// <summary>
    /// 스테이지를 X축으로 자른 한 토막.
    ///
    /// 통로를 굳이 두는 이유는 두 가지다. 라운드 사이에 숨 돌릴 구간이 있어야 정비 행동이
    /// 자연스럽게 들어가고, 카메라 락이 풀리자마자 다음 락이 걸리면 전환이 뚝뚝 끊겨 보인다.
    /// </summary>
    [Serializable]
    public struct StageSection
    {
        public SectionKind kind;
        public float minX;
        public float maxX;

        public float Center => (minX + maxX) * 0.5f;
        public float Width => maxX - minX;

        /// <summary>오른쪽 끝은 다음 구간의 것이다 — 경계에 서면 다음 구간으로 넘어간다.</summary>
        public bool Contains(float x) => x >= minX && x < maxX;

        public static StageSection Of(SectionKind kind, float minX, float maxX)
            => new StageSection { kind = kind, minX = minX, maxX = maxX };
    }

    /// <summary>
    /// 구간 목록을 읽는 <b>순수 함수</b>. 씬도 시간도 모른다.
    ///
    /// 떼어 놓은 이유는 경계에서의 판정 때문이다 — 구간이 딱 붙어 있어서
    /// <c>x == 6.0</c> 같은 값이 어느 쪽에 속하는지가 애매하고, 그 한 칸이 틀리면
    /// 아레나 트리거가 한 프레임 일찍 또는 영영 안 걸린다. 화면만 봐서는 절대 안 보인다.
    /// </summary>
    public static class StageLayoutRules
    {
        /// <summary>
        /// 이 X좌표가 속한 구간의 번호. 목록이 비면 -1.
        ///
        /// 목록 범위를 벗어난 좌표는 <b>양끝으로 물린다</b> — 넉백으로 벽을 뚫고 나가거나
        /// 마지막 아레나 오른쪽 끝에 서 있는 경우가 실제로 생기는데, 거기서 -1을 돌려주면
        /// 부르는 쪽이 전부 null 검사를 해야 한다.
        /// </summary>
        public static int SectionAt(IReadOnlyList<StageSection> sections, float x)
        {
            if (sections == null || sections.Count == 0) return -1;

            for (int i = 0; i < sections.Count; i++)
                if (sections[i].Contains(x)) return i;

            return x < sections[0].minX ? 0 : sections.Count - 1;
        }

        /// <summary>
        /// 아레나 진입 트리거 라인. 아레나의 <b>왼쪽 경계</b>다 —
        /// 플레이어가 이 선을 넘는 순간 락이 걸린다.
        /// </summary>
        public static float EntryLineOf(in StageSection arena) => arena.minX;

        /// <summary>
        /// 통로를 몇 장의 배경으로 덮어야 하는가. 배경 한 장이 통로보다 짧으면 바닥이 끊겨 보인다.
        /// </summary>
        public static int TileCount(float sectionWidth, float tileWidth)
        {
            if (tileWidth <= 0.0001f) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(sectionWidth / tileWidth));
        }
    }
}
