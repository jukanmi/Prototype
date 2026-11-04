// 스킬 전용 연출(참격 · 장판 · 사슬)의 계약. SkillState는 이것만 알고,
// 구현(SkillFx와 그 하위 연출들)은 맨 위 표현층(Vfx)이 SkillFxSink에 꽂는다.

using UnityEngine;

namespace Prototype
{
    /// <summary>스킬이 쓰는 전용 연출. <see cref="SkillData.fx"/>에서 고른다.</summary>
    public enum SkillFxKind
    {
        None,
        UpwardSlash,   // 올려베기
        SpinSlash,     // 회전베기
        FrostField,    // 서릿발
        FlashSlash,    // 일섬
        ComboSlash,    // 연격
        HookChain,     // 갈고리 사슬
        ChainBind,     // 사슬 속박
        MagicMissile,  // 마력 화살
        ManaRift,      // 마력 균열
        ShoulderCharge,// 숄더 차지
        ManaSpear,     // 마나 스피어
    }

    /// <summary>시전 한 번의 연출 타임라인. SkillState가 시전 · 매 프레임 · 타격마다 부른다.</summary>
    public interface ISkillFx
    {
        /// <summary>true면 투사체 본체를 숨긴다 — 연출이 투사체를 대신 그린다.</summary>
        bool HidesProjectile { get; }

        void OnCast();
        void Tick(float time);
        void OnHit(int index);
        void Release();
    }

    public interface ISkillFxFactory
    {
        /// <summary>이 스킬의 전용 연출. 없으면 null.</summary>
        ISkillFx Create(SkillData data, in SkillContext ctx);

        /// <summary>투사체의 렌더러를 끈다 (<see cref="ISkillFx.HidesProjectile"/>일 때).</summary>
        void HideRenderers(GameObject go);
    }

    /// <summary>구현이 꽂히는 자리. Vfx가 플레이 진입 · 에디터 로드 때 꽂는다. 비어 있으면 전용 연출 없이 시전한다.</summary>
    public static class SkillFxSink
    {
        public static ISkillFxFactory Current { get; set; }
    }
}
