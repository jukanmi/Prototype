using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 로드아웃 → 슬롯 배치 규칙. 씬도 <c>Awake</c>도 없이 도는 순수 계산이라
    /// <see cref="TagSwapRules"/> 테스트와 같은 자리에 둔다.
    /// </summary>
    public class PartyAssembleRulesTests
    {
        private readonly List<Object> spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in spawned) Object.DestroyImmediate(o);
            spawned.Clear();
        }

        private SkillData Skill(Role role)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.role = role;
            s.skillName = role + "_테스트";
            spawned.Add(s);
            return s;
        }

        private PartyMemberData Member(Role role, int cards = Ally.EquipSlots)
        {
            var m = ScriptableObject.CreateInstance<PartyMemberData>();
            m.memberId = role.ToString();
            m.role = role;
            m.equipped = new List<ComboCard>();
            for (int i = 0; i < cards; i++) m.equipped.Add(new ComboCard(Skill(role)));

            spawned.Add(m);
            return m;
        }

        private PartyLoadout Loadout(params PartyMemberData[] members)
        {
            var l = ScriptableObject.CreateInstance<PartyLoadout>();
            l.members = members;
            spawned.Add(l);
            return l;
        }

        // ── Resolve ─────────────────────────────────────

        [Test]
        public void Resolve_FullParty_FillsEverySlot()
        {
            PartyLoadout l = Loadout(Member(Role.Tanker), Member(Role.Warrior),
                                     Member(Role.Archer), Member(Role.Wizard));

            PartyMemberData[] slots = PartyAssembleRules.Resolve(l, 4);

            Assert.That(slots.Length, Is.EqualTo(4));
            Assert.That(slots[0].role, Is.EqualTo(Role.Tanker));
            Assert.That(slots[3].role, Is.EqualTo(Role.Wizard));
        }

        /// <summary>
        /// 3인 파티. 빈 칸을 <b>앞으로 당기지 않는다</b> — 슬롯 순서가 곧 F키 교대 순환 순서라
        /// 저작자가 2번에 둔 동료는 3번이 비어도 2번이어야 한다.
        /// </summary>
        [Test]
        public void Resolve_KeepsHoles_DoesNotCompact()
        {
            PartyLoadout l = Loadout(Member(Role.Tanker), null, Member(Role.Archer), null);

            PartyMemberData[] slots = PartyAssembleRules.Resolve(l, 4);

            Assert.That(slots[0], Is.Not.Null);
            Assert.That(slots[1], Is.Null, "빈 칸이 앞으로 당겨졌다 — 교대 순서가 바뀐다.");
            Assert.That(slots[2].role, Is.EqualTo(Role.Archer));
            Assert.That(slots[3], Is.Null);
        }

        [Test]
        public void Resolve_ShortLoadout_PadsWithNull()
        {
            PartyMemberData[] slots = PartyAssembleRules.Resolve(Loadout(Member(Role.Tanker)), 4);

            Assert.That(slots.Length, Is.EqualTo(4));
            Assert.That(slots[1], Is.Null);
            Assert.That(slots[3], Is.Null);
        }

        [Test]
        public void Resolve_OverlongLoadout_Truncates()
        {
            PartyLoadout l = Loadout(Member(Role.Tanker), Member(Role.Warrior),
                                     Member(Role.Archer), Member(Role.Wizard), Member(Role.Tanker));

            Assert.That(PartyAssembleRules.Resolve(l, 4).Length, Is.EqualTo(4));
        }

        [Test]
        public void Resolve_NullLoadout_ReturnsEmptySlots()
        {
            PartyMemberData[] slots = PartyAssembleRules.Resolve(null, 4);

            Assert.That(slots.Length, Is.EqualTo(4));
            foreach (PartyMemberData m in slots) Assert.That(m, Is.Null);
        }

        // ── IsValid ─────────────────────────────────────

        [Test]
        public void IsValid_EmptySlot_IsFine()
        {
            // 빈 칸은 저작 실수가 아니라 3인 파티다.
            Assert.That(PartyAssembleRules.IsValid(null, out _), Is.True);
        }

        /// <summary>
        /// 이 테스트가 잡는 것이 <b>가장 조용한 버그</b>다. 직업이 어긋난 카드는
        /// 덱에 정상적으로 들어가지만 <c>ResolveCaster</c>가 시전자를 못 찾아
        /// 손패 맨 앞에서 영영 안 나간다 — 에러도 경고도 없이 U키만 먹통이 된다.
        /// </summary>
        [Test]
        public void IsValid_RoleMismatch_IsRejected()
        {
            PartyMemberData m = Member(Role.Wizard, 0);
            m.equipped.Add(new ComboCard(Skill(Role.Archer)));

            Assert.That(PartyAssembleRules.IsValid(m, out string reason), Is.False);
            Assert.That(reason, Does.Contain("Archer"));
        }

        [Test]
        public void IsValid_EmptyCardSlot_IsRejected()
        {
            PartyMemberData m = Member(Role.Tanker, 0);
            m.equipped.Add(new ComboCard(null));

            Assert.That(PartyAssembleRules.IsValid(m, out _), Is.False);
        }

        [Test]
        public void IsValid_NoCards_IsRejected()
        {
            Assert.That(PartyAssembleRules.IsValid(Member(Role.Tanker, 0), out _), Is.False);
        }

        // ── 덱 · 직업 ───────────────────────────────────

        [Test]
        public void CardCount_FullParty_MatchesDeckSize()
        {
            PartyLoadout l = Loadout(Member(Role.Tanker), Member(Role.Warrior),
                                     Member(Role.Archer), Member(Role.Wizard));

            Assert.That(PartyAssembleRules.CardCount(l), Is.EqualTo(Deck.Size),
                $"파티 4명 × {Ally.EquipSlots}장이 덱 {Deck.Size}장과 맞지 않는다.");
        }

        [Test]
        public void RolesOf_Deduplicates()
        {
            PartyLoadout l = Loadout(Member(Role.Tanker), Member(Role.Tanker), Member(Role.Archer), null);

            List<Role> roles = PartyAssembleRules.RolesOf(l);

            Assert.That(roles.Count, Is.EqualTo(2));
            Assert.That(roles, Does.Contain(Role.Tanker));
            Assert.That(roles, Does.Contain(Role.Archer));
        }

        // ── Pick — Boot ↔ 단독 실행 ─────────────────────

        /// <summary>
        /// 런의 주인이 이긴다. <c>BulletTimeController.StartupMode</c>와 같은 규칙이라
        /// 두 결정이 서로 다른 방향으로 갈리지 않는다.
        /// </summary>
        [Test]
        public void Pick_RunLoadoutWins()
        {
            PartyLoadout run = Loadout(Member(Role.Wizard));
            PartyLoadout standalone = Loadout(Member(Role.Tanker));

            Assert.That(PartyAssembleRules.Pick(run, standalone), Is.SameAs(run));
        }

        [Test]
        public void Pick_NoRun_FallsBackToStandalone()
        {
            PartyLoadout standalone = Loadout(Member(Role.Tanker));

            Assert.That(PartyAssembleRules.Pick(null, standalone), Is.SameAs(standalone));
        }

        // ── 편성 (파티 선택 화면) ────────────────────────

        [Test]
        public void Toggle_AddsThenRemoves()
        {
            var party = new List<PartyMemberData>();
            PartyMemberData tan = Member(Role.Tanker);

            Assert.That(PartyAssembleRules.Toggle(party, tan, 4), Is.True);
            Assert.That(party, Has.Count.EqualTo(1));

            Assert.That(PartyAssembleRules.Toggle(party, tan, 4), Is.True);
            Assert.That(party, Is.Empty);
        }

        /// <summary>
        /// 슬롯 순서가 곧 F키 교대 순환 순서다. 중간을 빼면 뒤가 당겨져야 한다 —
        /// 빈 칸을 남기면 "3번이 비었는데 4번으로 교대된다"가 되어 순환이 안 읽힌다.
        /// </summary>
        [Test]
        public void Toggle_RemovingMiddle_CompactsOrder()
        {
            PartyMemberData a = Member(Role.Tanker), b = Member(Role.Warrior), c = Member(Role.Archer);
            var party = new List<PartyMemberData> { a, b, c };

            PartyAssembleRules.Toggle(party, b, 4);

            Assert.That(party, Is.EqualTo(new[] { a, c }));
        }

        /// <summary>
        /// 가득 찼을 때 새 사람을 누르면 아무 일도 안 한다.
        /// 조용히 누군가를 밀어내면 방금 뭘 잃었는지 화면에서 알 수가 없다.
        /// </summary>
        [Test]
        public void Toggle_WhenFull_RejectsNewMember()
        {
            var party = new List<PartyMemberData>
            {
                Member(Role.Tanker), Member(Role.Warrior), Member(Role.Archer), Member(Role.Wizard),
            };

            Assert.That(PartyAssembleRules.Toggle(party, Member(Role.Tanker), 4), Is.False);
            Assert.That(party, Has.Count.EqualTo(4));
        }

        [Test]
        public void Toggle_WhenFull_StillRemovesExisting()
        {
            PartyMemberData wiz = Member(Role.Wizard);
            var party = new List<PartyMemberData>
            {
                Member(Role.Tanker), Member(Role.Warrior), Member(Role.Archer), wiz,
            };

            Assert.That(PartyAssembleRules.Toggle(party, wiz, 4), Is.True);
            Assert.That(party, Has.Count.EqualTo(3));
        }

        [Test]
        public void DuplicateRoles_ReportsOnlyRepeats()
        {
            var party = new List<PartyMemberData>
            {
                Member(Role.Tanker), Member(Role.Tanker), Member(Role.Archer),
            };

            List<Role> dup = PartyAssembleRules.DuplicateRoles(party);

            Assert.That(dup, Is.EqualTo(new[] { Role.Tanker }));
        }

        [Test]
        public void CanStart_NeedsAtLeastOneMember()
        {
            Assert.That(PartyAssembleRules.CanStart(new List<PartyMemberData>()), Is.False);
            Assert.That(PartyAssembleRules.CanStart(new PartyMemberData[] { null, null }), Is.False);
            Assert.That(PartyAssembleRules.CanStart(new[] { Member(Role.Tanker) }), Is.True);
        }

        // ── 런타임 조합 ─────────────────────────────────

        /// <summary>
        /// 화면에서 짠 조합은 에셋이 아니라 메모리 인스턴스다.
        /// 넘긴 순서가 그대로 슬롯 순서(= 교대 순서)여야 한다.
        /// </summary>
        [Test]
        public void CreateRuntime_KeepsPickOrderAndPadsRest()
        {
            PartyMemberData a = Member(Role.Wizard), b = Member(Role.Tanker);

            PartyLoadout l = PartyLoadout.CreateRuntime(new[] { a, b });
            spawned.Add(l);

            Assert.That(l.members.Length, Is.EqualTo(PartyLoadout.MaxMembers));
            Assert.That(l.members[0], Is.SameAs(a));
            Assert.That(l.members[1], Is.SameAs(b));
            Assert.That(l.members[2], Is.Null);
            Assert.That(l.members[3], Is.Null);
            Assert.That(l.FilledCount, Is.EqualTo(2));
        }

        /// <summary>
        /// 편성 화면은 인스턴스를 하나만 만들어 계속 고쳐 쓴다. 줄어들 때 뒤 칸이
        /// 안 비워지면 이미 뺀 동료가 파티에 그대로 남는다.
        /// </summary>
        [Test]
        public void Fill_ClearsTrailingSlots()
        {
            PartyLoadout l = PartyLoadout.CreateRuntime(new[]
            {
                Member(Role.Tanker), Member(Role.Warrior), Member(Role.Archer), Member(Role.Wizard),
            });
            spawned.Add(l);

            l.Fill(new[] { Member(Role.Archer) });

            Assert.That(l.FilledCount, Is.EqualTo(1));
            Assert.That(l.members[1], Is.Null);
            Assert.That(l.members[3], Is.Null);
        }
    }
}
