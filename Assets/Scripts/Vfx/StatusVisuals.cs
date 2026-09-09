// 상태 표시 — 상태이상 막대와 그 색 · 이름 표.
// 표를 막대와 같은 파일에 둔다. 떨어뜨려 놓으면 상태를 하나 추가할 때
// 두 파일을 같이 고쳐야 하는데 한쪽을 빠뜨려도 컴파일은 통과한다.
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ StatusEffectBar ═══════════════════════════════════════════

    /// <summary>
    /// 머리 위에 걸린 상태의 <b>남은 시간</b>을 게이지로 띄운다. 적과 아군 모두.
    ///
    /// <see cref="EnemyStateLabel"/>은 "지금 경직이다"까지만 말해 준다. 언제 풀리는지는
    /// 안 보여서, 다음 스킬을 지금 넣을지 기다릴지는 감으로 눌러야 했다.
    /// 보호막 · 피해감소 · 흡혈은 아예 화면에 흔적이 없었다 — 걸렸는지조차 몰랐다.
    ///
    /// 무엇을 그릴지는 전부 <see cref="StatusEffectVisuals.Collect"/>가 정한다.
    /// 여기는 순회 · 배치 · 풀링만 한다.
    ///
    /// <see cref="ChargeGauge"/> · <see cref="EnemyStateLabel"/>과 같은 자리(<c>[BattleVfx]</c>)에
    /// 붙는다 — 씬 배선 0.
    /// </summary>
    public class StatusEffectBar : MonoBehaviour
    {
        [Tooltip("첫 줄이 뜨는 높이. 상태 글자(EnemyStateLabel, 1.9)는 글자 높이가 0.3 가까이 되므로 " +
                 "그 위를 넉넉히 비켜야 겹치지 않는다.")]
        [SerializeField] private float headOffset = 2.25f;

        [Tooltip("게이지 가로 폭(월드 단위). 가장 긴 이름(\"피해감소 4.2\")이 들어가야 한다.")]
        [SerializeField] private float width = 1.3f;

        [Tooltip("한 줄의 높이. 글자가 이 안에 들어간다.")]
        [SerializeField] private float rowHeight = 0.18f;

        [Tooltip("줄 사이 간격.")]
        [SerializeField] private float rowGap = 0.05f;

        [Tooltip("정렬 오프셋. 상태 글자(320)보다 앞이다.")]
        [SerializeField] private int sortingOffset = 340;

        [Tooltip("월드 단위 글자 크기. 한 줄 높이 안에 들어가야 한다(상태 글자 0.06의 절반쯤).")]
        [SerializeField] private float characterSize = 0.032f;

        [SerializeField] private int fontSize = 48;

        /// <summary>남은 시간이 닳은 자리. 게이지가 어디까지 찼는지 읽으려면 바탕이 있어야 한다.</summary>
        private static readonly Color TrackColor = new Color(0.06f, 0.06f, 0.08f, 0.78f);

        /// <summary>한 줄을 이루는 세 조각. 매 프레임 GetComponent하지 않으려고 같이 들고 다닌다.</summary>
        private struct Row
        {
            public SpriteRenderer track;
            public SpriteRenderer fill;
            public TextMesh text;
            public MeshRenderer textRenderer;
        }

        private readonly List<Row> pool = new List<Row>();
        private readonly List<StatusView> buffer = new List<StatusView>();

        private static Sprite barSprite;

        /// <summary>줄 하나의 세로 간격.</summary>
        private float RowStep => rowHeight + rowGap;

        /// <summary>
        /// <paramref name="row"/>번째 줄의 <b>왼쪽 끝</b>이 놓일 자리.
        ///
        /// <see cref="EnemyStateLabel.LabelPosition"/>과 같은 규칙이다 — 논리 좌표가 아니라
        /// <see cref="BeltScroll.ToView"/>를 거친 그리는 위치를 쓰고, 머리 오프셋과 줄 간격에는
        /// 깊이 배율을 먹인다(뒤에 선 적은 몸이 줄어 머리도 내려온다).
        /// 게이지 폭과 글자 크기는 안 건드린다 — 어느 깊이에서나 같게 읽혀야 한다.
        /// </summary>
        public static Vector3 RowPosition(Vector3 ground, float height, float headOffset,
                                          int row, float rowStep, float width)
        {
            Vector3 center = BeltScroll.ToView(ground, height)
                           + BeltScroll.ScreenUp * ((headOffset + row * rowStep) * BeltScroll.ScaleAt(ground.z));
            center.x -= width * 0.5f;
            return center;
        }

        // 캐릭터 위치는 LateUpdate에 확정된다(BeltScrollView). 그 뒤에 읽는다.
        // TimeControl을 보지 않으므로 불릿타임 중에도 보인다 — 조준하는 동안 남은 시간이 보여야
        // 이 스킬을 지금 넣을지 판단할 수 있다.
        private void LateUpdate()
        {
            int used = 0;
            used += DrawAll(BattleRegistry.Allies, used);
            used += DrawAll(BattleRegistry.Enemies, used);

            // 남는 줄은 끈다. 파괴하지 않는다 — 다음 상태에 다시 쓴다.
            for (int i = used; i < pool.Count; i++)
                Hide(pool[i]);
        }

        private int DrawAll(IReadOnlyList<Entity> list, int start)
        {
            if (list == null) return 0;

            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Entity e = list[i];
                if (e == null || e.Combat == null || e.Physics == null) continue;

                int rows = StatusEffectVisuals.Collect(e.Combat, buffer);
                for (int r = 0; r < rows; r++)
                {
                    Draw(Take(start + n), e, buffer[r], r);
                    n++;
                }
            }

            return n;
        }

        private void Draw(Row row, Entity e, in StatusView view, int index)
        {
            Physics phys = e.Physics;
            Vector3 ground = phys.GroundPosition;
            Vector3 left = RowPosition(ground, phys.Height, headOffset, index, RowStep, width);

            // 캐릭터와 같은 깊이 정렬 공식(BeltScrollView.ApplySorting) 위에서 오프셋만 준다.
            int order = Mathf.RoundToInt(-ground.z * 100f) + sortingOffset;

            Stretch(row.track, left, width, TrackColor, order);
            Stretch(row.fill, left, width * view.Ratio, view.color, order + 1);

            // 남은 시간은 숫자로도 준다. 게이지 길이만으로는 "1초 남았나 3초 남았나"가 안 읽힌다.
            row.text.text = $"{view.label} {view.remain:0.0}";
            row.text.transform.position = new Vector3(left.x + width * 0.5f, left.y, left.z);
            row.text.transform.rotation = BeltScroll.Billboard;   // 빌보드
            row.textRenderer.sortingOrder = order + 2;
            row.textRenderer.enabled = true;
        }

        /// <summary>
        /// 1x1 스프라이트를 늘려 막대를 만든다. 피벗이 왼쪽 가운데라
        /// x 스케일을 줄여도 <b>왼쪽 끝은 그 자리에 있다</b> — 게이지가 오른쪽에서 닳는다.
        /// </summary>
        private void Stretch(SpriteRenderer sr, Vector3 left, float w, Color color, int order)
        {
            if (w <= 0.0001f)
            {
                sr.enabled = false;
                return;
            }

            sr.transform.position = left;
            sr.transform.rotation = BeltScroll.Billboard;
            sr.transform.localScale = new Vector3(w, rowHeight, 1f);
            sr.color = color;
            sr.sortingOrder = order;
            sr.enabled = true;
        }

        private void Hide(Row row)
        {
            row.track.enabled = false;
            row.fill.enabled = false;
            row.textRenderer.enabled = false;
        }

        private Row Take(int index)
        {
            while (pool.Count <= index)
            {
                // 줄의 루트는 절대 움직이지 않는다. 세 조각이 각자 월드 좌표로 놓이므로
                // 루트를 옮기면 자식이 통째로 한 번 더 끌려간다.
                var go = new GameObject("StatusRow");
                go.transform.SetParent(transform, false);

                var textGo = new GameObject("Label");
                textGo.transform.SetParent(go.transform, false);

                var text = textGo.AddComponent<TextMesh>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = fontSize;
                text.fontStyle = FontStyle.Bold;
                text.characterSize = characterSize;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = Color.white;

                var textRenderer = textGo.GetComponent<MeshRenderer>();

                // TextMesh는 폰트를 꽂아도 렌더러 머티리얼을 스스로 맞추지 않는다.
                // 이걸 빼면 글자가 분홍 사각형으로 나온다(EnemyStateLabel과 같은 함정).
                textRenderer.sharedMaterial = text.font.material;
                textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                textRenderer.receiveShadows = false;
                textRenderer.enabled = false;

                pool.Add(new Row
                {
                    track = NewBar(go.transform, "Track"),
                    fill = NewBar(go.transform, "Fill"),
                    text = text,
                    textRenderer = textRenderer,
                });
            }

            return pool[index];
        }

        private static SpriteRenderer NewBar(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BarSprite();
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.enabled = false;

            return sr;
        }

        /// <summary>
        /// 흰 점 하나짜리 스프라이트. 막대는 이걸 늘려서 만들고 색은 <see cref="SpriteRenderer.color"/>로 준다.
        ///
        /// PPU를 1로 두면 스케일 값이 그대로 월드 크기가 된다 — 폭 계산에 변환이 끼지 않는다.
        /// 피벗은 왼쪽 가운데. 게이지가 왼쪽에 붙은 채 오른쪽에서 닳으려면 그래야 한다.
        /// </summary>
        private static Sprite BarSprite()
        {
            if (barSprite != null) return barSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "StatusBarPixel",
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            barSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);
            barSprite.hideFlags = HideFlags.HideAndDontSave;

            return barSprite;
        }
    }

    // ══ StatusEffectVisuals ═══════════════════════════════════════════

    /// <summary>머리 위 게이지 한 줄. 무엇에서 왔는지는 이미 잊은, 그리기용 값만 남은 형태.</summary>
    public readonly struct StatusView
    {
        public readonly string label;
        public readonly Color color;
        public readonly float remain;
        public readonly float duration;

        public StatusView(string label, Color color, float remain, float duration)
        {
            this.label = label;
            this.color = color;
            this.remain = remain;
            this.duration = duration;
        }

        /// <summary>남은 비율 0~1. 게이지 폭이 이걸 따른다.</summary>
        public float Ratio => duration > 0.0001f ? Mathf.Clamp01(remain / duration) : 0f;
    }

    /// <summary>
    /// 지속 상태를 화면 표현(글자 · 색)으로 옮기는 표. <see cref="CombatStateVisuals"/>의 짝이다.
    ///
    /// 경직 계열은 여기서 새로 만들지 않고 <see cref="CombatStateVisuals"/>를 그대로 부른다 —
    /// 머리 위 글자(<see cref="EnemyStateLabel"/>)와 그 아래 게이지가 다른 이름 · 다른 색으로
    /// 같은 상태를 가리키면 이중 부호화가 오히려 방해가 된다.
    ///
    /// 유니티 객체를 만지지 않으므로 EditMode에서 그대로 검증된다.
    /// </summary>
    public static class StatusEffectVisuals
    {
        /// <summary>한 캐릭터에 동시에 그릴 줄 수의 상한. 넘치면 머리 위가 탑이 된다.</summary>
        public const int MaxRows = 4;

        /// <summary>패링 성공 무적. 짧지만 "지금은 뭘 맞아도 안 아프다"가 보여야 반격이 읽힌다.</summary>
        public const string InvulnerableLabel = "무적";

        private static readonly Color InvulnerableColor = new Color(0.298f, 0.788f, 0.941f); // #4CC9F0
        private static readonly Color ShieldColor = new Color(0.361f, 0.855f, 0.804f);       // #5CDACD
        private static readonly Color DamageCutColor = new Color(0.678f, 0.780f, 1f);        // #ADC7FF
        private static readonly Color LifestealColor = new Color(0.804f, 0.294f, 0.427f);    // #CD4B6D
        private static readonly Color StunColor = new Color(1f, 0.878f, 0.400f);             // #FFE066
        private static readonly Color FreezeColor = new Color(0.561f, 0.890f, 0.961f);       // #8FE3F5

        public static string Label(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Shield: return "보호막";
                case StatusKind.DamageCut: return "피해감소";
                case StatusKind.Lifesteal: return "흡혈";
                case StatusKind.Stun: return "스턴";
                case StatusKind.Freeze: return "빙결";
                default: return kind.ToString();
            }
        }

        public static Color StatusColor(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Shield: return ShieldColor;
                case StatusKind.DamageCut: return DamageCutColor;
                case StatusKind.Lifesteal: return LifestealColor;
                case StatusKind.Stun: return StunColor;
                case StatusKind.Freeze: return FreezeColor;
                default: return Color.white;
            }
        }

        /// <summary>
        /// 이 캐릭터에게 지금 그려야 할 줄을 모은다. <b>표시 대상의 유일한 정의</b>다 —
        /// 화면에 그리는 쪽(<see cref="StatusEffectBar"/>)은 순회와 배치만 한다.
        ///
        /// 순서는 아래(머리에 가까운 쪽)부터: 경직 → 무적 → 버프. 경직이 가장 자주 바뀌고
        /// 가장 급하게 읽어야 하는 값이라 몸에서 제일 가깝다.
        ///
        /// 시체에는 아무것도 그리지 않는다 — <see cref="CombatStateVisuals.ShouldShow"/>와 같은 규칙.
        /// </summary>
        public static int Collect(Combat combat, List<StatusView> into)
        {
            if (into == null) return 0;

            into.Clear();
            if (combat == null || combat.IsDead) return 0;

            // 경직 · 다운 · 기상. 착지로만 풀리는 상태(공중피격 · 넉백)는 타이머가 0이라
            // 여기서 저절로 빠진다 — 남은 시간이 없는 것에 게이지를 그릴 수는 없다.
            if (CombatStateVisuals.ShouldShow(combat.CombatState) && combat.StunRemaining > 0f)
                into.Add(new StatusView(CombatStateVisuals.Label(combat.CombatState),
                                        CombatStateVisuals.StateColor(combat.CombatState),
                                        combat.StunRemaining, combat.StunDuration));

            if (combat.ParryInvulnRemaining > 0f)
                into.Add(new StatusView(InvulnerableLabel, InvulnerableColor,
                                        combat.ParryInvulnRemaining, combat.ParryInvulnDuration));

            // 목록을 두 번 돈다 — 디버프가 버프보다 아래(몸에 가까운 쪽)로 간다.
            // 걸린 순서대로 그리면 스턴이 버프 셋 위에 붙어 MaxRows에 잘려 나가는데,
            // "지금 얘가 굳어 있나"는 남은 보호막보다 급하게 읽어야 하는 값이다.
            IReadOnlyList<StatusEffects.Entry> active = combat.Statuses.Active;

            for (int i = 0; i < active.Count && into.Count < MaxRows; i++)
                if (StatusRules.IsDebuff(active[i].kind)) AddRow(active[i], into);

            for (int i = 0; i < active.Count && into.Count < MaxRows; i++)
                if (!StatusRules.IsDebuff(active[i].kind)) AddRow(active[i], into);

            // 경직 + 무적 + 버프가 한꺼번에 걸리면 상한을 넘을 수 있다. 위(나중에 걸린 버프)부터 자른다.
            if (into.Count > MaxRows) into.RemoveRange(MaxRows, into.Count - MaxRows);

            return into.Count;
        }

        private static void AddRow(StatusEffects.Entry e, List<StatusView> into)
            => into.Add(new StatusView(Label(e.kind), StatusColor(e.kind), e.remain, e.duration));
    }

    // ══ CombatStateVisuals ═══════════════════════════════════════════

    /// <summary>
    /// <see cref="CombatState"/> 위에 겹쳐 그리는 표시. 상태가 아니라 <b>행동</b>이라
    /// 전이표에 넣을 수 없는 것들이다.
    /// </summary>
    public enum CombatOverlay
    {
        None,
        /// <summary>공격 예고(선딜). 맞기 전에 읽을 수 있는 유일한 신호.</summary>
        Telegraph,
        /// <summary>보스 슈퍼아머. 때려도 밀리지 않는 구간.</summary>
        SuperArmor,
        /// <summary>스턴 디버프. <see cref="CombatState"/>가 아니라 <see cref="Debuff"/> 축이라 여기 있다.</summary>
        Stunned,
        /// <summary>빙결 디버프.</summary>
        Frozen,
    }

    /// <summary>
    /// 전투 상태를 화면 표현(색 · 글자)으로 옮기는 표. <b>한 벌만</b> 유지한다.
    ///
    /// <see cref="EnemyStateTint"/>(몸 색)와 <see cref="EnemyStateLabel"/>(머리 위 글자)이
    /// 같은 함수를 부른다. 두 벌로 갈리면 색과 글자가 어긋나 이중 부호화가 의미를 잃는다.
    ///
    /// 유니티 객체를 만지지 않으므로 EditMode에서 그대로 검증된다.
    /// </summary>
    public static class CombatStateVisuals
    {
        /// <summary>
        /// 기본 색을 상태색 쪽으로 끌어당기는 정도. 1이면 완전히 덮는다.
        /// 1로 두지 않는 이유는 적 고유색(고블린 빨강 · 궁수 파랑 · 멧돼지 주황)의
        /// 흔적을 남겨, 물든 뒤에도 무슨 적이었는지 알아볼 수 있게 하기 위해서다.
        /// </summary>
        public const float TintStrength = 0.8f;

        /// <summary>
        /// 예고(선딜) 표시의 세기. 경직(0.8)보다 <b>세게</b> 덮어 완전한 흰색으로 번쩍인다.
        ///
        /// 경직도 흰색 계열이라 세기까지 같으면 "때렸다"와 "맞기 직전이다"가 구분되지 않는다.
        /// 예고는 고유색을 지워 버릴 만큼 하얗고, 경직은 적 색이 비쳐 보인다.
        /// </summary>
        public const float TelegraphStrength = 1f;

        // 색과 글자를 함께 쓴다. 색만으로는 색약자가 구분하지 못하고,
        // 글자만으로는 난전에서 안 읽힌다.
        private static readonly Color LightHitColor = new Color(1f, 1f, 1f);           // #FFFFFF
        private static readonly Color AerialHitColor = new Color(0.349f, 0.761f, 1f);  // #59C2FF
        private static readonly Color KnockbackColor = new Color(1f, 0.549f, 0.259f);  // #FF8C42
        private static readonly Color WallBoundColor = new Color(0.886f, 0.290f, 1f);  // #E24AFF
        private static readonly Color DownColor = new Color(0.478f, 0.478f, 0.522f);   // #7A7A85
        private static readonly Color GetupColor = new Color(1f, 0.820f, 0.400f);      // #FFD166

        /// <summary>공격 예고. 이 색으로 번쩍이는 순간이 곧 "지금 대시하면 패링된다"는 신호다.</summary>
        private static readonly Color TelegraphColor = new Color(1f, 1f, 1f);          // #FFFFFF

        /// <summary>슈퍼아머. 로그가 쓰던 색과 같게 둔다 — 화면과 콘솔이 같은 것을 가리켜야 한다.</summary>
        private static readonly Color SuperArmorColor = new Color(1f, 0.820f, 0.400f); // #FFD166

        /// <summary>스턴 · 빙결. 머리 위 게이지(<see cref="StatusEffectVisuals"/>)와 같은 색을 쓴다.</summary>
        private static readonly Color StunnedColor = new Color(1f, 0.878f, 0.400f);    // #FFE066
        private static readonly Color FrozenColor = new Color(0.561f, 0.890f, 0.961f); // #8FE3F5

        /// <summary>예고 중에 머리 위에 띄울 글자. 색만으로는 색약자가 구분하지 못한다.</summary>
        public const string TelegraphLabel = "!";

        /// <summary>슈퍼아머 중에 띄울 글자.</summary>
        public const string SuperArmorLabel = "아머";

        /// <summary>
        /// 가드브레이크 중에 띄울 글자. <b>색은 주지 않는다</b> —
        /// 그 구간은 경직 · 공중 · 다운 상태색이 계속 바뀌는 게 피드백인데
        /// 위에 색을 덮으면 콤보가 먹히는지 안 먹히는지 안 보인다.
        /// </summary>
        public const string GuardBreakLabel = "브레이크";

        /// <summary>화면에 드러낼 상태인지. 평상시(Neutral)와 사망은 표시하지 않는다.</summary>
        public static bool ShouldShow(CombatState state)
            => state != CombatState.Neutral && state != CombatState.Dead;

        /// <summary>머리 위에 띄울 글자. 표시 대상이 아니면 빈 문자열.</summary>
        public static string Label(CombatState state)
        {
            switch (state)
            {
                case CombatState.LightHit: return "경직";
                case CombatState.AerialHit: return "공중";
                case CombatState.Knockback: return "넉백";
                case CombatState.WallBound: return "벽꽂";
                case CombatState.Down: return "다운";
                case CombatState.Getup: return "기상";
                default: return string.Empty;
            }
        }

        /// <summary>상태를 대표하는 색. 표시 대상이 아니면 흰색(중립값).</summary>
        public static Color StateColor(CombatState state)
        {
            switch (state)
            {
                case CombatState.LightHit: return LightHitColor;
                case CombatState.AerialHit: return AerialHitColor;
                case CombatState.Knockback: return KnockbackColor;
                case CombatState.WallBound: return WallBoundColor;
                case CombatState.Down: return DownColor;
                case CombatState.Getup: return GetupColor;
                default: return Color.white;
            }
        }

        /// <summary>
        /// 기본 색을 상태색 쪽으로 섞는다.
        ///
        /// 알파는 항상 기본 색의 것을 쓴다 — 사망 페이드가 알파를 깎는데
        /// 여기서 덮으면 죽는 연출이 도중에 끊긴다.
        /// </summary>
        public static Color Tint(Color baseColor, CombatState state)
            => Tint(baseColor, state, CombatOverlay.None);

        /// <summary>
        /// 겹침 표시까지 반영한 색. 우선순위는 <b>전투 상태 &gt; 빙결 · 스턴 &gt; 아머 &gt; 예고 &gt; 기본</b>이다.
        ///
        /// 전투 상태가 가장 위인 이유: 예고 중에 맞으면 특수 행동이 취소되므로(EnemyControl)
        /// 그 순간 화면도 피격을 보여야 한다. 그래서 굳어 있는 적이 맞으면 얼음색이 잠깐
        /// 흰색으로 번쩍였다 돌아온다 — 버그가 아니라 "얼어 있는데 지금 맞았다"는 정보다.
        ///
        /// 디버프가 아머보다 위인 이유: 얼어붙은 보스가 금색으로 보이면 거짓말이다.
        /// 아머는 "때려도 안 밀린다"는 뜻인데, 굳은 몸은 오히려 밀린다.
        /// </summary>
        public static Color Tint(Color baseColor, CombatState state, CombatOverlay overlay)
        {
            if (ShouldShow(state))
                return Mix(baseColor, StateColor(state), TintStrength);

            if (state == CombatState.Dead) return baseColor;

            switch (overlay)
            {
                case CombatOverlay.Frozen:
                    return Mix(baseColor, FrozenColor, TintStrength);
                case CombatOverlay.Stunned:
                    return Mix(baseColor, StunnedColor, TintStrength);
                case CombatOverlay.SuperArmor:
                    return Mix(baseColor, SuperArmorColor, TintStrength);
                case CombatOverlay.Telegraph:
                    return Mix(baseColor, TelegraphColor, TelegraphStrength);
                default:
                    return baseColor;
            }
        }

        /// <summary>
        /// 알파는 항상 기본 색의 것을 쓴다 — 사망 페이드가 알파를 깎는데
        /// 여기서 덮으면 죽는 연출이 도중에 끊긴다.
        /// </summary>
        private static Color Mix(Color baseColor, Color target, float strength)
        {
            Color mixed = Color.Lerp(baseColor, target, strength);
            mixed.a = baseColor.a;
            return mixed;
        }
    }
}
