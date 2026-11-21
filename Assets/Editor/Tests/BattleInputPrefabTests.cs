using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Prototype.EditorTools;

namespace Prototype.Tests
{
    /// <summary>
    /// 입력 배선을 본다. 손으로 붙이는 부분이라 컴파일러도 다른 테스트도 안 잡아 준다 —
    /// 틀리면 씬을 켜 봐야 안다.
    ///
    /// 예전에는 이 배선이 Player 프리팹에 있었다. 몸은 <c>SetActive(false)</c>로
    /// 꺼질 수 있어서 거기 있으면 안 된다 — 몸이 꺼지는 순간 키까지 함께 죽는다.
    /// </summary>
    public class BattleInputPrefabTests
    {
        // 경로가 아니라 컴포넌트로 찾는다 — 프리팹을 옮겨도 안 끊긴다.
        private static string HostPath => PrefabLocator.BattleInputPath;
        private static string PlayerPath => PrefabLocator.PlayerPath;
        private const string ActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

        private static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(go, Is.Not.Null,
                $"{path} 를 못 찾았다.");
            return go;
        }

        // ── 파티 체력 HUD ───────────────────────────────────

        /// <summary>
        /// 줄은 <c>RowTemplate</c>을 복제해 만들고 조각을 <b>이름으로</b> 찾는다
        /// (<c>PartyHealthHUD.NewRow</c>). 이름이 어긋나면 복제한 줄에서
        /// <c>NullReferenceException</c>이 나는데, 그때는 이미 씬을 켠 뒤다.
        /// </summary>
        [Test]
        public void PartyHealthHUD_IsWired()
        {
            var hud = Load(HostPath).GetComponentInChildren<PartyHealthHUD>(true);
            Assert.That(hud, Is.Not.Null, "BattleInput 에 PartyHealthHUD 가 없다.");

            var so = new SerializedObject(hud);

            foreach (string field in new[] { "panel", "rowTemplate" })
                Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null,
                    $"PartyHealthHUD.{field} 배선이 비었다 — 파티 체력 줄이 안 뜬다.");
        }

        [TestCaseSource(typeof(PartyHealthHUD), nameof(PartyHealthHUD.PartPaths))]
        public void PartyHealthRowTemplate_HasEveryPart(string path)
        {
            var hud = Load(HostPath).GetComponentInChildren<PartyHealthHUD>(true);
            var template = (RectTransform)new SerializedObject(hud)
                .FindProperty("rowTemplate").objectReferenceValue;

            Assert.That(template, Is.Not.Null, "rowTemplate 이 비었다.");
            Assert.That(template.Find(path), Is.Not.Null,
                $"RowTemplate/{path} 가 없다 — NewRow 가 이 이름으로 찾는다.");
        }

        /// <summary>좌측 하단 HUD다 — 패널이 왼쪽 아래 모서리에 붙어 있어야 한다.</summary>
        [Test]
        public void PartyHealthPanel_AnchorsBottomLeft()
        {
            var hud = Load(HostPath).GetComponentInChildren<PartyHealthHUD>(true);
            var panel = (RectTransform)new SerializedObject(hud)
                .FindProperty("panel").objectReferenceValue;

            Assert.That(panel.anchorMin, Is.EqualTo(Vector2.zero), "패널 앵커가 좌하단이 아니다.");
            Assert.That(panel.anchorMax, Is.EqualTo(Vector2.zero), "패널 앵커가 좌하단이 아니다.");
        }

        /// <summary>
        /// 초상화 옆 체력바 바로 밑에 총공격 게이지가 붙는다. 게이지는 코드가 <see cref="HudLayout"/>대로 앉히고,
        /// 체력 줄은 프리팹 값이라 — 둘이 갈리면 게이지가 체력바와 어긋나거나 판 사이에 틈이 벌어진다.
        /// </summary>
        [Test]
        public void PartyHealthPanel_SharesHudOrigin()
        {
            var hud = Load(HostPath).GetComponentInChildren<PartyHealthHUD>(true);
            var panel = (RectTransform)new SerializedObject(hud)
                .FindProperty("panel").objectReferenceValue;

            Assert.That(panel.pivot, Is.EqualTo(Vector2.zero), "패널 피벗이 좌하단이 아니다.");
            Assert.That(panel.anchoredPosition,
                Is.EqualTo(new Vector2(HudLayout.MarginX, HudLayout.MarginBottom)),
                "체력 패널 원점이 HudLayout 과 다르다 — 게이지와 어긋난다.");
        }

        /// <summary>
        /// 체력바는 HudLayout 자리에 선다. 게이지는 코드가 같은 표로 앉히므로,
        /// 여기가 어긋나면 두 막대의 왼쪽 끝이 갈리거나 맞닿는 테두리가 벌어진다.
        /// </summary>
        [Test]
        public void PartyHealthBar_MatchesHudLayout()
        {
            var bar = (RectTransform)Template().Find(PartyHealthHUD.PathBarBack);

            Assert.That(bar.anchoredPosition.x, Is.EqualTo(HudLayout.BarX).Within(0.01f), "체력바 x 가 게이지와 다르다.");
            Assert.That(bar.anchoredPosition.y, Is.EqualTo(HudLayout.HpY).Within(0.01f), "체력바 높이가 표와 다르다.");
            Assert.That(bar.sizeDelta, Is.EqualTo(new Vector2(HudLayout.HpWidth, HudLayout.HpHeight)),
                "체력바 크기가 표와 다르다.");
        }

        /// <summary>
        /// 게이지는 체력바 밑에 <b>테두리를 맞대고</b> 붙는다 — 사이가 뜨면 따로 노는 두 막대로 읽힌다.
        /// 게이지 윗변이 체력 테두리 두께만큼 겹쳐 올라가야 검은 줄이 한 겹이다.
        /// </summary>
        [Test]
        public void Gauge_TouchesHealthBarFromBelow()
        {
            float gaugeTop = HudLayout.GaugeY + HudLayout.GaugeHeight;

            Assert.That(gaugeTop, Is.EqualTo(HudLayout.HpY + HudLayout.Border).Within(0.01f),
                "게이지 윗변이 체력바 테두리와 안 맞는다 — 틈이 나거나 두 줄로 겹친다.");
            Assert.That(HudLayout.GaugeHeight, Is.LessThan(HudLayout.HpHeight), "게이지가 체력바보다 얇아야 한다.");
            Assert.That(HudLayout.GaugeWidth, Is.LessThan(HudLayout.HpWidth), "게이지가 체력바보다 짧아야 계단이 된다.");
        }

        /// <summary>막대 조각은 전부 같은 각으로 꺾인 SlantedBar 다 — 하나라도 사각 Image 면 끝이 어긋난다.</summary>
        [TestCase(PartyHealthHUD.PathBarBack)]
        [TestCase(PartyHealthHUD.PathTrack)]
        [TestCase(PartyHealthHUD.PathTrail)]
        [TestCase(PartyHealthHUD.PathFill)]
        public void PartyHealthBarParts_AreSlanted(string path)
        {
            var bar = Template().Find(path).GetComponent<SlantedBar>();
            Assert.That(bar, Is.Not.Null, $"{path} 가 SlantedBar 가 아니다.");
            Assert.That(bar.SlantRatio, Is.EqualTo(HudLayout.SlantRatio).Within(0.001f), $"{path} 의 꺾임 각이 다르다.");
        }

        /// <summary>이름 · 바탕판은 뺐다. 남아 있으면 초상화 옆에 빈 판이나 글자가 다시 뜬다.</summary>
        [TestCase("Name")]
        [TestCase("Plate")]
        public void PartyHealthRow_HasNo(string child)
        {
            Assert.That(Template().Find(child), Is.Null, $"RowTemplate/{child} 가 남아 있다.");
        }

        /// <summary>체력바 왼쪽 끝은 초상화 틀 오른쪽 변에 딱 붙는다 — 틈도 겹침도 없다.</summary>
        [Test]
        public void PartyHealthBar_TouchesFrameRightEdge()
        {
            var frame = (RectTransform)Template().Find(PartyHealthHUD.PathFrame);
            var bar = (RectTransform)Template().Find(PartyHealthHUD.PathBarBack);

            float frameRight = frame.anchoredPosition.x + frame.sizeDelta.x;
            Assert.That(frame.sizeDelta, Is.EqualTo(new Vector2(HudLayout.FrameSize, HudLayout.FrameSize)), "틀 크기가 표와 다르다.");
            Assert.That(bar.anchoredPosition.x, Is.EqualTo(frameRight).Within(0.01f), "체력바가 틀에 딱 붙지 않았다.");
        }

        /// <summary>
        /// 얼굴은 틀 위로 삐져나오고 아래는 틀 안에서 잘린다 — 자르는 상자(RectMask2D)가
        /// 위로만 <see cref="HudLayout.PortraitOverflow"/>만큼 열려 있어야 한다.
        /// </summary>
        [Test]
        public void Portrait_PopsOutAboveFrame()
        {
            var clip = (RectTransform)Template().Find(PartyHealthHUD.PathClip);
            Assert.That(clip.GetComponent<UnityEngine.UI.RectMask2D>(), Is.Not.Null, "얼굴을 자르는 RectMask2D 가 없다.");

            Assert.That(clip.offsetMax.y, Is.EqualTo(HudLayout.PortraitOverflow).Within(0.01f), "위쪽이 틀 밖으로 열려 있지 않다.");
            Assert.That(clip.offsetMin.y, Is.EqualTo(HudLayout.FrameBorder).Within(0.01f), "아래쪽이 틀 안쪽에서 잘리지 않는다.");

            var face = (RectTransform)Template().Find(PartyHealthHUD.PathFace);
            Assert.That(face.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(face.anchorMax, Is.EqualTo(Vector2.one), "얼굴이 자르는 상자를 꽉 채우지 않는다.");
        }

        /// <summary>틀이 줄의 맨 앞이어야 삐져나온 머리가 체력바 테두리에 가리지 않는다.</summary>
        [Test]
        public void Frame_IsDrawnLast()
        {
            RectTransform row = Template();
            Assert.That(row.Find(PartyHealthHUD.PathFrame).GetSiblingIndex(), Is.EqualTo(row.childCount - 1));
        }

        /// <summary>체력바는 단색이다 — 채움 위에 하이라이트 같은 덧칠 조각이 없어야 한다.</summary>
        [Test]
        public void PartyHealthFill_IsSolid()
        {
            Transform fill = Template().Find(PartyHealthHUD.PathFill);
            Assert.That(fill.childCount, Is.Zero, "체력바 채움 위에 덧칠 조각이 남아 있다 — 단색이 아니다.");
        }

        private static RectTransform Template()
        {
            var hud = Load(HostPath).GetComponentInChildren<PartyHealthHUD>(true);
            var template = (RectTransform)new SerializedObject(hud).FindProperty("rowTemplate").objectReferenceValue;
            Assert.That(template, Is.Not.Null, "rowTemplate 이 비었다.");
            return template;
        }

        /// <summary>원본은 꺼진 채로 둔다. 켜 두면 아무도 없는 빈 줄이 하나 남는다.</summary>
        [Test]
        public void PartyHealthRowTemplate_StartsHidden()
        {
            var hud = Load(HostPath).GetComponentInChildren<PartyHealthHUD>(true);
            var template = (RectTransform)new SerializedObject(hud)
                .FindProperty("rowTemplate").objectReferenceValue;

            Assert.That(template.gameObject.activeSelf, Is.False,
                "RowTemplate 이 켜진 채로 저장됐다 — 빈 줄이 하나 남는다.");
        }

        // ── 입력 호스트 ─────────────────────────────────────

        [Test]
        public void Host_HasPlayerInput()
        {
            // 자식에 붙이면 PlayerInputController 가 같은 오브젝트에서
            // PlayerInput 을 못 찾아 Awake 에서 죽는다.
            Assert.That(Load(HostPath).GetComponent<PlayerInput>(), Is.Not.Null,
                "입력 호스트 최상단에 PlayerInput 이 없다.");
        }

        [Test]
        public void Host_HasPlayerInputController()
        {
            Assert.That(Load(HostPath).GetComponent<PlayerInputController>(), Is.Not.Null,
                "입력 호스트에 PlayerInputController 가 없다.");
        }

        [Test]
        public void Host_HasInputMapSwitcher()
        {
            // 없으면 조준 맵이 켜진 채로 굳어 이동과 조준이 같은 WASD를 계속 물고 있다.
            Assert.That(Load(HostPath).GetComponent<InputMapSwitcher>(), Is.Not.Null,
                "입력 호스트에 InputMapSwitcher 가 없다.");
        }

        [Test]
        public void Host_HasCommanderAndRoster()
        {
            GameObject host = Load(HostPath);

            // 지휘키(E)를 읽는 주체. 몸에 두면 몸이 꺼질 때 함께 죽는다.
            Assert.That(host.GetComponent<BattleCommander>(), Is.Not.Null,
                "입력 호스트에 BattleCommander 가 없다 — E 가 아무 데서도 안 읽힌다.");

            Assert.That(host.GetComponent<PartyRoster>(), Is.Not.Null,
                "입력 호스트에 PartyRoster 가 없다 — 주인공이 시작 자리에 서지 않는다.");
        }

        [Test]
        public void PlayerInput_UsesProjectActionsAsset()
        {
            var playerInput = Load(HostPath).GetComponent<PlayerInput>();
            Assert.That(playerInput, Is.Not.Null, "PlayerInput 이 없다.");
            Assert.That(playerInput.actions, Is.Not.Null, "PlayerInput 의 Actions 칸이 비어 있다.");

            var expected = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            Assert.That(playerInput.actions, Is.SameAs(expected),
                $"PlayerInput 이 {ActionsPath} 가 아닌 다른 자산을 물고 있다.");
        }

        [Test]
        public void PlayerInput_DefaultMapIsGameplay()
        {
            var playerInput = Load(HostPath).GetComponent<PlayerInput>();

            // 비어 있으면 아무 맵도 자동으로 안 켜진다. PlayerInputController 가
            // OnEnable 에서 직접 켜기는 하지만, 그 전에 도는 코드가 입력을 못 읽는다.
            Assert.That(playerInput.defaultActionMap, Is.EqualTo(InputActionNames.Gameplay.Map),
                "Default Map 이 Gameplay 가 아니다.");
        }

        [Test]
        public void PlayerInput_UsesCSharpEvents()
        {
            var playerInput = Load(HostPath).GetComponent<PlayerInput>();

            // 폴링으로 읽으므로 콜백은 아무도 안 받는다. SendMessage 로 두면
            // 매 입력마다 리플렉션만 돌고 얻는 게 없다.
            Assert.That(playerInput.notificationBehavior,
                Is.EqualTo(PlayerNotifications.InvokeCSharpEvents),
                "Behavior 가 Invoke C Sharp Events 가 아니다.");
        }

        // ── 몸에는 입력이 없어야 한다 ───────────────────────

        [Test]
        public void PlayerBody_HasNoInputComponents()
        {
            GameObject body = Load(PlayerPath);

            Assert.That(body.GetComponent<PlayerInput>(), Is.Null,
                "Player 프리팹에 PlayerInput 이 남아 있다 — 몸이 꺼지면 입력이 통째로 죽는다.");
            Assert.That(body.GetComponent<PlayerInputController>(), Is.Null,
                "Player 프리팹에 PlayerInputController 가 남아 있다.");
            Assert.That(body.GetComponent<InputMapSwitcher>(), Is.Null,
                "Player 프리팹에 InputMapSwitcher 가 남아 있다.");
        }

        /// <summary>
        /// 몸마다 컨트롤을 붙이던 시절의 잔해가 남아 있으면 안 된다. 몸에 조종사가 붙어 있던 시절에는
        /// 프리팹과 씬이 조용히 어긋났다 — 어떤 몸에는 붙고 어떤 몸에는 안 붙어도
        /// 게임이 그냥 돌아가 버렸다.
        /// </summary>
        [Test]
        public void Bodies_CarryNoDriverExceptEnemyAi()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponent<Entity>() == null) continue;

                foreach (Control c in go.GetComponents<Control>())
                    Assert.That(c, Is.TypeOf<EnemyControl>(),
                        $"{path} 에 {c.GetType().Name} 이 붙어 있다 — 몸에 붙는 드라이버는 EnemyControl 뿐이다.");
            }
        }
    }
}
