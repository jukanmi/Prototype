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
        [Tooltip("날아가는 높이. 0이면 바닥을 긁는다.")]
        [SerializeField] private float flightHeight = 0.6f;

        /// <summary>
        /// 지상에서 쏠 때의 총구 높이. 공중에서 쏘는 쪽이 "발밑 기준 높이 + 이 값"으로
        /// 자기 총구 위치를 계산한다.
        /// </summary>
        public float FlightHeight => flightHeight;

        /// <summary>
        /// 몸의 총구 — 발(공중이면 떠 있는 만큼 위) + 총구 높이. 쏘는 쪽은 여기서 출발하고,
        /// 대상의 같은 자리를 겨눈다. 그래서 둘 다 지상이면 수평으로, 한쪽이 떠 있으면 비스듬히 난다.
        ///
        /// 대상 높이로 <b>수평 비행</b>하던 예전 방식은 점프한 대상을 겨눈 적의 화살이 공중에서 생겨나
        /// 점프 회피가 안 됐다. 지금은 쏘는 순간의 대상 자리로 오르내리므로, 쏜 뒤에 뛰면 밑으로 지나간다.
        ///
        /// 평타(<see cref="Entity.FireBasicProjectile"/>)와 스킬(<see cref="SkillState.LaunchProjectile"/>)이
        /// 같은 규칙을 봐야 같은 화살이 시전 경로에 따라 다르게 날지 않는다.
        /// </summary>
        public Vector3 Muzzle(Physics body) => body.transform.position + Vector3.up * flightHeight;

        private Attack hitbox;

        // 프리팹에 구워진 원래 크기. 깊이 배율을 여기에 곱한다.
        private Vector3 spriteBaseScale = Vector3.one;
        private Vector3 shadowBaseScale = Vector3.one;

        private Vector3 logical;      // 논리 좌표. 바닥 기준 XZ + 높이는 flightHeight
        private Vector3 direction;
        private float speed;
        private float remaining;      // 남은 사거리
        private int pierceLeft;       // 남은 관통 수. 0이면 다음 적중에 소멸
        private LayerMask wallMask;
        private bool live;

        private float blastRadius;    // 0이면 직격 하나만 맞는다
        private SkillVfx blastStyle;
        private bool detonateOnArrival;

        private void Awake()
        {
            hitbox = GetComponent<Attack>();

            // 깊이 배율은 매 프레임 곱해지므로 원본을 한 번만 잡아 둬야 한다.
            // 갱신된 값을 다시 읽으면 배율이 누적돼 투사체가 점점 사라진다.
            if (sprite != null) spriteBaseScale = sprite.localScale;
            if (shadow != null) shadowBaseScale = shadow.localScale;
        }

        /// <summary>
        /// 발사. 시전자 · 판정 데이터 · 방향을 받아 살아난다.
        ///
        /// <paramref name="origin"/>은 출발점(보통 <see cref="Muzzle"/>), <paramref name="dir"/>은 <b>3D</b> 방향이다 —
        /// Y 성분만큼 오르내린다. 사거리도 그 경로를 따라 센다.
        ///
        /// <paramref name="blast"/>가 양수면 <b>첫 적중 지점에서 그 반경만큼 터진다</b>.
        /// 0이면 직격 하나만 맞는다 — 평타가 이쪽이다.
        ///
        /// <paramref name="detonateOnArrival"/>이 켜지면 <b>닿아도 안 터진다</b>. 히트박스를 아예 안 켜
        /// 스치는 적은 아무도 안 맞고, 사거리 끝(= 쏠 때 찍은 자리)에서 <paramref name="blast"/>만큼 터진다.
        /// 마력 화살처럼 "날아가는 그림 + 대상 자리 판정"이 필요한 스킬이 쓴다.
        /// </summary>
        public void Launch(Combat attacker, in HitData hit, Vector3 origin, Vector3 dir,
                           float speed, float range, int pierce, LayerMask wallMask, int layer,
                           in SkillVfx vfx = default, float blast = 0f,
                           bool detonateOnArrival = false)
        {
            if (dir.sqrMagnitude <= 0.0001f) dir = Vector3.forward;
            direction = dir.normalized;

            this.speed = Mathf.Max(0.1f, speed);
            this.wallMask = wallMask;
            // 도착 폭발은 0도 뜻이 있다(코앞 = 그 자리에서 즉시). 접촉 판정은 한 프레임은 살아야 맞힌다.
            remaining = detonateOnArrival ? Mathf.Max(0f, range) : Mathf.Max(0.5f, range);
            pierceLeft = Mathf.Max(0, pierce);

            // 시전자 자리에서 그대로 출발한다. 사거리도 여기서부터 센다 —
            // 좌표로 쏘는 쪽이 |도착점 − 시전자|를 그대로 사거리로 넘길 수 있다.
            logical = origin;

            blastRadius = Mathf.Max(0f, blast);
            blastStyle = vfx;
            this.detonateOnArrival = detonateOnArrival;

            gameObject.layer = layer;
            transform.position = logical;
            // 수직으로 쏘면 LookRotation이 위쪽 축을 못 잡는다. 판정이 구라 회전은 그림에만 쓰인다.
            if (Mathf.Abs(direction.y) < 0.999f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            hitbox.Attacker = attacker;
            hitbox.HitData = hit;   // 폭발이 읽는다. Begin도 같은 값을 넣지만 도착 폭발은 Begin을 안 부른다.

            // 도착 폭발은 히트박스를 안 켠다 — Attack은 Begin 전엔 콜라이더가 꺼져 있어 스쳐도 안 맞는다.
            if (!detonateOnArrival)
            {
                hitbox.OnHit += HandleHit;
                // 궤적은 투사체에서 억제된다. 색은 적중 이펙트에만 쓰인다.
                hitbox.Begin(in hit, in vfx);
            }

            live = true;
            UpdateView();

            BattleLog.Log(LogCategory.Skill,
                $"  └ 투사체 발사 {BattleLog.Name(attacker)} dir={direction} 속도 {speed:0.#} 사거리 {range:0.#} 관통 {pierce}" +
                (detonateOnArrival ? " · 도착 폭발" : ""), this);
        }

        private void Update()
        {
            if (!live) return;

            // 불릿타임에는 그대로 멈춰 있어야 한다. Time.deltaTime을 쓰면 안 된다.
            float dt = TimeControl.DeltaTime;
            if (dt <= 0f) return;

            // 남은 거리보다 더 가지 않는다. 안 자르면 한 프레임 이동량(20 × 0.016 ≈ 0.33)만큼
            // 도착점을 지나쳐 터져, 프리뷰가 그린 자리와 어긋난다.
            float step = Mathf.Min(speed * dt, remaining);

            // 히트박스 ↔ 벽은 충돌 매트릭스에서 꺼 뒀다. 직접 본다.
            if (UnityEngine.Physics.Raycast(logical, direction, step, wallMask))
            {
                BattleLog.Log(LogCategory.Skill, "  └ 투사체 벽 적중 — 소멸", this);
                Despawn();
                return;
            }

            logical += direction * step;
            transform.position = logical;

            // 공중에서 내리꽂은 화살은 대상을 지나 땅으로 들어간다. 바닥 밑에서 계속 날면
            // 뒤에 선 적의 발목을 땅속에서 맞힌다.
            if (logical.y < GroundRegistry.HeightAt(logical, 0f))
            {
                Despawn();
                return;
            }

            remaining -= step;
            UpdateView();

            if (remaining <= 0f)
            {
                // 도착 폭발 — 사거리 끝이 곧 쏠 때 찍은 자리다. 벽에 막혔으면 여기까지 못 온다.
                if (detonateOnArrival) Detonate(null);
                Despawn();
            }
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

            // 비행 고도에서 터진다. 바닥(y=0)으로 누르면 띄운 적을 노린 화살이 발밑에서 터져
            // 정작 그 적은 못 맞는다 — AreaStrike는 이미 대상 고도 중심을 받는다(사슬 속박).
            Vector3 center = logical;

            HitData hit = hitbox.HitData;
            int extra = EffectUtil.AreaStrike(center, blastRadius, hitbox.Attacker,
                                              in hit, in blastStyle, direct);

            BattleLog.Log(LogCategory.Skill,
                direct != null
                    ? $"  └ 투사체 폭발 중심 {center} 반경 {blastRadius:0.#} → 직격 1 + 추가 {extra}마리"
                    : $"  └ 투사체 도착 폭발 중심 {center} 반경 {blastRadius:0.#} → {extra}마리", this);
        }

        /// <summary>논리 좌표를 화면 좌표로 접는다. 캐릭터와 같은 변환 · 같은 깊이 배율을 쓴다.</summary>
        private void UpdateView()
        {
            Vector3 ground = logical;
            ground.y = 0f;

            // 캐릭터만 줄고 화염구는 안 줄면 뒤쪽 적에게 날아갈 때 눈에 띄게 어긋난다.
            float scale = BeltScroll.ScaleAt(ground.z);

            if (sprite != null)
            {
                sprite.position = BeltScroll.ToView(ground, logical.y);
                sprite.rotation = BeltScroll.Billboard;   // 빌보드
                sprite.localScale = spriteBaseScale * scale;
            }

            if (shadow != null)
            {
                // 그림자는 빌보드가 아니라 바닥 평면에 눕는다 — 캐릭터 그림자와 같은 규칙.
                shadow.position = BeltScroll.ToView(ground) + Vector3.up * 0.01f;
                shadow.rotation = BeltScrollView.LieOnGround;
                shadow.localScale = shadowBaseScale * scale;
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
