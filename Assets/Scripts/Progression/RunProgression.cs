// 런 진행 — 경험치 규칙 · 보상 표 · 파티 상태 · 런 전체 진행.
// 런 하나가 굴러가는 동안의 지속 데이터를 한자리에 둔다.

using System.Collections.Generic;
using System;
using UnityEngine;

namespace Prototype
{
    // ══ RunProgression ═══════════════════════════════════════════

    /// <summary>
    /// 런 하나가 들고 가는 성장 상태 — 경험치 · 레벨 · <b>런 덱</b>.
    ///
    /// <see cref="UnityEngine.MonoBehaviour"/>가 아니다. 씬 오브젝트를 절대 참조하지 않는
    /// <c>GameManager</c>가 소유하므로, 스테이지 씬이 언로드돼도 여기 든 카드는 살아남는다.
    /// 이게 없으면 <c>BulletTimeController.BuildDeckFromParty</c>가 씬마다 덱을 새로 짜면서
    /// 레벨업으로 얻은 카드를 통째로 지운다.
    /// </summary>
    public class RunProgression
    {
        private readonly List<ComboCard> cards = new List<ComboCard>();

        /// <summary>
        /// Boot 씬을 거치지 않고 스테이지 씬만 단독으로 켰을 때 쓰는 런.
        ///
        /// <c>GameManager</c>가 없다고 경험치 · 레벨업이 통째로 죽으면 스테이지 하나만 띄워
        /// 감각을 보는 흐름에서 이 시스템을 아예 확인할 수 없다. 대신 씬을 넘어가지 못하므로
        /// 이 런은 그 판에서 끝난다.
        /// </summary>
        private static RunProgression standalone;

        /// <summary>
        /// 지금 굴러가는 런. <c>GameManager</c>가 있으면 그쪽이 주인이다.
        /// <b>절대 null이 아니다</b> — 호출부마다 null 검사를 흩뿌리지 않기 위해서다.
        /// </summary>
        public static RunProgression Current
        {
            get
            {
                GameManager game = GameManager.Instance;
                if (game != null) return game.Run;

                return standalone ?? (standalone = new RunProgression());
            }
        }

        /// <summary>Domain Reload가 꺼져 있으면 static이 플레이 세션을 넘어 살아남는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => standalone = null;

        public int Exp { get; private set; }
        public int Level { get; private set; } = ExpRules.FirstLevel;

        /// <summary>런 덱. 전투 씬이 뜰 때 이걸로 덱을 짠다.</summary>
        public IReadOnlyList<ComboCard> Cards => cards;

        /// <summary>덱에 카드가 한 장이라도 있는가.</summary>
        public bool HasDeck => cards.Count > 0;

        /// <summary>
        /// 시작 덱이 이미 정해졌는가. <b><see cref="HasDeck"/>과 다르다</b> —
        /// 테스트 모드는 0장으로 시작하므로, 장수로 판단하면 씬이 바뀔 때마다
        /// "아직 안 짰다"로 보고 파티 카드 16장을 도로 부어 버린다.
        /// </summary>
        public bool Seeded { get; private set; }

        /// <summary>경험치 · 레벨 · 덱 중 무엇이든 바뀌면 발화. UI가 이걸 보고 다시 그린다.</summary>
        public event Action OnChanged;

        // ── 경험치 ──────────────────────────────────────

        public void AddExp(int amount)
        {
            if (amount <= 0) return;

            Exp += amount;
            OnChanged?.Invoke();
        }

        public bool CanLevelUp(int tier) => ExpRules.CanAfford(Exp, Level, tier);

        /// <summary>지금 살 수 있는 가장 비싼 단계. 하나도 못 사면 -1.</summary>
        public int HighestAffordableTier => ExpRules.HighestAffordableTier(Exp, Level);

        /// <summary>이번 레벨에서 그 단계에 드는 경험치.</summary>
        public int CostOf(int tier) => ExpRules.CostOf(Level, tier);

        /// <summary>
        /// 그 단계로 레벨을 올린다. <b>그 단계 값만</b> 빠지고 남은 경험치는 그대로 있다.
        /// 단계와 무관하게 레벨은 +1이다 — 단계가 바꾸는 것은 가격과 황금 확률뿐이다.
        /// </summary>
        public bool Spend(int tier)
        {
            if (!CanLevelUp(tier)) return false;

            Exp -= ExpRules.CostOf(Level, tier);
            Level++;

            OnChanged?.Invoke();
            return true;
        }

