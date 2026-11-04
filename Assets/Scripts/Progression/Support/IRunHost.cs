// 런의 주인(GameManager · App 층)이 넘겨주는 것. 런 데이터 · 전투 · 파티 조립은 이것만 알고
// GameManager를 모른다. 비어 있으면(스테이지 씬 단독 재생) 각자 단독 기본값으로 돈다.

namespace Prototype
{
    public interface IRunHost
    {
        /// <summary>지금 굴러가는 런.</summary>
        RunProgression Run { get; }

        /// <summary>전투 시작 시 덱을 어떻게 채우는가.</summary>
        DeckStartupMode DeckStartupMode { get; }

        /// <summary>파티 선택 화면에서 고른 조합. 안 골랐으면 null.</summary>
        PartyLoadout Loadout { get; }
    }

    /// <summary>구현이 꽂히는 자리. GameManager가 Awake에서 꽂고 OnDestroy에서 뺀다.</summary>
    public static class RunHost
    {
        public static IRunHost Current { get; set; }
    }
}
