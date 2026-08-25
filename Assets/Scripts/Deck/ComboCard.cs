using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 덱에 들어가는 카드 한 장. SkillData 에셋을 가리키고 드로우 보정만 따로 들고 있다.
    /// </summary>
    [Serializable]
    public class ComboCard
    {
        [SerializeField] private SkillData data;
        [Tooltip("드로우 가중치. 시동기가 안 나오는 패 꼬임을 막는 인챈트(선발투수 기믹).\n" +
                 "현재 미사용 — 드로우가 덱 맨 위에서 순서대로 가져가므로 셔플 단계에 편향을 주는 식으로 되살려야 한다.")]
        [SerializeField] private float drawWeight = 1f;

        [Tooltip("황금 카드. 데미지가 ExpRules.GoldenDamageMul배가 되고 테두리가 금색으로 뜬다.\n" +
                 "레벨업 2 · 3단계에서 뽑히거나, 같은 카드 세 장째를 합성하면 붙는다.")]
        [SerializeField] private bool golden;

        public SkillData Data => data;
        public float DrawWeight => Mathf.Max(0.01f, drawWeight);
        public bool Golden => golden;

        /// <summary>
        /// 이 카드로 낸 스킬의 데미지 배율. <see cref="SkillContext.damageScale"/>로 실려 나간다.
        /// 차징 배율과는 <b>곱해진다</b> — 둘은 서로 다른 층에서 붙는 값이다.
        /// </summary>
        public float DamageScale => golden ? ExpRules.GoldenDamageMul : 1f;

        /// <summary>시동기 여부. 지정하지 않으면 스킬의 공격 유형으로 판단한다.</summary>
        public bool IsStarter => data != null && data.IsStarterType;

        public Role Role => data != null ? data.role : Role.Tanker;

        public ComboCard(SkillData data, float drawWeight = 1f, bool golden = false)
        {
            this.data = data;
            this.drawWeight = drawWeight;
            this.golden = golden;
        }

        public ComboCard Clone() => new ComboCard(data, drawWeight, golden);

        /// <summary>같은 스킬의 황금판. 합성 결과를 만들 때 쓴다.</summary>
        public ComboCard AsGolden() => new ComboCard(data, drawWeight, true);
    }
}
