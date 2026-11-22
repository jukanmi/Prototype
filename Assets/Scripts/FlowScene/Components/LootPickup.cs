using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 바닥에 놓인 보상 하나 — 금화 · 상자 · 스킬 두루마리(<see cref="ClearLoot"/>가 짓는다).
    ///
    /// 그림은 몸(<see cref="BeltScrollView"/>)과 같은 규칙이다 — 스프라이트는 카메라를 보고 서고(빌보드),
    /// 그림자는 바닥에 눕고, 정렬은 깊이(z)로 정한다. 다가가 상호작용 키를 누르면 <c>onPick</c>을 부른다
    /// (<see cref="Interactable"/>을 코드로 붙인다).
    ///
    /// 상자에서 튀어나오는 두루마리는 <see cref="LaunchFrom"/>으로 포물선을 그리며 날아와 앉고,
    /// 앉기 전에는 주울 수 없다. <see cref="Skill"/>이 있으면 다가갔을 때 설명 창(<see cref="SkillTooltip"/>)을 띄운다.
    /// </summary>
    public class LootPickup : MonoBehaviour
    {
        private const float ShadowAlpha = 0.35f;
        private const float BobHeight = 0.08f;
        private const float BobSpeed = 2.6f;
        private const float SpinSpeed = 3.2f;

        private Transform view;
        private SpriteRenderer sprite;
        private Transform shadow;

        private float size;
        private float hover;
        private bool bob;
        private bool spin;
        private float phase;

        private Vector3 home;
        private Vector3 flyFrom;
        private float flyTime;
        private float flyDuration;

        private string label;
        private float radius;
        private Action<LootPickup> onPick;
        private Interactable spot;

        /// <summary>있으면 다가갔을 때 이 스킬의 설명 창을 띄운다.</summary>
        public SkillData Skill { get; set; }

        public SpriteRenderer Sprite => sprite;

        /// <summary>
        /// 보상 하나를 바닥에 놓는다. <paramref name="ground"/>는 발밑(y = 바닥)이다.
        /// </summary>
        /// <param name="size">그림의 화면 크기(월드 단위, 긴 변).</param>
        /// <param name="hover">바닥에서 띄우는 높이. 동전 · 두루마리는 떠서 까딱이고 상자는 바닥에 앉는다.</param>
        public static LootPickup Create(string name, Vector3 ground, Sprite art, Color color, float size, float hover,
                                        string label, float radius, Action<LootPickup> onPick)
        {
            var go = new GameObject(name);
            go.transform.position = ground;

            LootPickup loot = go.AddComponent<LootPickup>();
            loot.home = ground;
            loot.size = size;
            loot.hover = hover;
            loot.bob = hover > 0f;
            loot.phase = UnityEngine.Random.value * 10f;
            loot.label = label;
            loot.radius = radius;
            loot.onPick = onPick;

            loot.Build(art, color);
            loot.Arm();
            return loot;
        }

        /// <summary>동전처럼 좌우로 돈다.</summary>
        public LootPickup Spinning()
        {
            spin = true;
            return this;
        }

        /// <summary>
        /// 그림 앞에 한 겹 더 얹는다(동전의 안쪽 원, 두루마리 뒤의 빛). <paramref name="front"/>가 거짓이면 뒤에 깐다.
        /// </summary>
        /// <param name="relativeSize">본 그림 크기(<c>size</c>)에 대한 비율. 그림마다 PPU가 달라 월드 크기로 맞춘다.</param>
        public SpriteRenderer AddLayer(string name, Sprite art, Color color, float relativeSize, bool front)
        {
            float longest = art != null ? Mathf.Max(art.bounds.size.x, art.bounds.size.y) : 1f;
            float parentScale = Mathf.Max(0.0001f, sprite.transform.localScale.x);
            float local = relativeSize * size / Mathf.Max(0.0001f, longest) / parentScale;

            var go = new GameObject(name);
            go.transform.SetParent(sprite.transform, false);
            go.transform.localScale = new Vector3(local, local, 1f);
            go.transform.localPosition = new Vector3(0f, 0f, front ? -0.01f : 0.01f);

            var layer = go.AddComponent<SpriteRenderer>();
            layer.sprite = art;
            layer.color = color;
            layer.sortingOrder = sprite.sortingOrder + (front ? 1 : -1);
            return layer;
        }

        /// <summary>
        /// <paramref name="from"/>에서 포물선으로 날아와 지금 자리에 앉는다. 앉기 전에는 주울 수 없다.
        /// </summary>
        public void LaunchFrom(Vector3 from, float duration)
        {
            flyFrom = from;
            flyTime = 0f;
            flyDuration = Mathf.Max(0.05f, duration);
            Disarm();
        }

        /// <summary>주울 수 없게 한다(상자를 연 뒤 · 날아가는 중). 이름표도 감춘다.</summary>
        public void Disarm()
        {
            if (spot != null) spot.enabled = false;
        }

        // ── 짓기 ────────────────────────────────────────

        private void Build(Sprite art, Color color)
        {
            LootArt kit = LootArt.Get();
            Sprite round = kit != null ? kit.coin : null;

            int order = Mathf.RoundToInt(-home.z * 100f);

            shadow = new GameObject("Shadow").transform;
            shadow.SetParent(transform, false);
            shadow.rotation = BeltScrollView.LieOnGround;
            shadow.localScale = new Vector3(size * 0.75f, size * 0.75f, 1f);

            var shadowRenderer = shadow.gameObject.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = round;
            shadowRenderer.color = new Color(0f, 0f, 0f, ShadowAlpha);
            shadowRenderer.sortingOrder = order;

            view = new GameObject("View").transform;
            view.SetParent(transform, false);

            sprite = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            sprite.transform.SetParent(view, false);
            sprite.sprite = art;
            sprite.color = color;
            sprite.sortingOrder = order + 2;

            // 긴 변을 size에 맞춘다. 그림의 PPU가 제각각(아이콘 100 · 원 256)이라 배율로 맞춰야 크기가 고르다.
            float longest = art != null ? Mathf.Max(art.bounds.size.x, art.bounds.size.y) : 1f;
            float scale = size / Mathf.Max(0.0001f, longest);
            sprite.transform.localScale = new Vector3(scale, scale, 1f);

            Place(home, hover);
        }

        private void Arm()
        {
            if (spot == null)
            {
                // 이름표는 월드 Y로 올라간다. 화면에서 그림 위에 오려면 카메라 기울기만큼 더 올려야 한다
                // (월드 Y 1은 화면에서 cosθ만큼 보인다).
                float screenTop = hover + size + 0.3f;
                float up = Mathf.Max(0.3f, BeltScroll.ScreenUp.y);

                spot = gameObject.AddComponent<Interactable>();
                spot.Configure(label, radius, screenTop / up);
                spot.OnInteract.AddListener(Pick);
            }

            spot.enabled = true;
        }

        private void Pick() => onPick?.Invoke(this);

        // ── 매 프레임 ───────────────────────────────────

        private void LateUpdate()
        {
            if (flyDuration > 0f) Fly();
            else Place(home, hover + (bob ? BobHeight * Mathf.Sin((Time.time + phase) * BobSpeed) : 0f));

            if (spin)
            {
                // 옆으로 선 순간에도 얇게는 보이게 바닥을 둔다 — 0이면 깜빡 사라진다.
                float w = Mathf.Max(0.15f, Mathf.Abs(Mathf.Cos((Time.time + phase) * SpinSpeed)));
                view.localScale = new Vector3(w, 1f, 1f);
            }

            if (Skill != null) SkillTooltip.Track(this, Skill, spot != null && spot.IsFocused);
        }

        private void Fly()
        {
            flyTime += Time.deltaTime;
            float t = Mathf.Clamp01(flyTime / flyDuration);

            Vector3 ground = Vector3.Lerp(flyFrom, home, t);
            float arc = 4f * 1.4f * t * (1f - t);   // 꼭짓점 1.4
            Place(ground, hover + arc);

            if (t < 1f) return;

            flyDuration = 0f;
            Arm();
        }

        /// <summary>발밑 <paramref name="ground"/>에서 <paramref name="height"/>만큼 뜬 자리에 그림을 세운다.</summary>
        private void Place(Vector3 ground, float height)
        {
            transform.position = ground;

            // 화면 위로 올린다 — 월드 Y로 올리면 카메라 기울기만큼 짧아져 바닥에 파묻힌다(BeltScrollView와 같다).
            view.position = BeltScroll.ToView(ground, height) + BeltScroll.ScreenUp * (size * 0.5f);
            view.rotation = BeltScroll.Billboard;

            shadow.position = BeltScroll.ToView(ground) + Vector3.up * 0.01f;
        }

        private void OnDestroy() => SkillTooltip.Release(this);

        // ── 떠오르는 글 ─────────────────────────────────

        /// <summary>"+60 골드"처럼 잠깐 떠올랐다 사라지는 글.</summary>
        public static void Popup(Vector3 ground, string text, Color color)
        {
            var go = new GameObject("LootPopup");
            go.transform.position = BeltScroll.ToView(ground, 1.6f);
            go.transform.rotation = BeltScroll.Billboard;

            var mesh = go.AddComponent<TextMesh>();
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MeshRenderer popupRenderer = go.GetComponent<MeshRenderer>();
            popupRenderer.sharedMaterial = mesh.font.material;
            popupRenderer.sortingOrder = Interactable.TagSortingOrder;   // 이름표처럼 그림에 안 가린다
            mesh.text = text;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = 0.09f;
            mesh.fontSize = 56;
            mesh.color = color;

            go.AddComponent<Rise>();
        }

        /// <summary>떠오르며 옅어지다 스스로 지운다.</summary>
        private sealed class Rise : MonoBehaviour
        {
            private const float Life = 1.1f;
            private float age;
            private TextMesh mesh;

            private void Awake() => mesh = GetComponent<TextMesh>();

            private void LateUpdate()
            {
                age += Time.deltaTime;
                transform.position += BeltScroll.ScreenUp * (Time.deltaTime * 1.2f);
                transform.rotation = BeltScroll.Billboard;

                Color c = mesh.color;
                c.a = 1f - Mathf.Clamp01((age - Life * 0.5f) / (Life * 0.5f));
                mesh.color = c;

                if (age >= Life) Destroy(gameObject);
            }
        }
    }
}
