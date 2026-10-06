using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 마력화살 VFX.
/// 시전자 몸에서 마력탄이 나와 주위로 소용돌이치며 퍼졌다가, 대상의 히트박스 위 탄착점으로 하나씩 날아가 터집니다.
/// 보여주기만 하는 투사체라서 충돌 판정은 하지 않습니다. 피해는 게임 쪽에서 히트스캔으로 처리하세요.
///
///   missile.Fire(casterTransform, enemyCollider2D);                          // 시전자와 대상을 따라다님
///   missile.Fire(casterTransform, enemyTransform, hitboxSize, centerOffset);  // 콜라이더 없이 크기를 직접 지정
///   missile.Fire(casterPosition, hitboxBounds);                               // 고정된 위치
///
/// 탄착점은 히트박스 기준 좌표(-1..1)로 지정합니다. 기본값은 좌상단 / 우측 중앙 / 중앙 하단의 3발입니다.
/// 탄이 닿는 순간마다 onImpact 가 호출되고, 닿기까지 걸리는 시간은 GetImpactDelay(i) 로 미리 알 수 있습니다.
///
/// 이 오브젝트의 로컬 XY 평면에 그려집니다. 넘겨받은 월드 위치(시전자, 히트박스 중심)는 그 평면으로 (로컬 Z 를 따라) 눌러서 씁니다 —
/// 오브젝트를 카메라 쪽으로 돌려 두면 3D 공간의 점이 화면에 보이는 자리 그대로 놓입니다. 히트박스 크기는 평면(화면) 기준입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MagicMissileVFX : MonoBehaviour
{
    [Header("Shader (컴포넌트를 붙이면 자동으로 채워짐)")]
    public Shader missileShader;

    [Header("탄착점 (히트박스 기준: x -1 = 왼쪽 끝, +1 = 오른쪽 끝 / y -1 = 아래 끝, +1 = 위 끝)")]
    [Tooltip("원소 하나가 마력탄 한 발. 기본: 좌상단, 우측 중앙, 중앙 하단")]
    public Vector2[] impactAnchors =
    {
        new Vector2(-0.7f, 0.7f),
        new Vector2(0.8f, 0f),
        new Vector2(0f, -0.8f)
    };

    [Header("움직임")]
    [Tooltip("시전자 위치에서 마력탄이 나오는 지점까지의 오프셋 (보통 가슴 높이)")]
    public Vector2 casterOffset = new Vector2(0f, 1.0f);
    [Tooltip("몸에서 나와 주위로 퍼지는 시간")]
    [Min(0.05f)] public float emergeTime = 0.30f;
    [Tooltip("퍼진 자리에서 잠깐 머무는 시간")]
    [Min(0f)] public float hoverTime = 0.10f;
    [Tooltip("한 발씩 차례로 출발하는 간격")]
    [Min(0f)] public float stagger = 0.07f;
    [Tooltip("머문 자리에서 탄착점까지 날아가는 시간")]
    [Min(0.05f)] public float flightTime = 0.24f;
    [Tooltip("시전자 주위로 퍼지는 거리 배율")]
    [Min(0.1f)] public float spreadScale = 1f;

    [Header("모양")]
    [Tooltip("마력탄 크기 (빛무리 포함, 월드 유닛)")]
    [Min(0.1f)] public float orbSize = 1.25f;
    [Tooltip("뒤따르는 잔상의 수")]
    [Range(0, 30)] public int ghostCount = 14;
    [Tooltip("잔상 사이의 시간 간격 (초). 크면 잔상이 띄엄띄엄 놓입니다")]
    [Min(0.005f)] public float ghostInterval = 0.022f;
    [Tooltip("잔상을 잇는 부드러운 꼬리의 폭 (0 = 꼬리 없음)")]
    [Min(0f)] public float ribbonWidth = 0.44f;

    [Header("탄착 효과")]
    [Range(0, 20)] public int impactSparks = 7;
    [Min(0f)] public float impactFlashSize = 2.0f;
    [Min(0f)] public float impactRingSize = 1.5f;

    [Header("Color")]
    [ColorUsage(false, true)] public Color coreColor = Color.white;
    [ColorUsage(false, true)] public Color midColor = new Color(0.72f, 0.45f, 1f);
    [ColorUsage(false, true)] public Color edgeColor = new Color(0.50f, 0f, 1f);          // #8000FF
    [Range(0f, 2f)] public float glow = 0.9f;
    [Range(0f, 1f)] public float alpha = 1f;

    [Header("Playback")]
    public bool useUnscaledTime = false;
    [Tooltip("0 이면 쏠 때마다 결이 달라지고, 다른 값이면 항상 같은 모양")]
    public int seed = 0;

    [Header("Sorting")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 100;

    [Header("Preview")]
    [Tooltip("켜두면 가상의 대상에게 계속 반복 발사 (룩 조정용)")]
    public bool previewLoop = false;
    [Tooltip("이 오브젝트 위치 기준, 가상 대상의 발밑 위치")]
    public Vector2 previewTargetOffset = new Vector2(6f, 0f);
    public Vector2 previewHitboxSize = new Vector2(1.5f, 2.4f);
    public float previewPause = 0.6f;

    [Header("Events")]
    [Tooltip("마력탄 한 발이 탄착점에 닿는 순간. LastImpactIndex / LastImpactPoint 로 어느 탄인지 알 수 있습니다")]
    public UnityEvent onImpact;
    [Tooltip("한 번 쏜 마력탄이 모두 닿은 순간")]
    public UnityEvent onAllImpacted;

    public bool IsPlaying { get { return volleys.Count > 0 || sparkles.Count > 0 || needles.Count > 0 || rings.Count > 0; } }

    /// <summary>가장 최근에 닿은 마력탄의 번호 (0부터)</summary>
    public int LastImpactIndex { get; private set; }

    /// <summary>가장 최근 탄착 위치 (월드)</summary>
    public Vector3 LastImpactPoint { get; private set; }

    /// <summary>발사 후 index 번째 마력탄이 닿기까지 걸리는 시간 (초). 히트스캔 피해 타이밍을 맞출 때 사용.</summary>
    public float GetImpactDelay(int index)
    {
        return emergeTime + hoverTime + index * stagger + flightTime;
    }

    // ------------------------------------------------------------------ internals

    static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    // 3발 기준의 움직임. 탄이 더 많으면 이 값을 돌려 씀
    static readonly float[] FinalAngle = { 115f, 165f, 215f };   // 퍼진 뒤의 위치 (대상 방향을 0도로, 위쪽이 +). 대상의 반대편에 모임
    static readonly float[] SpreadRadius = { 1.9f, 2.3f, 1.7f };
    static readonly float[] SpinDir = { 1f, -1f, 1f };            // 소용돌이 방향
    static readonly float[] SweepAngle = { 250f, 200f, 250f };    // 소용돌이치며 도는 각도
    static readonly float[] FlightCurve = { 1.1f, 0.5f, -1.0f };  // 날아갈 때 휘는 정도 (+ 위로, - 아래로)

    struct Sample
    {
        public float time;
        public Vector3 pos;
    }

    class Orb
    {
        public float seed;
        public bool hit;
        public readonly List<Sample> history = new List<Sample>();   // 잔상과 꼬리를 그리기 위한 지나온 자리
    }

    class Volley   // 한 번의 발사
    {
        public float start;
        public Transform casterT, targetT;
        public Collider2D targetCollider;
        public Vector3 casterPos;        // casterT 가 없거나 사라졌을 때 쓰는 마지막 위치 (로컬 평면)
        public Bounds hitbox;            // 대상이 사라졌을 때 쓰는 마지막 히트박스 (로컬 평면)
        public Vector2 hitboxSize, hitboxOffset;
        public Orb[] orbs;
        public int impacts;
    }

    struct Sparkle
    {
        public Vector3 pos;
        public float size, start, life, rot;
    }

    struct Needle
    {
        public Vector3 pos, vel;
        public float size, start, life;
    }

    struct Ring
    {
        public Vector3 pos;
        public float size, start, life;
    }

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;
    Material mat;
    bool initialized;
    System.Random rng;

    readonly List<Volley> volleys = new List<Volley>();
    readonly List<Sparkle> sparkles = new List<Sparkle>();
    readonly List<Needle> needles = new List<Needle>();
    readonly List<Ring> rings = new List<Ring>();

    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector2> uv0 = new List<Vector2>();
    readonly List<Vector4> uv1 = new List<Vector4>();
    readonly List<int> tris = new List<int>();
    readonly List<Vector3> trailPoints = new List<Vector3>();

    float clock;
    float previewTimer;

    // ------------------------------------------------------------------ public API

    /// <summary>시전자와 대상을 따라다니며 발사합니다. 탄착점은 대상 콜라이더의 bounds 기준.</summary>
    public void Fire(Transform caster, Collider2D target)
    {
        if (caster == null || target == null) return;
        Volley v = NewVolley();
        if (v == null) return;
        v.casterT = caster;
        v.casterPos = ToPlane(caster.position);
        v.targetCollider = target;
        v.hitbox = ToPlane(target.bounds);
    }

    /// <summary>시전자와 대상을 따라다니며 발사합니다. 히트박스는 대상 위치 + centerOffset 을 중심으로 한 hitboxSize 크기의 사각형.</summary>
    public void Fire(Transform caster, Transform target, Vector2 hitboxSize, Vector2 centerOffset)
    {
        if (caster == null || target == null) return;
        Volley v = NewVolley();
        if (v == null) return;
        v.casterT = caster;
        v.casterPos = ToPlane(caster.position);
        v.targetT = target;
        v.hitboxSize = hitboxSize;
        v.hitboxOffset = centerOffset;
        v.hitbox = new Bounds(ToPlane(target.position) + (Vector3)centerOffset, hitboxSize);
    }

    /// <summary>고정된 위치에서 고정된 히트박스로 발사합니다.</summary>
    public void Fire(Vector3 casterPosition, Bounds targetHitbox)
    {
        Volley v = NewVolley();
        if (v == null) return;
        v.casterPos = ToPlane(casterPosition);
        v.hitbox = ToPlane(targetHitbox);
    }

    /// <summary>index 번째 탄착점의 현재 월드 위치 (가장 최근에 쏜 것 기준).</summary>
    public Vector3 GetImpactPoint(int index)
    {
        if (volleys.Count == 0) return LastImpactPoint;
        return transform.TransformPoint(ImpactPoint(volleys[volleys.Count - 1], index));
    }

    /// <summary>즉시 숨기고 멈춥니다.</summary>
    public void Stop()
    {
        volleys.Clear(); sparkles.Clear(); needles.Clear(); rings.Clear();
        if (meshRenderer != null) meshRenderer.enabled = false;
    }

    // ------------------------------------------------------------------ lifecycle

    void Reset()
    {
        // 에디터에서 컴포넌트를 붙일 때 호출됨. 여기서 참조를 걸어두면 빌드에도 셰이더가 포함됩니다.
        missileShader = Shader.Find("VFX/MagicMissile");
    }

    void Awake()
    {
        Init();
    }

    void OnDisable()
    {
        Stop();
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (mat != null) Destroy(mat);
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Prototype.TimeControl.DeltaTime;
        clock += dt;

        if (previewLoop && mat != null && !IsPlaying)
        {
            previewTimer -= dt;
            if (previewTimer <= 0f)
            {
                Vector3 feet = transform.position + (Vector3)previewTargetOffset;
                Fire(transform.position, new Bounds(feet + new Vector3(0f, previewHitboxSize.y * 0.5f, 0f), previewHitboxSize));
                previewTimer = previewPause;
            }
        }

        if (!IsPlaying) return;

        // ---- 마력탄 이동, 탄착 처리
        for (int vi = 0; vi < volleys.Count; vi++)
        {
            Volley v = volleys[vi];
            RefreshTargets(v);
            float t = clock - v.start;

            for (int i = 0; i < v.orbs.Length; i++)
            {
                Orb o = v.orbs[i];
                if (o.hit) continue;

                Vector3 pos;
                if (OrbPosition(v, i, t, out pos))
                {
                    Sample s = new Sample();
                    s.time = clock; s.pos = pos;
                    o.history.Add(s);
                }
                else
                {
                    o.hit = true;
                    v.impacts++;
                    Impact(v, o, i);
                    if (!isActiveAndEnabled || volleys.Count == 0) return;      // 이벤트 안에서 Stop() 등을 부른 경우
                }
            }
        }

        // ---- 수명이 끝난 요소 정리
        float trailTime = (ghostCount + 1) * ghostInterval;
        for (int vi = volleys.Count - 1; vi >= 0; vi--)
        {
            Volley v = volleys[vi];
            bool done = true;
            for (int i = 0; i < v.orbs.Length; i++)
            {
                Orb o = v.orbs[i];
                // 오래된 기록은 버림
                int drop = 0;
                while (drop < o.history.Count - 1 && o.history[drop + 1].time < clock - trailTime) drop++;
                if (drop > 0) o.history.RemoveRange(0, drop);

                if (!o.hit) done = false;
                else if (o.history.Count > 0 && clock - o.history[o.history.Count - 1].time < trailTime) done = false;   // 잔상이 아직 남음
            }
            if (done) volleys.RemoveAt(vi);
        }
        for (int i = sparkles.Count - 1; i >= 0; i--)
            if (clock >= sparkles[i].start + sparkles[i].life) sparkles.RemoveAt(i);
        for (int i = needles.Count - 1; i >= 0; i--)
            if (clock >= needles[i].start + needles[i].life) needles.RemoveAt(i);
        for (int i = rings.Count - 1; i >= 0; i--)
            if (clock >= rings[i].start + rings[i].life) rings.RemoveAt(i);

        if (!IsPlaying)
        {
            meshRenderer.enabled = false;
            return;
        }

        BuildMesh();
        ApplyMaterial();
    }

    // ------------------------------------------------------------------ setup

    void Init()
    {
        if (initialized) return;
        initialized = true;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (missileShader == null) missileShader = Shader.Find("VFX/MagicMissile");
        if (missileShader == null)
        {
            Debug.LogError("[MagicMissileVFX] 'VFX/MagicMissile' 셰이더를 찾을 수 없습니다. MagicMissile.shader 를 프로젝트에 넣고 Missile Shader 칸에 지정하세요.", this);
            enabled = false;
            return;
        }

        mat = new Material(missileShader);
        mat.name = "MagicMissile (Instance)";
        mat.hideFlags = HideFlags.DontSave;

        mesh = new Mesh();
        mesh.name = "MagicMissileMesh";
        mesh.hideFlags = HideFlags.DontSave;
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;

        meshRenderer.sharedMaterial = mat;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder;
        meshRenderer.enabled = false;
    }

    Volley NewVolley()
    {
        Init();
        if (mat == null) return null;
        if (impactAnchors == null || impactAnchors.Length == 0)
        {
            Debug.LogWarning("[MagicMissileVFX] Impact Anchors 가 비어 있어 쏠 마력탄이 없습니다.", this);
            return null;
        }
        if (rng == null || !IsPlaying) rng = seed != 0 ? new System.Random(seed) : new System.Random();

        Volley v = new Volley();
        v.start = clock;
        v.orbs = new Orb[impactAnchors.Length];
        for (int i = 0; i < v.orbs.Length; i++)
        {
            v.orbs[i] = new Orb();
            v.orbs[i].seed = Rand(0f, 10f);
        }
        volleys.Add(v);

        if (!meshRenderer.enabled)
        {
            mesh.Clear();
            meshRenderer.enabled = true;
        }
        ApplyMaterial();
        return v;
    }

    float Rand(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    static float SStep(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    void ApplyMaterial()
    {
        mat.SetColor(CoreColorId, coreColor);
        mat.SetColor(MidColorId, midColor);
        mat.SetColor(EdgeColorId, edgeColor);
        mat.SetFloat(GlowId, glow);
        mat.SetFloat(AlphaId, alpha);
    }

    // ------------------------------------------------------------------ motion

    /// <summary>따라다니는 대상의 현재 위치를 읽어 둡니다. 대상이 사라졌으면 마지막 값을 그대로 씁니다.</summary>
    void RefreshTargets(Volley v)
    {
        if (v.casterT != null) v.casterPos = ToPlane(v.casterT.position);
        if (v.targetCollider != null) v.hitbox = ToPlane(v.targetCollider.bounds);
        else if (v.targetT != null) v.hitbox = new Bounds(ToPlane(v.targetT.position) + (Vector3)v.hitboxOffset, v.hitboxSize);
    }

    Vector3 ImpactPoint(Volley v, int index)
    {
        Vector2 anchor = impactAnchors[Mathf.Clamp(index, 0, impactAnchors.Length - 1)];
        Vector3 c = v.hitbox.center;
        Vector3 e = v.hitbox.extents;
        return new Vector3(c.x + anchor.x * e.x, c.y + anchor.y * e.y, c.z);
    }

    /// <summary>발사 후 t 초가 지난 시점의 index 번째 마력탄 위치. 이미 닿았으면 false.</summary>
    bool OrbPosition(Volley v, int index, float t, out Vector3 pos)
    {
        Vector3 center = v.casterPos + (Vector3)casterOffset;
        float side = v.hitbox.center.x >= center.x ? 1f : -1f;       // 대상이 왼쪽이면 좌우를 뒤집음
        int m = index % 3;
        int lap = index / 3;                                          // 4발째부터는 조금씩 어긋난 자리로
        float finalAng = (FinalAngle[m] + lap * 17f) * Mathf.Deg2Rad;
        float radius = (SpreadRadius[m] + lap * 0.35f) * spreadScale;

        float hoverEnd = emergeTime + hoverTime + index * stagger;
        float impact = hoverEnd + flightTime;

        // 1) 몸에서 나와 소용돌이치며 퍼짐
        if (t < emergeTime)
        {
            float x = Mathf.Clamp01(t / emergeTime);
            float inv = 1f - x;
            float ease = 1f - inv * inv * inv;
            float ang = finalAng - SpinDir[m] * SweepAngle[m] * Mathf.Deg2Rad * (1f - ease);
            pos = Around(center, side, ang, radius * (1f - inv * inv));
            return true;
        }

        // 2) 퍼진 자리에서 살짝 흔들리며 대기
        Vector3 hover = Around(center, side, finalAng, radius) + new Vector3(0f, 0.05f * Mathf.Sin(t * 14f + index * 2.1f), 0f);
        if (t < hoverEnd)
        {
            pos = hover;
            return true;
        }
        if (t >= impact)
        {
            pos = hover;
            return false;
        }

        // 3) 휘어지는 궤적으로 가속하며 탄착점으로
        float u = (t - hoverEnd) / flightTime;
        float k = Mathf.Pow(u, 2.2f);
        Vector3 p2 = ImpactPoint(v, index);
        p2.z = hover.z;
        Vector3 d = p2 - hover;
        float len = d.magnitude;
        Vector3 perp = len > 1e-4f ? new Vector3(-d.y, d.x, 0f) / len : Vector3.up;
        Vector3 p1 = hover + d * 0.35f + perp * (FlightCurve[m] * side);
        float ik = 1f - k;
        pos = ik * ik * hover + 2f * ik * k * p1 + k * k * p2;
        return true;
    }

    static Vector3 Around(Vector3 center, float side, float angle, float radius)
    {
        return center + new Vector3(Mathf.Cos(angle) * side, Mathf.Sin(angle) * 0.9f, 0f) * radius;
    }

    /// <summary>탄착: 섬광 + 퍼지는 고리 + 날아온 방향으로 튀는 불티</summary>
    void Impact(Volley v, Orb o, int index)
    {
        Vector3 hit = ImpactPoint(v, index);
        Vector3 from = o.history.Count > 0 ? o.history[o.history.Count - 1].pos : hit - Vector3.right;
        hit.z = from.z;
        Vector3 d = hit - from;
        float baseAng = d.sqrMagnitude > 1e-8f ? Mathf.Atan2(d.y, d.x) : 0f;

        // 잔상이 탄착점까지 이어지도록 마지막 위치를 하나 더 기록
        Sample last = new Sample();
        last.time = clock; last.pos = hit;
        o.history.Add(last);

        if (impactFlashSize > 0f)
        {
            AddSparkle(hit, impactFlashSize * Rand(0.85f, 1.15f), clock, 0.18f, Rand(0f, 1.57f));
            AddSparkle(hit + new Vector3(Rand(-0.3f, 0.3f), Rand(-0.3f, 0.3f), 0f), impactFlashSize * Rand(0.3f, 0.45f), clock + 0.05f, 0.16f, 0f);
        }
        if (impactRingSize > 0f)
        {
            Ring r = new Ring();
            r.pos = hit; r.size = impactRingSize; r.start = clock; r.life = 0.22f;
            rings.Add(r);
        }
        for (int i = 0; i < impactSparks; i++)
        {
            float ang = baseAng + Rand(-1.3f, 1.3f);
            Needle n = new Needle();
            n.pos = hit;
            n.vel = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * Rand(4f, 10f);
            n.size = Rand(0.35f, 0.7f);
            n.start = clock;
            n.life = Rand(0.18f, 0.34f);
            needles.Add(n);
        }

        LastImpactIndex = index;
        LastImpactPoint = transform.TransformPoint(hit);
        if (onImpact != null) onImpact.Invoke();
        if (v.impacts >= v.orbs.Length && onAllImpacted != null) onAllImpacted.Invoke();
    }

    void AddSparkle(Vector3 pos, float size, float start, float life, float rot)
    {
        Sparkle s = new Sparkle();
        s.pos = pos; s.size = size; s.start = start; s.life = life; s.rot = rot;
        sparkles.Add(s);
    }

    /// <summary>when 시각에 마력탄이 있던 자리 (기록 사이를 보간). 그때 아직 없었으면 false.</summary>
    static bool SampleHistory(List<Sample> history, float when, out Vector3 pos)
    {
        pos = Vector3.zero;
        if (history.Count == 0 || when < history[0].time) return false;
        for (int k = history.Count - 1; k > 0; k--)
        {
            if (history[k - 1].time <= when)
            {
                float span = Mathf.Max(history[k].time - history[k - 1].time, 1e-6f);
                pos = Vector3.Lerp(history[k - 1].pos, history[k].pos, (when - history[k - 1].time) / span);
                return true;
            }
        }
        pos = history[0].pos;
        return true;
    }

    // ------------------------------------------------------------------ mesh

    void BuildMesh()
    {
        verts.Clear(); uv0.Clear(); uv1.Clear(); tris.Clear();

        for (int vi = 0; vi < volleys.Count; vi++)
        {
            Volley v = volleys[vi];
            for (int i = 0; i < v.orbs.Length; i++)
            {
                Orb o = v.orbs[i];
                if (o.history.Count == 0) continue;
                float lastTime = o.history[o.history.Count - 1].time;
                int n = Mathf.Max(ghostCount, 0);

                // ---- 꼬리: 지나온 자리를 잇는 부드러운 띠
                if (ribbonWidth > 0f && n > 0) AddRibbon(o, lastTime, n);

                // ---- 잔상 (오래된 것부터) -> 마지막에 마력탄 본체
                for (int k = n; k >= 0; k--)
                {
                    float when = clock - k * ghostInterval;
                    if (when > lastTime) continue;                    // 이미 닿은 탄은 본체 없이 잔상만 남음
                    Vector3 pos;
                    if (!SampleHistory(o.history, when, out pos)) continue;

                    float f = n > 0 ? k / (float)n : 0f;
                    float grow = SStep(0f, 0.12f, when - v.start) * 0.6f + 0.4f;      // 몸에서 나올 때는 작게 시작
                    float half = orbSize * (1f - 0.65f * f) * grow * 0.5f;
                    float a = k == 0 ? 1f : 0.9f * Mathf.Pow(1f - f, 0.9f);
                    AddQuad(pos, new Vector3(half, 0f, 0f), new Vector3(0f, half, 0f),
                            new Vector4(0f, when - v.start, o.seed + k * 0.37f, a));
                }
            }
        }

        // ---- 탄착 고리
        for (int i = 0; i < rings.Count; i++)
        {
            float t = (clock - rings[i].start) / rings[i].life;
            if (t <= 0f || t >= 1f) continue;
            float half = rings[i].size * 0.5f;
            AddQuad(rings[i].pos, new Vector3(half, 0f, 0f), new Vector3(0f, half, 0f), new Vector4(4f, t, 0f, 0f));
        }

        // ---- 불티
        for (int i = 0; i < needles.Count; i++)
        {
            Needle nd = needles[i];
            float age = clock - nd.start;
            if (age <= 0f || age >= nd.life) continue;

            float k = age / nd.life;
            Vector3 pos = nd.pos + nd.vel * ((1f - Mathf.Exp(-4f * age)) / 4f);
            Vector3 dir = nd.vel.normalized;
            float halfLen = nd.size * (1f - 0.5f * k) * 0.5f;
            AddQuad(pos, dir * halfLen, new Vector3(-dir.y, dir.x, 0f) * (halfLen * 0.16f), new Vector4(3f, 1f - k * k, 0f, 0f));
        }

        // ---- 섬광
        for (int i = 0; i < sparkles.Count; i++)
        {
            Sparkle sp = sparkles[i];
            float t = (clock - sp.start) / sp.life;
            if (t <= 0f || t >= 1f) continue;

            Vector3 ax = new Vector3(Mathf.Cos(sp.rot), Mathf.Sin(sp.rot), 0f) * (sp.size * 0.5f);
            AddQuad(sp.pos, ax, new Vector3(-ax.y, ax.x, 0f), new Vector4(2f, t, 0f, 0f));
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }

    void AddRibbon(Orb o, float lastTime, int n)
    {
        // 잔상과 같은 간격으로 지나온 자리를 뽑음 (uv.x: 0 = 마력탄 쪽, 1 = 꼬리 끝)
        trailPoints.Clear();
        int firstK = -1;
        for (int k = 0; k <= n; k++)
        {
            float when = clock - k * ghostInterval;
            if (when > lastTime) continue;
            Vector3 pos;
            if (!SampleHistory(o.history, when, out pos)) break;
            if (firstK < 0) firstK = k;
            trailPoints.Add(pos);
        }
        if (trailPoints.Count < 2) return;

        float halfW = ribbonWidth * 0.5f;
        Vector4 mode = new Vector4(1f, 1f, 0f, 0f);
        int start = verts.Count;
        for (int j = 0; j < trailPoints.Count; j++)
        {
            Vector3 prev = trailPoints[Mathf.Max(j - 1, 0)];
            Vector3 next = trailPoints[Mathf.Min(j + 1, trailPoints.Count - 1)];
            Vector3 d = next - prev;
            Vector3 perp = d.sqrMagnitude > 1e-10f ? new Vector3(-d.y, d.x, 0f).normalized : Vector3.up;
            float u = (firstK + j) / (float)n;
            AddVertex(trailPoints[j] - perp * halfW, new Vector2(u, 0f), mode);
            AddVertex(trailPoints[j] + perp * halfW, new Vector2(u, 1f), mode);
        }
        for (int j = 0; j < trailPoints.Count - 1; j++)
        {
            int q = start + j * 2;
            tris.Add(q); tris.Add(q + 1); tris.Add(q + 2);
            tris.Add(q + 2); tris.Add(q + 1); tris.Add(q + 3);
        }
    }

    // 중심과 두 반축으로 사각형 하나. uv.x 는 ax 방향, uv.y 는 ay 방향
    void AddQuad(Vector3 center, Vector3 ax, Vector3 ay, Vector4 mode)
    {
        int q = verts.Count;
        AddVertex(center - ax - ay, new Vector2(0f, 0f), mode);
        AddVertex(center - ax + ay, new Vector2(0f, 1f), mode);
        AddVertex(center + ax - ay, new Vector2(1f, 0f), mode);
        AddVertex(center + ax + ay, new Vector2(1f, 1f), mode);
        tris.Add(q); tris.Add(q + 1); tris.Add(q + 2);
        tris.Add(q + 2); tris.Add(q + 1); tris.Add(q + 3);
    }

    // 메시를 짜는 좌표가 이미 로컬 평면이므로 그대로 씀
    void AddVertex(Vector3 local, Vector2 uv, Vector4 mode)
    {
        local.z = 0f;
        verts.Add(local);
        uv0.Add(uv);
        uv1.Add(mode);
    }

    /// <summary>월드 위치 -> 이 오브젝트의 로컬 XY 평면 (로컬 Z 를 따라 눌러 붙임)</summary>
    Vector3 ToPlane(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        local.z = 0f;
        return local;
    }

    /// <summary>월드 히트박스 -> 로컬 평면 (중심만 옮기고 크기는 그대로)</summary>
    Bounds ToPlane(Bounds world)
    {
        return new Bounds(ToPlane(world.center), world.size);
    }

#if UNITY_EDITOR
    // 메뉴: GameObject > Effects > Magic Missile VFX (마력화살)
    [UnityEditor.MenuItem("GameObject/Effects/Magic Missile VFX", false, 10)]
    static void CreateFromMenu(UnityEditor.MenuCommand command)
    {
        GameObject go = new GameObject("MagicMissileVFX");
        MagicMissileVFX fx = go.AddComponent<MagicMissileVFX>();
        fx.previewLoop = true;               // Play 를 누르면 바로 반복 발사되도록. 실제 사용 시에는 끄세요
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Magic Missile VFX");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
