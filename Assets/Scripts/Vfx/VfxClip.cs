using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스프라이트 시트 이펙트 한 벌.
    ///
    /// Animator를 쓰지 않는다. <see cref="TimeControl"/>은 <c>Time.timeScale</c>을 건드리지 않으므로
    /// (결정 로그 ⑥) Animator는 불릿타임에 혼자 재생된다. 매 프레임 <c>animator.speed</c>를
    /// 덮어쓰는 것보다, 프레임 인덱스를 직접 세는 쪽이 짧고 정확하다.
    /// 컨트롤러 · 클립 에셋도 필요 없어 시트 한 장에 에셋 한 개로 끝난다.
    /// </summary>
    [CreateAssetMenu(fileName = "VFX_", menuName = "Prototype/Vfx Clip")]
    public class VfxClip : ScriptableObject
    {
        [Tooltip("재생 순서대로. 슬라이스한 시트를 그대로 끌어다 넣는다.")]
        public Sprite[] frames;

        [Tooltip("초당 프레임.")]
        public float fps = 24f;

        [Tooltip("몇 바퀴 돌릴지. 회전 고리처럼 이어지는 시트를 길게 보여줄 때 올린다.")]
        [Min(1)] public int loops = 1;

        [Header("크기 · 배치")]
        [Tooltip("켜면 호출부가 넘긴 반경(히트박스 크기)에 맞춰 늘린다. 끄면 시트 원래 크기.")]
        public bool fitRadius = true;

        [Tooltip("추가 배율. fitRadius를 꺼도 이 값은 먹는다.")]
        public float scale = 1f;

        [Tooltip("바닥에서 띄울 높이. 발밑에서 터지면 착지처럼 읽힌다.")]
        public float heightOffset = 0f;

        [Tooltip("정렬 오프셋. 캐릭터와 같은 깊이일 때 앞(+)에 그릴지 뒤(-)에 깔지.")]
        public int sortingOffset = 50;

        [Header("방향")]
        [Tooltip("시전 방향으로 회전시킨다. 궤적처럼 방향이 있는 시트에.")]
        public bool rotateToFacing = false;

        [Tooltip("회전 대신 좌우 반전만 한다. 옆에서 본 시트에.")]
        public bool flipToFacing = false;

        [Header("색")]
        [Tooltip("시트에 곱할 기본 색. 흰색이면 원본 그대로.")]
        public Color tint = Color.white;

        [Tooltip("켜면 스킬 색(SkillVfx)을 곱한다. 색이 있는 시트는 꺼 두는 게 낫다.")]
        public bool useSkillColor = false;

        [Tooltip("끝에서 알파를 빼는 구간(0~1). 0이면 시트가 알아서 사라진다고 본다.")]
        [Range(0f, 1f)] public float fadeOut = 0f;

        /// <summary>총 재생 시간. 프레임이 없으면 0 — 호출부가 폴백으로 넘어간다.</summary>
        public float Duration => IsValid ? frames.Length * Mathf.Max(1, loops) / Mathf.Max(1f, fps) : 0f;

        public bool IsValid => frames != null && frames.Length > 0;

        /// <summary>
        /// 진행도 0~1에서의 프레임.
        /// <see cref="loops"/>가 1보다 크면 그만큼 돌고, 끝에서는 마지막 프레임을 넘지 않는다.
        /// 게이지처럼 시간이 아니라 <b>비율</b>로 읽는 쪽도 이 함수를 그대로 쓴다.
        /// </summary>
        public Sprite FrameAt(float t)
        {
            if (!IsValid) return null;

            int n = frames.Length;
            int step = Mathf.Clamp(Mathf.FloorToInt(t * n * Mathf.Max(1, loops)), 0, n * Mathf.Max(1, loops) - 1);
            return frames[step % n];
        }
    }
}
