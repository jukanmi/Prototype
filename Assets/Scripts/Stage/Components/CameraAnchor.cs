using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 카메라가 <b>영원히 이것만</b> 바라보는 앵커. <c>BattleInput</c> 프리팹의 자식이다.
    ///
    /// <b>무엇을 고치는가.</b> 지금까지 <see cref="CameraFollow.SetTarget"/>이 몸 트랜스폼을
    /// 직접 받았고, 그 배선이 세 군데에서 각자 이뤄졌다 —
    /// <c>ArenaSceneBuilder</c> · <see cref="CameraFollow"/>의 Awake 폴백 · <see cref="TagSwapController"/>.
    /// 앵커를 두면 카메라는 앵커 하나만 알고, "누구를 비추는가"는
    /// <see cref="TagSwapController"/> 한 곳에서만 정해진다.
    ///
    /// <b>튐도 같이 사라진다.</b> 태그 교대(F)는 새 몸이 <b>같은 자리</b>에 서므로 차이가 0이라
    /// 앵커가 즉시 반응한다. 튀는 건 콤보에서 같은 시전자가 두 번 나올 때다 —
    /// 그 몸은 이미 무대에 있어서 자리를 다시 안 잡는데(돌진으로 전진한 걸 되감지 않으려는
    /// 의도적 설계) 카메라 대상만 그쪽으로 즉시 옮겨간다. 앵커는 그 거리를 미끄러진다.
    ///
    /// <b>루트 고정 규약을 안 깬다.</b> 원점에 박히는 것은 <c>BattleInput</c> 루트와
    /// <c>Party</c> 컨테이너뿐이고, 이 앵커는 그 형제로서 자유롭게 움직인다 —
    /// 파티 몸들이 그렇듯이.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class CameraAnchor : MonoBehaviour
    {
        [Tooltip("비추는 대상을 따라가는 속도. 클수록 즉각적.\n\n" +
                 "CameraFollow(6)보다 확실히 크게 잡을 것 — 둘이 겹쳐 걸리므로 " +
                 "여기가 느리면 카메라 반응이 통째로 늘어진다. 실제 감속은 카메라 쪽이 정한다.")]
        [SerializeField] private float rate = 20f;

        [Tooltip("비우면 씬에서 찾는다. 스테이지 씬의 Main Camera에 붙은 것.")]
        [SerializeField] private CameraFollow follow;

        /// <summary>지금 비추는 몸. 아무도 없으면 앵커는 제자리에 선다.</summary>
        public Transform Focus { get; private set; }

        /// <summary>
        /// 첫 <see cref="LateUpdate"/>에서 카메라를 붙일 것인가. <see cref="Start"/>에서 스냅하면 안 된다.
        ///
        /// <b>경계가 아직 안 정해졌기 때문이다.</b> 아레나 스테이지에서 구간 경계를 넘기는
        /// <see cref="StageRunner"/>는 실행 순서 0이라 이 컴포넌트(-50)의 Start보다 <b>뒤에</b> 돈다.
        /// 그 전에 스냅하면 <see cref="StageBounds"/>가 (0, 0)인 채로 클램프가 접혀
        /// 카메라가 원점을 잡고, 그다음에야 제자리로 미끄러진다 — 스테이지가 열리는 첫 순간이
        /// 통째로 흘러가는 그림이 된다.
        ///
        /// 모든 <c>Start</c>는 어떤 <c>LateUpdate</c>보다 먼저 끝나므로, 한 프레임 미루면
        /// 경계 · 스폰 자리 · 첫 몸이 전부 확정된 뒤에 붙는다.
        /// </summary>
        private bool pendingSnap;

        /// <summary>
        /// 카메라를 앵커에 물린다. <b>여기서 한 번 하고 다시는 안 바꾼다.</b>
        ///
        /// <b>꺼져 있는 <see cref="CameraFollow"/>는 건드리지 않는다.</b> 웨이브 스테이지는
        /// <c>SceneLayoutBuilder</c>가 그 컴포넌트를 일부러 꺼 둔다 — "방 하나가 한 화면,
        /// 카메라는 방 중심에 고정"이라는 아이작 구도이고, 그 자리는 씬에 구워져 있다.
        ///
        /// <c>null</c>만 검사하면 여기 걸린다. <c>FindAnyObjectByType</c>은 비활성
        /// <b>GameObject</b>만 거르지 비활성 <b>컴포넌트</b>는 그대로 돌려주고,
        /// <see cref="CameraFollow.SnapToTarget"/>은 public이라 꺼진 컴포넌트에서도 실행된다 —
        /// 그래서 고정돼 있어야 할 카메라가 파티 스폰 자리로 끌려간다.
        /// </summary>
        private void Start()
        {
            if (follow == null) follow = FindAnyObjectByType<CameraFollow>();
            if (follow == null || !follow.enabled) { follow = null; return; }

            follow.SetTarget(transform);
            pendingSnap = true;
        }

        /// <summary>
        /// 비출 몸을 바꾼다. <see cref="TagSwapController"/>만 부른다.
        /// null을 넘기면 앵커가 마지막 자리에 그대로 선다 — 카메라가 원점으로 튀지 않는다.
        /// </summary>
        public void SetFocus(Transform body)
        {
            if (body != null) Focus = body;
        }

        /// <summary>
        /// 보간 없이 옮긴다. 스테이지가 처음 올라올 때 한 번
        /// (<see cref="TagSwapController.SeedSeat"/>).
        /// </summary>
        public void SnapTo(float x)
        {
            Vector3 p = transform.position;
            transform.position = new Vector3(x, p.y, p.z);
        }

        /// <summary>
        /// <b>X만 따라간다.</b> <see cref="CameraFollow"/>가 읽는 것이 <c>target.position.x</c>
        /// 하나뿐이라 Y·Z를 옮길 이유가 없고, 옮기면 기즈모로 볼 때 앵커가 몸 속에 파묻힌다.
        ///
        /// <b>LateUpdate에서, 실행 순서 -50으로</b> 돈다 — 카메라도 LateUpdate에서 읽으므로
        /// 여기가 늦으면 카메라가 한 프레임 묵은 자리를 보고 전환 내내 미세하게 떤다.
        /// <see cref="StageBounds"/>가 같은 이유로 같은 자리에 있다.
        ///
        /// 시간은 <b>스케일 안 된 것</b>을 쓴다. 불릿타임 중에 시전자가 바뀌는데
        /// 게임 시간으로 돌리면 그동안 앵커가 굳어 카메라만 멎어 보인다.
        /// </summary>
        private void LateUpdate()
        {
            if (Focus != null)
            {
                float x = CameraFrameRules.Approach(
                    transform.position.x, Focus.position.x, rate, Time.unscaledDeltaTime);

                SnapTo(x);
            }

            if (!pendingSnap) return;

            // 사이에 누가 껐을 수 있다. 꺼진 카메라는 씬에 구워진 자리를 지켜야 한다.
            if (follow == null || !follow.enabled) { pendingSnap = false; return; }

            // 이 시점에는 경계도 스폰 자리도 첫 몸도 전부 확정돼 있다.
            pendingSnap = false;
            follow.SnapToTarget();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.8f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);

            if (Focus != null) Gizmos.DrawLine(transform.position, Focus.position);
        }
#endif
    }
}
