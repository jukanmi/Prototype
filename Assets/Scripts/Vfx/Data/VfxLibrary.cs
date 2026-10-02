using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 전역 기본 이펙트 시트. 스킬이 따로 지정하지 않았을 때 여기로 떨어진다.
    ///
    /// <see cref="BattleVfx"/>는 static이고 <see cref="VfxRunner"/>는 자동 생성이라
    /// 인스펙터로 프리팹을 물릴 자리가 없다. 씬 배선을 0으로 유지하려면
    /// <c>Resources</c> 경로 로드가 유일한 통로다.
    ///
    /// <b>에셋이 없어도 된다.</b> 없으면 전부 지금의 LineRenderer 연출로 폴백하므로
    /// 시트가 한 장씩 들어올 때마다 칸을 채워 나가면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "VfxLibrary", menuName = "Prototype/Vfx Library")]
    public class VfxLibrary : ScriptableObject
    {
        /// <summary><c>Assets/Resources/</c> 아래 이 이름으로 둬야 잡힌다.</summary>
        public const string ResourcePath = "VfxLibrary";

        [Tooltip("적중 순간의 타격.")]
        public VfxClip impact;
        [Tooltip("적중 순간의 충격파. 비우면 타격 하나만 나온다.")]
        public VfxClip shock;
        [Tooltip("스킬 시전. 조준으로 찍은 자리에 뜬다.")]
        public VfxClip cast;
        [Tooltip("벽 바운드. 비우면 타격 시트를 쓴다.")]
        public VfxClip wall;

        [Header("UI")]
        [Tooltip("차징 게이지. 프레임 0이 가득 참, 마지막이 빈 상태여야 한다.")]
        public VfxClip chargeGauge;

        private static VfxLibrary cached;
        private static bool loaded;

        /// <summary>없으면 null. 호출부는 null을 폴백 신호로 본다.</summary>
        public static VfxLibrary Get()
        {
            if (loaded) return cached;

            loaded = true;
            cached = Resources.Load<VfxLibrary>(ResourcePath);
            return cached;
        }

        /// <summary>Domain Reload가 꺼져 있으면 static이 플레이 세션을 넘어 살아남는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cached = null;
            loaded = false;
        }
    }
}