        // ── 런 덱 ───────────────────────────────────────

        /// <summary>
        /// 시작 덱의 씨를 뿌린다. <b>런에 한 번뿐이다</b> — 두 번째 스테이지에서 다시 뿌리면
        /// 레벨업으로 얻은 카드가 지워진다. <paramref name="source"/>가 비어 있어도
        /// "정해졌다"는 사실은 남는다(테스트 모드의 0장 덱).
        /// </summary>
        public bool SeedDeck(IEnumerable<ComboCard> source)
        {
            if (Seeded) return false;

            Seeded = true;

            if (source != null)
                foreach (ComboCard c in source)
                {
                    if (c == null || c.Data == null) continue;
                    cards.Add(c.Clone());
                }

            OnChanged?.Invoke();
            return cards.Count > 0;
        }

        /// <summary>
        /// 덱을 통째로 갈아 끼운다. 디버그 모드(<see cref="DeckBuilderUI"/>)가 짠 덱이 들어오는 통로다.
        /// <see cref="SeedDeck"/>와 달리 이미 정해진 덱도 덮는다.
        /// </summary>
        public void SetDeck(IEnumerable<ComboCard> source)
        {
            cards.Clear();
            Seeded = true;

            if (source != null)
                foreach (ComboCard c in source)
                {
                    if (c == null || c.Data == null) continue;
                    cards.Add(c.Clone());
                }

            OnChanged?.Invoke();
        }

        /// <summary>
        /// 레벨업에서 고른 카드를 런 덱에 넣는다. 합성이면 기존 두 장이 사라진다.
        /// 실제로 들어간 카드를 돌려준다 — 부르는 쪽이 <b>같은 인스턴스</b>를 손패에도 꽂는다.
        /// </summary>
        public ComboCard Grant(in CardOffer pick)
        {
            ComboCard added = CardGrantRules.Apply(cards, in pick);
            if (added == null) return null;

            OnChanged?.Invoke();
            return added;
        }

        /// <summary>
        /// 한 직업의 카드를 <b>런 덱</b>에서 걷어낸다. 동료가 죽어 그 직업 시전자가
        /// 하나도 안 남았을 때 <c>BulletTimeController.HandleAllyDied</c>가 부른다.
        ///
        /// <b>그 판의 덱만 정리하면 부족하다.</b> <c>BulletTimeController.PurgeRole</c>은
        /// 지금 씬의 덱 · 손패 · 버린 더미를 비우지만, 다음 스테이지의 <c>BuildDeck</c>은
        /// 여기 있는 <see cref="Cards"/>를 다시 읽는다 — 여기서 안 걷으면 죽은 동료의 카드가
        /// 스테이지가 바뀌는 순간 통째로 되살아난다.
        /// </summary>
        public int PurgeRole(Role role)
        {
            int removed = cards.RemoveAll(c => c != null && c.Data != null && c.Data.role == role);
            if (removed > 0) OnChanged?.Invoke();

            return removed;
        }

        /// <summary>
        /// 스테이지를 넘어가는 파티의 몸 상태 — 잔여 체력과 생사.
        /// 런 덱과 같은 이유로 여기 산다(씬 오브젝트가 아니어야 스테이지를 넘어간다).
        /// </summary>
        public PartyState Party { get; } = new PartyState();

        /// <summary>새 런. <c>GameManager.StartNewRun</c>이 부른다.</summary>
        public void Reset()
        {
            Exp = 0;
            Level = ExpRules.FirstLevel;
            Seeded = false;
            cards.Clear();

            // 빠뜨리기 쉬운 자리다 — 안 비우면 새 런이 지난 런의 시체를 물고 시작한다.
            Party.Clear();

            OnChanged?.Invoke();
        }
    }

    // ══ ExpRules ═══════════════════════════════════════════

