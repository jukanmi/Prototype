using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 불변 에셋. <b>에셋 1개 = 스킬 1개</b>.
    /// 런타임 상태는 <see cref="SkillState"/>가 따로 들고 있으므로
    /// 여러 동료가 같은 에셋을 동시에 써도 서로 간섭하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Prototype/Skill Data", fileName = "SK_")]
    public class SkillData : ScriptableObject
    {
        [Header("표시")]
        public string skillName;
        public Sprite icon;
        [TextArea] public string description;

        [Header("분류")]
        public Role role;
        public AttackType attackType;

        [Header("콤보 전이")]
        [Tooltip("선행 상태. Neutral이면 조건 없음.")]
        public CombatState requireState = CombatState.Neutral;
        [Tooltip("이 스킬이 만들어 내는 결과 상태. 예측 표시에 쓴다.")]
        public CombatState resultState = CombatState.LightHit;

        [Header("조준")]
        public TargetingType targeting = TargetingType.None;
        [Tooltip("GroundPoint 계열의 유효 반경. 프리뷰 원의 크기.")]
        public float radius = 3f;

        [Tooltip("자동 조준이 고를 상대. 훑기 · 부채꼴 없이 이 둘 중 하나로만 정해진다.\n\n" +
                 "· Nearest — 가장 가까운 적. 모으기 · 시동기 · 공격기의 기본.\n" +
                 "· Farthest — 가장 먼 적. 벽까지 밀 거리가 필요한 밀치기에 쓴다.")]
        public TargetPick targetPick = TargetPick.Nearest;

        [Header("타이밍")]
        [Tooltip("선딜 — 발동 준비 모션.")]
        public float castTime = 0.15f;
        [Tooltip("다단 히트 간격.")]
        public float hitInterval = 0.3f;
        [Tooltip("후딜 — 다음 슬롯으로 넘어가기 전 여유.")]
        public float recoveryTime = 0.2f;

        [Header("차징")]
        [Tooltip("모을 수 있는 최대 시간. 콤보가 이보다 길어도 더 세지지 않는다.")]
        public float maxChargeTime = 2.5f;
        [Tooltip("최대까지 모았을 때의 데미지 · 넉백 배율.")]
        public float maxChargeDamageMul = 2.5f;
        [Tooltip("최대까지 모았을 때의 범위 배율. 광역 효과에 곱해진다.")]
        public float maxChargeRadiusMul = 1.6f;

        /// <summary>
        /// 차징 스킬인지. <see cref="AttackType"/> 중 <b>유일하게 동작이 달라지는</b> 값이다 —
        /// 나머지는 분류 태그일 뿐이고 실제 판정은 hitDataList가 정한다.
        /// </summary>
        public bool IsCharge => attackType == AttackType.Charge;

        [Header("투사체")]
        [Tooltip("비우면 근접 — 시전자 앞 히트박스를 켠다. 넣으면 매 타격마다 하나씩 날아간다.")]
        public Projectile projectile;
        [Tooltip("초당 이동 거리.")]
        public float projectileSpeed = 14f;
        [Tooltip("이 거리를 날면 소멸한다.")]
        public float projectileRange = 12f;
        [Tooltip("몇 명을 더 뚫는지. 0이면 첫 적중에 소멸.")]
        public int projectilePierce = 0;

        public bool IsRanged => projectile != null;

        /// <summary>
        /// <see cref="radius"/>가 곧 타격 범위인지. 조준 링과 헛침 경고가 진실인 스킬.
        ///
        /// 원거리는 즉시 장판이든 투사체 도착 폭발이든 결국 radius만큼 터진다.
        /// 근거리만 예외 — 시전자 몸에 붙은 히트박스로 때린다.
        /// </summary>
        public bool UsesRadius => role != Role.Tanker && role != Role.Warrior;

        /// <summary>
        /// 기다리지 않고 <b>시전 즉시</b> 기준점 반경이 터지는 스킬인지.
        /// 투사체가 있으면 날아가서 터지므로 여기에 속하지 않는다.
        /// </summary>
        public bool IsAreaSkill => UsesRadius && !IsRanged;

        [Tooltip("시전 전에 대상에게서 이만큼까지 붙는다. 0이면 스킬 성격에서 자동으로 정해진다.\n\n" +
                 "· 근접 — 1.1 (바로 옆)\n" +
                 "· 장판 — radius의 절반\n" +
                 "· 투사체 — 사거리의 절반")]
        public float approachDistance = 0f;

        /// <summary>
        /// 0을 기본값으로 접어 주는 읽기 창구. 에셋 16개를 손으로 채우지 않아도 된다.
        ///
        /// <b>자동 발동은 이 값 하나로 끝난다</b> — "대상보다 멀면 여기까지 걸어 들어가 때린다".
        /// 직업별 분기(근접만 이동 / 원거리는 제자리)를 없앤 자리라, 원거리도 사거리 밖이면
        /// 들어오고 사거리 안이면 그대로 쏜다.
        /// </summary>
        public float ApproachDistance => approachDistance > 0f ? approachDistance : DefaultApproach;

        /// <summary>
        /// 성격에서 접근 거리를 유도한다. 근접은 몸이 닿아야 하고,
        /// 장판·투사체는 자기 유효 거리의 절반까지만 들어가면 빗나갈 일이 없다.
        /// </summary>
        private float DefaultApproach
        {
            get
            {
                if (IsRanged) return Mathf.Max(3f, projectileRange * 0.5f);
                if (IsAreaSkill) return Mathf.Max(1.5f, radius * 0.5f);
                return 1.1f;
            }
        }

        [Header("판정")]
        public List<HitData> hitDataList = new List<HitData>();

        [Tooltip("조합으로 스킬 개성을 만든다. switch 분기 대신 이 리스트를 쓴다.")]
        [SerializeReference] public List<ISkillEffect> effects = new List<ISkillEffect>();

        [Header("코스트")]
        [Tooltip("둘 다 구현해 두고 실험 후 하나로 정한다(결정 로그 ⑤).")]
        public float cooldown = 5f;
        public float manaCost = 20f;

        [Header("연출")]
        [Tooltip("시전자 본체 모션. 이펙트가 아니다 — Skill 슬롯 클립을 갈아끼운다.")]
        public AnimationClip animation;

        [Tooltip("타격마다 나오는 이펙트. custom을 끄면 전역 기본색을 쓴다.")]
        public SkillVfx vfx = SkillVfx.Default;

        /// <summary>
        /// 런타임 인스턴스를 매번 새로 만드는 팩토리.
        /// 에셋이 IState를 필드로 들고 있으면 동시 사용 시 상태가 꼬인다.
        /// </summary>
        public virtual IState CreateState(in SkillContext ctx)
        {
            return IsCharge
                ? new ChargeSkillState(this, in ctx)
                : new SkillState(this, in ctx);
        }

        /// <summary>이 스킬이 시동기(띄우기) 인지. 드로우 확률 보정에 쓴다.</summary>
        public bool IsStarterType => attackType == AttackType.Launcher;

        /// <summary>전체 소요 시간. 콤보 큐 타이밍 계산에 쓴다.</summary>
        public float TotalDuration
        {
            get
            {
                int hits = Mathf.Max(1, hitDataList.Count);
                return castTime + hitInterval * (hits - 1) + recoveryTime;
            }
        }
    }
}
