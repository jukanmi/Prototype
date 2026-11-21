using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 전투 칸을 깨면 바닥에 떨어지는 보상(금화 · 상자 · 스킬 두루마리)의 그림.
    ///
    /// 보상은 코드로 지어지므로 인스펙터에 그림을 물릴 자리가 없다 — <see cref="SkillCatalog"/>처럼
    /// <c>Assets/Data/Resources/</c>에 하나 두고 이름으로 읽는다. 비어 있어도 보상은 돈다(그림만 안 보인다).
    /// </summary>
    [CreateAssetMenu(menuName = "Prototype/Loot Art", fileName = ResourcePath)]
    public class LootArt : ScriptableObject
    {
        /// <summary><c>Assets/Data/Resources/</c> 아래 이 이름으로 둬야 잡힌다.</summary>
        public const string ResourcePath = "LootArt";

        [Header("금화 — 투사체 그림(원)을 빌려 쓴다")]
        [Tooltip("동전 몸과 그림자에 같이 쓴다.")]
        public Sprite coin;
        public Color coinColor = new Color(1f, 0.85f, 0.35f);
        [Tooltip("동전 가운데 한 겹 작은 원. 테두리가 생겨 납작한 원이 동전으로 읽힌다.")]
        public Color coinFaceColor = new Color(0.93f, 0.66f, 0.18f);

        [Header("상자")]
        public Sprite chestClosed;
        public Sprite chestOpen;
        [Tooltip("정예 칸의 상자. 비우면 일반 상자를 쓴다.")]
        public Sprite eliteChestClosed;
        public Sprite eliteChestOpen;

        [Header("스킬 두루마리 — 직업마다 하나")]
        public Sprite tankerScroll;
        public Sprite warriorScroll;
        public Sprite archerScroll;
        public Sprite wizardScroll;

        public Sprite ChestOf(bool elite, bool open)
        {
            Sprite eliteSprite = open ? eliteChestOpen : eliteChestClosed;
            if (elite && eliteSprite != null) return eliteSprite;
            return open ? chestOpen : chestClosed;
        }

        public Sprite ScrollOf(Role role)
        {
            switch (role)
            {
                case Role.Tanker:  return tankerScroll;
                case Role.Warrior: return warriorScroll;
                case Role.Archer:  return archerScroll;
                default:           return wizardScroll;
            }
        }

        private static LootArt cached;
        private static bool loaded;

        /// <summary>없으면 null. 한 번만 찾는다.</summary>
        public static LootArt Get()
        {
            if (loaded) return cached;

            loaded = true;
            cached = Resources.Load<LootArt>(ResourcePath);
            if (cached == null)
                Debug.LogWarning($"[Loot] Resources/{ResourcePath} 가 없다 — 보상은 돌지만 그림이 안 보인다.");

            return cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cached = null;
            loaded = false;
        }
    }
}
