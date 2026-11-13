// 영구 진행 — 런을 넘어 남는 것(기억 조각 · 강화 · 해금 · 기록)과 그 수치.
// 런 하나의 상태는 RunProgression, 런을 넘어가는 상태는 여기다.
// 계획서: docs/Lobby_Plan.md (3 · 4단계)

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Prototype
{
    // ══ MetaUpgrade ═══════════════════════════════════════════

    /// <summary>
    /// 유산 제단에서 사는 강화. <b>효과를 걸 자리가 이미 있는 것만</b> 1차로 둔다(계획서 1.4) —
    /// 최대 체력 · 게이지 시작치는 스탯 보정 경로가 생기면 더한다.
    /// 저장 파일에는 이름으로 들어가므로 순서를 바꿔도 된다.
    /// </summary>
    public enum MetaUpgrade
    {
        StartGold,
        ShopDiscount,
        RestHeal,
    }

    // ══ MetaRules ═══════════════════════════════════════════

    /// <summary>
    /// 영구 진행의 <b>수치 전부</b>. 지금은 임시값이다 — 밸런스는 나중에 이 파일만 고친다.
    /// 순수 함수라 씬 없이 검증한다.
    /// </summary>
    public static class MetaRules
    {
        /// <summary>끝낸 칸 하나당 기억 조각.</summary>
        public const int PerNode = 10;

        /// <summary>보스 층까지 끝냈을 때 더 주는 기억 조각.</summary>
        public const int BossBonus = 50;

        /// <summary>
        /// [처음부터]로 런을 버릴 때 주는 비율(%). 전액을 주면 "나쁜 지도면 리셋"이 공짜가 된다.
        /// 클리어 · 패배 · ESC 이탈은 100이다(계획서 1.3).
        /// </summary>
        public const int RestartPercent = 50;

        /// <summary>잠긴 동료 한 명 고용 값.</summary>
        public const int HireCost = 100;

        public const int UpgradeCostPerLevel = 50;
        public const int MaxUpgradeLevel = 3;

        // 효과량 — 단계당
        public const int StartGoldPerLevel = 30;
        public const int ShopDiscountPercentPerLevel = 10;
        public const float RestHealPerLevel = 0.1f;

        /// <summary>런 하나를 끝냈을 때 받는 기억 조각.</summary>
        public static int Reward(int completedNodes, bool bossCleared, int percent)
        {
            int full = Math.Max(0, completedNodes) * PerNode + (bossCleared ? BossBonus : 0);
            return full * Mathf.Clamp(percent, 0, 100) / 100;
        }

        /// <summary>지금 단계에서 다음 단계로 올리는 값. 최대 단계면 0.</summary>
        public static int UpgradeCost(int currentLevel)
            => currentLevel >= MaxUpgradeLevel ? 0 : UpgradeCostPerLevel * (currentLevel + 1);

        public static int StartGold(int level) => StartGoldPerLevel * Clamp(level);
        public static int ShopDiscountPercent(int level) => ShopDiscountPercentPerLevel * Clamp(level);
        public static float RestHealBonus(int level) => RestHealPerLevel * Clamp(level);

        /// <summary>할인을 건 값. 1보다 작아지지 않는다.</summary>
        public static int Discounted(int price, int discountPercent)
            => Math.Max(1, price * (100 - Mathf.Clamp(discountPercent, 0, 100)) / 100);

        public static string Label(MetaUpgrade u)
        {
            switch (u)
            {
                case MetaUpgrade.StartGold:    return "시작 골드";
                case MetaUpgrade.ShopDiscount: return "상점 할인";
                case MetaUpgrade.RestHeal:     return "휴식 회복량";
                default:                       return u.ToString();
            }
        }

        /// <summary>그 단계의 효과를 한 줄로.</summary>
        public static string Effect(MetaUpgrade u, int level)
        {
            switch (u)
            {
                case MetaUpgrade.StartGold:    return $"+{StartGold(level)} 골드";
                case MetaUpgrade.ShopDiscount: return $"-{ShopDiscountPercent(level)}%";
                case MetaUpgrade.RestHeal:     return $"+{RestHealBonus(level):0%}";
                default:                       return "";
            }
        }

        private static int Clamp(int level) => Mathf.Clamp(level, 0, MaxUpgradeLevel);
    }

    // ══ MetaSave ═══════════════════════════════════════════

    /// <summary>
    /// 저장 파일의 모양. <c>JsonUtility</c>가 읽고 쓰므로 필드만 둔다 —
    /// 딕셔너리 · 프로퍼티는 직렬화되지 않는다.
    /// </summary>
    [Serializable]
    public class MetaSave
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        /// <summary>기억 조각.</summary>
        public int currency;

        /// <summary>고용한 동료의 애셋 이름. 기본 조합 동료는 여기 없어도 열려 있다.</summary>
        public List<string> unlocked = new List<string>();

        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();

        /// <summary>끝낸 런 수.</summary>
        public int runs;

        /// <summary>가장 깊이 간 층 번호(1부터). 0이면 기록 없음.</summary>
        public int bestFloor;

        [Serializable]
        public struct UpgradeLevel
        {
            public string id;
            public int level;
        }
    }

    // ══ MetaProgression ═══════════════════════════════════════════

    /// <summary>
    /// 런을 넘어 남는 진행. <c>GameManager</c>가 소유하고 Awake에서 읽는다.
    ///
    /// <b>쓰는 시점은 두 번뿐이다</b> — 런 정산 직후와 구매 직후. 씬 전환마다 쓰지 않는다.
    /// <b>플레이 중에만 디스크를 만진다.</b> EditMode 테스트가 사용자의 진짜 저장 파일을 덮으면 안 된다.
    /// </summary>
    public class MetaProgression
    {
        public const string FileName = "meta.json";

        private MetaSave data = new MetaSave();

        /// <summary>
        /// 지금 진행. <c>GameManager</c>가 꽂는다. 없으면(씬 단독 실행 · 테스트) 빈 판 —
        /// 강화가 하나도 없는 상태라 효과를 읽는 자리가 null 검사를 안 해도 된다.
        /// </summary>
        public static MetaProgression Current
        {
            get => current ?? (current = new MetaProgression());
            set => current = value;
        }

        private static MetaProgression current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => current = null;

        public int Currency => data.currency;
        public int Runs => data.runs;
        public int BestFloor => data.bestFloor;

        /// <summary>
        /// 마을이 아직 안 띄운 정산 합계. 없으면 -1. [처음부터]는 마을을 안 거치므로
        /// 다음에 마을에 들어올 때 함께 띄운다 — 그래서 덮지 않고 더한다.
        /// </summary>
        public int LastSettlement { get; private set; } = -1;

        public void ClearLastSettlement() => LastSettlement = -1;

        public event Action OnChanged;

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        // ── 강화 · 해금 ─────────────────────────────────

        public int LevelOf(MetaUpgrade u)
        {
            string id = u.ToString();
            foreach (MetaSave.UpgradeLevel e in data.upgrades)
                if (e.id == id) return e.level;
            return 0;
        }

        /// <summary>한 단계 올린다. 최대 단계거나 기억 조각이 모자라면 거짓.</summary>
        public bool TryUpgrade(MetaUpgrade u)
        {
            int level = LevelOf(u);
            int cost = MetaRules.UpgradeCost(level);
            if (cost <= 0 || !TrySpend(cost)) return false;

            string id = u.ToString();
            data.upgrades.RemoveAll(e => e.id == id);
            data.upgrades.Add(new MetaSave.UpgradeLevel { id = id, level = level + 1 });

            Commit();
            return true;
        }

        /// <summary>
        /// 이 동료를 편성에 넣을 수 있는가. 기본 조합 동료는 처음부터 열려 있다(계획서 1.5).
        /// </summary>
        public bool IsUnlocked(PartyMemberData member)
        {
            if (member == null) return false;
            if (data.unlocked.Contains(member.name)) return true;

            PartyLoadout def = PartyCatalog.Default();
            if (def == null || def.members == null) return true;   // 기본 조합이 없으면 잠글 기준이 없다

            foreach (PartyMemberData m in def.members)
                if (m == member) return true;

            return false;
        }

        /// <summary>고용. 이미 열려 있거나 기억 조각이 모자라면 거짓.</summary>
        public bool TryHire(PartyMemberData member)
        {
            if (member == null || IsUnlocked(member)) return false;
            if (!TrySpend(MetaRules.HireCost)) return false;

            data.unlocked.Add(member.name);
            Commit();
            return true;
        }

        private bool TrySpend(int amount)
        {
            if (amount < 0 || data.currency < amount) return false;
            data.currency -= amount;
            return true;
        }

        // ── 런 정산 ─────────────────────────────────────

        /// <summary>
        /// 런 하나를 끝낸다. 끝낸 칸 수만큼 기억 조각을 쌓고 기록을 갱신한 뒤 저장한다.
        /// <paramref name="progress"/>가 null이면 아무것도 안 한다 — 두 번 정산 방지는 부르는 쪽이
        /// 정산 직후 진행을 버리는 것으로 한다.
        /// </summary>
        public int Settle(RunMapProgress progress, int percent)
        {
            if (progress == null) return 0;

            int reward = MetaRules.Reward(progress.Visited.Count, progress.IsFinished, percent);

            data.currency += reward;
            data.runs++;
            data.bestFloor = Math.Max(data.bestFloor, progress.Visited.Count);
            LastSettlement = Math.Max(0, LastSettlement) + reward;

            Commit();
            return reward;
        }

        // ── 디스크 ──────────────────────────────────────

        /// <summary>
        /// 파일을 읽는다. 없으면 새 판. <b>깨졌으면 <c>.bak</c>으로 옮기고</b> 새 판 —
        /// 조용히 덮으면 진행이 날아가고 원인도 못 찾는다.
        /// </summary>
        public void Load()
        {
            data = new MetaSave();
            if (!Application.isPlaying) return;

            string path = FilePath;
            if (!File.Exists(path)) return;

            try
            {
                MetaSave loaded = JsonUtility.FromJson<MetaSave>(File.ReadAllText(path));
                if (loaded == null) throw new InvalidDataException("빈 파일");

                loaded.unlocked = loaded.unlocked ?? new List<string>();
                loaded.upgrades = loaded.upgrades ?? new List<MetaSave.UpgradeLevel>();
                data = loaded;

                Debug.Log($"[Meta] 불러옴 — 기억 조각 {data.currency} · 런 {data.runs} · 최고 {data.bestFloor}층");
            }
            catch (Exception e)
            {
                string backup = path + ".bak";
                try { File.Copy(path, backup, overwrite: true); } catch { /* 백업도 못 하면 경고만 */ }
                Debug.LogWarning($"[Meta] 저장 파일을 못 읽었다 — {backup}로 옮기고 새로 시작한다. ({e.Message})");
            }
        }

        private void Commit()
        {
            OnChanged?.Invoke();
            if (!Application.isPlaying) return;

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Meta] 저장 실패 — {FilePath} ({e.Message})");
            }
        }
    }
}
