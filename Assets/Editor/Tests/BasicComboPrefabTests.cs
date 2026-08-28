using System.Collections.Generic;
using NUnit.Framework;
using Prototype.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 평타 연타 저작이 프리팹에 실제로 들어가 있는지, 그리고
    /// <b>적에게는 안 들어갔는지</b>. 후자가 회귀 방어의 핵심이다 —
    /// 콤보를 상태에 넣었으므로 데이터가 새면 적까지 연타를 친다.
    /// </summary>
    public class BasicComboPrefabTests
    {
        /// <summary>연타를 저작하지 않아야 하는 프리팹.</summary>
        private static readonly string[] SingleHitPrefabs =
        {
            "Assets/Prefabs/enemy.prefab",
            "Assets/Prefabs/Enemy_Melee.prefab",
            "Assets/Prefabs/Enemy_Ranged.prefab",
            "Assets/Prefabs/Enemy_Charger.prefab",
            "Assets/Prefabs/Enemy_Boss.prefab",
        };

        [Test]
        public void PlayerAndAlly_HaveThreeStages()
        {
            foreach (Entity e in ComboEntities())
                Assert.That(e.BasicComboStageCount, Is.EqualTo(BasicComboBuilder.StageCount),
                            $"{e.name}: 3연타 단계가 없다 — Prototype ▸ 평타 - 3연타 클립 굽기 + 프리팹 배선 을 실행할 것");
        }

        [Test]
        public void Damage_GrowsWithEachStage()
        {
            foreach (Entity e in ComboEntities())
            {
                float prev = 0f;
                for (int i = 0; i < e.BasicComboStageCount; i++)
                {
                    float dmg = e.BuildBasicHit(i).damageData.damage;
                    Assert.That(dmg, Is.GreaterThan(prev),
                                $"{e.name} {i + 1}타: 뒤로 갈수록 세져야 완주할 이유가 생긴다");
                    prev = dmg;
                }
            }
        }

        [Test]
        public void OnlyTheFinisher_Launches()
        {
            foreach (Entity e in ComboEntities())
            {
                int last = e.BasicComboStageCount - 1;

                for (int i = 0; i < last; i++)
                    Assert.That(e.BuildBasicHit(i).airborneHeight, Is.Zero,
                                $"{e.name} {i + 1}타가 띄우면 마무리의 값어치가 사라진다");

                HitData finisher = e.BuildBasicHit(last);
                Assert.That(finisher.airborneHeight, Is.GreaterThan(0f), $"{e.name}: 마무리가 안 띄운다");
                Assert.That(finisher.nextState, Is.EqualTo(CombatState.AerialHit),
                            $"{e.name}: airborneHeight만 넣으면 몸은 뜨는데 상태가 어긋나 착지 전이가 깨진다");
            }
        }

        [Test]
        public void Timings_AreOrdered()
        {
            foreach (Entity e in ComboEntities())
            {
                for (int i = 0; i < e.BasicComboStageCount; i++)
                {
                    BasicAttackTiming t = e.GetBasicStageTiming(i);

                    Assert.That(t.windup, Is.LessThan(t.activeEnd), $"{e.name} {i + 1}타: 히트박스가 열리지 않는다");
                    Assert.That(t.activeEnd, Is.LessThanOrEqualTo(t.total), $"{e.name} {i + 1}타: 판정이 후딜을 넘어간다");
                    Assert.That(t.cancelStart, Is.GreaterThanOrEqualTo(t.activeEnd),
                                $"{e.name} {i + 1}타: 판정이 살아 있는 동안 캔슬되면 그 타가 맞기도 전에 사라진다");
                }
            }
        }

        [Test]
        public void EachStage_HasItsOwnClip()
        {
            foreach (Entity e in ComboEntities())
            {
                var seen = new HashSet<AnimationClip>();

                for (int i = 0; i < e.BasicComboStageCount; i++)
                {
                    AnimationClip clip = e.GetBasicStageClip(i);
                    Assert.That(clip, Is.Not.Null, $"{e.name} {i + 1}타 모션이 비어 있다");
                    Assert.That(seen.Add(clip), Is.True,
                                $"{e.name} {i + 1}타가 앞 타와 같은 클립이다 — 세 타가 같은 그림으로 보인다");
                }
            }
        }

        [Test]
        public void EnemiesStaySingleHit()
        {
            foreach (string path in SingleHitPrefabs)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;

                var e = root.GetComponent<Entity>();
                if (e == null) continue;

                Assert.That(e.HasBasicCombo, Is.False,
                            $"{path}: 적에게 연타 단계가 들어갔다. AI는 선입력 버퍼를 못 채우므로 " +
                            "1타에서 멈추긴 하지만, 데이터가 새는 것 자체가 저작 실수다");
            }
        }

        private static IEnumerable<Entity> ComboEntities()
        {
            foreach (string path in BasicComboBuilder.TargetPrefabs)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(root, Is.Not.Null, $"{path}를 찾지 못했다");

                var e = root.GetComponent<Entity>();
                Assert.That(e, Is.Not.Null, $"{path}에 Entity가 없다");

                yield return e;
            }
        }
    }
}
