using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 동료 = 스킬의 주체. 실시간에는 손패 카드를, 불릿타임에는 콤보 큐를 받아 시전한다.
    /// 실제 움직임과 타격은 전부 <see cref="SkillState"/>가 수행한다.
    /// </summary>
    public class Ally : Entity
    {
        public const int EquipSlots = 4;

        [Header("표")]
        [Tooltip("이 동료의 수치 · 장착 · 외형. 비우면 아래 인스펙터 값이 그대로 쓰인다.\n\n" +
                 "PartyAssembler 가 이 몸을 만들면서 꽂아 주고, 이 컴포넌트의 Awake 가 적용한다. " +
                 "Enemy ↔ EnemyData 와 같은 관계다.\n\n" +
                 "몸 자체는 PartyMemberData.prefab 이 정한다 — 동료 전용 프리팹을 따로 만들어 두면 " +
                 "체형 · 콜라이더 · 애니메이션 계층을 자유롭게 짤 수 있다.")]
        [SerializeField] private PartyMemberData data;

        [Header("정체성")]
        [SerializeField] private Role role;

        [Tooltip("컷인에 뜨는 얼굴. 비우면 직업 색 박스로 대체된다.")]
        [SerializeField] private Sprite portrait;

        [Tooltip("직업당 6종 중 4장. 덱 16장의 1/4을 이룬다.")]
        [SerializeField] private List<ComboCard> equipped = new List<ComboCard>(EquipSlots);

        public Role Role => role;
        public Sprite Portrait => portrait;

        /// <summary>스킬 로그의 눈매 띠. 표(<see cref="PartyMemberData.skillLogFace"/>)에만 있다 — 없으면 null.</summary>
        public Sprite SkillLogFace => data != null ? data.skillLogFace : null;
        public IReadOnlyList<ComboCard> Equipped => equipped;

        /// <summary>이 동료가 물고 있는 표. 없으면 null — 인스펙터 값으로 도는 중이다.</summary>
        public PartyMemberData Data => data;

        /// <summary>
        /// 표를 꽂는다. <b>이 컴포넌트의 <c>Awake</c>보다 먼저</b> 불려야 한다 —
        /// 적용은 Awake 가 하기 때문이다.
        ///
        /// <see cref="PartyAssembler"/>가 그 순서를 <b>구조로</b> 보장한다: 몸을 비활성 임시
        /// 부모 밑에서 만들어(그러면 Unity 가 Awake 를 안 부른다) 여기를 부른 다음,
        /// 파티 컨테이너로 옮기며 깨운다.
        /// </summary>
        public void SetData(PartyMemberData source) => data = source;

        protected override void Awake()
        {
            base.Awake();
            ApplyData(data);
        }

        /// <summary>
        /// 표를 실제 컴포넌트에 밀어 넣는다. <see cref="Enemy.ApplyData"/>와 같은 구조다.
        ///
        /// <b>0 · null 은 "건드리지 않는다"는 뜻이다.</b> 표에 안 적힌 값까지 덮으면
        /// 프리팹 설정이 조용히 지워진다 — 근접 동료의 평타가 사라지는 식이다.
        ///
        /// <see cref="equipped"/>는 <b>반드시 복제본을 받는다</b>. ComboCard 는 ScriptableObject 가
        /// 아니라 <c>[Serializable]</c> 클래스라, 표의 인스턴스를 그대로 물면 레벨업 합성의
        /// 황금 승급이 에셋에 눌러붙어 다음 런까지 따라간다.
        /// </summary>
        public void ApplyData(PartyMemberData source)
        {
            data = source;
            if (data == null) return;

            // 직업이 먼저다. 아래 장착 카드의 검증 · 시전자 탐색이 전부 이 값을 본다.
            role = data.role;
            if (data.portrait != null) portrait = data.portrait;

            if (data.equipped != null && data.equipped.Count > 0)
                equipped = data.CloneCards();

            if (data.hp > 0f) Combat.SetMaxHealth(data.hp);
            if (data.atk > 0f) Stats.Set(StatType.AttackPower, data.atk);
            if (data.moveSpeed > 0f) Stats.Set(StatType.MoveSpeed, data.moveSpeed);

            // 투사체가 없는 표는 근접 그대로 둔다. null 로 덮으면 프리팹 설정이 지워진다.
            if (data.basicProjectile != null)
                ConfigureBasicProjectile(data.basicProjectile, data.projectileSpeed,
                                         data.projectileRange, data.projectilePierce);
        }

        /// <summary>
        /// 등록이 <c>Start</c>가 아니라 여기인 이유는 동료가 어시스트로 나왔다 들어갈 때마다
        /// <c>SetActive</c>로 켜지고 꺼지기 때문이다(<see cref="AssistManager"/>). Start에 두면
        /// 꺼진 채 시작한 동료는 영영 등록되지 않는다.
        /// </summary>
        private void OnEnable()
        {
            BattleRegistry.RegisterAlly(this);
        }

        /// <summary>
        /// 내려가기 전 뒷정리. 등록을 빼지 않으면 적 AI가
        /// <see cref="BattleRegistry.Allies"/>를 보고 화면에 없는 몸의 좌표로 걸어간다.
        /// 나머지는 <see cref="Entity.ReleaseBody"/>가 한다.
        /// </summary>
        private void OnDisable()
        {
            BattleRegistry.Unregister(this);
            ReleaseBody();
        }

        // ── 덱 구성 (포스트 배틀 전용) ────────────────────

        public bool EquipSkill(ComboCard card)
        {
            if (card == null || equipped.Count >= EquipSlots) return false;
            if (card.Data != null && card.Data.role != role) return false;

            equipped.Add(card);
            return true;
        }

        public bool RemoveSkill(ComboCard card)
        {
            return equipped.Remove(card);
        }

        /// <summary>
        /// 유저 조준이 없을 때 쓰는 자동 조준. 대상의 <b>좌표</b>만 뽑아 담는다 —
        /// 손패 카드가 조준 없이 발동할 때 쓴다.
        /// 대상 자체는 시전 순간 <see cref="SkillState.ResolveTarget"/>이 같은 규칙으로 다시 고른다.
        ///
        /// <b>동료의 판단이 아니다.</b> 자율 BT는 조종사가 몸 밖으로 나가면서 사라졌다 —
        /// 이건 유저가 조준을 생략했을 때 대신 채워 주는 편의 기능이다.
        ///
        /// 규칙은 둘뿐이다 — 가장 가까운 적, 가장 먼 적(<see cref="SkillData.targetPick"/>).
        /// 훑기도 부채꼴도 없다.
        /// </summary>
        public TargetInfo AutoTarget(SkillData data)
        {
            if (data == null) return TargetInfo.None;

            Entity target = BattleRegistry.PickEnemy(transform.position, data.targetPick);

            switch (data.targeting)
            {
                case TargetingType.GroundPoint:
                    return TargetInfo.Ground(target != null ? target.Physics.GroundPosition : Physics.GroundPosition);
                case TargetingType.Direction:
                    return TargetInfo.Dir(target != null
                        ? target.Physics.GroundPosition - Physics.GroundPosition
                        : Physics.Facing);
                default:
                    return TargetInfo.None;
            }
        }

    }
}
