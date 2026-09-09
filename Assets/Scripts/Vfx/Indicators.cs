// 바닥 표시 넷 — 넉백 방향 · 범위 원 · 평타 범위 · 스킬 범위.
// 전부 라인 렌더러로 바닥(XZ 평면)에 그리고 같은 깊이 정렬 규약을 쓴다:
//   sortingOrder = round(-z * 100) + 오프셋
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    // ══ KnockbackIndicator ═══════════════════════════════════════════

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
        [SerializeField] private EnemyRadiusProbe radiusProbe;

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
            if (radiusProbe == null) radiusProbe = FindAnyObjectByType<EnemyRadiusProbe>();

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
                if (radiusProbe != null)
                    radiusProbe.EnemiesInRadius(selector.CursorPoint, data.radius, victims);
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

    // ══ RangeIndicator ═══════════════════════════════════════════

    /// <summary>
    /// 불릿타임 조준 중 스킬 시전 범위를 바닥에 그린다.
    ///
    /// <see cref="TargetSelector"/>가 이미 논리 좌표 · 반경 · 헛침 여부를 다 들고 있다.
    /// 지금까지는 <c>OnDrawGizmosSelected</c>뿐이라 에디터에서 그 오브젝트를 고른 동안만 보였다.
    /// 여기서는 실제 게임 화면에 그린다.
    /// </summary>
    public class RangeIndicator : MonoBehaviour
    {
        private const int Segments = 40;

        [SerializeField] private TargetSelector selector;

        [Tooltip("반경 안에 적이 있을 때.")]
        [SerializeField] private Color okColor = new Color(0.35f, 0.9f, 1f, 0.85f);
        [Tooltip("반경 안에 적이 하나도 없을 때 — 헛침 경고.")]
        [SerializeField] private Color whiffColor = new Color(1f, 0.35f, 0.35f, 0.85f);
        // 공격 VFX가 0.13~0.16으로 그린다. 0.06은 그 옆에서 안 읽혔다.
        [SerializeField] private float lineWidth = 0.12f;
        [Tooltip("초당 깜빡임 횟수. 0이면 고정.")]
        [SerializeField] private float pulseSpeed = 2.2f;

        private LineRenderer ring;
        private LineRenderer cursor;
        private Material shared;
        private float pulse;

        private void Awake()
        {
            shared = BattleVfx.CreateMaterial();

            ring = CreateLine("RangeRing", Segments);
            cursor = CreateLine("RangeCursor", 4);
        }

        private LineRenderer CreateLine(string name, int points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.loop = true;
            lr.positionCount = points;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = shared;
            lr.enabled = false;

            return lr;
        }

        // 조준은 시간이 멈춘 동안 돌아간다. TargetSelector가 커서를 옮긴 뒤에 그려야
        // 한 프레임 밀리지 않으므로 LateUpdate에서 처리한다.
        private void LateUpdate()
        {
            // 씬이 바뀌면 참조가 끊긴다. 이 오브젝트는 DontDestroyOnLoad라 매번 확인한다.
            if (selector == null) selector = FindAnyObjectByType<TargetSelector>();

            bool show = selector != null
                        && selector.IsSelecting
                        && selector.Current != null
                        && selector.Current.targeting == TargetingType.GroundPoint;

            ring.enabled = show;
            cursor.enabled = show;
            if (!show) return;

            // 시간이 멈춰 있으므로 unscaled로 돈다. TargetSelector와 같은 시계.
            pulse += TimeControl.UnscaledDeltaTime * pulseSpeed;

            Color c = selector.WillWhiff ? whiffColor : okColor;
            // 하한 0.55는 실효 알파가 0.47까지 떨어져 배경에 묻혔다. 깜빡임은 남기고 바닥만 올린다.
            c.a *= pulseSpeed > 0f ? Mathf.Lerp(0.75f, 1f, (Mathf.Sin(pulse * Mathf.PI * 2f) + 1f) * 0.5f) : 1f;

            DrawRing(selector.CursorPoint, selector.Current.radius, c);
            DrawCursor(selector.CursorViewPoint, c);
        }

        /// <summary>
        /// 논리 XZ 원을 화면 좌표로 접는다. 접히고 나면 바닥에 누운 타원으로 보인다 —
        /// 캐릭터가 서 있는 평면과 같은 공간이라야 "저기가 사거리 안"이 읽힌다.
        /// </summary>
        private void DrawRing(Vector3 center, float radius, Color c)
        {
            center.y = 0f;

            for (int i = 0; i < Segments; i++)
            {
                float a = i * (360f / Segments) * Mathf.Deg2Rad;
                Vector3 ground = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                ring.SetPosition(i, BeltScroll.ToView(ground));
            }

            ring.startColor = c;
            ring.endColor = c;
            ring.startWidth = lineWidth;
            ring.endWidth = lineWidth;

            // 바닥에 깔린 표시다. 같은 깊이의 캐릭터보다 뒤에 그려야 발을 가리지 않는다.
            ring.sortingOrder = Mathf.RoundToInt(-center.z * 100f) - 20;
        }

        /// <summary>커서 자리의 작은 마름모. 원이 커지면 중심을 놓치기 쉽다.</summary>
        private void DrawCursor(Vector3 viewPoint, Color c)
        {
            const float s = 0.18f;

            cursor.SetPosition(0, viewPoint + new Vector3(0f, s, 0f));
            cursor.SetPosition(1, viewPoint + new Vector3(s, 0f, 0f));
            cursor.SetPosition(2, viewPoint + new Vector3(0f, -s, 0f));
            cursor.SetPosition(3, viewPoint + new Vector3(-s, 0f, 0f));

            cursor.startColor = c;
            cursor.endColor = c;
            cursor.startWidth = lineWidth;
            cursor.endWidth = lineWidth;

            // 커서만은 항상 보여야 한다. 캐릭터 위로 올린다.
            cursor.sortingOrder = Mathf.RoundToInt(-viewPoint.z * 100f) + 200;
        }

        private void OnDestroy()
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
