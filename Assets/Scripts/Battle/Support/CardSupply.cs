using System;
using System.Collections.Generic;

namespace Prototype
{
    /// <summary>
    /// 이 판의 카드 더미 셋(덱 · 손패 · 버린 더미)과 그 사이를 오가는 규칙.
    /// <see cref="BulletTimeController"/>가 소유하고, 언제 부를지는 그쪽(전술 페이즈)이 정한다.
    ///
    /// <b>여기는 "무엇을"만 한다.</b> 시간 · 게이지 · 입력은 모른다 — 그래서 씬 없이 검증할 수 있다.
    /// </summary>
    public sealed class CardSupply
    {
        private readonly UnityEngine.Object logContext;

        /// <summary>견본 손패. 비어 있지 않으면 덱을 거치지 않고 이 순서를 순환한다.</summary>
        private IReadOnlyList<SkillData> fixedHand;

        /// <summary>고정 손패에서 다음에 낼 카드의 인덱스. 순환한다.</summary>
        private int fixedCursor;

        public CardSupply(UnityEngine.Object logContext = null)
        {
            this.logContext = logContext;
        }

        public Deck Deck { get; } = new Deck();
        public Hand Hand { get; } = new Hand();
        public Discard Discard { get; } = new Discard();

        /// <summary>고정 손패가 켜져 있고 실제로 낼 카드가 있는지.</summary>
        public bool UsesFixedHand => fixedHand != null && fixedHand.Count > 0;

        /// <summary>견본 손패를 켠다. null이면 끈다.</summary>
        public void UseFixedHand(IReadOnlyList<SkillData> order)
        {
            fixedHand = order;
            fixedCursor = 0;
        }

        // ── 보충 ─────────────────────────────────────────

        /// <summary>덱에서 뽑아 손패를 4장까지 채운다. 덱이 비면 Discard가 섞여 되돌아온다.</summary>
        public void Refill()
        {
            if (UsesFixedHand)
            {
                RefillFixedHand();
                return;
            }

            int drawn = Hand.Refill(Deck, Discard);
            if (drawn > 0)
                BattleLog.Log(LogCategory.Deck,
                    $"손패 보충 {drawn}장 → {Hand.Count}장 | 덱 {Deck.Count} | Discard {Discard.Count}", logContext);
        }

        /// <summary>
        /// 견본 손패 보충. 덱·Discard를 아예 건드리지 않는다 —
        /// 여기서 나간 카드는 회수할 필요가 없다(<see cref="Recycle"/>가 거른다).
        /// </summary>
        private void RefillFixedHand()
        {
            int added = 0;

            // 리스트가 전부 null이면 아래 continue가 영원히 돌 수 있다.
            // 손패를 다 채우거나 리스트를 한 바퀴 다 훑으면 그만둔다.
            int tries = Hand.Size + fixedHand.Count;

            while (Hand.Count < Hand.Size && tries-- > 0)
            {
                SkillData data = fixedHand[fixedCursor];
                fixedCursor = (fixedCursor + 1) % fixedHand.Count;

                if (data == null) continue;
                if (!Hand.Add(new ComboCard(data))) break;

                added++;
            }

            if (added > 0)
                BattleLog.Log(LogCategory.Deck,
                    $"<b>견본 손패</b> 보충 {added}장 → {Hand.Count}장 | {DescribeHand()}", logContext);
        }

        /// <summary>손패를 "A → B → C" 한 줄로. 로그에서 체인 순서를 눈으로 확인하는 용도.</summary>
        private string DescribeHand()
        {
            var names = new string[Hand.Count];
            for (int i = 0; i < Hand.Count; i++)
            {
                SkillData d = Hand.Get(i).Data;
                names[i] = d != null ? d.skillName : "?";
            }

            return string.Join(" → ", names);
        }

        /// <summary>
        /// 다 쓴 카드를 버린 더미로. 견본 손패는 카드를 그 자리에서 찍어 내므로 회수하지 않는다 —
        /// 넣어 두면 아무도 뽑지 않는 더미만 무한히 커진다.
        /// </summary>
        public void Recycle(ComboCard card)
        {
            if (UsesFixedHand) return;
            Discard.Add(card);
        }

        // ── 실행 ─────────────────────────────────────────

