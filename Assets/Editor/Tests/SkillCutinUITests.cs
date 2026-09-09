using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 컷인 본체. 시간 정지와 복원이 핵심이다 —
    /// 여기서 새면 게임이 영구 정지하거나 불릿타임이 제멋대로 풀린다.
    /// </summary>
    public class SkillCutinUITests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        private SkillCutinUI cutin;

        [SetUp]
        public void SetUp()
        {
            TimeControl.Reset();
            cutin = NewObject("SkillCutinUI").AddComponent<SkillCutinUI>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++) Object.DestroyImmediate(spawned[i]);
            for (int i = 0; i < assets.Count; i++) Object.DestroyImmediate(assets[i]);
            spawned.Clear();
            assets.Clear();
            cutin = null;
            TimeControl.Reset();
        }

        [Test]
        public void Play_FreezesTimeImmediately()
        {
            cutin.Play(NewAlly("Warrior", Role.Warrior), NewSkill("베어내기", Role.Warrior));

            Assert.That(TimeControl.Scale, Is.EqualTo(0f));
            Assert.That(cutin.IsPlaying, Is.True);
        }

        [Test]
        public void DrainingTheRoutine_RestoresTime()
        {
            IEnumerator routine = cutin.Play(NewAlly("Warrior", Role.Warrior), NewSkill("베어내기", Role.Warrior));

            Drain(routine);

            Assert.That(TimeControl.Scale, Is.EqualTo(1f));
            Assert.That(cutin.IsPlaying, Is.False);
        }

        [Test]
        public void Cancel_MidPlay_RestoresTime()
        {
            IEnumerator routine = cutin.Play(NewAlly("Warrior", Role.Warrior), NewSkill("베어내기", Role.Warrior));
            routine.MoveNext();

            Assert.That(TimeControl.Scale, Is.EqualTo(0f), "선행 조건: 아직 멈춰 있어야 한다");

            cutin.Cancel();

            Assert.That(TimeControl.Scale, Is.EqualTo(1f));
            Assert.That(cutin.IsPlaying, Is.False);
        }

        [Test]
        public void Cancel_WhenIdle_DoesNotTouchTime()
        {
            TimeControl.Scale = 0f;   // 불릿타임 중이라고 가정

            cutin.Cancel();

            Assert.That(TimeControl.Scale, Is.EqualTo(0f), "재생 중이 아니면 남의 정지를 풀면 안 된다");
        }

        [Test]
        public void PlayingWhileAlreadyFrozen_RestoresToFrozen()
        {
            TimeControl.Scale = 0f;

            IEnumerator routine = cutin.Play(NewAlly("Warrior", Role.Warrior), NewSkill("베어내기", Role.Warrior));
            Drain(routine);

            Assert.That(TimeControl.Scale, Is.EqualTo(0f), "들어올 때 0이었으면 나갈 때도 0이다");
        }

        [Test]
        public void NullCaster_DoesNotThrowAndStillRestoresTime()
        {
            IEnumerator routine = cutin.Play(null, NewSkill("베어내기", Role.Warrior));

            Assert.DoesNotThrow(() => Drain(routine));
            Assert.That(TimeControl.Scale, Is.EqualTo(1f));
        }

        [Test]
        public void RoleColor_IsDistinctPerRole()
        {
            var seen = new HashSet<Color>
            {
                SkillCutinUI.RoleColor(Role.Tanker),
                SkillCutinUI.RoleColor(Role.Warrior),
                SkillCutinUI.RoleColor(Role.Archer),
                SkillCutinUI.RoleColor(Role.Wizard),
            };

            Assert.That(seen.Count, Is.EqualTo(4), "직업마다 색이 달라야 폴백이 구실을 한다");
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private static void Drain(IEnumerator routine)
        {
            int guard = 0;
            while (routine.MoveNext())
            {
                if (++guard > 100000)
                    Assert.Fail("컷인 코루틴이 끝나지 않는다 — 무한 루프");
            }
        }

        private SkillData NewSkill(string skillName, Role role)
        {
            var data = ScriptableObject.CreateInstance<SkillData>();
            data.skillName = skillName;
            data.role = role;
            assets.Add(data);
            return data;
        }

        private Ally NewAlly(string name, Role role)
        {
            Ally ally = NewObject(name).AddComponent<Ally>();

            // role은 [SerializeField] private이라 SerializedObject로 넣는다.
            var so = new UnityEditor.SerializedObject(ally);
            so.FindProperty("role").enumValueIndex = (int)role;
            so.ApplyModifiedPropertiesWithoutUndo();

            return ally;
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }
    }
}
