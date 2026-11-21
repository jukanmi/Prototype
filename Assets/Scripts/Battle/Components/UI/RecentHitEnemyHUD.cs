using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 적 체력 바. <b>아군에게 맞은 적의 발밑</b>에 작은 막대가 붙고, 한동안 안 맞으면 사라진다.
    ///
    /// 적마다 막대가 하나씩 따로 뜬다 — 광역기로 여럿을 맞히면 여럿이 동시에 켜진다.
    /// 막대 하나의 모양은 프리팹의 템플릿(<see cref="panel"/>)이 쥐고, 이 컴포넌트는 그걸 복제해
    /// 적을 따라다니게 하고 값만 밀어 넣는다.
    ///
    /// <b>화면은 프리팹이 쥔다</b> — 이 컴포넌트는 <c>CombatManager</c> 프리팹의
    /// <c>RecentHitEnemyCanvas</c>에 붙어 있다. 막대 모양 · 색을 바꾸려면 프리팹을 연다.
    /// </summary>
    public class RecentHitEnemyHUD : MonoBehaviour
    {
        /// <summary>마지막으로 맞은 뒤 막대가 떠 있는 시간.</summary>
        public const float VisibleDuration = 3f;

        /// <summary>사라질 때 흐려지는 시간. <see cref="VisibleDuration"/>의 끝자락이다.</summary>
        private const float FadeTime = 0.35f;

        /// <summary>발밑(바닥 좌표)에서 막대 윗변까지, 화면 아래로 내리는 거리(캔버스 단위). 그림자를 피한다.</summary>
        public const float FootGap = 14f;

        private const float TrailHold = 0.35f;
        private const float TrailSpeed = 1.2f;
        private const float FlashTime = 0.15f;

        [Header("배선")]
        [Tooltip("막대 하나의 원본. 꺼진 채로 두고 적마다 복제해 쓴다.")]
        [SerializeField] private GameObject panel;

        [Tooltip("원본 안의 적 이름. 강화 개체일 때만 켠다.")]
        [SerializeField] private Text nameLabel;

        [Tooltip("원본 안의 체력 채움. 오른쪽 앵커를 움직여 폭을 만든다.")]
        [SerializeField] private RectTransform healthFill;

        [Tooltip("원본 안의 잔상 막대. 깎인 만큼이 잠깐 남았다가 따라 줄어든다. 비워도 된다.")]
        [SerializeField] private RectTransform healthTrail;

        private sealed class Bar
        {
            public Combat target;
            public RectTransform root;
            public CanvasGroup group;
            public Image border;
            public Color borderColor;
            public Text name;
            public RectTransform fill;
            public RectTransform trail;
            /// <summary>막대 밑의 "현재/최대". 코드가 복제본마다 붙인다(기획서 5.2).</summary>
            public Text numbers;
            public float expiresAt;
            public float shown;
            public float trailRatio;
            public float holdLeft;
            public float flashLeft;
            public Action onDead;
            public Action<Energy> onChanged;
        }

        private readonly Dictionary<Combat, Bar> bars = new Dictionary<Combat, Bar>();
        private readonly List<Bar> pool = new List<Bar>();
        private readonly List<Combat> expired = new List<Combat>();

        // 원본 안에서 조각을 찾는 상대 경로. 복제본에서 같은 경로로 집는다.
        private string namePath;
        private string fillPath;
        private string trailPath;

        private RectTransform canvasRect;
        private Combat currentTarget;
        private Color defaultNameColor = Color.white;

        /// <summary>배선이 빈 채로 돌 때 경고를 한 번만 낸다.</summary>
        private bool warned;

        /// <summary>가장 최근에 맞은 적. 테스트와 다른 HUD가 읽는다.</summary>
        public Combat CurrentTarget => currentTarget;

        /// <summary>막대가 하나라도 떠 있는가.</summary>
        public bool IsVisible => bars.Count > 0;

        /// <summary>지금 떠 있는 막대 수.</summary>
        public int VisibleCount => bars.Count;

        /// <summary>이 적의 막대가 떠 있는가.</summary>
        public bool IsShowing(Combat target) => target != null && bars.ContainsKey(target);

        /// <summary>가장 최근에 맞은 적의 막대 밑 숫자("현재/최대"). 테스트가 읽는다.</summary>
        public string CurrentNumbers
            => currentTarget != null && bars.TryGetValue(currentTarget, out Bar b) && b.numbers != null ? b.numbers.text : null;

        /// <summary>막대 밑 숫자 줄의 높이.</summary>
        public const float NumbersHeight = 16f;

        /// <summary>"27/165". 소수 체력은 올림 — 0.4 남은 적이 "0"으로 보이면 죽은 줄 안다.</summary>
        public static string FormatNumbers(float cur, float max)
            => $"{Mathf.CeilToInt(Mathf.Max(0f, cur))}/{Mathf.CeilToInt(Mathf.Max(0f, max))}";

        /// <summary>가장 최근에 맞은 적의 막대가 그리는 비율. 0~1.</summary>
        public float HealthFillRatio
            => currentTarget != null && bars.TryGetValue(currentTarget, out Bar b) ? b.fill.anchorMax.x : 0f;

        private void Awake()
        {
            // 프리팹에 박힌 글자에도 글꼴 · 외곽선 규격을 입힌다(UiTheme).
            UiKit.Dress(this);
            EnsureInit();
        }

        /// <summary>
        /// 원본에서 조각 경로를 뽑아 둔다. Awake에서 부르지만 <see cref="Track"/>도 부른다 —
        /// 에디트모드 테스트처럼 Awake가 안 도는 판에서도 막대가 떠야 한다.
        /// </summary>
        private bool EnsureInit()
        {
            if (fillPath != null) return true;

            canvasRect = (RectTransform)transform;

            if (panel == null || nameLabel == null || healthFill == null)
            {
                Warn();
                return false;
            }

            defaultNameColor = nameLabel.color;

            Transform root = panel.transform;
            namePath = PathFrom(root, nameLabel.transform);
            fillPath = PathFrom(root, healthFill);
            trailPath = healthTrail != null ? PathFrom(root, healthTrail) : null;

            // 원본은 복제용이다. 프리팹에서 켜 둬도(배치를 보려고) 여기서 끈다.
            panel.SetActive(false);
            return true;
        }

        private void Warn()
        {
            if (warned) return;

            warned = true;
            Debug.LogWarning(
                "[RecentHitEnemyHUD] 배선이 비어 있다 — 적 체력 바가 안 뜬다. " +
                "CombatManager 프리팹의 RecentHitEnemyCanvas 배선을 확인할 것.", this);
        }

        private void OnEnable() => CombatEvents.OnAnyHitLanded += Track;

        private void OnDisable()
        {
            CombatEvents.OnAnyHitLanded -= Track;
            ClearAll();
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            TickExpiry(now);

            Camera cam = BeltScroll.Cam;
            float dt = Time.unscaledDeltaTime;

            foreach (Bar bar in bars.Values)
            {
                Animate(bar, dt);
                Place(bar, cam);

                float left = bar.expiresAt - now;
                bar.group.alpha = Mathf.Clamp01(left / FadeTime);
            }
        }

        /// <summary>
        /// 표시 시간 만료 검사. 현재 시각을 인자로 받는다 —
        /// 에디트모드 테스트가 3초를 실제로 기다리지 않고 앞당길 수 있어야 한다.
        /// 사라진(파괴 · 비활성) 적의 막대도 여기서 거둔다.
        /// </summary>
        public void TickExpiry(float unscaledNow)
        {
            expired.Clear();

            foreach (KeyValuePair<Combat, Bar> pair in bars)
            {
                Combat t = pair.Key;
                if (t == null || !t.isActiveAndEnabled || t.IsDead || unscaledNow >= pair.Value.expiresAt)
                    expired.Add(t);
            }

            for (int i = 0; i < expired.Count; i++) Release(expired[i]);
        }

        public void Track(Combat attacker, Combat target)
        {
            if (attacker?.Owner?.Faction != Faction.Ally ||
                target?.Owner?.Faction != Faction.Enemy || target.IsDead) return;

            if (!EnsureInit()) return;

            if (!bars.TryGetValue(target, out Bar bar))
            {
                bar = Acquire(target);
                bars.Add(target, bar);
            }

            currentTarget = target;
            bar.expiresAt = Time.unscaledTime + VisibleDuration;
            bar.flashLeft = FlashTime;
            bar.group.alpha = 1f;

            // 막 뜬 막대는 다음 LateUpdate 전에 한 프레임 엉뚱한 자리에 보일 수 있다. 바로 앉힌다.
            Place(bar, BeltScroll.Cam);
            Refresh(bar);
        }

        // ── 막대 하나 ─────────────────────────────────────

        private Bar Acquire(Combat target)
        {
            Bar bar;
            if (pool.Count > 0)
            {
                bar = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
            }
            else
            {
                bar = Build();
            }

            bar.target = target;
            bar.shown = -1f;
            bar.holdLeft = 0f;
            bar.root.gameObject.SetActive(true);
            bar.root.SetAsLastSibling();

            // 강화 개체는 글자로도 말한다. 발밑 고리는 모양 신호라 "강화"라는 뜻까지는 여기가 전한다.
            bool elite = target.Owner is Enemy enemy && enemy.IsElite;
            bar.name.gameObject.SetActive(elite);
            bar.name.text = EliteMarkRules.HudName(target.name, elite);
            bar.name.color = elite ? EliteMarkRules.AuraColor : defaultNameColor;

            bar.onDead = () => Release(target);
            bar.onChanged = _ => Refresh(bar);
            target.OnDead += bar.onDead;
            target.Health.OnChanged += bar.onChanged;

            return bar;
        }

        private Bar Build()
        {
            GameObject go = Instantiate(panel, panel.transform.parent);
            go.name = "EnemyBar";

            var root = (RectTransform)go.transform;
            // 캔버스 가운데를 원점으로 두면 ScreenPointToLocalPointInRectangle 결과를 그대로 쓴다.
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);

            CanvasGroup group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Image border = go.GetComponent<Image>();

            // 막대 바로 밑에 "현재/최대". 원본(프리팹)은 건드리지 않고 복제본에만 붙인다.
            Text numbers = UiKit.NewText(root, "Numbers", UiTheme.Size.Micro, UiTheme.TextMain, FontStyle.Bold);
            var nr = numbers.rectTransform;
            nr.anchorMin = new Vector2(0f, 0f);
            nr.anchorMax = new Vector2(1f, 0f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.sizeDelta = new Vector2(0f, NumbersHeight);
            nr.anchoredPosition = new Vector2(0f, -2f);

            return new Bar
            {
                numbers = numbers,
                root = root,
                group = group,
                border = border,
                borderColor = border != null ? border.color : Color.black,
                name = root.Find(namePath).GetComponent<Text>(),
                fill = (RectTransform)root.Find(fillPath),
                trail = trailPath != null ? (RectTransform)root.Find(trailPath) : null,
            };
        }

        private void Release(Combat target)
        {
            if (target is null || !bars.TryGetValue(target, out Bar bar)) return;

            bars.Remove(target);
            Unsubscribe(bar);

            bar.target = null;
            bar.root.gameObject.SetActive(false);
            pool.Add(bar);

            if (ReferenceEquals(currentTarget, target)) currentTarget = null;
        }

        private void ClearAll()
        {
            expired.Clear();
            expired.AddRange(bars.Keys);
            for (int i = 0; i < expired.Count; i++) Release(expired[i]);
            currentTarget = null;
        }

        private static void Unsubscribe(Bar bar)
        {
            // 파괴된 적이라도 C# 객체는 살아 있어 이벤트를 뗄 수 있다.
            Combat t = bar.target;
            if (t is null) return;

            if (bar.onDead != null) t.OnDead -= bar.onDead;
            if (bar.onChanged != null && t.Health != null) t.Health.OnChanged -= bar.onChanged;
            bar.onDead = null;
            bar.onChanged = null;
        }

        // ── 그리기 ───────────────────────────────────────

        private static void Refresh(Bar bar)
        {
            if (bar.target == null) return;

            float ratio = bar.target.IsDead ? 0f : Mathf.Clamp01(bar.target.Health.Ratio);

            if (bar.shown < 0f)
            {
                bar.shown = bar.trailRatio = ratio;
            }
            else if (ratio < bar.shown)
            {
                bar.holdLeft = TrailHold;
            }

            bar.trailRatio = Mathf.Max(bar.trailRatio, ratio);
            bar.shown = ratio;

            bar.fill.anchorMax = new Vector2(ratio, 1f);
            if (bar.trail != null) bar.trail.anchorMax = new Vector2(bar.trailRatio, 1f);

            if (bar.numbers != null)
            {
                Energy hp = bar.target.Health;
                bar.numbers.text = FormatNumbers(bar.target.IsDead ? 0f : hp.CurValue, hp.MaxValue);
            }
        }

        private static void Animate(Bar bar, float dt)
        {
            if (bar.holdLeft > 0f) bar.holdLeft -= dt;
            else if (bar.trailRatio > bar.shown)
                bar.trailRatio = Mathf.Max(bar.shown, bar.trailRatio - TrailSpeed * dt);

            if (bar.trail != null) bar.trail.anchorMax = new Vector2(bar.trailRatio, 1f);

            // 맞은 순간 테두리가 하얗게 번쩍인다 — 여럿 중 "지금 맞은 놈"이 보이게.
            if (bar.flashLeft > 0f) bar.flashLeft -= dt;
            if (bar.border != null)
                bar.border.color = Color.Lerp(bar.borderColor, Color.white, Mathf.Clamp01(bar.flashLeft / FlashTime));
        }

        /// <summary>
        /// 적의 <b>바닥 좌표</b>(발밑)를 화면에 찍고 그보다 조금 아래에 앉힌다.
        /// 점프 · 띄우기 높이는 따라가지 않는다 — 막대가 그림자와 함께 바닥에 남아야
        /// 공중 콤보 중에도 눈이 위아래로 흔들리지 않는다.
        /// </summary>
        private void Place(Bar bar, Camera cam)
        {
            if (cam == null || bar.target == null) return;

            Entity owner = bar.target.Owner;
            Vector3 ground = owner != null && owner.Physics != null
                ? owner.Physics.GroundPosition
                : bar.target.transform.position;

            Vector3 screen = cam.WorldToScreenPoint(ground);

            // 카메라 뒤로 넘어간 적은 그리지 않는다(뒤집혀 화면에 찍힌다).
            bool front = screen.z > 0f;
            bar.root.gameObject.SetActive(front);
            if (!front) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                bar.root.anchoredPosition = local + new Vector2(0f, -FootGap);
        }

        private static string PathFrom(Transform root, Transform child)
        {
            if (child == root) return "";

            string path = child.name;
            for (Transform t = child.parent; t != null && t != root; t = t.parent)
                path = t.name + "/" + path;

            return path;
        }
    }
}
