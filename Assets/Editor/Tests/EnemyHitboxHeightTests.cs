using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 적 히트박스는 바닥에 낮게 깐다 — <b>점프로 넘을 수 있게</b>. 규칙(진영 · 높이 예외)이 아니라
    /// 콜라이더 모양이 곧 판정이라, 높은 히트박스가 하나 끼면 그 공격만 조용히 점프로 못 피하게 된다.
    ///
    /// 몸통은 발 기준 −0.5 ~ +0.5(반지름 0.5 캡슐)다. 위쪽이 <see cref="MaxTop"/>이면
    /// 발이 MaxTop + 0.5 위로 뜨는 동안 안 맞는다 — 점프(높이 2.5 · 체공 0.7s) 중 약 0.5s.
    /// </summary>
    public class EnemyHitboxHeightTests
    {
        /// <summary>히트박스 위쪽 상한(발 기준).</summary>
        private const float MaxTop = 0.75f;

        [TestCase("Assets/Prefabs/Enemy_Melee.prefab")]
        [TestCase("Assets/Prefabs/Enemy_Ranged.prefab")]
        [TestCase("Assets/Prefabs/Enemy_Charger.prefab")]
        [TestCase("Assets/Prefabs/Enemy_Boss.prefab")]
        [TestCase("Assets/Prefabs/Enemy_Dummy.prefab")]
        public void EveryHitbox_IsLowEnoughToJumpOver(string path)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(root, Is.Not.Null, $"{path} 가 없다");

            Attack[] hitboxes = root.GetComponentsInChildren<Attack>(true);
            Assert.That(hitboxes, Is.Not.Empty);

            foreach (Attack a in hitboxes)
            {
                VerticalSpan(root.transform, a.GetComponent<Collider>(), out float bottom, out float top);

                Assert.That(top, Is.LessThanOrEqualTo(MaxTop + 0.001f), $"{a.name}: 위쪽 {top:0.###} — 점프로 못 넘는다");
                Assert.That(bottom, Is.LessThanOrEqualTo(0f), $"{a.name}: 아래쪽 {bottom:0.###} — 서 있는 대상의 발목이 빈다");
            }
        }

        /// <summary>콜라이더의 위아래 끝을 루트(발) 기준으로. 꺼진 콜라이더도 읽도록 bounds 대신 모양에서 잰다.</summary>
        private static void VerticalSpan(Transform root, Collider c, out float bottom, out float top)
        {
            Assert.That(c, Is.Not.Null);

            Bounds local;
            switch (c)
            {
                case BoxCollider b: local = new Bounds(b.center, b.size); break;
                case SphereCollider s: local = new Bounds(s.center, Vector3.one * s.radius * 2f); break;
                case MeshCollider m: local = m.sharedMesh.bounds; break;
                case CapsuleCollider cap:
                    Vector3 size = Vector3.one * cap.radius * 2f;
                    size[cap.direction] = Mathf.Max(cap.height, cap.radius * 2f);
                    local = new Bounds(cap.center, size);
                    break;
                default:
                    Assert.Fail($"{c.name}: 모르는 콜라이더 {c.GetType().Name}");
                    bottom = top = 0f;
                    return;
            }

            bottom = float.MaxValue;
            top = float.MinValue;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));

                float y = root.InverseTransformPoint(c.transform.TransformPoint(corner)).y;
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
            }
        }
    }
}
