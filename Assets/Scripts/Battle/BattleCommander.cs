using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 몸에서 떨어져 나온 지휘 입력. 불릿타임 진입 · 카드 즉시 사용 · 태그 교대를 받는다.
    ///
    /// <b>절대 꺼지지 않는 오브젝트에 붙어야 한다</b>. 태그로 내려가는 몸에 붙이면
    /// 교대하는 순간 되돌아올 키까지 함께 죽는다. 같은 이유로
    /// <c>PlayerInput</c> · <see cref="PlayerInputController"/> · <see cref="InputMapSwitcher"/>도
    /// 여기 함께 둔다.
    ///
    /// 몸을 실제로 움직이는 건 <see cref="PlayerControl"/>이다. 여기는 몸과 무관한 키만 본다.
    /// </summary>
    public class BattleCommander : MonoBehaviour
    {
        [SerializeField] private BulletTimeController bulletTime;
        [SerializeField] private TagSwapController swap;

        private void Awake()
        {
            if (bulletTime == null) bulletTime = FindAnyObjectByType<BulletTimeController>();
            if (swap == null) swap = FindAnyObjectByType<TagSwapController>();
        }

        /// <summary>
        /// 지휘 입력은 시간이 멈춰 있어도 받아야 한다. <c>TimeControl.Scale</c>은 자체 배율이라
        /// <c>Time.timeScale</c>을 건드리지 않으므로 Update는 정지 중에도 그대로 돈다
        /// (결정 로그 ⑥).
        /// </summary>
        private void Update()
        {
            PlayerInputController input = PlayerInputController.Instance;
            if (input == null) return;

            if (bulletTime != null)
            {
                // 진입과 실행이 한 키다(기본 E). Order 페이즈에서 이 키가 곧 실행이므로
                // 따로 부를 것이 없다 — 무엇을 할지는 전술 페이즈가 결정한다.
                if (input.BulletTimePressed)
                    bulletTime.Tactic.OnBulletTimeKey();

                // U — 손패 맨 왼쪽 카드 즉시 사용. RealTime 여부는 UseTopCard가 직접 본다.
                if (input.CardUsePressed)
                    bulletTime.UseTopCard();
            }

            // F — 태그 교대. 쿨타임과 "실시간에서만" 판정은 SwapNext가 직접 본다.
            if (swap != null && input.SwapPressed)
                swap.SwapNext();
        }
    }
}
