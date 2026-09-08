using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 계획서는 Assets/Tests/EditMode/ 를 지정했지만 여기(Assembly-CSharp-Editor)에 둔다.
    /// asmdef로 만든 어셈블리는 Assembly-CSharp(게임 코드)를 참조할 수 없다는 유니티 제약 때문에,
    /// 게임 코드 전체를 asmdef로 쪼개기 전까지는 이 위치가 유일하게 동작하는 자리다.
    /// </summary>
    public class RecentHitEnemyHUDTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private RecentHitEnemyHUD hud;

        private const string PrefabPath = "Assets/Prefabs/CombatManager.prefab";
        private const string CanvasName = "RecentHitEnemyCanvas";

        /// <summary>
        /// <b>프리팹에서 떠 온다.</b> 화면 조립이 코드에서 프리팹으로 옮겨 간 뒤로는
        /// 빈 GameObject에 컴포넌트만 붙이면 판도 체력 바도 없어 <c>IsVisible</c>이 늘 false다 —
        /// 이 클래스의 검사 대부분이 조용히 무의미해진다.
        ///
        /// <c>CombatManager</c> 전체가 아니라 <c>RecentHitEnemyCanvas</c> 자식만 떠 온다.
        /// 통째로 띄우면 BulletTimeController 같은 이웃의 Awake까지 에디트모드에서 돈다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"{PrefabPath} 를 못 찾았다.");

            Transform canvas = prefab.transform.Find(CanvasName);
            Assert.That(canvas, Is.Not.Null,
                $"{PrefabPath} 에 {CanvasName} 자식이 없다 — 프리팹 계층이 바뀌었다.");

            // HUD를 먼저 만들어 OnEnable이 static 이벤트를 구독하게 한다.
            GameObject go = Object.Instantiate(canvas.gameObject);
            spawned.Add(go);

            hud = go.GetComponent<RecentHitEnemyHUD>();
            Assert.That(hud, Is.Not.Null, $"{CanvasName} 에 RecentHitEnemyHUD 가 없다.");
        }

        [TearDown]
        public void TearDown()
        {
            // HUD를 먼저 지운다 — OnDisable이 static 구독을 떼어내야 다음 테스트가 오염되지 않는다.
            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
            hud = null;
        }

        [Test]
        public void AlliedAttackOnEnemy_ShowsTheEnemy()
        {
            Combat attacker = NewAlly();
            Combat target = NewEnemy();

            attacker.Attack(target, in Poke);

            Assert.That(hud.IsVisible, Is.True);
            Assert.That(hud.CurrentTarget, Is.SameAs(target));
        }

        [Test]
        public void EnemyAttackOnAlly_StaysHidden()
        {
            Combat attacker = NewEnemy();
            Combat target = NewAlly();

            attacker.Attack(target, in Poke);

            Assert.That(hud.IsVisible, Is.False);
            Assert.That(hud.CurrentTarget, Is.Null);
        }

        [Test]
        public void NewerAlliedHit_ReplacesTarget()
        {
            Combat attacker = NewAlly();
            Combat first = NewEnemy();
            Combat second = NewEnemy();

            attacker.Attack(first, in Poke);
            attacker.Attack(second, in Poke);

            Assert.That(hud.CurrentTarget, Is.SameAs(second));
            Assert.That(hud.IsVisible, Is.True);
        }

        [Test]
        public void HitRejectedByInvincibility_DoesNotRetrack()
        {
            Combat attacker = NewAlly();
            Combat downed = NewEnemy();
            Combat other = NewEnemy();

            attacker.Attack(downed, in Knockdown);   // 다운 = 무적 상태로 진입
            Assert.That(downed.CombatState, Is.EqualTo(CombatState.Down));

            attacker.Attack(other, in Poke);         // 표시를 다른 적으로 옮긴다
            Assert.That(hud.CurrentTarget, Is.SameAs(other));

            // 무적인 적을 OTG 아닌 공격으로 때린다 — 적중이 성립하지 않으므로 표시가 옮겨가면 안 된다.
            attacker.Attack(downed, in Poke);

            Assert.That(hud.CurrentTarget, Is.SameAs(other));
        }

        [Test]
        public void HealthBar_ShrinksWithTargetHealth()
        {
            Combat attacker = NewAlly();
            Combat target = NewEnemy();
            float half = target.Health.MaxValue * 0.5f;

            var chunk = new HitData
            {
                damageData = new DamageData(half),
                targetState = CombatState.Neutral,
                nextState = CombatState.LightHit,
                mode = KnockbackMode.Fixed,
                fixedDir = Vector3.forward,
            };
            attacker.Attack(target, in chunk);

            Assert.That(target.Health.Ratio, Is.EqualTo(0.5f).Within(0.01f), "선행 조건: 체력이 절반이어야 한다");
            Assert.That(hud.HealthFillRatio, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void AfterVisibleDuration_Hides()
        {
            Combat attacker = NewAlly();
            Combat target = NewEnemy();
            attacker.Attack(target, in Poke);

            hud.TickExpiry(Time.unscaledTime);
            Assert.That(hud.IsVisible, Is.True, "만료 전에는 계속 보여야 한다");

            hud.TickExpiry(Time.unscaledTime + 100f);

            Assert.That(hud.IsVisible, Is.False);
            Assert.That(hud.CurrentTarget, Is.Null);
        }

        [Test]
        public void TargetDeath_Hides()
        {
            Combat attacker = NewAlly();
            Combat target = NewEnemy();
            attacker.Attack(target, in Poke);
            Assert.That(hud.IsVisible, Is.True);

            target.TakeDamage(new DamageData(9999f));

            Assert.That(target.IsDead, Is.True);
            Assert.That(hud.IsVisible, Is.False);
            Assert.That(hud.CurrentTarget, Is.Null);
        }

        // ── 헬퍼 ─────────────────────────────────────────

        /// <summary>죽지 않을 만큼 약한 한 대. 상태 전이만 일으킨다.</summary>
        private static readonly HitData Poke = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
        };

        /// <summary>대상을 Down(= 무적)으로 보내는 한 대. canOtg가 false인 공격을 흘리게 된다.</summary>
        private static readonly HitData Knockdown = new HitData
        {
            damageData = new DamageData(1f),
            targetState = CombatState.Neutral,
            nextState = CombatState.Down,
            mode = KnockbackMode.Fixed,
            fixedDir = Vector3.forward,
        };

        private Combat NewAlly() => NewCombatant<Ally>("Ally");

        private Combat NewEnemy() => NewCombatant<Enemy>("Enemy");

        /// <summary>
        /// Entity.Combat은 Entity.Awake가 채우므로 여기서는 GetComponent로 직접 집는다.
        /// Combat은 [RequireComponent]로 Entity보다 먼저 붙는다.
        /// </summary>
        private Combat NewCombatant<T>(string name) where T : Entity
        {
            GameObject go = NewObject(name);
            go.AddComponent<T>();
            return go.GetComponent<Combat>();
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }
    }
}
