using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 카드 상세 패널이 <b>실제로 값을 싣는지</b>.
    ///
    /// 카드는 140×208이고 이름 · 설명 · 코스트는 아트 PNG에 그려져 있다 —
    /// <see cref="SkillData.description"/> · <c>manaCost</c> · <c>cooldown</c>은 화면에 한 번도 안 나왔다.
    /// 조립을 순수 함수로 빼 두었으므로 씬 없이 검증한다.
    /// </summary>
    public class ComboBoardDetailTests
    {
        private readonly List<Object> assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < assets.Count; i++) Object.DestroyImmediate(assets[i]);
            assets.Clear();
        }

        [Test]
        public void DetailLines_CarriesNameDescriptionAndCost()
        {
            SkillData data = NewSkill();

            string text = ComboBoardUI.DetailLines(data, chained: true, willBeDown: false);

            Assert.That(text, Does.Contain("올려베기"), "이름이 빠졌다");
            Assert.That(text, Does.Contain("적을 띄운다"), "설명이 빠졌다 — 이게 없어서 상세 패널을 만든 것이다");
            Assert.That(text, Does.Contain("20"), "마나 코스트가 빠졌다");
            Assert.That(text, Does.Contain("4"), "쿨다운이 빠졌다");
        }

        [Test]
        public void DetailRows_ShowsChainState()
        {
            SkillData data = NewSkill();

            string[] chained = ComboBoardUI.DetailRows(data, chained: true, willBeDown: false);
            string[] plain = ComboBoardUI.DetailRows(data, chained: false, willBeDown: false);

            Assert.That(chained[2], Does.Contain("강화"));
            Assert.That(plain[2], Does.Contain("기본"));
        }

        [Test]
        public void DetailRows_WarnsWhenTargetWillBeDown()
        {
            SkillData data = NewSkill();

            string[] rows = ComboBoardUI.DetailRows(data, chained: true, willBeDown: true);

            Assert.That(rows[2], Does.Contain("다운"),
                        "다운 무적에 흘릴 카드라는 걸 연계 줄이 말해야 한다");
            Assert.That(rows[2], Does.Contain("무효"));
        }

        [Test]
        public void DetailRows_EmptyDescription_StillReadable()
        {
            SkillData data = NewSkill();
            data.description = string.Empty;

            string[] rows = ComboBoardUI.DetailRows(data, chained: false, willBeDown: false);

            Assert.That(rows[1], Is.Not.Empty, "설명이 비어도 줄이 사라지면 패널 높이가 흔들린다");
        }

        private SkillData NewSkill()
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            assets.Add(data);

            data.skillName = "올려베기";
            data.description = "적을 띄운다. 공중 콤보의 시작.";
            data.role = Role.Warrior;
            data.attackType = AttackType.Launcher;
            data.requireState = CombatState.Neutral;
            data.resultState = CombatState.AerialHit;
            data.manaCost = 20f;
            data.cooldown = 4f;
            return data;
        }
    }
}