    /// <summary>
    /// 경험치와 레벨업 단계의 <b>수치 전부</b>. 순수 함수라 씬을 켜지 않고 검증한다
    /// (<see cref="EncounterClearRules"/> · <see cref="StageOutcomeRules"/>와 같은 이유).
    ///
    /// <code>
    ///   BaseCost(L)   = 10 × 1.1^(L-1)     레벨업할 때마다 다음 요구치가 1.1배
    ///   Cost(L, tier) = ceil(BaseCost(L) × [1.0, 1.5, 2.0][tier])
    ///   황금 확률      = [0%, 20%, 50%][tier]   선택지 카드 1장마다 따로 굴린다
    /// </code>
    ///
    /// <b>단계는 레벨 상승폭을 바꾸지 않는다</b> — 어느 단계로 올리든 레벨은 +1이다.
    /// 비싼 단계를 사는 이유는 오직 황금 카드 확률이고, 그래서 "싸게 여러 번 vs 모아서 크게"가
    /// 라운드마다 실제 선택이 된다. 단계가 레벨까지 더 올려 주면 3단계가 언제나 정답이 되어
    /// 선택이 사라진다.
    ///
    /// 소모는 <b>그 단계 값만</b> 빠진다. 남은 경험치는 다음 레벨업으로 이월된다.
    /// </summary>
    public static class ExpRules
    {
        /// <summary>1단계 · 2단계 · 3단계.</summary>
        public const int TierCount = 3;

        /// <summary>1레벨 1단계의 비용. 적 한 기가 떨어뜨리는 경험치와 같게 맞췄다.</summary>
        public const int BaseCost = 10;

        /// <summary>레벨이 오를 때마다 요구 경험치에 곱해지는 값.</summary>
        public const double LevelGrowth = 1.1;

        /// <summary>황금 카드의 데미지 배율.</summary>
        public const float GoldenDamageMul = 1.5f;

        /// <summary>레벨은 1부터 센다.</summary>
        public const int FirstLevel = 1;

        private static readonly double[] CostMul = { 1.0, 1.5, 2.0 };
        private static readonly float[] Golden = { 0f, 0.20f, 0.50f };

        /// <summary>
        /// 올림 직전에 빼 주는 오차 여유.
        ///
        /// <c>10 × 1.1</c>은 부동소수점에서 정확히 11이 아니라 11.000000000000002다.
        /// 그대로 올리면 2레벨 1단계가 11이 아니라 <b>12</b>가 되어 표와 어긋난다.
        /// </summary>
        private const double CeilEpsilon = 1e-6;

        public static bool IsValidTier(int tier) => tier >= 0 && tier < TierCount;

        /// <summary>이 레벨의 1단계 비용(올림 전 실수값).</summary>
        public static double BaseCostOf(int level)
            => BaseCost * Math.Pow(LevelGrowth, Math.Max(FirstLevel, level) - FirstLevel);

        /// <summary>이 레벨에서 그 단계로 올리는 데 드는 경험치. 잘못된 단계는 살 수 없게 int.MaxValue.</summary>
        public static int CostOf(int level, int tier)
            => IsValidTier(tier)
                ? (int)Math.Ceiling(BaseCostOf(level) * CostMul[tier] - CeilEpsilon)
                : int.MaxValue;

        /// <summary>이 단계에서 카드 한 장이 황금으로 나올 확률. 0~1.</summary>
        public static float GoldenChanceOf(int tier) => IsValidTier(tier) ? Golden[tier] : 0f;

        public static bool CanAfford(int exp, int level, int tier)
            => IsValidTier(tier) && exp >= CostOf(level, tier);

        /// <summary>지금 살 수 있는 가장 비싼 단계. 하나도 못 사면 -1.</summary>
        public static int HighestAffordableTier(int exp, int level)
        {
            for (int tier = TierCount - 1; tier >= 0; tier--)
                if (CanAfford(exp, level, tier)) return tier;

            return -1;
        }

        /// <summary>사람에게 보여 줄 번호. 0번 단계가 "1단계"다.</summary>
        public static int TierNumber(int tier) => tier + 1;
    }

    // ══ ExpRewards ═══════════════════════════════════════════

    /// <summary>
    /// 적이 죽었을 때 경험치를 누구에게 얼마나 주는지. 정책을 한 곳에 모아 둔다 —
    /// <see cref="Enemy"/>는 "죽었다"만 알리고 계산은 여기서 한다.
    /// </summary>
    public static class ExpRewards
    {
        /// <summary>
        /// 적 한 기의 사망 보상. <see cref="EnemyData.exp"/>가 0인 개체(훈련장 더미)는 그냥 넘어간다.
        /// </summary>
        public static void Award(Enemy enemy)
        {
            if (enemy == null || enemy.Data == null) return;

            RunProgression run = RunProgression.Current;

            int amount = enemy.Data.exp;
            if (amount <= 0) return;

            run.AddExp(amount);

            BattleLog.Log(LogCategory.State,
                $"{enemy.name} 처치 — 경험치 +{amount} (누적 {run.Exp}, Lv.{run.Level})", enemy);
        }
    }

