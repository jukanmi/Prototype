using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 아레나 입구·출구를 막는 문. 콜라이더 하나와 그림 한 장이다.
    ///
    /// <b>보이게 막는 것이 요점이다.</b> 카메라 락만 걸고 조용히 벽을 세우면
    /// 플레이어는 자기가 갇힌 걸 <b>버그로 오해한다</b> — 돌아가려다 안 되니까
    /// 조작이 씹혔다고 읽는다. 문이 내려오는 그림이 있으면 같은 제약이 규칙이 된다.
    ///
    /// 문이 <b>열릴 때</b> 콜라이더를 끄는 것도 같은 무게로 중요하다. 남겨 두면
    /// 라운드를 이기고도 통로로 못 나가는, 원인이 전혀 안 보이는 상태가 된다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ArenaGate : MonoBehaviour
    {
        [Tooltip("문이 내려오고 올라가는 데 걸리는 시간.")]
        [SerializeField] private float slideSeconds = 0.35f;

        [Tooltip("닫혔을 때 문짝이 놓이는 로컬 Y. 열리면 이 위로 올라가 사라진다.")]
        [SerializeField] private float closedY;

        [Tooltip("열렸을 때 문짝이 올라가는 높이.")]
        [SerializeField] private float openLift = 5f;

        [SerializeField] private Transform panel;

        private BoxCollider blocker;
        private float t;          // 0 = 열림, 1 = 닫힘
        private bool wantClosed;

        public bool IsClosed => wantClosed && t >= 1f;
        public bool IsOpen => !wantClosed && t <= 0f;

        private void Awake()
        {
            blocker = GetComponent<BoxCollider>();
            blocker.isTrigger = false;

            // 시작은 열린 상태다. 스테이지가 올라오자마자 갇혀 있으면 안 된다.
            t = 0f;
            wantClosed = false;
            Apply();
        }

        public void Close() => wantClosed = true;
        public void Open() => wantClosed = false;

        /// <summary>연출 없이 즉시. 스테이지가 처음 올라올 때.</summary>
        public void SnapOpen()
        {
            wantClosed = false;
            t = 0f;
            Apply();
        }

        private void Update()
        {
            float step = slideSeconds <= 0.0001f ? 1f : Time.unscaledDeltaTime / slideSeconds;
            float goal = wantClosed ? 1f : 0f;

            if (!Mathf.Approximately(t, goal))
            {
                t = Mathf.MoveTowards(t, goal, step);
                Apply();
            }
        }

        /// <summary>
        /// 막는 판정은 <b>내려오기 시작하는 순간</b> 켜고 <b>완전히 열린 뒤에</b> 끈다.
        ///
        /// 순서를 반대로 하면 문틈이 생긴다 — 내려오는 0.35초 동안 빠져나가거나,
        /// 올라가는 도중에 이미 통과할 수 있어서 연출이 거짓말이 된다.
        /// </summary>
        private void Apply()
        {
            if (blocker != null) blocker.enabled = wantClosed || t > 0f;

            if (panel == null) return;

            Vector3 p = panel.localPosition;
            p.y = Mathf.Lerp(closedY + openLift, closedY, t);
            panel.localPosition = p;

            // 완전히 열리면 그림도 치운다. 반쯤 뜬 문짝이 하늘에 걸려 있으면 이상하다.
            panel.gameObject.SetActive(t > 0.001f);
        }

        /// <summary>빌더가 문짝 그림을 물려 준다.</summary>
        public void SetPanel(Transform value) => panel = value;
    }
}
