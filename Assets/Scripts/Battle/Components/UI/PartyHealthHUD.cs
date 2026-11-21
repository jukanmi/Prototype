using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 화면 <b>좌측 하단</b>의 주인공 초상화 + 체력바. 로그라이크 액션류의 익숙한 자리다.
    ///
    /// 로스터(주인공 + 동료)를 받아 줄을 짓지만 지금은 주인공 줄만 띄운다(<see cref="Build"/>).
    /// 줄 모양은 프리팹의 RowTemplate이 쥐고, 코드는 복제해 쌓고 매 프레임 값만 밀어 넣는다.
    ///
    /// 연출은 셋이다 — 전부 "맞았다"를 눈 끝으로도 알게 하려는 것.
    /// <list type="bullet">
    /// <item><b>잔상 막대(Trail)</b>: 깎인 만큼이 잠깐 밝게 남았다가 뒤늦게 줄어든다. 얼마나 맞았는지가 보인다.</item>
    /// <item><b>피격 플래시 · 흔들림</b>: 초상화가 붉게 번쩍이고 몇 픽셀 떨린다.</item>
    /// <item><b>위기 점멸</b>: 체력이 <see cref="DangerRatio"/> 밑이면 초상화가 옅게 붉게 맥동한다.</item>
    /// </list>
    ///
    /// 이름 · 바탕판은 없다. 초상화 옆에 오른쪽 끝이 꺾인 체력바(<see cref="SlantedBar"/>)가 붙고,
    /// 그 바로 밑에 총공격 게이지(<see cref="BulletTimeGaugeUI"/>)가 더 얇고 짧게 테두리를 맞대고 붙는다 —
    /// 두 자리는 <see cref="HudLayout"/> 한 표로 맞춘다. 줄 안의 배치는 프리팹에 박혀 있다.
    /// </summary>
    public class PartyHealthHUD : MonoBehaviour
    {
        // 줄 <b>모양</b>은 프리팹의 RowTemplate이 쥔다. 여기 남은 건 줄을 쌓는 간격뿐이다 —
        // 줄 개수가 로스터에 따라 달라져서 세로 위치는 런타임에 계산해야 한다.
        private const float RowGap = 10f;

        private static readonly Color BarColor   = UiTheme.Hp;
        private static readonly Color TrailColor = UiTheme.TextMain.WithAlpha(0.9f);
        private static readonly Color NumberText = UiTheme.TextMain.WithAlpha(0.95f);
        private static readonly Color FaceDead   = UiTheme.TextOff;
        private static readonly Color HeroTint   = new Color32(0xD8, 0xB0, 0x4C, 0xFF);

        /// <summary>막대 테두리. 총공격 게이지 테두리와 같은 색이다.</summary>
        public static readonly Color BarBorderColor = UiTheme.OutlineColor.WithAlpha(0.95f);

        /// <summary>체력이 이 밑으로 내려가면 초상화가 붉게 맥동한다. 막대 색은 안 바뀐다.</summary>
        private const float DangerRatio = 0.25f;

        /// <summary>잔상 막대가 따라 내려오기 시작할 때까지의 정지 시간.</summary>
        private const float TrailHold = 0.45f;

        /// <summary>잔상 막대가 내려오는 속도(초당 비율).</summary>
        private const float TrailSpeed = 0.9f;

        private const float FlashTime = 0.28f;
        private const float ShakeTime = 0.22f;
        private const float ShakePixels = 6f;
        private const float DangerPulseHz = 2.2f;

        [Tooltip("비우면 같은 오브젝트에서 찾는다. 로스터의 주인이다.")]
        [SerializeField] private PartyRoster roster;

        [Tooltip("끄면 패널을 아예 만들지 않는다. 시연 녹화 때 화면을 비우는 용도.")]
        [SerializeField] private bool show = true;

        [Tooltip("체력 수치(120 / 200)를 막대 위에 함께 적는다.")]
        [SerializeField] private bool showNumbers = true;

        // ── 배선 ─────────────────────────────────────────
        // 줄 하나의 생김새는 프리팹의 RowTemplate이 통째로 쥔다. 코드는 그걸 복제해 쌓기만 한다 —
        // 줄 <b>개수</b>만 로스터에 따라 달라지고, 줄 <b>모양</b>은 늘 같다.

        [Header("배선")]
        [Tooltip("줄이 쌓이는 곳. 화면 좌측 하단에 붙는다.")]
        [SerializeField] private RectTransform panel;

        [Tooltip("줄 하나의 원본. 꺼진 채로 두고 복제해 쓴다.")]
        [SerializeField] private RectTransform rowTemplate;

        /// <summary>
        /// 복제한 줄에서 조각을 찾는 경로. <c>BattleInputPrefabTests</c>가 같은 목록으로
        /// RowTemplate을 검사한다 — 이름을 바꾸면 테스트가 먼저 운다.
        /// </summary>
        public static readonly string[] PartPaths =
        {
            PathFrame, PathBack, PathFlash, PathClip, PathFace, PathBarBack, PathTrack, PathTrail, PathFill, PathNumbers,
        };

        /// <summary>초상화 틀. 이 이미지 자체가 얇은 테두리이고, 안쪽을 <see cref="PathBack"/>이 채운다.</summary>
        public const string PathFrame   = "Frame";
        public const string PathBack    = "Frame/Back";
        /// <summary>틀 안 붉은 막. 얼굴 <b>뒤</b>에 깔려 피격 · 위기 때 틀 안이 붉게 번진다.</summary>
        public const string PathFlash   = "Frame/Flash";
        /// <summary>
        /// 얼굴을 자르는 상자(<see cref="RectMask2D"/>). 좌 · 우 · 아래는 틀 안쪽에 맞추고 위만 틀 밖으로
        /// <see cref="HudLayout.PortraitOverflow"/>만큼 열려 있다 — 머리는 삐져나오고 몸은 틀 아랫변에서 잘린다.
        /// </summary>
        public const string PathClip    = "Frame/Clip";
        public const string PathFace    = "Frame/Clip/Face";
        /// <summary>막대 테두리. 꺾인 모양 전체이고, 홈 · 잔상 · 채움이 그 안쪽에 같은 각으로 선다.</summary>
        public const string PathBarBack = "BarBack";
        public const string PathTrack   = "BarBack/Track";
        public const string PathTrail   = "BarBack/Trail";
        public const string PathFill    = "BarBack/Fill";
        public const string PathNumbers = "BarBack/Numbers";

        private sealed class Row
        {
            public Entity body;
            public RectTransform frameRect;
            public Vector2 frameHome;
            public Image frame;
            public Image face;
            public Text initial;
            public Image flash;
            public SlantedBar fill;
            public SlantedBar trail;
            public Text numbers;

            /// <summary>막대 비율. 처음 한 번은 잔상 없이 바로 맞춘다.</summary>
            public float shown = -1f;

            /// <summary>잔상 막대 비율. 깎일 땐 늦게 따라오고, 찰 땐 앞서 간다.</summary>
            public float trailRatio;

            public float holdLeft;
            public float flashLeft;
            public float shakeLeft;
        }

        private readonly List<Row> rows = new List<Row>();

        /// <summary>줄을 이미 지었는가. 로스터가 <c>Start</c>에서 만들어지므로 한 프레임 미룬다.</summary>
        private bool built;

        /// <summary>배선이 빈 채로 돌 때 경고를 한 번만 낸다.</summary>
        private bool warned;

        /// <summary>테스트와 다른 HUD가 읽는다. 지어진 줄 수 — 빈 편성 칸은 세지 않는다.</summary>
        public int RowCount => rows.Count;

        /// <summary>
        /// 막대 테두리 · 홈 색을 규격에 맞춘다 — 밑의 총공격 게이지와 같은 색이어야 한 묶음으로 읽힌다
        /// (<see cref="BulletTimeGaugeWidget"/>도 테두리 <see cref="UiTheme.OutlineColor"/>, 홈 <see cref="UiTheme.InkDeep"/>).
        /// 숫자는 굵게. 자리 · 크기는 프리팹 그대로.
        /// </summary>
        public static void ApplyTheme(Component root)
        {
            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (g.name == "BarBack") g.color = BarBorderColor;
                else if (g.name == "Track") g.color = UiTheme.InkDeep;
            }

            foreach (Text t in root.GetComponentsInChildren<Text>(true))
                if (t.name == "Numbers") t.fontStyle = FontStyle.Bold;
        }

        private void Awake()
        {
            // 프리팹에 박힌 글자에도 글꼴 · 외곽선 규격을 입힌다(UiTheme).
            UiKit.Dress(this);
            ApplyTheme(this);
            if (roster == null) roster = GetComponent<PartyRoster>();
        }

        /// <summary>
        /// <b>Start가 아니라 LateUpdate에서 짓는다.</b> <see cref="PartyRoster"/>가
        /// 로스터를 자기 <c>Start</c>에서 만드는데, 같은 오브젝트에 붙은 컴포넌트끼리는
        /// <c>Start</c> 순서가 보장되지 않는다 — Start에서 지으면 로스터가 빈 프레임에
        /// 걸려 줄이 하나도 안 생기는 판이 나온다.
        /// </summary>
        private void LateUpdate()
        {
            if (!show || roster == null) return;

            if (panel == null || rowTemplate == null)
            {
                Warn();
                return;
            }

            if (!built)
            {
                if (roster.Roster.Count == 0) return;

                Build();
                built = true;
            }

            panel.gameObject.SetActive(true);

            // 불릿타임으로 timeScale이 떨어져도 HUD 연출은 제 속도로 돈다.
            Refresh(Time.unscaledDeltaTime);
        }

        /// <summary>배선이 비면 조용히 아무것도 안 하는 대신 한 번 알린다.</summary>
        private void Warn()
        {
            if (warned) return;

            warned = true;
            Debug.LogWarning(
                "[PartyHealthHUD] 배선이 비어 있다 — 파티 체력 줄이 안 뜬다. " +
                "BattleInput 프리팹의 PartyHealthCanvas 배선을 확인할 것.", this);
        }

        // ── 짓기 ────────────────────────────────────────

        private void Build()
        {
            IReadOnlyList<Entity> members = roster.Roster;
            float rowHeight = rowTemplate.rect.height;
            float y = 0f;

            // 아래에서 위로 쌓는다. 로스터 0번(주인공)이 맨 위에 오도록 역순으로 만든다 —
            // 로스터 순서가 화면에서도 위에서 아래로 읽혀야 한다.
            for (int i = members.Count - 1; i >= 0; i--)
            {
                // 빈 편성 칸이거나 이번 런에서 전사해 슬롯이 지워진 자리다. 줄을 안 만든다 —
                // "없는 동료"를 회색 줄로 남겨 두면 부활할 수 있다는 인상을 준다.
                if (members[i] == null) continue;

                // 플레이어(주인공) 줄만 띄운다. 동료 체력바는 숨긴다.
                if (!(members[i] is Player)) continue;

                rows.Add(NewRow(members[i], y));
                y += rowHeight + RowGap;
            }

            panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Max(0f, y - RowGap));
        }

        /// <summary>
        /// 줄 하나를 <see cref="rowTemplate"/>에서 복제해 <paramref name="y"/> 높이에 앉힌다.
        ///
        /// 조각은 이름으로 찾는다(<see cref="PartPaths"/>) — 줄마다 뷰 컴포넌트를 하나씩 두는 것보다 싸고,
        /// 이름이 어긋나면 <c>BattleInputPrefabTests</c>가 잡는다.
        /// </summary>
        private Row NewRow(Entity body, float y)
        {
            RectTransform r = Instantiate(rowTemplate, panel);
            r.gameObject.SetActive(true);
            r.name = body.name;
            r.anchoredPosition = new Vector2(0f, y);

            Text numbers = r.Find(PathNumbers).GetComponent<Text>();
            numbers.gameObject.SetActive(showNumbers);

            var frameRect = (RectTransform)r.Find(PathFrame);
            Image face = r.Find(PathFace).GetComponent<Image>();

            var row = new Row
            {
                body = body,
                frameRect = frameRect,
                frameHome = frameRect.anchoredPosition,
                frame = frameRect.GetComponent<Image>(),
                face = face,
                flash = r.Find(PathFlash).GetComponent<Image>(),
                fill = r.Find(PathFill).GetComponent<SlantedBar>(),
                trail = r.Find(PathTrail).GetComponent<SlantedBar>(),
                numbers = showNumbers ? numbers : null,
            };

            Sprite portrait = PortraitOf(body);
            face.sprite = portrait;
            face.preserveAspect = true;

            // 얼굴 그림이 없으면 직업 색 바탕에 이름 첫 글자를 띄운다 — 빈 칸보다 낫다.
            if (portrait == null) row.initial = AddInitial(face, NameOf(body));

            return row;
        }

        private static Text AddInitial(Image face, string name)
        {
            Text t = UiKit.NewText(face.transform, "Initial", 56, UiTheme.TextOnLight.WithAlpha(0.9f), FontStyle.Bold);
            UiKit.Stretch((RectTransform)t.transform);
            t.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            return t;
        }

        /// <summary>직업 색. 주인공은 직업이 없으므로 금색으로 따로 둔다.</summary>
        private static Color TintOf(Entity body)
            => body is Ally ally ? SkillCutinUI.RoleColor(ally.Role) : HeroTint;

        /// <summary>표가 있으면 그 이름, 없으면 오브젝트 이름. 표는 편성 화면과 같은 말을 쓴다.</summary>
        private static string NameOf(Entity body)
        {
            if (body is Ally ally && ally.Data != null) return ally.Data.Label;
            if (body is Player hero && hero.Data != null) return hero.Data.Label;

            return body.name;
        }

        /// <summary>초상화. 주인공은 표(<see cref="PlayerData.portrait"/>), 동료는 몸이 쥔 얼굴.</summary>
        private static Sprite PortraitOf(Entity body)
        {
            if (body is Ally ally) return ally.Portrait;
            if (body is Player hero && hero.Data != null) return hero.Data.portrait;

            return null;
        }

        private static string RoleOf(Entity body)
            => body is Ally ally ? RoleNames.Of(ally.Role) : "주인공";

        // ── 갱신 ────────────────────────────────────────

        /// <summary>
        /// 매 프레임 다시 그린다. <see cref="Energy.OnChanged"/>를 구독하지 않는 이유는
        /// 몸마다 구독을 붙였다 떼는 관리가 줄 몇 개를 그리는 비용보다 비싸고,
        /// 어시스트로 몸이 꺼졌다 켜지는 이 게임에서는 해제 시점이 특히 새기 쉽기 때문이다.
        /// 피격도 같은 이유로 "지난 프레임보다 비율이 줄었나"로 알아챈다.
        /// </summary>
        private void Refresh(float dt)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                if (row.body == null) continue;

                Energy hp = row.body.Combat != null ? row.body.Combat.Health : null;
                bool dead = row.body.Combat != null && row.body.Combat.IsDead;
                float ratio = dead || hp == null ? 0f : Mathf.Clamp01(hp.Ratio);

                TrackChange(row, ratio);
                Tick(row, ratio, dt);

                Paint(row, hp, ratio, dead);
            }
        }

        /// <summary>비율이 바뀐 순간을 잡아 잔상 · 플래시 · 흔들림을 건다.</summary>
        private static void TrackChange(Row row, float ratio)
        {
            if (row.shown < 0f)
            {
                row.shown = row.trailRatio = ratio;
                return;
            }

            if (ratio < row.shown - 0.0001f)
            {
                // 연타를 맞는 동안엔 잔상이 계속 멈춰 있어야 "한 번에 얼마 깎였나"가 쌓여 보인다.
                row.holdLeft = TrailHold;
                row.flashLeft = FlashTime;
                row.shakeLeft = ShakeTime;
            }
            else if (ratio > row.shown + 0.0001f)
            {
                // 회복은 잔상 없이 바로 찬다. 잔상이 남아 있었다면 새 값 밑으로 묻힌다.
                row.trailRatio = Mathf.Max(row.trailRatio, ratio);
            }

            row.shown = ratio;
        }

        private static void Tick(Row row, float ratio, float dt)
        {
            if (row.holdLeft > 0f) row.holdLeft -= dt;
            else if (row.trailRatio > ratio) row.trailRatio = Mathf.Max(ratio, row.trailRatio - TrailSpeed * dt);

            if (row.flashLeft > 0f) row.flashLeft -= dt;
            if (row.shakeLeft > 0f) row.shakeLeft -= dt;
        }

        private static void Paint(Row row, Energy hp, float ratio, bool dead)
        {
            // 폭이 아니라 SlantedBar.Fill 로 자른다 — 폭을 줄이면 꺾인 끝이 채움 끝을 따라 움직인다.
            row.fill.Fill = ratio;
            row.trail.Fill = Mathf.Max(ratio, row.trailRatio);
            row.trail.color = TrailColor;
            row.fill.color = FillColor();

            // ── 초상화 ──
            // 위기 · 피격은 두 겹으로 말한다 — 틀 안 붉은 막(Flash, 얼굴 뒤)과 얼굴 자체의 붉은 물.
            // 얼굴이 틀 위로 삐져나와 있어 막만으로는 머리 쪽이 안 물든다.
            float flash = 0.6f * Mathf.Clamp01(row.flashLeft / FlashTime);
            if (!dead && ratio <= DangerRatio)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * DangerPulseHz * Mathf.PI * 2f);
                flash = Mathf.Max(flash, 0.28f * pulse);
            }
            row.flash.color = UiTheme.Hp.WithAlpha(flash);

            Color baseFace = row.face.sprite != null ? Color.white : TintOf(row.body);
            row.face.color = dead ? FaceDead : Color.Lerp(baseFace, UiTheme.Hp, flash * 0.6f);

            Vector2 shake = Vector2.zero;
            if (row.shakeLeft > 0f)
            {
                float k = row.shakeLeft / ShakeTime;
                shake = Random.insideUnitCircle * (ShakePixels * k);
            }
            row.frameRect.anchoredPosition = row.frameHome + shake;

            // ── 글자 ──
            if (row.numbers == null) return;

            row.numbers.text = dead
                ? "전사"
                : (hp != null ? $"{ShownHp(hp.CurValue)} / {ShownHp(hp.MaxValue)}"
                              : RoleOf(row.body));
            row.numbers.color = NumberText;
        }

        /// <summary>
        /// 화면에 적는 체력 수. 올림이라 0.3이 남아도 1로 보여 "살아 있다"가 읽힌다.
        /// 다만 float 오차(60이 60.000004로 저장되는 것)까지 올리면 61로 보이므로 오차만큼 깎고 올린다.
        /// </summary>
        public static int ShownHp(float value) => Mathf.CeilToInt(value - 0.001f);

        /// <summary>
        /// 막대는 <b>단색</b>이다. 체력에 따라 색을 바꾸지 않는다 — 위기는 초상화의 붉은 맥동이 말한다.
        /// </summary>
        private static Color FillColor() => BarColor;
    }
}
