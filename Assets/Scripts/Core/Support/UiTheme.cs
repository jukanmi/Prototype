using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// UI 규격 — 색 · 글자 · 간격 · 움직임. <b>이 표가 유일한 원본이다</b>(<see cref="UiLayer"/>와 같은 방식).
    ///
    /// 창마다 파일 맨 위에 자기 색을 적던 것을 여기로 걷어 왔다. 같은 뜻의 색이 파일마다
    /// 미세하게 달라지는 것(금색 세 벌, 판 검정 네 벌)이 이걸로 끝난다.
    /// 근거와 표는 <c>docs/UI_Unification_Plan.md</c> 3항.
    ///
    /// <b>새 색이 필요하면 여기에 이름을 붙여 추가한다.</b> UI 스크립트에 <c>new Color(</c>를 직접 쓰면
    /// <c>UiThemeUsageTests</c>가 잡는다. 알파만 다른 변형은 <see cref="WithAlpha"/>로 만든다.
    /// </summary>
    public static class UiTheme
    {
        // ── 판 ───────────────────────────────────────────

        /// <summary>기본 판 바탕. HUD 판 · 창 · 대사 줄.</summary>
        public static readonly Color Ink = Hex(0x0E1014, 0.90f);

        /// <summary>오목판 — 빈 슬롯 · 게이지 홈 · 막대 바탕 · 스크롤 영역.</summary>
        public static readonly Color InkDeep = Hex(0x07080A, 0.94f);

        /// <summary>판 위에 한 단 올라온 면 — 칸 · 줄 · 일반 버튼.</summary>
        public static readonly Color Raised = Hex(0x262B33);

        /// <summary>모달 뒤 화면 막.</summary>
        public static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.72f);

        /// <summary>판 테두리(바랜 놋쇠).</summary>
        public static readonly Color Rim = Hex(0xC9B48A, 0.55f);

        // ── 글자 ─────────────────────────────────────────

        public static readonly Color TextMain = Hex(0xF2EEE6);

        /// <summary>보조 설명 · 비활성 라벨.</summary>
        public static readonly Color TextSub = Hex(0xA9A59C);

        /// <summary>사용 불가 · 잠김 · 죽음.</summary>
        public static readonly Color TextOff = Hex(0x6B6862);

        /// <summary>밝은 바탕(키캡) 위의 글자.</summary>
        public static readonly Color TextOnLight = Hex(0x14161A);

        // ── 의미 색 ──────────────────────────────────────

        /// <summary><b>이름</b> — 제목 · 재화 · 키워드. 포커스에 쓰지 않는다.</summary>
        public static readonly Color Gold = Hex(0xFFDB73);

        /// <summary><b>지금</b> — 호버 · 키보드 포커스 · 선택. 이 색만 "고른 것"을 뜻한다.</summary>
        public static readonly Color Focus = Hex(0x5FE3E8);

        /// <summary>체력 · 피해 · 경고.</summary>
        public static readonly Color Hp = Hex(0xE5484D);

        /// <summary>방어 · 가드.</summary>
        public static readonly Color Guard = Hex(0x4F9BFF);

        /// <summary>강화된 수치 · 회복 · 확정.</summary>
        public static readonly Color Buff = Hex(0x7BE07B);

        /// <summary>상태이상 · 저주 · 가드 브레이크.</summary>
        public static readonly Color Debuff = Hex(0xC77DFF);

        /// <summary>총공격 게이지.</summary>
        public static readonly Color Gauge = Hex(0xA873FF);

        // ── 버튼 바탕 ────────────────────────────────────

        /// <summary>권하는 행동([계속] · [마을로] · 확정).</summary>
        public static readonly Color ButtonPrimary = Hex(0x5C4A1F);

        /// <summary>보통 행동.</summary>
        public static readonly Color ButtonNormal = Raised;

        /// <summary>되돌릴 수 없는 행동([처음부터] · 재시작 · 확인).</summary>
        public static readonly Color ButtonDanger = Hex(0x7A2E28);

        /// <summary>버튼 눌림 틴트(바탕색에 곱해진다).</summary>
        public static readonly Color PressedTint = new Color(0.82f, 0.82f, 0.82f, 1f);

        /// <summary>버튼 사용 불가 틴트.</summary>
        public static readonly Color DisabledTint = new Color(0.55f, 0.55f, 0.55f, 0.7f);

        // ── 지도 칸 종류 ─────────────────────────────────

        /// <summary>
        /// 지도 칸의 종류 색. 칸 아이콘(6항 아트)이 들어오면 아이콘 뒤 원판 색으로만 남는다.
        /// 채도를 눌러 둔 것은 <see cref="Focus"/>(갈 수 있는 칸 발광)와 <see cref="Gold"/>(지나온 길)가 이겨야 해서다.
        /// </summary>
        public static class Map
        {
            public static readonly Color Battle = new Color(0.50f, 0.28f, 0.24f);
            public static readonly Color Elite  = new Color(0.62f, 0.20f, 0.34f);
            public static readonly Color Rest   = new Color(0.22f, 0.48f, 0.32f);
            public static readonly Color Shop   = new Color(0.62f, 0.50f, 0.18f);
            public static readonly Color Event  = new Color(0.30f, 0.34f, 0.62f);
            public static readonly Color Boss   = new Color(0.52f, 0.10f, 0.10f);
        }

        // ── 글자 크기 — 6단계 ─────────────────────────────

        /// <summary>
        /// 글자 크기는 이 여섯 개뿐이다. 17단계(13 ~ 56)가 뜻 없이 섞여 있던 것을 접었다.
        /// </summary>
        public static class Size
        {
            /// <summary>결과 화면 큰 글씨, 콤보 타수.</summary>
            public const int Display = 56;
            /// <summary>창 제목.</summary>
            public const int Title = 36;
            /// <summary>선택지 제목 · 카드 이름 · 섹션 제목 · 큰 숫자.</summary>
            public const int Heading = 26;
            /// <summary>설명 · 버튼 라벨 · 대사.</summary>
            public const int Body = 20;
            /// <summary>보조 설명 · 키 안내 · 툴팁 본문.</summary>
            public const int Caption = 16;
            /// <summary>배지 숫자 · 스택 수.</summary>
            public const int Micro = 13;

            public static readonly int[] All = { Display, Title, Heading, Body, Caption, Micro };
        }

        // ── 간격 · 크기 ──────────────────────────────────

        /// <summary>간격은 4의 배수만 쓴다.</summary>
        public static class Space
        {
            public const float S4 = 4f, S8 = 8f, S12 = 12f, S16 = 16f, S24 = 24f, S32 = 32f, S48 = 48f;

            /// <summary>화면 가장자리 여백.</summary>
            public const float Edge = S32;
        }

        public static class ButtonHeight
        {
            public const float Small = 40f, Medium = 56f, Large = 72f;
            public const float MinWidth = 160f;
        }

        /// <summary>판 테두리 두께.</summary>
        public const float RimWidth = 2f;

        // ── 움직임(초, unscaled) ─────────────────────────

        public static class Motion
        {
            /// <summary>호버 테두리 켜짐 · 버튼 눌림.</summary>
            public const float Quick = 0.08f;
            /// <summary>창 열림 · 툴팁.</summary>
            public const float Normal = 0.18f;
            /// <summary>패널 이동 · 체력 잔상.</summary>
            public const float Slow = 0.35f;
            /// <summary>호버 후 툴팁이 뜨기까지.</summary>
            public const float TooltipDelay = 0.25f;
        }

        // ── 글꼴 ─────────────────────────────────────────

        /// <summary><c>Resources/</c> 안의 경로. 한글 글리프가 있는 글꼴이어야 한다.</summary>
        public const string FontPath = "Fonts/SpoqaHanSansNeo-Medium";

        private static Font font;

        /// <summary>
        /// 모든 UI 글자의 글꼴. 못 찾으면 내장 글꼴로 물러난다 — 글자가 아예 안 나오는 것보다
        /// 모양이 다른 편이 낫다.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>(FontPath);
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        // ── 스프라이트 ───────────────────────────────────

        /// <summary>
        /// 규격 스프라이트(<c>Resources/Ui/</c>). 판 계열은 <b>흰색으로 그려져 있어</b> <see cref="UnityEngine.UI.Image.color"/>가
        /// 토큰 색으로 물들인다 — 색은 여전히 이 표가 정한다. 아이콘은 색이 구워져 있다.
        ///
        /// 파일이 없으면 null — 부르는 쪽은 단색 사각형으로 물러난다(예전 모양). 같은 이름의 파일을 덮어쓰면
        /// 아트가 바뀐다(GUID 유지). 원본 생성 규칙은 기획서 6항.
        /// </summary>
        public static class Art
        {
            public const string Folder = "Ui/";

            /// <summary>둥근 판(9-slice). 창 · 버튼 · 툴팁.</summary>
            public static Sprite Plate => Load(ref plate, "Plate");
            /// <summary>오목판(9-slice, 안쪽 그림자). 슬롯 · 홈.</summary>
            public static Sprite Well => Load(ref well, "Well");
            /// <summary>키캡(9-slice, 아랫변 두께).</summary>
            public static Sprite Keycap => Load(ref keycap, "Keycap");
            /// <summary>포커스 발광(9-slice, 가운데 빔). 판보다 <see cref="GlowSpread"/>만큼 크게 깐다.</summary>
            public static Sprite FocusGlow => Load(ref focusGlow, "FocusGlow");
            /// <summary>개수 배지 원(테두리 구움).</summary>
            public static Sprite Badge => Load(ref badge, "Badge");

            public static Sprite IconHp => Load(ref iconHp, "Icon_Hp");
            public static Sprite IconGold => Load(ref iconGold, "Icon_Gold");
            public static Sprite IconDeck => Load(ref iconDeck, "Icon_Deck");
            public static Sprite IconFloor => Load(ref iconFloor, "Icon_Floor");

            /// <summary>발광 스프라이트의 투명 여백 — 판 밖으로 번지는 폭.</summary>
            public const float GlowSpread = 16f;

            private static Sprite plate, well, keycap, focusGlow, badge, iconHp, iconGold, iconDeck, iconFloor;

            private static Sprite Load(ref Sprite cache, string name)
            {
                if (cache == null) cache = Resources.Load<Sprite>(Folder + name);
                return cache;
            }

            /// <summary>이미지에 판 스프라이트를 얹는다. 없으면 아무것도 안 한다(단색 사각형 그대로).</summary>
            public static void Apply(UnityEngine.UI.Image img, Sprite sprite)
            {
                if (img == null || sprite == null) return;
                img.sprite = sprite;
                img.type = sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
                img.pixelsPerUnitMultiplier = 1f;
            }
        }

        /// <summary>글자 외곽선 — 어떤 배경 위에서도 읽히게.</summary>
        public static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.85f);

        // ── 도우미 ───────────────────────────────────────

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>명도만 곱한다(어둡게 &lt; 1 &lt; 밝게). 알파는 그대로.</summary>
        public static Color Shade(this Color c, float k)
            => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

        /// <summary>"#RRGGBB" — 리치 텍스트용.</summary>
        public static string ToHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>
        /// 가장 가까운 글자 단계. 크기를 받아 쓰던 옛 호출을 6단계로 끌어당긴다.
        /// 같은 거리면 큰 쪽 — 한글은 작을 때 먼저 뭉개진다.
        /// </summary>
        public static int SnapSize(int size)
        {
            int best = Size.All[0];
            foreach (int s in Size.All)
            {
                int d = Mathf.Abs(s - size), bd = Mathf.Abs(best - size);
                if (d < bd || (d == bd && s > best)) best = s;
            }
            return best;
        }

        /// <summary>WCAG 상대 명도 대비(1 ~ 21). 테스트가 본문/판 대비를 지키는 데 쓴다.</summary>
        public static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            if (la < lb) (la, lb) = (lb, la);
            return (la + 0.05f) / (lb + 0.05f);
        }

        private static float Luminance(Color c)
        {
            static float Ch(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Ch(c.r) + 0.7152f * Ch(c.g) + 0.0722f * Ch(c.b);
        }

        private static Color Hex(int rgb, float a = 1f)
            => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
    }

    /// <summary>
    /// 설명문 속 강조. 데이터에는 색 코드 대신 표식만 적고, 그리는 쪽이 바꾼다.
    /// <code>
    /// "{kw:뽑을 카드 더미}에 {n:9|5}장"  →  키워드는 금색, 9는 기본값 5보다 커서 초록
    /// </code>
    /// </summary>
    public static class UiText
    {
        public static string Paint(string s, Color c) => $"<color={UiTheme.ToHex(c)}>{s}</color>";

        /// <summary>키워드 — 금색.</summary>
        public static string Kw(string s) => Paint(s, UiTheme.Gold);

        /// <summary>숫자. 기본값보다 크면 <see cref="UiTheme.Buff"/>, 작으면 <see cref="UiTheme.Hp"/>.</summary>
        public static string Num(int value, int baseValue)
        {
            string s = value.ToString();
            if (value > baseValue) return Paint(s, UiTheme.Buff);
            if (value < baseValue) return Paint(s, UiTheme.Hp);
            return s;
        }

        /// <summary><c>{kw:…}</c> · <c>{n:값|기본}</c> · <c>{n:값}</c> 표식을 리치 텍스트로 바꾼다.</summary>
        public static string Format(string src)
        {
            if (string.IsNullOrEmpty(src)) return src;

            var sb = new System.Text.StringBuilder(src.Length + 32);
            int i = 0;
            while (i < src.Length)
            {
                int open = src.IndexOf('{', i);
                if (open < 0) { sb.Append(src, i, src.Length - i); break; }

                int close = src.IndexOf('}', open);
                if (close < 0) { sb.Append(src, i, src.Length - i); break; }

                sb.Append(src, i, open - i);
                string tag = src.Substring(open + 1, close - open - 1);
                sb.Append(Expand(tag) ?? src.Substring(open, close - open + 1));
                i = close + 1;
            }
            return sb.ToString();
        }

        private static string Expand(string tag)
        {
            if (tag.StartsWith("kw:")) return Kw(tag.Substring(3));
            if (!tag.StartsWith("n:")) return null;

            string body = tag.Substring(2);
            int bar = body.IndexOf('|');
            if (bar < 0) return int.TryParse(body, out _) ? body : null;

            if (int.TryParse(body.Substring(0, bar), out int v) &&
                int.TryParse(body.Substring(bar + 1), out int b))
                return Num(v, b);
            return null;
        }
    }
}
