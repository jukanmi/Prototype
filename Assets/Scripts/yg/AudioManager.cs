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

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (bgmSource != null) bgmSource.loop = true;
        }

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
    }
}
