using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Prototype.EditorTools;

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
            // 경로가 아니라 컴포넌트로 찾는다 — 프리팹을 옮겨도 안 끊긴다.
            PrefabLocator.PlayerPath,
            PrefabLocator.AllyPath,
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

        /// <summary>
        /// 위 테스트는 컨트롤러가 아는 클립만 본다. 그런데 스킬 클립은 SkillData.animation을
        /// 통해 Skill 슬롯에 런타임으로 꽂히므로 controller.animationClips에 안 잡힌다 —
        /// 실제로 궁수 스킬 4종이 삭제된 AerialAttack.anim을 가리킨 채로 이 구멍을 통과했었다.
        /// SkillData 에셋을 전부 훑어 같은 검사를 돌린다.
        /// </summary>
        [Test]
        public void SkillAnimationClips_ResolveInEveryPrefabHierarchy()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                string skillPath = AssetDatabase.GUIDToAssetPath(guid);
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(skillPath);
                Assert.That(skill, Is.Not.Null, $"SkillData를 못 읽었다: {skillPath}");

                AnimationClip clip = skill.animation;
                if (clip == null) continue; // 비어 있으면 기본 플레이스홀더로 폴백 — 정상.

                foreach (string prefabPath in PrefabPaths)
                {
                    var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    Assert.That(root, Is.Not.Null, $"프리팹이 없다: {prefabPath}");

                    foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
                        AssertResolves(root, clip, b);

                    foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                        AssertResolves(root, clip, b);
                }
            }
        }
    }
}
