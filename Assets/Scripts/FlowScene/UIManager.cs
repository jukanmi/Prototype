using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 게임 내 전체 UI를 총괄 관리하는 매니저.
    /// 사운드 볼륨 슬라이더, UI 팝업/패널 제어 등의 중앙 창구 역할을 담당합니다.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("오디오 설정 슬라이더")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("주요 패널")]
        [SerializeField] private GameObject settingsPanel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            InitAudioSliders();
        }

        /// <summary>
        /// AudioManager와 볼륨 슬라이더의 값을 동기화하고 리스너를 연결합니다.
        /// </summary>
        public void InitAudioSliders()
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;

            if (masterSlider != null)
            {
                masterSlider.minValue = 0f;
                masterSlider.maxValue = 1f;
                masterSlider.value = audio.MasterVolume;
                masterSlider.onValueChanged.RemoveAllListeners();
                masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }

            if (bgmSlider != null)
            {
                bgmSlider.minValue = 0f;
                bgmSlider.maxValue = 1f;
                bgmSlider.value = audio.BgmVolume;
                bgmSlider.onValueChanged.RemoveAllListeners();
                bgmSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 1f;
                sfxSlider.value = audio.SfxVolume;
                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }
        }

        // ── 슬라이더 이벤트 콜백 ─────────────────────────────

        public void OnMasterVolumeChanged(float value)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMasterVolume(value);
        }

        public void OnBgmVolumeChanged(float value)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetBgmVolume(value);
        }

        public void OnSfxVolumeChanged(float value)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetSfxVolume(value);
        }

        // ── 패널 제어 ───────────────────────────────────────

        /// <summary>설정창 열기/닫기 토글</summary>
        public void ToggleSettingsPanel()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(!settingsPanel.activeSelf);
        }

        public void OpenSettingsPanel()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(true);
        }

        public void CloseSettingsPanel()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }
    }
}

