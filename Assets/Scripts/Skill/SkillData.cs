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

        [Header("타이밍")]
        [Tooltip("선딜 — 발동 준비 모션.")]
        public float castTime = 0.15f;
        [Tooltip("다단 히트 간격.")]
        public float hitInterval = 0.3f;
        [Tooltip("후딜 — 다음 슬롯으로 넘어가기 전 여유.")]
        public float recoveryTime = 0.2f;

        [Header("판정")]
        public List<HitData> hitDataList = new List<HitData>();

        [Tooltip("조합으로 스킬 개성을 만든다. switch 분기 대신 이 리스트를 쓴다.")]
        [SerializeReference] public List<ISkillEffect> effects = new List<ISkillEffect>();

        [Header("코스트")]
        [Tooltip("둘 다 구현해 두고 실험 후 하나로 정한다(결정 로그 ⑤).")]
        public float cooldown = 5f;
        public float manaCost = 20f;

        [Header("연출")]
        public AnimationClip animation;

        /// <summary>
        /// 런타임 인스턴스를 매번 새로 만드는 팩토리.
        /// 에셋이 IState를 필드로 들고 있으면 동시 사용 시 상태가 꼬인다.
        /// </summary>
        public virtual IState CreateState(in SkillContext ctx)
        {
            return new SkillState(this, in ctx);
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
