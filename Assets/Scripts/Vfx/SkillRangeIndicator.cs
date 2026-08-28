using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
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
    public class SkillRangeIndicator : MonoBehaviour
    {
        /// <summary>원 한 장의 점 수. RangeIndicator · AttackRangeIndicator와 같은 값이라야 매끄러움이 같다.</summary>
        private const int Segments = 40;

        /// <summary>사각형 한 장의 점 수.</summary>
        private const int Corners = 4;

        /// <summary>부채꼴 호 하나를 몇 도 간격으로 쪼갤지. 작을수록 매끄럽다.</summary>
        private const float ConeDegreesPerSegment = 6f;

        [Tooltip("선 굵기. 다른 인디케이터와 같은 값이라야 같은 무게로 읽힌다.")]
        [SerializeField] private float lineWidth = 0.12f;

        [Tooltip("적 예고(빨강 계열)와 구분되는 톤 — RangeIndicator의 okColor와 맞춰 '내 스킬' 신호로 통일한다.")]
        [SerializeField] private Color color = new Color(0.35f, 0.9f, 1f, 0.85f);

        private readonly List<LineRenderer> pool = new List<LineRenderer>();
        private Material shared;

        private void Awake()
        {
            shared = BattleVfx.CreateMaterial();
        }

        // 캐릭터 위치는 LateUpdate에 확정된다(BeltScrollView). 그 뒤에 읽어야 한 프레임 밀리지 않는다.
        private void LateUpdate()
        {
            int used = DrawAll(BattleRegistry.Allies, 0);

            // 남는 선은 끈다. 파괴하지 않는다 — 다음 시전에 다시 쓴다.
            for (int i = used; i < pool.Count; i++)
                pool[i].enabled = false;
        }

        private int DrawAll(IReadOnlyList<Entity> list, int start)
        {
            if (list == null) return 0;

            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Entity e = list[i];
                if (e == null || e.Combat == null || e.Combat.IsDead) continue;
                if (e.StateMachine == null || !(e.StateMachine.CurState is SkillState skill)) continue;
                if (!skill.TryGetRangePreview(out AttackRangePreview range)) continue;

                Draw(Take(start + n), in range);
                n++;
            }

            return n;
        }

        /// <summary>
        /// 논리 XZ 도형을 화면 좌표로 접는다. 접히고 나면 바닥에 누운 모양으로 보인다 —
        /// 캐릭터가 서 있는 평면과 같은 공간이라야 "저기가 맞는 자리"가 읽힌다.
        ///
        /// 판정 모양을 그대로 따라간다 — 부채꼴을 원으로 그리면 각도 밖도 위험해 보이는 거짓말이 된다.
        /// </summary>
        private void Draw(LineRenderer lr, in AttackRangePreview range)
        {
            if (range.IsCone) DrawCone(lr, in range);
            else if (range.IsCircle) DrawCircle(lr, in range);
            else DrawBox(lr, in range);

            lr.startColor = color;
            lr.endColor = color;
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

            for (int i = 0; i <= arcSegments; i++)
            {
                float a = (startAngle + range.coneAngle * i / arcSegments) * Mathf.Deg2Rad;

                // facing을 0도로 두고 좌우로 벌린다. right축은 DrawBox와 같은 규약(LookRotation 오른쪽).
                Vector3 right = new Vector3(range.facing.z, 0f, -range.facing.x);
                Vector3 dir = range.facing * Mathf.Cos(a) + right * Mathf.Sin(a);
                Vector3 ground = range.center + dir * range.radius;

                lr.SetPosition(1 + i, BeltScroll.ToView(ground));
            }
        }

        private LineRenderer Take(int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject("SkillRange");
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

        private void OnDestroy()
        {
            if (shared != null) Destroy(shared);
        }
    }
}
