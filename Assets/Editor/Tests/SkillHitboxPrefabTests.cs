using NUnit.Framework;
using Prototype.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 스킬 히트박스가 프리팹에 붙어 있는지.
    ///
    /// 비어 있으면 <see cref="Entity.SkillAttack"/>이 평타 캡슐(r 0.5)로 떨어지고,
    /// 1·2타가 밀어낸 대상이 그 캡슐 밖으로 나가 <b>후속타가 씹힌다</b>.
    /// 회귀가 조용히 일어나는 자리라 프리팹 자체를 검사한다.
    /// </summary>
    public class SkillHitboxPrefabTests
    {
        [Test]
        public void EveryTargetPrefab_HasDedicatedSkillHitbox()
        {
            foreach (string path in SkillHitboxPrefabBuilder.TargetPrefabs)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(root, Is.Not.Null, $"{path}를 찾지 못했다");

                var entity = root.GetComponent<Entity>();
                Assert.That(entity, Is.Not.Null, $"{path}에 Entity가 없다");
                Assert.That(entity.BasicAttack, Is.Not.Null, $"{path}: 선행 조건 — 평타 히트박스가 있다");

                Assert.That(entity.SkillAttack, Is.Not.Null, $"{path}: 스킬 히트박스가 비어 있다");
                Assert.That(entity.SkillAttack, Is.Not.SameAs(entity.BasicAttack),
                            $"{path}: 스킬이 평타 히트박스로 떨어지면 다단히트 후속타가 씹힌다 — " +
                            "Prototype ▸ 프리팹 - 스킬 히트박스 붙이기 를 실행할 것");
            }
        }

        [Test]
        public void SkillHitbox_IsTrigger_AndBiggerThanBasic()
        {
            foreach (string path in SkillHitboxPrefabBuilder.TargetPrefabs)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var entity = root.GetComponent<Entity>();

                var skill = entity.SkillAttack.GetComponent<Collider>();
                var basic = entity.BasicAttack.GetComponent<Collider>();

                Assert.That(skill, Is.Not.Null, $"{path}: 스킬 히트박스에 콜라이더가 없다");
                Assert.That(skill.isTrigger, Is.True, $"{path}: 히트박스는 트리거여야 한다");

                // 밀려난 대상을 다시 잡으려면 평타보다 넓어야 한다.
                // bounds는 씬에 없는 프리팹 애셋에서 전부 0이라 못 쓴다 — 콜라이더 치수를 직접 읽는다.
                Vector3 s = LocalSize(skill);
                Vector3 b = LocalSize(basic);
                Assert.That(s.x, Is.GreaterThan(b.x), $"{path}: 스킬 히트박스가 평타보다 좁다");
                Assert.That(s.z, Is.GreaterThan(b.z), $"{path}: 스킬 히트박스 깊이가 평타보다 얕다");
            }
        }

        /// <summary>콜라이더의 로컬 치수. 프리팹 애셋은 월드 bounds가 없다.</summary>
        private static Vector3 LocalSize(Collider c)
        {
            switch (c)
            {
                case BoxCollider box: return box.size;
                case CapsuleCollider cap: return new Vector3(cap.radius * 2f, cap.height, cap.radius * 2f);
                case SphereCollider sphere: return Vector3.one * sphere.radius * 2f;
                default: return Vector3.zero;
            }
        }

        /// <summary>
        /// 켠 직후의 겹침 스윕이 쓰는 마스크. 충돌 매트릭스를 그대로 따라가야
        /// 스윕이 벽이나 아군을 새로 뚫고 때리지 않는다.
        /// </summary>
        [Test]
        public void SweepMask_FollowsCollisionMatrix()
        {
            const int AllyHitbox = 12;

            int mask = Attack.BuildSweepMask(AllyHitbox);

            for (int i = 0; i < 32; i++)
            {
                bool ignored = UnityEngine.Physics.GetIgnoreLayerCollision(AllyHitbox, i);
                bool inMask = (mask & (1 << i)) != 0;

                Assert.That(inMask, Is.EqualTo(!ignored),
                            $"레이어 {i}: 충돌 매트릭스와 스윕 마스크가 어긋난다");
            }
        }
    }
}
