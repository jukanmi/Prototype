using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 스킬 로그의 계산(<see cref="SkillLogModel"/>). 순수 C#이라 오브젝트 없이 돈다.
    /// </summary>
    public class SkillLogModelTests
    {
        private SkillLogModel model;

        [SetUp]
        public void SetUp() => model = new SkillLogModel();

        // ── 쌓기 · 번호 ──────────────────────────────────

        [Test]
        public void Add_NumbersFromOne_NewestOnTop()
        {
            var a = model.Add(null, null);
            var b = model.Add(null, null);
            var c = model.Add(null, null);

            Assert.That(new[] { a.Order, b.Order, c.Order }, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(new[] { c.Slot, b.Slot, a.Slot }, Is.EqualTo(new[] { 0, 1, 2 }),
                "마지막에 쓴 스킬이 맨 위, 먼저 쓴 것일수록 아래.");
            Assert.That(model.Entries[0], Is.SameAs(c));
        }

        [Test]
        public void NewEntry_PushesOthersDown_Smoothly()
        {
            var a = model.Add(null, null);
            model.Tick(1f);
            model.Add(null, null);

            Assert.That(a.Slot, Is.EqualTo(1), "기존 줄은 한 칸 아래로.");
            Assert.That(a.Y, Is.EqualTo(0f), "아직 제자리 — 순간이동하지 않는다.");

            model.Tick(1f / 60f);
            Assert.That(a.Y, Is.GreaterThan(0f).And.LessThan(1f), "미끄러지는 중.");
        }

        [Test]
        public void NewEntry_StartsAtItsSlot_ButHidden()
        {
            model.Add(null, null);
            var b = model.Add(null, null);

            Assert.That(b.Y, Is.EqualTo(0f), "새 줄은 맨 위 칸에서 바로 시작한다 — 미끄러지는 건 기존 줄뿐.");
            Assert.That(b.Appear, Is.EqualTo(0f));
            Assert.That(b.Alpha, Is.EqualTo(0f));
        }

        [Test]
        public void Appear_ReachesOne_AfterAppearTime()
        {
            var a = model.Add(null, null);
            model.Tick(SkillLogModel.AppearTime);
            Assert.That(a.Appear, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(a.Alpha, Is.EqualTo(1f).Within(1e-4f));
        }

        // ── 넘침 ─────────────────────────────────────────

        [Test]
        public void Overflow_EvictsOldest_AtTheBottom()
        {
            var oldest = model.Add(null, null);
            var second = model.Add(null, null);
            for (int i = 2; i < SkillLogModel.MaxRows; i++) model.Add(null, null);
            Assert.That(oldest.Evicting, Is.False);

            var newest = model.Add(null, null);

            Assert.That(oldest.Evicting, Is.True, $"{SkillLogModel.MaxRows}줄을 넘으면 맨 아래(가장 오래된) 줄이 빠진다.");
            Assert.That(second.Evicting, Is.False);
            Assert.That(second.Slot, Is.EqualTo(SkillLogModel.MaxRows - 1), "남는 줄 중 가장 오래된 것이 맨 아래.");
            Assert.That(newest.Slot, Is.EqualTo(0));
            Assert.That(newest.Order, Is.EqualTo(SkillLogModel.MaxRows + 1), "번호는 넘쳐도 이어서 센다.");
        }

        [Test]
        public void EvictedRow_IsRemoved_AfterEvictTime()
        {
            for (int i = 0; i <= SkillLogModel.MaxRows; i++) model.Add(null, null);
            Assert.That(model.Count, Is.EqualTo(SkillLogModel.MaxRows + 1));

            model.Tick(SkillLogModel.EvictTime + 0.01f);

            Assert.That(model.Count, Is.EqualTo(SkillLogModel.MaxRows));
        }

        [Test]
        public void MovedRow_GlidesToNewSlot_NotJumps()
        {
            var a = model.Add(null, null);
            model.Add(null, null);
            model.Add(null, null);
            Assert.That(a.Slot, Is.EqualTo(2));

            for (int i = 0; i < 120; i++) model.Tick(1f / 60f);
            Assert.That(a.Y, Is.EqualTo(2f).Within(1e-3f));
        }

        // ── 대상 사망 ────────────────────────────────────

        [Test]
        public void TargetDead_RowStays_ForDeathLinger()
        {
            var a = model.Add(null, null);
            model.NotifyTargetDead(a);
            model.Tick(SkillLogModel.DeathLinger - 0.05f);

            Assert.That(a.Evicting, Is.False, "대상이 죽어도 1초는 남는다.");
            Assert.That(model.Count, Is.EqualTo(1));
        }

        [Test]
        public void TargetDead_RowLeaves_AfterDeathLinger()
        {
            var a = model.Add(null, null);
            model.NotifyTargetDead(a);
            model.Tick(SkillLogModel.DeathLinger);
            Assert.That(a.Evicting, Is.True);

            model.Tick(SkillLogModel.EvictTime + 0.01f);
            Assert.That(model.Count, Is.EqualTo(0));
        }

        [Test]
        public void TargetDead_RowsBelowCloseTheGap()
        {
            var bottom = model.Add(null, null);
            var middle = model.Add(null, null);
            var top = model.Add(null, null);
            Assert.That(bottom.Slot, Is.EqualTo(2));

            model.NotifyTargetDead(middle);
            model.Tick(SkillLogModel.DeathLinger);

            Assert.That(top.Slot, Is.EqualTo(0));
            Assert.That(bottom.Slot, Is.EqualTo(1), "빠진 줄 아래가 한 칸 올라온다.");
        }

        [Test]
        public void TargetDead_CalledTwice_DoesNotRestartClock()
        {
            var a = model.Add(null, null);
            model.NotifyTargetDead(a);
            model.Tick(0.6f);
            model.NotifyTargetDead(a);
            model.Tick(0.4f);

            Assert.That(a.Evicting, Is.True, "두 번째 알림이 1초를 다시 걸면 안 된다.");
        }

        [Test]
        public void TargetDead_ClockFreezes_WhenTimeStops()
        {
            var a = model.Add(null, null);
            model.NotifyTargetDead(a);
            for (int i = 0; i < 100; i++) model.Tick(0f);
            Assert.That(a.DeathLeft, Is.EqualTo(SkillLogModel.DeathLinger));
        }

        [Test]
        public void Add_KeepsTarget()
        {
            var a = model.Add(null, null, null);
            Assert.That(a.Target, Is.Null);
        }

        // ── 3초 정리 ─────────────────────────────────────

        [Test]
        public void JustBeforeIdleWindow_StaysVisible()
        {
            model.Add(null, null);
            model.Tick(SkillLogModel.IdleWindow - 0.1f);
            Assert.That(model.IsClearing, Is.False);
            Assert.That(model.Count, Is.EqualTo(1));
        }

        [Test]
        public void AtIdleWindow_StartsClearing()
        {
            model.Add(null, null);
            model.Tick(SkillLogModel.IdleWindow);
            Assert.That(model.IsClearing, Is.True);
        }

        [Test]
        public void Clearing_EmptiesLog_AndRestartsNumbering()
        {
            model.Add(null, null);
            model.Add(null, null);
            model.Tick(SkillLogModel.IdleWindow);

            for (int i = 0; i < 60; i++) model.Tick(1f / 60f);

            Assert.That(model.Count, Is.EqualTo(0));
            Assert.That(model.IsClearing, Is.False);
            Assert.That(model.Add(null, null).Order, Is.EqualTo(1), "다 빠진 뒤의 첫 스킬은 다시 1번.");
        }

        [Test]
        public void Clearing_LeavesTopRowFirst()
        {
            var older = model.Add(null, null);
            var top = model.Add(null, null);
            model.Tick(SkillLogModel.IdleWindow);
            model.Tick(SkillLogModel.ExitStagger * 0.5f + 0.01f);

            Assert.That(top.Exit, Is.GreaterThan(older.Exit), "위 줄(가장 최근)부터 차례로 빠진다.");
        }

        [Test]
        public void AttackDuringClearing_CancelsIt_AndKeepsNumbering()
        {
            model.Add(null, null);
            model.Tick(SkillLogModel.IdleWindow);
            model.Tick(0.1f);
            Assert.That(model.IsClearing, Is.True);

            var b = model.Add(null, null);

            Assert.That(model.IsClearing, Is.False);
            Assert.That(model.Entries[1].Exit, Is.EqualTo(0f), "빠지던 줄이 되살아난다.");
            Assert.That(b.Order, Is.EqualTo(2), "정리가 취소됐으니 번호는 이어서 센다.");
        }

        [Test]
        public void NotifyAttack_ResetsIdle()
        {
            model.Add(null, null);
            model.Tick(2.5f);
            model.NotifyAttack();
            model.Tick(2.5f);

            Assert.That(model.IsClearing, Is.False, "타격이 있으면 3초는 다시 센다 — 다단히트 중에 사라지지 않는다.");
        }

        [Test]
        public void ZeroDelta_FreezesEverything()
        {
            var a = model.Add(null, null);
            for (int i = 0; i < 100; i++) model.Tick(0f);

            Assert.That(model.Idle, Is.EqualTo(0f), "시간이 멈춘 동안(총공격 선택) 3초가 흐르지 않는다.");
            Assert.That(a.Appear, Is.EqualTo(0f));
        }

        // ── 칸 색 ────────────────────────────────────────

        private static SkillData Skill(CombatState result, Debuff debuff = Debuff.None)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.resultState = result;
            s.hitDataList.Add(new HitData { debuff = Debuff.None });
            s.hitDataList.Add(new HitData { debuff = debuff });
            return s;
        }

        [Test]
        public void ChipColor_FollowsResultState()
        {
            var s = Skill(CombatState.AerialHit);
            Assert.That(SkillLogModel.ChipColor(s), Is.EqualTo(CombatStateVisuals.StateColor(CombatState.AerialHit)));
            Object.DestroyImmediate(s);
        }

        [Test]
        public void ChipColor_DebuffBeatsResultState()
        {
            var s = Skill(CombatState.AerialHit, Debuff.AirBind);
            CombatStateVisuals.TryDebuffColor(Debuff.AirBind, out Color bind);

            Assert.That(SkillLogModel.ChipColor(s), Is.EqualTo(bind), "묶는 스킬은 '묶는다'가 가장 큰 정보다.");
            Assert.That(bind, Is.Not.EqualTo(CombatStateVisuals.StateColor(CombatState.AerialHit)));
            Object.DestroyImmediate(s);
        }

        [Test]
        public void ChipColor_NeutralResult_IsMuted()
        {
            var s = Skill(CombatState.Neutral);
            Assert.That(SkillLogModel.ChipColor(s), Is.EqualTo(UiTheme.TextOff));
            Object.DestroyImmediate(s);
        }

        [Test]
        public void DebuffColor_UsesOverlayPriority()
        {
            CombatStateVisuals.TryDebuffColor(Debuff.AirBind, out Color bind);
            CombatStateVisuals.TryDebuffColor(Debuff.Stun | Debuff.AirBind, out Color mixed);
            Assert.That(mixed, Is.EqualTo(bind));
            Assert.That(CombatStateVisuals.TryDebuffColor(Debuff.None, out _), Is.False);
        }

        [Test]
        public void NumberColor_IsDarkOnLightChips()
        {
            Assert.That(SkillLogModel.NumberColor(Color.white), Is.EqualTo(UiTheme.TextOnLight), "경직(흰 칸)");
            CombatStateVisuals.TryDebuffColor(Debuff.Stun, out Color stun);
            Assert.That(SkillLogModel.NumberColor(stun), Is.EqualTo(UiTheme.TextOnLight), "스턴(노란 칸)");
            Assert.That(SkillLogModel.NumberColor(CombatStateVisuals.StateColor(CombatState.Down)),
                Is.EqualTo(UiTheme.TextOnLight).Or.EqualTo(UiTheme.TextMain));
        }

        [Test]
        public void NameOf_FallsBackToAssetName()
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.name = "Skill_Asset";
            s.skillName = "";
            Assert.That(SkillLogModel.NameOf(s), Is.EqualTo("Skill_Asset"));
            s.skillName = "회오리 베기";
            Assert.That(SkillLogModel.NameOf(s), Is.EqualTo("회오리 베기"));
            Object.DestroyImmediate(s);
        }
    }
}
