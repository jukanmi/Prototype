using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Prototype.EditorTools;

namespace Prototype.Tests
{
    /// <summary>
    /// 교대 등장 무적. 교대는 내려간 몸의 <b>자리를 그대로 물려받고</b>, 사망 교대라면 그 자리는
    /// 방금 아군 하나를 죽인 히트박스 한복판이다.
    ///
    /// 여기서 흘려 내지 않으면 새로 선 몸이 같은 히트박스에 그대로 또 잡힌다 —
    /// <c>Attack.alreadyHit</c>이 <see cref="Combat"/> 인스턴스 기준이라 새 몸은 언제나
    /// '처음 보는 대상'이기 때문이다. 그게 보스의 다단히트 한 번에 파티가 통째로 죽고
    /// "한 명만 죽었는데 게임오버"로 보이던 경로다.
    ///
    /// 교대 자체(<see cref="TagSwapController"/>)는 씬이 필요해 여기서 못 돈다.
    /// 무적의 <b>판정</b>과, 프리팹에 박힌 <b>길이가 충분한지</b>를 나눠 본다.
    /// </summary>
    public class SummonInvulnTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                Object.DestroyImmediate(spawned[i]);

            spawned.Clear();
        }

        // ── 판정 ────────────────────────────────────────

        [Test]
        public void WithoutSummon_HitLandsNormally()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            Assert.That(victim.IsSummonInvulnerable, Is.False, "선행 조건: 그냥 서 있는 몸이다");
            Assert.That(victim.Hit(in Poke, attacker), Is.True);
        }

        [Test]
        public void JustSummoned_ShrugsTheHit()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.GrantSummonInvuln();

            Assert.That(victim.IsSummonInvulnerable, Is.True);
            Assert.That(victim.Hit(in Poke, attacker), Is.False, "흘린 타격은 적중으로 치지 않는다");
            Assert.That(victim.Health.CurValue, Is.EqualTo(victim.Health.MaxValue), "데미지가 없고");
            Assert.That(victim.CombatState, Is.EqualTo(CombatState.Neutral), "경직도 없다");
        }

        /// <summary>
        /// 이 시스템의 요점. 교대로 선 몸은 <b>때린 쪽을 보고 있지 않다</b> —
        /// 방금 쓰러진 자리에 그대로 세워졌을 뿐이라, 방향을 보면 등 뒤로 들어오는
        /// 다음 타격을 그대로 맞고 연쇄가 이어진다.
        /// </summary>
        [Test]
        public void SummonInvuln_IgnoresDirection()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat back = NewCombat("Back", Behind);

            victim.GrantSummonInvuln();

            Assert.That(victim.Hit(in Poke, back), Is.False, "등 뒤도 흘린다");
        }

        [Test]
        public void SummonInvuln_ShrugsEveryAttacker_NotJustTheFirst()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat first = NewCombat("First", InFront);
            Combat second = NewCombat("Second", Behind);

            victim.GrantSummonInvuln();

            Assert.That(victim.Hit(in Poke, first), Is.False);
            Assert.That(victim.Hit(in Poke, second), Is.False, "다단히트의 남은 타격이 전부 흘러야 한다");
            Assert.That(victim.Health.CurValue, Is.EqualTo(victim.Health.MaxValue));
        }

        /// <summary>패링과 갈리는 지점. 교대 무적은 안전장치지 보상이 아니다.</summary>
        [Test]
        public void SummonInvuln_DoesNotCounterAttack()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.GrantSummonInvuln();
            victim.Hit(in Poke, attacker);

            Assert.That(attacker.CombatState, Is.EqualTo(CombatState.Neutral),
                        "공격자가 경직에 걸리면 교대가 곧 패링이 된다");
            Assert.That(attacker.Health.CurValue, Is.EqualTo(attacker.Health.MaxValue));
        }

        [Test]
        public void SummonInvuln_Expires()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);
            Combat attacker = NewCombat("Attacker", InFront);

            victim.GrantSummonInvuln();
            victim.Tick(victim.SummonInvulnDuration + 0.1f);

            Assert.That(victim.IsSummonInvulnerable, Is.False);
            Assert.That(victim.Hit(in Poke, attacker), Is.True, "끝나면 다시 맞는다");
            Assert.That(victim.CombatState, Is.EqualTo(CombatState.LightHit));
        }

        [Test]
        public void SummonInvuln_CountsDown()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);

            victim.GrantSummonInvuln();
            float full = victim.SummonInvulnRemaining;

            victim.Tick(0.1f);

            Assert.That(victim.SummonInvulnRemaining, Is.EqualTo(full - 0.1f).Within(0.0001f));
        }

        /// <summary>
        /// 필드에서 뺀 몸이 무적을 물고 돌아오면 안 된다 — 패링 창을 닫는 것과 같은 이유다.
        /// 다시 설 때는 <c>Summon</c>이 새로 준다.
        /// </summary>
        [Test]
        public void Benching_ClosesSummonInvuln()
        {
            Combat victim = NewCombat("Victim", Vector3.zero);

            victim.GrantSummonInvuln();
            victim.ClearHitStun();   // 태그로 내려가는 몸의 뒷정리

            Assert.That(victim.IsSummonInvulnerable, Is.False);
        }

        // ── 길이가 충분한가 (프리팹 · 보스 표) ────────────

        /// <summary>
        /// <b>연쇄 사망을 막는 실제 조건.</b> 무적이 보스 패턴 한 번이 켜 두는 시간보다 짧으면
        /// 새로 선 몸이 같은 패턴의 남은 타격에 그대로 맞아 사슬이 이어진다.
        ///
        /// 패턴 하나가 새 몸을 위협하는 마지막 순간은 <b>마지막 타격이 나가고 그 히트박스가
        /// 닫힐 때</b>다 — 삼연참(active 0.6 / 3타 / 0.12s)이면 0.4 + 0.12 = 0.52초.
        /// </summary>
        [Test]
        public void PartyBodies_SummonInvuln_OutlastsEveryBossPattern()
        {
            float needed = LongestBossThreat(out string worst);

            Assert.That(needed, Is.GreaterThan(0f), "보스 패턴 표를 못 읽었다");

            foreach ((string path, Combat combat) in PartyCombats())
            {
                Assert.That(combat.SummonInvulnDuration, Is.GreaterThanOrEqualTo(needed - 0.0001f),
                            $"{path}: 교대 무적 {combat.SummonInvulnDuration:0.##}s 는 " +
                            $"'{worst}'({needed:0.##}s)를 못 넘긴다 — 교대로 선 몸이 그대로 또 죽는다");
            }
        }

        [Test]
        public void PartyBodies_AllCarrySummonInvuln()
        {
            var bodies = new List<string>();

            foreach ((string path, Combat combat) in PartyCombats())
            {
                bodies.Add(path);
                Assert.That(combat.SummonInvulnDuration, Is.GreaterThan(0f),
                            $"{path}: 교대 무적이 0이다 — 이 몸만 교대로 서자마자 죽는다");
            }

            Assert.That(bodies, Is.Not.Empty, "파티가 쓸 몸을 하나도 못 찾았다");
        }

        /// <summary>
        /// 보스 패턴 중 <b>새로 선 몸을 가장 오래 위협하는</b> 시간.
        /// 마지막 타격 시각 + 그 히트박스가 켜져 있는 시간이다.
        /// </summary>
        private static float LongestBossThreat(out string label)
        {
            label = null;
            float worst = 0f;

            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            Assert.That(boss, Is.Not.Null, $"{BossPrefabPath} 가 없다");

            var action = boss.GetComponentInChildren<BossPatternAction>(true);
            Assert.That(action, Is.Not.Null, "보스 프리팹에 BossPatternAction 이 없다");

            foreach (BossPattern p in action.Patterns)
            {
                int total = Mathf.Max(1, p.hitCount);
                float last = BossPatternAction.HitTime(p.active, total - 1, total);
                float threat = last + Mathf.Max(0.01f, p.hitDuration);

                if (threat <= worst) continue;

                worst = threat;
                label = p.label;
            }

            return worst;
        }

        /// <summary>파티의 몸이 될 프리팹 전부. 실패 메시지에 어느 에셋인지가 남아야 한다.</summary>
        private static IEnumerable<(string path, Combat combat)> PartyCombats()
        {
            var seen = new HashSet<string>();

            foreach (string path in Paths())
            {
                if (string.IsNullOrEmpty(path) || !seen.Add(path)) continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                var combat = go.GetComponent<Combat>();
                if (combat != null) yield return (path, combat);
            }
        }

        private static IEnumerable<string> Paths()
        {
            yield return PrefabLocator.PlayerPath;
            yield return PrefabLocator.AllyPath;

            foreach (string path in Custom<PartyMemberData>(m => m.prefab)) yield return path;
            foreach (string path in Custom<PlayerData>(h => h.prefab)) yield return path;
        }

        private static IEnumerable<string> Custom<T>(System.Func<T, GameObject> pick)
            where T : ScriptableObject
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var data = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;

                GameObject go = pick(data);
                if (go != null) yield return AssetDatabase.GetAssetPath(go);
            }
        }

        // ── 헬퍼 ─────────────────────────────────────────

        private const string BossPrefabPath = "Assets/Prefabs/Enemy_Boss.prefab";

        /// <summary>Physics.Facing의 기본값이 Vector3.right이므로 +X가 정면이다.</summary>
        private static readonly Vector3 InFront = new Vector3(3f, 0f, 0f);
        private static readonly Vector3 Behind = new Vector3(-3f, 0f, 0f);

        /// <summary>죽지 않을 만큼 약한 한 대. 경직만 일으킨다.</summary>
        private static readonly HitData Poke = new HitData
        {
            damageData = new DamageData(5f),
            targetState = CombatState.Neutral,
            nextState = CombatState.LightHit,
            mode = KnockbackMode.AwayFromCaster,
            hitStunDuration = 0.3f,
        };

        private Combat NewCombat(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            spawned.Add(go);

            go.AddComponent<Ally>();
            return go.GetComponent<Combat>();
        }
    }
}
