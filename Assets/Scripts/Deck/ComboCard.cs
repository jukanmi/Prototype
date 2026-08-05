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

        public SkillData Data => data;
        public float DrawWeight => Mathf.Max(0.01f, drawWeight);

        /// <summary>시동기 여부. 지정하지 않으면 스킬의 공격 유형으로 판단한다.</summary>
        public bool IsStarter => data != null && data.IsStarterType;

        public Role Role => data != null ? data.role : Role.Tanker;

        public ComboCard(SkillData data, float drawWeight = 1f)
        {
            this.data = data;
            this.drawWeight = drawWeight;
        }

        public ComboCard Clone() => new ComboCard(data, drawWeight);
    }
}
