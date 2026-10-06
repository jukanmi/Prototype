using System.Collections.Generic;
using System;
using UnityEngine;

namespace Prototype
{
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
                // 강화 개체 발밑 고리. 머리 위와 몸 색은 이미 꽉 차 있어 바닥에 둔다(EliteMarkRules).
                go.AddComponent<EliteAura>();
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
}
