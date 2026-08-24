using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스폰 예고 표식. 몹이 나오기 <see cref="ArenaSpawnPlanner.TelegraphLead"/>초 전에
    /// 해당 벽에 붉게 번쩍이다 사라진다.
    ///
    /// <b>플레이어가 뒤를 잡히는 건 실력 부족이어야지 정보 부족이면 안 된다.</b>
    /// 후방 스폰은 예고가 없으면 그냥 부당하게 느껴지고, 유저는 "뒤를 계속 보고 있어야 하는
    /// 게임"이라고 학습해 버린다 — 그 순간 벨트스크롤의 전방 시야 규칙이 통째로 무너진다.
    ///
    /// 스스로 수명을 세고 사라진다. 부르는 쪽은 띄우기만 하면 된다.
    /// </summary>
    public class SpawnTelegraph : MonoBehaviour
    {
        private const float BlinkHz = 6f;

        private static readonly Color MarkColor = new Color(1f, 0.25f, 0.2f, 0.85f);

        /// <summary>배경(뒷벽 -10001, 바닥 -10000)보다는 앞, 캐릭터(-300 언저리)보다는 뒤.</summary>
        private const int SortingOrder = -5000;

        private SpriteRenderer mark;
        private float life;
        private float age;

        /// <summary>
        /// 표식 하나를 띄운다. <paramref name="seconds"/>가 지나면 스스로 사라진다.
        /// </summary>
        /// <param name="worldPoint">벽면 위 지상 좌표. 화면 위치는 벨트스크롤 투영을 거친다.</param>
        public static SpawnTelegraph Show(Vector3 worldPoint, float seconds, Vector2 size)
        {
            var go = new GameObject("SpawnTelegraph");
            go.transform.position = BeltScroll.ToView(worldPoint);
            go.transform.rotation = BeltScroll.Billboard;

            var telegraph = go.AddComponent<SpawnTelegraph>();
            telegraph.life = Mathf.Max(0.05f, seconds);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = MarkColor;
            sr.sortingOrder = SortingOrder;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            telegraph.mark = sr;
            return telegraph;
        }

        private void Update()
        {
            // 예고는 연출이다. 불릿타임에 얼면 "곧 나온다"는 정보가 멎어 버린다.
            age += Time.unscaledDeltaTime;

            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }

            if (mark == null) return;

            // 끝으로 갈수록 빨리 깜빡인다 — 남은 시간이 그대로 읽힌다.
            float urgency = Mathf.Clamp01(age / life);
            float blink = Mathf.PingPong(age * BlinkHz * (0.6f + urgency), 1f);

            Color c = MarkColor;
            c.a = Mathf.Lerp(0.25f, 0.95f, blink);
            mark.color = c;
        }

        /// <summary>
        /// 1x1 흰 스프라이트. 프로젝트에 전용 애셋을 만들지 않으려고 코드로 굽는다 —
        /// 한 장을 모두가 공유하므로 표식이 몇 개 떠도 텍스처는 하나다.
        /// </summary>
        private static Sprite cached;

        private static Sprite SolidSprite()
        {
            if (cached != null) return cached;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            cached = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            cached.hideFlags = HideFlags.HideAndDontSave;
            return cached;
        }
    }
}
