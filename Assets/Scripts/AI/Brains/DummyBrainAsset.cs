using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 아무것도 하지 않는 브레인. <b>허수아비 전용</b>이다.
    ///
    /// <see cref="EnemyControl"/>을 꺼 버리는 방법도 있지만 그러면 피격 반응 · 상태 색 ·
    /// 타겟 등록까지 같이 죽어서 "맞기만 하는 적"이 아니라 "없는 적"이 된다.
    /// 판단만 비우면 나머지 파이프라인은 실전과 완전히 같은 길을 탄다 —
    /// 훈련장에서 잰 값이 실전에서 그대로 나와야 하므로 그게 중요하다.
    /// </summary>
    [CreateAssetMenu(menuName = "Prototype/Brain/Dummy", fileName = "Brain_Dummy")]
    public class DummyBrainAsset : EnemyBrainAsset
    {
        public override EnemyIntent Decide(in EnemyBrainContext ctx) => EnemyIntent.None;
    }
}
