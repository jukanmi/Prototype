using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 컨트롤러는 <b>한 벌</b>이다. 6종이 같은 상태머신·같은 클립을 본다.
    /// 캐릭터별 아트가 생기면 컨트롤러를 복사하는 게 아니라
    /// AnimatorOverrideController 애셋으로 클립만 갈아끼운다.
    /// </summary>
    public class AnimatorSetupTests
    {
        private const string ControllerPath = "Assets/Data/Animation/EntityAnimator.controller";

        private static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/Player.prefab",
            "Assets/Prefabs/Ally.prefab",
            "Assets/Prefabs/enemy.prefab",
            "Assets/Prefabs/Enemy_Melee.prefab",
            "Assets/Prefabs/Enemy_Charger.prefab",
            "Assets/Prefabs/Enemy_Ranged.prefab",
        };

        private static AnimatorController Controller()
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(c, Is.Not.Null, $"컨트롤러가 없다: {ControllerPath}");
            return c;
        }

        private static ChildAnimatorState[] States() => Controller().layers[0].stateMachine.states;

        [TestCaseSource(nameof(PrefabPaths))]
        public void EveryEntityPrefab_SharesOneController(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(root, Is.Not.Null, $"프리팹이 없다: {prefabPath}");

            var animator = root.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null, $"{root.name}: 루트에 Animator가 없다");
            Assert.That(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
                Is.EqualTo(ControllerPath), $"{root.name}: 공용 컨트롤러를 안 본다");

            var entityAnimator = root.GetComponent<EntityAnimator>();
            Assert.That(entityAnimator, Is.Not.Null, $"{root.name}: EntityAnimator가 없다");

            // 직렬화된 animator 필드가 비면 Awake의 GetComponentInChildren 폴백에 기대게 된다.
            // 그 폴백은 자식 순서에 의존하므로 프리팹에서는 명시 배선을 강제한다.
            var so = new SerializedObject(entityAnimator);
            Assert.That(so.FindProperty("animator").objectReferenceValue, Is.SameAs(animator),
                $"{root.name}: EntityAnimator.animator가 루트 Animator를 안 가리킨다");
        }

        /// <summary>
        /// AnimatorOverrideController의 인덱서 키는 <b>원본 클립의 이름</b>이다.
        /// Skill 스테이트에 다른 클립을 꽂으면 EntityAnimator.SwapSkillClip이 조용히 아무 일도 안 한다.
        /// </summary>
        [Test]
        public void SkillState_KeepsOverrideSlotClip()
        {
            ChildAnimatorState skill = States().Single(s => s.state.name == "Skill");
            Assert.That(skill.state.motion, Is.Not.Null, "Skill 스테이트에 모션이 비었다");
            Assert.That(skill.state.motion.name, Is.EqualTo(EntityAnimator.SkillSlotClip));
        }

        /// <summary>
        /// EntityAnimator는 상태 클래스 이름에서 "State"를 떼어 Animator 스테이트를 찾는다.
        /// 못 찾으면 HasState 검사에 걸려 조용히 넘어가므로, 오타가 런타임에 드러나지 않는다.
        /// </summary>
        [Test]
        public void EveryEntityState_HasMatchingAnimatorState()
        {
            var names = new HashSet<string>(States().Select(s => s.state.name));

            IEnumerable<Type> stateTypes = typeof(EntityState).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(IState).IsAssignableFrom(t));

            foreach (Type t in stateTypes)
                Assert.That(names, Contains.Item(AnimatorStateName(t)),
                    $"{t.Name}에 대응하는 Animator 스테이트가 없다");
        }

        /// <summary>EntityAnimator.Apply와 같은 규칙. 스킬 계열은 전부 "Skill" 하나로 모인다.</summary>
        private static string AnimatorStateName(Type t)
        {
            if (typeof(SkillState).IsAssignableFrom(t)) return "Skill";
            return t.Name.EndsWith("State") ? t.Name.Substring(0, t.Name.Length - "State".Length) : t.Name;
        }
    }
}
