using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 마력균열 VFX (벨트스크롤용).
/// 지정한 위치에 공간의 균열을 열어 주변을 중앙으로 끌어당기는 스킬의 이펙트입니다.
///   1) 형성 (선딜)  : 한 점에서 금이 사방으로 뻗고, 어두운 핵이 부풀어 오르며 바닥에 소용돌이가 퍼집니다.
///   2) 공간 폭풍    : 바닥의 소용돌이가 돌고, 핵 주위로 금이 번쩍이며, 줄기와 파편이 핵으로 빨려 듭니다.
///                     지속시간 동안 핵은 위아래로 점점 눌려 납작해집니다.
///   3) 소멸 · 폭발  : 납작해진 핵이 하얗게 달아오르며 양옆에서 순식간에 접혀 사라지고, 그 자리에서 터집니다.
///
///   rift.Open(targetPosition, 2f);    // 그 위치(바닥)에 2초 동안 폭풍 유지 후 폭발
///   rift.Open(targetTransform, 2f);   // 대상을 따라다님
///   rift.Close();                     // 지속시간이 남았어도 지금 소멸시키기
///   rift.Center / rift.Contains(pos)  // 끌어당길 중심과, 그 위치가 폭풍 범위 안인지 (끌어당김은 게임 쪽에서 처리)
///
/// 지속시간에 0 이하를 넣으면 Close() 를 부를 때까지 유지합니다.
/// 바닥의 소용돌이는 캐릭터 발밑에 깔려야 하므로 별도의 정렬 순서(Floor Sorting Order)를 씁니다.
///
/// 이 오브젝트의 로컬 XY 평면에 그려집니다. 넘겨받은 월드 위치는 그 평면으로 (로컬 Z 를 따라) 눌러서 씁니다 —
/// 오브젝트를 카메라 쪽으로 돌려 두면 3D 공간의 점이 화면에 보이는 자리 그대로 놓입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ManaRiftVFX : MonoBehaviour
{
    public enum FinishAction
    {
        None,      // 그대로 대기 (다시 Open 해서 재사용)
        Disable,   // 비활성화 (오브젝트 풀링)
        Destroy    // 오브젝트 삭제 (Instantiate 해서 쓰는 경우)
    }

    [Header("Shader (컴포넌트를 붙이면 자동으로 채워짐)")]
    public Shader riftShader;

    [Header("범위")]
    [Tooltip("바닥 소용돌이의 반지름 (월드 유닛, 좌우 방향)")]
    [Min(0.5f)] public float radius = 3.2f;
    [Tooltip("바닥이 화면에서 세로로 눌려 보이는 비율. 카메라가 바닥을 비스듬히 내려다볼수록 작게")]
    [Range(0.1f, 1f)] public float groundTilt = 0.42f;
    [Tooltip("균열의 핵이 떠 있는 높이 (바닥에서부터)")]
    public float coreHeight = 1.0f;
    [Tooltip("균열 핵의 지름")]
    [Min(0.1f)] public float coreSize = 1.5f;

    [Header("타이밍")]
    [Tooltip("선딜: 균열이 형성되는 시간")]
    [Min(0.05f)] public float formTime = 0.45f;
    [Tooltip("소멸할 때 핵이 양옆에서 접혀 사라지는 시간 (이 직후 폭발)")]
    [Min(0.02f)] public float collapseTime = 0.14f;

    [Header("핵의 압축")]
    [Tooltip("지속시간이 끝날 때 핵의 세로 크기 비율. 작을수록 납작해짐 (1 이면 눌리지 않음)")]
    [Range(0.05f, 1f)] public float coreSquash = 0.22f;
    [Tooltip("눌리면서 가로로 살짝 퍼지는 정도 (0 이면 가로는 그대로)")]
    [Range(0f, 0.5f)] public float coreWiden = 0.10f;
    [Tooltip("지속시간 없이(0 이하) 열었을 때, 이 시간에 걸쳐 끝까지 눌림")]
    [Min(0.1f)] public float endlessSquashTime = 3f;

    [Header("공간 폭풍")]
    [Tooltip("핵 주위를 도는 공간 파편의 수")]
    [Range(0, 30)] public int orbitShards = 9;
    [Tooltip("핵으로 빨려 드는 줄기의 양 (초당)")]
    [Min(0f)] public float suctionRate = 28f;
    [Tooltip("핵 주위에서 번쩍이는 금의 빈도 (초당)")]
    [Min(0f)] public float crackRate = 7f;

    [Header("폭발")]
    [Tooltip("끄면 폭발 없이, 핵이 접힌 자리에 작은 섬광만 남기고 끝남 (onBurst 이벤트는 그대로 호출)")]
    public bool endBurst = true;
    [Range(0, 24)] public int burstCracks = 9;
    [Range(0, 60)] public int burstRays = 26;
    [Range(0, 40)] public int burstShards = 12;

    [Header("Color")]
    [ColorUsage(false, true)] public Color coreColor = Color.white;
    [ColorUsage(false, true)] public Color midColor = new Color(0.72f, 0.45f, 1f);
    [ColorUsage(false, true)] public Color edgeColor = new Color(0.50f, 0f, 1f);          // #8000FF
    [Tooltip("균열 속의 어두운 색")]
    public Color riftColor = new Color(0.04f, 0f, 0.10f);
    [Range(0f, 2f)] public float glow = 1f;
    [Range(0f, 1f)] public float alpha = 1f;

    [Header("Playback")]
    public bool useUnscaledTime = false;
    [Tooltip("0 이면 쓸 때마다 결이 달라지고, 다른 값이면 항상 같은 모양")]
    public int seed = 0;
    public FinishAction onFinishedAction = FinishAction.None;

    [Header("Sorting")]
    [Tooltip("핵, 금, 줄기, 파편, 섬광 (캐릭터보다 앞)")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 100;
    [Tooltip("바닥의 소용돌이와 충격파 (배경보다 앞, 캐릭터보다 뒤가 되도록 맞추세요)")]
    public string floorSortingLayerName = "Default";
    public int floorSortingOrder = -1;

    [Header("Preview")]
    [Tooltip("켜두면 이 오브젝트 위치에 계속 반복 재생 (룩 조정용)")]
    public bool previewLoop = false;
    public float previewDuration = 1.5f;
    public float previewPause = 0.6f;

    [Header("Events")]
    [Tooltip("균열이 다 형성되어 폭풍이 시작되는 순간 (끌어당김 시작)")]
    public UnityEvent onFormed;
    [Tooltip("소멸하며 폭발하는 순간 (마무리 데미지, 카메라 흔들림 등). 끌어당김은 여기서 끝내면 됩니다")]
    public UnityEvent onBurst;
    public UnityEvent onFinished;

    public bool IsPlaying { get { return active; } }

    /// <summary>폭풍이 몰아치는 동안 (형성이 끝난 뒤부터 폭발 전까지) true</summary>
    public bool IsOpen { get { return active && formedFired && !burstFired; } }

    /// <summary>끌어당길 중심: 균열 바로 아래의 바닥 위치 (월드)</summary>
    public Vector3 Center { get { return transform.TransformPoint(center); } }

    // ------------------------------------------------------------------ internals

    const float Tau = 6.2831853f;
    const float OrbEdge = 0.58f;         // 쿼드 반크기에 대한 핵의 반지름 비율 (셰이더의 수치와 같아야 함)

    static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    // 위치는 모두 중심(바닥) 또는 핵 기준의 오프셋으로 들고 있어서, 대상을 따라 움직여도 함께 따라감

    struct Crack     // 핵에서 뻗는 금
    {
        public float angle, length, width, start, life, grow, dark, seed;
    }

    struct Needle    // 줄기: 핵으로 빨려 드는 것(inward) 또는 폭발 때 튀어 나가는 것
    {
        public Vector3 offset, vel;
        public float size, start, life;
        public bool inward;
    }

    struct Orbit     // 핵 주위를 도는 파편
    {
        public float radius, height, angle0, speed, size, rot, rotSpeed;
    }

    struct Shard     // 폭발 때 튀어 나가는 파편
    {
        public Vector3 offset, vel;
        public float rot, rotSpeed, size, start, life;
    }

    struct Sparkle
    {
        public Vector3 offset;
        public float size, start, life, rot;
    }

    struct Shock     // 바닥 충격파
    {
        public float start, life, scale, seed;
    }

    struct Glow
    {
        public Vector3 offset;
        public float size, start, life;
    }

    // 메시 하나를 채우는 버퍼 묶음 (앞쪽용 / 바닥용)
    class Buffers
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector2> uv0 = new List<Vector2>();
        public readonly List<Vector4> uv1 = new List<Vector4>();
        public readonly List<Vector4> uv2 = new List<Vector4>();
        public readonly List<int> tris = new List<int>();

        public void Clear()
        {
            verts.Clear(); uv0.Clear(); uv1.Clear(); uv2.Clear(); tris.Clear();
        }

        public void Apply(Mesh m)
        {
            m.Clear();
            m.SetVertices(verts);
            m.SetUVs(0, uv0);
            m.SetUVs(1, uv1);
            m.SetUVs(2, uv2);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
        }
    }

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    MeshFilter floorFilter;
    MeshRenderer floorRenderer;
    Mesh mesh, floorMesh;
    Material mat;
    bool initialized;
    System.Random rng;

    readonly List<Crack> cracks = new List<Crack>();
    readonly List<Needle> needles = new List<Needle>();
    readonly List<Orbit> orbits = new List<Orbit>();
    readonly List<Shard> shards = new List<Shard>();
    readonly List<Sparkle> sparkles = new List<Sparkle>();
    readonly List<Shock> shocks = new List<Shock>();
    readonly List<Glow> glows = new List<Glow>();
    readonly Buffers front = new Buffers();
    readonly Buffers floor = new Buffers();

    float clock;
    bool active;
    Transform targetT;
    Vector3 center;                 // 로컬 평면
    float startTime;
    float closeAt = -1f;            // 시작 후 몇 초에 소멸을 시작하는지 (정해지지 않았으면 음수)
    float squashEnd = 1f;           // 시작 후 몇 초에 핵이 끝까지 눌리는지 (Close() 로 일찍 닫아도 바뀌지 않음)
    float riftSeed;
    float suctionAcc, crackAcc, sparkleAcc;
    bool formedFired, burstFired;
    float previewTimer;

    Vector3 CoreOffset { get { return new Vector3(0f, coreHeight, 0f); } }

    // ------------------------------------------------------------------ public API

    /// <summary>worldPosition(바닥)에 균열을 엽니다. duration 초 동안 폭풍이 몰아친 뒤 폭발합니다. (0 이하면 Close() 를 부를 때까지)</summary>
    public void Open(Vector3 worldPosition, float duration)
    {
        targetT = null;
        center = ToPlane(worldPosition);
        Begin(duration);
    }

    /// <summary>대상의 위치(발밑)에 균열을 열고 따라다닙니다.</summary>
    public void Open(Transform target, float duration)
    {
        if (target == null) return;
        targetT = target;
        center = ToPlane(target.position);
        Begin(duration);
    }

    /// <summary>지금 소멸시킵니다 (핵이 그때까지 눌린 모양에서 양옆으로 접힌 뒤 폭발).</summary>
    public void Close()
    {
        if (!active || burstFired) return;
        float t = clock - startTime;
        if (closeAt < 0f || closeAt > t) closeAt = Mathf.Max(t, formTime);
    }

    /// <summary>worldPosition(바닥 위의 점, 보통 적의 발밑)이 폭풍 범위 안에 있는지.</summary>
    public bool Contains(Vector3 worldPosition)
    {
        Vector3 p = ToPlane(worldPosition);
        float gx = (p.x - center.x) / radius;
        float gz = (p.y - center.y) / (radius * Mathf.Max(groundTilt, 0.01f));
        return gx * gx + gz * gz <= 1f;
    }

    /// <summary>즉시 숨기고 멈춥니다.</summary>
    public void Stop()
    {
        ClearAll();
        active = false;
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (floorRenderer != null) floorRenderer.enabled = false;
    }

    // ------------------------------------------------------------------ lifecycle

    void Reset()
    {
        // 에디터에서 컴포넌트를 붙일 때 호출됨. 여기서 참조를 걸어두면 빌드에도 셰이더가 포함됩니다.
        riftShader = Shader.Find("VFX/ManaRift");
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
        if (floorMesh != null) Destroy(floorMesh);
        if (mat != null) Destroy(mat);
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Prototype.TimeControl.DeltaTime;

        if (!active)
        {
            if (previewLoop && mat != null)
            {
                previewTimer -= dt;
                if (previewTimer <= 0f) Open(transform.position, previewDuration);
            }
            return;
        }

        clock += dt;
        if (targetT != null) center = ToPlane(targetT.position);          // 대상이 사라지면 마지막 위치에 남음

        float t = clock - startTime;
        float burstAt = closeAt >= 0f ? closeAt + collapseTime : -1f;
        bool closing = closeAt >= 0f && t >= closeAt;

        // ---- 형성 완료: 폭풍 시작
        if (!formedFired && t >= formTime)
        {
            formedFired = true;
            AddSparkle(CoreOffset, 4.0f, t, 0.20f, 0f);
            AddShock(t, 0.35f, 0.8f);
            if (onFormed != null) onFormed.Invoke();
            if (!active) return;                         // 이벤트 안에서 Stop() 을 부른 경우
        }

        // ---- 폭풍 (형성 중에도 줄기는 모여듦)
        if (!closing) SpawnStorm(t, dt);

        // ---- 폭발
        if (!burstFired && burstAt >= 0f && t >= burstAt)
        {
            burstFired = true;
            if (endBurst) SpawnBurst(t);
            else AddSparkle(CoreOffset, 2.4f, t, 0.16f, 0f);
            if (onBurst != null) onBurst.Invoke();
            if (!active) return;
        }

        // ---- 수명이 끝난 요소 정리
        for (int i = cracks.Count - 1; i >= 0; i--)
            if (t >= cracks[i].start + cracks[i].life) cracks.RemoveAt(i);
        for (int i = needles.Count - 1; i >= 0; i--)
            if (t >= needles[i].start + needles[i].life) needles.RemoveAt(i);
        for (int i = shards.Count - 1; i >= 0; i--)
            if (t >= shards[i].start + shards[i].life) shards.RemoveAt(i);
        for (int i = sparkles.Count - 1; i >= 0; i--)
            if (t >= sparkles[i].start + sparkles[i].life) sparkles.RemoveAt(i);
        for (int i = shocks.Count - 1; i >= 0; i--)
            if (t >= shocks[i].start + shocks[i].life) shocks.RemoveAt(i);
        for (int i = glows.Count - 1; i >= 0; i--)
            if (t >= glows[i].start + glows[i].life) glows.RemoveAt(i);

        if (burstFired && t >= burstAt + 0.25f && cracks.Count == 0 && needles.Count == 0 && shards.Count == 0
            && sparkles.Count == 0 && shocks.Count == 0 && glows.Count == 0)
        {
            Finish();
            return;
        }

        BuildMeshes(t, burstAt);
        ApplyMaterial();
    }

    void Finish()
    {
        ClearAll();
        active = false;
        meshRenderer.enabled = false;
        floorRenderer.enabled = false;
        previewTimer = previewPause;
        if (onFinished != null) onFinished.Invoke();

        switch (onFinishedAction)
        {
            case FinishAction.Disable: gameObject.SetActive(false); break;
            case FinishAction.Destroy: Destroy(gameObject); break;
        }
    }

    void ClearAll()
    {
        cracks.Clear(); needles.Clear(); orbits.Clear(); shards.Clear(); sparkles.Clear(); shocks.Clear(); glows.Clear();
        suctionAcc = crackAcc = sparkleAcc = 0f;
        formedFired = false;
        burstFired = false;
        closeAt = -1f;
    }

    // ------------------------------------------------------------------ setup

    void Init()
    {
        if (initialized) return;
        initialized = true;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (riftShader == null) riftShader = Shader.Find("VFX/ManaRift");
        if (riftShader == null)
        {
            Debug.LogError("[ManaRiftVFX] 'VFX/ManaRift' 셰이더를 찾을 수 없습니다. ManaRift.shader 를 프로젝트에 넣고 Rift Shader 칸에 지정하세요.", this);
            enabled = false;
            return;
        }

        mat = new Material(riftShader);
        mat.name = "ManaRift (Instance)";
        mat.hideFlags = HideFlags.DontSave;

        mesh = NewMesh("ManaRiftFront");
        meshFilter.sharedMesh = mesh;
        SetupRenderer(meshRenderer, sortingLayerName, sortingOrder);

        // 바닥의 소용돌이는 캐릭터 발밑에 깔려야 해서 정렬 순서가 다른 별도의 렌더러로 그림
        GameObject floorGo = new GameObject("ManaRiftFloor");
        floorGo.transform.SetParent(transform, false);
        floorFilter = floorGo.AddComponent<MeshFilter>();
        floorRenderer = floorGo.AddComponent<MeshRenderer>();
        floorMesh = NewMesh("ManaRiftFloor");
        floorFilter.sharedMesh = floorMesh;
        SetupRenderer(floorRenderer, floorSortingLayerName, floorSortingOrder);
    }

    static Mesh NewMesh(string meshName)
    {
        Mesh m = new Mesh();
        m.name = meshName;
        m.hideFlags = HideFlags.DontSave;
        m.MarkDynamic();
        return m;
    }

    void SetupRenderer(MeshRenderer r, string layerName, int order)
    {
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.sortingLayerName = layerName;
        r.sortingOrder = order;
        r.enabled = false;
    }

    void Begin(float duration)
    {
        Init();
        if (mat == null) return;

        ClearAll();
        rng = seed != 0 ? new System.Random(seed) : new System.Random();
        startTime = clock;
        closeAt = duration > 0f ? formTime + duration : -1f;
        squashEnd = formTime + (duration > 0f ? duration : endlessSquashTime);
        riftSeed = Rand(0f, 10f);
        active = true;

        // 핵 주위를 돌 파편들 (가까울수록 빠르게, 몇 개는 반대 방향으로)
        for (int i = 0; i < orbitShards; i++)
        {
            Orbit o = new Orbit();
            o.radius = Rand(0.9f, 2.4f);
            o.height = Rand(-0.6f, 0.8f);
            o.angle0 = Rand(0f, Tau);
            o.speed = Rand(3f, 6f) * (i % 4 == 0 ? -1f : 1f);
            o.size = Rand(0.18f, 0.40f);
            o.rot = Rand(0f, Tau);
            o.rotSpeed = Rand(-6f, 6f);
            orbits.Add(o);
        }

        // 형성: 한 점에서 금이 사방으로 뻗어 나감
        for (int i = 0; i < 6; i++)
        {
            float ang = (i + Rand(0.1f, 0.9f)) / 6f * Tau;
            AddCrack(ang, Rand(1.2f, 2.4f), Rand(0.25f, 0.4f), Rand(0f, 0.18f), formTime + 0.25f, 0.22f, 1f);
        }
        AddSparkle(CoreOffset, 1.6f, 0f, 0.20f, 0f);

        mesh.Clear();
        floorMesh.Clear();
        meshRenderer.enabled = true;
        floorRenderer.enabled = true;
        ApplyMaterial();
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

    static Vector3 Polar(float angle, float length)
    {
        return new Vector3(Mathf.Cos(angle) * length, Mathf.Sin(angle) * length, 0f);
    }

    void ApplyMaterial()
    {
        mat.SetColor(CoreColorId, coreColor);
        mat.SetColor(MidColorId, midColor);
        mat.SetColor(EdgeColorId, edgeColor);
        mat.SetColor(DarkColorId, riftColor);
        mat.SetFloat(GlowId, glow);
        mat.SetFloat(AlphaId, alpha);
    }

    // ------------------------------------------------------------------ spawning (시각은 모두 Open 이후의 초)

    void AddCrack(float angle, float length, float width, float start, float life, float grow, float dark)
    {
        Crack c = new Crack();
        c.angle = angle; c.length = length; c.width = width; c.start = start; c.life = life; c.grow = Mathf.Max(grow, 0.01f); c.dark = dark;
        c.seed = Rand(0f, 10f);
        cracks.Add(c);
    }

    void AddSparkle(Vector3 offset, float size, float start, float life, float rot)
    {
        Sparkle s = new Sparkle();
        s.offset = offset; s.size = size; s.start = start; s.life = life; s.rot = rot;
        sparkles.Add(s);
    }

    void AddShock(float start, float life, float scale)
    {
        Shock s = new Shock();
        s.start = start; s.life = life; s.scale = scale; s.seed = Rand(0f, 10f);
        shocks.Add(s);
    }

    /// <summary>폭풍이 몰아치는 동안 계속 생겨나는 것들: 빨려 드는 줄기, 번쩍이는 금, 반짝임</summary>
    void SpawnStorm(float t, float dt)
    {
        // 핵으로 빨려 드는 줄기 (형성 중에는 조금씩, 폭풍 때는 많이)
        suctionAcc += dt * suctionRate * (0.4f + 0.6f * SStep(0f, formTime, t));
        while (suctionAcc >= 1f)
        {
            suctionAcc -= 1f;
            float ang = Rand(0f, Tau);
            float dist = Rand(2f, 4f);
            Needle n = new Needle();
            n.offset = new Vector3(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist * 0.6f, 0f);    // 핵 기준
            n.inward = true;
            n.size = Rand(0.5f, 1.0f);
            n.start = t;
            n.life = Rand(0.28f, 0.45f);
            needles.Add(n);
        }

        if (!formedFired) return;

        // 핵 주위에서 번쩍이는 금: 속이 어두운 틈과 밝은 금이 섞여 나옴
        crackAcc += dt * crackRate;
        while (crackAcc >= 1f)
        {
            crackAcc -= 1f;
            AddCrack(Rand(0f, Tau), Rand(0.9f, 1.9f), Rand(0.22f, 0.36f), t, Rand(0.2f, 0.32f), 0.06f, Rand(0f, 1f) < 0.6f ? 1f : 0f);
        }

        sparkleAcc += dt * 3f;
        while (sparkleAcc >= 1f)
        {
            sparkleAcc -= 1f;
            float ang = Rand(0f, Tau);
            float dist = Rand(0.8f, 2.6f);
            AddSparkle(CoreOffset + new Vector3(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist * 0.6f, 0f),
                       Rand(0.3f, 0.6f), t, Rand(0.2f, 0.4f), 0f);
        }
    }

    /// <summary>소멸하며 터지는 순간: 섬광, 바닥 충격파, 사방으로 뻗는 금과 줄기, 튀어 나가는 파편</summary>
    void SpawnBurst(float t)
    {
        Vector3 core = CoreOffset;
        AddSparkle(core, 7.0f, t, 0.22f, 0f);
        AddSparkle(core, 4.5f, t + 0.03f, 0.20f, 0.785f);

        Glow g = new Glow();
        g.offset = core; g.size = 7f; g.start = t; g.life = 0.30f;
        glows.Add(g);

        AddShock(t, 0.42f, 1.25f);
        AddShock(t + 0.06f, 0.38f, 0.8f);

        for (int i = 0; i < burstCracks; i++)
        {
            float ang = (i + Rand(0.1f, 0.9f)) / burstCracks * Tau;
            AddCrack(ang, Rand(2.5f, 4.2f), Rand(0.3f, 0.5f), t, Rand(0.24f, 0.34f), 0.06f, 0f);
        }

        for (int i = 0; i < burstRays; i++)
        {
            float ang = Rand(0f, Tau);
            Vector3 d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.75f, 0f);
            Needle n = new Needle();
            n.offset = core + d * 0.4f;                  // 중심(바닥) 기준
            n.vel = d * Rand(8f, 18f);
            n.inward = false;
            n.size = Rand(0.5f, 1.2f);
            n.start = t + Rand(0f, 0.04f);
            n.life = Rand(0.2f, 0.4f);
            needles.Add(n);
        }

        for (int i = 0; i < burstShards; i++)
        {
            float ang = Rand(0f, Tau);
            Vector3 d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f + 0.3f, 0f);
            Shard s = new Shard();
            s.offset = core + d * 0.3f;
            s.vel = d * Rand(4f, 10f);
            s.rot = Rand(0f, Tau);
            s.rotSpeed = Rand(-12f, 12f);
            s.size = Rand(0.2f, 0.45f);
            s.start = t;
            s.life = Rand(0.5f, 0.9f);
            shards.Add(s);
        }
    }

    // ------------------------------------------------------------------ timeline

    /// <summary>핵의 가로/세로 크기 배율 (0 이면 보이지 않음)</summary>
    void CoreShape(float t, float burstAt, out float sx, out float sy)
    {
        if (t < formTime)
        {
            // 형성의 뒤쪽 65% 동안 튀어나오듯 부풀어 오름
            float x = Mathf.Clamp01((t - 0.35f * formTime) / (0.65f * formTime));
            float s = 0f;
            if (x > 0f)
            {
                float m = x - 1f;
                s = Mathf.Max(0f, 1f + 2.4f * m * m * m + 1.4f * m * m);
            }
            sx = s;
            sy = s;
            return;
        }
        if (burstAt >= 0f && t >= burstAt)
        {
            sx = 0f;
            sy = 0f;
            return;
        }

        // 폭풍: 지속시간에 걸쳐 위아래로 점점 눌림 (+ 숨쉬듯 맥동). 소멸이 시작되면 그 순간의 모양에서 멈춤
        bool closing = closeAt >= 0f && t > closeAt;
        float ts = closing ? closeAt : t;
        float k = Mathf.Clamp01((ts - formTime) / Mathf.Max(squashEnd - formTime, 0.001f));
        float pulse = 1f + 0.06f * Mathf.Sin(ts * 9f);
        sx = (1f + coreWiden * k) * pulse;
        sy = Mathf.Lerp(1f, coreSquash, k) * pulse;

        // 소멸: 양옆에서 순식간에 접힘 (처음이 가장 빠름)
        if (closing)
        {
            float x = Mathf.Clamp01((t - closeAt) / collapseTime);
            sx *= (1f - x) * (1f - x);
        }
    }

    /// <summary>바닥 소용돌이의 크기 배율과 세기</summary>
    void FloorState(float t, float burstAt, out float scale, out float intensity)
    {
        if (t < formTime)
        {
            float x = t / formTime;
            float inv = 1f - x;
            scale = Mathf.Lerp(0.3f, 1f, 1f - inv * inv * inv);
            intensity = SStep(0.1f, 1f, x);
        }
        else if (closeAt < 0f || t < closeAt)
        {
            scale = 1f;
            intensity = 1f;
        }
        else if (t < burstAt)
        {
            float x = (t - closeAt) / collapseTime;
            scale = Mathf.Lerp(1f, 0.6f, x * x);
            intensity = 1f;
        }
        else
        {
            float k = SStep(0f, 0.25f, t - burstAt);
            scale = Mathf.Lerp(0.6f, 1.1f, k);
            intensity = 1f - k;
        }
    }

    // ------------------------------------------------------------------ mesh

    void BuildMeshes(float t, float burstAt)
    {
        front.Clear();
        floor.Clear();
        Vector4 none = Vector4.zero;
        Vector3 corePos = center + CoreOffset;
        bool closing = closeAt >= 0f && t >= closeAt;

        // ================= 바닥 (캐릭터 발밑) =================
        float floorScale, floorIntensity;
        FloorState(t, burstAt, out floorScale, out floorIntensity);
        if (floorIntensity > 0.003f)
            AddGroundQuad(floor, radius * floorScale, 1.02f, new Vector4(0f, t, floorIntensity, riftSeed));

        for (int i = 0; i < shocks.Count; i++)
        {
            float st = (t - shocks[i].start) / shocks[i].life;
            if (st <= 0f || st >= 1f) continue;
            AddGroundQuad(floor, radius * shocks[i].scale, 1.05f, new Vector4(6f, st, shocks[i].seed, 0f));
        }

        // ================= 앞쪽 =================
        for (int i = 0; i < glows.Count; i++)
        {
            float gt = (t - glows[i].start) / glows[i].life;
            if (gt < 0f || gt >= 1f) continue;
            float h = glows[i].size * 0.5f;
            AddQuad(front, center + glows[i].offset, new Vector3(h, 0f, 0f), new Vector3(0f, h, 0f), new Vector4(7f, (1f - gt) * (1f - gt), 0f, 0f), none);
        }

        // 핵 주위를 도는 파편: 먼 쪽 절반은 핵보다 먼저(뒤에), 가까운 쪽 절반은 핵 다음에(앞에) 그림
        float orbitK = 0f;
        if (!burstFired)
        {
            orbitK = SStep(0.3f * formTime, formTime, t);
            if (closing) orbitK *= 1f - SStep(closeAt, burstAt, t);      // 소멸할 때 핵으로 빨려 들어감
        }
        AddOrbitShards(corePos, t, orbitK, false);

        // 금
        for (int i = 0; i < cracks.Count; i++)
        {
            Crack c = cracks[i];
            float age = t - c.start;
            if (age <= 0f || age >= c.life) continue;

            float k = age / c.life;
            Vector3 d = Polar(c.angle, 1f);
            float halfLen = c.length * 0.5f;
            AddQuad(front, corePos + d * halfLen, d * halfLen, new Vector3(-d.y, d.x, 0f) * c.width,
                    new Vector4(2f, Mathf.Clamp01(age / c.grow), 1f - SStep(0.55f, 1f, k), c.dark),
                    new Vector4(c.seed, 0f, 0f, 0f));
        }

        // 핵
        float coreSx, coreSy;
        CoreShape(t, burstAt, out coreSx, out coreSy);
        if (coreSx > 0.02f && coreSy > 0.02f)
        {
            // 쿼드 = 타원 + 빛무리 여백. 타원 자체는 셰이더가 (가로 배율, 세로 배율)로 그림
            float hsRef = coreSize * 0.5f / OrbEdge;                 // 눌리지 않았을 때의 쿼드 반크기
            float big = Mathf.Max(coreSx, coreSy);
            float hx = OrbEdge * coreSx + (1f - OrbEdge) * big;
            float hy = OrbEdge * coreSy + (1f - OrbEdge) * big;

            float flash = 0f;
            if (t >= formTime) flash = 0.85f * (1f - SStep(0f, 0.15f, t - formTime));     // 형성되는 순간 번쩍
            if (closing) flash = SStep(0f, 0.5f, (t - closeAt) / collapseTime);            // 소멸: 접히면서 하얗게 달아오름
            AddQuad(front, corePos, new Vector3(hsRef * hx, 0f, 0f), new Vector3(0f, hsRef * hy, 0f),
                    new Vector4(1f, t, flash, riftSeed), new Vector4(coreSx, coreSy, hx, hy));
        }

        AddOrbitShards(corePos, t, orbitK, true);

        // 줄기
        for (int i = 0; i < needles.Count; i++)
        {
            Needle nd = needles[i];
            float age = t - nd.start;
            if (age <= 0f || age >= nd.life) continue;

            float k = age / nd.life;
            Vector3 pos, dir;
            float halfLen, fade;
            if (nd.inward)
            {
                // 점점 빨라지며 핵으로 빨려 듦
                pos = corePos + nd.offset * (1f - k * k);
                dir = -nd.offset.normalized;
                halfLen = nd.size * (0.4f + 0.6f * k) * 0.5f;
                fade = SStep(0f, 0.25f, k) * (1f - k * k * k * k);
            }
            else
            {
                pos = center + nd.offset + nd.vel * ((1f - Mathf.Exp(-4f * age)) / 4f);
                dir = nd.vel.normalized;
                halfLen = nd.size * (1f - 0.5f * k) * 0.5f;
                fade = 1f - k * k;
            }
            AddQuad(front, pos, dir * halfLen, new Vector3(-dir.y, dir.x, 0f) * (halfLen * 0.14f), new Vector4(4f, fade, 0f, 0f), none);
        }

        // 폭발 때 튀어 나가는 파편
        for (int i = 0; i < shards.Count; i++)
        {
            Shard s = shards[i];
            float age = t - s.start;
            if (age <= 0f || age >= s.life) continue;

            float k = age / s.life;
            Vector3 pos = center + s.offset + s.vel * ((1f - Mathf.Exp(-2.5f * age)) / 2.5f) + new Vector3(0f, -4f * age * age, 0f);
            AddShard(front, pos, s.rot + s.rotSpeed * age, s.size * (1f - 0.4f * k), 1f - k * k);
        }

        // 섬광 / 반짝임
        for (int i = 0; i < sparkles.Count; i++)
        {
            Sparkle sp = sparkles[i];
            float st = (t - sp.start) / sp.life;
            if (st <= 0f || st >= 1f) continue;

            Vector3 ax = Polar(sp.rot, sp.size * 0.5f);
            AddQuad(front, center + sp.offset, ax, new Vector3(-ax.y, ax.x, 0f), new Vector4(3f, st, 0f, 0f), none);
        }

        front.Apply(mesh);
        floor.Apply(floorMesh);
    }

    void AddOrbitShards(Vector3 corePos, float t, float orbitK, bool nearSide)
    {
        if (orbitK <= 0.01f) return;
        for (int i = 0; i < orbits.Count; i++)
        {
            Orbit o = orbits[i];
            float a = o.angle0 + o.speed * t;
            float sn = Mathf.Sin(a);
            if ((sn < 0f) != nearSide) continue;                 // 화면 아래쪽(sin < 0)이 카메라에 가까운 쪽

            Vector3 pos = corePos + new Vector3(Mathf.Cos(a) * o.radius * orbitK,
                                                sn * o.radius * orbitK * groundTilt + o.height * orbitK, 0f);
            AddShard(front, pos, o.rot + o.rotSpeed * t, o.size * (0.5f + 0.5f * orbitK), 1f);
        }
    }

    // 삼각형 파편 하나. uv = 무게중심 좌표
    void AddShard(Buffers b, Vector3 pos, float rot, float size, float fade)
    {
        Vector4 mode = new Vector4(5f, fade, 0f, 0f);
        Vector4 none = Vector4.zero;
        int q = b.verts.Count;
        AddVertex(b, pos + Polar(rot, size), new Vector2(1f, 0f), mode, none);
        AddVertex(b, pos + Polar(rot + 2.3f, size * 0.55f), new Vector2(0f, 1f), mode, none);
        AddVertex(b, pos + Polar(rot + 4.1f, size * 0.75f), new Vector2(0f, 0f), mode, none);
        b.tris.Add(q); b.tris.Add(q + 1); b.tris.Add(q + 2);
    }

    // 바닥에 누운 사각형 (중심 기준, 좌우 반지름 r). uv 에 바닥 좌표(-extent..extent)를 그대로 실음
    void AddGroundQuad(Buffers b, float r, float extent, Vector4 mode)
    {
        Vector4 none = Vector4.zero;
        float hx = r * extent;
        float hy = r * extent * groundTilt;
        int q = b.verts.Count;
        AddVertex(b, center + new Vector3(-hx, -hy, 0f), new Vector2(-extent, -extent), mode, none);
        AddVertex(b, center + new Vector3(-hx, hy, 0f), new Vector2(-extent, extent), mode, none);
        AddVertex(b, center + new Vector3(hx, -hy, 0f), new Vector2(extent, -extent), mode, none);
        AddVertex(b, center + new Vector3(hx, hy, 0f), new Vector2(extent, extent), mode, none);
        b.tris.Add(q); b.tris.Add(q + 1); b.tris.Add(q + 2);
        b.tris.Add(q + 2); b.tris.Add(q + 1); b.tris.Add(q + 3);
    }

    // 중심과 두 반축으로 사각형 하나. uv.x 는 ax 방향, uv.y 는 ay 방향
    void AddQuad(Buffers b, Vector3 pos, Vector3 ax, Vector3 ay, Vector4 mode, Vector4 prm)
    {
        int q = b.verts.Count;
        AddVertex(b, pos - ax - ay, new Vector2(0f, 0f), mode, prm);
        AddVertex(b, pos - ax + ay, new Vector2(0f, 1f), mode, prm);
        AddVertex(b, pos + ax - ay, new Vector2(1f, 0f), mode, prm);
        AddVertex(b, pos + ax + ay, new Vector2(1f, 1f), mode, prm);
        b.tris.Add(q); b.tris.Add(q + 1); b.tris.Add(q + 2);
        b.tris.Add(q + 2); b.tris.Add(q + 1); b.tris.Add(q + 3);
    }

    // 메시를 짜는 좌표가 이미 로컬 평면이므로 그대로 씀
    void AddVertex(Buffers b, Vector3 local, Vector2 uv, Vector4 mode, Vector4 prm)
    {
        local.z = 0f;
        b.verts.Add(local);
        b.uv0.Add(uv);
        b.uv1.Add(mode);
        b.uv2.Add(prm);
    }

    /// <summary>월드 위치 -> 이 오브젝트의 로컬 XY 평면 (로컬 Z 를 따라 눌러 붙임)</summary>
    Vector3 ToPlane(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        local.z = 0f;
        return local;
    }

#if UNITY_EDITOR
    // 메뉴: GameObject > Effects > Mana Rift VFX (마력균열)
    [UnityEditor.MenuItem("GameObject/Effects/Mana Rift VFX", false, 10)]
    static void CreateFromMenu(UnityEditor.MenuCommand command)
    {
        GameObject go = new GameObject("ManaRiftVFX");
        ManaRiftVFX fx = go.AddComponent<ManaRiftVFX>();
        fx.previewLoop = true;               // Play 를 누르면 바로 반복 재생되도록. 실제 사용 시에는 끄세요
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Mana Rift VFX");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
