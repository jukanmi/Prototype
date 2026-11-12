// Actor 층(Entities)이 내놓는 계약 모음. 아래층에 두고 위층이 구현한다 — docs/uml/README.md 의 인터페이스 표.
using UnityEngine;

namespace Prototype
{
    // ══ IHittable ═══════════════════════════════════════════════════
    // 타격을 받는 쪽의 계약. Combat(Actor 층)을 시그니처에 쓰므로 Actor 층에 둔다.
    // HitData · DamageData 자체는 Kernel(Core/Support/HitData.cs)에 남는다.

    /// <summary>피격 판정을 받을 수 있는 대상. 상태 전이 · 넉백까지 포함한다.</summary>
    public interface IHittable
    {
        bool Hit(in HitData hitData, Combat attacker);
    }

    /// <summary>데미지만 받는 대상. 파괴 가능한 오브젝트 등.</summary>
    public interface IDamageable
    {
        void TakeDamage(in DamageData damageData);
    }

    // ══ IControl ════════════════════════════════════════════════════
    // 몸을 모는 쪽(조작 · AI)의 계약. Entity는 이것만 알고,
    // 구현(Control · EnemyControl · AssistControlModule …)은 Agent 층(Control 폴더)에 있다.
    public interface IControl
    {
        /// <summary>
        /// <c>MonoBehaviour.enabled</c> 그대로 — 인스펙터에서 꺼 둔 AI는 건너뛴다.
        /// MonoBehaviour 구현은 따로 쓸 것 없이 이 속성을 물려받아 만족한다.
        /// </summary>
        bool enabled { get; }

        void Tick(float dt);
    }

    // ══ IAnimatedState ══════════════════════════════════════════════
    // 애니메이션을 스스로 고르는 상태. EntityAnimator는 위층 상태(스킬 · 어시스트)를 모른다 —
    // 상태가 이 인터페이스를 구현하면 그쪽에 맡기고, 아니면 자기 표(StateToClip)로 재생한다.
    public interface IAnimatedState
    {
        /// <summary><see cref="EntityAnimator.PlayState"/> · <see cref="EntityAnimator.SetSkillClip"/> 등으로 재생한다.</summary>
        void Animate(EntityAnimator animator);
    }

    // ══ IChargeState ════════════════════════════════════════════════
    /// <summary>
    /// "지금 무언가를 모으고 있다"는 계약. 머리 위 게이지(<see cref="ChargeGauge"/>)가
    /// <see cref="Entity.ChargeState"/>로 읽어 그린다. 지금 구현은 보스 패턴(<see cref="EnemyPatternAction"/>)뿐이다.
    /// </summary>
    public interface IChargeState
    {
        /// <summary>아직 모으는 중인지.</summary>
        bool IsCharging { get; }
        /// <summary>0~1. 게이지가 얼마나 찼는지.</summary>
        float ChargeRatio { get; }
    }

    // ══ IGroundPlate ════════════════════════════════════════════════
    // 발판 하나의 계약. 높이를 묻는 GroundRegistry는 Actor 층(Physics가 씀)에 있고,
    // 실제 발판(GroundPlate · Stage 층)이 이 인터페이스로 자신을 등록한다.
    public interface IGroundPlate
    {
        GroundRect Rect { get; }
    }

    // ══ IBattleVfx ══════════════════════════════════════════════════
    // 전투 연출 창구(시전 · 적중 · 벽 바운드). 아래층(Entities · Skill)은 이 인터페이스로만 연출을 요청하고,
    // 맨 위 표현층(Vfx)이 구현을 꽂는다 — BattleVfx.InstallSink.
    // 본게임에서 연출을 갈아 끼울 때 바꿀 곳은 구현 하나다.
    public interface IBattleVfx
    {
        /// <summary>시전 순간. 조준으로 찍은 자리(<paramref name="ground"/>)에 뜬다.</summary>
        void Cast(Vector3 ground, float radius, in SkillVfx style);

        /// <summary>타격 파편. <paramref name="ground"/>는 바닥 좌표(y=0), <paramref name="height"/>는 터질 높이.</summary>
        void Impact(Vector3 ground, float height, Vector3 facing, float radius, in SkillVfx style);

        /// <summary>벽 바운드 파편. <paramref name="normal"/>은 벽 바깥쪽.</summary>
        void WallBounce(Vector3 ground, float height, Vector3 normal, float radius);
    }

    /// <summary>
    /// 구현이 꽂히는 자리. Vfx가 플레이 진입(BeforeSceneLoad)과 에디터 로드(InitializeOnLoadMethod) 때 꽂는다.
    /// 비어 있으면 연출 없이 지나간다.
    /// </summary>
    public static class BattleVfxSink
    {
        public static IBattleVfx Current { get; set; }
    }
}
