// 바닥 표시 둘 — 평타 범위 · 스킬 범위.
// 전부 라인 렌더러로 바닥(XZ 평면)에 그리고 같은 깊이 정렬 규약을 쓴다:
//   sortingOrder = round(-z * 100) + 오프셋
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ GroundShapeIndicator ═══════════════════════════════════════════

    /// <summary>
    /// <b>곧 맞을 자리</b>를 바닥에 선으로 그리는 공통 뼈대. 적 예고(<see cref="AttackRangeIndicator"/>)와
    /// 내 스킬(<see cref="SkillRangeIndicator"/>)이 쓴다 — 둘은 <b>어디서 도형을 얻나</b>와 <b>무슨 색인가</b>만 다르다.
    ///
    /// 판정 모양을 그대로 따라간다. 상자를 원으로, 부채꼴을 원으로 그리면 모서리 · 각도 밖이
    /// 거짓말이 된다 — 표시가 틀리면 없느니만 못하다.
    /// </summary>
    public abstract class GroundShapeIndicator : MonoBehaviour
    {
        /// <summary>사각형 한 장의 점 수.</summary>
        private const int Corners = 4;

        /// <summary>원 한 장의 점 수. RangeIndicator와 같은 값이라야 매끄러움이 같다.</summary>
        private const int Segments = 40;

        /// <summary>부채꼴 호 하나를 몇 도 간격으로 쪼갤지. 작을수록 매끄럽다.</summary>
        private const float ConeDegreesPerSegment = 6f;

        [Tooltip("선 굵기. 다른 인디케이터와 같은 값이라야 같은 무게로 읽힌다.")]
        [SerializeField] private float lineWidth = 0.12f;

        private readonly List<LineRenderer> pool = new List<LineRenderer>();
        private Material shared;
        private int used;

        /// <summary>이번 프레임에 그릴 도형을 <see cref="Emit"/>으로 넘긴다.</summary>
        protected abstract void CollectShapes();

        /// <summary>이 도형의 선 색.</summary>
        protected abstract Color ColorFor(in AttackRangePreview range);

        protected virtual void Awake()
        {
            shared = BattleVfx.CreateMaterial();
        }

        // 캐릭터 위치는 LateUpdate에 확정된다(BeltScrollView). 그 뒤에 읽어야 한 프레임 밀리지 않는다.
        protected virtual void LateUpdate()
        {
            used = 0;
            CollectShapes();

            // 남는 선은 끈다. 파괴하지 않는다 — 다음에 다시 쓴다.
            for (int i = used; i < pool.Count; i++)
                pool[i].enabled = false;
        }

        protected void Emit(in AttackRangePreview range)
        {
            LineRenderer lr = Take(used++);

            if (range.IsCone) DrawCone(lr, in range);
            else if (range.IsCircle) DrawCircle(lr, in range);
            else DrawBox(lr, in range);

            Color c = ColorFor(in range);
            lr.startColor = c;
            lr.endColor = c;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;

            // 바닥에 깔린 표시다. 같은 깊이의 캐릭터보다 뒤에 그려야 발을 가리지 않는다.
            lr.sortingOrder = Mathf.RoundToInt(-range.center.z * 100f) - 20;
            lr.enabled = true;
        }

        private static void DrawBox(LineRenderer lr, in AttackRangePreview range)
        {
            Vector3 f = range.facing;
            Vector3 right = new Vector3(f.z, 0f, -f.x);

            Vector3 lengthArm = f * range.halfLength;
            Vector3 widthArm = right * range.halfWidth;

            lr.positionCount = Corners;
            lr.SetPosition(0, BeltScroll.ToView(range.center - lengthArm - widthArm));
            lr.SetPosition(1, BeltScroll.ToView(range.center + lengthArm - widthArm));
            lr.SetPosition(2, BeltScroll.ToView(range.center + lengthArm + widthArm));
            lr.SetPosition(3, BeltScroll.ToView(range.center - lengthArm + widthArm));
        }

        /// <summary>
        /// 둘레 판정. 타원으로 접는다 —
        /// 플레이어가 이미 아는 모양이라야 "저 안"이 즉시 읽힌다.
        /// </summary>
        private static void DrawCircle(LineRenderer lr, in AttackRangePreview range)
        {
            lr.positionCount = Segments;

            for (int i = 0; i < Segments; i++)
            {
                float a = i * (360f / Segments) * Mathf.Deg2Rad;
                Vector3 ground = range.center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * range.radius;
                lr.SetPosition(i, BeltScroll.ToView(ground));
            }
        }

        /// <summary>
        /// 부채꼴 — 중심에서 한쪽 가장자리로, 호를 따라 반대쪽 가장자리로, 그리고
        /// loop가 다시 중심으로 닫는다. 세 변(두 반지름 · 한 호)이 그대로 판정 경계다.
        /// </summary>
        private static void DrawCone(LineRenderer lr, in AttackRangePreview range)
        {
            int arcSegments = Mathf.Clamp(Mathf.RoundToInt(range.coneAngle / ConeDegreesPerSegment), 2, Segments);

            lr.positionCount = arcSegments + 2;
            lr.SetPosition(0, BeltScroll.ToView(range.center));

            float startAngle = -range.coneAngle * 0.5f;

            // facing을 0도로 두고 좌우로 벌린다. right축은 DrawBox와 같은 규약(LookRotation 오른쪽).
            Vector3 right = new Vector3(range.facing.z, 0f, -range.facing.x);

            for (int i = 0; i <= arcSegments; i++)
            {
                float a = (startAngle + range.coneAngle * i / arcSegments) * Mathf.Deg2Rad;
                Vector3 dir = range.facing * Mathf.Cos(a) + right * Mathf.Sin(a);
                lr.SetPosition(1 + i, BeltScroll.ToView(range.center + dir * range.radius));
            }
        }

        private LineRenderer Take(int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject(GetType().Name);
                go.transform.SetParent(transform, false);

                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.alignment = LineAlignment.View;
                lr.loop = true;
                lr.positionCount = Corners;
                lr.numCapVertices = 2;
                lr.numCornerVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.sharedMaterial = shared;
                lr.enabled = false;

                pool.Add(lr);
            }

            return pool[index];
        }

        protected virtual void OnDestroy()
        {
            if (shared != null) Destroy(shared);
        }
    }

    // ══ AttackRangeIndicator ═══════════════════════════════════════════

    /// <summary>
    /// 적의 특수 행동이 <b>곧 때릴 자리</b>를 바닥에 그린다.
    ///
    /// 지금까지 예고는 몸이 하얗게 번쩍이는 것(<see cref="EnemyStateTint"/>)뿐이라
    /// "뭔가 온다"는 알아도 "어디로 피해야 하나"를 알 방법이 없었다. 히트박스는 근접 · 광역
    /// 두 종에 패턴마다 전진 거리까지 달라서, 그려 주지 않으면 외우는 수밖에 없다.
    ///
    /// <see cref="ChargeGauge"/>와 같은 자리에 붙는다 — 씬 배선 0.
    /// </summary>
    public class AttackRangeIndicator : GroundShapeIndicator
    {
        [Tooltip("막 시작했을 때의 색. 아직 시간이 있다.")]
        [SerializeField] private Color earlyColor = new Color(1f, 0.42f, 0.42f, 0.35f);

        [Tooltip("타격 직전의 색. 로그의 거부색(#FF6B6B)과 같은 톤이다 — 화면과 콘솔이 같은 것을 가리켜야 한다.")]
        [SerializeField] private Color lateColor = new Color(1f, 0.42f, 0.42f, 0.95f);

        protected override void CollectShapes()
        {
            IReadOnlyList<Entity> list = BattleRegistry.Enemies;
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                Entity e = list[i];
                if (e == null || e.Combat == null || e.Combat.IsDead) continue;
                if (!(e.Control is EnemyControl control)) continue;

                IEnemySpecialAction special = control.Special;
                if (special != null && special.TryGetRange(out AttackRangePreview range))
                    Emit(in range);
            }
        }

        // 시간이 갈수록 진해진다. 깜빡임을 안 쓰는 이유: 예고(!)가 이미 깜빡이고 있어
        // 둘 다 깜빡이면 어느 쪽이 급한 신호인지 구분이 안 된다.
        protected override Color ColorFor(in AttackRangePreview range)
            => Color.Lerp(earlyColor, lateColor, range.progress);
    }

    // ══ SkillRangeIndicator ═══════════════════════════════════════════

    /// <summary>
    /// 아군이 <b>지금 시전 중인 스킬</b>이 때릴 자리를 바닥에 그린다.
    ///
    /// <see cref="AttackRangeIndicator"/>와 같은 발상이다 — 적의 예고를 보여 주던 걸
    /// 내 스킬에도 붙였다. 다만 적은 "차징·예고 중에만"이고 이쪽은 <b>선딜부터 후딜까지</b> 계속
    /// 보인다(<see cref="SkillState.TryGetRangePreview"/>) — 근접 스킬은 조준 구간이 아예 없어서
    /// (targeting: None) 실행 중에 보여 주지 않으면 범위를 알 방법이 없기 때문이다.
    ///
    /// <see cref="ChargeGauge"/>와 같은 자리에 붙는다 — 씬 배선 0.
    /// </summary>
    public class SkillRangeIndicator : GroundShapeIndicator
    {
        [Tooltip("적 예고(빨강 계열)와 구분되는 톤 — RangeIndicator의 okColor와 맞춰 '내 스킬' 신호로 통일한다.")]
        [SerializeField] private Color color = new Color(0.35f, 0.9f, 1f, 0.85f);

        protected override void CollectShapes()
        {
            IReadOnlyList<Entity> allies = BattleRegistry.Allies;
            if (allies != null)
            {
                for (int i = 0; i < allies.Count; i++)
                {
                    Entity e = allies[i];
                    if (e == null || e.Combat == null || e.Combat.IsDead) continue;
                    if (e.StateMachine == null || !(e.StateMachine.CurState is SkillState skill)) continue;

                    if (skill.TryGetRangePreview(out AttackRangePreview range))
                        Emit(in range);
                }
            }

            // 설치기는 시전자 상태 밖에 산다 — 시전자는 이미 Idle이라 아군 순회로는 안 잡힌다.
            // 후속타가 남은 동안 설치 지점 원을 그린다.
            IReadOnlyList<Installation> live = Installation.Live;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i] != null && live[i].TryGetRangePreview(out AttackRangePreview range))
                    Emit(in range);
            }
        }

        protected override Color ColorFor(in AttackRangePreview range) => color;
    }
}
