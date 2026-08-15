using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 공격 예고 표시. 적이 때리기 <b>전에</b> 몸을 하얗게 번쩍여 대시 패링의 신호를 준다.
    ///
    /// 예고는 CombatState가 아니라 행동이라(평타 선딜 · 특수 행동 Telegraph 단계)
    /// <see cref="Entity.IsTelegraphing"/>이라는 별도 신호를 쓴다.
    /// </summary>
    public class TelegraphTintTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        // ── 색 규칙 (순수) ───────────────────────────────

        [Test]
        public void Telegraph_PaintsFullWhite()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);

            Color tinted = CombatStateVisuals.Tint(identity, CombatState.Neutral, telegraphing: true);

            Assert.That(tinted.r, Is.EqualTo(1f).Within(0.001f));
            Assert.That(tinted.g, Is.EqualTo(1f).Within(0.001f));
            Assert.That(tinted.b, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Telegraph_IsWhiterThanHitStun()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);

            Color stun = CombatStateVisuals.Tint(identity, CombatState.LightHit, telegraphing: false);
            Color telegraph = CombatStateVisuals.Tint(identity, CombatState.Neutral, telegraphing: true);

            Assert.That(telegraph.g, Is.GreaterThan(stun.g),
                        "둘 다 흰색 계열이라 세기로 갈린다 — 예고가 더 하얗다");
        }

        [Test]
        public void CombatState_BeatsTelegraph()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);

            Color both = CombatStateVisuals.Tint(identity, CombatState.Knockback, telegraphing: true);

            Assert.That(both, Is.EqualTo(CombatStateVisuals.Tint(identity, CombatState.Knockback)),
                        "예고 중에 맞으면 특수 행동이 취소된다 — 화면도 피격을 보여야 한다");
        }

        [Test]
        public void Dead_IgnoresTelegraph()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);

            Assert.That(CombatStateVisuals.Tint(identity, CombatState.Dead, telegraphing: true),
                        Is.EqualTo(identity), "시체는 예고하지 않는다");
        }

        [Test]
        public void Telegraph_KeepsAlpha_SoDespawnFadeSurvives()
        {
            var faded = new Color(0.9f, 0.3f, 0.28f, 0.5f);

            Color tinted = CombatStateVisuals.Tint(faded, CombatState.Neutral, telegraphing: true);

            Assert.That(tinted.a, Is.EqualTo(0.5f).Within(0.0001f));
        }

        // ── 특수 행동 예고 ───────────────────────────────

        [Test]
        public void TelegraphPhase_ShowsFromTheStart()
        {
            var sequence = new EnemySpecialSequence(telegraph: 0.6f, active: 0.2f, recovery: 0.2f);

            sequence.Begin();

            Assert.That(sequence.ShouldShowTelegraph, Is.True, "특수 행동은 예고 단계가 곧 예고다");
        }

        [Test]
        public void ActivePhase_StopsShowingTelegraph()
        {
            var sequence = new EnemySpecialSequence(telegraph: 0.2f, active: 0.5f, recovery: 0.2f);

            sequence.Begin();
            sequence.Tick(0.25f, Vector3.right);

            Assert.That(sequence.Phase, Is.EqualTo(EnemySpecialPhase.Active), "선행 조건: 발동에 들어갔다");
            Assert.That(sequence.ShouldShowTelegraph, Is.False, "피할 수 없는 구간에는 신호를 주지 않는다");
        }

        // ── 신호 → 몸 색 ─────────────────────────────────

        [Test]
        public void SettingTelegraph_TintsTheBody()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);
            Enemy enemy = NewEnemy(identity);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();

            enemy.SetTelegraph(true);

            Assert.That(tint.Body.color, Is.EqualTo(Color.white).Using(ColorComparer),
                        "예고가 켜지면 몸이 하얗게 번쩍인다");
        }

        [Test]
        public void ClearingTelegraph_RestoresIdentityColor()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);
            Enemy enemy = NewEnemy(identity);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();

            enemy.SetTelegraph(true);
            enemy.SetTelegraph(false);

            Assert.That(tint.Body.color, Is.EqualTo(identity).Using(ColorComparer));
        }

        [Test]
        public void TelegraphChange_FiresOnlyOnActualChange()
        {
            Enemy enemy = NewEnemy(Color.red);

            int calls = 0;
            enemy.OnTelegraphChanged += _ => calls++;

            enemy.SetTelegraph(true);
            enemy.SetTelegraph(true);
            enemy.SetTelegraph(false);

            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void Death_ClearsTelegraph()
        {
            Enemy enemy = NewEnemy(Color.red);

            enemy.SetTelegraph(true);
            enemy.Combat.TakeDamage(new DamageData(9999f));

            Assert.That(enemy.Combat.IsDead, Is.True, "선행 조건: 죽었다");
            Assert.That(enemy.IsTelegraphing, Is.False, "예고를 켠 채로 굳으면 시체가 계속 번쩍인다");
        }

        [Test]
        public void GettingHitMidTelegraph_ShowsTheHit()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);
            Enemy enemy = NewEnemy(identity);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();
            Combat attacker = NewAlly();

            enemy.SetTelegraph(true);
            attacker.Attack(enemy.Combat, in Poke);

            Assert.That(enemy.Combat.CombatState, Is.EqualTo(CombatState.LightHit), "선행 조건: 경직에 들어갔다");
            Assert.That(tint.Body.color,
                        Is.EqualTo(CombatStateVisuals.Tint(identity, CombatState.LightHit)).Using(ColorComparer));
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>부동소수 비교. Color의 == 는 근사비교지만 NUnit의 EqualTo는 정확비교다.</summary>
        private static readonly IEqualityComparer<Color> ColorComparer = new ApproxColor();

        private class ApproxColor : IEqualityComparer<Color>
        {
            public bool Equals(Color a, Color b)
                => Mathf.Abs(a.r - b.r) < 0.001f && Mathf.Abs(a.g - b.g) < 0.001f &&
                   Mathf.Abs(a.b - b.b) < 0.001f && Mathf.Abs(a.a - b.a) < 0.001f;

            public int GetHashCode(Color c) => c.GetHashCode();
        }

        private static readonly HitData Poke = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            hitStunDuration = 0.3f,
        };

        /// <summary>
        /// 렌더러를 <b>Enemy보다 먼저</b> 붙여야 한다. Enemy.Awake가 즉시 돌면서
        /// EnemyStateTint를 붙이고, 그 Awake가 그 자리에서 색을 읽기 때문이다
        /// (EnemyStateTintTests와 같은 방식).
        /// </summary>
        private Enemy NewEnemy(Color identity)
        {
            GameObject go = NewObject("Enemy");
            go.AddComponent<SpriteRenderer>().color = identity;
            return go.AddComponent<Enemy>();
        }

        private Combat NewAlly() => NewObject("Ally").AddComponent<Ally>().Combat;

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }
    }
}
