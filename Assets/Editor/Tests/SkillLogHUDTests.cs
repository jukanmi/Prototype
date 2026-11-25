using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 스킬 로그 화면(<see cref="SkillLogHUD"/>). <c>CombatManager</c>의 <c>SkillLogCanvas</c>만 떠 와서 돌린다 —
    /// <see cref="RecentHitEnemyHUDTests"/>와 같은 이유(통째로 띄우면 이웃의 Awake까지 돈다).
    /// </summary>
    public class SkillLogHUDTests
    {
        private const string PrefabPath = "Assets/Prefabs/CombatManager.prefab";
        private const string CanvasName = "SkillLogCanvas";

        private static readonly string[] Members = { "Arc", "Tan", "War", "Wiz" };

        private readonly List<Object> spawned = new List<Object>();
        private SkillLogHUD hud;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"{PrefabPath} 를 못 찾았다.");

            Transform canvas = prefab.transform.Find(CanvasName);
            Assert.That(canvas, Is.Not.Null, $"{PrefabPath} 에 {CanvasName} 자식이 없다.");

            GameObject go = Object.Instantiate(canvas.gameObject);
            spawned.Add(go);

            hud = go.GetComponent<SkillLogHUD>();
            Assert.That(hud, Is.Not.Null, $"{CanvasName} 에 SkillLogHUD 가 없다.");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();
            hud = null;
        }

        private T Spawn<T>(string name) where T : Entity
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go.AddComponent<T>();
        }

        private SkillData NewSkill(string name)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.skillName = name;
            s.resultState = CombatState.AerialHit;
            spawned.Add(s);
            return s;
        }

        private static PartyMemberData Member(string id)
            => AssetDatabase.LoadAssetAtPath<PartyMemberData>($"Assets/Data/Resources/Party/Party_{id}.asset");

        // ── 받기 ─────────────────────────────────────────

        [Test]
        public void AllySkillCast_AddsRow()
        {
            CombatEvents.RaiseSkillCast(Spawn<Ally>("Ally"), NewSkill("회오리 베기"));

            Assert.That(hud.RowCount, Is.EqualTo(1));
            Assert.That(hud.NameTextOf(hud.Model.Entries[0]), Is.EqualTo("회오리 베기"));
        }

        [Test]
        public void EnemySkillCast_IsIgnored()
        {
            CombatEvents.RaiseSkillCast(Spawn<Enemy>("Enemy"), NewSkill("적 스킬"));
            Assert.That(hud.RowCount, Is.EqualTo(0));
        }

        [Test]
        public void HeroSkillCast_IsIgnored()
        {
            CombatEvents.RaiseSkillCast(Spawn<Player>("Hero"), NewSkill("주인공 스킬"));
            Assert.That(hud.RowCount, Is.EqualTo(0), "주인공 스킬은 로그에 넣지 않는다.");
        }

        [Test]
        public void ChipColor_ReachesTheRow()
        {
            SkillData s = NewSkill("띄우기");
            CombatEvents.RaiseSkillCast(Spawn<Ally>("Ally"), s);

            Assert.That(hud.ChipOf(hud.Model.Entries[0]), Is.EqualTo(SkillLogModel.ChipColor(s)));
        }

        [Test]
        public void RowsDisappear_AfterThreeIdleSeconds()
        {
            CombatEvents.RaiseSkillCast(Spawn<Ally>("Ally"), NewSkill("A"));
            hud.Tick(SkillLogModel.IdleWindow);
            for (int i = 0; i < 60; i++) hud.Tick(1f / 60f);

            Assert.That(hud.RowCount, Is.EqualTo(0));
        }

        [Test]
        public void Rows_StackTopDown_FromJustAboveCenter()
        {
            Assert.That(SkillLogHUD.RowY(0), Is.EqualTo(-SkillLogHUD.MarginTop));
            Assert.That(SkillLogHUD.RowY(1), Is.LessThan(SkillLogHUD.RowY(0)), "다음 줄은 아래에.");

            // 첫 줄은 화면 가운데(1080/2)보다 약간 위에서 시작한다.
            Assert.That(SkillLogHUD.MarginTop, Is.LessThan(540f));
            Assert.That(SkillLogHUD.MarginTop, Is.GreaterThan(540f - 4 * SkillLogHUD.Pitch), "가운데에서 너무 멀다.");
        }

        [Test]
        public void RowX_SlidesInFromLeft_AndOutToLeft()
        {
            Assert.That(SkillLogHUD.RowX(1f, 0f), Is.EqualTo(SkillLogHUD.MarginX));
            Assert.That(SkillLogHUD.RowX(0f, 0f), Is.LessThan(SkillLogHUD.MarginX));
            Assert.That(SkillLogHUD.RowX(1f, 1f), Is.LessThan(SkillLogHUD.MarginX));
        }

        // ── 순서 · 대상 사망 ─────────────────────────────

        [Test]
        public void LatestSkill_IsOnTop()
        {
            Ally ally = Spawn<Ally>("Ally");
            CombatEvents.RaiseSkillCast(ally, NewSkill("첫째"));
            CombatEvents.RaiseSkillCast(ally, NewSkill("둘째"));

            Assert.That(hud.NameTextOf(hud.Model.Entries[0]), Is.EqualTo("둘째"));
            Assert.That(hud.Model.Entries[0].Slot, Is.EqualTo(0));
        }

        [Test]
        public void TargetDies_RowLeaves_OneSecondLater()
        {
            Ally ally = Spawn<Ally>("Ally");
            Enemy dying = Spawn<Enemy>("Dying");
            Enemy living = Spawn<Enemy>("Living");
            CombatEvents.RaiseSkillCast(ally, NewSkill("막타"), dying);
            CombatEvents.RaiseSkillCast(ally, NewSkill("다른 적"), living);
            Assert.That(hud.RowCount, Is.EqualTo(2));

            Object.DestroyImmediate(dying.gameObject);

            hud.Tick(SkillLogModel.DeathLinger - 0.1f);
            Assert.That(hud.RowCount, Is.EqualTo(2), "죽고 1초가 지나기 전에는 남는다.");

            for (int i = 0; i < 30; i++) hud.Tick(1f / 60f);
            Assert.That(hud.RowCount, Is.EqualTo(1), "1초가 지나면 그 줄만 빠진다.");
            Assert.That(hud.NameTextOf(hud.Model.Entries[0]), Is.EqualTo("다른 적"));
        }

        [Test]
        public void DeactivatedTarget_CountsAsDead()
        {
            Enemy e = Spawn<Enemy>("Pooled");
            CombatEvents.RaiseSkillCast(Spawn<Ally>("Ally"), NewSkill("스킬"), e);
            e.gameObject.SetActive(false);

            hud.Tick(SkillLogModel.DeathLinger);
            Assert.That(hud.Model.Entries[0].Evicting, Is.True, "풀로 돌아간 적도 죽은 것으로 본다.");
        }

        [Test]
        public void GroundSkill_TakesFirstHitVictimAsTarget()
        {
            Ally ally = Spawn<Ally>("Ally");
            Enemy victim = Spawn<Enemy>("Victim");
            CombatEvents.RaiseSkillCast(ally, NewSkill("장판"));
            Assert.That(hud.Model.Entries[0].Target, Is.Null);

            CombatEvents.RaiseHitLanded(ally.GetComponent<Combat>(), victim.GetComponent<Combat>());

            Assert.That(hud.Model.Entries[0].Target, Is.SameAs(victim));
        }

        [Test]
        public void AllyTarget_IsNotTracked()
        {
            Ally caster = Spawn<Ally>("Caster");
            Ally buddy = Spawn<Ally>("Buddy");
            CombatEvents.RaiseSkillCast(caster, NewSkill("버프"), buddy);

            Assert.That(hud.Model.Entries[0].Target, Is.Null, "아군 대상은 '죽으면 사라짐'의 대상이 아니다.");
        }

        [Test]
        public void NoTarget_RowStays_UntilIdle()
        {
            CombatEvents.RaiseSkillCast(Spawn<Ally>("Ally"), NewSkill("헛스윙"));
            hud.Tick(SkillLogModel.DeathLinger + 0.5f);
            Assert.That(hud.RowCount, Is.EqualTo(1));
        }

        // ── 동료 눈매 ────────────────────────────────────

        [TestCaseSource(nameof(Members))]
        public void PartyData_HasSkillLogFace(string id)
        {
            PartyMemberData data = Member(id);
            Assert.That(data, Is.Not.Null, $"Party_{id} 표가 없다.");
            Assert.That(data.skillLogFace, Is.Not.Null, $"Party_{id}.skillLogFace 가 비었다 — 로그에 직업 색 띠만 뜬다.");
            Assert.That(data.skillLogFace.name, Does.StartWith($"SkillLogEyes_{id}"), "다른 동료의 눈매가 꽂혔다.");
        }

        [TestCaseSource(nameof(Members))]
        public void AllyWithData_ShowsItsEyes(string id)
        {
            PartyMemberData data = Member(id);
            Assert.That(data, Is.Not.Null);

            var go = new GameObject($"Ally_{id}");
            go.SetActive(false);
            spawned.Add(go);
            var ally = go.AddComponent<Ally>();
            ally.ApplyData(data);

            CombatEvents.RaiseSkillCast(ally, NewSkill("스킬"));

            Assert.That(hud.FaceOf(hud.Model.Entries[0]), Is.SameAs(data.skillLogFace));
        }

        [Test]
        public void AllyWithoutData_FallsBackToRoleStrip()
        {
            CombatEvents.RaiseSkillCast(Spawn<Ally>("Ally"), NewSkill("스킬"));
            Assert.That(hud.FaceOf(hud.Model.Entries[0]), Is.Null, "그림이 없으면 직업 색 띠로 대체한다.");
        }
    }
}
