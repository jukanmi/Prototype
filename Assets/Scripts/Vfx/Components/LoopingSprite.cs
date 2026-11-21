using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 스프라이트 시트 한 벌(<see cref="VfxClip"/>)을 끝없이 돌린다 — 모닥불처럼 제자리에 놓는 연출.
    /// 카메라를 마주 보게 세운다(빌보드). 씬에 놓고 클립만 꽂으면 된다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class LoopingSprite : MonoBehaviour
    {
        [SerializeField] private VfxClip clip;

        private SpriteRenderer sprite;
        private float elapsed;

        private void Awake() => sprite = GetComponent<SpriteRenderer>();

        private void LateUpdate()
        {
            transform.rotation = BeltScroll.Billboard;
            if (clip == null || !clip.IsValid) return;

            elapsed += Time.deltaTime;
            sprite.sprite = clip.frames[Mathf.FloorToInt(elapsed * clip.fps) % clip.frames.Length];
            sprite.color = clip.tint;
        }
    }
}
