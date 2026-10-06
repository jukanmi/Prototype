using NUnit.Framework;

namespace Prototype.Tests
{
    /// <summary>
    /// 승패 판정 규칙. 씬 없이 도는 순수 함수라 여기서 전부 덮는다.
    ///
    /// 가장 중요한 항목은 <see cref="StageOutcomeRules.CanJudge"/>의 첫 프레임 보호다 —
    /// 이게 빠지면 스테이지가 뜨자마자 결과 화면이 나오는데, 원인이 "등록 순서"라
    /// 화면만 봐서는 절대 짐작할 수 없다.
    /// </summary>
    public class StageOutcomeRulesTests
    {
        // ── 판정 개시 ───────────────────────────────────

        /// <summary>Entity 는 Start 에서 등록한다. 그 전에는 양쪽 목록이 비어 있다.</summary>
        [Test]
        public void BeforeAnyoneRegisters_CannotJudge()
        {
            Assert.That(StageOutcomeRules.CanJudge(0, 0), Is.False);
        }

        [Test]
        public void OnlyOneSideRegistered_CannotJudge()
        {
            Assert.That(StageOutcomeRules.CanJudge(3, 0), Is.False, "아군이 아직 안 올라왔다");
            Assert.That(StageOutcomeRules.CanJudge(0, 5), Is.False, "적이 아직 안 올라왔다");
        }

        [Test]
        public void BothSidesRegistered_CanJudge()
        {
            Assert.That(StageOutcomeRules.CanJudge(1, 1), Is.True);
        }

        // ── 결말 ────────────────────────────────────────

        [Test]
        public void EnemiesAlive_AndAlliesAlive_IsUndecided()
        {
            Assert.That(StageOutcomeRules.Evaluate(2, false), Is.EqualTo(StageOutcome.Undecided));
        }

        [Test]
        public void NoEnemiesLeft_IsVictory()
        {
            Assert.That(StageOutcomeRules.Evaluate(0, false), Is.EqualTo(StageOutcome.Victory));
        }

        [Test]
        public void AllAlliesDead_IsDefeat()
        {
            Assert.That(StageOutcomeRules.Evaluate(2, true), Is.EqualTo(StageOutcome.Defeat));
        }

        /// <summary>
        /// 마지막 적과 마지막 아군이 같은 프레임에 쓰러졌다면 이긴 것으로 쳐 주지 않는다.
        /// 서로를 죽이고 끝났는데 다음 스테이지로 넘어가면 이상하다.
        /// </summary>
        [Test]
        public void MutualWipe_CountsAsDefeat()
        {
            Assert.That(StageOutcomeRules.Evaluate(0, true), Is.EqualTo(StageOutcome.Defeat));
        }

        /// <summary>살아 있는 적 수가 음수로 새어도 승리로 읽혀야 한다.</summary>
        [Test]
        public void NegativeEnemyCount_IsVictory()
        {
            Assert.That(StageOutcomeRules.Evaluate(-1, false), Is.EqualTo(StageOutcome.Victory));
        }

        // ── 전멸 판정 (명부 vs 등록 목록) ────────────────
        // BattleRegistry 에는 필드에 선 몸 하나만 들어 있다. 벤치로 내려간 동료는 OnDisable 에서
        // 빠지므로, 교대와 콤보 시전자 전환 사이에는 아무도 등록돼 있지 않은 프레임이 반드시 생긴다.
        // 그 한 프레임을 전멸로 읽던 것이 "파티 체력바는 멀쩡한데 패배 화면이 뜬다"의 정체다.

        /// <summary>
        /// <b>이 수정이 고친 증상.</b> 불릿타임 콤보 중에는 조작 캐릭터가 벤치로 내려가고
        /// 시전자 하나만 서 있다. 그 시전자가 죽으면 등록 목록이 통째로 비는데,
        /// 파티에는 아직 넷이 살아 있다.
        /// </summary>
        [Test]
        public void EmptyRegistryWhileRosterStillAlive_IsNotAWipe()
        {
            Assert.That(StageOutcomeRules.PartyWiped(rosterSize: 5, rosterAlive: 4, registeredAlive: 0),
                        Is.False);
        }

        [Test]
        public void Roster_OutranksTheRegistry()
        {
            // 등록 목록이 비어도 명부가 살아 있으면 전멸이 아니고,
            Assert.That(StageOutcomeRules.PartyWiped(5, 1, 0), Is.False);

            // 명부가 전멸했으면 등록 목록에 누가 남아 있어도 전멸이다
            // (시체가 아직 안 빠진 프레임 · 파티 밖의 소환물).
            Assert.That(StageOutcomeRules.PartyWiped(5, 0, 3), Is.True);
        }

