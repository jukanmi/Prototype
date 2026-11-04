// 도발을 받을 수 있는 쪽. TauntEffect(Skill)는 이것만 알고, 구현은 EnemyControl(Agent 층)이다.

namespace Prototype
{
    public interface ITauntable
    {
        /// <summary>타겟을 <paramref name="forced"/>로 고정한다.</summary>
        void SetTarget(Entity forced);
    }
}
