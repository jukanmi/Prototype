using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 주인공 평타 표가 손맛 규칙대로 저작돼 있는지 본다 — 숫자는 튜닝이라 크기만 본다.
    /// <list type="bullet">
    /// <item>연타는 <b>마지막 타만</b> 방향키 쪽으로 조금 나간다(<see cref="BasicAttackStage.advanceSpeed"/>).</item>
    /// <item>달리기 공격은 기본 판정보다 넓게 친다(<see cref="BasicAttackStage.hitboxScale"/>).</item>
    /// </list>
    /// </summary>
    public class HeroAttackDataTests
    {
        private const string HeroPrefab = "Assets/Prefabs/Player/Player.prefab";
        private const string HeroData = "Assets/Data/Resources/Party/Hero_Player.asset";

        [Test]
        public void Combo_OnlyTheLastHitSteps()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefab);
            Assert.That(prefab, Is.Not.Null, $"{HeroPrefab} 가 없다");

            BasicAttackProfile profile = prefab.GetComponentInChildren<BasicAttackProfile>(true);
            Assert.That(profile, Is.Not.Null, "주인공에 평타 표가 없다");
            Assert.That(profile.StageCount, Is.GreaterThan(1), "연타가 아니다");

            int last = profile.StageCount - 1;
            Assert.That(profile.AdvanceFor(last), Is.GreaterThan(0f), "마지막 타가 안 나간다");

            for (int i = 0; i < last; i++)
                Assert.That(profile.AdvanceFor(i), Is.EqualTo(0f), $"{i + 1}타가 나간다 — 마지막 타만 나가야 한다");
        }

        [Test]
        public void RunAttack_HitsWiderThanTheBasicBox()
        {
            var data = AssetDatabase.LoadAssetAtPath<PlayerData>(HeroData);
            Assert.That(data, Is.Not.Null, $"{HeroData} 가 없다");

            Assert.That(data.runAttack.hitboxScale, Is.GreaterThan(1f), "달리기 공격 판정이 기본 크기 그대로다");
        }
    }
}
