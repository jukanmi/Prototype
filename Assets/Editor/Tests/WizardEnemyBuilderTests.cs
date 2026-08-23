using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Prototype.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 마법사 원거리 적의 배선 규약.
    ///
    /// 이 적은 지금까지의 적과 <b>구조가 다르다</b> — 몸이 렌더러 16장짜리 스켈레탈 리그다.
    /// 그래서 "루트에 Animator", "flipX로 반전" 같은 기존 규약이 그대로는 안 통하고,
    /// 어긋나도 컴파일로는 안 잡힌다. 화면에서 리그가 분해되거나 클립이 아무 데도 안 붙는
    /// 형태로만 드러나므로 여기서 고정한다.
    /// </summary>
    public class WizardEnemyBuilderTests
    {
        private const string PrefabPath = "Assets/Prefabs/Enemy_WizardRanged.prefab";
        private const string ControllerPath = "Assets/Data/Animation/WizardAnimator.controller";
        private const string ClipFolder = "Assets/Data/Animation";
        private const string ArtFolder = "Assets/Art/Character/Wizard - 2D Character";
        private const string SoftDataPath = "Assets/Data/Enemy/Enemy_WizardSoft.asset";
        private const string HardDataPath = "Assets/Data/Enemy/Enemy_WizardHard.asset";

        private const string SpriteChild = "View/Sprite";

        [OneTimeSetUp]
        public void BuildOnce() => WizardEnemyBuilder.Build();

        private static GameObject Prefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(go, Is.Not.Null, $"{PrefabPath}가 생성되지 않았다");
            return go;
        }

        private static AnimatorController Controller()
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(c, Is.Not.Null, $"{ControllerPath}가 생성되지 않았다");
            return c;
        }

        private static ChildAnimatorState[] States() => Controller().layers[0].stateMachine.states;

        private static EnemyData Data(string path)
        {
            var d = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            Assert.That(d, Is.Not.Null, $"{path}가 생성되지 않았다");
            return d;
        }

        // ── 애니메이터 ──────────────────────────────────

        /// <summary>
        /// 클립의 바인딩 경로가 리그 루트(<c>Skeletal/...</c>) 기준이라 Animator가 리그를 떠나면
        /// 커브가 아무 데도 안 붙는다 — 마법사가 T포즈로 미끄러진다.
        /// 루트에 Animator를 남겨 두면 EntityAnimator의 폴백이 그쪽을 집으므로 같이 막는다.
        /// </summary>
        [Test]
        public void Animator_LivesOnTheRig_NotOnTheRoot()
        {
            GameObject root = Prefab();

            Assert.That(root.GetComponent<Animator>(), Is.Null,
                "루트에 Animator가 남아 있다. EntityAnimator의 폴백이 리그 대신 이쪽을 집는다");

            var entityAnimator = root.GetComponent<EntityAnimator>();
            Assert.That(entityAnimator, Is.Not.Null, "EntityAnimator가 없다");

            var so = new SerializedObject(entityAnimator);
            var animator = so.FindProperty("animator").objectReferenceValue as Animator;

            Assert.That(animator, Is.Not.Null, "EntityAnimator.animator가 비었다");
            Assert.That(animator.transform.IsChildOf(root.transform.Find(SpriteChild)), Is.True,
                $"Animator가 {SpriteChild} 아래 리그에 있지 않다");
            Assert.That(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
                Is.EqualTo(ControllerPath), "마법사 전용 컨트롤러를 안 본다");
        }

        /// <summary>
        /// <see cref="EntityAnimator"/>는 상태 클래스 이름에서 "State"를 떼어 Animator 스테이트를 찾는다.
        /// 못 찾으면 조용히 넘어가므로 빠진 상태가 런타임에 드러나지 않는다.
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

        private static string AnimatorStateName(Type t)
        {
            if (typeof(SkillState).IsAssignableFrom(t)) return "Skill";
            return t.Name.EndsWith("State") ? t.Name.Substring(0, t.Name.Length - "State".Length) : t.Name;
        }

        /// <summary>
        /// 클립 이름이 곧 조회 키다. <see cref="EntityAnimator"/>가 상태 길이에 재생 속도를 맞출 때
        /// <c>{직업}_{상태}</c> 규칙으로 클립을 찾으므로, 원본 이름(Run · Hurt · Die) 그대로면
        /// 그 조회가 전부 빗나가 평타 모션과 히트박스가 따로 논다.
        /// </summary>
        [Test]
        public void EveryState_HasMotion_NamedForItsState()
        {
            foreach (ChildAnimatorState s in States())
            {
                Assert.That(s.state.motion, Is.Not.Null, $"{s.state.name}: 모션이 비었다");

                string expected = s.state.name == "Skill"
                    ? EntityAnimator.SkillSlotClip
                    : "Wizard_" + s.state.name;

                Assert.That(s.state.motion.name, Is.EqualTo(expected),
                    $"{s.state.name}: 모션 이름이 조회 규칙과 다르다");
            }
        }

        /// <summary>
        /// AnimatorOverrideController의 인덱서 키는 <b>원본 클립의 이름</b>이다.
        /// Skill 스테이트에 다른 클립을 꽂으면 EntityAnimator.SwapSkillClip이 조용히 아무 일도 안 한다.
        /// </summary>
        [Test]
        public void SkillState_KeepsOverrideSlotClip()
        {
            ChildAnimatorState skill = States().Single(s => s.state.name == "Skill");
            Assert.That(skill.state.motion.name, Is.EqualTo(EntityAnimator.SkillSlotClip));
        }

        /// <summary>
        /// 클립은 <b>복사본</b>이어야 한다. 에셋 스토어 패키지 폴더의 원본을 그대로 물리면
        /// 패키지를 다시 임포트하는 순간 이름과 커브가 덮어써진다.
        /// </summary>
        [Test]
        public void EveryClip_LivesOutsideTheStorePackage()
        {
            foreach (ChildAnimatorState s in States())
            {
                string path = AssetDatabase.GetAssetPath(s.state.motion);

                Assert.That(path.StartsWith(ClipFolder), Is.True,
                    $"{s.state.name}: 클립이 {ClipFolder} 밖에 있다 ({path})");
                Assert.That(path.StartsWith(ArtFolder), Is.False,
                    $"{s.state.name}: 스토어 패키지의 원본 클립을 직접 물었다 ({path})");
            }
        }

        /// <summary>
        /// <c>writeDefaults</c>를 켜면 클립이 안 건드리는 값을 Animator가 매 프레임 되돌린다.
        /// Dead 클립은 알파를 안 건드리는데 DeadState가 코드로 페이드아웃하므로,
        /// 켜 두면 시체가 영영 안 사라진다.
        /// </summary>
        [Test]
        public void EveryState_WritesNoDefaults()
        {
            foreach (ChildAnimatorState s in States())
                Assert.That(s.state.writeDefaultValues, Is.False, $"{s.state.name}: writeDefaults가 켜져 있다");
        }

        [Test]
        public void DefaultState_IsIdle()
        {
            Assert.That(Controller().layers[0].stateMachine.defaultState.name, Is.EqualTo("Idle"));
        }

        // ── 2.5D 표현 ───────────────────────────────────

        /// <summary>
        /// 파츠 16장에 <c>flipX</c>를 걸면 각자 제자리에서 뒤집혀 리그가 분해된다.
        /// 반전은 반드시 부모 노드의 scale.x 부호가 맡아야 한다.
        /// </summary>
        [Test]
        public void Facing_FlipsTheWholeRig_NotASingleRenderer()
        {
            GameObject root = Prefab();
            Transform sprite = root.transform.Find(SpriteChild);
            Assert.That(sprite, Is.Not.Null, $"{SpriteChild}가 없다");

            var so = new SerializedObject(root.GetComponent<BeltScrollView>());

            Assert.That(so.FindProperty("flipToFacing").boolValue, Is.True,
                "flipToFacing이 꺼져 있다 — 왼쪽으로 움직여도 오른쪽을 본다");
            Assert.That(so.FindProperty("facingRoot").objectReferenceValue, Is.SameAs(sprite),
                "facingRoot가 리그의 부모를 안 가리킨다");
            Assert.That(so.FindProperty("facingRenderer").objectReferenceValue, Is.Null,
                "facingRenderer가 남아 있다 — 파츠 한 장만 뒤집힌다");
        }

        /// <summary>
        /// <see cref="BeltScrollView"/>는 이 목록의 index를 그대로 정렬값에 더한다.
        /// 순서가 원래 sortingOrder 오름차순이 아니면 화면에서 팔이 얼굴 앞으로 나온다.
        /// 그림자는 언제나 맨 뒤다.
        /// </summary>
        [Test]
        public void SortedRenderers_KeepTheRigsOwnOrder_WithShadowFirst()
        {
            GameObject root = Prefab();
            var so = new SerializedObject(root.GetComponent<BeltScrollView>());
            SerializedProperty sorted = so.FindProperty("sortedRenderers");

            Transform shadow = root.transform.Find("Shadow");
            Transform sprite = root.transform.Find(SpriteChild);
            SpriteRenderer[] body = sprite.GetComponentsInChildren<SpriteRenderer>(true);

            Assert.That(body.Length, Is.GreaterThan(1), "선행 조건: 몸이 파츠 여러 장이어야 한다");
            Assert.That(sorted.arraySize, Is.EqualTo(body.Length + 1),
                "그림자 + 몸 파츠 전부가 정렬 목록에 있어야 한다");

            Assert.That(sorted.GetArrayElementAtIndex(0).objectReferenceValue,
                Is.SameAs(shadow.GetComponent<SpriteRenderer>()), "그림자가 맨 앞(=맨 뒤에 그려짐)이 아니다");

            int previous = int.MinValue;
            for (int i = 1; i < sorted.arraySize; i++)
            {
                var r = sorted.GetArrayElementAtIndex(i).objectReferenceValue as SpriteRenderer;
                Assert.That(r, Is.Not.Null, $"정렬 목록 {i}번이 비었다");
                Assert.That(r.sortingOrder, Is.GreaterThanOrEqualTo(previous),
                    $"{r.name}: 정렬 목록이 원래 sortingOrder 순서가 아니다");
                previous = r.sortingOrder;
            }
        }

        /// <summary>피벗이 발밑이라 올려 줄 필요가 없다. 0이 아니면 발이 바닥에서 뜬다.</summary>
        [Test]
        public void SpriteOffset_KeepsFeetOnTheGround()
        {
            var so = new SerializedObject(Prefab().GetComponent<BeltScrollView>());
            Assert.That(so.FindProperty("spriteOffsetY").floatValue, Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>
        /// 원본 마법사는 4.1유닛으로 다른 적(2.9유닛)보다 한참 크다.
        /// 배율을 안 걸면 방 하나를 혼자 채운다.
        /// </summary>
        [Test]
        public void Rig_IsScaledDownToRoomSize()
        {
            Transform sprite = Prefab().transform.Find(SpriteChild);
            Assert.That(sprite.childCount, Is.EqualTo(1), "리그가 하나만 꽂혀 있어야 한다");

            float scale = sprite.GetChild(0).localScale.x;
            Assert.That(scale, Is.GreaterThan(0f).And.LessThan(1f), $"리그 배율이 {scale}다");
        }

        // ── 판정 배선 ───────────────────────────────────

        /// <summary>
        /// 레이어가 Default로 남으면 충돌 매트릭스가 아군 · 적을 못 가른다 —
        /// 마법사의 화살이 마법사를 맞힌다.
        /// </summary>
        [Test]
        public void Layers_AreEnemyHurtboxAndEnemyHitbox()
        {
            GameObject root = Prefab();
            var enemy = root.GetComponent<Enemy>();

            Assert.That(LayerMask.LayerToName(root.layer), Is.EqualTo("EnemyHurtbox"));
            Assert.That(enemy.BasicAttack, Is.Not.Null, "평타 히트박스가 안 물려 있다");
            Assert.That(LayerMask.LayerToName(enemy.BasicAttack.gameObject.layer), Is.EqualTo("EnemyHitbox"));
        }

        /// <summary>
        /// 기준 프리팹(<c>Enemy_Ranged</c>)에서 물려받은 고블린 데이터가 남아 있으면
        /// 씬에 그냥 끌어다 놨을 때 마법사가 궁수 수치로 싸운다.
        /// </summary>
        [Test]
        public void Prefab_DefaultsToTheWizardsOwnData()
        {
            var so = new SerializedObject(Prefab().GetComponent<Enemy>());
            var data = so.FindProperty("data").objectReferenceValue as EnemyData;

            Assert.That(data, Is.Not.Null, "EnemyData가 비었다");
            Assert.That(AssetDatabase.GetAssetPath(data), Is.EqualTo(SoftDataPath));
        }

        // ── 수치 ────────────────────────────────────────

        [TestCase(SoftDataPath)]
        [TestCase(HardDataPath)]
        public void EveryVariant_ShootsAndThinksLikeARangedEnemy(string path)
        {
            EnemyData data = Data(path);

            Assert.That(data.basicProjectile, Is.Not.Null, "투사체가 없으면 근접으로 때린다");
            Assert.That(data.brain, Is.InstanceOf<RangedBrainAsset>(), "원거리 브레인이 아니다");
            Assert.That(data.preferredMinRange, Is.GreaterThan(0f), "카이팅 거리가 0이면 붙어서 쏜다");
            Assert.That(data.leashRange, Is.GreaterThan(data.attackRange),
                "추격 포기 거리가 사거리보다 짧으면 다가오기도 전에 포기한다");
        }

        /// <summary>
        /// 실제로 닿는 거리는 투사체 사거리의 80%(<see cref="Entity.BasicAttackReach"/>)다.
        /// 그보다 멀리서 쏘기로 마음먹으면 닿지도 않을 화살에 쿨만 태운다.
        /// </summary>
        [TestCase(SoftDataPath)]
        [TestCase(HardDataPath)]
        public void EveryVariant_OnlyShootsWithinReach(string path)
        {
            EnemyData data = Data(path);

            // 여유는 상한에 더한다. Within은 EqualConstraint 전용이라 비교 제약에는 안 붙는다.
            // 애셋에 저장된 값(8.8)과 float 곱(11 * 0.8f = 8.800001)이 마지막 자리에서 갈린다.
            float reach = data.projectileRange * 0.8f + 0.0001f;

            Assert.That(data.attackRange, Is.LessThanOrEqualTo(reach),
                $"{data.name}: 사거리({data.attackRange})가 투사체가 닿는 거리를 넘는다");
        }

        /// <summary>난이도가 이름뿐이면 두 스테이지가 같은 방이 된다.</summary>
        [Test]
        public void Hard_IsHarderThanSoft_InEveryDimensionThatMatters()
        {
            EnemyData soft = Data(SoftDataPath);
            EnemyData hard = Data(HardDataPath);

            Assert.That(hard.hp, Is.GreaterThan(soft.hp), "체력");
            Assert.That(hard.atk, Is.GreaterThan(soft.atk), "공격력");
            Assert.That(hard.attackRange, Is.GreaterThan(soft.attackRange), "사거리");
            Assert.That(hard.attackInterval, Is.LessThan(soft.attackInterval), "공격 간격(짧을수록 강하다)");
            Assert.That(hard.preferredMinRange, Is.GreaterThan(soft.preferredMinRange), "카이팅 거리");
        }

        /// <summary>
        /// 데이터만 들고 소환하는 경로(스포너)가 프리팹을 못 찾으면 조용히 아무것도 안 나온다.
        /// </summary>
        [TestCase(SoftDataPath)]
        [TestCase(HardDataPath)]
        public void EveryVariant_PointsBackAtThePrefab(string path)
        {
            Assert.That(AssetDatabase.GetAssetPath(Data(path).prefab), Is.EqualTo(PrefabPath));
        }

        /// <summary>몇 번을 돌려도 애셋이 늘지 않아야 한다.</summary>
        [Test]
        public void Rebuilding_AddsNoAssets()
        {
            int before = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemy" }).Length;

            WizardEnemyBuilder.Build();

            Assert.That(AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemy" }).Length,
                Is.EqualTo(before));
        }
    }
}
