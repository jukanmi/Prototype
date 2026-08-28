using UnityEngine;

namespace Prototype.YG
{
    /// <summary>
    /// 사운드 골격. 클립이 아직 없으므로 인터페이스만 만들어 둔다.
    /// null 체크가 전부 들어간 이유 — 클립이 비어 있어도 예외 없이 넘어가야 한다.
    /// 프로토타입 진행을 오디오가 막으면 안 된다.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Clips (비어 있어도 무방)")]
        [SerializeField] private AudioClip menuBgm;
        [SerializeField] private AudioClip battleBgm;

        [Header("볼륨 (0~1)")]
        [Tooltip("전체 볼륨. BGM · SFX 각각의 값에 곱해진다.")]
        [Range(0f, 1f)][SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)][SerializeField] private float bgmVolume = 1f;
        [Range(0f, 1f)][SerializeField] private float sfxVolume = 1f;

        [Header("타격음")]
        [Tooltip("적중할 때마다 이 중 하나가 무작위로 난다. 직전에 난 것은 다시 고르지 않는다.")]
        [SerializeField] private AudioClip[] hitSfx;
        [Tooltip("재생마다 음정을 흔든다. 같은 파일이라도 연타가 기계음처럼 들리지 않는다.")]
        [SerializeField] private Vector2 hitPitchRange = new Vector2(0.94f, 1.06f);
        [Tooltip("장판처럼 여러 명이 한 프레임에 맞아도 이 간격 안에서는 한 번만 난다.")]
        [SerializeField] private float hitSfxInterval = 0.04f;

        /// <summary>직전에 고른 인덱스. 연속 중복을 피하는 데만 쓴다.</summary>
        private int lastHitIndex = -1;
        private float lastHitTime = float.NegativeInfinity;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (bgmSource != null) bgmSource.loop = true;

            ApplyVolumes();
        }

        // ── 볼륨 ─────────────────────────────────────────

        /// <summary>0~1. 슬라이더(<see cref="UIManager"/>)가 현재 값을 읽어 자기 위치를 맞춘다.</summary>
        public float MasterVolume => masterVolume;
        public float BgmVolume => bgmVolume;
        public float SfxVolume => sfxVolume;

        public void SetMasterVolume(float value) { masterVolume = Mathf.Clamp01(value); ApplyVolumes(); }
        public void SetBgmVolume(float value)    { bgmVolume    = Mathf.Clamp01(value); ApplyVolumes(); }
        public void SetSfxVolume(float value)    { sfxVolume    = Mathf.Clamp01(value); ApplyVolumes(); }

        /// <summary>
        /// 저장된 값을 실제 <see cref="AudioSource"/>에 민다.
        /// 마스터를 각 채널에 곱한다 — 마스터만 내려도 둘 다 같이 줄어야 한다.
        ///
        /// 소스가 비어 있어도 조용히 넘어간다. 클립처럼 배선이 덜 된 상태에서도
        /// 프로토타입이 멈추면 안 된다(이 클래스의 기존 규약).
        /// </summary>
        private void ApplyVolumes()
        {
            if (bgmSource != null) bgmSource.volume = masterVolume * bgmVolume;
            if (sfxSource != null) sfxSource.volume = masterVolume * sfxVolume;
        }

        /// <summary>
        /// Combat 의 static 이벤트를 문다. 시전자를 모르는 관전자 자리라 여기가 맞다 —
        /// 스킬 · 투사체 · 장판 어느 경로로 맞든 Combat.Attack 한 곳을 지나간다.
        /// </summary>
        private void OnEnable()  => Combat.OnAnyHitLanded += HandleAnyHitLanded;
        private void OnDisable() => Combat.OnAnyHitLanded -= HandleAnyHitLanded;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void PlayMenuBgm()   => PlayBgm(menuBgm);
        public void PlayBattleBgm() => PlayBgm(battleBgm);

        public void PlayBgm(AudioClip clip)
        {
            if (bgmSource == null || clip == null) return;                 // 클립 없으면 조용히 무시
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;     // 같은 곡 재시작 방지

            bgmSource.clip = clip;
            bgmSource.Play();
        }

        public void StopBgm()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        public void PlaySfx(AudioClip clip)
        {
            if (sfxSource == null || clip == null) return;

            sfxSource.PlayOneShot(clip);
        }

        // ── 타격음 ───────────────────────────────────────

        private void HandleAnyHitLanded(Combat attacker, Combat victim) => PlayHitSfx();

        /// <summary>적중 한 번. 클립이 비어 있으면 조용히 넘어간다.</summary>
        public void PlayHitSfx()
        {
            if (sfxSource == null || hitSfx == null || hitSfx.Length == 0) return;

            // 다단 히트와 장판은 한 프레임에 수십 번 들어온다. 그대로 겹치면 위상이 쌓여
            // 소리가 찢어지므로 최소 간격을 둔다. 시간 정지 연출 중에도 나야 해서 unscaled.
            if (Time.unscaledTime - lastHitTime < hitSfxInterval) return;

            AudioClip clip = PickHitClip();
            if (clip == null) return;

            lastHitTime = Time.unscaledTime;
            sfxSource.pitch = Random.Range(hitPitchRange.x, hitPitchRange.y);
            sfxSource.PlayOneShot(clip);
        }

        /// <summary>
        /// 직전 것을 뺀 나머지에서 균등하게 뽑는다.
        /// 뽑고 나서 다시 굴리는 방식은 최악의 경우 무한 루프라 인덱스를 밀어 처리한다.
        /// </summary>
        private AudioClip PickHitClip()
        {
            int count = hitSfx.Length;

            int index;
            if (count == 1 || lastHitIndex < 0)
            {
                index = Random.Range(0, count);
            }
            else
            {
                index = Random.Range(0, count - 1);
                if (index >= lastHitIndex) index++;
            }

            lastHitIndex = index;
            return hitSfx[index];
        }
    }
}