        /// <summary>
        /// 손패를 통째로 실행 큐로 굳힌다. 왼쪽부터 순서대로 들어간다.
        /// 시전자를 못 찾은 카드는 발동하지 못하므로 그대로 Discard로 보낸다.
        /// </summary>
        public Queue<ComboSlot> TakeQueue(Func<Role, Ally> resolveCaster)
        {
            var q = new Queue<ComboSlot>(Hand.Size);
            List<ComboSlot> all = Hand.TakeAll();

            for (int i = 0; i < all.Count; i++)
            {
                ComboSlot s = all[i];
                SkillData data = s.Data;
                if (data == null)
                {
                    // 그냥 continue하면 카드가 덱에서 증발한다. Discard로 돌려보낸다.
                    Recycle(s.card);
                    continue;
                }

                Ally caster = resolveCaster(data.role);
                if (caster == null)
                {
                    BattleLog.Warn(LogCategory.Combo,
                        $"{data.skillName} 건너뜀 — {data.role} 동료가 파티에 없거나 사망", logContext);
                    Recycle(s.card);
                    continue;
                }

                s.caster = caster;
                if (!s.aimed || !s.target.IsValid)
                    s.target = caster.AutoTarget(data);

                q.Enqueue(s);
            }

            BattleLog.Log(LogCategory.Combo,
                $"실행 큐 생성 {q.Count}장 | " +
                string.Join(" → ", Array.ConvertAll(q.ToArray(), s => s.Data != null ? s.Data.skillName : "?")), logContext);

            return q;
        }

        // ── 걷어내기 ─────────────────────────────────────

        /// <summary>
        /// 한 직업의 카드를 덱 · 손패 · 버린 더미에서 통째로 걷어낸다.
        /// <b>버린 더미까지 지우는 게 핵심</b> — 남겨 두면 덱이 소진될 때 회수돼 되살아난다.
        /// 손패의 구멍은 부르는 쪽이 메운다(실행 중에는 메우면 안 된다).
        /// </summary>
        public int PurgeRole(Role role)
        {
            bool Match(ComboCard c) => c != null && c.Data != null && c.Data.role == role;

            int fromDeck = Deck.RemoveAll(Match);
            int fromHand = Hand.RemoveAll(Match);
            int fromDiscard = Discard.RemoveAll(Match);
            int total = fromDeck + fromHand + fromDiscard;

            BattleLog.Log(LogCategory.Deck,
                $"<b>{role} 카드 {total}장 제거</b> — 덱 {fromDeck} · 손패 {fromHand} · 버린 더미 {fromDiscard} → " +
                $"덱 {Deck.Count} · 손패 {Hand.Count} · 버린 더미 {Discard.Count}", logContext);

            return total;
        }

        // ── 덱 짓기 ──────────────────────────────────────

        /// <summary>
        /// 카드는 <b>Discard에 적재</b>하고 덱은 0장으로 둔다 — 첫 드로우가 회수 · 셔플을 겸한다.
        ///
        /// <b>런 덱이 있으면 그것이 이긴다.</b> 파티 장착 카드는 런의 첫 씨앗일 뿐이고,
        /// 그 뒤로는 레벨업으로 얻은 카드까지 든 <see cref="RunProgression.Cards"/>가 진짜 덱이다.
        /// 여기서 매번 파티로 새로 짜면 스테이지를 넘어가는 순간 얻은 카드가 전부 사라진다.
        /// </summary>
        public void Build(Player player, DeckStartupMode mode)
        {
            if (player == null) return;

            int empty = 0;
            List<ComboCard> partyCards = CollectPartyCards(player, ref empty);

            // 첫 씬에서 한 번만 씨를 뿌리고, 그 뒤로는 런 덱을 그대로 읽는다.
            // 테스트 모드는 그 씨앗을 <b>비운다</b> — 0장으로 시작해 레벨업으로만 카드가 들어온다.
            RunProgression run = RunProgression.Current;
            run.SeedDeck(mode == DeckStartupMode.Empty ? null : partyCards);

            // 장수가 아니라 Seeded로 가른다. 테스트 모드의 0장 덱을 "아직 안 짰다"로 읽으면
            // 파티 카드 16장이 도로 부어진다.
            List<ComboCard> cards = run.Seeded ? new List<ComboCard>(run.Cards) : partyCards;

            Deck.Clear();
            Discard.Clear();
            Discard.AddRange(cards);

            int members = DeckRules.CountFilled(player.Party);

            BattleLog.Log(LogCategory.Deck,
                $"덱 구성 완료 — {cards.Count}장을 Discard에 적재 (덱 0장에서 시작) | " +
                $"동료 {members}명 · 한 바퀴 {DeckRules.CycleHands(cards.Count)}핸드 | 런 Lv.{run.Level}", logContext);

            if (empty > 0)
                BattleLog.Warn(LogCategory.Deck,
                    $"<b>SkillData가 비어 있는 카드 {empty}장을 덱에서 제외했다.</b> " +
                    "Ally 인스펙터의 Equipped 항목에 스킬 에셋을 지정할 것.", logContext);

            // 장수는 <b>파티 장착분</b>으로 따진다. 런 덱은 레벨업으로 늘고 합성으로 줄어드는 게
            // 정상이라 여기서 세면 성장할 때마다 거짓 경고가 뜬다.
            //
            // 목표는 상수 16이 아니라 <b>지금 인원 × 4</b>다. 3인 파티는 12장이 정상이고,
            // 동료가 영구 사망해 슬롯이 비어도 마찬가지다 — 상수와 비교하면 그때부터
            // 매 스테이지 거짓 경고가 뜬다.
            string problem = DeckRules.Explain(partyCards.Count, members);
            if (problem != null)
                BattleLog.Warn(LogCategory.Deck, $"파티 {problem}", logContext);
        }

