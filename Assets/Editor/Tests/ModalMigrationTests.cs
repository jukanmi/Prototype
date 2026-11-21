using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Prototype.Tests
{
    /// <summary>
    /// 3단계 — 창들이 공용 골격(<see cref="UiKit.Modal"/>) 위에 서는가, [처음부터]가 일시정지 창 안으로 옮겨 갔는가.
    /// 근거는 <c>docs/UI_Unification_Plan.md</c> 5.3 · D5.
    /// </summary>
    public class ModalMigrationTests
    {
        private GameObject owner;
        private bool hadEventSystem;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Owner", typeof(RectTransform));
            hadEventSystem = Object.FindAnyObjectByType<EventSystem>() != null;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);

            // 창이 열린 채 끝나도 다음 테스트가 멈춘 시간 · 잠긴 조작을 물려받지 않게.
            Time.timeScale = 1f;
            PlayerInputController.SetSuspended(false);

            // 결과 창은 Awake 에서 이벤트 시스템을 보장한다 — 테스트가 흘린 것만 치운다.
            if (!hadEventSystem)
            {
                EventSystem es = Object.FindAnyObjectByType<EventSystem>();
                if (es != null) Object.DestroyImmediate(es.gameObject);
            }
        }

        private static Button FindButton(Component root, string name)
            => root.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);

        // ── 일시정지 ─────────────────────────────────────

        [Test]
        public void Pause_UsesModalSkeleton()
        {
            PauseMenu menu = PauseMenu.Create(owner.transform, "마을로", null);
            menu.Open();

            Transform panel = menu.transform.Find("Root/Backdrop/Panel");
            Assert.That(panel, Is.Not.Null, "일시정지 창이 공용 골격(바탕막 → 판)으로 안 지어졌다.");
            Assert.That(panel.Find("Title").GetComponent<Text>().text, Is.EqualTo("일시정지"));
            Assert.That(panel.Find("Buttons"), Is.Not.Null, "버튼 줄이 없다.");

            menu.Close();
        }

        [Test]
        public void Pause_ButtonRow_ResumeIsPrimary_ExitIsNormal()
        {
            PauseMenu menu = PauseMenu.Create(owner.transform, "마을로", null);
            menu.Open();

            Button resume = FindButton(menu, "Btn_계속");
            Button exit = FindButton(menu, "Btn_마을로");
            Assert.That(resume.GetComponent<Image>().color, Is.EqualTo(UiKit.ButtonFill(ButtonKind.Primary)));
            Assert.That(exit.GetComponent<Image>().color, Is.EqualTo(UiKit.ButtonFill(ButtonKind.Normal)));
            Assert.That(resume.transform.parent.name, Is.EqualTo("Buttons"));

            menu.Close();
        }

        [Test]
        public void Pause_WithoutRestart_HasNoRestartButton()
        {
            // 마을 — 지울 런이 없다.
            PauseMenu menu = PauseMenu.Create(owner.transform, "타이틀로", null);
            menu.Open();

            Assert.That(FindButton(menu, "Btn_" + PauseMenu.RestartLabel), Is.Null);

            menu.Close();
        }

        [Test]
        public void Pause_WithRestart_HasDangerRestartButton()
        {
            PauseMenu menu = PauseMenu.Create(owner.transform, "마을로", null, showHint: true, onRestart: () => { });
            menu.Open();

            Button restart = FindButton(menu, "Btn_" + PauseMenu.RestartLabel);
            Assert.That(restart, Is.Not.Null, "[처음부터]가 일시정지 창 안에 없다.");
            Assert.That(restart.GetComponent<Image>().color, Is.EqualTo(UiKit.ButtonFill(ButtonKind.Danger)));

            menu.Close();
        }

        [Test]
        public void Restart_AsksFirst_ThenCancelReturns()
        {
            int restarted = 0;
            PauseMenu menu = PauseMenu.Create(owner.transform, "마을로", null, onRestart: () => restarted++);
            menu.Open();

            FindButton(menu, "Btn_" + PauseMenu.RestartLabel).onClick.Invoke();
            Assert.That(menu.IsConfirming, Is.True, "한 번 누르면 바로 런이 지워진다 — 확인을 거쳐야 한다.");
            Assert.That(restarted, Is.Zero);

            FindButton(menu, "Btn_취소").onClick.Invoke();
            Assert.That(menu.IsConfirming, Is.False);
            Assert.That(menu.IsOpen, Is.True, "취소는 일시정지 창으로 한 겹만 물러나야 한다.");

            menu.Close();
        }

        [Test]
        public void Restart_Confirm_ClosesAndInvokes()
        {
            int restarted = 0;
            PauseMenu menu = PauseMenu.Create(owner.transform, "마을로", null, onRestart: () => restarted++);
            menu.Open();

            FindButton(menu, "Btn_" + PauseMenu.RestartLabel).onClick.Invoke();
            FindButton(menu, "Btn_재시작").onClick.Invoke();

            Assert.That(restarted, Is.EqualTo(1));
            Assert.That(menu.IsOpen, Is.False, "창이 열린 채 넘기면 timeScale 0 이 남아 전환 페이드가 멈춘다.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void Close_AlsoHidesConfirm()
        {
            PauseMenu menu = PauseMenu.Create(owner.transform, "마을로", null, onRestart: () => { });
            menu.Open();
            FindButton(menu, "Btn_" + PauseMenu.RestartLabel).onClick.Invoke();

            menu.Close();
            menu.Open();
            Assert.That(menu.IsConfirming, Is.False, "다시 열었더니 확인 창이 남아 있다.");

            menu.Close();
        }

        // ── 결과 ─────────────────────────────────────────

        [Test]
        public void Defeat_UsesModal_RetryDanger_MenuPrimary()
        {
            StageResultUI ui = StageResultUI.Create(owner.transform);
            ui.ShowDefeat(null, null);

            Transform panel = ui.transform.Find("Backdrop/Panel");
            Assert.That(panel, Is.Not.Null, "결과 창이 공용 골격으로 안 지어졌다.");

            Text title = panel.Find("Title").GetComponent<Text>();
            Assert.That(title.text, Is.EqualTo("패배"));
            Assert.That(title.color, Is.EqualTo(UiTheme.Hp));
            Assert.That(title.fontSize, Is.EqualTo(UiTheme.Size.Display));

            Assert.That(FindButton(ui, "Btn_Retry").GetComponent<Image>().color,
                        Is.EqualTo(UiKit.ButtonFill(ButtonKind.Danger)));
            Assert.That(FindButton(ui, "Btn_Menu").GetComponent<Image>().color,
                        Is.EqualTo(UiKit.ButtonFill(ButtonKind.Primary)));
        }

        [Test]
        public void AllClear_TitleIsGold()
        {
            StageResultUI ui = StageResultUI.Create(owner.transform);
            ui.ShowAllClear(null);

            Text title = ui.transform.Find("Backdrop/Panel/Title").GetComponent<Text>();
            Assert.That(title.color, Is.EqualTo(UiTheme.Gold));
            Assert.That(FindButton(ui, "Btn_Retry"), Is.Null, "다 깼는데 재시작 버튼이 있다.");
        }

        // ── 프리팹 창 입히기 ─────────────────────────────

        [Test]
        public void DressModal_PaintsPrefabWindowLikeModal()
        {
            // 프리팹 창 흉내 — 바탕막 · Panel · Title · 버튼 하나.
            Image dim = UiKit.NewImage(owner.transform, "Backdrop", Color.black);
            Image panel = UiKit.NewImage(dim.transform, "Panel", Color.gray);
            Text title = UiKit.NewText(panel.transform, "Title", 20, Color.white, FontStyle.Normal);
            var btnGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(panel.transform, false);

            UiKit.DressModal(dim.gameObject);

            Assert.That(dim.color, Is.EqualTo(UiTheme.Backdrop));
            Assert.That(panel.GetComponent<Outline>(), Is.Not.Null, "판에 테두리가 없다.");
            Assert.That(title.color, Is.EqualTo(UiTheme.Gold));
            Assert.That(title.fontSize, Is.EqualTo(UiTheme.Size.Title));
            Assert.That(btnGo.GetComponent<UiFocusRim>(), Is.Not.Null, "프리팹 버튼에 공통 호버가 없다.");
        }
    }
}
