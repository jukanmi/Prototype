using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.Tests
{
    /// <summary>
    /// 화면 아래 검은 띠 — 방 밖 배경이 비치던 자리를 채운다. <b>맵보다 먼저 그려서</b> 맵을 하나도 안 가린다.
    /// </summary>
    public class HudBandTests
    {
        private GameObject owner;
        private Camera cam;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Owner");
            cam = new GameObject("Camera").AddComponent<Camera>();
            cam.orthographic = true;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(cam.gameObject);
        }

        [Test]
        public void BottomBand_IsDrawnBeforeTheMap()
        {
            RectTransform band = BulletTimeGaugeUI.BuildBottomBand(owner.transform, cam);
            Canvas canvas = band.GetComponentInParent<Canvas>();

            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera), "오버레이면 맵 위에 그려진다.");
            Assert.That(canvas.worldCamera, Is.SameAs(cam));
            Assert.That(canvas.sortingOrder, Is.EqualTo(UiLayer.BehindWorld), "맨 뒤 순서가 아니라 스프라이트를 가린다.");
            Assert.That(canvas.planeDistance, Is.LessThan(cam.farClipPlane), "먼 면 밖이라 아예 안 그려진다.");
        }

        [Test]
        public void BottomBand_SpansTheBottom_InTheBarColor()
        {
            RectTransform band = BulletTimeGaugeUI.BuildBottomBand(owner.transform, cam);

            Assert.That(band.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(band.anchorMax, Is.EqualTo(new Vector2(1f, 0f)), "띠가 화면 폭을 다 채우지 않는다.");
            Assert.That(band.sizeDelta.y, Is.EqualTo(HudLayout.BandHeight));
            Assert.That(band.GetComponent<Image>().color, Is.EqualTo(UiTheme.BarPlate), "런 바와 다른 색이다.");
        }

        [Test]
        public void NoCamera_NoBand()
        {
            // 오버레이로 대신 깔면 다시 맵을 가린다 — 없는 편이 낫다.
            Assert.That(BulletTimeGaugeUI.BuildBottomBand(owner.transform, null), Is.Null);
        }

        [Test]
        public void BandHeight_CoversTheWholeHud()
        {
            // 초상화 머리(삐져나온 부분)까지 띠 안이어야 경계에 배경이 한 줄 안 비친다.
            Assert.That(HudLayout.BandHeight, Is.GreaterThanOrEqualTo(HudLayout.MarginBottom + HudLayout.Height));
        }
    }
}
