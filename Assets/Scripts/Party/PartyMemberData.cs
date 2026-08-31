using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 동료 한 명의 수치 · 장착 · 외형 표. <see cref="EnemyData"/>와 같은 자리에 있는 물건이다.
    ///
    /// <b>왜 만들었나.</b> 이 값들은 전부 씬에 놓인 <see cref="Ally"/> 인스턴스의
    /// <b>프리팹 오버라이드</b>로 살고 있었다. 동료 하나당 51개, 씬 아홉 개에 흩어져
    /// 약 1,800개가 손으로 유지되고 있었다 — 장착 카드 한 장을 바꾸려면 씬 아홉 개를 열어야 했다.
    ///
    /// 여기로 옮기면 그게 에셋 한 개 수정이 된다. 씬은 파티를 아예 들지 않는다
    /// (<see cref="PartyAssembler"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Prototype/Party Member Data", fileName = "Party_")]
    public class PartyMemberData : ScriptableObject
    {
        [Header("정체성")]
        public string memberId = "Member_01";

        [Tooltip("컷인 · 파티 선택 화면에 뜨는 이름.")]
        public string displayName = "동료";

        [Tooltip("직업. 장착 카드의 직업과 반드시 같아야 한다 — 스킬은 직업 전용이다.")]
        public Role role;

        [Tooltip("컷인에 뜨는 얼굴. 비우면 직업 색 박스로 대체된다.")]
        public Sprite portrait;

        [Header("몸")]
        [Tooltip("이 동료의 프리팹. 루트에 Ally 컴포넌트가 있어야 한다.\n" +
                 "비우면 PartyAssembler 의 기본 Ally 프리팹으로 떨어진다.\n\n" +
                 "여기에 제 프리팹을 꽂으면 아래 외형 칸(bodyScale · spriteTint · animatorController)은 " +
                 "대개 필요 없다 — 그건 공용 Ally 프리팹 하나를 색과 크기로만 구분하던 시절의 통로다. " +
                 "체형 · 콜라이더 · 애니메이션 구조가 다른 동료는 프리팹 쪽이 답이다.")]
        public GameObject prefab;

        [Header("장착 카드")]
        [Tooltip("직업당 6종 중 4장. 덱 목표 장수는 상수가 아니라 지금 인원 × 4다 — " +
                 "4인이면 16장, 3인이면 12장이 정상이다(DeckRules.TargetSize).\n" +
                 "여기 든 카드의 SkillData.role 이 위의 role 과 다르면 그 카드는 영영 발동하지 않는다 — " +
                 "BulletTimeController.ResolveCaster 가 직업으로 시전자를 찾기 때문이다.")]
        public List<ComboCard> equipped = new List<ComboCard>(Ally.EquipSlots);

        [Header("전투 — 0이면 프리팹 값을 그대로 둔다")]
        [Tooltip("최대 체력. 0이면 Ally 프리팹의 값.")]
        public float hp;

        [Tooltip("공격력. 0이면 Ally 프리팹의 값.")]
        public float atk;

        [Tooltip("이동 속도. 0이면 Ally 프리팹의 값.")]
        public float moveSpeed;

        [Header("평타 — 원거리")]
        [Tooltip("넣으면 평타가 투사체가 된다. 비우면 근접 평타 그대로.\n" +
                 "지금은 궁수 · 마법사만 채워져 있다.")]
        public Projectile basicProjectile;

        public float projectileSpeed = 16f;
        public float projectileRange = 9f;
        public int projectilePierce;

        [Header("외형")]
        [Tooltip("비우면 Ally 프리팹의 컨트롤러를 그대로 쓴다.\n" +
                 "EntityAnimator 가 Awake 에서 이걸 AnimatorOverrideController 로 감싸므로, " +
                 "주입은 반드시 그 전에 끝나야 한다 — PartyAssembler 가 몸을 비활성 상태로 만들어 " +
                 "표를 꽂은 뒤에 깨우는 이유다.")]
        public RuntimeAnimatorController animatorController;

        [Tooltip("몸 크기 배수. 1이면 프리팹 그대로.\n" +
                 "씬에서 View · Shadow 를 따로 키우던 값을 하나로 모은 것이다.")]
        public float bodyScale = 1f;

        [Tooltip("스프라이트에 얹는 직업 색. 흰색이면 건드리지 않는다.\n" +
                 "지금까지 ArtImportBuilder 가 씬의 Ally 인스턴스에 직접 칠하던 값이다 — " +
                 "씬에 파티가 없어졌으므로 여기가 새 집이다.")]
        public Color spriteTint = Color.white;

        /// <summary>스프라이트가 있는 자리. 프리팹 계층과 같아야 한다.</summary>
        public const string SpritePath = "View/Sprite";

        /// <summary>
        /// 장착 카드의 <b>복제본</b>. 반드시 이걸로 받아야 한다.
        ///
        /// <see cref="ComboCard"/>는 ScriptableObject가 아니라 <c>[Serializable]</c> 클래스라
        /// 에셋에 든 인스턴스를 그대로 <see cref="Ally.Equipped"/>에 꽂으면 참조가 공유된다.
        /// 그 상태에서 레벨업 합성이 황금 카드로 승급시키면(<see cref="ComboCard.AsGolden"/>)
        /// 그 결과가 <b>에셋에 눌러붙어</b> 다음 런까지 따라간다.
        /// </summary>
        public List<ComboCard> CloneCards()
        {
            var list = new List<ComboCard>(Ally.EquipSlots);
            if (equipped == null) return list;

            for (int i = 0; i < equipped.Count; i++)
                if (equipped[i] != null) list.Add(equipped[i].Clone());

            return list;
        }

        /// <summary>사람에게 보여줄 이름. 비어 있으면 에셋 이름으로 떨어진다.</summary>
        public string Label => !string.IsNullOrEmpty(displayName) ? displayName : name;
    }
}
