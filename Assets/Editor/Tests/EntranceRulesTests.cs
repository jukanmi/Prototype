using NUnit.Framework;
using Prototype;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 화면 밖 좌표 계산. <b>틀려도 화면에는 안 보인다</b> —
    /// 시작점이 화면 안이면 "몹이 그냥 나타난다"로, 착지점보다 안쪽이면
    /// "몹이 거꾸로 들어간다"로만 보이고 숫자는 어디에도 안 뜬다.
    /// </summary>
    public class EntranceRulesTests
    {
        private const float Eps = 0.0001f;

        private const float CamX = 0f;
        private const float Half = 8f;

        // ── 화면 판정 ───────────────────────────────────

        [Test]
        public void IsOnScreen_CoversTheVisibleBand()
        {
            Assert.That(EntranceRules.IsOnScreen(0f, CamX, Half), Is.True);
            Assert.That(EntranceRules.IsOnScreen(Half, CamX, Half), Is.True, "가장자리는 아직 화면 안");
            Assert.That(EntranceRules.IsOnScreen(-Half, CamX, Half), Is.True);

            Assert.That(EntranceRules.IsOnScreen(Half + 0.01f, CamX, Half), Is.False);
            Assert.That(EntranceRules.IsOnScreen(-Half - 0.01f, CamX, Half), Is.False);
        }

        /// <summary>카메라가 움직여도 밴드가 함께 움직인다. 통로에서 이게 안 따라오면 밖이 안이 된다.</summary>
        [Test]
        public void IsOnScreen_FollowsTheCamera()
        {
            Assert.That(EntranceRules.IsOnScreen(20f, 20f, Half), Is.True);
            Assert.That(EntranceRules.IsOnScreen(0f, 20f, Half), Is.False);
        }

        // ── 가장자리 선택 ───────────────────────────────

        /// <summary>가까운 쪽에서 온다. 먼 쪽이면 같은 시간에 화면을 가로질러야 해서 착지가 급정거로 보인다.</summary>
        [Test]
        public void SideOf_PicksTheNearerEdge()
        {
            Assert.That(EntranceRules.SideOf(5f, CamX), Is.EqualTo(1));
            Assert.That(EntranceRules.SideOf(-5f, CamX), Is.EqualTo(-1));

            // 카메라가 옮겨 가면 같은 좌표라도 반대쪽이 된다.
            Assert.That(EntranceRules.SideOf(5f, 20f), Is.EqualTo(-1));
        }

        // ── 화면 밖 좌표 ────────────────────────────────

        /// <summary>이게 이 파일의 존재 이유다. 시작점이 화면 안이면 연출이 통째로 무의미하다.</summary>
        [Test]
        public void OffscreenX_LandsOutsideTheBand()
        {
            foreach (float landing in new[] { 0f, 3f, -3f, 7.9f, -7.9f })
            {
                int side = EntranceRules.SideOf(landing, CamX);
                float x = EntranceRules.OffscreenX(landing, side, CamX, Half);

                Assert.That(EntranceRules.IsOnScreen(x, CamX, Half), Is.False,
                            $"착지 {landing} → 시작 {x} 가 화면 안이다");
            }
        }

        /// <summary>
        /// 착지점이 <b>이미</b> 화면 밖인 경우. 화면 끝만 보고 잡으면 시작점이 착지점보다
        /// 안쪽으로 떨어져 몸이 거꾸로 들어온다 — 아레나처럼 구간이 화면보다 좁을 때 실제로 생긴다.
        /// </summary>
        [Test]
        public void OffscreenX_StaysBeyondTheLanding()
        {
            float landing = Half + 5f;
            float x = EntranceRules.OffscreenX(landing, 1, CamX, Half);
            Assert.That(x, Is.GreaterThan(landing));

            landing = -Half - 5f;
            x = EntranceRules.OffscreenX(landing, -1, CamX, Half);
            Assert.That(x, Is.LessThan(landing));
        }

        [Test]
        public void OffscreenX_HonorsTheSide()
        {
            Assert.That(EntranceRules.OffscreenX(0f, 1, CamX, Half), Is.GreaterThan(Half));
            Assert.That(EntranceRules.OffscreenX(0f, -1, CamX, Half), Is.LessThan(-Half));
        }

        /// <summary>옆으로만 벗어난다. 깊이가 흔들리면 정착 지점의 줄(레인)이 어긋난다.</summary>
        [Test]
        public void OffscreenPoint_KeepsDepthAndHeight()
        {
            var landing = new Vector3(3f, 1.5f, -2.25f);
            Vector3 start = EntranceRules.OffscreenPoint(landing, CamX, Half);

            Assert.That(start.y, Is.EqualTo(landing.y).Within(Eps));
            Assert.That(start.z, Is.EqualTo(landing.z).Within(Eps));
            Assert.That(EntranceRules.IsOnScreen(start.x, CamX, Half), Is.False);
        }

        // ── 곡선 ────────────────────────────────────────

        [Test]
        public void Ease_PinsBothEnds()
        {
            Assert.That(EntranceRules.Ease(0f), Is.EqualTo(0f).Within(Eps));
            Assert.That(EntranceRules.Ease(1f), Is.EqualTo(1f).Within(Eps));
        }

        [Test]
        public void Ease_IsMonotonic()
        {
            float prev = -1f;

            for (int i = 0; i <= 20; i++)
            {
                float v = EntranceRules.Ease(i / 20f);
                Assert.That(v, Is.GreaterThanOrEqualTo(prev), $"t={i / 20f}");
                prev = v;
            }
        }

        /// <summary>
        /// "튀어나온다"는 인상은 <b>첫 프레임의 속도</b>가 만든다.
        /// 절반 시점에 절반보다 훨씬 많이 와 있어야 감속으로 읽힌다.
        /// </summary>
        [Test]
        public void Ease_FrontLoadsTheMotion()
        {
            Assert.That(EntranceRules.Ease(0.5f), Is.GreaterThan(0.75f));
        }

        [Test]
        public void Sample_PinsBothEnds()
        {
            var a = new Vector3(-12f, 0f, 1f);
            var b = new Vector3(4f, 0f, 1f);

            Assert.That(Vector3.Distance(EntranceRules.Sample(a, b, 0f), a), Is.LessThan(Eps));
            Assert.That(Vector3.Distance(EntranceRules.Sample(a, b, 1f), b), Is.LessThan(Eps));
        }

        // ── 안전핀 ──────────────────────────────────────

        /// <summary>
        /// 진입 중인 적은 라운드 클리어 인구조사에 잡힌다. 상한이 없으면
        /// 저작 실수 하나로 라운드가 영영 안 끝난다.
        /// </summary>
        [Test]
        public void ClampSeconds_HasAnUpperBound()
        {
            Assert.That(EntranceRules.ClampSeconds(999f), Is.EqualTo(EntranceRules.MaxSeconds).Within(Eps));
            Assert.That(EntranceRules.ClampSeconds(0.001f), Is.EqualTo(EntranceRules.MinSeconds).Within(Eps));
        }

        /// <summary>0은 "안 정했다"는 뜻이다. 0초짜리 연출로 읽으면 등장이 통째로 사라진다.</summary>
        [Test]
        public void ClampSeconds_TreatsZeroAsDefault()
        {
            Assert.That(EntranceRules.ClampSeconds(0f),
                        Is.EqualTo(EntranceRules.DefaultSeconds).Within(Eps));
        }

        /// <summary>기본 연출 길이는 컷인(0.98초) 안에 묻혀야 한다 — 4단계에서 슬롯이 안 밀린다.</summary>
        [Test]
        public void DefaultSeconds_FitsUnderTheCutin()
        {
            Assert.That(EntranceRules.DefaultSeconds, Is.LessThan(0.98f));
        }
    }
}
