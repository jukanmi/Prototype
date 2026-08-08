using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 날아가는 판정. 스킬샷 · 화살 · 마력탄이 전부 이것 하나로 굴러간다.
    ///
    /// 루트는 <b>논리 좌표</b>(XZ 평면 + Y 높이)에 있고 콜라이더도 거기 붙는다 — 판정은 진짜 3D다.
    /// 화면에 그리는 건 <see cref="BeltScroll"/>로 접은 자식뿐이다. 둘을 섞으면
    /// 조준점 때처럼 "보이는 곳"과 "맞는 곳"이 어긋난다.
    /// </summary>
    [RequireComponent(typeof(Attack))]
    public class Projectile : MonoBehaviour
    {
        [Tooltip("화면에 그릴 자식. 논리 좌표를 접어서 여기에 얹는다.")]
        [SerializeField] private Transform sprite;
        [Tooltip("바닥 그림자. 비워도 된다.")]
        [SerializeField] private Transform shadow;
        [Tooltip("발사 지점을 시전자 앞으로 얼마나 밀지.")]
        [SerializeField] private float spawnOffset = 0.8f;
        [Tooltip("날아가는 높이. 0이면 바닥을 긁는다.")]
        [SerializeField] private float flightHeight = 0.6f;

        private Attack hitbox;

        private Vector3 logical;      // 논리 좌표. 바닥 기준 XZ + 높이는 flightHeight
        private Vector3 direction;
        private float speed;
        private float remaining;      // 남은 사거리
        private int pierceLeft;       // 남은 관통 수. 0이면 다음 적중에 소멸
        private LayerMask wallMask;
        private bool live;

        private float blastRadius;    // 0이면 직격 하나만 맞는다
        private SkillVfx blastStyle;

        private void Awake()
        {
            hitbox = GetComponent<Attack>();
        }

        /// <summary>
        /// 발사. 시전자 · 판정 데이터 · 방향을 받아 살아난다.
        ///
        /// <paramref name="height"/>가 음수면 프리팹의 <see cref="flightHeight"/>를 쓴다.
        /// 공중에 띄운 적을 노릴 때는 대상 높이를 넘겨야 한다 — 고정 높이로 쏘면
        /// 바닥을 긁고 지나가 공중 콤보가 통째로 끊긴다.
        ///
        /// <paramref name="blast"/>가 양수면 <b>첫 적중 지점에서 그 반경만큼 터진다</b>.
        /// 0이면 직격 하나만 맞는다 — 평타가 이쪽이다.
        /// </summary>
        public void Launch(Combat attacker, in HitData hit, Vector3 origin, Vector3 dir,
                           float speed, float range, int pierce, LayerMask wallMask, int layer,
                           in SkillVfx vfx = default, float height = -1f, float blast = 0f)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f) dir = Vector3.forward;
            direction = dir.normalized;

            this.speed = Mathf.Max(0.1f, speed);
            this.wallMask = wallMask;
            remaining = Mathf.Max(0.5f, range);
            pierceLeft = Mathf.Max(0, pierce);

            origin.y = 0f;
            logical = origin + direction * spawnOffset;
            logical.y = height >= 0f ? height : flightHeight;

            blastRadius = Mathf.Max(0f, blast);
            blastStyle = vfx;

            gameObject.layer = layer;
            transform.position = logical;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            hitbox.Attacker = attacker;
            hitbox.OnHit += HandleHit;
            // 궤적은 투사체에서 억제된다. 색은 적중 이펙트에만 쓰인다.
            hitbox.Begin(in hit, in vfx);

            live = true;
            UpdateView();

            BattleLog.Log(LogCategory.Skill,
                $"  └ 투사체 발사 {BattleLog.Name(attacker)} dir={direction} 속도 {speed:0.#} 사거리 {range:0.#} 관통 {pierce}", this);
        }

        private void Update()
        {
            if (!live) return;

            // 불릿타임에는 그대로 멈춰 있어야 한다. Time.deltaTime을 쓰면 안 된다.
            float dt = TimeControl.DeltaTime;
            if (dt <= 0f) return;

            float step = speed * dt;

            // 히트박스 ↔ 벽은 충돌 매트릭스에서 꺼 뒀다. 직접 본다.
            if (UnityEngine.Physics.Raycast(logical, direction, step, wallMask))
            {
                BattleLog.Log(LogCategory.Skill, "  └ 투사체 벽 적중 — 소멸", this);
                Despawn();
                return;
            }

            logical += direction * step;
            transform.position = logical;

            remaining -= step;
            UpdateView();

            if (remaining <= 0f) Despawn();
        }

        private void HandleHit(Combat victim)
        {
            if (!live) return;

            Detonate(victim);

            if (pierceLeft <= 0)
            {
                Despawn();
                return;
            }

            pierceLeft--;
        }

        /// <summary>
        /// 도착 지점 폭발. 히트박스에 직접 닿은 하나는 이미 맞았으므로 빼고,
        /// 반경 안 나머지에게 같은 판정을 먹인다.
        ///
        /// 상승 화살이 한 명만 띄우던 걸 <b>반경 안 전부</b> 띄우게 만드는 지점이다.
        /// </summary>
        private void Detonate(Combat direct)
        {
            if (blastRadius <= 0f || hitbox == null || hitbox.Attacker == null) return;

            Vector3 center = logical;
            center.y = 0f;

            HitData hit = hitbox.HitData;
            int extra = EffectUtil.AreaStrike(center, blastRadius, hitbox.Attacker,
                                              in hit, in blastStyle, direct);

            BattleLog.Log(LogCategory.Skill,
                $"  └ 투사체 폭발 중심 {center} 반경 {blastRadius:0.#} → 직격 1 + 추가 {extra}마리", this);
        }

        /// <summary>논리 좌표를 화면 좌표로 접는다. 캐릭터와 같은 변환을 쓴다.</summary>
        private void UpdateView()
        {
            Vector3 ground = logical;
            ground.y = 0f;

            if (sprite != null)
            {
                sprite.position = BeltScroll.ToView(ground, logical.y);
                sprite.rotation = Quaternion.identity;   // 빌보드
            }

            if (shadow != null)
            {
                shadow.position = BeltScroll.ToView(ground);
                shadow.rotation = Quaternion.identity;
            }
        }

        private void Despawn()
        {
            live = false;

            if (hitbox != null)
            {
                hitbox.OnHit -= HandleHit;
                hitbox.End();
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (hitbox != null) hitbox.OnHit -= HandleHit;
        }
    }
}
