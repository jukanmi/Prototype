using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 보스 브레인의 <b>판단</b>만 검증한다. 실행(히트박스·전진)은 여기 없다.
    ///
    /// 규칙 표는 실제 애셋을 쓰지 않고 테스트 안에서 만든다 —
    /// 밸런스 수치를 만질 때마다 판단 로직 테스트가 빨개지면 안 된다.
    /// </summary>
    public class BossBrainTests
    {
        // 실제 보스와 같은 모양의 표. 위에서부터 검사하므로 순서가 곧 우선순위다.
        private const int Rage = 3;
        private const int Slam = 1;
        private const int Dash = 2;
        private const int Combo = 0;

        private const float RageCooldown = 99f;
        private const float SlamCooldown = 6f;
        private const float DashCooldown = 5f;
        private const float ComboCooldown = 3.5f;

        /// <summary>모든 패턴의 쿨이 끝난 상태.</summary>
        private const int AllReady = (1 << Rage) | (1 << Slam) | (1 << Dash) | (1 << Combo);

        private GameObject targetObject;
        private BossBrainAsset boss;

        private static readonly EnemyBrainParams Params = new EnemyBrainParams
        {
            attackRange = 2.6f,
            leashRange = 0f,     // 보스방은 도망칠 곳이 없다
        };

        [SetUp]
        public void SetUp()
        {
            targetObject = new GameObject("Target");
            targetObject.AddComponent<Ally>();

            boss = ScriptableObject.CreateInstance<BossBrainAsset>();
            boss.patterns = new[]
            {
                Rule("격노",       Rage,  0f,   6f,   RageCooldown,  0.5f),
                Rule("대지가르기", Slam,  0f,   4.5f, SlamCooldown,  0f),
                Rule("돌진베기",   Dash,  3f,   9f,   DashCooldown,  0f),
                Rule("삼연참",     Combo, 0f,   3.2f, ComboCooldown, 0f),
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(boss);
        }

        private static BossBrainAsset.PatternRule Rule(string label, int index, float min, float max,
                                                       float cooldown, float maxHealthRatio)
            => new BossBrainAsset.PatternRule
            {
                label = label,
                index = index,
                minRange = min,
                maxRange = max,
                cooldown = cooldown,
                maxHealthRatio = maxHealthRatio,
            };

        /// <summary>타겟은 +X 방향으로 distance만큼 떨어져 있다.</summary>
        private EnemyBrainContext Ctx(float distance, float healthRatio = 1f, int readyMask = AllReady,
                                      bool attackReady = true, bool hasTarget = true,
                                      float leashRange = 0f)
        {
            EnemyBrainParams p = Params;
            p.leashRange = leashRange;

            return new EnemyBrainContext
            {
                target = hasTarget ? targetObject.GetComponent<Ally>() : null,
                toTarget = Vector3.right * distance,
                distance = distance,
                attackReady = attackReady,
                specialReady = (readyMask & 1) != 0,
                specialReadyMask = readyMask,
                healthRatio = healthRatio,
                p = p,
                dt = 0.02f,
            };
        }

        // ── 기본 ────────────────────────────────────────

        [Test]
        public void NoTarget_DoesNothing()
        {
            Assert.That(boss.Decide(Ctx(3f, hasTarget: false)).kind, Is.EqualTo(EnemyActionKind.None));
        }

        [Test]
        public void BeyondLeash_GivesUp()
        {
            Assert.That(boss.Decide(Ctx(20f, leashRange: 12f)).kind, Is.EqualTo(EnemyActionKind.None));
        }

        [Test]
        public void EmptyTable_FallsBackToNormalMelee()
        {
            boss.patterns = null;

            Assert.That(boss.Decide(Ctx(2f)).kind, Is.EqualTo(EnemyActionKind.Attack));
            Assert.That(boss.Decide(Ctx(6f)).kind, Is.EqualTo(EnemyActionKind.Move));
        }

        // ── 패턴 선택 ───────────────────────────────────

        /// <summary>격노 규칙은 체력 조건이 붙어 있다. 만피에서는 건너뛰어야 한다.</summary>
        [Test]
        public void FullHealth_SkipsRage_PicksSlam()
        {
            EnemyIntent intent = boss.Decide(Ctx(3f, healthRatio: 1f));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.Special));
            Assert.That(intent.specialIndex, Is.EqualTo(Slam));
        }

        [Test]
        public void HalfHealth_PicksRageFirst()
        {
            EnemyIntent intent = boss.Decide(Ctx(3f, healthRatio: 0.4f));

            Assert.That(intent.specialIndex, Is.EqualTo(Rage), "체력이 반 이하면 격노가 가장 먼저다");
        }

        /// <summary>경계값. 0.5 "이하"라고 적어 뒀으니 딱 0.5도 들어가야 한다.</summary>
        [Test]
        public void ExactlyAtHealthThreshold_PicksRage()
        {
            Assert.That(boss.Decide(Ctx(3f, healthRatio: 0.5f)).specialIndex, Is.EqualTo(Rage));
        }

        [Test]
        public void RageOnCooldown_FallsThroughToSlam()
        {
            int mask = AllReady & ~(1 << Rage);

            Assert.That(boss.Decide(Ctx(3f, healthRatio: 0.4f, readyMask: mask)).specialIndex,
                        Is.EqualTo(Slam));
        }

        [Test]
        public void LongRange_PicksDash()
        {
            Assert.That(boss.Decide(Ctx(7f)).specialIndex, Is.EqualTo(Dash),
                        "슬램 사거리(4.5) 밖이면 돌진이 남는다");
        }

        /// <summary>돌진은 최소 사거리가 있다. 붙어 있으면 밀고 들어갈 거리가 없다.</summary>
        [Test]
        public void PointBlank_SkipsDash()
        {
            int onlyDash = 1 << Dash;
            EnemyIntent intent = boss.Decide(Ctx(1.5f, readyMask: onlyDash));

            Assert.That(intent.kind, Is.Not.EqualTo(EnemyActionKind.Special));
        }

        [Test]
        public void OnlyComboReady_InComboRange_PicksCombo()
        {
            Assert.That(boss.Decide(Ctx(2f, readyMask: 1 << Combo)).specialIndex, Is.EqualTo(Combo));
        }

        // ── 폴백 ────────────────────────────────────────

        [Test]
        public void AllSpecialsOnCooldown_InAttackRange_UsesBasicAttack()
        {
            EnemyIntent intent = boss.Decide(Ctx(2f, readyMask: 0));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.Attack));
            Assert.That(intent.command, Is.EqualTo(Command.Attack));
        }

        [Test]
        public void AllSpecialsOnCooldown_OutOfAttackRange_Approaches()
        {
            EnemyIntent intent = boss.Decide(Ctx(6f, readyMask: 0));

            Assert.That(intent.kind, Is.EqualTo(EnemyActionKind.Move));
            Assert.That(intent.moveDirection.x, Is.GreaterThan(0f));
        }

        [Test]
        public void AllOnCooldown_AndBasicOnCooldown_Waits()
        {
            Assert.That(boss.Decide(Ctx(2f, readyMask: 0, attackReady: false)).kind,
                        Is.EqualTo(EnemyActionKind.None));
        }

        // ── 의도가 실어 보내는 값 ────────────────────────

        /// <summary>쿨 길이는 브레인이 정한다. 안 실어 보내면 전부 기본 쿨로 떨어진다.</summary>
        [Test]
        public void Intent_CarriesPatternCooldown()
        {
            Assert.That(boss.Decide(Ctx(3f)).specialCooldown, Is.EqualTo(SlamCooldown).Within(0.001f));
            Assert.That(boss.Decide(Ctx(7f)).specialCooldown, Is.EqualTo(DashCooldown).Within(0.001f));
        }

        /// <summary>특수 명령이 Command로 새면 상태머신이 평타로 오인해 AttackState로 끌고 간다.</summary>
        [Test]
        public void SpecialIntent_DoesNotLeakAttackCommand()
        {
            Assert.That(boss.Decide(Ctx(3f)).command, Is.EqualTo(Command.None));
        }

        [Test]
        public void SpecialIntent_FacesTarget()
        {
            Assert.That(boss.Decide(Ctx(3f)).moveDirection.x, Is.GreaterThan(0f));
        }

        // ── 마스크 규약 ─────────────────────────────────

        /// <summary>
        /// 브레인은 패턴 번호로 쿨을 묻는다. 배열 위치로 물으면 표 순서를 바꾸는 순간
        /// 엉뚱한 패턴의 쿨을 보게 된다.
        /// </summary>
        [Test]
        public void ReadyMask_IsIndexedByPatternNumber_NotRuleOrder()
        {
            // 표의 첫 줄은 격노(3번)다. 3번 비트만 세우고 체력 조건도 맞춘다.
            int onlyRage = 1 << Rage;

            Assert.That(boss.Decide(Ctx(3f, healthRatio: 0.4f, readyMask: onlyRage)).specialIndex,
                        Is.EqualTo(Rage));
        }

        [Test]
        public void IsSpecialReady_OutOfRangeIndex_IsFalse()
        {
            EnemyBrainContext ctx = Ctx(3f, readyMask: ~0);

            Assert.That(ctx.IsSpecialReady(-1), Is.False);
            Assert.That(ctx.IsSpecialReady(EnemyBrainContext.MaxSpecials), Is.False);
        }
    }
}
