using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 주인공 몸과 독립된 전투 지휘 입력 — 총공격키(기본 Space)로 총공격을 내고, E키로 총공격 슬롯 편집창을 연다.
    /// 둘 다 "눌렀다"만 전한다. 낼지 · 열지는 <see cref="BulletTimeController"/>와 편집창이 정한다.
    ///
    /// <b>절대 꺼지지 않는 오브젝트에 붙어야 한다</b>. 몸에 붙이면 몸이 꺼지는 순간 키까지 함께 죽는다.
    /// 같은 이유로 <c>PlayerInput</c> · <see cref="PlayerInputController"/> · <see cref="InputMapSwitcher"/>도
    /// 여기 함께 둔다.
    ///
    /// 몸을 실제로 움직이는 건 <see cref="PlayerPilot"/>이다. 여기는 몸과 무관한 키만 본다.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class BattleCommander : MonoBehaviour
    {
        [SerializeField] private BulletTimeController bulletTime;

        private void Awake()
        {
            if (bulletTime == null) bulletTime = FindAnyObjectByType<BulletTimeController>();
        }

        private void Update()
        {
            PlayerInputController input = PlayerInputController.Instance;
            if (input == null || input.GameplaySuspended || bulletTime == null) return;

            // 총공격은 키를 눌러야만 나간다 — 게이지 · 적 · 슬롯은 컨트롤러가 본다(AssaultRules).
            if (input.AssaultPressed)
                bulletTime.TryFireAssault();

            // 편집창은 칸 안 · 싸움 밖에서만 열린다(AssaultEditorUI.CanOpen).
            if (input.BulletTimePressed)
                bulletTime.OpenAssaultEditor();
        }
    }
}
