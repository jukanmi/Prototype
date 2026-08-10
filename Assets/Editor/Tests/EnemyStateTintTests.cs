using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 상태는 실제 타격 경로(<c>Combat.Attack</c>)로 만든다.
    /// <c>Combat.SetCombatState</c>가 private이라 밖에서 상태를 직접 세울 수 없다 —
    /// RecentHitEnemyHUDTests와 같은 방식이다.
    /// </summary>
    public class EnemyStateTintTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        [Test]
        public void Enemy_GetsTintComponentAutomatically()
        {
            Enemy enemy = NewRootRendererEnemy(Color.red);

            Assert.That(enemy.GetComponent<EnemyStateTint>(), Is.Not.Null);
        }

        [Test]
        public void BeforeAnyHit_BodyKeepsItsOwnColor()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);
            Enemy enemy = NewRootRendererEnemy(identity);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();

            Assert.That(tint.BaseColor, Is.EqualTo(identity));
            Assert.That(tint.Body.color, Is.EqualTo(identity));
        }

        [Test]
        public void OnHit_BodyTurnsTheStateColor()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);
            Enemy enemy = NewRootRendererEnemy(identity);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();
            Combat attacker = NewAlly();

            attacker.Attack(enemy.Combat, in Poke);

            Assert.That(enemy.Combat.CombatState, Is.EqualTo(CombatState.LightHit),
                        "선행 조건: 경직 상태로 들어가야 한다");
            Assert.That(tint.Body.color,
                        Is.EqualTo(CombatStateVisuals.Tint(identity, CombatState.LightHit)));
        }

        [Test]
        public void WhenStunEnds_BodyReturnsToItsOwnColor()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);
            Enemy enemy = NewRootRendererEnemy(identity);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();
            Combat attacker = NewAlly();

            attacker.Attack(enemy.Combat, in Poke);
            Assert.That(tint.Body.color, Is.Not.EqualTo(identity), "선행 조건: 일단 물들어야 한다");

            // Poke의 경직은 0.3초다. 넉넉히 넘겨 경직을 푼다.
            enemy.Combat.Tick(1f);

            Assert.That(enemy.Combat.CombatState, Is.EqualTo(CombatState.Neutral));
            Assert.That(tint.Body.color, Is.EqualTo(identity));
        }

        [Test]
        public void OnDeath_BodyReturnsToItsOwnColor()
        {
            var identity = new Color(0.9f, 0.3f, 0.28f, 1f);
            Enemy enemy = NewRootRendererEnemy(identity);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();
            Combat attacker = NewAlly();

            attacker.Attack(enemy.Combat, in Poke);
            enemy.Combat.TakeDamage(new DamageData(9999f));

            Assert.That(enemy.Combat.IsDead, Is.True);
            Assert.That(tint.Body.color, Is.EqualTo(identity), "Dead는 표시 대상이 아니다");

            // 이게 성립하는 이유는 Combat.Die()의 순서다:
            //   SetCombatState(Dead) → 여기서 색이 원복된다
            //   owner.ForceDead()    → DeadState.Enter가 그제야 색을 캐시한다
            // 순서가 뒤집히면 페이드가 물든 색에서 시작해 죽는 연출이 이상해진다.
        }

        [Test]
        public void TintKeepsAlpha_SoDespawnFadeSurvives()
        {
            var faded = new Color(0.9f, 0.3f, 0.28f, 0.5f);
            Enemy enemy = NewRootRendererEnemy(faded);
            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();
            Combat attacker = NewAlly();

            attacker.Attack(enemy.Combat, in Poke);

            Assert.That(tint.Body.color.a, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void WithBeltScrollView_PicksTheSpriteChildNotTheShadow()
        {
            Enemy enemy = NewBeltScrollEnemy(
                body: new Color(0.9f, 0.3f, 0.28f, 1f),
                shadow: new Color(0f, 0f, 0f, 0.35f));

            EnemyStateTint tint = enemy.GetComponent<EnemyStateTint>();

            Assert.That(tint.Body, Is.Not.Null);
            Assert.That(tint.Body.gameObject.name, Is.EqualTo("Sprite"));
            Assert.That(tint.BaseColor, Is.EqualTo(new Color(0.9f, 0.3f, 0.28f, 1f)));
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>죽지 않을 만큼 약한 한 대. 경직만 일으킨다.</summary>
        private static readonly HitData Poke = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
            hitStunDuration = 0.3f,
        };

        private Combat NewAlly()
        {
            GameObject go = NewObject("Ally");
            go.AddComponent<Ally>();
            return go.GetComponent<Combat>();
        }

        /// <summary>
        /// 변종 프리팹(Enemy_Ranged · Enemy_Charger) 형태 — 루트에 몸이 붙어 있다.
        /// 렌더러를 <b>Enemy보다 먼저</b> 붙여야 한다. Enemy.Awake가 즉시 돌면서
        /// EnemyStateTint를 붙이고, 그 Awake가 그 자리에서 색을 읽기 때문이다.
        /// </summary>
        private Enemy NewRootRendererEnemy(Color identity)
        {
            GameObject go = NewObject("Enemy");
            go.AddComponent<SpriteRenderer>().color = identity;
            return go.AddComponent<Enemy>();
        }

        /// <summary>기준 프리팹(enemy.prefab) 형태 — Sprite · Shadow 자식이 있다.</summary>
        private Enemy NewBeltScrollEnemy(Color body, Color shadow)
        {
            GameObject go = NewObject("Enemy");

            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(go.transform, false);
            shadowGo.AddComponent<SpriteRenderer>().color = shadow;

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(go.transform, false);
            spriteGo.AddComponent<SpriteRenderer>().color = body;

            var view = go.AddComponent<BeltScrollView>();

            // private [SerializeField]는 SerializedObject로만 안전하게 건드린다.
            var so = new SerializedObject(view);
            so.FindProperty("sprite").objectReferenceValue = spriteGo.transform;
            so.FindProperty("shadow").objectReferenceValue = shadowGo.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go.AddComponent<Enemy>();
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }
    }
}
