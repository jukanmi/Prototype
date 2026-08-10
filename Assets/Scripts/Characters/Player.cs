using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 커맨더. 직접 콤보를 치지 않는다.
    /// 시동기 적중이나 콤보 찬스를 보고 <b>수동으로</b> 불릿타임을 연다.
    /// </summary>
    public class Player : Entity
    {
        [SerializeField] private BulletTimeController bulletTime;
        [SerializeField] private Ally[] party = new Ally[4];

        private PlayerControl playerControl;

        public Ally[] Party => party;

        protected override void Awake()
        {
            base.Awake();
            playerControl = Control as PlayerControl;
            if (bulletTime == null) bulletTime = FindAnyObjectByType<BulletTimeController>();
        }

        protected override void Start()
        {
            base.Start();
            BattleRegistry.RegisterAlly(this);
        }

        // 시간이 멈춰도 지휘 입력은 받아야 한다.
        protected override bool ControlUsesUnscaledTime => true;

        protected override void Update()
        {
            base.Update();
            HandleCommanderInput();
        }

        private void HandleCommanderInput()
        {
            if (playerControl == null || bulletTime == null) return;

            // 키는 그대로 넘기고, 무엇을 할지는 전술 페이즈가 결정한다.
            if (playerControl.BulletTimePressed)
                bulletTime.Tactic.OnBulletTimeKey();

            if (playerControl.ExecutePressed)
                bulletTime.Tactic.OnExecuteKey();

            // Z — 손패 맨 왼쪽 카드 즉시 사용. RealTime 여부는 UseTopCard가 직접 본다.
            if (playerControl.CardUsePressed)
                bulletTime.UseTopCard();
        }

        private void OnDestroy()
        {
            BattleRegistry.Unregister(this);
        }
    }
}
