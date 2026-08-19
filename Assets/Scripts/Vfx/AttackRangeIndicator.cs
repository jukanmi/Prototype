using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 적의 특수 행동이 <b>곧 때릴 자리</b>를 바닥에 그린다.
    ///
    /// 지금까지 예고는 몸이 하얗게 번쩍이는 것(<see cref="EnemyStateTint"/>)뿐이라
    /// "뭔가 온다"는 알아도 "어디로 피해야 하나"를 알 방법이 없었다. 히트박스는 근접 · 광역
    /// 두 종에 패턴마다 전진 거리까지 달라서, 그려 주지 않으면 외우는 수밖에 없다.
    ///
    /// 플레이어 조준의 <see cref="RangeIndicator"/>와 같은 골격이지만 원이 아니라 사각형이다 —
    /// 실제 판정이 <c>BoxCollider</c>라 원으로 그리면 모서리에서 거짓말이 된다.
    ///
    /// <see cref="ChargeGauge"/>와 같은 자리에 붙는다 — 씬 배선 0.
    /// </summary>
    public class AttackRangeIndicator : MonoBehaviour
    {
        /// <summary>사각형 한 장의 점 수.</summary>
        private const int Corners = 4;

        /// <summary>원 한 장의 점 수. RangeIndicator와 같은 값이라야 두 표시의 매끄러움이 같다.</summary>
        private const int Segments = 40;

        [Tooltip("선 굵기. RangeIndicator와 같은 값이라야 두 표시가 같은 무게로 읽힌다.")]
        [SerializeField] private float lineWidth = 0.12f;

        [Tooltip("막 시작했을 때의 색. 아직 시간이 있다.")]
        [SerializeField] private Color earlyColor = new Color(1f, 0.42f, 0.42f, 0.35f);

        [Tooltip("타격 직전의 색. 로그의 거부색(#FF6B6B)과 같은 톤이다 — 화면과 콘솔이 같은 것을 가리켜야 한다.")]
        [SerializeField] private Color lateColor = new Color(1f, 0.42f, 0.42f, 0.95f);

        private readonly List<LineRenderer> pool = new List<LineRenderer>();
        private Material shared;

        private void Awake()
        {
            shared = BattleVfx.CreateMaterial();
        }

        // 캐릭터 위치는 LateUpdate에 확정된다(BeltScrollView). 그 뒤에 읽어야 한 프레임 밀리지 않는다.
        private void LateUpdate()
        {
            int used = DrawAll(BattleRegistry.Enemies, 0);

            // 남는 선은 끈다. 파괴하지 않는다 — 다음 예고에 다시 쓴다.
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

                if (!(e.Control is EnemyControl control)) continue;

                IEnemySpecialAction special = control.Special;
                if (special == null || !special.TryGetRange(out AttackRangePreview range)) continue;

                Draw(Take(start + n), in range);
                n++;
            }

            return n;
        }

        /// <summary>
        /// 논리 XZ 도형을 화면 좌표로 접는다. 접히고 나면 바닥에 누운 모양으로 보인다 —
        /// 캐릭터가 서 있는 평면과 같은 공간이라야 "저기가 위험"이 읽힌다.
        ///
        /// 판정 모양을 그대로 따라간다. 상자를 원으로 그리면 모서리가, 원을 상자로 그리면
        /// 대각선이 거짓말이 된다 — 표시가 틀리면 없느니만 못하다.
        /// </summary>
        private void Draw(LineRenderer lr, in AttackRangePreview range)
        {
            if (range.IsCircle) DrawCircle(lr, in range);
            else DrawBox(lr, in range);

            // 시간이 갈수록 진해진다. 깜빡임을 안 쓰는 이유: 예고(!)가 이미 깜빡이고 있어
            // 둘 다 깜빡이면 어느 쪽이 급한 신호인지 구분이 안 된다.
            Color c = Color.Lerp(earlyColor, lateColor, range.progress);

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
        /// 둘레 판정. 조준 링(<see cref="RangeIndicator"/>)과 같은 방식으로 접는다 —
        /// 플레이어가 이미 아는 모양이라야 "저 안이 위험"이 즉시 읽힌다.
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

        private LineRenderer Take(int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject("AttackRange");
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
