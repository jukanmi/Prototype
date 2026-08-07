using System;
using UnityEngine;

namespace Prototype
{
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
                // 깊이가 화면 세로로 접히므로 화면상의 각도도 같은 비율로 눌린다.
                // 접기를 빼먹으면 대각선 방향 궤적이 캐릭터와 다른 각도로 뜬다.
                float deg = Mathf.Atan2(facing.z * BeltScroll.DepthToScreen, facing.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, deg);
            }
            else
            {
                transform.rotation = Quaternion.identity;
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
