using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// <c>CombatManager.prefab</c>의 <b>UI 배선</b>을 본다.
    ///
    /// 전투 UI를 코드 조립에서 프리팹으로 되돌리면서 <c>Build*</c> 메서드가 사라졌다.
    /// 그전에는 참조가 코드에 있어 컴파일러가 지켜 줬지만, 이제는 인스펙터 칸이 비면
    /// <b>런타임에 화면이 조용히 안 뜬다</b> — 컴파일도 통과하고 예외도 안 난다.
    /// 배선이 끊기는 걸 잡는 건 여기뿐이다.
    ///
    /// 프리팹을 손으로 고치다 참조를 놓치는 순간이 이 테스트가 노리는 자리다.
    /// </summary>
    public class CombatManagerPrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/CombatManager.prefab";

        private static GameObject Prefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(go, Is.Not.Null, $"{PrefabPath} 를 못 찾았다.");
            return go;
        }

        /// <summary>
        /// 컴포넌트의 직렬화 필드가 비어 있지 않은지 본다.
        ///
        /// 루트와 자식을 모두 뒤진다 — UI마다 붙는 자리가 다르다.
        /// 이웃(<c>BulletTimeController</c>)을 <c>GetComponent</c>로 잡는 것은 루트에,
        /// 테스트가 서브트리만 떠 오는 것(<see cref="RecentHitEnemyHUD"/>)은 캔버스에 붙는다.
        /// </summary>
        private static void AssertWired<T>(params string[] fields) where T : Component
        {
            var target = Prefab().GetComponentInChildren<T>(true);
            Assert.That(target, Is.Not.Null, $"{typeof(T).Name} 이 CombatManager 어디에도 없다.");

            var so = new SerializedObject(target);

            foreach (string field in fields)
            {
                SerializedProperty p = so.FindProperty(field);
                Assert.That(p, Is.Not.Null,
                    $"{typeof(T).Name}.{field} 필드가 없다 — 이름이 바뀌었으면 이 테스트도 같이 고칠 것.");

                Assert.That(p.objectReferenceValue, Is.Not.Null,
                    $"{typeof(T).Name}.{field} 배선이 비었다 — 그 화면이 런타임에 조용히 안 뜬다.");
            }
        }

        [Test]
        public void ComboDamageHUD_IsWired()
            => AssertWired<ComboDamageHUD>("panel", "group", "hitsLabel", "damageLabel", "detailLabel");

        [Test]
        public void RebindUI_IsWired()
            => AssertWired<RebindUI>(
                "_openButton", "_panelRoot", "_content", "_statusText",
                "_listeningRoot", "_footerRow", "_resetButton", "_closeButton");

        [Test]
        public void SkillCutinUI_IsWired()
            => AssertWired<SkillCutinUI>(
                "panel", "portraitRect", "portraitImage", "nameLabel", "labelRect", "skillLabel");

        [Test]
        public void RecentHitEnemyHUD_IsWired()
            => AssertWired<RecentHitEnemyHUD>("panel", "nameLabel", "healthFill", "healthTrail");

        /// <summary>
        /// 적 막대 원본은 꺼진 채로 둔다. 켜 두면 아무 적도 안 가리키는 막대가
        /// 화면 가운데 하나 떠 있다(Awake가 끄긴 하지만, 그 전 한 프레임에 보인다).
        /// </summary>
        [Test]
        public void RecentHitEnemyTemplate_StartsHidden()
        {
            var hud = Prefab().GetComponentInChildren<RecentHitEnemyHUD>(true);
            var panel = (GameObject)new SerializedObject(hud).FindProperty("panel").objectReferenceValue;

            Assert.That(panel.activeSelf, Is.False, "적 체력 막대 원본이 켜진 채로 저장됐다.");
        }

        [Test]
        public void BulletTimeGaugeUI_IsWired()
            => AssertWired<BulletTimeGaugeUI>("_canvas");

        /// <summary>
        /// 켜고 끄는 대상은 <b>꺼진 채로</b> 저장돼야 한다. 켜 두면 전투 시작과 동시에
        /// 설정 창이 화면을 덮은 채로 판이 시작된다.
        /// </summary>
        [TestCase("RebindCanvas/Backdrop")]
        [TestCase("RebindCanvas/Backdrop/Panel/Listening")]
        public void ModalRoots_StartHidden(string path)
        {
            Transform t = Prefab().transform.Find(path);
            Assert.That(t, Is.Not.Null, $"{path} 가 없다.");

            Assert.That(t.gameObject.activeSelf, Is.False,
                $"{path} 가 켜진 채로 저장됐다 — 판이 시작되자마자 화면을 덮는다.");
        }

        /// <summary>
        /// 클릭을 안 받는 HUD에 <c>GraphicRaycaster</c>가 붙으면 그 패널이
        /// 아래층의 클릭을 통째로 가로챈다. 증상이 UI가 아니라 조작에서 나온다.
        /// </summary>
        [TestCase("ComboDamageCanvas")]
        [TestCase("SkillCutinCanvas")]
        [TestCase("RecentHitEnemyCanvas")]
        [TestCase("SkillLogCanvas")]
        public void PassiveHuds_HaveNoRaycaster(string child)
        {
            Transform t = Prefab().transform.Find(child);
            Assert.That(t, Is.Not.Null, $"{child} 가 없다.");

            Assert.That(t.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Null,
                $"{child} 에 GraphicRaycaster 가 붙었다 — 아래층 클릭을 가로챈다.");
        }

        /// <summary>
        /// 캔버스 겹침 순서는 <see cref="UiLayer"/>가 유일한 원본이다.
        /// 프리팹에서 손으로 바꿔 놓으면 표와 화면이 갈린다.
        /// </summary>
        [TestCase("GaugeCanvas", UiLayer.Gauge)]
        [TestCase("RecentHitEnemyCanvas", UiLayer.RecentHitEnemy)]
        [TestCase("ComboDamageCanvas", UiLayer.ComboDamage)]
        [TestCase("SkillCutinCanvas", UiLayer.SkillCutin)]
        [TestCase("RebindCanvas", UiLayer.Rebind)]
        [TestCase("SkillLogCanvas", UiLayer.SkillLog)]
        public void CanvasOrder_MatchesUiLayer(string child, int expected)
        {
            Transform t = Prefab().transform.Find(child);
            Assert.That(t, Is.Not.Null, $"{child} 가 없다.");

            var canvas = t.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null, $"{child} 에 Canvas 가 없다.");

            Assert.That(canvas.sortingOrder, Is.EqualTo(expected),
                $"{child} 의 정렬 순서가 UiLayer 표와 다르다.");
        }

        /// <summary>
        /// 스킬 로그는 36줄까지 내려와 좌하단 묶음(초상화 · 체력 · 총공격 게이지)과 겹칠 수 있다.
        /// 그때 묶음이 위에 그려져야 한다 — 로그가 그 두 캔버스보다 아래층이어야 한다.
        /// </summary>
        [Test]
        public void SkillLog_IsBelowBottomLeftHud()
        {
            Assert.That(UiLayer.SkillLog, Is.LessThan(UiLayer.Gauge), "총공격 게이지가 로그 위에 그려져야 한다.");
            Assert.That(UiLayer.SkillLog, Is.LessThan(UiLayer.PartyHealth), "초상화 · 체력이 로그 위에 그려져야 한다.");
            Assert.That(UiLayer.SkillLog, Is.GreaterThan(UiLayer.BehindWorld), "월드보다 먼저 그리면 로그가 안 보인다.");
        }

        /// <summary>스킬 로그 캔버스는 좌하단 묶음과 같은 스케일러(1920×1080, 세로 기준)여야 자리 계산이 맞는다.</summary>
        [Test]
        public void SkillLogCanvas_ScalesByHeight()
        {
            Transform t = Prefab().transform.Find("SkillLogCanvas");
            Assert.That(t, Is.Not.Null);
            var scaler = t.GetComponent<UnityEngine.UI.CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(1f));
            Assert.That(t.GetComponent<SkillLogHUD>(), Is.Not.Null);
        }
    }
}