        [Test]
        public void WholeRosterDead_IsAWipe()
        {
            Assert.That(StageOutcomeRules.PartyWiped(5, 0, 0), Is.True);
        }

        [Test]
        public void LastMemberStanding_IsNotAWipe()
        {
            Assert.That(StageOutcomeRules.PartyWiped(5, 1, 1), Is.False);
        }

        /// <summary>
        /// 명부가 없는 씬(스킬 실험장 · 훈련장)과 로스터를 못 만든 경우.
        /// <b>명부가 비었다고 전멸로 치면 배선 실수가 곧바로 패배 화면이 된다.</b>
        /// </summary>
        [Test]
        public void NoRoster_FallsBackToTheRegistry()
        {
            Assert.That(StageOutcomeRules.PartyWiped(rosterSize: 0, rosterAlive: 0, registeredAlive: 2),
                        Is.False, "명부가 없으면 등록 목록이 답이다");

            Assert.That(StageOutcomeRules.PartyWiped(rosterSize: 0, rosterAlive: 0, registeredAlive: 0),
                        Is.True, "명부도 등록도 없으면 그때는 전멸로 본다");
        }

        /// <summary>전멸 판정이 곧 패배다 — 두 규칙이 이어 붙는 자리를 못으로 박는다.</summary>
        [Test]
        public void Wipe_FeedsDefeat()
        {
            bool wiped = StageOutcomeRules.PartyWiped(5, 0, 0);

            Assert.That(StageOutcomeRules.Evaluate(3, wiped), Is.EqualTo(StageOutcome.Defeat));
        }

        [Test]
        public void MidComboDeath_DoesNotEndTheStage()
        {
            bool wiped = StageOutcomeRules.PartyWiped(5, 4, 0);

            Assert.That(StageOutcomeRules.Evaluate(3, wiped, wavesRemaining: true),
                        Is.EqualTo(StageOutcome.Undecided));
        }

        // ── 출구 ────────────────────────────────────────

        private const float ExitX = 5f;

        [Test]
        public void ShortOfExit_DoesNotTrigger()
        {
            Assert.That(StageOutcomeRules.ReachedExit(4.9f, ExitX), Is.False);
        }

        /// <summary>경계값은 통과 쪽에 넣는다. 딱 닿았는데 안 열리면 버그로 보인다.</summary>
        [Test]
        public void ExactlyAtExit_Triggers()
        {
            Assert.That(StageOutcomeRules.ReachedExit(ExitX, ExitX), Is.True);
        }

        [Test]
        public void PastExit_Triggers()
        {
            Assert.That(StageOutcomeRules.ReachedExit(5.5f, ExitX), Is.True);
        }

        /// <summary>왼쪽 끝에 있어도 절대 열리면 안 된다.</summary>
        [Test]
        public void FarLeft_DoesNotTrigger()
        {
            Assert.That(StageOutcomeRules.ReachedExit(-6f, ExitX), Is.False);
        }

        /// <summary>
        /// 문턱은 벽(x = 6)보다 안쪽이어야 한다. 몸통 반지름 때문에 캐릭터는 5.5 근처에서
        /// 막히므로, 문턱을 벽에 맞추면 영영 닿지 않는다.
        /// </summary>
        [Test]
        public void DefaultExit_IsReachableBeforeTheWall()
        {
            const float wallX = 6f;
            const float bodyRadius = 0.5f;

            Assert.That(ExitX, Is.LessThan(wallX - bodyRadius));
        }

        // ── 출구선과 방 크기 ─────────────────────────────
        // 방 크기 보정(RoomRules)이 오른쪽 벽을 옮기므로 문턱도 같이 움직여야 한다.
        // 고정 5로 두면 80% 방에서 캐릭터가 문턱에 닿기 전에 벽에 막혀 — 이겨도 다음 칸으로
        // 못 넘어간다. 화면에는 "오른쪽 벽으로 이동" 안내가 그대로 떠 있어서
        // 클리어 판정이 안 난 것처럼 보이는 종류의 버그다.

        /// <summary>몸통 반지름 때문에 캐릭터가 오른쪽 벽 앞에서 멈추는 x.</summary>
        private static float StopsAt(float roomMaxX) => roomMaxX - BodyRadius;

        private const float BodyRadius = 0.5f;

        /// <summary>100% 방에서는 저작해 둔 값 그대로여야 한다 — 이 수정이 기존 방을 안 건드린다.</summary>
        [Test]
        public void FullRoom_ExitLine_MatchesAuthoredValue()
        {
            RoomRect room = RoomRules.Scale(RoomRect.Default, RoomRules.FullPercent);

            Assert.That(StageOutcomeRules.ExitLine(room.MaxX), Is.EqualTo(ExitX).Within(0.001f));
        }

