using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Prototype
{
    /// <summary>
    /// 화면에 겹치는 순서. <b>이 표가 유일한 원본이다</b> — 값을 파일마다 손으로 적고
    /// "누가 누구보다 위"를 주석으로 추적하던 것을 여기로 걷어 왔다.
    ///
    /// 캔버스가 서로 겹치는 순간은 드물지만, 겹칠 때 어느 쪽이 이기는지는
    /// 눈으로 봐야만 알 수 있어서 회귀가 조용히 일어난다.
    /// </summary>
    public static class UiLayer
    {
        /// <summary>손패 보드. 전투 중 항상 떠 있는 바닥층.</summary>
        public const int ComboBoard = 0;

        /// <summary>최근 피격 적 정보.</summary>
        public const int RecentHitEnemy = 2;

        /// <summary>콤보 타수 · 누적 데미지.</summary>
        public const int ComboDamage = 5;

        /// <summary>스킬 컷인. 콤보 숫자를 덮는다.</summary>
        public const int SkillCutin = 10;

        /// <summary>파티 체력 HUD.</summary>
        public const int PartyHealth = 15;

        /// <summary>덱 상황판 팝업. 손패 · 피격 HUD를 덮는다.</summary>
        public const int DeckInspector = 20;

        /// <summary>메인화면 파티 편성.</summary>
        public const int PartySelect = 50;

        /// <summary>인게임 [처음부터] 버튼.</summary>
        public const int BattleRestart = 100;

        /// <summary>스테이지 종료 화면.</summary>
        public const int StageResult = 200;

        /// <summary>
        /// 조작키 설정.
        ///
        /// <b><see cref="StageResult"/>와 값이 같다.</b> 둘이 동시에 뜨는 경로가 없어서
        /// 지금은 문제가 안 되지만, 한쪽이라도 상시 표시로 바뀌면 여기가 사고 지점이다.
        /// </summary>
        public const int Rebind = 200;

        /// <summary>레벨 업 선택. 결과 화면 · 조작키보다 위.</summary>
        public const int LevelUp = 210;

        /// <summary>덱 편성.</summary>
        public const int DeckBuilder = 220;

        /// <summary>
        /// 씬 전환 페이드. 무엇보다도 위 — 전환 중엔 아무것도 안 보여야 한다.
        ///
        /// 이것만 <b>Boot 씬에 박혀 있다</b>(<c>SceneLoader.fadeCanvas</c>는 씬 배선이다).
        /// 코드가 읽지는 않지만, 위 값들이 "얼마나 아래인가"의 기준이라 여기 적어 둔다.
        /// </summary>
        public const int SceneFade = 999;
    }

    /// <summary>
    /// 코드로 짓는 런타임 UI의 공용 조각. 이 프로젝트의 UI는 전부 씬·프리팹 배선 없이
    /// 스스로 만들어지므로, 같은 열 줄이 파일마다 반복되지 않도록 여기 모은다.
    ///
    /// TMP 대신 레거시 <see cref="Text"/>를 쓰는 이유 — TMP Essential Resources 가
    /// 임포트돼 있지 않으면 런타임에 글자가 아예 안 나온다.
    /// </summary>
    public static class UiKit
    {
        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ── 캔버스 ───────────────────────────────────────

        /// <summary>
        /// 화면을 덮는 캔버스 하나. 정렬 순서는 <see cref="UiLayer"/>에서 고른다.
        /// </summary>
        /// <param name="match">
        /// 1이면 세로 기준으로만 늘어난다 — 가로로 넓은 모니터에서 HUD가 안 커진다.
        /// 0.5는 가로·세로를 절반씩 섞는다. 화면 가장자리에 붙는 것은 1, 가운데 패널은 0.5.
        /// </param>
        /// <param name="raycaster">
        /// 클릭을 받는 캔버스만 true. 비대화형 HUD에 붙이면 그 패널이 아래층 클릭을 가로챈다.
        /// </param>
        public static Canvas BuildCanvas(GameObject host, int sortingOrder,
                                         float match = 0.5f, bool raycaster = true)
        {
            Canvas canvas = host.GetComponent<Canvas>();
            if (canvas == null) canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = host.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = match;

            if (raycaster && host.GetComponent<GraphicRaycaster>() == null)
                host.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        /// <summary>
        /// Boot 씬을 거쳐 들어오면 EventSystem 이 이미 있다. 배틀 씬만 단독으로 Play 했을 때를 위한 보험.
        /// 중복 생성되면 UI 입력이 불안정해지므로 없을 때만 만든다.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ── 조각 ─────────────────────────────────────────

        public static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image NewImage(Transform parent, string name, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>클릭을 받는 이미지. 버튼 바탕에 쓴다.</summary>
        public static GameObject CreateImage(Transform parent, string name, Color color)
            => NewImage(parent, name, color, raycast: true).gameObject;

        public static Text NewText(Transform parent, string name, int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            Text t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;   // 버튼 클릭을 라벨이 가로채지 않도록
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize, Color color)
        {
            Text text = NewText(parent, name, fontSize, color, FontStyle.Normal);
            text.text = content;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Color color,
                                          Vector2 size, int fontSize)
        {
            GameObject go = CreateImage(parent, name, color);
            ((RectTransform)go.transform).sizeDelta = size;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();

            Text text = CreateText(go.transform, "Label", label, fontSize, Color.white);
            Stretch((RectTransform)text.transform);

            return button;
        }

        // ── 배치 ─────────────────────────────────────────

        /// <summary>부모를 꽉 채운다. <paramref name="inset"/>만큼 사방을 안으로 들인다.</summary>
        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>앵커·피벗을 한 점으로 모으고 그 자리에 놓는다.</summary>
        public static RectTransform Place(Component target, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)target.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            return rect;
        }
    }
}
