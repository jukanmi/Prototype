using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 인스펙터에서 로그 카테고리를 토글한다. 씬 아무 오브젝트에나 하나 붙이면 된다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class BattleLogSettings : MonoBehaviour
    {
        [Tooltip("콘솔에 찍을 카테고리. 시끄러우면 여기서 끈다.")]
        [SerializeField] private LogCategory mask = LogCategory.All;
        [Tooltip("한 프레임에 몰린 이벤트를 구분하려면 켠다.")]
        [SerializeField] private bool showFrame = true;

        private void Awake() => Apply();
        private void OnValidate() => Apply();

        private void Apply()
        {
            BattleLog.Mask = mask;
            BattleLog.ShowFrame = showFrame;
        }
    }
}
