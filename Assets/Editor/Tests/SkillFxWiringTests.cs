using NUnit.Framework;
using UnityEditor;

namespace Prototype.Tests
{
    /// <summary>
    /// 스킬 전용 연출(<see cref="SkillFxKind"/>) 배선.
    ///
    /// 연출은 타격 시각에 붙어 돌기 때문에 에셋의 타 수가 연출 구조와 맞아야 한다 —
    /// 서릿발은 퍼짐 · 깨짐 두 타, 일섬은 돌진 한 타 + 참격들. 타 수를 바꾸면 여기서 먼저 알린다.
    /// 셰이더도 Resources의 VfxLibrary가 잡고 있어야 빌드에 들어간다.
    /// </summary>
    public class SkillFxWiringTests
    {
        private const string SkillFolder = "Assets/Data/Skills/";

        private static SkillData Load(string name)
        {
            var s = AssetDatabase.LoadAssetAtPath<SkillData>(SkillFolder + name + ".asset");
            Assert.IsNotNull(s, $"{name} 에셋이 없다");
            return s;
        }

        [TestCase("SK_TK올려베기", SkillFxKind.UpwardSlash)]
        [TestCase("SK_WR회전베기", SkillFxKind.SpinSlash)]
        [TestCase("SK_WR서릿발", SkillFxKind.FrostField)]
        [TestCase("SK_WR일섬", SkillFxKind.FlashSlash)]
        [TestCase("SK_WR연격", SkillFxKind.ComboSlash)]
        [TestCase("SK_TK사슬견인", SkillFxKind.HookChain)]
        [TestCase("SK_WZ사슬속박", SkillFxKind.ChainBind)]
        [TestCase("SK_WZ마력화살", SkillFxKind.MagicMissile)]
        [TestCase("SK_WZ마력균열", SkillFxKind.ManaRift)]
        [TestCase("SK_TK숄더차지", SkillFxKind.ShoulderCharge)]
        [TestCase("SK_WZ마나스피어", SkillFxKind.ManaSpear)]
        public void Skill_UsesItsFx(string name, SkillFxKind kind)
        {
            Assert.AreEqual(kind, Load(name).fx);
        }

        [Test]
        public void FrostField_HasSpreadAndBurstHits()
        {
            Assert.GreaterOrEqual(Load("SK_WR서릿발").hitDataList.Count, 2, "1타 퍼짐 · 2타 깨짐");
        }

        [Test]
        public void FlashSlash_HasDashAndSlashHits()
        {
            Assert.GreaterOrEqual(Load("SK_WR일섬").hitDataList.Count, 2, "1타 돌진 · 2타부터 참격");
        }

        [Test]
        public void FrostField_BurstFitsBetweenHits()
        {
            // 퍼짐(최소 0.08s) + 달아오름이 1 → 2타 사이에 들어가야 깨짐이 2타 판정과 겹친다.
            SkillData s = Load("SK_WR서릿발");
            Assert.Greater(s.HitTime(1) - s.HitTime(0), 0.08f);
        }

        [Test]
        public void ChainBind_AppliesAirBind()
        {
            // 연출은 AirBind가 풀리는 순간 사슬을 끊는다. 다른 디버프로 바꾸면 사슬이 0.5초 만에 끊긴다.
            HitData hit = Load("SK_WZ사슬속박").hitDataList[0];
            Assert.IsTrue((hit.debuff & Debuff.AirBind) != 0);
            Assert.Greater(hit.debuffDuration, 0f);
        }

        [Test]
        public void MagicMissile_ImpactOffsetsStayOnHitbox()
        {
            // 탄착점은 히트박스 배수(-0.5..0.5)를 반폭 앵커(-1..1)로 바꿔 쓴다. 밖이면 잘린다.
            SkillData s = Load("SK_WZ마력화살");
            Assert.IsTrue(s.detonateOnArrival, "마력탄이 투사체 도착과 같은 순간에 닿도록 맞춘다");
            foreach (HitData h in s.hitDataList)
            {
                Assert.LessOrEqual(UnityEngine.Mathf.Abs(h.impactOffset.x), 0.5f);
                Assert.LessOrEqual(UnityEngine.Mathf.Abs(h.impactOffset.y), 0.5f);
            }
        }

        [Test]
        public void ManaRift_IsInstallWithStormHits()
        {
            // 형성 = 1타, 폭발 = 마지막 타. 시전 순간 놓이는 설치기라야 시전 때 연 균열과 시계가 같다.
            SkillData s = Load("SK_WZ마력균열");
            Assert.IsTrue(s.install);
            Assert.GreaterOrEqual(s.hitDataList.Count, 2);
        }

        [Test]
        public void VfxLibrary_HoldsSkillFxShaders()
        {
            VfxLibrary lib = AssetDatabase.LoadAssetAtPath<VfxLibrary>("Assets/Data/Resources/VfxLibrary.asset");
            Assert.IsNotNull(lib);
            Assert.IsNotNull(lib.animeSlash, "VFX/AnimeSlash");
            Assert.IsNotNull(lib.slashSpark, "VFX/SlashSpark");
            Assert.IsNotNull(lib.spinSlash, "VFX/SpinSlash");
            Assert.IsNotNull(lib.frostField, "VFX/FrostField");
            Assert.IsNotNull(lib.flashSlash, "VFX/FlashSlash");
            Assert.IsNotNull(lib.hookChain, "VFX/HookChain");
            Assert.IsNotNull(lib.chainBind, "VFX/ChainBind");
            Assert.IsNotNull(lib.magicMissile, "VFX/MagicMissile");
            Assert.IsNotNull(lib.manaRift, "VFX/ManaRift");
            Assert.IsNotNull(lib.manaSpear, "VFX/ManaSpear");
            Assert.IsNotNull(lib.shoulderCharge, "VFX/ShoulderCharge");
            Assert.IsNotNull(lib.spriteSilhouette, "VFX/SpriteSilhouette");
        }
    }
}
