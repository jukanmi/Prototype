using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 이벤트 싸움의 검객 — 회전 베기 패턴(<see cref="EnemyPatternAction"/>) 하나를 쓴다.
    /// 예고(멈춤 · "!" · 바닥 원)를 보고 피할 수 있어야 하므로, 예고 뒤에 자리를 옮기면 안 된다.
    /// </summary>
    public class DuelistPatternTests
    {
        private const string DataPath = "Assets/Data/Enemy/Enemy_Duelist.asset";
        private const string MeleePrefab = "Assets/Prefabs/Enemy_Melee.prefab";

        private static EnemyData Load()
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DataPath);
            Assert.That(data, Is.Not.Null, $"{DataPath} 가 없다");
            Assert.That(data.prefab, Is.Not.Null, "검객 데이터에 프리팹이 없다");
            return data;
        }

        private static EnemyPattern Spin(EnemyData data)
        {
            var action = data.prefab.GetComponent<EnemyPatternAction>();
            Assert.That(action, Is.Not.Null, "검객에 패턴 실행기가 없다");
            Assert.That(action.Count, Is.EqualTo(1), "브레인은 특수 0번만 고른다 — 패턴이 하나여야 한다");
            return action.Patterns[0];
        }

        [Test]
        public void Spin_HasATelegraph_AndStaysPut()
        {
            EnemyPattern p = Spin(Load());

            Assert.That(p.telegraph, Is.GreaterThan(0f), "예고 없이 나간다");
            Assert.That(p.advanceSpeed, Is.EqualTo(0f), "휘두르며 움직인다 — 예고한 자리를 벗어난다");
            Assert.That(p.hitCount, Is.GreaterThan(0));
        }

        [Test]
        public void Spin_HitsAllAround_AndIsUsedWithinReach()
        {
            EnemyData data = Load();
            EnemyPattern p = Spin(data);

            Assert.That(p.hitbox, Is.Not.Null, "패턴 히트박스가 비었다 — 평타 상자(앞쪽)로 때린다");
            Assert.That(AttackRangePreview.TryReadDisk(p.hitbox, out _, out float radius), Is.True,
                        "둘레 판정(원반)이 아니다 — 바닥 예고도 원으로 안 그려진다");

            Assert.That(data.specialRange, Is.GreaterThan(0f), "쓸 거리가 0이라 브레인이 안 고른다");
            Assert.That(data.specialRange, Is.LessThanOrEqualTo(radius),
                        $"쓰는 거리 {data.specialRange} > 맞는 반지름 {radius} — 못 맞히는 자리에서 휘두른다");
        }

        [Test]
        public void Duelist_DoesNotChangeTheMeleeEnemy()
        {
            // 평타병 프리팹에 패턴을 달면 모든 근접 적이 회전 베기를 쓴다.
            var melee = AssetDatabase.LoadAssetAtPath<GameObject>(MeleePrefab);

            Assert.That(melee.GetComponent<EnemyPatternAction>(), Is.Null);
            Assert.That(Load().prefab, Is.Not.SameAs(melee));
        }
    }
}
