using NUnit.Framework;

namespace Prototype.Tests
{
    /// <summary>
    /// 평타 선입력 버퍼. 프레임을 정확히 맞춰야만 연타가 되는 건 조작이 아니라 운이다 —
    /// 이 struct가 그 유예를 만든다.
    /// </summary>
    public class AttackInputBufferTests
    {
        [Test]
        public void FreshPress_IsConsumable()
        {
            var buffer = new AttackInputBuffer();
            buffer.Press(0.25f);

            Assert.That(buffer.HasInput, Is.True);
            Assert.That(buffer.TryConsume(), Is.True);
        }

        [Test]
        public void EmptyBuffer_ConsumesNothing()
        {
            var buffer = new AttackInputBuffer();

            Assert.That(buffer.HasInput, Is.False);
            Assert.That(buffer.TryConsume(), Is.False);
        }

        [Test]
        public void Consume_IsOneShot()
        {
            var buffer = new AttackInputBuffer();
            buffer.Press(0.25f);

            Assert.That(buffer.TryConsume(), Is.True);
            Assert.That(buffer.TryConsume(), Is.False,
                        "큐가 아니다 — 두들긴다고 4타 · 5타가 예약되면 안 된다");
        }

        [Test]
        public void ExpiredPress_IsGone()
        {
            var buffer = new AttackInputBuffer();
            buffer.Press(0.25f);
            buffer.Tick(0.3f);

            Assert.That(buffer.TryConsume(), Is.False);
        }

        [Test]
        public void PressAgain_RefillsWindow()
        {
            var buffer = new AttackInputBuffer();
            buffer.Press(0.25f);
            buffer.Tick(0.2f);
            buffer.Press(0.25f);
            buffer.Tick(0.2f);

            Assert.That(buffer.TryConsume(), Is.True, "마지막에 누른 것이 기준이다");
        }

        [Test]
        public void FrozenTime_DoesNotExpire()
        {
            var buffer = new AttackInputBuffer();
            buffer.Press(0.25f);

            // 불릿타임에는 스케일된 dt가 0이다. 시간이 멈춘 동안 선입력만 혼자 만료되면
            // 조준을 끝내고 나온 순간 콤보가 끊긴다.
            for (int i = 0; i < 100; i++) buffer.Tick(0f);

            Assert.That(buffer.TryConsume(), Is.True);
        }

        [Test]
        public void Clear_DropsInput()
        {
            var buffer = new AttackInputBuffer();
            buffer.Press(0.25f);
            buffer.Clear();

            Assert.That(buffer.HasInput, Is.False);
        }
    }
}