    // ══ PartyState ═══════════════════════════════════════════

    /// <summary>
    /// 스테이지를 넘어가는 파티의 <b>몸 상태</b> — 잔여 체력과 생사.
    ///
    /// <b>왜 필요한가.</b> 스테이지가 바뀌면 씬이 통째로 새로 로드되고, <see cref="Combat"/>가
    /// 프리팹의 <c>maxHealth</c>로 <see cref="Energy"/>를 새로 만든다. 아무것도 안 하면
    /// <b>매 스테이지 전원이 풀피로 부활</b>한다 — 로그라이크식 소모전이 성립하지 않는다.
    ///
    /// <see cref="MonoBehaviour"/>가 아니다. <see cref="RunProgression"/>이 소유하고
    /// <c>GameManager</c>가 Boot 씬에서 들고 있으므로 씬을 넘어 살아남는다 —
    /// 런 덱이 살아남는 것과 정확히 같은 이유다.
    ///
    /// <b>슬롯 번호가 아니라 <see cref="PartyMemberData"/> 에셋을 키로 쓴다.</b>
    /// 편성 순서가 바뀌어도 따라오고, 죽은 동료의 슬롯이 사라진 뒤에도 기록이 남는다.
    /// </summary>
    public class PartyState
    {
        /// <summary>
        /// 주인공이 쓰러진 채로 스테이지를 넘겼을 때 되살아나는 체력 비율.
        ///
        /// <b>주인공만 예외다.</b> 동료의 사망은 런 끝까지 영구지만, 주인공은 태그 로스터 0번이자
        /// <c>FindAnyObjectByType&lt;Player&gt;()</c>가 다섯 군데에서 찾는 앵커라 없앨 수가 없다.
        /// 동료가 살아남아 스테이지를 클리어했는데 주인공만 영영 못 일어나면
        /// 그 다음 스테이지를 시작할 방법이 없다.
        /// </summary>
        public const float HeroReviveRatio = 0.3f;

        public readonly struct MemberState
        {
            public readonly float HpRatio;
            public readonly bool Dead;

            public MemberState(float hpRatio, bool dead)
            {
                HpRatio = hpRatio;
                Dead = dead;
            }
        }

        private readonly Dictionary<PartyMemberData, MemberState> members =
            new Dictionary<PartyMemberData, MemberState>();

        /// <summary>주인공의 잔여 체력 비율. 쓰러졌으면 <see cref="HeroReviveRatio"/>로 낮춰 담는다.</summary>
        public float HeroHpRatio { get; private set; } = 1f;

        /// <summary>한 번이라도 기록했는가. 로그와 디버그용이다.</summary>
        public bool HasSnapshot { get; private set; }

        // ── 읽기 ────────────────────────────────────────

        /// <summary>이 동료가 이번 런에서 이미 죽었는가. 기록이 없으면 살아 있다.</summary>
        public bool IsDead(PartyMemberData member)
            => member != null && members.TryGetValue(member, out MemberState s) && s.Dead;

        /// <summary>
        /// 이 동료가 물고 갈 체력 비율. 기록이 없으면 1(첫 스테이지).
        /// 죽은 동료는 물어볼 일이 없지만, 물어보면 0이다.
        /// </summary>
        public float HpRatioOf(PartyMemberData member)
        {
            if (member == null || !members.TryGetValue(member, out MemberState s)) return 1f;
            return s.Dead ? 0f : s.HpRatio;
        }

        public int DeadCount
        {
            get
            {
                int n = 0;
                foreach (KeyValuePair<PartyMemberData, MemberState> kv in members)
                    if (kv.Value.Dead) n++;

                return n;
            }
        }

        // ── 기록 ────────────────────────────────────────

