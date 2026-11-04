// 스테이지를 연 쪽(런 · GameManager)이 넘겨주는 이번 판의 조건.
// Stage는 이것만 알고 지도 칸(MapNode · Run 층)이나 GameManager(App 층)를 모른다.

namespace Prototype
{
    public interface IStageContext
    {
        /// <summary>들어와 있는 지도 칸이 있는가. 없으면 씬 단독 재생 · 런 밖.</summary>
        bool HasNode { get; }

        /// <summary>그 칸이 거는 보정. 칸이 없으면 <see cref="EncounterModifier.None"/>.</summary>
        EncounterModifier NodeModifier { get; }
    }

    /// <summary>구현이 꽂히는 자리. GameManager가 Awake에서 꽂고 OnDestroy에서 뺀다. 비어 있으면 저작 그대로 돈다.</summary>
    public static class StageContext
    {
        public static IStageContext Current { get; set; }
    }
}
