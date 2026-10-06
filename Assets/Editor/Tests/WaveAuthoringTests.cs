using NUnit.Framework;
using Prototype;

namespace Prototype.Tests
{
    /// <summary>
    /// 웨이브 저작 어휘의 <b>불변식</b>. 좌표도 시간도 없는 순수 데이터라
    /// 여기서 못 지키면 뒤 단계가 전부 그 위에 쌓인다.
    ///
    /// 계획서: docs/Wave_Authoring_Refactor_Plan.md
    /// </summary>
    public class WaveAuthoringTests
    {
        // ── 직렬화 값이 곧 규약이다 ──────────────────────

        /// <summary>
        /// 인스펙터에서 줄을 새로 추가하면 모든 칸이 <b>0으로 직렬화</b>된다.
        /// 그래서 0번 값이 지금까지의 동작이어야 한다 — 아무것도 안 고른 줄이
        /// 자동 배치 · 화면 밖 비행 · 전멸 조건으로 돌아야 저작자가 놀라지 않는다.
        /// </summary>
        [Test]
        public void DefaultEnumValues_AreTodaysBehaviour()
        {
            Assert.That((int)SpawnOrigin.Auto, Is.EqualTo(0), "새 줄이 씬 지점을 찾으려 든다");
            Assert.That((int)SpawnMotion.FlyIn, Is.EqualTo(0), "새 줄의 등장 연출이 바뀐다");
            Assert.That((int)WaveAdvance.AllCleared, Is.EqualTo(0), "새 웨이브가 안 열리는 조건으로 선다");
        }

        /// <summary>
        /// 나머지 값도 못 박는다. 애셋은 enum 을 <b>정수로</b> 저장하므로
        /// 중간에 항목을 끼워 넣으면 이미 구운 애셋의 뜻이 조용히 바뀐다.
        /// </summary>
        [Test]
        public void EnumOrdinals_AreFrozen()
        {
            Assert.That((int)SpawnOrigin.Point, Is.EqualTo(1));

            Assert.That((int)SpawnMotion.FromWall, Is.EqualTo(1));
            Assert.That((int)SpawnMotion.Burrow, Is.EqualTo(2));

            Assert.That((int)WaveAdvance.TargetKilled, Is.EqualTo(1));
        }

        /// <summary>기본 생성한 구조체가 그대로 굴러가야 한다. 인스펙터의 새 줄이 이 상태다.</summary>
        [Test]
        public void DefaultEntry_IsAutoAndWellFormed()
        {
            var entry = new WaveSpawnEntry();

            Assert.That(entry.origin, Is.EqualTo(SpawnOrigin.Auto));
            Assert.That(entry.motion, Is.EqualTo(SpawnMotion.FlyIn));
            Assert.That(entry.UsesPoint, Is.False);
            Assert.That(entry.IsWellFormed, Is.True, "빈 줄이 저작 실수로 잡혔다");
            Assert.That(entry.AppearAt, Is.EqualTo(0f));
        }

        // ── 등장 시각 ───────────────────────────────────

        /// <summary>웨이브가 시작되기 전은 없다. 음수는 0으로 본다.</summary>
        [Test]
        public void AppearAt_ClampsNegativeToZero()
        {
            var entry = WaveSpawnEntry.Auto(EnemyRole.Melee, appearAt: -3f);

            Assert.That(entry.AppearAt, Is.EqualTo(0f));
        }

        [Test]
        public void AppearAt_KeepsAuthoredValue()
        {
            var entry = WaveSpawnEntry.Auto(EnemyRole.Charger, appearAt: 3.5f);

            Assert.That(entry.AppearAt, Is.EqualTo(3.5f).Within(0.0001f));
        }

        // ── 위치 소스 ───────────────────────────────────

