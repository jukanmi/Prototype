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

        [Header("시전 범위 — 시전자 피격 범위 배수")]
        [Tooltip("근접 히트박스의 크기를 시전자 피격 콜라이더에 대한 배수로 적는다.\n\n" +
                 "· x — 가로(좌우 폭)\n" +
                 "· y — 높이\n" +
                 "· z — 세로(정면 깊이)\n\n" +
                 "기획서 '시전 범위' 행을 그대로 옮기는 자리다. 0이면 프리팹 SkillHitbox 크기를 그대로 쓴다.\n" +
                 "장판 · 투사체는 이 값을 보지 않는다 — 그쪽은 radius가 범위다.")]
        public Vector3 castRangeScale = Vector3.zero;

        /// <summary>시전 범위를 직접 정한 스킬인지. 셋 중 하나라도 0이면 프리팹 기본값으로 떨어진다.</summary>
        public bool HasCastRange => IsRangeSet(castRangeScale);

        [Tooltip("전방 부채꼴로 판정할 각도(도). 0이면 박스 히트박스를 쓴다.\n\n" +
                 "부채꼴일 때는 castRangeScale.x가 <b>반지름</b> 배수가 된다 — " +
                 "기획서 '반지름 = 플레이어 가로 범위 * 4 / 각도 = 60'이 x=4, 각도 60이다.\n" +
                 "사슬처럼 앞으로 길게 뻗되 옆으로는 안 닿아야 하는 판정에 쓴다.")]
        [Range(0f, 360f)] public float castConeAngle = 0f;

        /// <summary>전방 부채꼴로 때리는 스킬인지. 각도가 0이면 여전히 박스 히트박스다.</summary>
        public bool IsCone => castConeAngle > 0f;

        [Tooltip("타격 판정을 시전자 몸이 아니라 시전 시작 권적에 고정한다. " +
                 "파고드는 스킬(일섬)이 쓴다 — 시전자가 이미 건너뛴 뒤에 다단히트가 " +
                 "터지므로, 몸을 따라가면 지나온 공간이 아니라 도착지만 벤다.")]
        public bool fixedOrigin;

        /// <summary>
        /// 대상 <b>위로</b> 올라가 시전하는지. 기획서 '플레이어가 공중으로 이동 후 타격' 행이다.
        ///
        /// 따로 적지 않는다 — 뜬 적을 골라 치는 스킬(<see cref="TargetPick.NearestAerial"/>)은
        /// 그 적 위로 가는 것 말고 할 게 없다. 올라갈 <b>거리</b>도 새 값이 아니라
        /// <see cref="ApproachDistance"/>다: "대상에게서 얼마나 떨어져 서나"가 수평이든 수직이든
        /// 같은 질문이라, 방향만 여기서 정해 주면 된다.
        /// </summary>
        public bool CastsAboveTarget => targetPick == TargetPick.NearestAerial;

        /// <summary>
        /// 이 타가 실제로 쓸 범위 배수. 타별 값이 있으면 그쪽, 없으면 스킬 공통값이다.
        /// <see cref="ApproachDistance"/>와 같은 "0이면 접어 준다" 규약.
        /// </summary>
        public Vector3 RangeScaleFor(in HitData hit)
            => IsRangeSet(hit.castRangeScale) ? hit.castRangeScale : castRangeScale;

        private static bool IsRangeSet(Vector3 v) => v.x > 0f && v.y > 0f && v.z > 0f;

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

        /// <summary>
        /// <paramref name="index"/>번째 타가 나가는 시각(시전 시작 기준).
        ///
        /// <see cref="HitData.castDelay"/>가 있으면 그만큼, 없으면 <see cref="hitInterval"/>만큼
        /// 앞 사건에서 떨어진다. <b>첫 타의 castDelay는 castTime에 더해진다</b> —
        /// 기획서가 선딜을 두 단계로 적는 스킬(내려찍기 0.2 이동 + 0.3 시전)이 그 모양이다.
        ///
        /// 시전 타임라인의 <b>유일한 출처</b>다. SkillState와 TotalDuration이 같은 함수를 봐야
        /// 콤보 큐가 잡은 시간과 실제 타격 시각이 어긋나지 않는다.
        /// </summary>
        public float HitTime(int index)
        {
            float t = castTime;
            if (hitDataList == null) return t;

            for (int i = 0; i <= index && i < hitDataList.Count; i++)
            {
                float delay = hitDataList[i].castDelay;

                // 첫 타만 규칙이 다르다 — 간격이 아니라 castTime 위에 얹는 추가 선딜이다.
                if (i == 0) t += delay;
                else t += delay > 0f ? delay : hitInterval;
            }

            return t;
        }

        /// <summary>전체 소요 시간. 콤보 큐 타이밍 계산에 쓴다.</summary>
        public float TotalDuration
        {
            get
            {
                int last = Mathf.Max(0, (hitDataList?.Count ?? 0) - 1);
                return HitTime(last) + recoveryTime;
            }
        }
    }
}
