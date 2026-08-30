using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 주인공 한 명의 표. 태그 로스터 0번 슬롯이 쓴다.
    ///
    /// <b><see cref="PartyMemberData"/>와 담는 것이 다르다.</b> 동료는 스킬의 주체라
    /// 직업과 장착 카드 4장이 핵심이지만, 주인공은 <b>카드를 한 장도 안 낸다</b>
    /// (<c>BulletTimeController.CollectPartyCards</c>가 동료만 훑는다).
    /// 주인공이 담당하는 것은 평타와 <b>조작감</b>이다.
    ///
    /// 그래서 여기 든 값 중 가장 중요한 것은 <see cref="dashCooldown"/>과
    /// <see cref="attackBufferWindow"/>다 — 주인공을 고른다는 말의 실질이 거기 있다.
    ///
    /// <b>지금은 주인공이 하나뿐이라 이 표와 <c>Player.prefab</c>의 고정값이 같은 값이다.</b>
    /// 값을 내는 건 주인공을 여러 명 만들 때부터고, 그때 이 통로만 채우면 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "Prototype/Player Data", fileName = "Hero_")]
    public class PlayerData : ScriptableObject
    {
        [Header("정체성")]
        public string heroId = "Hero_01";
        public string displayName = "주인공";

        [Tooltip("파티 편성 화면과 컷인에 뜨는 얼굴.")]
        public Sprite portrait;

        [Header("전투 — 0이면 프리팹 값을 그대로 둔다")]
        public float hp;
        public float atk;
        public float moveSpeed;

        [Header("평타 타이밍 — 0이면 프리팹 값")]
        [Tooltip("선딜. 이 시점에 히트박스가 켜진다.")]
        public float basicAttackWindup;

        [Tooltip("히트박스가 꺼지는 시점.")]
        public float basicAttackActiveEnd;

        [Tooltip("후딜 포함 전체 길이. windup < activeEnd < total 순서를 지킬 것.")]
        public float basicAttackTotal;

        [Tooltip("평타 연타 단계. 비우면 프리팹의 단계를 그대로 쓴다.\n" +
                 "각 칸의 타이밍이 0이면 위의 기본 평타 값으로 떨어진다.")]
        public BasicAttackStage[] basicComboStages = new BasicAttackStage[0];

        [Header("조작감 — 0이면 프리팹 값")]
        [Tooltip("대시를 다시 쓸 수 있을 때까지의 시간. 캐릭터마다 다른 값이다.")]
        public float dashCooldown;

        [Tooltip("평타 선입력이 살아 있는 시간. 1타 캔슬 시점(cancelStart)보다 확실히 길게 잡을 것.")]
        public float attackBufferWindow;

        [Header("외형")]
        [Tooltip("비우면 Player 프리팹의 컨트롤러를 그대로 쓴다.\n" +
                 "EntityAnimator 가 Awake 에서 이걸 AnimatorOverrideController 로 감싸므로, " +
                 "주입은 반드시 그 전에 끝나야 한다 — PartyAssembler 가 -200 에서 도는 이유다.")]
        public RuntimeAnimatorController animatorController;

        [Tooltip("몸 크기 배수. 1이면 프리팹 그대로.")]
        public float bodyScale = 1f;

        [Tooltip("스프라이트에 얹는 색. 흰색이면 건드리지 않는다.")]
        public Color spriteTint = Color.white;

        public string Label => !string.IsNullOrEmpty(displayName) ? displayName : name;
    }
}