        /// <summary>파티 4명이 장착한 카드를 걷는다. SkillData가 빈 카드는 세어서 제외한다.</summary>
        public static List<ComboCard> CollectPartyCards(Player player, ref int empty)
        {
            var cards = new List<ComboCard>(Prototype.Deck.Size);

            foreach (Ally a in player.Party)
            {
                if (a == null) continue;

                foreach (ComboCard c in a.Equipped)
                {
                    if (c == null) continue;

                    // SkillData가 없는 카드는 영영 발동할 수 없다.
                    // 덱에 들이면 손패 맨 앞을 막아 U키가 먹통이 되므로 여기서 잘라 낸다.
                    if (c.Data == null)
                    {
                        empty++;
                        continue;
                    }

                    cards.Add(c);
                }
            }

            return cards;
        }

        // ── 보상 카드 ────────────────────────────────────

        /// <summary>
        /// 받은 카드를 <b>곧바로 손패에 꽂는다</b>. 합성이면 재료를 먼저 걷는다.
        /// 남은 칸 보충과 로그는 부르는 쪽 몫이다.
        /// </summary>
        public void Grant(ComboCard card, SkillData fusedFrom, int fuseCount)
        {
            if (fusedFrom != null && fuseCount > 0)
            {
                int eaten = ConsumeNormalCopies(fusedFrom, fuseCount);

                BattleLog.Log(LogCategory.Deck,
                    $"합성 재료 회수 — {fusedFrom.skillName} {eaten}/{fuseCount}장", logContext);
            }

            PlaceInHand(card);
        }

        /// <summary>
        /// 카드를 손패에 바로 꽂는다. 손패가 차 있으면 <b>맨 오른쪽</b> 칸을 덱으로 돌려보내고
        /// 그 자리를 내준다 — 왼쪽부터 발동하므로 오른쪽 끝이 유저의 다음 몇 수에 가장 영향이 적다.
        /// </summary>
        private void PlaceInHand(ComboCard card)
        {
            if (Hand.IsFull)
            {
                ComboSlot pushed = Hand.RemoveAt(Hand.Count - 1);

                // 밀려난 카드는 잃지 않는다. 덱 아래로 돌아가 제 차례에 다시 나온다.
                if (pushed.card != null) Deck.Add(pushed.card);
            }

            // 손패가 꽉 찬 채로 여기 오는 경우는 없지만, 실패해도 카드를 잃지는 않는다.
            if (!Hand.Add(card)) Discard.Add(card);
        }

        /// <summary>
        /// 합성 재료를 지금 판에서 걷어낸다. <b>버린 더미 → 덱 → 손패</b> 순서다 —
        /// 손패는 유저가 눈으로 짜 둔 순서라 마지막까지 아낀다.
        /// 황금은 재료가 아니므로 건드리지 않는다.
        /// </summary>
        private int ConsumeNormalCopies(SkillData data, int count)
        {
            int budget = count;

            bool Match(ComboCard c)
            {
                if (budget <= 0) return false;
                if (c == null || c.Golden || c.Data != data) return false;

                budget--;
                return true;
            }

            int removed = Discard.RemoveAll(Match);
            removed += Deck.RemoveAll(Match);
            removed += Hand.RemoveAll(Match);

            return removed;
        }
    }
}
