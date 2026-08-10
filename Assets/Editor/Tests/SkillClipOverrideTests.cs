using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// EntityAnimator.SwapSkillClip은 스킬의 <c>animation</c>이 비면(null) 오버라이드도
    /// 무조건 null로 덮어써 Skill 슬롯을 원래(플레이스홀더) 클립으로 되돌린다.
    /// 이 테스트는 그 전제 — AnimatorOverrideController 인덱서에 null을 대입하면
    /// 원본 클립으로 리셋되는지 — 를 직접 확인한다. 이게 깨지면 이전 스킬의 클립이
    /// 다음 스킬에도 그대로 남는다.
    /// </summary>
    public class SkillClipOverrideTests
    {
        private const string ControllerPath = "Assets/Data/Animation/EntityAnimator.controller";

        private AnimatorOverrideController overrideController;

        [TearDown]
        public void TearDown()
        {
            if (overrideController != null)
                Object.DestroyImmediate(overrideController);
        }

        [Test]
        public void AssigningNull_ResetsSlotToOriginalClip()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null, $"컨트롤러가 없다: {ControllerPath}");

            overrideController = new AnimatorOverrideController(controller);

            AnimationClip original = overrideController[EntityAnimator.SkillSlotClip];
            Assert.That(original, Is.Not.Null, "원본 Skill_Placeholder 클립이 없다");
            Assert.That(original.name, Is.EqualTo(EntityAnimator.SkillSlotClip));

            // 다른 클립을 꽂는다 — 컨트롤러가 아는 아무 클립이나 빌려온다.
            AnimationClip other = controller.animationClips.First(c => c != original);
            overrideController[EntityAnimator.SkillSlotClip] = other;
            Assert.That(overrideController[EntityAnimator.SkillSlotClip], Is.SameAs(other),
                "오버라이드 대입이 반영되지 않았다");

            // null을 대입하면 원본으로 되돌아간다 — EntityAnimator.SwapSkillClip이 기대는 동작.
            overrideController[EntityAnimator.SkillSlotClip] = null;
            Assert.That(overrideController[EntityAnimator.SkillSlotClip], Is.SameAs(original),
                "null 대입이 원본 클립으로 리셋하지 않았다");
        }
    }
}
