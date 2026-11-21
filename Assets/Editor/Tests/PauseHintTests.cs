using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.Tests
{
    /// <summary>
    /// 우측 상단 [ESC 일시정지] 안내 버튼. 전투에서만 뜨고(마을은 그 자리에 재화가 있다),
    /// 화면 오른쪽 위 모서리에 붙는다.
    /// </summary>
    public class PauseHintTests
    {
        private GameObject owner;

        [SetUp]
        public void SetUp() => owner = new GameObject("Owner");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(owner);

        [Test]
        public void WithHint_BuildsButtonAtTopRight()
        {
            PauseMenu menu = PauseMenu.Create(owner.transform, "마을로", null, showHint: true);

            Button hint = menu.Hint;
            Assert.That(hint, Is.Not.Null, "안내 버튼이 안 지어졌다.");

            var rect = (RectTransform)hint.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.one), "안내 버튼이 우측 상단에 붙지 않았다.");
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one), "안내 버튼이 우측 상단에 붙지 않았다.");
            Assert.That(rect.anchoredPosition.x, Is.LessThan(0f), "화면 오른쪽 밖으로 나갔다.");
            Assert.That(rect.anchoredPosition.y, Is.LessThan(0f), "화면 위쪽 밖으로 나갔다.");
        }

        [Test]
        public void WithHint_ShowsEscKey()
        {
            PauseMenu menu = PauseMenu.Create(owner.transform, "마을로", null, showHint: true);

            Transform key = menu.Hint.transform.Find("Keycap/Key");
            Assert.That(key, Is.Not.Null, "키캡이 없다.");
            Assert.That(key.GetComponent<Text>().text, Is.EqualTo("ESC"));
        }

        /// <summary>안내 캔버스가 클릭을 받아야 버튼이 눌린다 — 레이캐스터가 없으면 그냥 그림이다.</summary>
        [Test]
        public void WithHint_CanvasReceivesClicks()
        {
            PauseMenu menu = PauseMenu.Create(owner.transform, "마을로", null, showHint: true);

            Canvas canvas = menu.Hint.GetComponentInParent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.sortingOrder, Is.EqualTo(UiLayer.PauseHint));
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null, "안내 캔버스에 레이캐스터가 없다 — 클릭이 안 된다.");
        }

        /// <summary>마을은 우측 상단에 재화를 띄운다 — 기본값은 안내 없음이다.</summary>
        [Test]
        public void WithoutHint_BuildsNothing()
        {
            PauseMenu menu = PauseMenu.Create(owner.transform, "타이틀로", null);

            Assert.That(menu.Hint, Is.Null);
        }
    }
}
