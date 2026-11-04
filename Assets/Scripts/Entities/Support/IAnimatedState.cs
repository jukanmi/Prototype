// 애니메이션을 스스로 고르는 상태. EntityAnimator는 위층 상태(스킬 · 어시스트)를 모른다 —
// 상태가 이 인터페이스를 구현하면 그쪽에 맡기고, 아니면 자기 표(StateToClip)로 재생한다.

namespace Prototype
{
    public interface IAnimatedState
    {
        /// <summary><see cref="EntityAnimator.PlayState"/> · <see cref="EntityAnimator.SetSkillClip"/> 등으로 재생한다.</summary>
        void Animate(EntityAnimator animator);
    }
}
