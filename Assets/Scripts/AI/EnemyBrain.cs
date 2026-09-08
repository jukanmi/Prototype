// 적 두뇌 규약 — 브레인 인터페이스 · 특수 행동 인터페이스 · 특수 행동 시퀀스.
// 브레인 애셋(BossBrainAsset 등)은 .asset이 물어 각자 파일로 남는다.

using System;
using UnityEngine;

namespace Prototype
{
    // ══ IEnemyBrain ═══════════════════════════════════════════

    /// <summary>
    /// 브레인에 넘기는 수치. EnemyData에서 오거나, 없으면 EnemyControl의 인스펙터 폴백값.
    /// 브레인 애셋이 특정 EnemyData를 참조하면 여러 적이 공유할 수 없으므로 struct로 끊는다.
    /// </summary>
    [Serializable]
    public struct EnemyBrainParams
    {
        [Tooltip("이 거리 안이면 공격한다.")]
        public float attackRange;

        [Tooltip("타겟이 이보다 멀면 포기한다. 0이면 무제한.")]
        public float leashRange;

        [Tooltip("원거리 전용. 이보다 가까우면 물러난다. 0이면 물러나지 않는다.")]
        public float preferredMinRange;

        [Tooltip("특수 행동(돌진)이 닿는 최대 거리. 0이면 특수 행동을 쓰지 않는다.")]
        public float specialRange;
    }

    /// <summary>브레인이 판단에 쓰는 입력. EnemyControl이 매 프레임 채운다.</summary>
    public struct EnemyBrainContext
    {
        /// <summary>특수 행동 인덱스의 상한. 비트마스크가 int라 여기서 막힌다.</summary>
        public const int MaxSpecials = 32;

        public Entity self;

        /// <summary>없으면 null. 브레인이 먼저 검사해야 한다.</summary>
        public Entity target;

        /// <summary>타겟까지의 벡터. XZ 평면이고 정규화 전.</summary>
        public Vector3 toTarget;

        /// <summary>toTarget.magnitude. 브레인이 다시 sqrt를 돌리지 않도록 미리 넣어 준다.</summary>
        public float distance;

        /// <summary>공격 쿨이 끝났는지. 쿨 관리는 EnemyControl이 한다.</summary>
        public bool attackReady;

        /// <summary>
        /// 0번 특수 행동의 쿨이 끝났는지. 평타 쿨과 따로 돈다.
        /// 특수가 하나뿐인 적(돌진 멧돼지)을 위한 지름길이며,
        /// <see cref="specialReadyMask"/>의 0번 비트와 항상 같은 값이다.
        /// </summary>
        public bool specialReady;

        /// <summary>
        /// 특수 행동 인덱스별 쿨 완료 여부. 비트 i가 서 있으면 i번을 지금 쓸 수 있다.
        ///
        /// bool 배열이 아니라 비트마스크인 이유: 컨텍스트는 매 프레임 만들어지는 struct라
        /// 배열을 실으면 프레임마다 할당이 생기고, 브레인은 무상태여야 해서
        /// 자기 쪽에 캐시를 둘 수도 없다.
        /// </summary>
        public int specialReadyMask;

        /// <summary>남은 체력 비율(0~1). 보스 페이즈 판정이 읽는다.</summary>
        public float healthRatio;

        public EnemyBrainParams p;
        public float dt;

        /// <summary>인덱스 하나의 쿨 완료 여부. 브레인이 비트 연산을 직접 쓰지 않게 감싼다.</summary>
        public bool IsSpecialReady(int index)
            => index >= 0 && index < MaxSpecials && (specialReadyMask & (1 << index)) != 0;
    }

    /// <summary>
    /// 브레인이 고른 행동의 종류.
    /// <see cref="Command"/>와 나눠 둔 이유: 특수 행동은 상태머신이 아는 명령이 아니라
    /// EnemyControl이 붙잡고 돌리는 <b>실행기</b>(<see cref="IEnemySpecialAction"/>)가 처리한다.
    /// </summary>
    public enum EnemyActionKind
    {
        None,
        Move,
        Attack,

        /// <summary>실행기가 가진 패턴 하나. 어느 패턴인지는 <see cref="EnemyIntent.specialIndex"/>가 정한다.</summary>
        Special,
    }

    /// <summary>브레인이 내는 결론. EnemyControl이 그대로 Control 프로퍼티로 옮긴다.</summary>
    public struct EnemyIntent
    {
        public EnemyActionKind kind;
        public Command command;
        public Vector3 moveDirection;

        /// <summary>실행기 안에서 몇 번 패턴인가. kind가 Special일 때만 의미가 있다.</summary>
        public int specialIndex;

        /// <summary>
        /// 이 패턴을 쓴 뒤 걸리는 쿨. 0 이하면 EnemyControl의 specialInterval로 떨어진다.
        ///
        /// 쿨 <b>길이</b>를 브레인이 정하고 쿨 <b>타이머</b>는 EnemyControl이 굴린다.
        /// 브레인은 무상태여야 해서 타이머를 못 들고, 실행기는 패턴을 언제 다시 쓸지에
        /// 관여하지 않는다(그건 판단이다).
        /// </summary>
        public float specialCooldown;

