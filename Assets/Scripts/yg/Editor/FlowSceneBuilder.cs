using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Prototype.YG.EditorTools
{
    /// <summary>
    /// 메인화면 ↔ 배틀 씬 전환 구조를 한 번에 조립한다.
    /// 메뉴: Prototype ▸ YG ▸ 메인화면 흐름 씬 만들기
    ///
    /// 만드는 것 — Boot.unity, MainMenu.unity, SampleScene 에 BattleSceneController 추가,
    /// Build Settings 등록(Boot 0 / MainMenu 1 / SampleScene 2).
    /// SampleScene 의 기존 오브젝트 배치는 건드리지 않는다.
    /// </summary>
    public static class FlowSceneBuilder
    {
        private const string SceneDir  = "Assets/Scenes";
        private const string BootPath  = SceneDir + "/Boot.unity";
        private const string MenuPath  = SceneDir + "/MainMenu.unity";
        private const string BattlePath = SceneDir + "/SampleScene.unity";

        [MenuItem("Prototype/YG/메인화면 흐름 씬 만들기", priority = 20)]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog(
                    "메인화면 흐름 씬 만들기",
                    "Boot.unity · MainMenu.unity 를 새로 만들고 SampleScene 에\n" +
                    "BattleSceneController 를 추가한다. Build Settings 도 다시 쓴다.\n\n" +
                    "같은 이름의 기존 씬은 덮어쓴다. 계속할까?",
                    "만들기", "취소"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(SceneDir);

            BuildBootScene();
            BuildMainMenuScene();
            PatchBattleScene();
            RegisterBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(BootPath, OpenSceneMode.Single);

            Debug.Log("<b>[FlowSceneBuilder]</b> 완료 — Boot 씬을 열어 두었다. Play 를 누를 것.");
            ReportInputHandling();
        }

        // ── Boot ─────────────────────────────────────────

        /// <summary>
        /// 앱 실행부터 종료까지 언로드되지 않는 씬. 그래서 DontDestroyOnLoad 는 쓰지 않는다.
        /// 카메라를 두지 않는 것이 핵심 — MainMenu / SampleScene 이 각자 카메라를 갖고 있어
        /// Boot 에도 카메라가 있으면 Additive 상태에서 화면이 겹친다.
        /// </summary>
        private static void BuildBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var managerGo = new GameObject("GameManager");
            managerGo.AddComponent<GameManager>();
            managerGo.AddComponent<BootStrapper>();

            var loaderGo = new GameObject("SceneLoader");
            SceneLoader loader = loaderGo.AddComponent<SceneLoader>();

            BuildAudioManager();
            BuildFallbackCamera();
            CanvasGroup fadeGroup = BuildFadeCanvas();

            // EventSystem 은 여기에만 둔다. 중복되면 콘솔 경고와 함께 UI 입력이 불안정해진다.
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemGo.AddComponent<StandaloneInputModule>();
#endif

            SetReference(loader, "fadeCanvas", fadeGroup);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BootPath);
        }

        private static void BuildAudioManager()
        {
            var go = new GameObject("AudioManager");
            AudioManager manager = go.AddComponent<AudioManager>();

            AudioSource bgm = CreateAudioSource(go.transform, "BgmSource", loop: true);
            AudioSource sfx = CreateAudioSource(go.transform, "SfxSource", loop: false);

            SetReference(manager, "bgmSource", bgm);
            SetReference(manager, "sfxSource", sfx);
        }

        /// <summary>
        /// 전환 순간에는 로드된 씬의 카메라가 0개가 된다 — 이전 씬을 먼저 내리기 때문이다.
        /// 그때 Game 뷰에 "No cameras rendering" 이 뜬다. 에디터 전용 메시지라 빌드에는 나오지
        /// 않지만, 항상 렌더링 대상이 있도록 아무것도 그리지 않는 카메라를 하나 깔아 없앤다.
        ///
        /// 명세는 "Boot 에 카메라를 두지 말 것" 이라고 했고 그 이유는 화면 겹침이었다.
        /// 이 카메라는 <b>cullingMask 가 Nothing</b> 이라 어떤 오브젝트도 그리지 않고,
        /// depth 가 가장 낮아 씬 카메라가 항상 그 위를 덮는다. 겹칠 수가 없다.
        ///
        /// 두 가지를 반드시 지켜야 한다.
        /// - <b>MainCamera 태그를 붙이지 않는다.</b> 붙이면 <c>Camera.main</c> 이 이 카메라를
        ///   잡아 TargetSelector 의 조준과 DebugComboHUD 의 좌표 변환이 전부 깨진다.
        /// - <b>AudioListener 를 붙이지 않는다.</b> 씬 카메라 것과 중복되면 경고가 뜬다.
        /// </summary>
        private static void BuildFallbackCamera()
        {
            var go = new GameObject("FallbackCamera");

            Camera camera = go.AddComponent<Camera>();
            camera.cullingMask = 0;                          // Nothing — 아무 레이어도 찍지 않는다
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.depth = -100f;                            // 항상 씬 카메라보다 먼저 · 아래
            camera.orthographic = true;
        }

        private static AudioSource CreateAudioSource(Transform parent, string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;

            return source;
        }

        /// <summary>
        /// Screen Space - Overlay 라 카메라가 없는 순간(씬 전환 중)에도 정상 렌더링된다.
        /// Sort Order 999 — 어떤 UI보다 위에 와야 전환이 가려진다.
        /// </summary>
        private static CanvasGroup BuildFadeCanvas()
        {
            var go = new GameObject("FadeCanvas", typeof(RectTransform));

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            AddScaler(go);
            go.AddComponent<GraphicRaycaster>();

            CanvasGroup group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var imageGo = new GameObject("Black", typeof(RectTransform));
            imageGo.transform.SetParent(go.transform, false);

            Image image = imageGo.AddComponent<Image>();
            image.color = Color.black;
            Stretch(image.rectTransform);

            return group;
        }

        // ── MainMenu ─────────────────────────────────────

        /// <summary>EventSystem 을 넣지 않는다. Boot 씬 것을 공유한다.</summary>
        private static void BuildMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.09f, 0.11f);
            cameraGo.AddComponent<AudioListener>();

            var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform));
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            AddScaler(canvasGo);
            canvasGo.AddComponent<GraphicRaycaster>();

            CreateLabel(canvasGo.transform, "Panel_Title", "CAPSTONE PROTOTYPE",
                        anchor: new Vector2(0.5f, 0.72f), size: new Vector2(900f, 140f), fontSize: 64);

            Button start = CreateButton(canvasGo.transform, "Btn_Start", "게임 시작", yOffset: 20f);
            Button quit  = CreateButton(canvasGo.transform, "Btn_Quit",  "종료",     yOffset: -70f);

            var controllerGo = new GameObject("MainMenuController");
            MainMenuController controller = controllerGo.AddComponent<MainMenuController>();

            SetReference(controller, "startButton", start);
            SetReference(controller, "quitButton", quit);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MenuPath);
        }

        private static Button CreateButton(Transform parent, string name, string label, float yOffset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.22f, 0.24f, 0.30f);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 72f);
            rect.anchoredPosition = new Vector2(0f, yOffset);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

            GameObject text = CreateLabel(go.transform, "Label", label,
                                          anchor: new Vector2(0.5f, 0.5f), size: Vector2.zero, fontSize: 28);
            Stretch((RectTransform)text.transform);

            return button;
        }

        /// <summary>
        /// TMP Essential Resources 가 임포트돼 있으면 TextMeshProUGUI 를, 아니면 레거시 Text 를 쓴다.
        /// 폰트 에셋이 없는 상태로 TMP 를 만들면 런타임에 글자가 아예 안 나온다.
        /// </summary>
        private static GameObject CreateLabel(Transform parent, string name, string content,
                                              Vector2 anchor, Vector2 size, int fontSize)
        {
            // RectTransform 을 생성자에서 지정해야 한다. 그냥 new GameObject 는 일반 Transform 이
            // 붙어서 (RectTransform) 캐스트가 InvalidCastException 을 던진다.
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            if (IsTmpReady())
            {
                TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = content;
                tmp.fontSize = fontSize;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }
            else
            {
                Text text = go.AddComponent<Text>();
                text.text = content;
                text.fontSize = fontSize;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return go;
        }

        private static bool IsTmpReady()
        {
            return Resources.Load<TMP_Settings>("TMP Settings") != null;
        }

        // ── SampleScene ──────────────────────────────────

        /// <summary>BattleSceneController 하나만 추가한다. 다른 오브젝트는 일절 손대지 않는다.</summary>
        private static void PatchBattleScene()
        {
            if (!File.Exists(BattlePath))
            {
                Debug.LogWarning($"[FlowSceneBuilder] {BattlePath} 가 없다. 배틀 씬 패치를 건너뛴다.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(BattlePath, OpenSceneMode.Single);

            if (Object.FindAnyObjectByType<BattleSceneController>() == null)
            {
                var go = new GameObject("BattleSceneController");
                go.AddComponent<BattleSceneController>();
            }

            // Boot 씬 것과 중복되면 UI 입력이 불안정해진다.
            foreach (EventSystem existing in Object.FindObjectsByType<EventSystem>(
                         FindObjectsInactive.Include))
            {
                Debug.Log($"[FlowSceneBuilder] SampleScene 의 중복 EventSystem 삭제: {existing.name}");
                Object.DestroyImmediate(existing.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ── Build Settings ───────────────────────────────

        /// <summary>인덱스 0 이 시작 씬이 되므로 순서가 중요하다.</summary>
        private static void RegisterBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(MenuPath, true),
            };

            if (File.Exists(BattlePath))
                scenes.Add(new EditorBuildSettingsScene(BattlePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ReportInputHandling()
        {
#if ENABLE_INPUT_SYSTEM
            Debug.Log("[FlowSceneBuilder] Active Input Handling 에 Input System 이 켜져 있다. " +
                      "ESC 는 UI/Cancel 액션으로 읽는다.");
#else
            // 입력 계층 전체가 Input System 위에 서 있다. 꺼져 있으면 컴파일은 되고 조작만 죽는다.
            Debug.LogError("[FlowSceneBuilder] Active Input Handling 에 Input System 이 꺼져 있다. " +
                           "PlayerInputController 가 동작하지 않아 조작이 전부 먹통이 된다.");
#endif
        }

        // ── 공용 ─────────────────────────────────────────

        private static void EnsureFolder(string dir)
        {
            if (Directory.Exists(dir)) return;

            Directory.CreateDirectory(dir);
            AssetDatabase.ImportAsset(dir);
        }

        private static void AddScaler(GameObject go)
        {
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>[SerializeField] private 슬롯을 코드로 채운다.</summary>
        private static void SetReference(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);

            if (prop == null)
            {
                Debug.LogError($"[FlowSceneBuilder] {target.GetType().Name}.{fieldName} 필드를 찾지 못했다.");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
