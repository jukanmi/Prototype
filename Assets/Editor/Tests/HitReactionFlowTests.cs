using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 전투 상태(CombatState)가 바뀌면 몸(IState)이 따라가는가.
    ///
    /// 몸을 옮기는 자리는 <see cref="Entity"/>의 OnCombatStateChanged 구독 한 곳이다.
    /// 예전에는 Combat 다섯 군데와 경직 상태의 폴링이 제각각 옮겼고, 한 곳을 빼먹으면
    /// "경직이 안 풀린다"로만 보였다. 여기서 경직 → Idle, 다운 → 기상 → Idle 두 길을 지킨다.
    /// </summary>
    public class HitReactionFlowTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        [Test]
        public void LightHit_ReturnsToIdle_WhenStunEnds()
        {
            Combat c = NewBody("Victim");

            c.Hit(in Jab, null);
            Assert.That(c.Owner.StateMachine.CurState, Is.SameAs(c.Owner.HitState), "선행 조건: 약경직에 들어가야 한다");

            c.Tick(0.4f);   // 경직 0.3초가 지났다

            Assert.That(c.CombatState, Is.EqualTo(CombatState.Neutral));
            Assert.That(c.Owner.StateMachine.CurState, Is.SameAs(c.Owner.IdleState),
                        "전투 상태는 풀렸는데 몸이 경직에 남았다");
        }

        [Test]
        public void GroundedAerialHit_GoesDown_ThenGetsUp_ThenIdle()
        {
            Combat c = NewBody("Victim");

            // 띄우기 높이가 0이라 몸은 바닥에 있다. 공중 피격은 착지로만 풀리므로
            // 경직이 끝나는 순간 착지와 같게 처리돼 다운으로 간다.
            c.Hit(in GroundedLauncher, null);
            Assert.That(c.Owner.StateMachine.CurState, Is.SameAs(c.Owner.AerialHitState), "선행 조건");

            c.Tick(0.4f);
            Assert.That(c.CombatState, Is.EqualTo(CombatState.Down));
            Assert.That(c.Owner.StateMachine.CurState, Is.SameAs(c.Owner.DownState));

            c.Tick(1.3f);   // 다운 1.2초
            Assert.That(c.CombatState, Is.EqualTo(CombatState.Getup));
            Assert.That(c.Owner.StateMachine.CurState, Is.SameAs(c.Owner.GetupState));

            c.Tick(0.5f);   // 기상 0.4초
            Assert.That(c.CombatState, Is.EqualTo(CombatState.Neutral));
            Assert.That(c.Owner.StateMachine.CurState, Is.SameAs(c.Owner.IdleState),
                        "기상이 끝났는데 몸이 기상 상태에 남았다");
        }

        private Combat NewBody(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.AddComponent<Ally>();
            return go.GetComponent<Combat>();
        }

        private static readonly HitData Jab = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            hitStunDuration = 0.3f,
        };

        private static readonly HitData GroundedLauncher = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.AerialHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            airborneHeight = 0f,
            hitStunDuration = 0.3f,
        };
    }
}
