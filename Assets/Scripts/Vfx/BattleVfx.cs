// 전투 연출 — 진입점 · 스프라이트 재생 · 스킬 표.
// 셋이 한 세트다: 호출부는 BattleVfx에만 말을 걸고, 실제 그림은 VfxSprite가,
// 스킬별 어떤 연출인지는 SkillVfx가 정한다. 뒤의 둘은 소비자가 BattleVfx뿐이다.
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Prototype
{
    // ══ BattleVfx ═══════════════════════════════════════════

    /// <summary>
    /// 전투 연출 진입점. 호출부는 <b>논리 좌표와 크기만</b> 넘긴다 —
    /// 화면 접기(<see cref="BeltScroll"/>)와 정렬은 여기서 한 번만 처리한다.
    ///
    /// 실제 그림은 <see cref="VfxSprite"/>가 스프라이트 시트로 그린다.
    /// 시트는 <see cref="VfxLibrary"/>(전역) 또는 <see cref="SkillVfx"/>(스킬 전용)에서 온다.
    /// 둘 다 비어 있으면 <b>아무것도 그리지 않는다</b> — 아트가 없던 시절의
    /// LineRenderer 대체 연출은 시트가 들어오면서 걷어냈다.
    /// </summary>
    public static class BattleVfx
    {
        /// <summary>연출을 끄고 싶을 때. 성능 비교나 캡처용.</summary>
        public static bool Enabled = true;

        /// <summary>흑백 시트에 곱하는 전역 색. 스킬이 <c>custom</c>을 켜면 그쪽이 이긴다.</summary>
        public static Color ImpactColor = new Color(1f, 0.86f, 0.45f, 1f);
        public static Color ShockColor = new Color(1f, 0.55f, 0.3f, 0.75f);

        /// <summary>
        /// 스킬 타격은 평타보다 크게 그린다.
        /// 평타가 계속 터지는 난전에서 "지금 스킬이 나갔다"가 읽혀야 한다.
        /// </summary>
        private const float SkillSize = 1.7f;

        /// <summary>
        /// 시전 표시. 스킬이 실제로 터지는 순간, 터지는 자리에 한 번 그린다.
        ///
        /// 불릿타임에서 유저가 찍은 좌표가 곧 여기다 — 조준할 때 본
        /// <see cref="RangeIndicator"/> 타원과 같은 자리·같은 반경으로 나온다.
        /// 시전자 본체 모션과 타격은 순간적이라 "내가 찍은 데서 터졌다"가 안 읽혔다.
        /// </summary>
        public static void Cast(Vector3 ground, float radius, in SkillVfx style = default)
        {
            if (!Enabled) return;

            VfxClip clip = Pick(style.castClip, Library != null ? Library.cast : null);
            if (clip == null) return;

            VfxRunner r = VfxRunner.Instance;
            if (r == null) return;

            Color tint = style.custom ? style.impact : ImpactColor;
            r.SpawnSprite(clip, ground, 0f, Vector3.right, Mathf.Max(0.5f, radius * style.Scale), tint);
        }

        /// <summary>타격. 실제로 맞았을 때만 부른다.</summary>
        public static void Impact(Vector3 ground, float height, Vector3 facing, float radius,
                                  in SkillVfx style = default)
        {
            if (!Enabled) return;

            VfxClip clip = Pick(style.hitClip, Library != null ? Library.impact : null);
            if (clip == null) return;

            VfxRunner r = VfxRunner.Instance;
            if (r == null) return;

            float s = style.Scale * (style.fromSkill ? SkillSize : 1f);

            r.SpawnSprite(clip, ground, height, facing, radius * s,
                          style.custom ? style.impact : ImpactColor);

            // 충격파는 시트가 따로 있을 때만 겹친다. 없으면 타격 한 장으로 끝.
            VfxClip wave = Library != null ? Library.shock : null;
            if (wave != null && wave.IsValid)
                r.SpawnSprite(wave, ground, height, facing, radius * 1.5f * s,
                              style.custom ? style.shock : ShockColor);
        }

        /// <summary>
        /// 벽 바운드. 전용 시트가 없으면 <see cref="Impact"/>와 같은 그림으로 떨어진다.
        /// 벽에 처박는 순간은 콤보에서 제일 잘 보이는 지점이라 칸을 따로 뒀다.
        /// </summary>
        public static void WallBounce(Vector3 ground, float height, Vector3 normal, float radius)
        {
            if (!Enabled) return;

            VfxClip clip = Library != null ? Library.wall : null;
            if (clip == null || !clip.IsValid)
            {
                Impact(ground, height, normal, radius);
                return;
            }

            VfxRunner r = VfxRunner.Instance;
            if (r == null) return;

            r.SpawnSprite(clip, ground, height, normal, radius, ImpactColor);
        }

        private static VfxLibrary Library => VfxLibrary.Get();

        /// <summary>스킬 전용 시트가 우선. 없으면 전역, 그것도 없으면 null(그리지 않음).</summary>
        private static VfxClip Pick(VfxClip own, VfxClip fallback)
        {
            if (own != null && own.IsValid) return own;
            return fallback != null && fallback.IsValid ? fallback : null;
        }

        /// <summary>
        /// 조준 표시(<see cref="RangeIndicator"/>)가 쓰는 공유 머티리얼.
        /// 선이 정점 색을 따르도록 스프라이트 셰이더를 쓴다.
        /// 빌드에서 셰이더가 스트립되지 않도록 Always Included Shaders 확인이 필요하다.
        /// </summary>
        internal static Material CreateMaterial()
        {
            Shader s = Shader.Find("Sprites/Default")
                       ?? Shader.Find("Universal Render Pipeline/Unlit")
                       ?? Shader.Find("Unlit/Color");

            if (s == null)
            {
                BattleLog.Warn(LogCategory.State, "BattleVfx: 셰이더를 찾지 못했다. 연출이 보이지 않는다.");
                return null;
            }

            return new Material(s) { hideFlags = HideFlags.HideAndDontSave };
        }
    }

    /// <summary>
    /// <see cref="VfxSprite"/> 풀. 씬에 미리 놓지 않아도 되도록 첫 호출에 스스로 생긴다
    /// (<see cref="EffectRunner"/>와 같은 방식).
    /// </summary>
    public class VfxRunner : MonoBehaviour
    {
        private const int Prewarm = 8;

        private static VfxRunner instance;
        private static bool quitting;

        /// <summary>
        /// 첫 타격 전에 미리 띄운다.
        ///
        /// 이 게터는 <see cref="BattleVfx.Impact"/> 계열에서만 닿는다. 그런데
        /// <see cref="RangeIndicator"/>·<see cref="ChargeGauge"/>도 여기 붙으므로,
        /// 아무도 때리지 않은 동안에는 조준 표시가 아예 존재하지 않았다 —
        /// 전투 시작 직후 첫 불릿타임에서 안 보이던 원인.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            quitting = false;
            _ = Instance;
        }

        private readonly Stack<VfxSprite> idleSprites = new Stack<VfxSprite>();

        public static VfxRunner Instance
        {
            get
            {
                if (instance != null) return instance;
                // 에디터 정지 중이나 종료 중에 오브젝트를 만들면 씬에 쓰레기가 남는다.
                if (quitting || !Application.isPlaying) return null;

                var go = new GameObject("[BattleVfx]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<VfxRunner>();
                go.AddComponent<RangeIndicator>();
                // 사거리 원 옆에 "어디로 밀려나는가"를 같이 그린다. 둘 다 조준 중에만 켜진다.
                go.AddComponent<KnockbackIndicator>();
                go.AddComponent<ChargeGauge>();
                go.AddComponent<AttackRangeIndicator>();
                // 내 스킬이 선딜~후딜 동안 어디를 때리는지. 적 예고와 같은 자리, 다른 색.
                go.AddComponent<SkillRangeIndicator>();
                // 이 러너는 첫 연출이 터질 때 만들어진다. 라벨이 필요한 시점은
                // 적이 맞은 뒤인데 타격은 항상 Impact 연출을 동반하므로 순서가 어긋나지 않는다.
                go.AddComponent<EnemyStateLabel>();
                // 상태 게이지는 적뿐 아니라 아군도 그린다 — 내 보호막이 언제 풀리는지는
                // 상대 경직만큼이나 급한 정보다.
                go.AddComponent<StatusEffectBar>();
                // 표식 레이어. 소스를 같은 오브젝트에서 찾아 매 프레임 모아 그린다.
                // 레이어를 먼저 얹어도 되고 나중이어도 된다 — 소스 탐색은 첫 LateUpdate에서 한다.
                go.AddComponent<MarkerLayer>();
                // 조작 중인 캐릭터 머리 위 화살표
                go.AddComponent<ControlledCharacterArrow>();
                return instance;
            }
        }

        private void Awake()
        {
            for (int i = 0; i < Prewarm; i++)
                idleSprites.Push(NewSprite());
        }

        public void SpawnSprite(VfxClip clip, Vector3 ground, float height, Vector3 facing,
                                float radius, Color skillColor)
        {
            if (clip == null || !clip.IsValid) return;

            VfxSprite s = idleSprites.Count > 0 ? idleSprites.Pop() : NewSprite();
            s.Play(clip, ground, height, facing, radius, skillColor, RecycleSprite);
        }

        private VfxSprite NewSprite()
        {
            var go = new GameObject("VfxSprite");
            go.transform.SetParent(transform, false);
            return go.AddComponent<VfxSprite>();
        }

        private void RecycleSprite(VfxSprite s)
        {
            if (s != null) idleSprites.Push(s);
        }

        private void OnApplicationQuit() => quitting = true;

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }

    // ══ VfxSprite ═══════════════════════════════════════════

    /// <summary>
    /// 풀링되는 스프라이트 이펙트 하나. <see cref="VfxRunner"/>만 만들고 되돌려 받는다.
    ///
    /// 좌표 접기와 정렬 공식은 <see cref="BeltScrollView"/>와 같은 것을 쓴다 —
    /// 두 벌이 어긋나면 이펙트가 캐릭터와 다른 높이에 뜬다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class VfxSprite : MonoBehaviour
    {
        private SpriteRenderer sr;
        private VfxClip clip;

        private Vector3 origin;      // 논리 좌표. 바닥(y = 0) 기준
        private float height;
        private Color tint;
        private float duration;
        private float timer;
        private bool active;
        private Action<VfxSprite> onDone;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.enabled = false;
        }

        /// <summary>
        /// 재생 시작. ground는 <b>논리 좌표</b>다 — 접힌 화면 좌표를 넣으면 두 번 접혀 위로 튄다.
        /// </summary>
        public void Play(VfxClip clip, Vector3 ground, float height, Vector3 facing,
                         float radius, Color skillColor, Action<VfxSprite> onDone)
        {
            this.clip = clip;
            this.origin = new Vector3(ground.x, 0f, ground.z);
            this.height = height + clip.heightOffset;
            this.onDone = onDone;

            duration = Mathf.Max(0.02f, clip.Duration);
            timer = 0f;
            active = true;

            tint = clip.useSkillColor ? clip.tint * skillColor : clip.tint;

            ApplyTransform(facing, radius);

            // 캐릭터와 같은 규칙으로 깊이 정렬한다(BeltScrollView.ApplySorting).
            sr.sortingOrder = Mathf.RoundToInt(-origin.z * 100f) + clip.sortingOffset;

            sr.enabled = true;
            Redraw(0f);
        }

        /// <summary>
        /// 위치 · 회전 · 크기는 한 번만 잡는다. 이펙트가 도중에 움직이지는 않는다.
        /// </summary>
        private void ApplyTransform(Vector3 facing, float radius)
        {
            transform.position = BeltScroll.ToView(origin, height);

            facing.y = 0f;
            bool hasFacing = facing.sqrMagnitude > 0.0001f;

            if (clip.rotateToFacing && hasFacing)
            {
                // 카메라가 기울어 있어 바닥 방향이 화면에서 납작해 보인다. 그만큼 각도를 눕혀야
                // 궤적이 캐릭터가 실제로 가는 쪽과 같은 각도로 뜬다.
                //
                // 바닥 벡터 (x, 0, z)의 화면 성분은 right·v = x, up·v = z·sinθ 다.
                // ScreenUp.z가 곧 sinθ이므로 각도를 따로 들고 있을 필요가 없다.
                float deg = Mathf.Atan2(facing.z * BeltScroll.ScreenUp.z, facing.x) * Mathf.Rad2Deg;

                // 빌보드로 먼저 카메라를 마주 본 뒤, 그 평면 안에서 각도를 돌린다. 순서를 뒤집으면
                // 회전축이 월드 Z가 돼 스프라이트가 화면 밖으로 기운다.
                transform.rotation = BeltScroll.Billboard * Quaternion.Euler(0f, 0f, deg);
            }
            else
            {
                transform.rotation = BeltScroll.Billboard;
            }

            sr.flipX = clip.flipToFacing && hasFacing && facing.x < 0f;

            float s = clip.scale;
            if (clip.fitRadius)
            {
                // 첫 프레임의 원본 크기를 기준으로 지름을 맞춘다.
                Sprite first = clip.frames[0];
                float size = first != null ? Mathf.Max(first.bounds.size.x, first.bounds.size.y) : 1f;
                if (size > 0.0001f) s *= radius * 2f / size;
            }

            transform.localScale = new Vector3(s, s, 1f);
        }

        private void Update()
        {
            // 풀에 놀고 있는 것은 계산하지 않는다.
            if (!active) return;

            // 불릿타임에는 멈춰 있어야 한다. 투사체와 같은 시계.
            float dt = TimeControl.DeltaTime;
            if (dt <= 0f) return;

            timer += dt;

            float t = timer / duration;
            if (t >= 1f)
            {
                Stop();
                return;
            }

            Redraw(t);
        }

        private void Redraw(float t)
        {
            sr.sprite = clip.FrameAt(t);

            Color c = tint;
            if (clip.fadeOut > 0f && t > 1f - clip.fadeOut)
                c.a *= Mathf.InverseLerp(1f, 1f - clip.fadeOut, t);

            sr.color = c;
        }

        private void Stop()
        {
            active = false;
            sr.enabled = false;
            sr.sprite = null;

            Action<VfxSprite> done = onDone;
            onDone = null;
            done?.Invoke(this);
        }
    }

    // ══ SkillVfx ═══════════════════════════════════════════

    /// <summary>
    /// 스킬 하나의 이펙트 색·크기. <see cref="SkillData"/>가 들고 있다가
    /// 타격을 낼 때 히트박스로 흘려 보낸다.
    ///
    /// <b>기본값(<c>default</c>)은 "전역 설정을 쓴다"는 뜻이다</b> —
    /// 필드를 추가해도 기존 에셋이 검은색으로 터지지 않는다.
    /// </summary>
    [Serializable]
    public struct SkillVfx
    {
        [Tooltip("끄면 BattleVfx의 전역 색을 쓴다. 켜면 아래 값이 이 스킬에만 적용된다.")]
        public bool custom;

        [Tooltip("적중 순간 파편 색. 시전 이펙트도 이 색을 쓴다.")]
        public Color impact;
        [Tooltip("적중 순간 충격파 색. 파편보다 옅게 두면 겹쳐 보인다.")]
        public Color shock;

        [Tooltip("이펙트 크기 배율. 0이면 1로 본다 — 판정 크기는 건드리지 않는다.")]
        public float scale;

        [Header("전용 시트 (비우면 VfxLibrary 전역값)")]
        [Tooltip("시전 순간. 조준으로 찍은 자리에 뜬다.")]
        public VfxClip castClip;
        [Tooltip("적중 순간.")]
        public VfxClip hitClip;

        /// <summary>
        /// 이 타격이 스킬에서 나왔는지. <b>인스펙터에 노출하지 않는다</b> —
        /// <see cref="SkillState"/>가 히트박스로 넘기는 복사본에만 런타임으로 켠다.
        ///
        /// 히트박스는 평타와 공유된다(<c>Entity.SkillAttack</c>). 그래서 히트박스 쪽에서는
        /// 평타인지 스킬인지 알 방법이 없다. 색과 같은 경로로 이 표시를 같이 실어 보낸다.
        /// </summary>
        [NonSerialized] public bool fromSkill;

        /// <summary>0을 1로 접어 주는 읽기 창구. 인스펙터에서 안 채운 값이 이펙트를 지우지 않게.</summary>
        public float Scale => scale > 0f ? scale : 1f;

        /// <summary>스킬 경로로 넘길 복사본. 원본 에셋 값은 건드리지 않는다.</summary>
        public SkillVfx AsSkill()
        {
            SkillVfx v = this;
            v.fromSkill = true;
            return v;
        }

        /// <summary>인스펙터에서 custom을 켰을 때 출발점이 되는 값.</summary>
        public static SkillVfx Default => new SkillVfx
        {
            custom = false,
            impact = new Color(1f, 0.86f, 0.45f, 1f),
            shock = new Color(1f, 0.55f, 0.3f, 0.75f),
            scale = 1f,
        };
    }
}
