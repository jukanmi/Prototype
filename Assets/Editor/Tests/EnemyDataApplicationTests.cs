using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// EnemyData의 원거리 수치가 Entity의 평타 설정으로 실제로 넘어가는지 검증한다.
    /// 여기서 새면 프리팹은 멀쩡한데 적이 근접처럼 붙는다.
    /// </summary>
    public class EnemyDataApplicationTests
    {
        private GameObject entityObject;
        private GameObject projectileObject;

        private Entity entity;
        private Projectile projectile;

        [SetUp]
        public void SetUp()
        {
            entityObject = new GameObject("Entity");
            entity = entityObject.AddComponent<Ally>();

            projectileObject = new GameObject("Projectile");
            projectileObject.AddComponent<BoxCollider>();
            projectileObject.AddComponent<Attack>();
            projectile = projectileObject.AddComponent<Projectile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(entityObject);
            Object.DestroyImmediate(projectileObject);
        }

        [Test]
        public void Default_IsMelee()
        {
            Assert.That(entity.BasicIsRanged, Is.False);
        }

        [Test]
        public void Configure_MakesBasicAttackRanged()
        {
            entity.ConfigureBasicProjectile(projectile, 20f, 10f, 0);

            Assert.That(entity.BasicIsRanged, Is.True);
        }

        /// <summary>AI가 멈춰 서는 거리. 최대 사거리 가장자리에서 헛쏘지 않게 짧게 잡는다.</summary>
        [Test]
        public void Configure_ReachIsEightyPercentOfRange()
        {
            entity.ConfigureBasicProjectile(projectile, 20f, 10f, 0);

            Assert.That(entity.BasicAttackReach, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void Configure_NonPositiveRange_FallsBackToMinimum()
        {
            entity.ConfigureBasicProjectile(projectile, 20f, 0f, 0);

            Assert.That(entity.BasicAttackReach, Is.GreaterThan(0f), "사거리 0이면 영영 공격 판정에 못 들어간다");
        }

        [Test]
        public void Configure_NonPositiveSpeed_FallsBackToMinimum()
        {
            entity.ConfigureBasicProjectile(projectile, 0f, 10f, 0);

            Assert.That(entity.BasicProjectileSpeed, Is.GreaterThan(0f), "속도 0이면 투사체가 제자리에 선다");
        }

        [Test]
        public void Configure_NegativePierce_ClampsToZero()
        {
            entity.ConfigureBasicProjectile(projectile, 20f, 10f, -3);

            Assert.That(entity.BasicProjectilePierce, Is.EqualTo(0));
        }

        [Test]
        public void Configure_NullPrefab_StaysMelee()
        {
            entity.ConfigureBasicProjectile(null, 20f, 10f, 0);

            Assert.That(entity.BasicIsRanged, Is.False);
        }

        /// <summary>EnemyData에 투사체가 없으면 근접 그대로여야 한다.</summary>
        [Test]
        public void EnemyWithMeleeData_StaysMelee()
        {
            var enemyObject = new GameObject("Enemy");
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.attackRange = 1.8f;

            Enemy enemy = enemyObject.AddComponent<Enemy>();
            enemy.ApplyData(data);

            Assert.That(enemy.BasicIsRanged, Is.False);

            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void EnemyWithRangedData_BecomesRanged()
        {
            var enemyObject = new GameObject("Enemy");
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.basicProjectile = projectile;
            data.projectileSpeed = 18f;
            data.projectileRange = 10f;
            data.projectilePierce = 0;

            Enemy enemy = enemyObject.AddComponent<Enemy>();
            enemy.ApplyData(data);

            Assert.That(enemy.BasicIsRanged, Is.True);
            Assert.That(enemy.BasicAttackReach, Is.EqualTo(8f).Within(0.001f));

            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(data);
        }
    }
}
