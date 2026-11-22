using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.Tests
{
    /// <summary>
    /// 4단계 — 런 바(<see cref="RunBarUI"/>). 지도 · 전투 · 칸이 같은 띠를 쓰고, 전투만 체력을 숨긴다(D2).
    /// 근거는 <c>docs/UI_Unification_Plan.md</c> 5.1.
    /// </summary>
    public class RunBarTests
    {
        private GameObject owner;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Owner", typeof(RectTransform));
            RunProgression.Current.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
            RunProgression.Current.Reset();
        }

        // ── 층 순서 ──────────────────────────────────────

        [Test]
        public void Layers_BattleBarSitsInHudFloor()
        {
            Assert.That(UiLayer.RunBar, Is.GreaterThan(UiLayer.ComboDamage));
            Assert.That(UiLayer.RunBar, Is.LessThan(UiLayer.SkillCutin), "컷인이 런 바 밑에 깔린다.");
            Assert.That(UiLayer.RunBar, Is.LessThan(UiLayer.Pause));
        }

        [Test]
        public void Layers_OverlayBarSitsAboveNodeWindow_BelowModals()
        {
            Assert.That(UiLayer.RunBarOverWindow, Is.GreaterThan(UiLayer.NodeWindow), "지도 · 칸 창이 런 바를 덮는다.");
            Assert.That(UiLayer.RunBarOverWindow, Is.LessThan(UiLayer.Pause));
            Assert.That(UiLayer.RunBarOverWindow, Is.LessThan(UiLayer.StageResult));
        }

        [TestCase(RunBarUI.Mode.Battle, UiLayer.RunBar)]
        [TestCase(RunBarUI.Mode.Overlay, UiLayer.RunBarOverWindow)]
        public void Canvas_UsesModeLayer(RunBarUI.Mode mode, int expected)
        {
            RunBarUI bar = RunBarUI.Create(owner.transform, mode);
            Assert.That(bar.GetComponent<Canvas>().sortingOrder, Is.EqualTo(expected));
        }

        // ── 모양 ─────────────────────────────────────────

        [Test]
        public void Plate_SpansTopEdge_AtFixedHeight()
        {
            RunBarUI bar = RunBarUI.Create(owner.transform, RunBarUI.Mode.Overlay);
            var plate = (RectTransform)bar.transform.Find("Plate");

            Assert.That(plate.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(plate.anchorMax, Is.EqualTo(new Vector2(1f, 1f)), "띠가 화면 폭을 다 채우지 않는다.");
            Assert.That(plate.sizeDelta.y, Is.EqualTo(RunBarUI.Height));
        }

        [Test]
        public void Battle_HidesHp()
        {
            RunBarUI bar = RunBarUI.Create(owner.transform, RunBarUI.Mode.Battle);

            Assert.That(bar.HpVisible, Is.False, "전투 중 체력은 좌하단 HUD가 주인이다(D2).");
            Assert.That(bar.SlotButtons, Has.Count.EqualTo(RunProgression.AssaultSlotCount));
        }

        [Test]
        public void Overlay_ShowsHp()
        {
            RunBarUI bar = RunBarUI.Create(owner.transform, RunBarUI.Mode.Overlay);

            Assert.That(bar.HpVisible, Is.True);
        }

        // ── 총공격 슬롯 — 편성 문을 따른다 ────────────────
        // 슬롯은 편집창을 여는 버튼이라 E키와 같은 문(AssaultEditorUI.CanOpen)을 지난다 — 모드가 아니라 칸이 정한다.

        [TestCase(RunBarUI.Mode.Battle)]
        [TestCase(RunBarUI.Mode.Overlay)]
        public void Slots_AreReadOnly_WithoutANode(RunBarUI.Mode mode)
        {
            // 편성 문을 거는 칸 지휘자가 없다 — 지도 위와 같다.
            Assume.That(AssaultEditGate.CanEdit, Is.False, "앞 테스트의 편성 문이 남아 있다.");

            RunBarUI bar = RunBarUI.Create(owner.transform, mode);

            foreach (Button b in bar.SlotButtons)
                Assert.That(b.interactable, Is.False, "지도에서 슬롯이 눌린다 — 편성은 칸 안에서만 한다.");
        }

        [TestCase(RunBarUI.Mode.Battle)]
        [TestCase(RunBarUI.Mode.Overlay)]
        public void Slots_FollowTheNodesEditGate(RunBarUI.Mode mode)
        {
            var gate = new FakeAssaultEditGate { Editable = false };
            AssaultEditGate.Register(gate);
            try
            {
                RunBarUI bar = RunBarUI.Create(owner.transform, mode);
                foreach (Button b in bar.SlotButtons)
                    Assert.That(b.interactable, Is.False, "싸우는 중인데 슬롯이 눌린다.");

                gate.Editable = true;
                bar.Refresh();
                foreach (Button b in bar.SlotButtons)
                    Assert.That(b.interactable, Is.True, "싸움이 끝났는데 슬롯이 안 눌린다.");

                gate.Editable = false;
                bar.Refresh();
                foreach (Button b in bar.SlotButtons)
                    Assert.That(b.interactable, Is.False, "다시 싸움이 났는데 슬롯이 그대로 눌린다.");
            }
            finally
            {
                AssaultEditGate.Unregister(gate);
            }
        }

        // ── 값 ───────────────────────────────────────────

        [Test]
        public void Refresh_ReadsGold_AndShowsSkillListButton()
        {
            RunBarUI bar = RunBarUI.Create(owner.transform, RunBarUI.Mode.Overlay);

            RunProgression.Current.AddGold(37);
            bar.Refresh();

            Assert.That(bar.Gold.text, Is.EqualTo("37"));
            Assert.That(bar.SkillListLabel.text, Is.EqualTo(RunBarUI.SkillListText), "예전 \"덱 N\" 자리는 [스킬 목록] 버튼이다.");
            Assert.That(bar.SkillList, Is.Not.Null);
            Assert.That(bar.SkillList.interactable, Is.True, "지도 · 칸에서도 스킬 목록은 열린다.");
            Assert.That(bar.Hp.text, Does.EndWith("%"));
        }

        [Test]
        public void Floor_IsBlankOutsideARun()
        {
            // 에디트 모드엔 GameManager 가 없다 — 씬 단독 실행과 같다.
            Assert.That(RunBarUI.FloorLabel(), Is.Empty);
        }

        // ── [ESC] 설정 ───────────────────────────────────

        [Test]
        public void Settings_OnlyWhenSceneHasPauseMenu()
        {
            Assume.That(PauseMenu.Current == null, "앞 테스트의 일시정지 창이 남아 있다.");
            RunBarUI without = RunBarUI.Create(owner.transform, RunBarUI.Mode.Overlay);
            Assert.That(without.Settings, Is.Null, "일시정지 창이 없는 씬(지도 · 칸)에 누를 곳 없는 [ESC]가 섰다.");

            var host = new GameObject("Host", typeof(RectTransform));
            host.transform.SetParent(owner.transform, false);
            PauseMenu.Create(host.transform, "마을로", null);

            RunBarUI with = RunBarUI.Create(host.transform, RunBarUI.Mode.Battle);
            Assert.That(with.Settings, Is.Not.Null, "일시정지 창이 있는데 [ESC] 설정이 없다.");
            Assert.That(with.Settings.GetComponentInChildren<Text>().text, Is.EqualTo("ESC"));
        }
    }
}
