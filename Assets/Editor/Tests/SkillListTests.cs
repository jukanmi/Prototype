using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 런 바의 [스킬 목록] → 보유 스킬 창(<see cref="SkillListUI"/>). 예전 "덱 N" 자리를 대신한다.
    /// </summary>
    public class SkillListTests
    {
        private readonly List<SkillData> made = new List<SkillData>();

        [TearDown]
        public void TearDown()
        {
            if (SkillListUI.Current != null)
            {
                SkillListUI.Current.Hide();
                Object.DestroyImmediate(SkillListUI.Current.gameObject);
            }
            PlayerInputController.SetSuspended(false);

            foreach (SkillData s in made) Object.DestroyImmediate(s);
            made.Clear();
        }

        private SkillData Skill(string name)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.skillName = name;
            made.Add(s);
            return s;
        }

        [Test]
        public void Order_SlottedFirst_ThenByCount_ThenByName()
        {
            var run = new RunProgression();
            run.AddSkill(Skill("가"), 1);
            run.AddSkill(Skill("나"), 3);
            run.AddSkill(Skill("다"), 2);
            run.AddSkill(Skill("라"), 2);
            run.SetAssaultSlot(0, "가");

            Assert.That(SkillListUI.Order(run), Is.EqualTo(new[] { "가", "나", "다", "라" }),
                        "총공격에 든 스킬이 먼저, 나머지는 많은 순 · 같으면 이름 순이어야 한다.");
        }

        [Test]
        public void Fill_OneRowPerOwnedSkill()
        {
            var run = new RunProgression();
            run.AddSkill(Skill("회전베기"), 2);
            run.AddSkill(Skill("방패치기"), 1);

            SkillListUI.Open();
            SkillListUI ui = SkillListUI.Current;
            ui.Fill(run);

            Assert.That(ui.RowCount, Is.EqualTo(2));
            Assert.That(ui.RowNames.ToList(), Is.EqualTo(SkillListUI.Order(run)));
        }

        [Test]
        public void Fill_EmptyRun_ShowsNoRows()
        {
            SkillListUI.Open();
            SkillListUI ui = SkillListUI.Current;
            ui.Fill(new RunProgression());

            Assert.That(ui.RowCount, Is.Zero);
            Assert.That(ui.transform.Find("Backdrop/Panel/Body/Empty").gameObject.activeSelf, Is.True,
                        "빈 목록인데 안내가 안 뜬다.");
        }

        [Test]
        public void Toggle_OpensAndCloses()
        {
            SkillListUI.Toggle();
            Assert.That(SkillListUI.IsOpen, Is.True);

            SkillListUI.Toggle();
            Assert.That(SkillListUI.IsOpen, Is.False);
        }

        [Test]
        public void UsesModalSkeleton_AboveRunBar()
        {
            SkillListUI.Open();
            Transform panel = SkillListUI.Current.transform.Find("Backdrop/Panel");

            Assert.That(panel, Is.Not.Null, "스킬 목록이 공용 창 골격으로 안 지어졌다.");
            Assert.That(panel.Find("Title").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("스킬 목록"));
            Assert.That(UiLayer.SkillList, Is.GreaterThan(UiLayer.RunBarOverWindow), "지도 · 칸의 런 바가 창을 덮는다.");
        }
    }
}
