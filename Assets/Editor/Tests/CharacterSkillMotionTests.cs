using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    public class CharacterSkillMotionTests
    {
        private GameObject instance;
        private AnimationClip temporaryClip;
        private SkillData temporarySkill;

        private EntityAnimator Create(string path)
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var animator = instance.GetComponent<EntityAnimator>();
            typeof(EntityAnimator).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(animator, null);
            return animator;
        }

        private AnimationClip SkillClip =>
            ((AnimatorOverrideController)instance.GetComponent<Animator>().runtimeAnimatorController)
                [EntityAnimator.SkillSlotClip];

        [TearDown]
        public void Cleanup()
        {
            if (instance != null) Object.DestroyImmediate(instance);
            if (temporaryClip != null) Object.DestroyImmediate(temporaryClip);
            if (temporarySkill != null) Object.DestroyImmediate(temporarySkill);
        }

        [TestCase("162b0d25154687b634727a49a865b9f3", "Nun_ChainPull")]
        [TestCase("f350a5b837d446148f3d2a368e433c2e", "Nun_Guard")]
        public void Tan_ResolvesLocalMotionEvenWhenSharedSkillHasNoAnimation(string guid, string expected)
        {
            var animator = Create("Assets/Prefabs/Player/Allies/Ally_Tan.prefab");
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
            AnimationClip original = skill.animation;
            animator.SetSkillClip(original, skill);
            Assert.That(SkillClip.name, Is.EqualTo(expected));
            Assert.That(skill.animation, Is.SameAs(original), "Shared skill assets must stay unchanged");

            animator.SetSkillClip(null);
            Assert.That(SkillClip.name, Is.EqualTo(EntityAnimator.SkillSlotClip), "Clearing a skill restores the rig's default motion");
        }

        [Test]
        public void CharacterWithoutLocalMotions_PreservesSharedClipAndNullFallback()
        {
            var animator = Create("Assets/Prefabs/Player/Ally.prefab");
            temporaryClip = new AnimationClip { name = "SharedSkill" };
            temporarySkill = ScriptableObject.CreateInstance<SkillData>();
            animator.SetSkillClip(temporaryClip, temporarySkill);
            Assert.That(SkillClip, Is.SameAs(temporaryClip));
            animator.SetSkillClip(null, temporarySkill);
            Assert.That(SkillClip.name, Is.EqualTo(EntityAnimator.SkillSlotClip));
        }
    }
}