        public static EnemyIntent None => default;

        public static EnemyIntent Move(Vector3 dir)
            => new EnemyIntent { kind = EnemyActionKind.Move, command = Command.Move, moveDirection = dir };

        /// <summary>
        /// face는 AttackState.Enter가 Physics.Face에 쓴다.
        /// 비우면 Facing이 갱신되지 않아 마지막 이동 방향으로 헛휘두른다.
        /// </summary>
        public static EnemyIntent Attack(Vector3 face)
            => new EnemyIntent { kind = EnemyActionKind.Attack, command = Command.Attack, moveDirection = face };

        /// <summary>
        /// 특수 행동. command는 None으로 남긴다 — 평타 명령으로 새면 상태머신이 AttackState로 끌고 간다.
        /// </summary>
        public static EnemyIntent Special(int index, Vector3 dir, float cooldown = 0f)
            => new EnemyIntent
            {
                kind = EnemyActionKind.Special,
                command = Command.None,
                moveDirection = dir,
                specialIndex = index,
                specialCooldown = cooldown,
            };
    }

    /// <summary>
    /// 적 종류별 판단 로직. <b>무상태여야 한다</b> — 애셋 하나를 여러 적이 공유한다.
    /// 타이머·타겟이 필요하면 EnemyControl에 두고 컨텍스트로 받는다.
    /// </summary>
    public interface IEnemyBrain
    {
        EnemyIntent Decide(in EnemyBrainContext ctx);
    }

    /// <summary>
    /// ScriptableObject로 만드는 브레인의 공통 베이스.
    /// SerializeReference 대신 애셋을 쓰는 이유: GUID 참조라 클래스 이름을 바꿔도 안 끊기고,
    /// 인스펙터 드래그&드롭이 기본으로 되고, 수치만 다른 변종을 애셋으로 찍을 수 있다.
    /// </summary>
    public abstract class EnemyBrainAsset : ScriptableObject, IEnemyBrain
    {
        public abstract EnemyIntent Decide(in EnemyBrainContext ctx);
    }

    // ══ IEnemySpecialAction ═══════════════════════════════════════════

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

    // ══ EnemySpecialSequence ═══════════════════════════════════════════

    /// <summary>특수 행동의 진행 단계.</summary>
    public enum EnemySpecialPhase
    {
        Idle,
        /// <summary>
        /// 차징. 제자리에서 힘을 모은다 — 머리 위 게이지가 그 양을 보여 준다.
        /// 예고와 달리 <b>길고, 밀 수 있다</b>(<see cref="EnemySpecialSequence.Delay"/>).
        /// 차징 길이가 0인 패턴은 이 단계를 아예 건너뛴다.
        /// </summary>
        Charge,
        /// <summary>예고. 제자리에서 타겟을 계속 노려본다 — 플레이어가 피할 창.</summary>
        Telegraph,
        /// <summary>발동. 예고가 끝난 순간의 방향으로 판정이 나간다.</summary>
        Active,
        /// <summary>후딜. 반격당하는 구간.</summary>
        Recovery,
    }

    /// <summary>
    /// 특수 행동의 <b>시간 축</b>만 담당하는 순수 객체. MonoBehaviour도 물리도 모른다.
    /// 실행(회전·이동·히트박스)은 <see cref="IEnemySpecialAction"/> 구현체가 이 단계를 보고 한다.
    ///
    /// 돌진 · 내려찍기 · 연타가 전부 같은 3단(예고→발동→후딜) 골격이라 한 벌만 둔다.
    /// 길이는 <see cref="Begin(float,float,float)"/>로 시작할 때마다 갈아끼울 수 있어
    /// 패턴마다 다른 타이밍을 쓰면서도 타이머 구현이 늘지 않는다.
    /// </summary>
    public class EnemySpecialSequence
    {
        private float chargeDuration;
        private float telegraphDuration;
        private float activeDuration;
        private float recoveryDuration;

        private float timer;

        /// <summary>길이를 시작할 때마다 넘길 경우.</summary>
        public EnemySpecialSequence() { }

        public EnemySpecialSequence(float telegraph, float active, float recovery)
        {
            SetDurations(0f, telegraph, active, recovery);
        }

        public EnemySpecialPhase Phase { get; private set; } = EnemySpecialPhase.Idle;

        /// <summary>Active 진입 순간에 한 번만 고정되는 진행 방향. 정규화되어 있다.</summary>
        public Vector3 LockedDirection { get; private set; }

        /// <summary>현재 단계에서 흐른 시간. 연출 보간과 다단히트 간격에 쓴다.</summary>
        public float PhaseTime => timer;

        /// <summary>
        /// 예고 표시(!)를 켤 구간인가. 특수 행동은 예고 단계가 곧 예고다.
        ///
        /// <b>차징 단계는 켜지 않는다.</b> "!"는 "지금 피해라"라는 뜻으로 고정해 둬야
        /// <see cref="CombatStateRules.TelegraphLead"/>에 맞춘 패링 타이밍을 익힐 수 있다.
        /// 차징을 알리는 건 머리 위 게이지다.
        /// </summary>
        public bool ShouldShowTelegraph => Phase == EnemySpecialPhase.Telegraph;