        /// <summary>줄어든 방에서 벽에 막힌 자리가 문턱을 넘어야 한다.</summary>
        [Test]
        public void ShrunkRoom_ExitLine_IsReachable()
        {
            RoomRect room = RoomRules.Scale(RoomRect.Default, RoomRules.MinPercent);

            Assert.That(StageOutcomeRules.ReachedExit(StopsAt(room.MaxX),
                                                     StageOutcomeRules.ExitLine(room.MaxX)), Is.True);
        }

        /// <summary>
        /// 고정 문턱이 줄어든 방에서 <b>영영 안 열린다</b>는 사실을 못으로 박는다.
        /// 이게 이 수정이 고친 증상이다 — 80% 방의 벽은 x = 4.8 이고 캐릭터는 4.3 에서 멈춘다.
        /// </summary>
        [Test]
        public void ShrunkRoom_FixedExitX_IsUnreachable()
        {
            RoomRect room = RoomRules.Scale(RoomRect.Default, RoomRules.MinPercent);

            Assert.That(StageOutcomeRules.ReachedExit(StopsAt(room.MaxX), ExitX), Is.False);
        }

        /// <summary>커진 방에서는 고정 문턱이 너무 일찍 열린다 — 벽까지 한참 남았는데 지도로 튄다.</summary>
        [Test]
        public void GrownRoom_ExitLine_IsNotTrippedEarly()
        {
            RoomRect room = RoomRules.Scale(RoomRect.Default, RoomRules.MaxPercent);
            float line = StageOutcomeRules.ExitLine(room.MaxX);

            Assert.That(line, Is.GreaterThan(ExitX));
            Assert.That(StageOutcomeRules.ReachedExit(ExitX, line), Is.False);
            Assert.That(StageOutcomeRules.ReachedExit(StopsAt(room.MaxX), line), Is.True);
        }

        /// <summary>
        /// 굴릴 수 있는 <b>모든</b> 비율에서 출구가 열려야 한다. 한 칸이라도 안 열리면
        /// 그 시드로 굴린 런이 그 칸에서 끝난다 — 다시 재현하기가 지독히 어렵다.
        /// </summary>
        [Test]
        public void EveryValidPercent_ExitIsReachableAndInsideTheWall()
        {
            for (int percent = RoomRules.MinPercent; percent <= RoomRules.MaxPercent; percent += RoomRules.Step)
            {
                RoomRect room = RoomRules.Scale(RoomRect.Default, percent);
                float line = StageOutcomeRules.ExitLine(room.MaxX);

                Assert.That(line, Is.LessThan(StopsAt(room.MaxX)),
                            $"{percent}% 방: 문턱 {line} 이(가) 캐릭터가 멈추는 {StopsAt(room.MaxX)} 보다 밖이다.");

                Assert.That(line, Is.LessThan(room.MaxX), $"{percent}% 방: 문턱이 벽 밖이다.");

                Assert.That(StageOutcomeRules.ReachedExit(StopsAt(room.MaxX), line), Is.True,
                            $"{percent}% 방: 벽에 막힌 자리에서 출구가 안 열린다.");
            }
        }

    
        // ── 웨이브 방 ───────────────────────────────────
        // 웨이브 사이에는 살아 있는 적이 0명인 구간이 반드시 생긴다. 그 한 프레임이
        // 승리로 잡히면 첫 웨이브만 잡고 스테이지가 끝난다 — 화면만 봐서는
        // "왜 벌써 클리어지?" 말고는 아무 단서가 없는 종류의 버그다.

        [Test]
        public void BetweenWaves_NoEnemiesAlive_IsStillUndecided()
        {
            Assert.That(StageOutcomeRules.Evaluate(0, false, wavesRemaining: true),
                        Is.EqualTo(StageOutcome.Undecided));
        }

        [Test]
        public void LastWaveCleared_IsVictory()
        {
            Assert.That(StageOutcomeRules.Evaluate(0, false, wavesRemaining: false),
                        Is.EqualTo(StageOutcome.Victory));
        }

        /// <summary>뒤에 웨이브가 몇 개 남았든 아군이 전멸하면 진 것이다.</summary>
        [Test]
        public void AllAlliesDead_LosesEvenWithWavesLeft()
        {
            Assert.That(StageOutcomeRules.Evaluate(3, true, wavesRemaining: true),
                        Is.EqualTo(StageOutcome.Defeat));
        }

        /// <summary>웨이브가 없는 방(씬에 적을 직접 놓은 방)은 지금까지와 똑같이 돈다.</summary>
        [Test]
        public void TwoArgumentForm_MeansNoWavesLeft()
        {
            Assert.That(StageOutcomeRules.Evaluate(0, false),
                        Is.EqualTo(StageOutcomeRules.Evaluate(0, false, wavesRemaining: false)));
        }
    }
}
