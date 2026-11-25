using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 화면 <b>왼쪽</b>의 스킬 로그. 동료가 스킬을 쓸 때마다 <b>맨 위</b>에 한 줄이 끼고 기존 줄은 아래로 밀린다.
    /// 3초 동안 공격이 없으면 위 줄부터 차례로 빠진다. 줄의 대상이 죽으면 1초 뒤 그 줄만 빠진다.
    ///
    /// <code>
    /// ┌──┬────────────────────────┐
    /// │ 1│ 회오리 베기      ░▒▓눈매│   ← 순서 칸(상태색) · 스킬명 · 눈매 띠(왼쪽 끝이 투명으로 녹아 있다)
    /// └──┴────────────────────────┘
    /// </code>
    ///
    /// 계산은 <see cref="SkillLogModel"/>이 한다 — 여기는 그리기만 한다.
    /// 주인공(<see cref="Player"/>)과 적의 스킬은 받지 않는다. 동료(<see cref="Ally"/>)만.
    ///
    /// 줄은 코드가 짓는다. 프리팹(<c>CombatManager/SkillLogCanvas</c>)은 캔버스와 이 컴포넌트만 쥔다 —
    /// 줄 수가 36까지 늘었다 줄었다 하는 판이라 템플릿을 손으로 배선할 이득이 없다.
    /// 정렬 순서는 <see cref="UiLayer.SkillLog"/> — 좌하단 묶음보다 아래라, 줄이 거기까지 내려가도 묶음이 위에 그려진다.
    /// </summary>
    public class SkillLogHUD : MonoBehaviour
    {
        // ── 자리 (캔버스 단위, 1920×1080 세로 기준) ─────────

        /// <summary>화면 왼쪽 끝에서 줄 왼쪽 끝까지. 좌하단 묶음과 같은 여백.</summary>
        public const float MarginX = HudLayout.MarginX;

        /// <summary>화면 위 끝에서 첫 줄 윗변까지. 첫 줄이 화면 가운데(540)보다 약간 위에 온다.</summary>
        public const float MarginTop = 400f;

        public const float RowHeight = 38f;
        public const float RowGap = 2f;
        public const float RowWidth = 300f;

        /// <summary>순서 칸 한 변. 줄 높이와 같다.</summary>
        public const float ChipSize = RowHeight;

        /// <summary>눈매 띠 원본 비율(256×80).</summary>
        public const float FaceAspect = 256f / 80f;

        /// <summary>눈매 띠 폭. 줄 높이에 원본 비율을 맞춘다.</summary>
        public const float FaceWidth = RowHeight * FaceAspect;

        /// <summary>등장 · 퇴장 때 왼쪽으로 빠지는 거리.</summary>
        public const float SlideDistance = 48f;

        /// <summary>한 줄이 차지하는 세로 간격.</summary>
        public const float Pitch = RowHeight + RowGap;

        [Tooltip("끄면 로그를 아예 안 띄운다. 시연 녹화 때 화면을 비우는 용도.")]
        [SerializeField] private bool show = true;

        private sealed class Row
        {
            public RectTransform root;
            public CanvasGroup group;
            public Image chip;
            public Text number;
            public Text name;
            public Image face;
            public Text initial;
        }

        private readonly SkillLogModel model = new SkillLogModel();
        private readonly Dictionary<SkillLogModel.Entry, Row> rows = new Dictionary<SkillLogModel.Entry, Row>();
        private readonly List<Row> pool = new List<Row>();
        private readonly List<SkillLogModel.Entry> stale = new List<SkillLogModel.Entry>();

        public SkillLogModel Model => model;

        /// <summary>지금 화면에 붙은 줄 수.</summary>
        public int RowCount => rows.Count;

        private void OnEnable()
        {
            CombatEvents.OnAnySkillCast += HandleSkillCast;
            CombatEvents.OnAnyDamageDealt += HandleDamage;
            CombatEvents.OnAnyHitLanded += HandleHit;
        }

        private void OnDisable()
        {
            CombatEvents.OnAnySkillCast -= HandleSkillCast;
            CombatEvents.OnAnyDamageDealt -= HandleDamage;
            CombatEvents.OnAnyHitLanded -= HandleHit;
            Clear();
        }

        /// <summary>스케일된 dt — 시간이 멈춘 동안(총공격 선택)에는 3초가 흐르지 않는다.</summary>
        private void Update() => Tick(TimeControl.DeltaTime);

        /// <summary>한 프레임. 에디트모드 테스트가 스케줄러 없이 직접 부른다.</summary>
        public void Tick(float dt)
        {
            WatchTargets();
            model.Tick(dt);
            Redraw();
        }

        /// <summary>
        /// 대상이 죽었는지 본다. 이벤트가 아니라 매 프레임 묻는 이유: 죽은 적이 곧장 파괴되면
        /// 구독을 떼어 낼 자리가 없고, 파괴된 참조(<c>== null</c>)도 "죽었다"로 같이 잡힌다.
        /// </summary>
        private void WatchTargets()
        {
            foreach (SkillLogModel.Entry e in model.Entries)
            {
                if (e.TargetDead || e.Evicting) continue;
                if (ReferenceEquals(e.Target, null)) continue;   // 아직 대상 모름 — 첫 적중을 기다린다

                if (IsGone(e.Target)) model.NotifyTargetDead(e);
            }
        }

        /// <summary>바깥에서 통째로 지운다.</summary>
        public void Clear()
        {
            model.Reset();
            Redraw();
        }

        // ── 받기 ─────────────────────────────────────────

        /// <summary>동료의 시전만 받는다. 주인공 · 적은 <see cref="Ally"/>가 아니라 여기서 걸러진다.</summary>
        public void HandleSkillCast(Entity caster, SkillData data, Entity target = null)
        {
            if (!(caster is Ally ally) || data == null) return;

            // 적이 아닌 대상(아군 대상 버프 등)은 "죽으면 사라진다"의 대상이 아니다.
            if (target != null && target.Faction != Faction.Enemy) target = null;

            model.Add(ally, data, target);
            Redraw();
        }

        /// <summary>죽었거나 파괴됐거나 풀로 돌아가 꺼진 적. 판정 몸(Combat)이 없으면 죽을 수 없는 것으로 본다.</summary>
        public static bool IsGone(Entity target)
        {
            if (target == null) return true;   // 파괴됨
            if (!target.gameObject.activeInHierarchy) return true;
            Combat c = target.Combat;
            return c != null && c.IsDead;
        }

        /// <summary>
        /// 땅을 겨눈 스킬(장판 · 설치기)은 시전 때 대상이 없다. 그 시전자의 <b>가장 최근</b> 줄 중
        /// 대상이 빈 것에 첫 적중 상대를 채운다 — 그 적이 죽으면 그 줄이 빠진다.
        /// </summary>
        private void HandleHit(Combat attacker, Combat victim)
        {
            if (!(attacker?.Owner is Ally ally)) return;
            if (victim?.Owner == null || victim.Owner.Faction != Faction.Enemy) return;

            foreach (SkillLogModel.Entry e in model.Entries)
            {
                if (e.Caster != ally) continue;
                if (ReferenceEquals(e.Target, null)) e.Target = victim.Owner;
                return;   // 위가 최근 — 이 시전자의 가장 최근 줄만 본다
            }
        }

        /// <summary>아군 → 적 피해. 다단히트 · 설치기가 아직 때리는 중에 로그가 사라지지 않게 쉬는 시간을 되돌린다.</summary>
        private void HandleDamage(Combat attacker, Combat victim, float amount)
        {
            if (model.Count == 0) return;
            if (attacker?.Owner?.Faction != Faction.Ally) return;
            if (victim?.Owner?.Faction != Faction.Enemy) return;

            model.NotifyAttack();
        }

        // ── 그리기 ───────────────────────────────────────

        private void Redraw()
        {
            // 모델에서 빠진 줄을 풀로 돌린다.
            stale.Clear();
            foreach (SkillLogModel.Entry e in rows.Keys)
                if (!Contains(e)) stale.Add(e);
            foreach (SkillLogModel.Entry e in stale)
            {
                Row r = rows[e];
                rows.Remove(e);
                r.root.gameObject.SetActive(false);
                pool.Add(r);
            }

            if (!show) return;

            foreach (SkillLogModel.Entry e in model.Entries)
            {
                if (!rows.TryGetValue(e, out Row r))
                {
                    r = Rent();
                    Dress(r, e);
                    rows[e] = r;
                }

                Layout(r, e);
            }
        }

        private bool Contains(SkillLogModel.Entry e)
        {
            IReadOnlyList<SkillLogModel.Entry> list = model.Entries;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == e) return true;
            return false;
        }

        /// <summary>줄 하나의 가로 위치 — 등장은 ease-out으로 들어오고, 퇴장은 ease-in으로 빠진다.</summary>
        public static float RowX(float appear, float exit)
        {
            float a = Mathf.Clamp01(appear);
            float inEase = 1f - (1f - a) * (1f - a);
            float x = Mathf.Clamp01(exit);
            float outEase = x * x;
            return MarginX - SlideDistance * (1f - inEase) - SlideDistance * outEase;
        }

        /// <summary>칸 번호(소수)를 캔버스 y로. 위 끝 기준이라 음수다.</summary>
        public static float RowY(float slot) => -(MarginTop + slot * Pitch);

        private static void Layout(Row r, SkillLogModel.Entry e)
        {
            r.root.anchoredPosition = new Vector2(RowX(e.Appear, e.Exit), RowY(e.Y));
            r.group.alpha = e.Alpha;
        }

        private void Dress(Row r, SkillLogModel.Entry e)
        {
            Color chip = SkillLogModel.ChipColor(e.Skill);
            r.chip.color = chip;
            r.number.text = e.Order.ToString();
            r.number.color = SkillLogModel.NumberColor(chip);
            r.name.text = SkillLogModel.NameOf(e.Skill);

            Sprite face = e.Caster != null ? e.Caster.SkillLogFace : null;
            r.face.sprite = face;
            r.face.preserveAspect = face != null;

            // 그림이 없으면 직업 색 띠에 이름 첫 글자 — 누구 차례인지는 알아야 한다.
            Role role = e.Caster != null ? e.Caster.Role : Role.Wizard;
            r.face.color = face != null ? Color.white : SkillCutinUI.RoleColor(role).WithAlpha(0.55f);
            r.initial.gameObject.SetActive(face == null);
            string who = e.Caster == null ? string.Empty
                : e.Caster.Data != null && !string.IsNullOrEmpty(e.Caster.Data.displayName) ? e.Caster.Data.displayName
                : e.Caster.name;
            r.initial.text = face == null && who.Length > 0 ? who.Substring(0, 1) : string.Empty;

            r.root.SetAsLastSibling();
            r.root.gameObject.SetActive(true);
        }

        /// <summary>그림이 쓰는 스프라이트. 테스트가 대체 표시를 확인한다.</summary>
        public Sprite FaceOf(SkillLogModel.Entry e) => rows.TryGetValue(e, out Row r) ? r.face.sprite : null;

        /// <summary>이 줄의 순서 칸 색.</summary>
        public Color ChipOf(SkillLogModel.Entry e) => rows.TryGetValue(e, out Row r) ? r.chip.color : Color.clear;

        /// <summary>이 줄의 스킬명 글자.</summary>
        public string NameTextOf(SkillLogModel.Entry e) => rows.TryGetValue(e, out Row r) ? r.name.text : null;

        // ── 줄 짓기 ──────────────────────────────────────

        private Row Rent()
        {
            if (pool.Count > 0)
            {
                Row r = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                return r;
            }

            return Build(transform);
        }

        /// <summary>
        /// 줄 하나. 왼쪽 위 앵커라 <see cref="RowY"/>가 그대로 캔버스 y가 된다.
        /// <code>
        /// Row (Image 바탕 · CanvasGroup)
        /// ├─ Face    오른쪽 끝 눈매 띠 (왼쪽 끝이 투명으로 녹아 이름과 어우러진다)
        /// │  └─ Initial  그림이 없을 때만
        /// ├─ Chip    순서 칸 (상태색)
        /// │  └─ Number
        /// └─ Name    스킬명 — 눈매 위에 겹쳐도 외곽선으로 읽힌다
        /// </code>
        /// </summary>
        private static Row Build(Transform parent)
        {
            Image bg = UiKit.NewImage(parent, "Row", UiTheme.Ink.WithAlpha(0.78f));
            RectTransform root = UiKit.Place(bg, new Vector2(0f, 1f), Vector2.zero, new Vector2(RowWidth, RowHeight));
            CanvasGroup group = bg.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Image face = UiKit.NewImage(root, "Face", Color.white);
            RectTransform fr = (RectTransform)face.transform;
            fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(1f, 0.5f);
            fr.sizeDelta = new Vector2(FaceWidth, RowHeight);
            fr.anchoredPosition = Vector2.zero;

            Text initial = UiKit.NewText(face.transform, "Initial", UiTheme.Size.Caption, UiTheme.TextMain, FontStyle.Bold);
            UiKit.Stretch((RectTransform)initial.transform);
            initial.alignment = TextAnchor.MiddleRight;
            ((RectTransform)initial.transform).offsetMax = new Vector2(-UiTheme.Space.S8, 0f);

            Image chip = UiKit.NewImage(root, "Chip", UiTheme.TextOff);
            UiKit.Place(chip, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(ChipSize, ChipSize));
            ((RectTransform)chip.transform).pivot = new Vector2(0f, 0.5f);

            Text number = UiKit.NewText(chip.transform, "Number", UiTheme.Size.Body, UiTheme.TextOnLight, FontStyle.Bold);
            UiKit.Stretch((RectTransform)number.transform);
            UiKit.NoOutline(number);   // 밝은 칸 위 어두운 글자 — 외곽선이 오히려 뭉갠다

            Text name = UiKit.NewText(root, "Name", UiTheme.Size.Body, UiTheme.TextMain, FontStyle.Normal);
            RectTransform nr = (RectTransform)name.transform;
            nr.anchorMin = new Vector2(0f, 0f);
            nr.anchorMax = new Vector2(1f, 1f);
            nr.offsetMin = new Vector2(ChipSize + UiTheme.Space.S8, 0f);
            nr.offsetMax = new Vector2(-UiTheme.Space.S8, 0f);
            name.alignment = TextAnchor.MiddleLeft;

            return new Row
            {
                root = root, group = group, chip = chip, number = number,
                name = name, face = face, initial = initial,
            };
        }
    }
}
