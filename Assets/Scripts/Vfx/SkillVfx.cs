using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스킬 하나의 이펙트 색·크기. <see cref="SkillData"/>가 들고 있다가
    /// 타격을 낼 때 히트박스로 흘려 보낸다.
    ///
    /// <b>기본값(<c>default</c>)은 "전역 설정을 쓴다"는 뜻이다</b> —
    /// 필드를 추가해도 기존 에셋이 검은색으로 터지지 않는다.
    /// </summary>
    [Serializable]
    public struct SkillVfx
    {
        [Tooltip("끄면 BattleVfx의 전역 색을 쓴다. 켜면 아래 값이 이 스킬에만 적용된다.")]
        public bool custom;

        [Tooltip("적중 순간 파편 색. 시전 이펙트도 이 색을 쓴다.")]
        public Color impact;
        [Tooltip("적중 순간 충격파 색. 파편보다 옅게 두면 겹쳐 보인다.")]
        public Color shock;

        [Tooltip("이펙트 크기 배율. 0이면 1로 본다 — 판정 크기는 건드리지 않는다.")]
        public float scale;

        [Header("전용 시트 (비우면 VfxLibrary 전역값)")]
        [Tooltip("시전 순간. 조준으로 찍은 자리에 뜬다.")]
        public VfxClip castClip;
        [Tooltip("적중 순간.")]
        public VfxClip hitClip;

        /// <summary>
        /// 이 타격이 스킬에서 나왔는지. <b>인스펙터에 노출하지 않는다</b> —
        /// <see cref="SkillState"/>가 히트박스로 넘기는 복사본에만 런타임으로 켠다.
        ///
        /// 히트박스는 평타와 공유된다(<c>Entity.SkillAttack</c>). 그래서 히트박스 쪽에서는
        /// 평타인지 스킬인지 알 방법이 없다. 색과 같은 경로로 이 표시를 같이 실어 보낸다.
        /// </summary>
        [NonSerialized] public bool fromSkill;

        /// <summary>0을 1로 접어 주는 읽기 창구. 인스펙터에서 안 채운 값이 이펙트를 지우지 않게.</summary>
        public float Scale => scale > 0f ? scale : 1f;

        /// <summary>스킬 경로로 넘길 복사본. 원본 에셋 값은 건드리지 않는다.</summary>
        public SkillVfx AsSkill()
        {
            SkillVfx v = this;
            v.fromSkill = true;
            return v;
        }

        /// <summary>인스펙터에서 custom을 켰을 때 출발점이 되는 값.</summary>
        public static SkillVfx Default => new SkillVfx
        {
            custom = false,
            impact = new Color(1f, 0.86f, 0.45f, 1f),
            shock = new Color(1f, 0.55f, 0.3f, 0.75f),
            scale = 1f,
        };
    }
}