        /// <summary>
        /// 지금 파티의 상태를 찍는다. <b>스테이지를 넘어가기 직전에</b> 한 번 부른다.
        ///
        /// <b>기존 기록을 지우지 않는다.</b> 이미 죽어 슬롯이 사라진 동료는
        /// <see cref="Player.Party"/>에 <c>null</c>로만 남아 누구였는지 알 수가 없다 —
        /// 통째로 갈아엎으면 그 사망 기록이 사라지고 다음 스테이지에서 되살아난다.
        ///
        /// <b>패배 후 재시작이 저절로 맞는 것도 이 시점 선택 덕분이다.</b> 기록은 클리어할 때만
        /// 남으므로, 지고 나서 같은 스테이지를 다시 하면 <b>그 스테이지를 시작할 때의 기록</b>이
        /// 그대로 다시 읽힌다. 되돌리는 코드가 따로 필요 없다.
        /// </summary>
        public void Capture(Player player)
        {
            if (player == null) return;

            HasSnapshot = true;

            HeroHpRatio = player.Combat != null && player.Combat.IsDead
                ? HeroReviveRatio
                : Ratio(player);

            int recorded = 0;
            int skipped = 0;

            foreach (Ally a in player.Party)
            {
                // null 칸은 "빈 편성"이거나 "이미 죽어 지워진 슬롯"이다. 둘 다 기록할 것이 없고,
                // 후자는 여기서 건드리면 안 되는 기존 기록을 이미 갖고 있다.
                if (a == null) continue;

                // 표가 없으면 키가 없어서 기록할 수가 없다 — 그 동료만 조용히 만피로
                // 되살아난다. 로드아웃 배선이 빠졌다는 신호이므로 조용히 넘기지 않는다.
                if (a.Data == null)
                {
                    skipped++;
                    continue;
                }

                bool dead = a.Combat != null && a.Combat.IsDead;
                Record(a.Data, Ratio(a), dead);
                recorded++;
            }

            if (skipped > 0)
                BattleLog.Warn(LogCategory.State,
                    $"동료 {skipped}명이 PartyMemberData 를 안 물고 있어 상태를 못 찍었다 — " +
                    "그 동료만 다음 스테이지에서 만피로 시작한다. PartyLoadout 배선을 확인할 것.", player);

            BattleLog.Log(LogCategory.State,
                $"파티 상태 기록 — 동료 {recorded}명 · 전사 누적 {DeadCount}명 · " +
                $"주인공 체력 {HeroHpRatio:P0}", player);
        }

        /// <summary>
        /// 한 명의 상태를 직접 적는다. <see cref="Capture"/>가 쓰는 통로이자
        /// <b>테스트가 들어오는 이음매</b>다.
        ///
        /// 에디트모드에서는 <c>Awake</c>가 안 돌아 <c>Combat.Die()</c>가 물리에서 터진다 —
        /// 즉 "죽은 동료"를 만들 방법이 없다(<see cref="TagSwapRules"/>가 술어를 받는 것과 같은 이유).
        /// 영구 사망 규칙이 이 시스템의 핵심인데 그것만 검증 못 하면 안 되므로 여기를 열어 둔다.
        ///
        /// <b>죽음은 되돌아가지 않는다.</b> 한 번 죽었다고 적힌 동료는 이후 어떤 기록으로도
        /// 되살아나지 않는다 — 되살아날 수 있으면 그건 A안(영구 사망)이 아니다.
        /// </summary>
        public void Record(PartyMemberData member, float hpRatio, bool dead)
        {
            if (member == null) return;

            HasSnapshot = true;

            if (IsDead(member)) return;

            members[member] = dead
                ? new MemberState(0f, true)
                : new MemberState(Mathf.Clamp(hpRatio, 0.01f, 1f), false);
        }

        /// <summary>
        /// 살아 있는 몸의 체력 비율. <b>0을 돌려주지 않는다</b> — 산 캐릭터를 0으로 복원하면
        /// 체력은 비었는데 <see cref="CombatState"/>는 살아 있는, 아무도 못 죽이는 몸이 된다.
        /// </summary>
        private static float Ratio(Entity e)
        {
            if (e == null || e.Combat == null || e.Combat.Health == null) return 1f;
            return Mathf.Clamp(e.Combat.Health.Ratio, 0.01f, 1f);
        }

        /// <summary>새 런. <see cref="RunProgression.Reset"/>이 부른다.</summary>
        public void Clear()
        {
            members.Clear();
            HeroHpRatio = 1f;
            HasSnapshot = false;
        }
    }
}
