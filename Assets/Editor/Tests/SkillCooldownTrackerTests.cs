using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 실시간(U키) 카드 쿨타임 장부. 시간을 스스로 읽지 않으므로 씬 없이 전부 검증된다.
    /// </summary>
    public class SkillCooldownTrackerTests
    {
        private readonly List<Object> assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < assets.Count; i++)
                Object.DestroyImmediate(assets[i]);

            assets.Clear();
        }

        private SkillData NewSkill(float cooldown)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            data.skillName = "테스트 스킬";
            data.cooldown = cooldown;
            assets.Add(data);
            return data;
        }

        [Test]
        public void Start_BlocksUntilTicked()
        {
            var tracker = new SkillCooldownTracker();
            SkillData skill = NewSkill(5f);

            Assert.That(tracker.IsCooling(skill), Is.False, "쓴 적 없으면 쿨타임이 아니다");

            tracker.Start(skill, skill.cooldown);
            Assert.That(tracker.Remaining(skill), Is.EqualTo(5f).Within(0.001f));

            tracker.Tick(2f);
            Assert.That(tracker.Remaining(skill), Is.EqualTo(3f).Within(0.001f));

            tracker.Tick(3f);
            Assert.That(tracker.IsCooling(skill), Is.False, "다 흐르면 풀린다");
            Assert.That(tracker.Count, Is.Zero, "끝난 항목은 장부에서 지워진다");
        }

        [Test]
        public void Skills_DoNotShareSkillCooldown()
        {
            var tracker = new SkillCooldownTracker();
            SkillData a = NewSkill(4f);
            SkillData b = NewSkill(4f);

            tracker.Start(a, a.cooldown);

            Assert.That(tracker.IsCooling(a), Is.True);
            Assert.That(tracker.IsCooling(b), Is.False, "에셋이 다르면 다른 스킬이다");
        }

        /// <summary>
        /// U 연타가 통과하던 구멍. 손패 4장이 서로 다른 스킬이라
        /// 스킬 쿨만으로는 한 장도 못 막는다 — 공용 쿨이 그 자리를 맡는다.
        /// </summary>
        [Test]
        public void GlobalCooldown_BlocksEveryOtherSkill()
        {
            var tracker = new SkillCooldownTracker();
            SkillData used = NewSkill(5f);
            SkillData nextCard = NewSkill(5f);

            tracker.Start(used, used.cooldown);
            tracker.StartGlobal(1.5f);

            Assert.That(tracker.IsCooling(nextCard), Is.True, "한 장 쓰면 다음 카드도 잠긴다");
            Assert.That(tracker.Remaining(nextCard), Is.EqualTo(1.5f).Within(0.001f));

            tracker.Tick(1.5f);

            Assert.That(tracker.IsCooling(nextCard), Is.False, "공용 쿨이 풀리면 다음 카드는 나간다");
            Assert.That(tracker.IsCooling(used), Is.True, "쓴 스킬 자신은 아직 5초 중 3.5초 남았다");
            Assert.That(tracker.Remaining(used), Is.EqualTo(3.5f).Within(0.001f));
        }

        [Test]
        public void Remaining_TakesLongerOfGlobalAndSkill()
        {
            var tracker = new SkillCooldownTracker();
            SkillData skill = NewSkill(5f);

            tracker.Start(skill, 5f);
            tracker.StartGlobal(1.5f);

            Assert.That(tracker.Remaining(skill), Is.EqualTo(5f).Within(0.001f), "긴 쪽이 남는다");
            Assert.That(tracker.OwnRemaining(skill), Is.EqualTo(5f).Within(0.001f));
            Assert.That(tracker.GlobalRemaining, Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void StartGlobal_KeepsLongerRemaining()
        {
            var tracker = new SkillCooldownTracker();

            tracker.StartGlobal(2f);
            tracker.Tick(0.5f);

            tracker.StartGlobal(1f);
            Assert.That(tracker.GlobalRemaining, Is.EqualTo(1.5f).Within(0.001f), "짧은 쿨이 긴 쿨을 깎지 않는다");

            tracker.StartGlobal(4f);
            Assert.That(tracker.GlobalRemaining, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void GlobalCooldown_TicksWithoutAnySkillEntry()
        {
            var tracker = new SkillCooldownTracker();

            tracker.StartGlobal(1f);
            Assert.That(tracker.Count, Is.Zero, "공용 쿨은 스킬 장부에 안 올라간다");

            tracker.Tick(1f);
            Assert.That(tracker.GlobalRemaining, Is.Zero, "스킬 장부가 비어도 공용 쿨은 흐른다");
        }

        [Test]
        public void Restart_KeepsLongerRemaining()
        {
            var tracker = new SkillCooldownTracker();
            SkillData skill = NewSkill(6f);

            tracker.Start(skill, 6f);
            tracker.Tick(1f);

            // 짧은 쿨을 다시 걸어도 남은 5초를 깎아 내리지 않는다.
            tracker.Start(skill, 2f);
            Assert.That(tracker.Remaining(skill), Is.EqualTo(5f).Within(0.001f));

            // 더 긴 쿨은 덮어쓴다.
            tracker.Start(skill, 9f);
            Assert.That(tracker.Remaining(skill), Is.EqualTo(9f).Within(0.001f));
        }

        [Test]
        public void ZeroCooldown_NeverEnters()
        {
            var tracker = new SkillCooldownTracker();
            SkillData skill = NewSkill(0f);

            tracker.Start(skill, skill.cooldown);

            Assert.That(tracker.Count, Is.Zero, "0초 쿨은 장부에 올리지 않는다");
            Assert.That(tracker.IsCooling(skill), Is.False);
        }

        [Test]
        public void NullSkill_IsSafe()
        {
            var tracker = new SkillCooldownTracker();

            tracker.Start(null, 5f);

            Assert.That(tracker.Count, Is.Zero);
            Assert.That(tracker.Remaining(null), Is.Zero, "SkillData 없는 카드는 쿨타임이 없다");
        }

        [Test]
        public void Tick_HandlesManySkillsAtOnce()
        {
            var tracker = new SkillCooldownTracker();
            SkillData a = NewSkill(1f);
            SkillData b = NewSkill(3f);

            tracker.Start(a, 1f);
            tracker.Start(b, 3f);

            // 순회 중 하나가 끝나 지워져도 나머지는 그대로 흐른다.
            tracker.Tick(1f);

            Assert.That(tracker.IsCooling(a), Is.False);
            Assert.That(tracker.Remaining(b), Is.EqualTo(2f).Within(0.001f));
        }
    }
}
