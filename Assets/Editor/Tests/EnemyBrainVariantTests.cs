using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 원거리·돌진 브레인의 <b>판단</b>만 검증한다.
    /// 실행(투사체 발사·돌진 이동)은 여기 없다 — 브레인은 무상태 판단기다.
    /// </summary>
    public class EnemyBrainVariantTests
    {
        private GameObject targetObject;
        private RangedBrainAsset ranged;
        private ChargerBrainAsset charger;

        private static readonly EnemyBrainParams RangedParams = new EnemyBrainParams
        {
            attackRange = 7f,
            preferredMinRange = 4f,
            leashRange = 12f,
        };

        private static readonly EnemyBrainParams ChargerParams = new EnemyBrainParams
        {
            attackRange = 2f,
            specialRange = 7f,
            leashRange = 12f,
        };

        [SetUp]
        public void SetUp()
        {
            targetObject = new GameObject("Target");
            targetObject.AddComponent<Ally>();

            ranged = ScriptableObject.CreateInstance<RangedBrainAsset>();
            charger = ScriptableObject.CreateInstance<ChargerBrainAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(ranged);
            Object.DestroyImmediate(charger);
        }

        /// <summary>타겟은 +X 방향으로 distance만큼 떨어져 있다.</summary>
        private EnemyBrainContext Ctx(float distance, in EnemyBrainParams p,
                                      bool attackReady = true, bool specialReady = true, bool hasTarget = true)
        {
            return new EnemyBrainContext
            {
                target = hasTarget ? targetObject.GetComponent<Ally>() : null,
                toTarget = Vector3.right * distance,
                distance = distance,
                attackReady = attackReady,
                specialReady = specialReady,
                p = p,
                dt = 0.02f,
            };
        }

        // ── 원거리 ──────────────────────────────────────

        [Test]
        public void Ranged_TooClose_BacksAway()
        {
            EnemyIntent intent = ranged.Decide(Ctx(3f, RangedParams));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.Move));
            Assert.That(intent.moveDirection.x, Is.LessThan(0f), "선호 거리보다 가까우면 반대로 물러나야 한다");
        }

        [Test]
        public void Ranged_AtPreferredEdge_Attacks()
        {
            EnemyIntent intent = ranged.Decide(Ctx(4f, RangedParams));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.Attack));
            Assert.That(intent.command, Is.EqualTo(Command.Attack));
        }

        [Test]
        public void Ranged_AtMaxRange_Attacks()
        {
            EnemyIntent intent = ranged.Decide(Ctx(7f, RangedParams));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.Attack));
            Assert.That(intent.moveDirection.x, Is.GreaterThan(0f), "쏘는 방향으로 얼굴을 돌려야 한다");
        }

        [Test]
        public void Ranged_OutOfRange_Approaches()
        {
            EnemyIntent intent = ranged.Decide(Ctx(8f, RangedParams));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.Move));
            Assert.That(intent.moveDirection.x, Is.GreaterThan(0f));
        }

        [Test]
        public void Ranged_OnCooldown_HoldsPosition()
        {
            EnemyIntent intent = ranged.Decide(Ctx(5f, RangedParams, attackReady: false));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.None));
        }

        [Test]
        public void Ranged_BeyondLeash_GivesUp()
        {
            EnemyIntent intent = ranged.Decide(Ctx(13f, RangedParams));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.None));
        }

        [Test]
        public void Ranged_NoTarget_DoesNothing()
        {
            EnemyIntent intent = ranged.Decide(Ctx(5f, RangedParams, hasTarget: false));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.None));
        }

        // ── 돌진 ────────────────────────────────────────

        [Test]
        public void Charger_InMeleeRange_UsesBasicAttack()
        {
            EnemyIntent intent = charger.Decide(Ctx(2f, ChargerParams));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.Attack));
            Assert.That(intent.command, Is.EqualTo(Command.Attack));
        }

        [Test]
        public void Charger_InChargeBand_Charges()
        {
            EnemyIntent intent = charger.Decide(Ctx(3f, ChargerParams));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.Charge));
            Assert.That(intent.moveDirection.x, Is.GreaterThan(0f));
        }

        [Test]
        public void Charger_AtChargeEdge_Charges()
        {
            EnemyIntent intent = charger.Decide(Ctx(7f, ChargerParams));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.Charge));
        }

        [Test]
        public void Charger_SpecialOnCooldown_Approaches()
        {
            EnemyIntent intent = charger.Decide(Ctx(3f, ChargerParams, specialReady: false));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.Move));
            Assert.That(intent.moveDirection.x, Is.GreaterThan(0f));
        }

        [Test]
        public void Charger_BeyondChargeBand_Approaches()
        {
            EnemyIntent intent = charger.Decide(Ctx(8f, ChargerParams));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.Move));
        }

        [Test]
        public void Charger_BeyondLeash_GivesUp()
        {
            EnemyIntent intent = charger.Decide(Ctx(13f, ChargerParams));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.None));
        }

        /// <summary>돌진 명령은 Command로 새지 않아야 한다 — 상태머신이 평타로 오인한다.</summary>
        [Test]
        public void Charger_ChargeIntent_DoesNotLeakAttackCommand()
        {
            EnemyIntent intent = charger.Decide(Ctx(5f, ChargerParams));

            Assert.That(intent.command, Is.EqualTo(Command.None));
        }
    }
}
