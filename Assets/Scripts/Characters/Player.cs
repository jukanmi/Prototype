using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 플레이어 캐릭터의 <b>몸</b>. 태그 로스터의 0번 슬롯이고, 나머지 슬롯인
    /// <see cref="Ally"/>와 같은 자격으로 필드에 서고 내려간다.
    ///
    /// 예전에는 여기가 입력과 지휘까지 들고 있었지만 전부 떼어 냈다 —
    /// 태그로 내려가면 <c>SetActive(false)</c>가 되는데 입력이 붙어 있으면
    /// 되돌아올 키까지 함께 죽기 때문이다. 지금 그 일은
    /// <see cref="BattleCommander"/>와 <see cref="TagSwapController"/>가 한다.
    ///
    /// 파티 명단이 아직 여기 남아 있는 것은 덱 구성(<see cref="BulletTimeController"/>)이
    /// 이 배열을 보기 때문이다.
    /// </summary>
    public class Player : Entity
    {
        [Header("표")]
        [Tooltip("이 주인공의 수치 · 평타 · 조작감 · 외형. 비우면 프리팹 값이 그대로 쓰인다.\n\n" +
                 "PartyAssembler 가 Awake(-200)에서 꽂아 주고, 이 컴포넌트의 Awake 가 적용한다. " +
                 "Ally ↔ PartyMemberData 와 같은 관계다.")]
        [SerializeField] private PlayerData data;

        [Tooltip("동료 4명. 태그 로스터는 이 앞에 플레이어를 붙여 5칸이 된다.")]
        [SerializeField] private Ally[] party = new Ally[4];

        public Ally[] Party => party;

        /// <summary>
        /// 파티 명단을 갈아 끼운다. <see cref="PartyAssembler"/>가 <c>Awake</c>(-200)에서 부른다 —
        /// 로드아웃이 안 채운 칸은 <b>몸을 아예 안 만들고</b> 여기 <c>null</c>로 남는다.
        ///
        /// <b>null 칸은 정상이다.</b> <see cref="TagSwapRules.IsSelectable"/>이 빈 칸을 건너뛰므로
        /// 3인 파티도 교대 순환이 그대로 돈다.
        ///
        /// <c>TagSwapController.BuildRoster</c> · <c>BulletTimeController.BuildDeck</c>이
        /// <c>Start</c>에서 이 배열을 읽는다. 그 전에 확정돼 있어야 한다.
        /// </summary>
        public void SetParty(Ally[] members)
        {
            party = members ?? new Ally[0];
        }

        /// <summary>이 주인공이 물고 있는 표. 없으면 null — 프리팹 값으로 도는 중이다.</summary>
        public PlayerData Data => data;

        /// <summary>
        /// 표를 꽂는다. <b>이 컴포넌트의 <c>Awake</c>보다 먼저</b> 불려야 한다 —
        /// 적용은 Awake 가 하기 때문이다. <see cref="PartyAssembler"/>가 몸을 비활성 상태로
        /// 만들어 여기를 부른 뒤 깨우므로, 그 순서는 구조로 보장된다.
        /// </summary>
        public void SetData(PlayerData source) => data = source;

        protected override void Awake()
        {
            // 연타 단계는 base.Awake보다 먼저 꽂는다. BuildStates가 AttackState를 만들면서
            // 단계 수를 읽고, EntityAnimator가 그 위에 오버라이드를 씌운다.
            if (data != null) ConfigureBasicCombo(data.basicComboStages);

            base.Awake();
            ApplyData(data);
        }

        /// <summary>
        /// 표를 실제 컴포넌트에 밀어 넣는다. <see cref="Ally.ApplyData"/>와 같은 구조이고
        /// <b>0 · null 은 "건드리지 않는다"</b>는 규칙도 같다.
        ///
        /// <b>장착 카드가 없다.</b> 주인공은 카드를 한 장도 안 낸다 —
        /// <c>BulletTimeController.CollectPartyCards</c>가 동료만 훑기 때문이고,
        /// 그래서 덱 장수 계산(<see cref="DeckRules"/>)도 주인공을 세지 않는다.
        /// </summary>
        public void ApplyData(PlayerData source)
        {
            data = source;
            if (data == null) return;

            if (data.hp > 0f) Combat.SetMaxHealth(data.hp);
            if (data.atk > 0f) Stats.Set(StatType.AttackPower, data.atk);
            if (data.moveSpeed > 0f) Stats.Set(StatType.MoveSpeed, data.moveSpeed);

            ConfigureBasicAttack(data.basicAttackWindup, data.basicAttackActiveEnd, data.basicAttackTotal);

            // 조작감은 몸에 붙은 Pilotable이 들고 있다 — 조종사는 몸을 갈아타므로
            // 거기 두면 전사로 대시하고 교대한 마법사가 그 쿨을 물려받는다.
            GetComponent<Pilotable>()?.ApplyData(data);
        }

        /// <summary>
        /// <see cref="Ally"/>와 같은 이유로 등록이 Start가 아니라 여기다 —
        /// 태그로 내려간 몸은 꺼져 있으므로 적의 후보에서 빠져야 한다.
        /// </summary>
        private void OnEnable()
        {
            BattleRegistry.RegisterAlly(this);
        }

        private void OnDisable()
        {
            BattleRegistry.Unregister(this);
            ReleaseBody();
        }
    }
}
