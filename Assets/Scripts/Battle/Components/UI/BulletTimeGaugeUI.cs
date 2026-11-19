using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 화면 아래 가운데의 총공격 게이지. 막대는 <see cref="BulletTimeGaugeWidget"/>이 코드로 짓고,
    /// 여기서는 그 막대를 프리팹의 캔버스에 앉혀 매 프레임 먹인다.
    /// <see cref="BulletTimeController"/>가 붙은 GameObject에 둔다.
    /// </summary>
    [RequireComponent(typeof(BulletTimeController))]
    public class BulletTimeGaugeUI : MonoBehaviour
    {
        /// <summary>화면 아래에서 막대 중심까지의 높이(캔버스 단위).</summary>
        public const float BottomOffset = 167f;

        [Tooltip("막대를 그릴 캔버스.")]
        [SerializeField] private RectTransform _canvas;

        private BulletTimeController _bulletTime;
        private readonly BulletTimeGaugeWidget _gauge = new BulletTimeGaugeWidget();

        private void Awake() => _bulletTime = GetComponent<BulletTimeController>();

        private void Start()
        {
            // 전투 씬을 단독으로 켜도 이벤트 시스템이 있게 한다 — 마우스 평타가 UI 위 클릭을 거르는 데 쓴다.
            UiKit.EnsureEventSystem();

            if (_canvas == null)
            {
                Debug.LogWarning(
                    "[BulletTimeGaugeUI] 캔버스 배선이 비어 있다 — 게이지가 안 뜬다. " +
                    "CombatManager 프리팹의 GaugeCanvas 배선을 확인할 것.", this);
                return;
            }

            _gauge.Build(_canvas);

            RectTransform bar = _gauge.Root;
            bar.anchorMin = new Vector2(0.5f, 0f);
            bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.anchoredPosition = new Vector2(0f, BottomOffset);
        }

        // UI는 비배율 시간으로 돈다 — TimeControl.Scale과 무관하게 막대는 움직여야 한다.
        private void Update() => _gauge.Refresh(_bulletTime, TimeControl.UnscaledDeltaTime);
    }
}
