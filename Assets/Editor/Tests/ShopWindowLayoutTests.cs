using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 상점 창은 화면을 덮지 않고 아래 대화창 위에 뜬다. 조작 설명은 대화창(<see cref="DialogueWindow.Hold"/>)이 맡는다.
    /// </summary>
    public class ShopWindowLayoutTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp() => root = new GameObject("Root", typeof(RectTransform));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void PanelAboveDialogue_SitsOnTopOfTheDialogueBox_WithoutABackdrop()
        {
            RectTransform panel = NodeWindows.PanelAboveDialogue(root.transform, "상점", new Vector2(1180f, 700f));

            Assert.That(panel.anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)), "아래 기준으로 서야 대화창 위에 얹힌다.");
            Assert.That(panel.anchoredPosition.y, Is.GreaterThanOrEqualTo(DialogueWindow.Top), "대화창과 겹친다.");
            Assert.That(root.transform.Find("Backdrop"), Is.Null, "상점 창이 화면 전체를 덮는다.");
        }

        [Test]
        public void ShopControls_NameTheInteractKey()
        {
            // 키를 못 찾으면 "?"가 들어간다 — 그래도 설명 줄 자체는 있어야 한다.
            Assert.That(ShopWindow.Controls(), Does.Contain("사기").And.Contain("ESC"));
        }
    }
}
