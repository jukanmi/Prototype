using System;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 의사 물리(Pseudo-Physics). 바닥 이동(XZ)과 공중 높이(Y) 연산을 <b>완전히 분리</b>한다.
    /// 조작감을 위해 유니티 물리에 맡기지 않고 속도를 코드로 직접 제어한다.
    /// Rigidbody는 벽 충돌 해석에만 쓴다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Physics : MonoBehaviour
    {
        [Header("바닥")]
        [SerializeField] private float groundY = 0f;
        [SerializeField] private LayerMask wallMask = ~0;

        [Header("이동 감각")]
        [Tooltip("XZ 가속도. 클수록 즉각적.")]
        [SerializeField] private float acceleration = 60f;
        [Tooltip("XZ 감속도. 입력이 없을 때 적용.")]
        [SerializeField] private float deceleration = 80f;
        [Tooltip("충격 속도가 사그라지는 비율(1/s).")]
        [SerializeField] private float impulseDamping = 8f;

        [Header("중력")]
        [SerializeField] private float gravity = 30f;
        [Tooltip("내려올 때만 곱하는 중력 배율. 1보다 작으면 정점에서 둥실 떠 있다가 천천히 떨어진다.\n" +
                 "올라가는 속도는 건드리지 않으므로 launchForce 튜닝값이 그대로 유지된다.")]
        [Range(0.1f, 2f)][SerializeField] private float fallGravityScale = 1f;

        [Tooltip("정점에서 붙잡아 두는 시간(행맨타임). 0이면 사용하지 않는다.\n" +
                 "체공에 그대로 더해지므로 띄운 높이를 키우지 않고 시간만 벌 수 있다 —\n" +
                 "너무 높이 뜨면 후속타 히트박스가 닿지 않는다.")]
        [SerializeField] private float apexHangTime = 10f;
        [Tooltip("수직 속도가 이 값 이하이면 정점으로 본다.")]
        [SerializeField] private float apexVelocityThreshold = 3f;

        [Tooltip("공중에서 맞았을 때 launchForce에 곱하는 배율. 1보다 크면 공중 연계가 더 높이 뜬다. " +
                 "지상 첫 타는 영향받지 않으므로 띄우기 시작 높이를 건드리지 않고 공중만 조절할 수 있다.")]
        [Range(0.5f, 3f)][SerializeField] private float airLaunchScale = 1.1f;

        [Tooltip("이미 떠 있는 대상이 맞을 때마다 최소한 이만큼은 올려 준다. 띄우기 값이 없는 평타도 포함. " +
                 "0이면 끈다.\n\n" +
                 "지상에 서 있는 대상에게는 절대 걸리지 않는다 — 걸면 모든 평타가 띄우기가 되어 " +
                 "지상 콤보가 사라진다. 한 대마다 정점이 갱신되므로 콤보를 이어 갈수록 체공만 늘어난다.")]
        [SerializeField] private float airHitLift = 4f;

        [Tooltip("바라보는 방향으로 transform을 돌린다. 히트박스 방향이 여기 따라간다.")]
        [SerializeField] private bool rotateToFacing = true;

        // ── 속도 3종. 합산해서 최종 XZ 속도를 만든다. ──
        private Vector3 internalVelocity;    // 이동 입력 기반
        private Vector3 impulseVelocity;     // 순간 충격 (넉백 등). 시간에 따라 감쇠.
        private Vector3 continuousVelocity;  // 지속 힘 (몹몰이 등). 매 프레임 갱신, 자동 소멸.

        private float verticalVelocity;
        private float baseGravity;
        /// <summary>이번 체공에서 정점에 남은 체류 시간. 띄울 때 충전되고 착지하면 사라진다.</summary>
        private float apexHangLeft;

        private Vector3 desiredMoveDir;
        private float desiredMoveSpeed;
        private bool hasMoveInput;

        private Transform cachedTransform;

        /// <summary>
        /// 지연 초기화. BeltScrollView가 [ExecuteAlways]라 Awake 전에도 읽는다.
        /// </summary>
        public Transform Transform => cachedTransform != null ? cachedTransform : (cachedTransform = base.transform);

        public Rigidbody Rigidbody { get; private set; }
        public PhysicsState PhysicsState { get; private set; } = PhysicsState.Ground;

        /// <summary>바닥 좌표. 그림자와 벨트스크롤 렌더가 이 값을 쓴다.</summary>
        public Vector3 GroundPosition
        {
            get
            {
                Vector3 p = Transform.position;
                p.y = groundY;
                return p;
            }
        }

        /// <summary>바닥으로부터의 높이.</summary>
        public float Height => transform.position.y - groundY;
        public float GroundY => groundY;
        /// <summary>벽으로 볼 레이어. 투사체도 같은 기준으로 소멸한다.</summary>
        public LayerMask WallMask => wallMask;
        public float Gravity => gravity;

        /// <summary>
        /// 충격 감쇠 계수. <see cref="AddImpulse"/>는 지수감쇠라 총 이동거리가
        /// <c>force / impulseDamping</c>로 정해진다 — 끌어당기는 쪽이 거리를 힘으로 환산할 때 쓴다.
        /// </summary>
        public float ImpulseDamping => impulseDamping;

        /// <summary>거리 <paramref name="distance"/>만큼 밀려나게 하는 충격량.</summary>
        public float ImpulseToTravel(float distance) => distance * impulseDamping;

        /// <summary>
        /// <see cref="ImpulseToTravel"/>의 역함수. 충격량 <paramref name="force"/>가 만들어 낼 이동 거리.
        /// 프리뷰가 "어디까지 밀려나는가"를 그릴 때 쓴다 — 실전과 같은 상수를 봐야 거짓말을 안 한다.
        /// </summary>
        public float TravelForImpulse(float force) => impulseDamping > 0.0001f ? force / impulseDamping : 0f;

        public float VerticalVelocity => verticalVelocity;

        /// <summary>
        /// 공중 피격 최소 부양. 띄우기 값이 없는 타격도 이만큼은 올린다.
        /// <b>지상 대상에는 쓰지 않는다</b> — 호출부(<c>Combat.ResolveLaunch</c>)가 공중일 때만 읽는다.
        /// </summary>
        public float AirHitLift => airHitLift;
        public Vector3 Facing { get; private set; } = Vector3.right;

        /// <summary>
        /// 벽에 닿은 순간의 정보. 법선만으로는 "살짝 밀려 닿았다"와 "전속력으로 처박았다"를
        /// 구분할 수 없어 바운드가 항상 같은 세기로 나왔다. 파고든 속도를 같이 넘긴다.
        /// </summary>
        public readonly struct WallHit
        {
            /// <summary>벽에서 바깥으로 나오는 방향(수평 성분만).</summary>
            public readonly Vector3 normal;
            /// <summary>접촉 지점. 연출을 벽면에 붙이는 데 쓴다.</summary>
            public readonly Vector3 point;
            /// <summary>벽으로 파고들던 속도. 0이면 스쳤을 뿐이다.</summary>
            public readonly float speed;

            public WallHit(Vector3 normal, Vector3 point, float speed)
            {
                this.normal = normal;
                this.point = point;
                this.speed = speed;
            }
        }

        /// <summary>착지 순간. Combat이 구독해 공중피격 → 다운 전이를 처리한다.</summary>
        public event Action OnLand;
        /// <summary>벽 접촉. 넉백 중이면 벽 바운드로 이어진다.</summary>
        public event Action<WallHit> OnWallHit;

        /// <summary>합산 수평 속도. 벽 반사와 디버그가 읽는다.</summary>
        public Vector3 HorizontalVelocity
        {
            get
            {
                Vector3 v = internalVelocity + impulseVelocity + continuousVelocity;
                v.y = 0f;
                return v;
            }
        }

        private void Awake()
        {
            cachedTransform = base.transform;
            Rigidbody = GetComponent<Rigidbody>();
            Rigidbody.useGravity = false;
            Rigidbody.freezeRotation = true;
            Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            baseGravity = gravity;
        }

        private void FixedUpdate()
        {
            // 불릿타임 배율을 고정 스텝에 곱해 넣는다. Time.timeScale 미사용(결정 로그 ⑥).
            float dt = Time.fixedDeltaTime * TimeControl.Scale;
            if (dt <= 0f)
            {
                Rigidbody.linearVelocity = Vector3.zero;
                return;
            }

            HandleMovement(dt);
            HandleGravity(dt);
            Apply(dt);
        }

        // ── 입력 API ────────────────────────────────────

        /// <summary>이동 입력. XZ축을 가/감속하며 이동한다.</summary>
        public void Move(Vector3 dir, float speed)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            desiredMoveDir = dir;
            desiredMoveSpeed = speed;
            hasMoveInput = dir.sqrMagnitude > 0.0001f;

            if (hasMoveInput)
                Facing = dir.normalized;
        }

        /// <summary>대쉬. 가속을 무시하고 XZ 속도를 즉시 덮어쓴다.</summary>
        public void Dash(Vector3 dir, float speed)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f) dir = Facing;
            dir.Normalize();

            internalVelocity = dir * speed;
            impulseVelocity = Vector3.zero;
            Facing = dir;

            BattleLog.Log(LogCategory.Physics, $"{name} 대쉬 dir={dir} speed={speed:0.#}", this);
        }

        /// <summary>점프. 목표 높이 h와 도달 시간 t로 중력을 역산한다. G = 2h / t².</summary>
        public void Jump(float height, float time)
        {
            if (time <= 0f) return;

            gravity = 2f * height / (time * time);
            baseGravity = gravity;
            verticalVelocity = gravity * time;   // v0 = G * t = 2h / t
            PhysicsState = PhysicsState.Aerial;

            BattleLog.Log(LogCategory.Physics,
                $"{name} 점프 h={height:0.##} t={time:0.##} → G={gravity:0.##}, v0={verticalVelocity:0.##}", this);
        }

        /// <summary>순간 충격. 넉백 · 띄우기가 사용한다. 기존 관성은 끊는다.</summary>
        public void AddImpulse(Vector3 dir, float force, bool resetInertia = true)
        {
            dir.y = 0f;
            if (resetInertia)
                ResetInertia();

            if (dir.sqrMagnitude > 0.0001f)
            {
                impulseVelocity += dir.normalized * force;
                BattleLog.Log(LogCategory.Physics, $"{name} 넉백 dir={dir.normalized} force={force:0.#}", this);
            }
        }

        /// <summary>
        /// 수직 충격. 띄우기 전용.
        ///
        /// 공중에서 맞으면 <see cref="airLaunchScale"/>을 곱하고, <b>지금 속도보다 느리게는
        /// 만들지 않는다</b>. 그냥 덮어쓰면 올라가는 중에 맞은 후속타의 <c>launchForce</c>가
        /// 현재 상승속도보다 작을 때 몸이 오히려 주저앉았다 — 띄우는 힘이 부족해 보이는 원인이다.
        /// </summary>
        public void AddLaunch(float force)
        {
            if (force <= 0f) return;

            bool aerial = PhysicsState == PhysicsState.Aerial;
            float applied = aerial ? force * airLaunchScale : force;
            if (aerial) applied = Mathf.Max(verticalVelocity, applied);

            verticalVelocity = applied;
            PhysicsState = PhysicsState.Aerial;
            // 다시 띄울 때마다 정점 체류를 새로 채운다. 추가타마다 한 번씩 붕 뜬다.
            apexHangLeft = apexHangTime;

            BattleLog.Log(LogCategory.Physics,
                $"{name} 띄우기 force={force:0.#}" +
                (aerial ? $" → 공중 보정 {applied:0.#} (x{airLaunchScale:0.##})" : string.Empty), this);
        }

        /// <summary>지속 힘. 매 프레임 호출해야 유지된다. ex) 몹몰이 장판.</summary>
        public void AddForce(Vector3 dir, float force)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f) return;
            continuousVelocity += dir.normalized * force;
        }

        /// <summary>
        /// <b>낙하 중일 때만</b> 수직 속도를 끊는다. 상승 중이면 건드리지 않는다.
        ///
        /// <see cref="ResetInertia"/>는 XZ만 지운다 — 떨어지던 속도가 남으면 같은
        /// <c>launchForce</c>인데도 뜨는 높이가 매번 달라 공중 연계가 재현되지 않는다.
        ///
        /// 반대로 올라가는 쪽은 살려 둔다. 상승 중에 0으로 리셋하면 띄워 놓은 몸이
        /// 후속타를 맞는 순간 정점에서 뚝 끊겨 그대로 떨어졌다.
        ///
        /// 공중 상태(<see cref="PhysicsState"/>)는 건드리지 않는다 —
        /// 띄우기 없는 타격이면 그 자리에서 그대로 떨어져야 한다.
        /// </summary>
        public void StopFall()
        {
            if (verticalVelocity >= 0f) return;

            verticalVelocity = 0f;
            apexHangLeft = 0f;
        }

        /// <summary>관성 강제 초기화. 스킬 적중 시 연계가 빗나가는 오차를 차단한다.</summary>
        public void ResetInertia()
        {
            internalVelocity = Vector3.zero;
            impulseVelocity = Vector3.zero;
            continuousVelocity = Vector3.zero;
            desiredMoveDir = Vector3.zero;
            hasMoveInput = false;
        }

        // ── 중력 보정 (전투 시스템 연계) ──────────────────

        /// <summary>중력 원본값을 설정한다. 점프 계산이 되돌릴 기준이다.</summary>
        public void SetGravity(float value)
        {
            baseGravity = value;
            gravity = value;
        }

        /// <summary>낙하 중 중력 배율. 1보다 작으면 천천히 내려온다.</summary>
        public float FallGravityScale
        {
            get => fallGravityScale;
            set => fallGravityScale = Mathf.Max(0.01f, value);
        }

        // ── 내부 처리 ───────────────────────────────────

        /// <summary>XZ축 이동. 입력이 있으면 가속, 없으면 감속.</summary>
        private void HandleMovement(float dt)
        {
            Vector3 target = hasMoveInput ? desiredMoveDir * desiredMoveSpeed : Vector3.zero;
            float rate = hasMoveInput ? acceleration : deceleration;

            internalVelocity = Vector3.MoveTowards(internalVelocity, target, rate * dt);
            impulseVelocity = Vector3.MoveTowards(impulseVelocity, Vector3.zero, impulseDamping * impulseVelocity.magnitude * dt);
        }

        /// <summary>Y축 속도를 중력만큼 감소시키고 착지를 판정한다.</summary>
        private void HandleGravity(float dt)
        {
            float y = Transform.position.y;

            if (PhysicsState == PhysicsState.Aerial || y > groundY + 0.001f)
            {
                // 정점 근처에서 잠깐 붙잡아 둔다. 높이를 키우지 않고 체공만 늘리는 수단이라
                // 후속타 히트박스가 닿는 범위를 유지할 수 있다.
                if (apexHangLeft > 0f && Mathf.Abs(verticalVelocity) <= apexVelocityThreshold)
                {
                    apexHangLeft -= dt;
                    verticalVelocity = 0f;
                }
                else
                {
                    // 내려올 때만 배율을 먹인다. 올라가는 구간은 그대로 둬야 띄운 높이가 안 변한다.
                    float g = verticalVelocity < 0f ? Gravity * fallGravityScale : Gravity;
                    verticalVelocity -= g * dt;
                }

                PhysicsState = PhysicsState.Aerial;
            }

            float nextY = y + verticalVelocity * dt;
            if (PhysicsState == PhysicsState.Aerial && nextY <= groundY && verticalVelocity <= 0f)
            {
                Vector3 p = Transform.position;
                p.y = groundY;
                Transform.position = p;

                verticalVelocity = 0f;
                apexHangLeft = 0f;
                PhysicsState = PhysicsState.Ground;
                OnLand?.Invoke();
            }
        }

        private void Apply(float dt)
        {
            Vector3 horizontal = internalVelocity + impulseVelocity + continuousVelocity;
            horizontal.y = 0f;

            float vy = PhysicsState == PhysicsState.Aerial ? verticalVelocity : 0f;

            // 불릿타임에는 배율만큼 실제 이동량이 줄어야 한다.
            Rigidbody.linearVelocity = (horizontal + Vector3.up * vy) * TimeControl.Scale;

            // 히트박스는 자식 오브젝트라 루트가 돌아야 방향이 맞는다.
            if (rotateToFacing && Facing.sqrMagnitude > 0.0001f)
                Transform.rotation = Quaternion.LookRotation(Facing, Vector3.up);

            // 지속 힘은 매 프레임 다시 쌓아야 유지된다.
            continuousVelocity = Vector3.zero;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if ((wallMask.value & (1 << collision.gameObject.layer)) == 0)
                return;

            ContactPoint c = collision.GetContact(0);

            Vector3 normal = c.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude <= 0.0001f) return;
            normal.Normalize();

            // Rigidbody 속도는 솔버가 이미 지웠다. 우리가 들고 있는 값이 충돌 직전 속도다.
            float into = Mathf.Max(0f, Vector3.Dot(HorizontalVelocity, -normal));

            OnWallHit?.Invoke(new WallHit(normal, c.point, into));
        }

        /// <summary>
        /// 벽 반사. 파고들던 성분을 뒤집고 <paramref name="restitution"/>만큼 남긴다.
        /// 벽을 따라 흐르던 성분은 그대로 둔다 — 비스듬히 박으면 비스듬히 튄다.
        /// </summary>
        /// <returns>튕겨 나가는 속력. 호출부가 연출·띄우기 세기를 여기 맞춘다.</returns>
        public float Reflect(Vector3 normal, float restitution, float minSpeed = 0f)
        {
            normal.y = 0f;
            if (normal.sqrMagnitude <= 0.0001f) return 0f;
            normal.Normalize();

            Vector3 bounced = Vector3.Reflect(HorizontalVelocity, normal) * Mathf.Max(0f, restitution);
            bounced.y = 0f;

            // 반사 후에도 벽 안쪽을 향하는 성분이 남으면 그 자리에서 다시 충돌한다. 잘라낸다.
            float into = Vector3.Dot(bounced, normal);
            if (into < 0f) bounced -= normal * into;

            // 살짝 닿았어도 최소한은 튕겨야 벽에 붙어 비비는 그림이 안 나온다.
            if (bounced.magnitude < minSpeed) bounced = normal * minSpeed;

            internalVelocity = Vector3.zero;
            continuousVelocity = Vector3.zero;
            desiredMoveDir = Vector3.zero;
            hasMoveInput = false;
            impulseVelocity = bounced;

            BattleLog.Log(LogCategory.Physics,
                $"{name} 벽 반사 normal={normal} → v={bounced.magnitude:0.#} (반발 {restitution:0.##})", this);

            return bounced.magnitude;
        }

        /// <summary>
        /// 모으기 계열의 Z축 정렬. 벨트스크롤에서 Z가 어긋나면 후속타가 전부 빗나간다.
        /// </summary>
        public void SnapZ(float z)
        {
            Vector3 p = Transform.position;
            BattleLog.Log(LogCategory.Physics, $"{name} Z 정렬 {p.z:0.##} → {z:0.##} (모으기 보정)", this);
            p.z = z;
            Transform.position = p;
        }

        /// <summary>불릿타임 중 시전자 배치. 위치를 즉시 덮어쓴다.</summary>
        public void Teleport(Vector3 groundPoint)
        {
            BattleLog.Log(LogCategory.Physics, $"{name} 텔레포트 {Transform.position} → {groundPoint}", this);

            groundPoint.y = groundY;
            Transform.position = groundPoint;
            verticalVelocity = 0f;
            PhysicsState = PhysicsState.Ground;
            ResetInertia();
        }

        public void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                Facing = dir.normalized;
        }
    }
}
