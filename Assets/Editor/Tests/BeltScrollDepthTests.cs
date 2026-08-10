using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 깊이 배율은 순수 함수다. 여기서 값이 어긋나면 캐릭터 · 투사체가 한꺼번에 틀어진다.
    /// </summary>
    public class BeltScrollDepthTests
    {
        [SetUp]
        public void SetDefaults()
        {
            BeltScroll.DepthToScreen = 0.9f;
            BeltScroll.DepthToScreenX = 0.45f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        [TearDown]
        public void ResetStatics()
        {
            // static이라 다음 테스트로 새어 나간다. 원래 기본값으로 돌려놓는다.
            BeltScroll.DepthToScreen = 0.5f;
            BeltScroll.DepthToScreenX = 0.45f;
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

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

        /// <summary>
        /// 배율이 좌표 변환에 새면 마우스 피킹("보이는 곳"과 "찍히는 곳")이 어긋난다.
        /// ScaleAt을 넣은 뒤에도 왕복이 그대로여야 한다.
        /// </summary>
        [Test]
        public void ViewToGround_RoundTrip_StillMatches()
        {
            var ground = new Vector3(2.5f, 0f, -1.75f);

            Vector3 back = BeltScroll.ToGround(BeltScroll.ToView(ground));

            Assert.That(back.x, Is.EqualTo(ground.x).Within(0.0001f));
            Assert.That(back.z, Is.EqualTo(ground.z).Within(0.0001f));
        }

        // ── 가로 밀림(oblique) ───────────────────────────

        /// <summary>깊이가 가로로도 밀려야 바닥이 평행사변형으로 보인다.</summary>
        [Test]
        public void ToView_ShiftsSidewaysWithDepth()
        {
            Vector3 back = BeltScroll.ToView(new Vector3(0f, 0f, 3f));
            Vector3 front = BeltScroll.ToView(new Vector3(0f, 0f, -3f));

            Assert.That(back.x, Is.EqualTo(3f * 0.45f).Within(0.0001f), "뒤쪽이 오른쪽으로 밀린다");
            Assert.That(front.x, Is.EqualTo(-3f * 0.45f).Within(0.0001f), "앞쪽이 왼쪽으로 밀린다");
        }

        /// <summary>z=0 평면은 밀림이 없다 — 기준선이 흔들리면 안 된다.</summary>
        [Test]
        public void ToView_AtZeroDepth_DoesNotShift()
        {
            Vector3 p = BeltScroll.ToView(new Vector3(4.2f, 0f, 0f));

            Assert.That(p.x, Is.EqualTo(4.2f).Within(0.0001f));
        }

        /// <summary>
        /// 밀림이 붙어도 역변환이 성립해야 한다 — 마우스 피킹이 여기 걸려 있다.
        /// 세로에서 깊이를 먼저 되찾고 그 깊이로 가로에서 밀림을 뺀다.
        /// </summary>
        [Test]
        public void ViewToGround_UndoesTheSidewaysShift()
        {
            var ground = new Vector3(-1.5f, 0f, 2.25f);

            Vector3 view = BeltScroll.ToView(ground);
            Assert.That(view.x, Is.Not.EqualTo(ground.x).Within(0.001f), "밀림이 실제로 걸려야 한다");

            Vector3 back = BeltScroll.ToGround(view);

            Assert.That(back.x, Is.EqualTo(ground.x).Within(0.0001f));
            Assert.That(back.z, Is.EqualTo(ground.z).Within(0.0001f));
        }

        /// <summary>밀림을 끄면 정면 투영으로 돌아간다.</summary>
        [Test]
        public void ToView_WithZeroShear_KeepsX()
        {
            BeltScroll.DepthToScreenX = 0f;

            Vector3 p = BeltScroll.ToView(new Vector3(1f, 0f, 3f));

            Assert.That(p.x, Is.EqualTo(1f).Within(0.0001f));
        }
    }
}
