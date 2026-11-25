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
        /// <summary>
        /// 월드보다 <b>먼저</b> 그리는 화면 판(아래 검은 띠). 오버레이가 아니라 카메라 공간 캔버스라
        /// 같은 정렬층의 스프라이트와 순서로 겨룬다 — 맨 뒤 순서라 바닥 · 벽 · 몸이 전부 그 위에 그려진다.
        /// </summary>
        public const int BehindWorld = short.MinValue;

        /// <summary>
        /// 스킬 로그(<see cref="SkillLogHUD"/>). 화면 왼쪽 위에서 아래로 최대 36줄까지 쌓여
        /// 좌하단 묶음(초상화 · 체력 · 총공격 게이지)까지 내려올 수 있다 — 그때 묶음이 위에 그려지도록
        /// <b>게이지(<see cref="Gauge"/>)보다도 아래</b>에 둔다.
        /// </summary>
        public const int SkillLog = -1;

        /// <summary>총공격 게이지. 전투 중 항상 떠 있는 바닥층.</summary>
        public const int Gauge = 0;

        /// <summary>최근 피격 적 정보.</summary>
        public const int RecentHitEnemy = 2;

        /// <summary>우측 상단 [ESC 일시정지] 안내 버튼. 전투 HUD와 같은 바닥층 무리 — 창이 뜨면 그 밑에 깔린다.</summary>
        public const int PauseHint = 4;

        /// <summary>콤보 타수 · 누적 데미지.</summary>
        public const int ComboDamage = 5;

        /// <summary>전투의 런 바(<see cref="RunBarUI"/>). HUD 바닥층 무리 — 콤보 위, 컷인 아래, 창이 뜨면 그 밑.</summary>
        public const int RunBar = 6;

        /// <summary>스킬 컷인. 콤보 숫자를 덮는다.</summary>
        public const int SkillCutin = 10;

        /// <summary>파티 체력 HUD.</summary>
        public const int PartyHealth = 15;

        /// <summary>바닥 보상(스킬 두루마리)에 다가가면 뜨는 설명. HUD는 덮고 창 밑에 깔린다.</summary>
        public const int LootTooltip = 20;

        /// <summary>메인화면 파티 편성.</summary>
        public const int PartySelect = 50;

        /// <summary>
        /// 휴식 · 상점 · 이벤트 칸의 창. 그 씬에는 전투 HUD가 없어서 겹칠 상대가 없다 —
        /// 값은 편성 화면과 같은 "혼자 뜨는 전면 창" 자리다.
        /// </summary>
        public const int NodeWindow = 50;

        /// <summary>
        /// 지도 · 칸 씬의 런 바. 그 씬은 화면 전체가 전면 창(<see cref="NodeWindow"/>)이라 그 위에 얹어야 보인다.
        /// 일시정지 · 결과 창보다는 아래.
        /// </summary>
        public const int RunBarOverWindow = 60;

        /// <summary>일시정지 창. 조작키 창(<see cref="Rebind"/>)이 그 위에 떠야 한다.</summary>
        public const int Pause = 190;

        /// <summary>
        /// 스킬 목록(<see cref="SkillListUI"/>). 런 바에서 여는 보기 전용 창 — 런 바(지도 · 칸은 60)와
        /// 일시정지보다 위, 결과 · 조작키보다 아래.
        /// </summary>
        public const int SkillList = 195;

        /// <summary>스테이지 종료 화면.</summary>
        public const int StageResult = 200;

        /// <summary>
        /// 조작키 설정.
        ///
        /// <b><see cref="StageResult"/>와 값이 같다.</b> 둘이 동시에 뜨는 경로가 없어서
        /// 지금은 문제가 안 되지만, 한쪽이라도 상시 표시로 바뀌면 여기가 사고 지점이다.
        /// </summary>
        public const int Rebind = 200;

        /// <summary>덱 편성.</summary>
        public const int DeckBuilder = 220;

        /// <summary>툴팁(<see cref="UiTooltip"/>). 어느 창 위에서 호버해도 보여야 한다 — 전환 페이드만 그 위.</summary>
        public const int Tooltip = 900;

        /// <summary>
        /// 씬 전환 페이드. 무엇보다도 위 — 전환 중엔 아무것도 안 보여야 한다.
        ///
        /// 이것만 <b>Boot 씬에 박혀 있다</b>(<c>SceneLoader.fadeCanvas</c>는 씬 배선이다).
        /// 코드가 읽지는 않지만, 위 값들이 "얼마나 아래인가"의 기준이라 여기 적어 둔다.
        /// </summary>
        public const int SceneFade = 999;
    }

    /// <summary>
    /// 좌측 하단 HUD 묶음의 자리. 왼쪽에 <b>초상화 틀</b>, 그 오른쪽 변에 <b>체력바</b>(<see cref="PartyHealthHUD"/>)가
    /// 딱 붙고, 그 바로 밑에 <b>총공격 게이지</b>(<see cref="BulletTimeGaugeUI"/>)가 테두리를 맞대고 붙는다.
    /// 두 막대 모두 오른쪽 끝이 꺾였고(<see cref="SlantedBar"/>), 게이지가 더 얇고 짧아 계단처럼 내려간다.
    ///
    /// <code>
    ///    (머리)               ← 얼굴이 틀 위로 삐져나온다 (PortraitOverflow)
    /// ┌──┴──┐
    /// │ 얼굴 │━━━━━━━━━━━━━━━━━━━━━━━┓
    /// │      │ ███████ 203 / 254      /   ← 체력 (HpY, HpHeight, HpWidth)
    /// │      │━━━━━━━━━━━━━━━━━━━━━┳━┛
    /// │      │ ▓▓▓▓ 총공격 62%    /       ← 게이지 (GaugeY, GaugeHeight, GaugeWidth)
    /// └──────┘━━━━━━━━━━━━━━━━━━┛
    /// </code>
    ///
    /// 둘은 다른 프리팹 · 다른 캔버스에 산다(BattleInput / CombatManager). 두 캔버스의 스케일러가
    /// 같아서(1920×1080, 세로 기준) 캔버스 좌표가 곧 같은 화면 자리다 — 그래서 위치를 이 표 하나로 맞춘다.
    /// 게이지 캔버스가 아래층이라 맞닿는 테두리는 체력 쪽이 덮는다 — 게이지 윗변을 체력 테두리 두께만큼
    /// 겹쳐 올려 검은 줄이 한 겹으로 보이게 한다(<see cref="GaugeY"/>).
    ///
    /// 막대의 왼쪽 끝은 틀의 오른쪽 변과 같은 x다(<see cref="BarX"/> = <see cref="FrameSize"/>) — 틈도 겹침도 없다.
    ///
    /// 모든 값은 묶음 원점(<see cref="MarginX"/>, <see cref="MarginBottom"/>) 기준이다.
    /// 체력 쪽 값은 프리팹에 박혀 있고, 어긋나면 <c>BattleInputPrefabTests</c>가 잡는다.
    /// </summary>
    public static class HudLayout
    {
        /// <summary>화면 왼쪽 끝에서 묶음(초상화 틀 왼쪽 끝)까지.</summary>
        public const float MarginX = 36f;

        /// <summary>화면 아래 끝에서 묶음(초상화 틀 아랫변)까지.</summary>
        public const float MarginBottom = 32f;

        /// <summary>초상화 틀 한 변(테두리 포함).</summary>
        public const float FrameSize = 104f;

        /// <summary>틀 테두리 두께. 얼굴은 이만큼 안쪽에서 잘린다(위쪽 제외).</summary>
        public const float FrameBorder = 2f;

        /// <summary>얼굴이 틀 윗변 위로 삐져나오는 높이.</summary>
        public const float PortraitOverflow = 26f;

        /// <summary>두 막대가 공유하는 왼쪽 끝 — 틀 오른쪽 변에 딱 붙는다.</summary>
        public const float BarX = FrameSize;

        /// <summary>막대 테두리 두께. 체력 · 게이지 같은 값이어야 맞닿는 줄이 고르다.</summary>
        public const float Border = 3f;

        /// <summary>꺾인 끝의 가로 ÷ 높이. 두 막대가 같은 각으로 꺾인다.</summary>
        public const float SlantRatio = 0.5f;

        /// <summary>체력바(테두리 포함) 아랫변 · 두께 · 폭. 틀 가운데보다 살짝 아래에 선다.</summary>
        public const float HpY = 30f;
        public const float HpHeight = 34f;
        public const float HpWidth = 358f;

        /// <summary>게이지(테두리 포함) 두께 · 폭. 체력보다 얇고 짧다.</summary>
        public const float GaugeHeight = 20f;
        public const float GaugeWidth = 318f;

        /// <summary>게이지 아랫변. 윗변이 체력 테두리 안쪽까지 올라가 맞닿는 검은 줄이 한 겹이 된다.</summary>
        public const float GaugeY = HpY + Border - GaugeHeight;

        /// <summary>묶음 전체 폭 — 틀 왼쪽 끝 ~ 체력바 꺾인 끝.</summary>
        public const float Width = BarX + HpWidth;

        /// <summary>묶음 전체 높이 — 틀 아랫변 ~ 삐져나온 머리 꼭대기.</summary>
        public const float Height = FrameSize + PortraitOverflow;

        /// <summary>
        /// 화면 아래 검은 띠의 높이 — 묶음 아래 여백만큼 위에도 남긴다. 삐져나온 머리(<see cref="PortraitOverflow"/>)까지 띠 안이다.
        /// </summary>
        public const float BandHeight = MarginBottom * 2 + FrameSize;
    }

    /// <summary>
    /// 코드로 짓는 런타임 UI의 공용 조각. 이 프로젝트의 UI는 전부 씬·프리팹 배선 없이
    /// 스스로 만들어지므로, 같은 열 줄이 파일마다 반복되지 않도록 여기 모은다.
    ///
    /// TMP 대신 레거시 <see cref="Text"/>를 쓰는 이유 — TMP Essential Resources 가
    /// 임포트돼 있지 않으면 런타임에 글자가 아예 안 나온다.
    ///
    /// 색 · 글자 크기 · 글꼴은 <see cref="UiTheme"/>이 정한다. 여기서 만드는 글자는 전부
    /// 그 글꼴 · 6단계 크기 · 외곽선을 기본으로 가진다.
    /// </summary>
    public static partial class UiKit
    {
        public static Font Font => UiTheme.Font;

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

        /// <summary>
        /// 글자 하나. 크기는 <see cref="UiTheme.Size"/> 6단계 중 가장 가까운 것으로 끌어당기고,
        /// 외곽선을 기본으로 붙인다 — 배경 일러스트 위에서도 읽히게. 밝은 바탕 위 글자처럼
        /// 외곽선이 해로운 자리만 <see cref="NoOutline"/>으로 뗀다.
        /// </summary>
        public static Text NewText(Transform parent, string name, int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            Text t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = UiTheme.SnapSize(fontSize);
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.supportRichText = true;  // UiText.Kw · Num 강조
            t.raycastTarget = false;   // 버튼 클릭을 라벨이 가로채지 않도록
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            AddOutline(t);
            return t;
        }

        /// <summary>
        /// 글자 외곽선. 큰 글자(<see cref="UiTheme.Size.Title"/> 이상)는 아래로 그림자를 하나 더 쌓아 떠 보이게 한다.
        /// 프리팹에 박힌 글자에도 쓴다 — 여러 번 불러도 한 벌만 붙는다.
        /// </summary>
        public static void AddOutline(Text t)
        {
            if (t == null || t.GetComponent<Outline>() != null) return;

            float d = t.fontSize >= UiTheme.Size.Heading ? 2f : 1.5f;
            Outline o = t.gameObject.AddComponent<Outline>();
            o.effectColor = UiTheme.OutlineColor;
            o.effectDistance = new Vector2(d, -d);

            if (t.fontSize >= UiTheme.Size.Title)
            {
                // Outline 은 Shadow 의 파생이라 GetComponent<Shadow> 로는 구분이 안 된다 — 따로 붙인다.
                Shadow s = t.gameObject.AddComponent<Shadow>();
                s.effectColor = UiTheme.OutlineColor.WithAlpha(0.6f);
                s.effectDistance = new Vector2(0f, -4f);
            }
        }

        /// <summary>외곽선을 뗀다. 밝은 바탕(키캡 등) 위의 어두운 글자용.</summary>
        public static void NoOutline(Text t)
        {
            if (t == null) return;
            foreach (Shadow s in t.GetComponents<Shadow>())
            {
                if (Application.isPlaying) Object.Destroy(s);
                else Object.DestroyImmediate(s);
            }
        }

        /// <summary>
        /// 프리팹 · 씬에 박혀 있던 글자들을 규격에 맞춘다 — 글꼴과 외곽선. 크기는 프리팹이 정한 대로 둔다
        /// (프리팹 배치 테스트가 그 값을 지킨다).
        /// </summary>
        public static void Dress(Component root)
        {
            if (root == null) return;
            foreach (Text t in root.GetComponentsInChildren<Text>(true))
            {
                t.font = Font;
                AddOutline(t);
            }
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize, Color color)
        {
            Text text = NewText(parent, name, fontSize, color, FontStyle.Normal);
            text.text = content;
            return text;
        }

        /// <summary>
        /// 버튼 하나. 바탕색은 부르는 쪽이 고르되(<see cref="UiTheme.ButtonPrimary"/> 등), 테두리 · 호버 · 눌림은
        /// 전 화면 공통이다 — <see cref="UiFocusRim"/>. 새 코드는 종류로 고르는 <see cref="NewButton"/>를 쓴다.
        /// </summary>
        public static Button CreateButton(Transform parent, string name, string label, Color color,
                                          Vector2 size, int fontSize)
        {
            GameObject go = CreateImage(parent, name, color);
            ((RectTransform)go.transform).sizeDelta = size;
            UiTheme.Art.Apply(go.GetComponent<Image>(), UiTheme.Art.Plate);   // 둥근 판 — 색은 여전히 color

            Button button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();

            Text text = CreateText(go.transform, "Label", label, fontSize, UiTheme.TextMain);
            Stretch((RectTransform)text.transform);

            // 눌림 · 사용 불가는 색 틴트로, 호버 · 포커스는 테두리(UiFocusRim)로 — 둘이 겹치지 않게
            // 하이라이트 틴트는 끈다. 프리팹 버튼과 같은 길(DressButton)을 탄다.
            DressButton(button);

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

        /// <summary>앵커 구간(<paramref name="min"/>..<paramref name="max"/>)을 채우고 좌우만 <paramref name="padX"/> 들인다.</summary>
        public static void Band(Component target, Vector2 min, Vector2 max, float padX)
        {
            var rect = (RectTransform)target.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(padX, 0f);
            rect.offsetMax = new Vector2(-padX, 0f);
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
