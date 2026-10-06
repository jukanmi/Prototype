using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 깊이 배율과 좌표 변환은 순수 함수다. 여기서 값이 어긋나면 캐릭터 · 투사체가
    /// 한꺼번에 틀어진다.
    ///
    /// 예전에는 <c>ToView</c>가 Z를 화면 세로 · 가로로 손수 접었다. 그 가로 접기가
    /// "위로 걸으면 대각선으로 간다"의 원인이었고, 논리 좌표의 사각형을 화면에서
    /// 평행사변형으로 만들어 바닥 그림과도 어긋났다. 지금은 카메라가 기울어 깊이를
    /// 보여 주므로 <c>ToView</c>는 높이만 더한다.
    /// </summary>
    public class BeltScrollDepthTests
    {
        private GameObject rig;

        [SetUp]
        public void SetUp()
        {
            BeltScroll.DepthScalePerUnit = 0.06f;
            rig = BeltScrollTestCamera.Attach();
        }

        [TearDown]
        public void TearDown()
        {
            BeltScrollTestCamera.Detach(rig);
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        // ── 깊이 배율 ───────────────────────────────────

        [Test]
        public void ScaleAt_Origin_IsOne()
        {
            Assert.That(BeltScroll.ScaleAt(0f), Is.EqualTo(1f).Within(0.0001f));
        }

        /// <summary>방 깊이 ±3에서 앞뒤 1.44배. 이게 "보통" 강도로 고른 값이다.</summary>
        [Test]
        public void ScaleAt_BackShrinks_FrontGrows()
        {
            Assert.That(BeltScroll.ScaleAt(3f), Is.EqualTo(0.82f).Within(0.0001f), "뒤쪽이 작아야 한다");
            Assert.That(BeltScroll.ScaleAt(-3f), Is.EqualTo(1.18f).Within(0.0001f), "앞쪽이 커야 한다");
        }

        /// <summary>하한이 없으면 배율이 음수가 돼 스프라이트가 뒤집힌다.</summary>
        [Test]
        public void ScaleAt_FarBeyondRoom_ClampsToMinimum()
        {
            Assert.That(BeltScroll.ScaleAt(1000f), Is.EqualTo(BeltScroll.MinScale).Within(0.0001f));
        }

        // ── 가로 밀림이 없다는 것 ────────────────────────

        /// <summary>
        /// <b>이 테스트가 이 변경의 핵심이다.</b> 깊이가 화면 가로로 새면 위아래 이동이
        /// 대각선이 되고, 걷는 영역이 평행사변형이 돼 사각형 바닥 그림과 어긋난다.
        /// </summary>
        [Test]
        public void ToView_NeverShiftsSideways()
        {
            Vector3 back = BeltScroll.ToView(new Vector3(0f, 0f, 3f));
            Vector3 front = BeltScroll.ToView(new Vector3(0f, 0f, -3f));

            Assert.That(back.x, Is.EqualTo(0f).Within(0.0001f), "뒤쪽이 옆으로 밀리면 안 된다");
            Assert.That(front.x, Is.EqualTo(0f).Within(0.0001f), "앞쪽이 옆으로 밀리면 안 된다");
        }

        /// <summary>기울여도 카메라 right는 (1,0,0)이다 — 피치만 주기 때문이다.</summary>
        [Test]
        public void ScreenRight_HasNoDepthComponent()
        {
            Assert.That(BeltScroll.ScreenRight.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(BeltScroll.ScreenRight.y, Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>화면 위 = (0, cosθ, sinθ). 깊이와 높이가 세로로 환산되는 비율이 여기 있다.</summary>
        [Test]
        public void ScreenUp_IsThePitchedCameraUp()
        {
            Vector3 up = BeltScroll.ScreenUp;

            Assert.That(up.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(up.y, Is.EqualTo(BeltScrollTestCamera.Cos).Within(0.0001f));
            Assert.That(up.z, Is.EqualTo(BeltScrollTestCamera.Sin).Within(0.0001f));
        }

        /// <summary>기울기가 0이면 깊이가 화면에서 안 보인다 — 정면 투영으로 돌아간다.</summary>
        [Test]
        public void ScreenUp_AtZeroTilt_IsWorldUp()
        {
            BeltScrollTestCamera.Detach(rig);
            rig = BeltScrollTestCamera.Attach(0f);

            Assert.That(BeltScroll.ScreenUp.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(BeltScroll.ScreenUp.y, Is.EqualTo(1f).Within(0.0001f));
        }

        // ── 좌표 변환 ───────────────────────────────────

        /// <summary>높이는 월드 Y로 얹는다. 깊이는 손대지 않는다.</summary>
        [Test]
        public void ToView_AddsHeightOnly()
        {
            Vector3 p = BeltScroll.ToView(new Vector3(4.2f, 0f, -1.75f), 2f);

            Assert.That(p.x, Is.EqualTo(4.2f).Within(0.0001f));
            Assert.That(p.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(p.z, Is.EqualTo(-1.75f).Within(0.0001f));
        }

        [Test]
        public void ViewToGround_RoundTrip_StillMatches()
        {
            var ground = new Vector3(2.5f, 0f, -1.75f);

            Vector3 back = BeltScroll.ToGround(BeltScroll.ToView(ground, 3f));

            Assert.That(back.x, Is.EqualTo(ground.x).Within(0.0001f));
            Assert.That(back.y, Is.EqualTo(0f).Within(0.0001f), "높이는 떨어져야 한다");
            Assert.That(back.z, Is.EqualTo(ground.z).Within(0.0001f));
        }

        // ── 마우스 피킹 ─────────────────────────────────

        /// <summary>
        /// 화면 → 바닥이 왕복해야 "보이는 곳"과 "찍히는 곳"이 일치한다.
        /// 기울기를 바꿔도 성립해야 한다 — 레이캐스트라 각도를 손으로 풀지 않는다.
        /// </summary>
        [Test]
        public void ScreenToGround_RoundTripsThroughTheCamera()
        {
            Camera cam = BeltScroll.Cam;
            cam.orthographicSize = 5f;
            cam.transform.position = -(cam.transform.forward * 10f);

            var ground = new Vector3(2.5f, 0f, -1.75f);
            Vector3 screen = cam.WorldToScreenPoint(ground);

            Vector3 back = BeltScroll.ScreenToGround(cam, screen);

            Assert.That(back.x, Is.EqualTo(ground.x).Within(0.001f));
            Assert.That(back.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(back.z, Is.EqualTo(ground.z).Within(0.001f));
        }

        /// <summary>카메라가 없으면 터지지 말고 원점으로 접어야 한다.</summary>
        [Test]
        public void ScreenToGround_WithoutCamera_ReturnsGroundOrigin()
        {
            Vector3 p = BeltScroll.ScreenToGround(null, Vector2.zero, groundY: 0f);

            Assert.That(p, Is.EqualTo(Vector3.zero));
        }
    }
}
