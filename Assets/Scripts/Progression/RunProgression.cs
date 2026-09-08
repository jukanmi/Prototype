using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
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
}
