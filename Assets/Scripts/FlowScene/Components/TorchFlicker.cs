using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 타이틀 화면 횃대의 횃불. 불꽃 자체의 일렁임은 셰이더(<c>Prototype/UI/TorchFlame</c>)가 맡고,
    /// 여기서는 그 위에 "살아 있는 불" 느낌을 얹는다:
    /// <list type="bullet">
    /// <item>불꽃 크기·세기를 펄린 노이즈로 흔든다.</item>
    /// <item>벽에 번지는 빛무리(<see cref="glows"/>)를 불꽃과 같은 박자로 밝혔다 죽인다.</item>
    /// <item>불씨를 몇 개 띄워 위로 날린다(풀링, 할당 없음).</item>
    /// </list>
    /// 일시정지 · 슬로모와 무관하게 unscaled 시간을 쓴다.
    /// </summary>
    public class TorchFlicker : MonoBehaviour
    {
        [SerializeField] private RectTransform flame;
        [SerializeField] private Graphic flameGraphic;

        [Tooltip("벽에 번지는 빛무리들. 기본 알파를 기준으로 깜빡인다.")]
        [SerializeField] private Graphic[] glows;

        [Header("깜빡임")]
        [SerializeField, Min(0f)] private float flickerSpeed = 7f;
        [SerializeField, Range(0f, 1f)] private float glowFlicker = 0.35f;
        [SerializeField, Range(0f, 0.5f)] private float scaleFlicker = 0.12f;

        [Header("불씨")]
        [SerializeField] private Sprite emberSprite;
        [SerializeField] private Material emberMaterial;
        [SerializeField, Range(0, 24)] private int emberCount = 8;
        [SerializeField] private Vector2 emberSize = new Vector2(4f, 7f);
        [SerializeField] private Vector2 emberLife = new Vector2(0.9f, 1.8f);
        [SerializeField] private Vector2 emberRise = new Vector2(60f, 130f);
        [SerializeField] private Color emberColor = new Color(1f, 0.62f, 0.2f, 1f);

        private float[] glowBaseAlpha;
        private Vector3 flameBaseScale;
        private float seed;

        private struct Ember
        {
            public RectTransform rect;
            public Image image;
            public float age, life, speed, drift, size;
        }

        private Ember[] embers;

        private void Awake()
        {
            seed = Random.value * 100f;
            if (flame != null) flameBaseScale = flame.localScale;

            glowBaseAlpha = new float[glows != null ? glows.Length : 0];
            for (int i = 0; i < glowBaseAlpha.Length; i++)
                glowBaseAlpha[i] = glows[i] != null ? glows[i].color.a : 0f;

            BuildEmbers();
        }

        private void BuildEmbers()
        {
            embers = new Ember[emberCount];
            for (int i = 0; i < emberCount; i++)
            {
                var go = new GameObject("Ember", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(transform, false);
                var img = go.GetComponent<Image>();
                img.sprite = emberSprite;
                img.material = emberMaterial;
                img.raycastTarget = false;

                embers[i] = new Ember { rect = rect, image = img };
                Respawn(ref embers[i]);
                embers[i].age = Random.value * embers[i].life;   // 처음부터 흩어져 있게
            }
        }

        private void Respawn(ref Ember e)
        {
            e.age = 0f;
            e.life = Random.Range(emberLife.x, emberLife.y);
            e.speed = Random.Range(emberRise.x, emberRise.y);
            e.drift = Random.Range(-1f, 1f);
            e.size = Random.Range(emberSize.x, emberSize.y);
        }

        private void Update()
        {
            float t = Time.unscaledTime * flickerSpeed + seed;
            // 느린 숨 + 빠른 떨림
            float n = Mathf.PerlinNoise(t, 0.37f) * 0.7f + Mathf.PerlinNoise(t * 2.7f, 5.1f) * 0.3f;   // 0..1
            float k = (n - 0.5f) * 2f;   // -1..1

            if (flame != null)
            {
                float sy = 1f + k * scaleFlicker;
                float sx = 1f - k * scaleFlicker * 0.4f;
                flame.localScale = new Vector3(flameBaseScale.x * sx, flameBaseScale.y * sy, flameBaseScale.z);
            }

            if (flameGraphic != null)
            {
                var c = flameGraphic.color;
                c.a = Mathf.Clamp01(0.85f + k * 0.15f);
                flameGraphic.color = c;
            }

            for (int i = 0; i < glowBaseAlpha.Length; i++)
            {
                if (glows[i] == null) continue;
                var c = glows[i].color;
                c.a = glowBaseAlpha[i] * (1f + k * glowFlicker);
                glows[i].color = c;
            }

            UpdateEmbers(Time.unscaledDeltaTime);
        }

        private void UpdateEmbers(float dt)
        {
            if (embers == null) return;
            for (int i = 0; i < embers.Length; i++)
            {
                ref Ember e = ref embers[i];
                e.age += dt;
                if (e.age >= e.life) Respawn(ref e);

                float p = e.age / e.life;   // 0..1
                float y = e.speed * e.age;
                float x = e.drift * 18f * p + Mathf.Sin((e.age + i) * 6f) * 3f * p;
                e.rect.anchoredPosition = new Vector2(x, y);

                float s = e.size * (1f - p * 0.6f);
                e.rect.sizeDelta = new Vector2(s, s);

                var c = emberColor;
                c.a = Mathf.Sin(p * Mathf.PI) * emberColor.a;   // 생겨나서 사그라든다
                e.image.color = c;
            }
        }
    }
}
