using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
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
}
