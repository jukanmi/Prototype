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
        private PlayerInputController input;

        public Ally[] Party => party;

        protected override void Awake()
        {
            base.Awake();
            playerControl = Control as PlayerControl;
            input = GetComponent<PlayerInputController>();
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
            if (playerControl == null || bulletTime == null || input == null) return;

            // 지휘 입력은 시간이 멈춰 있어도 받아야 하므로 게이트를 거치지 않는다.
            // 그래서 PlayerControl을 통하지 않고 컨트롤러에서 곧바로 읽는다.
            //
            // 진입과 실행이 한 키다(기본 E · Space). Order 페이즈에서 OnBulletTimeKey가
            // 곧 실행이므로 따로 부를 것이 없다 — 무엇을 할지는 전술 페이즈가 결정한다.
            if (input.BulletTimePressed)
                bulletTime.Tactic.OnBulletTimeKey();

            // U — 손패 맨 왼쪽 카드 즉시 사용. RealTime 여부는 UseTopCard가 직접 본다.
            if (input.CardUsePressed)
                bulletTime.UseTopCard();

            // 라이브 페이즈 고유기 — 동료 4명에게 각각 매핑.
            // Resolve 중에는 막는다. 지휘받는 동료와 입력이 충돌한다.
            if (bulletTime.Phase == TacticPhase.RealTime && playerControl.SelfSkillPressed >= 0)
                CastSelfSkill(playerControl.SelfSkillPressed);
        }

        private void CastSelfSkill(int index)
        {
            if (index < 0 || index >= party.Length) return;

            Ally ally = party[index];
            if (ally != null) ally.CastSelfSkill();
        }

        private void OnDestroy()
        {
            BattleRegistry.Unregister(this);
        }
    }
}
