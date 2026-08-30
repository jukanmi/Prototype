using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 화면 <b>우측 하단</b>의 파티 체력. 태그 로스터 전원을 한 줄씩 세운다.
    ///
    /// <b>왜 전원인가.</b> 교대(F)는 로스터를 순환하므로, 바꾸기 전에 누가 성한지 알아야
    /// 고를 수가 있다. 조작 중인 한 명만 띄우면 "바꿔 봐야 아는" 상태가 된다.
    /// 같은 이유로 <b>지금 조작 중인 줄</b>을 표시한다 — 그게 없으면 다섯 줄이 그냥 목록이다.
    ///
    /// 동료 사망이 런 끝까지 영구가 된 뒤로는 더 중요해졌다(<see cref="PartyState"/>).
    /// 누가 위험한지가 그 판의 손실이 아니라 <b>런 전체의 손실</b>을 정하기 때문이다.
    ///
    /// 캔버스를 코드로 짓는다 — 이 프로젝트의 전투 UI가 전부 그렇고(씬 배선 0),
    /// 배치는 <see cref="ComboBoardUI"/>(하단 중앙) · <see cref="ComboDamageHUD"/>(우측 중앙)와
    /// 겹치지 않는 자리를 골랐다.
    /// </summary>
    public class PartyHealthHUD : MonoBehaviour
    {
        /// <summary>ComboDamageHUD(5) · SkillCutinUI(10)보다 위, DeckInspectorUI(20)보다 아래.</summary>
        private const int SortingOrder = 15;

        private const float PanelWidth = 300f;
        private const float RowHeight = 30f;
        private const float RowGap = 4f;
        private const float ScreenMargin = 24f;
        private const float BandWidth = 5f;
        private const float BarHeight = 8f;
        private const float NameWidth = 120f;

        private static readonly Color RowIdle    = new Color(0.10f, 0.11f, 0.13f, 0.78f);
        private static readonly Color RowCurrent = new Color(0.18f, 0.21f, 0.26f, 0.92f);
        private static readonly Color BarBack    = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color BarAlive   = new Color(0.44f, 0.78f, 0.42f);
        private static readonly Color BarHurt    = new Color(0.86f, 0.72f, 0.28f);
        private static readonly Color BarDanger  = new Color(0.84f, 0.30f, 0.26f);
        private static readonly Color NameAlive  = new Color(0.92f, 0.93f, 0.95f);
        private static readonly Color NameDead   = new Color(0.45f, 0.46f, 0.50f);

        /// <summary>체력이 이 밑으로 내려가면 막대가 색을 바꾼다.</summary>
        private const float HurtRatio = 0.5f;
        private const float DangerRatio = 0.25f;

        [Tooltip("비우면 같은 오브젝트에서 찾는다. 로스터의 주인이다.")]
        [SerializeField] private TagSwapController swap;

        [Tooltip("끄면 패널을 아예 만들지 않는다. 시연 녹화 때 화면을 비우는 용도.")]
        [SerializeField] private bool show = true;

        [Tooltip("체력 수치(120 / 200)를 막대 위에 함께 적는다.")]
        [SerializeField] private bool showNumbers = true;

        private sealed class Row
        {
            public Entity body;
            public Image background;
            public Image band;
            public Image fill;
            public Text label;
            public Text numbers;
            public RectTransform fillRect;
        }

        private readonly List<Row> rows = new List<Row>();
        private RectTransform panel;

        /// <summary>줄을 이미 지었는가. 로스터가 <c>Start</c>에서 만들어지므로 한 프레임 미룬다.</summary>
        private bool built;

        /// <summary>테스트와 다른 HUD가 읽는다. 지어진 줄 수 — 빈 편성 칸은 세지 않는다.</summary>
        public int RowCount => rows.Count;

        private void Awake()
        {
            if (swap == null) swap = GetComponent<TagSwapController>();
        }

        /// <summary>
        /// <b>Start가 아니라 LateUpdate에서 짓는다.</b> <see cref="TagSwapController"/>가
        /// 로스터를 자기 <c>Start</c>에서 만드는데, 같은 오브젝트에 붙은 컴포넌트끼리는
        /// <c>Start</c> 순서가 보장되지 않는다 — Start에서 지으면 로스터가 빈 프레임에
        /// 걸려 줄이 하나도 안 생기는 판이 나온다.
        /// </summary>
        private void LateUpdate()
        {
            if (!show || swap == null) return;

            if (!built)
            {
                if (swap.Roster.Count == 0) return;

                Build();
                built = true;
            }

            Refresh();
        }

        // ── 짓기 ────────────────────────────────────────

        private void Build()
        {
            var canvasGo = new GameObject("PartyHealthHUD", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 클릭을 안 받는다. GraphicRaycaster를 안 붙이면 이 패널이 손패 클릭을 가리지 않는다.
            panel = UiFactory.NewRect(canvasGo.transform, "Panel");
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = new Vector2(-ScreenMargin, ScreenMargin);

            IReadOnlyList<Entity> roster = swap.Roster;
            float y = 0f;

            // 아래에서 위로 쌓는다. 로스터 0번(주인공)이 맨 위에 오도록 역순으로 만든다 —
            // 교대 순환 순서가 화면에서도 위에서 아래로 읽혀야 한다.
            for (int i = roster.Count - 1; i >= 0; i--)
            {
                // 빈 편성 칸이거나 이번 런에서 전사해 슬롯이 지워진 자리다. 줄을 안 만든다 —
                // "없는 동료"를 회색 줄로 남겨 두면 부활할 수 있다는 인상을 준다.
                if (roster[i] == null) continue;

                rows.Add(NewRow(roster[i], ref y));
            }

            panel.sizeDelta = new Vector2(PanelWidth, Mathf.Max(0f, y - RowGap));
        }

        private Row NewRow(Entity body, ref float y)
        {
            Image bg = UiFactory.NewImage(panel, body.name, RowIdle);
            RectTransform r = bg.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
            r.pivot = new Vector2(0f, 0f);
            r.anchoredPosition = new Vector2(0f, y);
            r.sizeDelta = new Vector2(PanelWidth, RowHeight);

            y += RowHeight + RowGap;

            // 직업 색 띠. 초상화가 없어도 다섯을 구분할 수 있어야 한다 —
            // 컷인이 쓰는 것과 같은 색이라 두 화면이 같은 말을 한다.
            Image band = UiFactory.NewImage(r, "Band", TintOf(body));
            RectTransform b = band.rectTransform;
            b.anchorMin = new Vector2(0f, 0f);
            b.anchorMax = new Vector2(0f, 1f);
            b.pivot = new Vector2(0f, 0.5f);
            b.sizeDelta = new Vector2(BandWidth, 0f);
            b.anchoredPosition = Vector2.zero;

            Text label = UiFactory.NewText(r, "Name", 15, NameAlive, FontStyle.Normal);
            RectTransform l = label.rectTransform;
            l.anchorMin = new Vector2(0f, 0f);
            l.anchorMax = new Vector2(0f, 1f);
            l.pivot = new Vector2(0f, 0.5f);
            l.anchoredPosition = new Vector2(BandWidth + 6f, 0f);
            l.sizeDelta = new Vector2(NameWidth, 0f);
            label.alignment = TextAnchor.MiddleLeft;

            // 막대는 이름 오른쪽의 남는 폭을 전부 쓴다.
            float barLeft = BandWidth + 6f + NameWidth + 6f;

            Image back = UiFactory.NewImage(r, "BarBack", BarBack);
            RectTransform k = back.rectTransform;
            k.anchorMin = new Vector2(0f, 0.5f);
            k.anchorMax = new Vector2(0f, 0.5f);
            k.pivot = new Vector2(0f, 0.5f);
            k.anchoredPosition = new Vector2(barLeft, 0f);
            k.sizeDelta = new Vector2(PanelWidth - barLeft - 8f, BarHeight);

            // 왼쪽을 고정하고 오른쪽 앵커만 움직여 폭을 만든다.
            // RecentHitEnemyHUD가 쓰는 것과 같은 방식이라 두 막대가 같은 규칙으로 움직인다.
            Image fill = UiFactory.NewImage(k, "Fill", BarAlive);
            RectTransform f = fill.rectTransform;
            f.anchorMin = Vector2.zero;
            f.anchorMax = Vector2.one;
            f.offsetMin = Vector2.zero;
            f.offsetMax = Vector2.zero;

            Text numbers = null;
            if (showNumbers)
            {
                numbers = UiFactory.NewText(r, "Numbers", 12, NameDead, FontStyle.Normal);
                RectTransform n = numbers.rectTransform;
                n.anchorMin = new Vector2(1f, 0f);
                n.anchorMax = new Vector2(1f, 1f);
                n.pivot = new Vector2(1f, 0.5f);
                n.anchoredPosition = new Vector2(-8f, -7f);
                n.sizeDelta = new Vector2(PanelWidth - barLeft, 0f);
                numbers.alignment = TextAnchor.MiddleRight;
            }

            return new Row
            {
                body = body,
                background = bg,
                band = band,
                fill = fill,
                fillRect = f,
                label = label,
                numbers = numbers,
            };
        }

        /// <summary>직업 색. 주인공은 직업이 없으므로 금색으로 따로 둔다.</summary>
        private static Color TintOf(Entity body)
            => body is Ally ally ? SkillCutinUI.RoleColor(ally.Role) : new Color32(0xC8, 0xA0, 0x40, 0xFF);

        /// <summary>표가 있으면 그 이름, 없으면 오브젝트 이름. 표는 편성 화면과 같은 말을 쓴다.</summary>
        private static string NameOf(Entity body)
        {
            if (body is Ally ally && ally.Data != null) return ally.Data.Label;
            if (body is Player hero && hero.Data != null) return hero.Data.Label;

            return body.name;
        }

        private static string RoleOf(Entity body)
            => body is Ally ally ? RoleNames.Of(ally.Role) : "주인공";

        // ── 갱신 ────────────────────────────────────────

        /// <summary>
        /// 매 프레임 다시 그린다. <see cref="Energy.OnChanged"/>를 구독하지 않는 이유는
        /// 몸마다 구독을 붙였다 떼는 관리가 다섯 줄을 그리는 비용보다 비싸고,
        /// 교대로 몸이 꺼졌다 켜지는 이 게임에서는 해제 시점이 특히 새기 쉽기 때문이다.
        /// </summary>
        private void Refresh()
        {
            Entity current = swap.Current;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                if (row.body == null) continue;

                Energy hp = row.body.Combat != null ? row.body.Combat.Health : null;
                bool dead = row.body.Combat != null && row.body.Combat.IsDead;
                float ratio = dead || hp == null ? 0f : Mathf.Clamp01(hp.Ratio);

                row.fillRect.anchorMax = new Vector2(ratio, 1f);
                row.fill.color = FillColor(ratio, dead);

                bool isCurrent = ReferenceEquals(row.body, current);
                row.background.color = isCurrent ? RowCurrent : RowIdle;

                // 조작 중인 줄에만 표식을 단다. 다섯 줄이 그냥 목록이 되지 않게 하는 유일한 단서다.
                row.label.text = (isCurrent ? "▸ " : "   ") + NameOf(row.body);
                row.label.color = dead ? NameDead : NameAlive;

                // 죽은 줄은 색까지 빼서 목록에서 물러나게 한다 — 교대 후보가 아니다.
                row.band.color = dead ? NameDead : TintOf(row.body);

                if (row.numbers == null) continue;

                row.numbers.text = dead
                    ? "전사"
                    : (hp != null ? $"{Mathf.CeilToInt(hp.CurValue)} / {Mathf.CeilToInt(hp.MaxValue)}"
                                  : RoleOf(row.body));

                row.numbers.color = dead ? BarDanger : NameDead;
            }
        }

        private static Color FillColor(float ratio, bool dead)
        {
            if (dead) return BarDanger;
            if (ratio <= DangerRatio) return BarDanger;
            if (ratio <= HurtRatio) return BarHurt;

            return BarAlive;
        }
    }
}
