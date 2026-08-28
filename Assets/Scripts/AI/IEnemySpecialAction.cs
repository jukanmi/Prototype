using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 특수 행동이 <b>곧 때릴 자리</b>. 바닥에 그려 주기 위한 최소 정보다.
    ///
    /// 히트박스는 프리팹의 <c>BoxCollider</c>라 실제로는 3D 상자지만, 표시에 필요한 건
    /// 바닥 발자국뿐이다 — 벨트스크롤에서 피하고 못 피하고를 가르는 건 XZ 평면이다.
    ///
    /// <see cref="Prototype.AttackRangeIndicator"/>가 그린다.
    /// </summary>
    public struct AttackRangePreview
    {
        /// <summary>월드 지상 좌표(y는 무시한다).</summary>
        public Vector3 center;
        /// <summary>정면. 정규화되어 있다.</summary>
        public Vector3 facing;
        /// <summary>좌우 반폭.</summary>
        public float halfWidth;
        /// <summary>전후 반길이. 전진 패턴은 이동 거리만큼 늘어난다.</summary>
        public float halfLength;
        /// <summary>
        /// 0보다 크면 <b>원</b>이다 — 둘레 전부를 때리는 패턴(횡베기).
        /// 이때 <see cref="halfWidth"/> · <see cref="halfLength"/>는 쓰이지 않는다.
        /// <see cref="coneAngle"/>도 0보다 크면 이 반지름을 부채꼴 반지름으로 같이 쓴다.
        /// </summary>
        public float radius;
        /// <summary>0보다 크면 부채꼴의 중심각(도). <see cref="radius"/> · <see cref="facing"/>과 함께 쓴다.</summary>
        public float coneAngle;
        /// <summary>0~1. 타격까지 얼마나 왔는지 — 표시가 진해지는 정도로 쓴다.</summary>
        public float progress;

        /// <summary>
        /// 원으로 그릴지. 상자를 원으로 그리면 모서리에서 거짓말이 되고, 반대도 마찬가지다.
        /// <see cref="IsCone"/>도 <c>radius &gt; 0f</c>라 같이 true가 된다 — 그리는 쪽은
        /// <see cref="IsCone"/>을 먼저 검사해야 부채꼴이 원으로 잘못 그려지지 않는다.
        /// </summary>
        public bool IsCircle => radius > 0f;

        /// <summary>부채꼴로 그릴지. 둘레 전부가 아니라 정면 쪽 각도만 위험하다는 뜻이다.</summary>
        public bool IsCone => coneAngle > 0f;

        /// <summary>
        /// 히트박스 상자 하나를 발자국으로 편다. <b>순수 함수</b> —
        /// 전진 거리를 더하는 계산이 화면에서 눈으로 검증하기 가장 어려운 부분이라
        /// 테스트가 직접 부른다.
        /// </summary>
        /// <param name="ownerGround">시전자 발밑 월드 좌표.</param>
        /// <param name="facing">시전자 정면. 히트박스 로컬 +Z가 이 방향이다.</param>
        /// <param name="localOffset">히트박스의 시전자 기준 로컬 오프셋(x=좌우, z=앞뒤).</param>
        /// <param name="boxSize">히트박스 크기(x=폭, z=길이).</param>
        /// <param name="advanceDistance">발동 중 전진 거리. 0이면 제자리.</param>
        public static AttackRangePreview FromBox(
            Vector3 ownerGround, Vector3 facing, Vector3 localOffset, Vector3 boxSize,
            float advanceDistance, float progress)
        {
            Vector3 f = Flatten(facing);

            // LookRotation(f)의 오른쪽 축과 같다. 쿼터니언을 만들지 않고 직접 돌린다 —
            // 매 프레임 적 수만큼 도는 자리다.
            Vector3 right = new Vector3(f.z, 0f, -f.x);

            float advance = Mathf.Max(0f, advanceDistance);

            // 전진하는 동안 상자가 쓸고 지나간 자리 전체가 위험 구역이다.
            // 길이를 늘리는 만큼 중심도 앞으로 밀어야 뒤쪽 경계가 제자리에 남는다.
            Vector3 center = ownerGround
                             + right * localOffset.x
                             + f * (localOffset.z + advance * 0.5f);
            center.y = 0f;

            return new AttackRangePreview
            {
                center = center,
                facing = f,
                halfWidth = Mathf.Max(0f, boxSize.x) * 0.5f,
                halfLength = Mathf.Max(0f, boxSize.z) * 0.5f + advance * 0.5f,
                progress = Mathf.Clamp01(progress),
            };
        }

        /// <summary>
        /// 둘레 전부를 때리는 판정. 상자와 달리 <b>방향이 없다</b> —
        /// 보스가 어디를 보고 있든 같은 자리를 덮으므로 정면을 받지 않는다.
        /// </summary>
        public static AttackRangePreview FromCircle(Vector3 center, float radius, float progress)
        {
            center.y = 0f;

            return new AttackRangePreview
            {
                center = center,
                facing = Vector3.forward,
                radius = Mathf.Max(0f, radius),
                progress = Mathf.Clamp01(progress),
            };
        }

        /// <summary>
        /// 전방 부채꼴 판정(<see cref="EffectUtil.ConeStrike"/>와 같은 모양). 원과 달리
        /// <b>방향을 받는다</b> — 정면 각도 밖은 안전하다는 뜻이라 시전자가 어디를 보는지가 곧 정보다.
        /// </summary>
        public static AttackRangePreview FromCone(Vector3 center, Vector3 facing, float radius,
                                                   float angleDegrees, float progress)
        {
            center.y = 0f;

            return new AttackRangePreview
            {
                center = center,
                facing = Flatten(facing),
                radius = Mathf.Max(0f, radius),
                coneAngle = Mathf.Max(0f, angleDegrees),
                progress = Mathf.Clamp01(progress),
            };
        }

        /// <summary>
        /// 둘레 판정 히트박스를 읽는다. 구가 아니면 false — 그때는 상자 경로로 떨어진다.
        ///
        /// 반경에 <c>lossyScale</c>의 <b>최댓값</b>을 곱한다. 유니티의 SphereCollider가
        /// 그렇게 동작하기 때문이다 — 축마다 다른 배율을 줘도 구는 찌그러지지 않고
        /// 가장 큰 축을 따른다. 판정과 표시가 같은 규칙을 써야 한다.
        /// </summary>
        public static bool TryReadSphere(Attack hitbox, out Vector3 worldCenter, out float radius)
        {
            worldCenter = default;
            radius = 0f;

            if (hitbox == null) return false;

            var sphere = hitbox.GetComponent<SphereCollider>();
            if (sphere == null) return false;

            worldCenter = sphere.transform.TransformPoint(sphere.center);

            Vector3 s = sphere.transform.lossyScale;
            radius = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            return true;
        }

        /// <summary>
        /// 히트박스 상자를 시전자 로컬 기준으로 읽는다. 상자가 아니면 false —
        /// 그리지 않는 편이 틀린 자리에 그리는 것보다 낫다.
        ///
        /// 시전자의 자식이 아닐 수도 있으므로 월드를 한 번 거쳐 되돌린다.
        /// </summary>
        public static bool TryReadBox(Attack hitbox, Transform ownerRoot,
                                      out Vector3 localOffset, out Vector3 size)
        {
            localOffset = default;
            size = default;

            if (hitbox == null || ownerRoot == null) return false;

            var box = hitbox.GetComponent<BoxCollider>();
            if (box == null) return false;

            localOffset = ownerRoot.InverseTransformPoint(box.transform.TransformPoint(box.center));
            size = Vector3.Scale(box.size, box.transform.lossyScale);
            return true;
        }

        private static Vector3 Flatten(Vector3 dir)
        {
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        }
    }

    /// <summary>
    /// 평타가 아닌 행동의 <b>실행기</b>. 예고 · 이동 · 히트박스처럼 인스턴스가 있어야 되는 일을 맡는다.
    /// 판단(언제 쓸지)은 <see cref="IEnemyBrain"/>이, 타이머는 <see cref="EnemyControl"/>이 갖는다.
    ///
    /// <b>Entity 하나에 구현체는 하나만 붙인다.</b> 패턴이 여럿이면 컴포넌트를 늘리지 말고
    /// 한 구현체가 <see cref="Count"/>개의 패턴을 들게 한다 — 여러 컴포넌트에 인덱스를 나눠 주면
    /// 인스펙터의 컴포넌트 순서가 곧 패턴 번호가 되어, 순서를 바꾸는 것만으로 보스가 다른 기술을 쓴다.
    /// </summary>
    public interface IEnemySpecialAction
    {
        /// <summary>가진 패턴 수. EnemyControl이 쿨 타이머 배열의 크기를 여기에 맞춘다.</summary>
        int Count { get; }

        bool IsRunning { get; }

        /// <summary>
        /// 시작 시도. 거절하면(false) 쿨이 소모되지 않는다 —
        /// 1회성 패턴은 두 번째 요청을 여기서 막으면 브레인이 자연히 다음 패턴을 고른다.
        /// </summary>
        bool TryStart(int index, Entity target);

        /// <summary>EnemyControl이 실행 중에만 부른다.</summary>
        void Tick(float dt);

        /// <summary>피격 · 사망 · AI 정지. 어느 단계든 흔적 없이 되돌린다.</summary>
        void Cancel();

        /// <summary>
        /// 지금 바닥에 그려 줄 범위가 있는지. <b>차징 · 예고 중에만</b> true다 —
        /// 발동에 들어가면 이미 판정이 나가고 있어, 표시가 정보가 아니라 잔상이 된다.
        /// </summary>
        bool TryGetRange(out AttackRangePreview range);
    }
}
