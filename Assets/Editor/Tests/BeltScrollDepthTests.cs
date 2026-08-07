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
            BeltScroll.DepthScalePerUnit = 0.06f;
        }

        [TearDown]
        public void ResetStatics()
        {
            // static이라 다음 테스트로 새어 나간다. 원래 기본값으로 돌려놓는다.
            BeltScroll.DepthToScreen = 0.5f;
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
    }
}
