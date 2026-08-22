using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 밀치기는 <b>무조건 벽으로</b> 간다.
    ///
    /// 예전에는 시전자 반대쪽(AwayFromCaster)이었다. 그래서 시전자가 어디 섰느냐에 따라
    /// 같은 카드가 적을 벽에 처박기도 하고 방 한복판으로 날려 아무 일도 안 일어나게도 했다.
    /// 밀치기의 존재 이유가 벽바운드 연계이므로 방향은 벽이 정한다.
    ///
    /// 두 층을 같이 본다 — 방향 계산(<see cref="WallFinder"/>)과 에셋 데이터.
    /// 코드만 고치고 에셋을 안 고치면 조용히 옛 동작으로 돌아간다.
    /// </summary>
    public class PushToWallTests
    {
        private const string SkillFolder = "Assets/Data/Skills";

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        // ── 방향 계산 ────────────────────────────────────

        [Test]
        public void PushDirection_PointsAtTheNearerWall()
        {
            // 오른쪽 벽이 4, 왼쪽 벽이 12만큼 떨어져 있다.
            Wall(new Vector3(4f, 0f, 0f));
            Wall(new Vector3(-12f, 0f, 0f));

            LayerMask mask = ~0;
            Vector3 dir = WallFinder.PushDirection(Vector3.zero, mask, Vector3.left);

            Assert.That(dir.x, Is.GreaterThan(0.5f),
                        "가까운 쪽은 오른쪽 벽이다 — fallback(왼쪽)이 이기면 안 된다");
        }

        [Test]
        public void PushDirection_FallsBackWhenNoWall()
        {
            // 벽 레이어에 아무것도 없는 마스크. 레이가 잡을 게 없다.
            LayerMask empty = 0;
            Vector3 dir = WallFinder.PushDirection(Vector3.zero, empty, Vector3.left);

            Assert.That(dir, Is.EqualTo(Vector3.left).Using(Vec3Comparer),
                        "벽이 없으면 원래 방향(시전자 반대쪽)으로 떨어진다");
        }

        [Test]
        public void DistanceToWall_MatchesGeometry()
        {
            Wall(new Vector3(6f, 0f, 0f));

            float d = WallFinder.DistanceToWall(Vector3.zero, Vector3.right, ~0);

            // 벽 큐브의 반쪽(0.5)만큼 앞에서 맞는다.
            Assert.That(d, Is.EqualTo(5.5f).Within(0.6f));
        }

        [Test]
        public void ResolveDirection_TowardWall_UsesWallNotCaster()
        {
            Wall(new Vector3(5f, 0f, 0f));

            var hit = new HitData { mode = KnockbackMode.TowardWall, knockbackForce = 20f };

            // 시전자가 대상의 오른쪽에 서 있다 — AwayFromCaster였다면 왼쪽(벽 반대)으로 밀린다.
            Vector3 casterPos = new Vector3(2f, 0f, 0f);
            Vector3 targetPos = Vector3.zero;

            Vector3 dir = hit.ResolveDirection(casterPos, Vector3.right, targetPos, ~0);

            Assert.That(dir.x, Is.GreaterThan(0.5f),
                        "시전자가 벽 쪽에 서 있어도 적은 벽으로 간다");
        }

        // ── 에셋 데이터 ──────────────────────────────────

        [Test]
        public void EveryPushSkill_SendsTargetToWall()
        {
            foreach (SkillData s in PushSkills())
            {
                Assert.That(s.hitDataList[0].mode, Is.EqualTo(KnockbackMode.TowardWall),
                            $"{s.name}: 밀치기는 벽으로만 보낸다");
                Assert.That(s.targetPick, Is.EqualTo(TargetPick.Farthest),
                            $"{s.name}: 가장 먼 적이어야 벽까지 밀 거리가 나온다");
                Assert.That(s.hitDataList[0].knockbackForce, Is.GreaterThan(0f),
                            $"{s.name}: 밀 힘이 0이면 벽까지 못 간다");
            }
        }

        /// <summary>
        /// 밀치기는 체인의 마무리이자 <b>보스 가드를 허무는 역할</b>이다.
        /// 데미지가 공격기보다 낮으면 마무리를 넣을 이유가 사라진다.
        /// </summary>
        [Test]
        public void PushSkills_HitHardAndBreakGuard()
        {
            foreach (SkillData s in PushSkills())
            {
                Assert.That(s.hitDataList[0].damageData.damage, Is.GreaterThanOrEqualTo(30f),
                            $"{s.name}: 밀치기 데미지가 너무 낮다 — 체인 마무리로서 값이 안 된다");
                // 보스 평타 한 대분(BossPrefabBuilder.GuardDamage)과 직접 견준다.
                // 상수로 1을 적어 두면 가드 스케일이 바뀔 때 이 테스트만 조용히 무의미해진다 —
                // 실제로 defaultGuardDamage가 1에서 6으로 오른 적이 있다.
                Assert.That(s.hitDataList[0].guardDamage,
                            Is.GreaterThan(Prototype.EditorTools.BossPrefabBuilder.GuardDamage),
                            $"{s.name}: 가드를 보스 평타 한 대분 이하로 깎으면 가드 브레이커가 아니다");
            }
        }

        /// <summary>
        /// 직업마다 정석 4슬롯 체인이 한 벌씩 성립하는지.
        /// 한 유형이 둘이고 다른 유형이 없으면 그 직업만으로는 콤보 한 싸이클이 안 돈다.
        /// </summary>
        [Test]
        public void EveryRole_HasFullChain()
        {
            AttackType[] chain =
            {
                AttackType.Gather, AttackType.Launcher, AttackType.Strike, AttackType.Push,
            };

            foreach (Role role in System.Enum.GetValues(typeof(Role)))
            {
                var types = new List<AttackType>();
                foreach (SkillData s in AllSkills())
                    if (s.role == role) types.Add(s.attackType);

                foreach (AttackType need in chain)
                    Assert.That(types, Contains.Item(need),
                                $"[{role}] {need} 스킬이 없다 — 이 직업만으로는 체인이 안 돈다");
            }
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private static IEnumerable<SkillData> PushSkills()
        {
            foreach (SkillData s in AllSkills())
            {
                if (s.attackType != AttackType.Push) continue;
                if (s.hitDataList == null || s.hitDataList.Count == 0)
                {
                    Assert.Fail($"{s.name}: 밀치기인데 hitDataList가 비었다");
                    continue;
                }

                yield return s;
            }
        }

        private static IEnumerable<SkillData> AllSkills()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SkillData", new[] { SkillFolder }))
            {
                var s = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null) yield return s;
            }
        }

        /// <summary>레이가 맞을 벽 한 장. 큐브 한 변이 1이라 표면은 중심에서 0.5 앞이다.</summary>
        private GameObject Wall(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Wall";
            go.transform.position = position;
            go.transform.localScale = new Vector3(1f, 4f, 40f);
            spawned.Add(go);

            // 에디트모드에는 물리 스텝이 돌지 않는다. 방금 옮긴 콜라이더를 레이가 보려면 직접 맞춰 준다.
            UnityEngine.Physics.SyncTransforms();
            return go;
        }

        private static readonly IEqualityComparer<Vector3> Vec3Comparer = new ApproxVector3();

        private class ApproxVector3 : IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.0001f;
            public int GetHashCode(Vector3 v) => v.GetHashCode();
        }
    }
}
