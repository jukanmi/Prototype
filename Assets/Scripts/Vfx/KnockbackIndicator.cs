using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 불릿타임 조준 중 "이 카드가 적을 어디로 보내는가"를 바닥에 그린다.
    ///
    /// <see cref="RangeIndicator"/>와 같은 방식이다 — 공유 머티리얼,
    /// <see cref="LineRenderer"/> 풀, 파괴하지 않고 <c>enabled</c>만 토글.
    /// 예측 계산은 전부 <see cref="KnockbackPreview"/>가 한다. 여기는 그리기만 한다.
    /// </summary>
    public class KnockbackIndicator : MonoBehaviour
    {
        /// <summary>동시에 그릴 대상 수 상한. 광역 모으기가 화면을 선으로 덮는 걸 막는다.</summary>
        private const int MaxTargets = 8;
        private const int RingSegments = 12;
        /// <summary>화살표 폴리라인 점 수 — 몸통 2 + 촉 4.</summary>
        private const int ArrowPoints = 6;
        /// <summary>촉 길이 비율. 짧은 밀치기에서도 촉이 보이도록 최소값을 같이 둔다.</summary>
        private const float HeadRatio = 0.28f;
        private const float MinHeadLength = 0.25f;
        /// <summary>이 거리보다 덜 움직이면 화살표를 그리지 않는다 — 점처럼 뭉쳐 안 읽힌다.</summary>
        private const float MinTravel = 0.15f;

        [SerializeField] private TargetSelector selector;
        [SerializeField] private ComboPredictor predictor;

        [Tooltip("밀려나는 방향. 사거리 원(하늘색)과 구분되도록 따뜻한 색을 쓴다.")]
        [SerializeField] private Color pushColor = new Color(1f, 0.78f, 0.35f, 0.9f);
        [Tooltip("띄우기 세로 화살표.")]
        [SerializeField] private Color launchColor = new Color(0.7f, 1f, 0.55f, 0.9f);
        [SerializeField] private float lineWidth = 0.1f;
        [Tooltip("초당 깜빡임 횟수. 0이면 고정.")]
        [SerializeField] private float pulseSpeed = 2.2f;

        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private readonly List<Combat> victims = new List<Combat>();
        private Material shared;
        private float pulse;
        private int used;

        private void Awake()
        {
            shared = BattleVfx.CreateMaterial();
        }

        // RangeIndicator와 같은 이유로 LateUpdate에서 돈다 — TargetSelector가 커서를 옮긴 뒤에 그려야
        // 한 프레임 밀리지 않는다.
        private void LateUpdate()
        {
            used = 0;

            if (selector == null) selector = FindAnyObjectByType<TargetSelector>();
            if (predictor == null) predictor = FindAnyObjectByType<ComboPredictor>();

            SkillData data = selector != null && selector.IsSelecting ? selector.Current : null;

            if (data != null && KnockbackPreview.MovesTarget(data))
            {
                pulse += TimeControl.UnscaledDeltaTime * pulseSpeed;
                DrawAll(data);
            }

            // 이번 프레임에 안 쓴 선은 꺼 둔다. 파괴하지 않는다.
            for (int i = used; i < lines.Count; i++)
                lines[i].enabled = false;
        }

        private void DrawAll(SkillData data)
        {
            CollectVictims(data);
            if (victims.Count == 0) return;

            Vector3 casterGround = CasterGround();
            float alpha = pulseSpeed > 0f
                ? Mathf.Lerp(0.75f, 1f, (Mathf.Sin(pulse * Mathf.PI * 2f) + 1f) * 0.5f)
                : 1f;

            for (int i = 0; i < victims.Count && i < MaxTargets; i++)
            {
                Combat v = victims[i];
                if (v == null || v.Physics == null) continue;

                Vector3 castOrigin = CastOrigin(data, casterGround, v.Physics.GroundPosition);
                Vector3 facing = Facing(castOrigin, v.Physics.GroundPosition);

                if (!KnockbackPreview.TryPredict(data, castOrigin, facing, v.Physics, out var r)) continue;

                Draw(in r, alpha);
            }
        }

        /// <summary>
        /// 누가 맞는가. 반경 스킬은 찍은 원 안 전부, 근접은 커서에서 가장 가까운 하나다 —
        /// 근접은 시전자 히트박스로 때리므로 radius가 진실이 아니다(<see cref="SkillData.UsesRadius"/>).
        /// </summary>
        private void CollectVictims(SkillData data)
        {
            victims.Clear();

            if (data.UsesRadius)
            {
                if (predictor != null)
                    predictor.EnemiesInRadius(selector.CursorPoint, data.radius, victims);
                return;
            }

            Entity near = BattleRegistry.NearestEnemy(selector.CursorPoint);
            if (near != null && near.Combat != null && !near.Combat.IsDead)
                victims.Add(near.Combat);
        }

        private Vector3 CasterGround()
        {
            Transform t = selector != null ? selector.CursorOrigin : null;
            if (t == null) return selector != null ? selector.CursorPoint : Vector3.zero;

            var phys = t.GetComponentInParent<Physics>();
            return phys != null ? phys.GroundPosition : t.position;
        }

        /// <summary>
        /// 넉백 방향의 기준점. 근접은 <b>대상 옆으로 붙은 뒤</b> 때리므로 시전자 제자리가 아니다 —
        /// 실전(<see cref="SkillState"/>)과 같은 <see cref="KnockbackPreview.ApproachSpot"/>을 쓴다.
        /// </summary>
        private Vector3 CastOrigin(SkillData data, Vector3 casterGround, Vector3 victimGround)
        {
            if (data.UsesRadius) return selector.CursorPoint;
            return KnockbackPreview.ApproachSpot(casterGround, victimGround, data.ApproachDistance);
        }

        private static Vector3 Facing(Vector3 castOrigin, Vector3 victimGround)
        {
            Vector3 d = victimGround - castOrigin;
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.right;
        }

        private void Draw(in KnockbackPreview.Result r, float alpha)
        {
            Vector3 flat = r.to - r.from;
            flat.y = 0f;

            if (flat.magnitude >= MinTravel)
            {
                DrawGroundArrow(r.from, r.to, Tint(pushColor, alpha));
                DrawLandingRing(r.to, Tint(pushColor, alpha));
            }

            if (r.apexHeight > 0.05f)
                DrawLaunchArrow(r.to, r.apexHeight, Tint(launchColor, alpha));
        }

        private static Color Tint(Color c, float alpha)
        {
            c.a *= alpha;
            return c;
        }

        /// <summary>바닥 화살표. 좌표는 전부 <see cref="BeltScroll.ToView"/>를 거친다.</summary>
        private void DrawGroundArrow(Vector3 from, Vector3 to, Color c)
        {
            LineRenderer lr = Acquire(ArrowPoints, loop: false);
            if (lr == null) return;

            Vector3 a = BeltScroll.ToView(Flat(from));
            Vector3 b = BeltScroll.ToView(Flat(to));

            WriteArrow(lr, a, b, c);
            lr.sortingOrder = Depth(to) - 20;
        }

        /// <summary>
        /// 세로 화살표 — 얼마나 뜨는지. 착지점 위로 <paramref name="apexHeight"/>만큼 세운다.
        /// 화면 위쪽이 곧 높이이므로 <see cref="BeltScroll.ToView"/>의 height 인자를 쓴다.
        /// </summary>
        private void DrawLaunchArrow(Vector3 ground, float apexHeight, Color c)
        {
            LineRenderer lr = Acquire(ArrowPoints, loop: false);
            if (lr == null) return;

            Vector3 a = BeltScroll.ToView(Flat(ground));
            Vector3 b = BeltScroll.ToView(Flat(ground), apexHeight);

            WriteArrow(lr, a, b, c);
            // 세로 화살표는 캐릭터에 가리면 의미가 없다. 바닥 표시보다 앞으로 뺀다.
            lr.sortingOrder = Depth(ground) + 30;
        }

        /// <summary>몸통 2점 + 촉 4점. 촉은 몸통 끝에서 되꺾어 그린다.</summary>
        private void WriteArrow(LineRenderer lr, Vector3 a, Vector3 b, Color c)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.0001f) dir = Vector3.right;
            else dir /= len;

            float head = Mathf.Max(MinHeadLength, len * HeadRatio);
            Vector3 side = new Vector3(-dir.y, dir.x, 0f);   // 화면 평면 기준 수직

            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.SetPosition(2, b - dir * head + side * head * 0.5f);
            lr.SetPosition(3, b);
            lr.SetPosition(4, b - dir * head - side * head * 0.5f);
            lr.SetPosition(5, b);

            Paint(lr, c);
        }

        private void DrawLandingRing(Vector3 center, Color c)
        {
            LineRenderer lr = Acquire(RingSegments, loop: true);
            if (lr == null) return;

            Vector3 flat = Flat(center);

            for (int i = 0; i < RingSegments; i++)
            {
                float rad = i * (360f / RingSegments) * Mathf.Deg2Rad;
                Vector3 p = flat + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * KnockbackPreview.LandingRingRadius;
                lr.SetPosition(i, BeltScroll.ToView(p));
            }

            Paint(lr, c);
            lr.sortingOrder = Depth(center) - 20;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>RangeIndicator.DrawRing과 같은 규약 — 깊은 곳이 뒤로 간다.</summary>
        private static int Depth(Vector3 ground) => Mathf.RoundToInt(-ground.z * 100f);

        private void Paint(LineRenderer lr, Color c)
        {
            lr.startColor = c;
            lr.endColor = c;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
        }

        /// <summary>풀에서 하나 꺼낸다. 상한을 넘으면 null — 그 프레임은 덜 그린다.</summary>
        private LineRenderer Acquire(int points, bool loop)
        {
            const int MaxLines = MaxTargets * 3;   // 대상마다 바닥 화살표 · 착지 링 · 세로 화살표
            if (used >= MaxLines) return null;

            while (lines.Count <= used)
                lines.Add(CreateLine($"KnockbackLine{lines.Count}"));

            LineRenderer lr = lines[used++];
            lr.loop = loop;
            lr.positionCount = points;
            lr.enabled = true;
            return lr;
        }

        private LineRenderer CreateLine(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = shared;
            lr.enabled = false;

            return lr;
        }

        private void OnDestroy()
        {
            if (shared != null) Destroy(shared);
        }
    }
}
