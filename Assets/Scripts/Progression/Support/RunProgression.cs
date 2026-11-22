// 런 진행 — 골드 · 런 덱 · 보상 표 · 파티 상태.
// 런 하나가 굴러가는 동안의 지속 데이터를 한자리에 둔다.

using System.Collections.Generic;
using System;
using UnityEngine;

namespace Prototype
{
    // ══ RunProgression ═══════════════════════════════════════════

    /// <summary>
    /// 런 하나가 들고 가는 상태 — 골드 · <b>런 덱</b>.
    ///
    /// <see cref="UnityEngine.MonoBehaviour"/>가 아니다. 씬 오브젝트를 절대 참조하지 않는
    /// <c>GameManager</c>가 소유하므로, 스테이지 씬이 언로드돼도 여기 든 카드는 살아남는다.
    /// 이게 없으면 <c>BulletTimeController.BuildDeckFromParty</c>가 씬마다 덱을 새로 짜면서
    /// 보상으로 얻은 카드를 통째로 지운다.
    /// </summary>
    public class RunProgression
    {
        /// <summary>보유 스킬. <b>스킬이름:개수</b>로만 저장한다(합성 레벨은 <see cref="SkillFusion"/>이 개수에서 뽑는다).</summary>
        private readonly Dictionary<string, int> skillCounts = new Dictionary<string, int>();

        /// <summary>이름 → 에셋. 저장 대상이 아니라 이번 세션에 본 에셋을 이름으로 되찾는 조회용이다.</summary>
        private readonly Dictionary<string, SkillData> skillLookup = new Dictionary<string, SkillData>();

        /// <summary>총공격 슬롯(스킬 이름). 빈 칸은 null. 뒤 칸이 앞으로 당겨지지 않는다.</summary>
        private readonly string[] assaultSlots = new string[AssaultSlotCount];

        /// <summary>총공격 슬롯 수.</summary>
        public const int AssaultSlotCount = 4;

        /// <summary>
        /// Boot 씬을 거치지 않고 스테이지 씬만 단독으로 켰을 때 쓰는 런.
        ///
        /// <c>GameManager</c>가 없다고 골드 · 보상이 통째로 죽으면 스테이지 하나만 띄워
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
                IRunHost host = RunHost.Current;
                if (host != null) return host.Run;

