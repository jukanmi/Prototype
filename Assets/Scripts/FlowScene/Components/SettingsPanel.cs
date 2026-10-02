using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 설정 패널. 볼륨 슬라이더 세 개를 <see cref="AudioManager"/>와 잇고, 패널을 열고 닫는다.
    ///
    /// 옛 이름은 UIManager 였다. 하는 일이 이것뿐이라 이름이 역할을 부풀렸다 —
    /// 스크립트 GUID(.meta)는 그대로라 Boot 씬 · 프리팹의 참조는 유지된다.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("오디오 설정 슬라이더")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("주요 패널")]
        [SerializeField] private GameObject settingsPanel;

        private void Start()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null) return;

            Bind(masterSlider, audio.MasterVolume, audio.SetMasterVolume);
            Bind(bgmSlider, audio.BgmVolume, audio.SetBgmVolume);
            Bind(sfxSlider, audio.SfxVolume, audio.SetSfxVolume);
        }

        /// <summary>슬라이더를 현재 값에 맞추고 움직이면 그대로 밀어 넣는다.</summary>
        private static void Bind(Slider slider, float value, UnityEngine.Events.UnityAction<float> apply)
        {
            if (slider == null) return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(apply);
        }

        // ── 패널 제어 (버튼 OnClick에 꽂는 자리) ─────────────

        public void Toggle()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(!settingsPanel.activeSelf);
        }

        public void Open()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(true);
        }

        public void Close()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }
    }
}