        [Test]
        public void Auto_LeavesPointIdEmpty()
        {
            var entry = WaveSpawnEntry.Auto(EnemyRole.Ranged, SpawnSide.Left, 1.2f);

            Assert.That(entry.origin, Is.EqualTo(SpawnOrigin.Auto));
            Assert.That(entry.side, Is.EqualTo(SpawnSide.Left));
            Assert.That(entry.UsesPoint, Is.False);
            Assert.That(entry.elite, Is.False);
        }

        [Test]
        public void At_MarksPointOrigin()
        {
            var entry = WaveSpawnEntry.At(EnemyRole.Charger, "굴_좌", 3.5f, SpawnMotion.Burrow);

            Assert.That(entry.origin, Is.EqualTo(SpawnOrigin.Point));
            Assert.That(entry.pointId, Is.EqualTo("굴_좌"));
            Assert.That(entry.motion, Is.EqualTo(SpawnMotion.Burrow));
            Assert.That(entry.UsesPoint, Is.True);
            Assert.That(entry.IsWellFormed, Is.True);
        }

        /// <summary>
        /// 지점을 쓰겠다고 해 놓고 이름이 비어 있으면 <b>저작 실수</b>다.
        /// 조용히 원점에 소환하지 않고 자동 배치로 떨어뜨린 뒤 경고하기 위해 여기서 갈린다.
        /// </summary>
        [Test]
        public void PointWithoutId_IsNotWellFormed()
        {
            var entry = WaveSpawnEntry.Auto(EnemyRole.Melee);
            entry.origin = SpawnOrigin.Point;

            Assert.That(entry.UsesPoint, Is.False);
            Assert.That(entry.IsWellFormed, Is.False, "이름 없는 지점 지정이 통과했다");
        }

        /// <summary>
        /// 공백만 남은 이름은 눈으로는 빈 칸과 구분이 안 되는데,
        /// 이름 대조에서는 절대 안 맞는 이름이 된다. 빈 것으로 본다.
        /// </summary>
        [Test]
        public void WhitespacePointId_CountsAsEmpty()
        {
            var entry = WaveSpawnEntry.At(EnemyRole.Melee, "   ");

            Assert.That(entry.UsesPoint, Is.False);
            Assert.That(entry.IsWellFormed, Is.False);
        }

        /// <summary>자동 배치 줄은 이름이 비어 있는 것이 정상이다. 실수로 잡으면 안 된다.</summary>
        [Test]
        public void AutoWithoutId_IsWellFormed()
        {
            var entry = WaveSpawnEntry.Auto(EnemyRole.Melee);

            Assert.That(entry.IsWellFormed, Is.True);
        }

        // ── 다음 웨이브 조건 ────────────────────────────

        /// <summary>
        /// <c>TargetKilled</c>는 이름만 잡아 둔 자리다. 지원한다고 답하기 시작하면
        /// 디렉터가 안 도는 조건으로 서서 스테이지가 영영 안 끝난다.
        /// </summary>
        [Test]
        public void OnlyAllCleared_IsSupportedForNow()
        {
            Assert.That(WaveAdvanceRules.IsSupported(WaveAdvance.AllCleared), Is.True);
            Assert.That(WaveAdvanceRules.IsSupported(WaveAdvance.TargetKilled), Is.False,
                        "미구현 조건이 지원된다고 답했다");
        }

        [Test]
        public void Resolve_FoldsUnsupportedToAllCleared()
        {
            Assert.That(WaveAdvanceRules.Resolve(WaveAdvance.AllCleared),
                        Is.EqualTo(WaveAdvance.AllCleared));
            Assert.That(WaveAdvanceRules.Resolve(WaveAdvance.TargetKilled),
                        Is.EqualTo(WaveAdvance.AllCleared));
        }

        /// <summary>정의되지 않은 값이 애셋에 박혀 있어도 전멸 조건으로 돈다. 멈추면 안 된다.</summary>
        [Test]
        public void Resolve_HandlesGarbageValues()
        {
            Assert.That(WaveAdvanceRules.Resolve((WaveAdvance)99),
                        Is.EqualTo(WaveAdvance.AllCleared));
        }
    }
}
