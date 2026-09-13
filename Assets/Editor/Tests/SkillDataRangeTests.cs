using NUnit.Framework;
using Prototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Prototype.Tests
{
    /// <summary>
    /// 원거리 직업 스킬이 <b>반경</b>으로 때리는지 <b>전방 상자</b>로 때리는지.
    /// 시전 범위를 적은 원거리 스킬(마나 스피어)은 근거리와 같은 상자 경로를 타야 한다 —
    /// 반경 경로로 가면 판정이 시전자 발밑에서 터져 긴 직선이 나오지 않는다.
    /// </summary>
    public class SkillDataRangeTests
    {
        private SkillData skill;

        [SetUp]
        public void SetUp()
        {
            skill = ScriptableObject.CreateInstance<SkillData>();
            skill.role = Role.Wizard;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(skill);

        [Test]
        public void 원거리_시전범위_없으면_반경으로_때린다()
        {
            skill.castRangeScale = Vector3.zero;
            Assert.IsTrue(skill.UsesRadius);
            Assert.IsTrue(skill.IsAreaSkill);
        }

        [Test]
        public void 원거리_시전범위_있으면_전방상자로_때린다()
        {
            skill.castRangeScale = new Vector3(2f, 1f, 10f);
            Assert.IsFalse(skill.UsesRadius);
            Assert.IsFalse(skill.IsAreaSkill);
        }
    }
}
