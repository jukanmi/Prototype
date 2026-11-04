// 전투 연출 창구(시전 · 적중 · 벽 바운드). 아래층(Entities · Skill)은 이 인터페이스로만 연출을 요청하고,
// 맨 위 표현층(Vfx)이 구현을 꽂는다 — BattleVfx.InstallSink.
// 본게임에서 연출을 갈아 끼울 때 바꿀 곳은 구현 하나다.

using UnityEngine;

namespace Prototype
{
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
