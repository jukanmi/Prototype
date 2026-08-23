using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 카메라 · 경계 전환. 둘 다 화면으로는 "뭔가 이상한데"로만 보이고 숫자가 안 보이는 종류라
    /// 순수 함수로 떼어 두고 여기서 본다.
    ///
    /// 가장 중요한 항목은 <b>아레나 락에 분기가 없다</b>는 것이다 — 락은 별도 코드가 아니라
    /// "구간이 화면보다 좁으면 클램프가 한 점으로 접힌다"는 계산의 결과일 뿐이다.
    /// </summary>
    public class StageCameraTests
    {
        private const float Eps = 0.001f;

        // ── 클램프 = 락 ─────────────────────────────────

        /// <summary>통로처럼 넓은 구간에서는 목표를 그대로 따라간다.</summary>
        [Test]
        public void WideSection_FollowsTheTarget()
        {
            Assert.That(CameraFrameRules.ClampToSection(20f, 0f, 40f, 8.9f), Is.EqualTo(20f).Within(Eps));
        }

        [Test]
        public void WideSection_StopsAtTheEdges()
        {
            Assert.That(CameraFrameRules.ClampToSection(0f, 0f, 40f, 8.9f), Is.EqualTo(8.9f).Within(Eps));
            Assert.That(CameraFrameRules.ClampToSection(99f, 0f, 40f, 8.9f), Is.EqualTo(31.1f).Within(Eps));
        }

        /// <summary>
        /// <b>이게 아레나 락이다.</b> 구간이 화면보다 좁으면 어디를 봐도 밖이 보이므로
        /// 가운데로 접힌다 — 목표가 어디에 있든 결과가 같다.
        /// </summary>
        [Test]
        public void NarrowSection_LocksToTheCenter()
        {
            const float min = -6f, max = 6f, half = 8.9f;

            Assert.That(CameraFrameRules.ClampToSection(-6f, min, max, half), Is.Zero.Within(Eps));
            Assert.That(CameraFrameRules.ClampToSection(0f, min, max, half), Is.Zero.Within(Eps));
            Assert.That(CameraFrameRules.ClampToSection(6f, min, max, half), Is.Zero.Within(Eps));
        }

        [Test]
        public void OffsetArena_LocksToItsOwnCenter()
        {
            Assert.That(CameraFrameRules.ClampToSection(40f, 33f, 45f, 8.9f), Is.EqualTo(39f).Within(Eps));
        }

        [Test]
        public void IsLocked_MatchesTheClampBehaviour()
        {
            Assert.That(CameraFrameRules.IsLocked(-6f, 6f, 8.9f), Is.True, "12폭 아레나는 잠긴다");
            Assert.That(CameraFrameRules.IsLocked(0f, 40f, 8.9f), Is.False, "40폭 통로는 스크롤된다");
        }

        /// <summary>
        /// 통로가 화면보다 짧으면 <b>스크롤이 아예 안 일어난다</b>.
        /// 통로를 화면 폭의 1.5배로 잡는 이유가 이것이다.
        /// </summary>
        [Test]
        public void CorridorShorterThanScreen_WouldNotScroll()
        {
            float half = CameraFrameRules.HalfWidth(5f, 16f / 9f);
            float screen = half * 2f;

            Assert.That(CameraFrameRules.IsLocked(0f, screen * 0.8f, half), Is.True, "짧은 통로는 굳는다");
            Assert.That(CameraFrameRules.IsLocked(0f, screen * 1.5f, half), Is.False, "1.5배면 스크롤된다");
        }

        [Test]
        public void HalfWidth_IsSizeTimesAspect()
        {
            Assert.That(CameraFrameRules.HalfWidth(5f, 16f / 9f), Is.EqualTo(8.888f).Within(0.01f));
        }

        // ── 데드존 ──────────────────────────────────────

        /// <summary>밴드 안이면 카메라는 제자리다. 없으면 한 발짝마다 배경이 흔들린다.</summary>
        [Test]
        public void InsideTheBand_CameraStaysPut()
        {
            Assert.That(CameraFrameRules.ApplyDeadZone(10f, 11f, 1.5f), Is.EqualTo(10f).Within(Eps));
            Assert.That(CameraFrameRules.ApplyDeadZone(10f, 9f, 1.5f), Is.EqualTo(10f).Within(Eps));
        }

        /// <summary>밴드를 벗어나면 딱 밴드 끝만큼만 따라간다 — 목표를 중앙에 붙이지 않는다.</summary>
        [Test]
        public void OutsideTheBand_CameraTrailsByTheBandWidth()
        {
            Assert.That(CameraFrameRules.ApplyDeadZone(10f, 14f, 1.5f), Is.EqualTo(12.5f).Within(Eps));
            Assert.That(CameraFrameRules.ApplyDeadZone(10f, 6f, 1.5f), Is.EqualTo(7.5f).Within(Eps));
        }

        [Test]
        public void ZeroBand_FollowsExactly()
        {
            Assert.That(CameraFrameRules.ApplyDeadZone(10f, 14f, 0f), Is.EqualTo(14f).Within(Eps));
        }

        // ── 경계 전환 ───────────────────────────────────

        [Test]
        public void Snap_TakesEffectImmediately()
        {
            var blend = new BoundsBlend();
            blend.Snap(-6f, 6f);

            Assert.That(blend.Min, Is.EqualTo(-6f).Within(Eps));
            Assert.That(blend.Max, Is.EqualTo(6f).Within(Eps));
            Assert.That(blend.IsBlending, Is.False);
        }

        [Test]
        public void To_MovesAcrossTheDuration()
        {
            var blend = new BoundsBlend();
            blend.Snap(0f, 10f);
            blend.To(100f, 110f, 0.3f);

            Assert.That(blend.Min, Is.EqualTo(0f).Within(Eps), "전환 시작 프레임에 이미 튀었다");
            Assert.That(blend.IsBlending, Is.True);

            blend.Tick(0.3f);

            Assert.That(blend.Min, Is.EqualTo(100f).Within(Eps));
            Assert.That(blend.Max, Is.EqualTo(110f).Within(Eps));
            Assert.That(blend.IsBlending, Is.False);
        }

        [Test]
        public void HalfwayThrough_SitsBetween()
        {
            var blend = new BoundsBlend();
            blend.Snap(0f, 10f);
            blend.To(100f, 110f, 0.3f);
            blend.Tick(0.15f);

            Assert.That(blend.Min, Is.InRange(1f, 99f));
        }

        /// <summary>매 프레임 같은 목표가 들어온다. 그때마다 다시 시작하면 영영 도착하지 않는다.</summary>
        [Test]
        public void RepeatingTheSameTarget_DoesNotRestart()
        {
            var blend = new BoundsBlend();
            blend.Snap(0f, 10f);
            blend.To(100f, 110f, 0.3f);

            blend.Tick(0.2f);
            blend.To(100f, 110f, 0.3f);   // 같은 목표
            blend.Tick(0.2f);

            Assert.That(blend.IsBlending, Is.False, "같은 목표가 전환을 다시 시작시켰다");
        }

        /// <summary>
        /// 전환 도중에 또 전환이 걸릴 수 있다(통로를 빨리 지나가면 그렇다).
        /// <b>지금 보이는 값</b>에서 출발해야 카메라가 안 튄다.
        /// </summary>
        [Test]
        public void InterruptedBlend_StartsFromWhereItIs()
        {
            var blend = new BoundsBlend();
            blend.Snap(0f, 10f);
            blend.To(100f, 110f, 0.3f);
            blend.Tick(0.15f);

            float mid = blend.Min;
            blend.To(200f, 210f, 0.3f);

            Assert.That(blend.Min, Is.EqualTo(mid).Within(Eps), "전환이 끊기면서 화면이 튀었다");
        }

        [Test]
        public void TargetBounds_AreReadableDuringTheBlend()
        {
            var blend = new BoundsBlend();
            blend.Snap(0f, 10f);
            blend.To(33f, 45f, 0.3f);

            Assert.That(blend.TargetMin, Is.EqualTo(33f).Within(Eps));
            Assert.That(blend.TargetMax, Is.EqualTo(45f).Within(Eps));
        }
    }
}
