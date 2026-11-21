using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.Tests
{
    /// <summary>
    /// 5단계 — 전투 HUD가 규격 색을 입는가(기획서 5.2). 프리팹은 자리 · 크기를 쥐고, 색은 코드가 입힌다.
    /// 프리팹 원본을 건드리지 않게 복제본에서 본다.
    /// </summary>
    public class BattleHudThemeTests
    {
        private GameObject instance;

        [TearDown]
        public void TearDown()
        {
            if (instance != null) Object.DestroyImmediate(instance);
        }

        private T Spawn<T>(string prefabPath, string child) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, $"{prefabPath} 를 못 찾았다.");

            // 이웃 컴포넌트의 Awake 가 에디트모드에서 돌지 않게 필요한 자식만 떠 온다.
            Transform source = child != null ? prefab.transform.Find(child) : prefab.transform;
            Assert.That(source, Is.Not.Null, $"{prefabPath} 에 {child} 가 없다.");

            instance = Object.Instantiate(source.gameObject);
            T c = instance.GetComponentInChildren<T>(true);
            Assert.That(c, Is.Not.Null, $"{typeof(T).Name} 이 없다.");
            return c;
        }

        private static Text FindText(Component root, string name)
        {
            foreach (Text t in root.GetComponentsInChildren<Text>(true))
                if (t.name == name) return t;
            return null;
        }

        [Test]
        public void Combo_HitsGold_DamageMain()
        {
            ComboDamageHUD hud = Spawn<ComboDamageHUD>("Assets/Prefabs/CombatManager.prefab", "ComboDamageCanvas");
            hud.ApplyTheme();

            Assert.That(FindText(hud, "Hits").color, Is.EqualTo(UiTheme.Gold), "큰 타수는 금색이다.");
            Assert.That(FindText(hud, "Damage").color, Is.EqualTo(UiTheme.TextMain));
            Text suffix = FindText(hud, "HitsSuffix");
            if (suffix != null) Assert.That(suffix.color, Is.EqualTo(UiTheme.Gold), "접미사가 타수와 다른 색이다.");
        }

        /// <summary>체력바 테두리 · 홈은 밑의 총공격 게이지와 같은 색이다 — 한 묶음으로 읽혀야 한다.</summary>
        [Test]
        public void PartyHealth_BarColorsMatchGauge()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/BattleInput.prefab");
            Assert.That(prefab, Is.Not.Null);
            PartyHealthHUD source = prefab.GetComponentInChildren<PartyHealthHUD>(true);
            Assert.That(source, Is.Not.Null, "BattleInput 에 PartyHealthHUD 가 없다.");

            instance = Object.Instantiate(source.gameObject);
            PartyHealthHUD.ApplyTheme(instance.transform);

            bool sawBorder = false, sawTrack = false;
            foreach (Graphic g in instance.GetComponentsInChildren<Graphic>(true))
            {
                if (g.name == "BarBack")
                {
                    sawBorder = true;
                    Assert.That(g.color, Is.EqualTo(PartyHealthHUD.BarBorderColor), "체력바 테두리 색이 게이지와 다르다.");
                }
                if (g.name == "Track")
                {
                    sawTrack = true;
                    Assert.That(g.color, Is.EqualTo(UiTheme.InkDeep), "체력바 홈 색이 게이지와 다르다.");
                }
            }
            Assert.That(sawBorder && sawTrack, Is.True, "체력바 테두리(BarBack) · 홈(Track)을 못 찾았다 — 프리팹 계층이 바뀌었다.");
        }

        [Test]
        public void RunBar_AssaultHint_SitsBottomRight_AndRebuildsOnKeyChange()
        {
            var owner = new GameObject("Owner", typeof(RectTransform));
            instance = owner;
            RunBarUI bar = RunBarUI.Create(owner.transform, RunBarUI.Mode.Battle);

            bar.SetAssaultHint("E");
            RectTransform first = bar.AssaultHint;
            Assert.That(first, Is.Not.Null);
            Assert.That(first.anchorMin, Is.EqualTo(new Vector2(1f, 0f)), "행동 안내는 오른아래다.");
            Assert.That(first.Find("Keycap/Key").GetComponent<Text>().text, Is.EqualTo("E"));

            bar.SetAssaultHint("E");
            Assert.That(bar.AssaultHint, Is.SameAs(first), "같은 키인데 다시 지었다.");

            bar.SetAssaultHint("Q");
            Assert.That(bar.AssaultHint.Find("Keycap/Key").GetComponent<Text>().text, Is.EqualTo("Q"));
        }
    }
}