                return standalone ?? (standalone = new RunProgression());
            }
        }

        /// <summary>Domain Reload가 꺼져 있으면 static이 플레이 세션을 넘어 살아남는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => standalone = null;

        /// <summary>
        /// 런 골드. 전투 칸을 깨면 떨어지는 금화를 주워 들어오고(<see cref="GoldRules.StageClearReward"/>)
        /// 상점 · 이벤트 칸에서 나간다. 적을 잡을 때는 안 들어온다.
        /// </summary>
        public int Gold { get; private set; }

        /// <summary>
        /// 보유 스킬의 종류별 카드 하나씩(읽기용 파생 뷰). 개수는 <see cref="CountOf"/>.
        /// 저장소가 아니다 — 부를 때마다 새로 만든다.
        /// </summary>
        public IReadOnlyList<ComboCard> Cards
        {
            get
            {
                var list = new List<ComboCard>(skillCounts.Count);
                foreach (KeyValuePair<string, int> kv in skillCounts)
                {
                    SkillData d = Resolve(kv.Key);
                    if (d != null) list.Add(new ComboCard(d, CardGrantRules.WeightOf(d)));
                }

                return list;
            }
        }

        /// <summary>덱에 카드가 한 장이라도 있는가.</summary>
        public bool HasDeck => skillCounts.Count > 0;

        /// <summary>보유 스킬 이름 → 개수.</summary>
        public IReadOnlyDictionary<string, int> SkillCounts => skillCounts;

        /// <summary>총공격 슬롯. 길이 <see cref="AssaultSlotCount"/> 고정, 빈 칸은 null.</summary>
        public IReadOnlyList<string> AssaultSlots => assaultSlots;

        public int CountOf(string skillName)
            => skillName != null && skillCounts.TryGetValue(skillName, out int n) ? n : 0;

        /// <summary>이름으로 에셋을 찾는다. 이번 세션에 본 적 없으면 <see cref="SkillCatalog"/>에서 찾는다.</summary>
        public SkillData Resolve(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return null;
            if (skillLookup.TryGetValue(skillName, out SkillData d) && d != null) return d;

            SkillCatalog catalog = SkillCatalog.Get();
            if (catalog != null)
                foreach (SkillData s in catalog.Skills)
                    if (s != null && s.skillName == skillName)
                    {
                        skillLookup[skillName] = s;
                        return s;
                    }

            return null;
        }

        /// <summary>스킬을 <paramref name="count"/>개 더한다(음수면 뺀다). 0 이하가 되면 지운다.</summary>
        public void AddSkill(SkillData data, int count = 1)
        {
            if (data == null || string.IsNullOrEmpty(data.skillName) || count == 0) return;

            skillLookup[data.skillName] = data;
            int next = CountOf(data.skillName) + count;

            if (next > 0) skillCounts[data.skillName] = next;
            else
            {
                skillCounts.Remove(data.skillName);
                ClearSlotsOf(data.skillName);
            }

            OnChanged?.Invoke();
        }

        /// <summary>한 칸을 채운다(null이면 비운다). 빈 칸을 비워도 뒤 칸은 안 당긴다.</summary>
        public bool SetAssaultSlot(int index, string skillName)
        {
            if (index < 0 || index >= AssaultSlotCount) return false;
            if (skillName != null && CountOf(skillName) <= 0) return false;

            // 한 스킬(합성된 한 장)은 한 슬롯에만 들어간다. 다른 칸에 있던 건 이쪽으로 옮겨 온다.
            if (skillName != null) ClearSlotsOf(skillName);

            assaultSlots[index] = skillName;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>가장 앞 빈 칸에 넣는다(push_back). 가득 찼으면 -1.</summary>
        public int PushAssaultSlot(string skillName)
        {
            if (CountOf(skillName) <= 0 || IsInAssaultSlot(skillName)) return -1;

            for (int i = 0; i < AssaultSlotCount; i++)
                if (assaultSlots[i] == null)
                {
                    assaultSlots[i] = skillName;
                    OnChanged?.Invoke();
                    return i;
                }

            return -1;
        }

        /// <summary>두 칸을 맞바꾼다(빈 칸 포함).</summary>
        public void SwapAssaultSlots(int a, int b)
        {
            if (a < 0 || b < 0 || a >= AssaultSlotCount || b >= AssaultSlotCount || a == b) return;

            (assaultSlots[a], assaultSlots[b]) = (assaultSlots[b], assaultSlots[a]);
            OnChanged?.Invoke();
        }

        public bool IsInAssaultSlot(string skillName)
        {
            if (skillName == null) return false;
            for (int i = 0; i < AssaultSlotCount; i++)
                if (assaultSlots[i] == skillName) return true;

            return false;
        }

        private void ClearSlotsOf(string skillName)
        {
            for (int i = 0; i < AssaultSlotCount; i++)
                if (assaultSlots[i] == skillName) assaultSlots[i] = null;
        }

        private void ClearSkills()
        {
            skillCounts.Clear();
            for (int i = 0; i < AssaultSlotCount; i++) assaultSlots[i] = null;
        }

        /// <summary>
        /// 시작 덱이 이미 정해졌는가. <b><see cref="HasDeck"/>과 다르다</b> —
        /// 테스트 모드는 0장으로 시작하므로, 장수로 판단하면 씬이 바뀔 때마다
        /// "아직 안 짰다"로 보고 파티 카드 16장을 도로 부어 버린다.
        /// </summary>
        public bool Seeded { get; private set; }

        /// <summary>골드 · 덱 중 무엇이든 바뀌면 발화. UI가 이걸 보고 다시 그린다.</summary>
        public event Action OnChanged;

        // ── 골드 ────────────────────────────────────────

        public void AddGold(int amount)
        {
            if (amount <= 0) return;

            Gold += amount;
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 가진 만큼만 쓴다. 모자라면 한 푼도 안 빠지고 거짓이다 —
        /// 음수 골드를 허용하면 "사지도 못한 카드 값이 빚으로 남는" 상태가 생긴다.
        /// </summary>
        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || amount > Gold) return false;
            if (amount == 0) return true;

            Gold -= amount;
            OnChanged?.Invoke();
            return true;
        }

        // ── 런 덱 ───────────────────────────────────────

        /// <summary>
        /// 시작 덱의 씨를 뿌린다. <b>런에 한 번뿐이다</b> — 두 번째 스테이지에서 다시 뿌리면
        /// 보상으로 얻은 카드가 지워진다. <paramref name="source"/>가 비어 있어도
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
                    AddSkill(c.Data);

                    // 시작 스킬은 처음부터 총공격에 들어가 있다 — 편집창을 안 열어도 첫 싸움에서 총공격이 나간다.
                    // 같은 스킬은 한 칸, 넘치면 앞에서부터 네 칸(PushAssaultSlot).
                    PushAssaultSlot(c.Data.skillName);
                }

            OnChanged?.Invoke();
            return skillCounts.Count > 0;
        }

        /// <summary>
        /// 런을 떠나기 전(마을)에 편성이 바뀌었다 — 시작 덱을 지금 편성으로 다시 뿌린다. 씨를 아직 안 뿌렸으면 아무것도 안 한다.
        ///
        /// 남은 동료의 스킬은 총공격 칸을 그대로 지킨다(마을에서 순서를 바꿨을 수 있다). 빠진 동료의 스킬은 칸째 지우고,
        /// 새로 온 동료의 스킬은 앞쪽 빈 칸에 넣는다 — 처음 뿌릴 때(<see cref="SeedDeck"/>)와 같은 규칙이다.
        /// </summary>
        public void ReseedDeck(IEnumerable<ComboCard> source)
        {
            if (!Seeded) return;

            skillCounts.Clear();

            var names = new List<string>();
            if (source != null)
                foreach (ComboCard c in source)
                {
                    if (c == null || c.Data == null) continue;
                    AddSkill(c.Data);
                    names.Add(c.Data.skillName);
                }

            for (int i = 0; i < AssaultSlotCount; i++)
                if (assaultSlots[i] != null && CountOf(assaultSlots[i]) <= 0) assaultSlots[i] = null;

            foreach (string name in names) PushAssaultSlot(name);

            OnChanged?.Invoke();
        }

        /// <summary>
        /// 덱을 통째로 갈아 끼운다. 디버그 모드(<see cref="AssaultEditorUI"/>)가 짠 덱이 들어오는 통로다.
        /// <see cref="SeedDeck"/>와 달리 이미 정해진 덱도 덮는다.
        /// </summary>
        public void SetDeck(IEnumerable<ComboCard> source)
        {
            ClearSkills();
            Seeded = true;

            if (source != null)
                foreach (ComboCard c in source)
                {
                    if (c == null || c.Data == null) continue;
                    AddSkill(c.Data);
                }

            OnChanged?.Invoke();
        }

        /// <summary>
        /// 고른 카드를 런 덱에 넣는다.
        /// 실제로 들어간 카드를 돌려준다 — 부르는 쪽이 <b>같은 인스턴스</b>를 손패에도 꽂는다.
        /// </summary>
        public ComboCard Grant(in CardOffer pick)
        {
            if (pick.IsEmpty) return null;

            AddSkill(pick.data);
            return new ComboCard(pick.data, CardGrantRules.WeightOf(pick.data));
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
            int removed = 0;
            var names = new List<string>(skillCounts.Keys);
            foreach (string n in names)
            {
                SkillData d = Resolve(n);
                if (d == null || d.role != role) continue;

                removed += skillCounts[n];
                skillCounts.Remove(n);
                ClearSlotsOf(n);
            }

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
            Gold = 0;
            Seeded = false;
            ClearSkills();

            // 빠뜨리기 쉬운 자리다 — 안 비우면 새 런이 지난 런의 시체를 물고 시작한다.
            Party.Clear();

            OnChanged?.Invoke();
        }
    }

    // ══ GoldRules ═══════════════════════════════════════════

    /// <summary>
    /// 골드의 <b>수치 전부</b> — 들어오는 값과 나가는 값. 순수 함수라 씬 없이 검증한다.
    ///
    /// <code>
    ///   클리어 보상(n, 칸) = (40 + 10 × (n − 1)) × 배율     n = 1부터 세는 층 번호
    ///   배율               = 정예 150% · 나머지 100%
    ///   카드 값            = 50
    /// </code>
    ///
    /// 1층 보상으로 카드 한 장을 못 산다. 두 판을 모아야 한 장이다 —
    /// 매 판 한 장씩 사지면 상점이 보상 상자의 복사본이 된다.
    ///
    /// <b>정예 칸의 대가가 배율이다.</b> 더 어렵기만 하면 아무도 안 고른다. 150%면 3층 정예(90)가
    /// 일반 전투(60)보다 카드 반 장 값을 더 준다 — "상점 앞에서 정예를 골라 한 장 더 산다"가 실제 선택이 된다.
    /// 배율은 정수 백분율로 둔다. 기본 보상이 10 단위라 소수 없이 떨어지고, 부동소수점 올림 오차가 안 생긴다.
    /// </summary>
    public static class GoldRules
    {
        public const int BaseClearReward = 40;
        public const int ClearRewardPerStage = 10;

        /// <summary>정예 칸 클리어 보상 배율(%).</summary>
        public const int EliteClearRewardPercent = 150;

        public const int CardPrice = 50;

        /// <summary>
        /// 그 층의 그 칸을 클리어하면 받는 골드. 1보다 작은 번호는 1로 본다.
        ///
        /// <b>칸 종류를 기본값 없이 받는다.</b> 번호만 받는 오버로드를 남기면 정예 칸에서 그걸 부르는 자리가
        /// 조용히 일반 보상으로 돈다(Room_Size_Plan 2.3의 "기본값 폴백을 두지 않는다"와 같은 이유).
        /// </summary>
        public static int StageClearReward(int floorNumber, MapNodeKind kind)
            => BaseReward(floorNumber) * ClearRewardPercent(kind) / 100;

        /// <summary>칸 종류가 거는 보상 배율(%). 화면 설명(<c>RunMapViewRules.Describe</c>)도 이 값을 읽는다.</summary>
        public static int ClearRewardPercent(MapNodeKind kind)
            => kind == MapNodeKind.Elite ? EliteClearRewardPercent : 100;

        private static int BaseReward(int floorNumber)
            => BaseClearReward + ClearRewardPerStage * (Math.Max(1, floorNumber) - 1);

        /// <summary>
        /// 상점에서 카드 한 장의 값. 유산 제단의 상점 할인이 걸린다(<see cref="MetaRules.ShopDiscountPercent"/>).
        /// </summary>
        public static int CardPriceNow()
            => MetaRules.Discounted(CardPrice,
                                    MetaRules.ShopDiscountPercent(MetaProgression.Current.LevelOf(MetaUpgrade.ShopDiscount)));
    }

    // ══ ClearRewardRules ═══════════════════════════════════════════

    /// <summary>
    /// 전투 칸을 깼을 때 받는 <b>물건</b>. 골드(<see cref="GoldRules"/>)와 별개다.
    /// 일반 전투 · 정예는 상자 하나(스킬 셋 중 한 장)를 받고, 정예는 유물 한 개를 더 받는다.
    /// 유물은 아직 Mock이다 — 받았다는 사실만 알리고 효과는 없다.
    /// </summary>
    public static class ClearRewardRules
    {
        public static bool GrantsCard(MapNodeKind kind)
            => kind == MapNodeKind.Battle || kind == MapNodeKind.Elite;

        public static bool GrantsRelic(MapNodeKind kind) => kind == MapNodeKind.Elite;
    }

    // ══ NodeRules ═══════════════════════════════════════════

    /// <summary>
    /// 비전투 칸(휴식 · 상점 · 이벤트)의 수치와 판단. 창을 그리는 쪽(<c>NodeWindows</c>)은
    /// 여기 값을 읽기만 한다.
    /// </summary>
    public static class NodeRules
    {
        /// <summary>휴식 칸에 들어가면 파티 전원이 회복하는 체력 비율.</summary>
        public const float BaseRestHealRatio = 0.3f;

        /// <summary>휴식 회복 비율 — 기본값 + 유산 제단 강화(<see cref="MetaRules.RestHealBonus"/>).</summary>
        public static float RestHealRatio
            => BaseRestHealRatio + MetaRules.RestHealBonus(MetaProgression.Current.LevelOf(MetaUpgrade.RestHeal));

        /// <summary>
        /// 카드를 쓸 수 있는 직업들. <b>이번 런에서 죽은 동료의 직업은 뺀다.</b>
        ///
        /// 전투 씬은 몸을 보고 거르지만(<c>BulletTimeController.AvailableRoles</c>) 상점에는 몸이 없다.
        /// 편성과 사망 기록으로 같은 답을 낸다 — 안 거르면 시전자가 없는 카드를 돈 주고 산다.
        /// </summary>
        public static List<Role> AvailableRoles(IReadOnlyList<PartyMemberData> roster, PartyState state)
        {
            var roles = new List<Role>(PartyLoadout.MaxMembers);
            if (roster == null) return roles;

            for (int i = 0; i < roster.Count; i++)
            {
                PartyMemberData m = roster[i];
                if (m == null || (state != null && state.IsDead(m))) continue;
                if (!roles.Contains(m.role)) roles.Add(m.role);
            }

            return roles;
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
        /// <b>주인공만 예외다.</b> 동료의 사망은 런 끝까지 영구지만, 주인공은 파티 명부 0번이자
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
        /// 즉 "죽은 동료"를 만들 방법이 없다.
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
        /// 몸이 없는 곳(휴식 · 이벤트 칸)에서 파티 체력을 한꺼번에 올리거나 내린다.
        /// 산 사람만 바뀌고, <b>죽은 동료는 그대로 죽어 있다</b>(영구 사망).
        ///
        /// <paramref name="roster"/>를 받는 이유 — 한 번도 기록되지 않은 동료(첫 스테이지 전)는
        /// 이 표에 없어서 누구인지 알 수가 없다. 편성을 같이 받아야 그 동료도 깎인다.
        /// 결과는 기록이 되므로 다음 전투의 <see cref="PartyAssembler"/>가 그대로 읽는다.
        ///
        /// 산 몸은 <b>0으로 떨어지지 않는다</b> — 이벤트가 동료를 죽이는 통로가 되면 안 된다.
        /// </summary>
        public void ChangeHp(float delta, IReadOnlyList<PartyMemberData> roster)
        {
            if (Mathf.Approximately(delta, 0f)) return;

            HeroHpRatio = Mathf.Clamp(HeroHpRatio + delta, 0.01f, 1f);
            HasSnapshot = true;

            if (roster == null) return;

            for (int i = 0; i < roster.Count; i++)
            {
                PartyMemberData m = roster[i];
                if (m == null || IsDead(m)) continue;

                Record(m, HpRatioOf(m) + delta, false);
            }
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
