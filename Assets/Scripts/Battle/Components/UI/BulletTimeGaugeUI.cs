using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 화면 <b>좌측 하단</b>, 주인공 초상화 옆 · 체력바 바로 밑의 총공격 게이지. 자리는 <see cref="HudLayout"/>이 정한다.
    /// 막대는 <see cref="BulletTimeGaugeWidget"/>이 코드로 짓고,
    /// 여기서는 그 막대를 프리팹의 캔버스에 앉혀 매 프레임 먹인다.
    /// <see cref="BulletTimeController"/>가 붙은 GameObject에 둔다.
    /// </summary>
    [RequireComponent(typeof(BulletTimeController))]
    public class BulletTimeGaugeUI : MonoBehaviour
    {
        /// <summary>
        /// 막대가 앉는 자리 — 화면 왼쪽 아래 기준, 막대의 왼쪽 아래 꼭짓점.
        /// 체력바와 같은 x에서 시작하고, 윗변이 체력바 테두리에 맞닿는다.
        /// </summary>
        public static Vector2 Position
            => new Vector2(HudLayout.MarginX + HudLayout.BarX, HudLayout.MarginBottom + HudLayout.GaugeY);

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

            // 아래 띠는 맵보다 먼저 그린다 — 맵이 그 위를 덮고, 방 밖 배경이 비치던 자리만 검게 남는다.
            BuildBottomBand(transform, Camera.main);
            _gauge.Build(_canvas);

            RectTransform bar = _gauge.Root;
            bar.anchorMin = bar.anchorMax = bar.pivot = Vector2.zero;
            bar.anchoredPosition = Position;
        }

        /// <summary>띠를 까는 깊이 — 카메라 먼 면의 이 비율. 방 · 몸은 전부 이보다 가깝다.</summary>
        private const float BandDepth = 0.95f;

        /// <summary>
        /// 화면 아래를 가로지르는 검은 띠 — 방 밖 배경이 비치던 자리를 채운다. 위쪽 런 바와 같은 판 색이다.
        ///
        /// <b>맵보다 먼저 그린다.</b> 화면에 붙은 캔버스지만 오버레이가 아니라 카메라 공간이다 — 카메라 먼 쪽에 깔고
        /// 정렬 순서를 맨 뒤(<see cref="UiLayer.BehindWorld"/>)로 둬서 바닥 · 벽 · 몸이 전부 그 위에 그려진다.
        /// 그래서 맵은 하나도 안 가리고 맵이 없는 자리만 검게 보인다. 초상화 · 체력 · 게이지는 오버레이라 늘 그 위다.
        /// 카메라가 없으면 안 짓는다 — 오버레이로 대신 깔면 다시 맵을 가린다.
        /// </summary>
        public static RectTransform BuildBottomBand(Transform owner, Camera camera)
        {
            if (camera == null) return null;

            var host = new GameObject("BottomBand", typeof(RectTransform));
            host.transform.SetParent(owner, false);

            Canvas canvas = UiKit.BuildCanvas(host, UiLayer.BehindWorld, raycaster: false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.farClipPlane * BandDepth;

            Image plate = UiKit.NewImage(host.transform, "Plate", UiTheme.BarPlate);
            RectTransform p = plate.rectTransform;
            p.anchorMin = Vector2.zero;
            p.anchorMax = new Vector2(1f, 0f);
            p.pivot = new Vector2(0.5f, 0f);
            p.sizeDelta = new Vector2(0f, HudLayout.BandHeight);
            p.anchoredPosition = Vector2.zero;
            return p;
        }

        // UI는 비배율 시간으로 돈다 — TimeControl.Scale과 무관하게 막대는 움직여야 한다.
        private void Update() => _gauge.Refresh(_bulletTime, TimeControl.UnscaledDeltaTime);
    }
}
