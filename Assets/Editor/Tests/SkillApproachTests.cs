using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 스킬 자동 발동의 <b>유일한 규칙</b>: 대상보다 멀면 사거리 안까지 들어가 때린다.
    ///
    /// 예전에는 직업으로 갈렸다 — 탱커·전사만 이동하고 궁수·마법사는 제자리. 그래서 같은 카드가
    /// 시전자에 따라 다르게 움직였고, 원거리는 사거리 밖이면 아무 일도 없이 투사체만 증발했다.
    /// 여기서 지키는 건 "규칙이 하나"라는 사실이다.
    /// </summary>
    public class SkillApproachTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            for (int i = 0; i < assets.Count; i++)
                if (assets[i] != null) Object.DestroyImmediate(assets[i]);

            spawned.Clear();
            assets.Clear();
        }

        // ── 성격별 기본 접근 거리 ────────────────────────

        [Test]
        public void Melee_StandsNextToTarget()
        {
            SkillData melee = NewSkill(Role.Warrior);

            Assert.That(melee.ApproachDistance, Is.EqualTo(1.1f).Within(0.001f),
                        "근접은 몸이 닿아야 한다");
        }

        [Test]
        public void Area_KeepsHalfItsRadius()
        {
            SkillData area = NewSkill(Role.Wizard);
            area.radius = 6f;

            Assert.That(area.ApproachDistance, Is.EqualTo(3f).Within(0.001f),
                        "장판은 자기 반경의 절반까지만 들어가면 빗나갈 일이 없다");
        }

        [Test]
        public void Ranged_KeepsHalfItsRange()
        {
            SkillData ranged = NewSkill(Role.Archer);
            ranged.projectile = NewProjectile();
            ranged.projectileRange = 12f;

            Assert.That(ranged.ApproachDistance, Is.EqualTo(6f).Within(0.001f),
                        "투사체는 사거리 절반이면 확실히 닿는다");
        }

        [Test]
        public void ExplicitValue_Wins()
        {
            SkillData s = NewSkill(Role.Warrior);
            s.approachDistance = 4.5f;

            Assert.That(s.ApproachDistance, Is.EqualTo(4.5f).Within(0.001f),
                        "에셋에 직접 적은 값은 성격 추론을 이긴다");
        }

        // ── 이동 판단 ────────────────────────────────────

        [Test]
        public void FarTarget_MovesIntoRange()
        {
            Entity target = Dummy(new Vector3(10f, 0f, 0f));

            bool moved = SkillState.TryApproach(Vector3.zero, target, 1.1f, out Vector3 spot);

            Assert.That(moved, Is.True, "10만큼 떨어져 있으면 들어가야 한다");
            Assert.That(Vector3.Distance(spot, target.transform.position),
                        Is.EqualTo(1.1f).Within(0.01f),
                        "정확히 접근 거리만큼 떨어진 자리에 선다");
        }

        [Test]
        public void CloseTarget_StaysPut()
        {
            Entity target = Dummy(new Vector3(3f, 0f, 0f));

            bool moved = SkillState.TryApproach(Vector3.zero, target, 6f, out _);

            Assert.That(moved, Is.False,
                        "이미 사거리 안이면 움직이지 않는다 — 코앞의 궁수를 뒤로 밀어내지 않는다");
        }

        [Test]
        public void ApproachSpot_KeepsTargetLane()
        {
            Entity target = Dummy(new Vector3(10f, 0f, 3f));

            SkillState.TryApproach(Vector3.zero, target, 1.1f, out Vector3 spot);

            Assert.That(spot.z, Is.EqualTo(3f).Within(0.001f),
                        "벨트스크롤에서 Z가 어긋나면 후속타가 전부 빗나간다");
        }

        [Test]
        public void ApproachSpot_ComesFromTheSideWeCameFrom()
        {
            Entity target = Dummy(new Vector3(10f, 0f, 0f));

            SkillState.TryApproach(Vector3.zero, target, 1.1f, out Vector3 spot);

            Assert.That(spot.x, Is.LessThan(target.transform.position.x),
                        "대상을 관통해 반대편으로 넘어가지 않는다");
        }

        [Test]
        public void NullTarget_DoesNotMove()
        {
            Assert.That(SkillState.TryApproach(Vector3.zero, null, 1.1f, out _), Is.False);
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private SkillData NewSkill(Role role)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.role = role;
            assets.Add(s);
            return s;
        }

        private Projectile NewProjectile()
        {
            var go = new GameObject("Projectile");
            spawned.Add(go);
            return go.AddComponent<Projectile>();
        }

        private Entity Dummy(Vector3 position)
        {
            var go = new GameObject("Target", typeof(Rigidbody), typeof(Physics), typeof(Combat), typeof(Entity));
            go.transform.position = position;
            spawned.Add(go);
            return go.GetComponent<Entity>();
        }
    }
}
