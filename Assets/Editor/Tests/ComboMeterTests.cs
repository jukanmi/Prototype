using NUnit.Framework;

namespace Prototype.Tests
{
    /// <summary>
    /// 화면 오른쪽 콤보 카운터가 세는 방식. <see cref="ComboMeter"/>는 순수 계산이라
    /// UI를 세우지 않고 그대로 돌린다.
    ///
    /// 여기서 지키는 건 세 가지다 — 이어지는 동안 누적되고, 공백이 길면 끊기고,
    /// <b>길이에 공백이 안 들어간다</b>. 마지막 것이 DPS의 진실성을 정한다.
    /// </summary>
    public class ComboMeterTests
    {
        private static ComboMeter New(float window = 2.5f, float linger = 1.5f)
            => new ComboMeter { Window = window, LingerDuration = linger };

        [Test]
        public void FirstHit_StartsAtOne()
        {
            ComboMeter m = New();
            m.AddHit(30f);

            Assert.That(m.Hits, Is.EqualTo(1));
            Assert.That(m.Damage, Is.EqualTo(30f).Within(0.001f));
            Assert.That(m.IsRunning, Is.True);
            Assert.That(m.IsVisible, Is.True);
        }

        [Test]
        public void HitsWithinWindow_KeepAccumulating()
        {
            ComboMeter m = New();

            m.AddHit(10f);
            m.Tick(0.4f);
            m.AddHit(20f);
            m.Tick(0.4f);
            m.AddHit(30f);

            Assert.That(m.Hits, Is.EqualTo(3));
            Assert.That(m.Damage, Is.EqualTo(60f).Within(0.001f));
            Assert.That(m.Duration, Is.EqualTo(0.8f).Within(0.001f),
                        "첫 타부터 마지막 타까지가 콤보 길이다");
        }

        [Test]
        public void Gap_EndsCombo_AndNextHitRestartsAtOne()
        {
            ComboMeter m = New(window: 2.5f);

            m.AddHit(10f);
            m.AddHit(10f);
            Assert.That(m.Hits, Is.EqualTo(2));

            m.Tick(2.6f);
            Assert.That(m.IsRunning, Is.False, "창을 넘기면 콤보가 끝난다");
            Assert.That(m.Hits, Is.EqualTo(2), "끝나도 숫자는 남는다 — 마지막 값을 읽을 여유");

            m.AddHit(10f);
            Assert.That(m.Hits, Is.EqualTo(1), "다음 타격은 1타부터 다시 센다");
            Assert.That(m.Damage, Is.EqualTo(10f).Within(0.001f), "피해도 같이 초기화된다");
            Assert.That(m.Duration, Is.Zero);
        }

        /// <summary>
        /// 마지막 타 이후의 공백은 길이에 안 들어간다. 여기가 새면 콤보가 끝난 뒤
        /// 화면에 남아 있는 동안 DPS가 계속 떨어진다 — 다 끝난 값이 혼자 움직인다.
        /// </summary>
        [Test]
        public void TrailingGap_DoesNotStretchDuration()
        {
            ComboMeter m = New();

            m.AddHit(50f);
            m.Tick(0.5f);
            m.AddHit(50f);

            float duration = m.Duration;
            float dps = m.Dps;

            m.Tick(2.0f);
            m.Tick(2.0f);

            Assert.That(m.Duration, Is.EqualTo(duration).Within(0.001f));
            Assert.That(m.Dps, Is.EqualTo(dps).Within(0.001f));
        }

        [Test]
        public void Dps_IsDamageOverDuration()
        {
            ComboMeter m = New();

            m.AddHit(100f);
            m.Tick(1f);
            m.AddHit(100f);

            Assert.That(m.Dps, Is.EqualTo(200f).Within(0.01f));
        }

        [Test]
        public void SingleHit_HasNoDps()
        {
            ComboMeter m = New();
            m.AddHit(999f);

            Assert.That(m.Dps, Is.Zero, "길이가 0인데 나눠 버리면 무한대가 화면에 뜬다");
        }

        // ── 표시 ─────────────────────────────────────────

        [Test]
        public void NothingHit_IsInvisible()
        {
            ComboMeter m = New();

            Assert.That(m.IsVisible, Is.False);
            Assert.That(m.Alpha, Is.Zero);
        }

        [Test]
        public void AfterLinger_FadesOutAndHides()
        {
            ComboMeter m = New(window: 2.5f, linger: 1.5f);
            m.AddHit(10f);

            m.Tick(2.5f);                       // 콤보 종료 시점
            Assert.That(m.Alpha, Is.EqualTo(1f).Within(0.01f), "끝난 직후에는 아직 또렷하다");

            m.Tick(0.75f);                      // 잔류 구간 절반
            Assert.That(m.Alpha, Is.EqualTo(0.5f).Within(0.02f));

            m.Tick(0.8f);                       // 잔류 구간 끝
            Assert.That(m.IsVisible, Is.False, "다 사라지면 패널을 내린다");
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            ComboMeter m = New();
            m.AddHit(10f);
            m.Tick(0.2f);
            m.AddHit(10f);

            m.Reset();

            Assert.That(m.Hits, Is.Zero);
            Assert.That(m.Damage, Is.Zero);
            Assert.That(m.Duration, Is.Zero);
            Assert.That(m.IsRunning, Is.False);
            Assert.That(m.IsVisible, Is.False);
        }

        /// <summary>
        /// 정지 중(dt 0)에는 아무것도 흐르지 않아야 한다. 불릿타임으로 카드를 정렬하는 동안
        /// 콤보가 혼자 끊기면 "정지 중에 콤보가 죽는다"가 된다.
        /// </summary>
        [Test]
        public void FrozenTime_DoesNotExpireCombo()
        {
            ComboMeter m = New(window: 2.5f);
            m.AddHit(10f);

            for (int i = 0; i < 300; i++) m.Tick(0f);

            Assert.That(m.IsRunning, Is.True);
            Assert.That(m.SinceLastHit, Is.Zero);
        }
    }
}
