using NUnit.Framework;
using Prototype.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 적 변종 프리팹이 실제로 쓸 수 있는 상태인지 검증한다.
    /// 프리팹은 컴파일 오류로 안 잡히는 배선 실수가 가장 많이 나는 곳이다.
    ///
    /// 생성기(<c>EnemyPrefabBuilder</c>)를 지운 뒤로는 <b>커밋된 애셋을 그대로</b> 검사한다.
    /// </summary>
    public class EnemyPrefabTests
    {
        private const string MeleePath = "Assets/Prefabs/Enemy_Melee.prefab";
        private const string RangedPath = "Assets/Prefabs/Enemy_Ranged.prefab";
        private const string ChargerPath = "Assets/Prefabs/Enemy_Charger.prefab";
        private const string BasePath = "Assets/Prefabs/enemy.prefab";

        private static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(go, Is.Not.Null, $"{path} 가 없다");
            return go;
        }

        // ── 공통 구조 ───────────────────────────────────

        [TestCase(MeleePath)]
        [TestCase(RangedPath)]
        [TestCase(ChargerPath)]
        public void EveryVariant_HasCoreComponents(string path)
        {
            GameObject go = Load(path);

            Assert.That(go.GetComponent<Enemy>(), Is.Not.Null, "Enemy 없음");
            Assert.That(go.GetComponent<EnemyControl>(), Is.Not.Null, "EnemyControl 없음");
            Assert.That(go.GetComponent<Physics>(), Is.Not.Null, "Physics 없음");
            Assert.That(go.GetComponent<Combat>(), Is.Not.Null, "Combat 없음");
        }

        /// <summary>평타 히트박스가 없으면 근접이든 돌진이든 아무도 못 때린다.</summary>
        [TestCase(MeleePath)]
        [TestCase(RangedPath)]
        [TestCase(ChargerPath)]
        public void EveryVariant_HasWiredBasicHitbox(string path)
        {
            GameObject go = Load(path);
            Enemy enemy = go.GetComponent<Enemy>();

            Assert.That(enemy.BasicAttack, Is.Not.Null, "평타 히트박스가 배선되지 않았다");
            Assert.That(enemy.BasicAttack.GetComponent<Collider>(), Is.Not.Null);
        }

        [TestCase(MeleePath)]
        [TestCase(RangedPath)]
        [TestCase(ChargerPath)]
        public void EveryVariant_HasDataAndBrain(string path)
        {
            GameObject go = Load(path);
            EnemyData data = go.GetComponent<Enemy>().Data;

            Assert.That(data, Is.Not.Null, "EnemyData 미배정");
            Assert.That(data.brain, Is.Not.Null, "EnemyData.brain 미배정");
            Assert.That(go.GetComponent<EnemyControl>().Brain, Is.Not.Null, "폴백 브레인 미배정");
        }

        [TestCase(MeleePath)]
        [TestCase(RangedPath)]
        [TestCase(ChargerPath)]
        public void EveryVariant_HasNoMissingScript(string path)
        {
            GameObject go = Load(path);

            foreach (Component c in go.GetComponentsInChildren<Component>(true))
                Assert.That(c, Is.Not.Null, $"{path} 에 missing script가 있다");
        }

        private static Material BodyMaterial(GameObject go)
        {
            Transform body = go.transform.Find("Body");
            Assert.That(body, Is.Not.Null, "Body 메쉬가 없다");
            return body.GetComponent<MeshRenderer>().sharedMaterial;
        }

        /// <summary>색이 같으면 씬에서 어느 적인지 구분이 안 된다.</summary>
        [Test]
        public void Variants_HaveDistinctColors()
        {
            Color melee = BodyMaterial(Load(MeleePath)).color;
            Color ranged = BodyMaterial(Load(RangedPath)).color;
            Color charger = BodyMaterial(Load(ChargerPath)).color;

            Assert.That(melee, Is.Not.EqualTo(ranged));
            Assert.That(ranged, Is.Not.EqualTo(charger));
            Assert.That(melee, Is.Not.EqualTo(charger));
        }

        [TestCase(MeleePath)]
        [TestCase(RangedPath)]
        [TestCase(ChargerPath)]
        public void EveryVariant_HasOwnMaterial(string path)
        {
            Material mat = BodyMaterial(Load(path));

            Assert.That(mat, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(mat), Does.StartWith("Assets/Prefabs/Materials/M_Enemy_"));
        }

        // ── 지오메트리 규약 ─────────────────────────────
        // Physics.Apply가 LookRotation(Facing)으로 루트를 돌린다. 정면은 로컬 +Z다.
        // ±X에 히트박스를 두면 회전 후 깊이축으로 빠져 옆에 선 대상에게 영영 닿지 않는다.

        [TestCase(MeleePath)]
        [TestCase(RangedPath)]
        [TestCase(ChargerPath)]
        public void EveryVariant_PutsHitboxInFront(string path)
        {
            Transform hitbox = Load(path).GetComponent<Enemy>().BasicAttack.transform;

            Assert.That(hitbox.localPosition.z, Is.GreaterThan(0.5f), "정면(+Z)에 있어야 한다");
            Assert.That(Mathf.Abs(hitbox.localPosition.x), Is.LessThan(0.01f), "X로 밀면 회전 후 깊이축으로 빠진다");
            Assert.That(hitbox.localPosition.y, Is.GreaterThan(0.5f), "몸통 높이에 맞춰야 한다");
        }

        /// <summary>몸통 캡슐이 바닥(y=0) 위에 서야 다른 캐릭터의 히트박스 높이와 겹친다.</summary>
        [TestCase(MeleePath)]
        [TestCase(RangedPath)]
        [TestCase(ChargerPath)]
        public void EveryVariant_StandsOnGround(string path)
        {
            var capsule = Load(path).GetComponent<CapsuleCollider>();

            Assert.That(capsule, Is.Not.Null);
            Assert.That(capsule.center.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(capsule.height, Is.EqualTo(2f).Within(0.001f));
        }

        /// <summary>루트가 Y축으로 도는데 스프라이트를 붙이면 옆면이 보여 사라진다.</summary>
        [TestCase(MeleePath)]
        [TestCase(RangedPath)]
        [TestCase(ChargerPath)]
        public void EveryVariant_UsesMeshBodyNotSprite(string path)
        {
            GameObject go = Load(path);

            Assert.That(go.GetComponent<SpriteRenderer>(), Is.Null, "회전하는 루트에 스프라이트가 남아 있다");
            Assert.That(go.transform.Find("Body"), Is.Not.Null);
        }

        // ── 종류별 ──────────────────────────────────────

        [Test]
        public void Melee_UsesMeleeBrain_AndStaysMelee()
        {
            GameObject go = Load(MeleePath);

            Assert.That(go.GetComponent<Enemy>().Data.brain, Is.TypeOf<MeleeBrainAsset>());
            Assert.That(go.GetComponent<Enemy>().BasicIsRanged, Is.False);
            Assert.That(go.GetComponent<EnemyChargeAction>(), Is.Null, "근접에 돌진 실행기가 붙어 있다");
        }

        [Test]
        public void Ranged_UsesRangedBrain_AndFiresProjectiles()
        {
            GameObject go = Load(RangedPath);
            Enemy enemy = go.GetComponent<Enemy>();

            Assert.That(enemy.Data.brain, Is.TypeOf<RangedBrainAsset>());
            Assert.That(enemy.BasicIsRanged, Is.True, "투사체가 배선되지 않았다");
            Assert.That(enemy.Data.basicProjectile, Is.Not.Null);
            Assert.That(enemy.Data.preferredMinRange, Is.GreaterThan(0f), "물러날 거리가 없다");
            Assert.That(enemy.Data.attackRange, Is.GreaterThan(enemy.Data.preferredMinRange));
        }

        [Test]
        public void Charger_UsesChargerBrain_AndHasChargeAction()
        {
            GameObject go = Load(ChargerPath);
            Enemy enemy = go.GetComponent<Enemy>();
            var action = go.GetComponent<EnemyChargeAction>();

            Assert.That(enemy.Data.brain, Is.TypeOf<ChargerBrainAsset>());
            Assert.That(action, Is.Not.Null, "돌진 실행기가 없다");
            Assert.That(action.ChargeHitbox, Is.Not.Null, "돌진 전용 히트박스가 배선되지 않았다");
            Assert.That(action.ChargeHitbox.gameObject, Is.Not.SameAs(enemy.BasicAttack.gameObject),
                        "돌진 히트박스가 평타 히트박스와 같으면 평타가 돌진을 끊는다");
            Assert.That(enemy.Data.specialRange, Is.GreaterThan(enemy.Data.attackRange));
        }

        // ── 안전장치 ────────────────────────────────────

        /// <summary>기존 프리팹을 갈아엎으면 씬 참조가 통째로 날아간다.</summary>
        [Test]
        public void BasePrefab_IsLeftAlone()
        {
            GameObject basePrefab = Load(BasePath);

            Assert.That(basePrefab.name, Is.EqualTo("enemy"));
            Assert.That(basePrefab.GetComponent<EnemyChargeAction>(), Is.Null);
        }

        /// <summary>변종마다 애셋이 한 벌씩만 있어야 한다. 복제본이 늘면 어느 쪽이 물리는지 알 수 없다.</summary>
        [TestCase(MeleePath)]
        [TestCase(RangedPath)]
        [TestCase(ChargerPath)]
        public void VariantAsset_ExistsExactlyOnce(string path)
        {
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Not.Empty, $"{path} 가 없다");

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            string[] found = AssetDatabase.FindAssets($"{name} t:EnemyData", new[] { "Assets/Data/Enemy" });
            Assert.That(found.Length, Is.EqualTo(1), $"{name} 데이터 애셋이 한 벌이 아니다");
        }
    }
}
