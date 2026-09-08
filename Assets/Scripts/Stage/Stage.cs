// 스테이지 구간 · 진행도 · 카메라 프레이밍 규칙.
// 셋 다 순수 규칙이라 씬을 켜지 않고 검증된다.

using System.Collections.Generic;
using System;
using UnityEngine;

namespace Prototype
{
    // ══ StageSection ═══════════════════════════════════════════

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
    }

    // ══ CameraFrameRules ═══════════════════════════════════════════

    /// <summary>
    /// 카메라가 이번 프레임에 어디를 봐야 하는가. <b>순수 함수</b>다.
    ///
    /// 두 규칙뿐이고 둘 다 눈으로 검증하기 어렵다 — 카메라 버그는 "뭔가 이상한데"로만
    /// 보이지 숫자가 안 보이기 때문이다. 그래서 여기 떼어 놓고 테스트가 직접 부른다.
    /// </summary>
    public static class CameraFrameRules
    {
        /// <summary>
        /// 구간 밖을 보지 않도록 카메라 X를 물린다.
        ///
        /// <b>구간이 화면보다 좁으면 중앙에 고정한다.</b> 이게 아레나 락의 정체다 —
        /// 락을 위한 코드가 따로 있는 게 아니라, 아레나 경계가 화면 폭보다 좁아서
        /// 클램프 구간이 한 점으로 접힌 것뿐이다. 데드존 로직은 그대로 돌아도
        /// 결과가 안 바뀐다.
        /// </summary>
        /// <param name="desiredX">따라가고 싶은 좌표(보통 플레이어).</param>
        /// <param name="halfWidth">카메라가 한쪽으로 보는 폭. 직교 카메라면 size × aspect.</param>
        public static float ClampToSection(float desiredX, float sectionMin, float sectionMax, float halfWidth)
        {
            float min = sectionMin + halfWidth;
            float max = sectionMax - halfWidth;

            // 구간이 화면보다 좁다 — 어디를 봐도 밖이 보이므로 가운데가 가장 낫다.
            if (min >= max) return (sectionMin + sectionMax) * 0.5f;

            return Mathf.Clamp(desiredX, min, max);
        }

        /// <summary>
        /// 구간이 화면보다 좁아 카메라가 결국 고정되는가. 표시·디버그용 질문이다.
        /// </summary>
        public static bool IsLocked(float sectionMin, float sectionMax, float halfWidth)
            => sectionMin + halfWidth >= sectionMax - halfWidth;

        /// <summary>
        /// 데드존. 목표가 화면 중앙 밴드를 <b>벗어날 때만</b> 카메라를 민다.
        ///
        /// 없으면 통로에서 플레이어가 한 발짝 움직일 때마다 배경이 따라 흔들려서
        /// 눈이 피로하다. 밴드 안이면 지금 자리를 그대로 돌려준다.
        /// </summary>
        /// <returns>카메라가 향해야 할 X.</returns>
        public static float ApplyDeadZone(float cameraX, float targetX, float halfBand)
        {
            if (halfBand <= 0f) return targetX;

            float delta = targetX - cameraX;

            if (delta > halfBand) return targetX - halfBand;
            if (delta < -halfBand) return targetX + halfBand;

            return cameraX;
        }

        /// <summary>
        /// 직교 카메라가 한쪽으로 보는 폭. <paramref name="aspect"/>는 가로/세로다.
        /// </summary>
        public static float HalfWidth(float orthographicSize, float aspect)
            => Mathf.Max(0f, orthographicSize) * Mathf.Max(0.0001f, aspect);

        /// <summary>
        /// 프레임률에 안 휘는 지수 접근. <c>Lerp(a, b, rate·dt)</c>와 달리 60fps와 30fps에서
        /// 같은 시간에 같은 거리를 간다.
        ///
        /// <b>차이가 0이면 아무 일도 안 한다</b>는 점이 중요하다 — 카메라 앵커가 이걸 쓰는데,
        /// 태그 교대는 새 몸이 <b>같은 자리</b>에 서므로 차이가 0이고 그래서 지금과 완전히
        /// 똑같이 즉각 반응한다. 콤보 시전자가 돌진해 나간 자리로 넘어갈 때만 미끄러진다.
        /// "즉시냐 부드럽게냐"를 분기로 나눌 필요가 없다.
        /// </summary>
        public static float Approach(float from, float to, float rate, float dt)
        {
            if (rate <= 0f || dt <= 0f) return to;
            return Mathf.Lerp(from, to, 1f - Mathf.Exp(-rate * dt));
        }
    }
}