        /// <summary>
        /// 차징 진행도 0~1. 차징 중이 아니면 0이다 —
        /// 게이지가 이 값을 보고 그릴지 말지를 정하므로 예고로 넘어간 뒤엔 사라져야 한다.
        /// </summary>
        public float ChargeProgress => Phase == EnemySpecialPhase.Charge && chargeDuration > 0f
            ? Mathf.Clamp01(timer / chargeDuration)
            : 0f;

        public float ChargeDuration => chargeDuration;
        public float TelegraphDuration => telegraphDuration;
        public float ActiveDuration => activeDuration;
        public float RecoveryDuration => recoveryDuration;

        public bool IsRunning => Phase != EnemySpecialPhase.Idle;

        /// <summary>현재 단계의 진행도 0~1. 길이가 0인 단계는 항상 1이다.</summary>
        public float PhaseProgress
        {
            get
            {
                float len = CurrentDuration();
                return len <= 0f ? 1f : Mathf.Clamp01(timer / len);
            }
        }

        /// <summary>
        /// 차징 길이가 0이면 예고부터 시작한다 — 차징을 안 쓰는 패턴은
        /// 이 클래스가 생기기 전과 똑같은 경로를 탄다.
        /// </summary>
        public void Begin()
        {
            Phase = chargeDuration > 0f ? EnemySpecialPhase.Charge : EnemySpecialPhase.Telegraph;
            timer = 0f;
            LockedDirection = Vector3.zero;
        }

        /// <summary>이번에 쓸 길이를 실어 시작한다. 패턴마다 타이밍이 다를 때.</summary>
        public void Begin(float telegraph, float active, float recovery)
        {
            Begin(0f, telegraph, active, recovery);
        }

        /// <summary>차징까지 실어 시작한다.</summary>
        public void Begin(float charge, float telegraph, float active, float recovery)
        {
            SetDurations(charge, telegraph, active, recovery);
            Begin();
        }

        /// <summary>
        /// liveDirection은 예고 중에만 쓴다 — 발동에 들어가면 무시한다.
        /// 유도되는 돌진은 피할 방법이 없어진다.
        /// </summary>
        public void Tick(float dt, Vector3 liveDirection)
        {
            if (Phase == EnemySpecialPhase.Idle) return;

            timer += dt;

            switch (Phase)
            {
                case EnemySpecialPhase.Charge:
                    if (timer >= chargeDuration) Enter(EnemySpecialPhase.Telegraph);
                    return;

                case EnemySpecialPhase.Telegraph:
                    if (timer < telegraphDuration) return;

                    LockedDirection = Normalize(liveDirection);
                    Enter(EnemySpecialPhase.Active);
                    return;

                case EnemySpecialPhase.Active:
                    if (timer >= activeDuration) Enter(EnemySpecialPhase.Recovery);
                    return;

                case EnemySpecialPhase.Recovery:
                    if (timer >= recoveryDuration) Enter(EnemySpecialPhase.Idle);
                    return;
            }
        }

        /// <summary>
        /// 몸통이 무언가에 닿았다. 발동 중일 때만 후딜로 끊는다.
        /// 제자리 패턴은 이걸 구독하지 않는다 — 첫 타 적중이 자기 공격을 끊어 버린다.
        /// </summary>
        public void HitOrWall()
        {
            if (Phase != EnemySpecialPhase.Active) return;
            Enter(EnemySpecialPhase.Recovery);
        }

        /// <summary>차징을 즉시 끝내고 예고로 넘긴다. 외부에서 강제로 터뜨릴 때.</summary>
        public void ReleaseCharge()
        {
            if (Phase != EnemySpecialPhase.Charge) return;

            Enter(EnemySpecialPhase.Telegraph);
        }

        /// <summary>피격·사망·AI 정지. 어느 단계든 즉시 끝낸다.</summary>
        public void Cancel()
        {
            Enter(EnemySpecialPhase.Idle);
        }

        private void SetDurations(float charge, float telegraph, float active, float recovery)
        {
            chargeDuration = Mathf.Max(0f, charge);
            telegraphDuration = Mathf.Max(0f, telegraph);
            activeDuration = Mathf.Max(0f, active);
            recoveryDuration = Mathf.Max(0f, recovery);
        }

        private float CurrentDuration()
        {
            switch (Phase)
            {
                case EnemySpecialPhase.Charge: return chargeDuration;
                case EnemySpecialPhase.Telegraph: return telegraphDuration;
                case EnemySpecialPhase.Active: return activeDuration;
                case EnemySpecialPhase.Recovery: return recoveryDuration;
                default: return 0f;
            }
        }

        private void Enter(EnemySpecialPhase next)
        {
            Phase = next;
            timer = 0f;
        }

        private static Vector3 Normalize(Vector3 dir)
        {
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        }
    }
}
