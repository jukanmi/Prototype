using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 애니메이션 커브는 <b>Animator 기준 상대 경로</b>로 대상을 찾는다.
    /// 계층에 노드를 하나 끼워 넣으면 클립이 통째로 조용히 죽는다 — 에러도 경고도 없이,
    /// 그냥 아무것도 안 움직인다. 실제로 커밋 7099ac1이 `View`를 넣으면서 그렇게 됐다.
    ///
    /// 그래서 "경로가 계층에서 해석되는가"를 테스트로 고정한다.
    /// </summary>
    public class AnimationBindingTests
    {
        private static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/Player.prefab",
            "Assets/Prefabs/Ally.prefab",
            "Assets/Prefabs/enemy.prefab",
            "Assets/Prefabs/Enemy_Melee.prefab",
            "Assets/Prefabs/Enemy_Charger.prefab",
            "Assets/Prefabs/Enemy_Ranged.prefab",
        };

        [TestCaseSource(nameof(PrefabPaths))]
        public void EveryCurvePath_ResolvesInPrefabHierarchy(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(root, Is.Not.Null, $"프리팹이 없다: {prefabPath}");

            var animator = root.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null, $"{root.name}: 루트에 Animator가 없다");

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            Assert.That(controller, Is.Not.Null, $"{root.name}: Animator에 컨트롤러가 비었다");
            Assert.That(controller.animationClips, Is.Not.Empty, $"{root.name}: 컨트롤러에 클립이 없다");

            foreach (AnimationClip clip in controller.animationClips)
            {
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
                    AssertResolves(root, clip, b);

                // 스프라이트 교체는 PPtr 커브라 위 목록에 안 나온다. 따로 물어봐야 한다.
                foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    AssertResolves(root, clip, b);
            }
        }

        private static void AssertResolves(GameObject root, AnimationClip clip, EditorCurveBinding binding)
        {
            Transform target = string.IsNullOrEmpty(binding.path)
                ? root.transform
                : root.transform.Find(binding.path);

            Assert.That(target, Is.Not.Null,
                $"{root.name} / {clip.name}: 경로 '{binding.path}'가 계층에 없다 ({binding.propertyName})");
        }
    }
}
