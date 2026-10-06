using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 회전베기 VFX (3연타).
///   1타, 2타 : 캐릭터 주위를 한 바퀴 도는 참격. 둘은 같은 범위, 같은 모양입니다.
///   3타      : 같은 범위를 더 두껍고 오래 빛나는 참격으로 베고, 바닥 충격파와 불티가 터지는 마무리 일격.
///
/// 캐릭터는 제자리에 있고, 베는 범위는 캐릭터 앞쪽으로 치우친 타원입니다 (기본 전방 5.4 : 후방 1.8 = 75 : 25).
/// 이 오브젝트를 캐릭터의 자식으로 두고 위치를 캐릭터 발밑에 맞추세요. 이펙트는 이 오브젝트를 따라다닙니다.
///
///   spin.PlayNext();        // 호출할 때마다 1 → 2 → 3타 (콤보가 끊기면 다시 1타)
///   spin.PlayHit(3);        // 특정 타를 직접 재생 (애니메이션 이벤트에서도 호출 가능)
///   spin.Contains(pos);     // 그 위치(적의 발밑)가 베는 범위 안인지 (데미지 판정용)
///
/// 타마다 참격은 끊김 없는 한 줄기입니다 (칼날의 꼬리가 가는 선 하나로 이어지다 사라짐).
/// 원의 먼 쪽 절반은 캐릭터 뒤에, 가까운 쪽 절반은 캐릭터 앞에 그려지도록 정렬 순서를 둘로 나눠 쓰고,
/// 두 절반이 만나는 자리는 겹쳐서 서서히 이어지게 해 참격이 반듯한 선으로 잘려 보이지 않습니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SpinSlashVFX : MonoBehaviour
{
    [Header("Shader (컴포넌트를 붙이면 자동으로 채워짐)")]
    public Shader spinShader;

    [Header("베는 범위 (캐릭터 발밑 기준, 월드 유닛)")]
    [Tooltip("캐릭터 앞쪽으로 닿는 거리")]
    [Min(0.1f)] public float frontReach = 5.4f;
    [Tooltip("캐릭터 뒤쪽으로 닿는 거리")]
    [Min(0.1f)] public float backReach = 1.8f;
    [Tooltip("화면 안쪽/바깥쪽(깊이 방향)으로 닿는 거리")]
    [Min(0.1f)] public float depthReach = 2.6f;
    [Tooltip("바닥이 화면에서 세로로 눌려 보이는 비율. 카메라가 바닥을 비스듬히 내려다볼수록 작게")]
    [Range(0.1f, 1f)] public float groundTilt = 0.42f;
    [Tooltip("칼이 지나가는 높이 (발밑에서부터)")]
    public float bladeHeight = 0.9f;

    [Header("1타 · 2타")]
    [Tooltip("한 바퀴 도는 데 걸리는 시간")]
    [Min(0.05f)] public float spinTime = 0.26f;
    [Tooltip("다 돈 뒤 궤적이 사라지는 시간")]
    [Min(0.02f)] public float linger = 0.22f;
    [Tooltip("참격의 두께 (범위 대비)")]
    [Range(0.05f, 0.9f)] public float thickness = 0.36f;
    [Tooltip("칼끝 뒤로 남는 궤적의 길이 (도)")]
    [Range(30f, 330f)] public float trailAngle = 206f;

    [Header("3타 (마무리 일격)")]
    [Min(0.05f)] public float finisherSpinTime = 0.30f;
    [Min(0.02f)] public float finisherLinger = 0.50f;
    [Range(0.05f, 0.9f)] public float finisherThickness = 0.56f;
    [Range(30f, 330f)] public float finisherTrailAngle = 264f;
    [Tooltip("바닥으로 퍼지는 충격파 고리")]
    public bool finisherShockRing = true;
    [Tooltip("범위 가장자리에서 바깥으로 튀는 불티의 수")]
    [Range(0, 60)] public int finisherSparks = 28;

    [Header("바람 먼지")]
    [Tooltip("칼이 지나간 자리에서 일어나는 바람 먼지의 양 (0 = 없음)")]
    [Range(0f, 2f)] public float windAmount = 1f;

    [Header("Color")]
    [ColorUsage(false, true)] public Color coreColor = Color.white;
    [ColorUsage(false, true)] public Color midColor = new Color(0.66f, 0.93f, 1f);
    [ColorUsage(false, true)] public Color edgeColor = new Color(0.13f, 0.50f, 1f);
    [Range(0f, 2f)] public float glow = 0.6f;
    [Range(0f, 1f)] public float streak = 0.75f;
    [Range(0f, 1f)] public float alpha = 1f;

    [Header("Combo")]
    [Tooltip("마지막 타격 후 이 시간이 지나면 PlayNext() 가 다시 1타부터 시작")]
    public float comboResetTime = 0.9f;
    public bool useUnscaledTime = false;
    [Tooltip("0 이면 쓸 때마다 결이 달라지고, 다른 값이면 항상 같은 모양")]
    public int seed = 0;

    [Header("Sorting")]
    [Tooltip("원의 가까운 쪽 절반, 먼지, 불티 (캐릭터보다 앞)")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 100;
    [Tooltip("원의 먼 쪽 절반, 바닥 충격파 (배경보다 앞, 캐릭터보다 뒤가 되도록 맞추세요)")]
    public string backSortingLayerName = "Default";
    public int backSortingOrder = -1;

    [Header("Preview")]
    [Tooltip("켜두면 1 → 2 → 3타를 계속 반복 재생 (룩 조정용)")]
    public bool previewLoop = false;
    public float previewInterval = 0.45f;
    public float previewPause = 0.9f;

    [Header("Events")]
    [Tooltip("각 타의 칼날이 전방을 지나는 순간 (데미지, 사운드 등). CurrentHit 으로 몇 타인지 알 수 있습니다")]
    public UnityEvent onHit;
    [Tooltip("3타의 충격파가 터지는 순간 (카메라 흔들림, 히트스톱 등)")]
    public UnityEvent onFinisherImpact;
    public UnityEvent onFinished;

    /// <summary>마지막으로 재생한 타격 번호 (1~3, 아직 없으면 0)</summary>
    public int CurrentHit { get; private set; }

    public bool IsPlaying { get { return active; } }

    // ------------------------------------------------------------------ internals

    const float Tau = 6.2831853f;

    static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int StreakId = Shader.PropertyToID("_Streak");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    struct Layer     // 회전 참격 한 겹
    {
        public float start, spin, linger;        // 시작 시각, 도는 시간, 사라지는 시간
        public float radius, thickness, trail, sweep, startAngle, lines, seed;
        public bool strong;
    }

    struct Puff
    {
        public Vector3 pos, vel;
        public float size, start, life, seed;
    }

    struct Sparkle
    {
        public Vector3 pos;
        public float size, start, life, rot;
    }

    struct Needle    // 불티
    {
        public Vector3 pos, vel;
        public float size, start, life;
    }

    struct Shock     // 바닥 충격파
    {
        public float start, life, seed;
    }

    struct Cue       // 이벤트를 부를 시각
    {
        public float time;
        public bool finisher;
    }

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    MeshFilter backFilter;
    MeshRenderer backRenderer;
    Mesh mesh, backMesh;
    Material mat;
    bool initialized;
    System.Random rng;

    readonly List<Layer> layers = new List<Layer>();
    readonly List<Puff> puffs = new List<Puff>();
    readonly List<Sparkle> sparkles = new List<Sparkle>();
    readonly List<Needle> needles = new List<Needle>();
    readonly List<Shock> shocks = new List<Shock>();
    readonly List<Cue> cues = new List<Cue>();

    // 메시 하나를 채우는 버퍼 묶음 (앞쪽용 / 뒤쪽용)
    class Buffers
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector2> uv0 = new List<Vector2>();
        public readonly List<Vector4> uv1 = new List<Vector4>();
        public readonly List<Vector4> uv2 = new List<Vector4>();
        public readonly List<Vector4> uv3 = new List<Vector4>();
        public readonly List<int> tris = new List<int>();

        public void Clear()
        {
            verts.Clear(); uv0.Clear(); uv1.Clear(); uv2.Clear(); uv3.Clear(); tris.Clear();
        }

        public void Apply(Mesh m)
        {
            m.Clear();
            m.SetVertices(verts);
            m.SetUVs(0, uv0);
            m.SetUVs(1, uv1);
            m.SetUVs(2, uv2);
            m.SetUVs(3, uv3);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
        }
    }

    readonly Buffers front = new Buffers();
    readonly Buffers back = new Buffers();

    float clock;
    bool active;
    int nextIndex;
    float lastHitTime = -999f;
    float previewTimer;

    // ------------------------------------------------------------------ public API

    /// <summary>다음 타격을 재생하고 그 번호(1~3)를 돌려줍니다. 콤보가 끊겼으면 1타부터.</summary>
    public int PlayNext()
    {
        if (clock - lastHitTime > comboResetTime) nextIndex = 0;
        int hitNumber = nextIndex + 1;
        PlayHit(hitNumber);
        return hitNumber;
    }

    /// <summary>지정한 타격(1~3)을 재생합니다. 애니메이션 이벤트에서 바로 호출할 수 있습니다.</summary>
    public void PlayHit(int hitNumber)
    {
        Init();
        if (mat == null) return;
        if (rng == null || !active) rng = seed != 0 ? new System.Random(seed) : new System.Random();

        hitNumber = Mathf.Clamp(hitNumber, 1, 3);
        float now = clock;

        if (hitNumber < 3)
        {
            // 뒤쪽(180도)에서 시작해 화면 앞쪽 -> 전방 -> 화면 안쪽 순으로 한 바퀴
            AddLayer(now, spinTime, linger, 0.90f, thickness, trailAngle * Mathf.Deg2Rad, Tau + 0.6f, Mathf.PI, 1f, false);
            cues.Add(new Cue { time = now + spinTime * 0.3f, finisher = false });
        }
        else
        {
            float T = finisherSpinTime;
            AddLayer(now, T, finisherLinger, 0.92f, finisherThickness, finisherTrailAngle * Mathf.Deg2Rad, Tau + 0.9f, Mathf.PI, 1f, true);
            cues.Add(new Cue { time = now + T * 0.3f, finisher = false });

            float hit = now + T * 0.8f;
            cues.Add(new Cue { time = hit, finisher = true });
            AddFinisherBurst(hit);
        }

        CurrentHit = hitNumber;
        nextIndex = hitNumber % 3;
        lastHitTime = now;

        if (!active)
        {
            active = true;
            meshRenderer.enabled = true;
            backRenderer.enabled = true;
        }
        BuildMeshes();
        ApplyMaterial();
    }

    /// <summary>다음 PlayNext() 가 1타부터 시작하도록 합니다.</summary>
    public void ResetCombo()
    {
        nextIndex = 0;
        CurrentHit = 0;
    }

    /// <summary>캐릭터가 보는 방향에 맞춰 좌우 반전합니다. (캐릭터를 scale.x 로 뒤집는다면 필요 없음)</summary>
    public void SetFacing(bool facingRight)
    {
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (facingRight ? 1f : -1f);
        transform.localScale = s;
    }

    /// <summary>worldPosition(바닥 위의 점, 보통 적의 발밑)이 베는 범위 안에 있는지.</summary>
    public bool Contains(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        float rx = (frontReach + backReach) * 0.5f;
        float cx = (frontReach - backReach) * 0.5f;
        float gu = (local.x - cx) / rx;
        float gv = local.y / (depthReach * Mathf.Max(groundTilt, 0.01f));
        return gu * gu + gv * gv <= 1f;
    }

    /// <summary>즉시 숨기고 멈춥니다.</summary>
    public void Stop()
    {
        layers.Clear(); puffs.Clear(); sparkles.Clear(); needles.Clear(); shocks.Clear(); cues.Clear();
        active = false;
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (backRenderer != null) backRenderer.enabled = false;
    }

    // ------------------------------------------------------------------ lifecycle

    void Reset()
    {
        // 에디터에서 컴포넌트를 붙일 때 호출됨. 여기서 참조를 걸어두면 빌드에도 셰이더가 포함됩니다.
        spinShader = Shader.Find("VFX/SpinSlash");
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
        if (backMesh != null) Destroy(backMesh);
        if (mat != null) Destroy(mat);
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Prototype.TimeControl.DeltaTime;
        clock += dt;

        if (previewLoop && mat != null)
        {
            previewTimer -= dt;
            if (previewTimer <= 0f)
            {
                int played = nextIndex + 1;
                PlayHit(played);
                previewTimer = played >= 3 ? previewInterval + previewPause : previewInterval;
            }
        }

        if (!active) return;

        // ---- 이벤트
        for (int i = cues.Count - 1; i >= 0; i--)
        {
            if (clock < cues[i].time) continue;
            bool finisher = cues[i].finisher;
            cues.RemoveAt(i);
            if (finisher) { if (onFinisherImpact != null) onFinisherImpact.Invoke(); }
            else { if (onHit != null) onHit.Invoke(); }
            if (!active) return;                         // 이벤트 안에서 Stop() 을 부른 경우
            i = Mathf.Min(i, cues.Count);
        }

        // ---- 수명이 끝난 요소 정리
        for (int i = layers.Count - 1; i >= 0; i--)
            if (clock >= layers[i].start + layers[i].spin + layers[i].linger) layers.RemoveAt(i);
        for (int i = puffs.Count - 1; i >= 0; i--)
            if (clock >= puffs[i].start + puffs[i].life) puffs.RemoveAt(i);
        for (int i = sparkles.Count - 1; i >= 0; i--)
            if (clock >= sparkles[i].start + sparkles[i].life) sparkles.RemoveAt(i);
        for (int i = needles.Count - 1; i >= 0; i--)
            if (clock >= needles[i].start + needles[i].life) needles.RemoveAt(i);
        for (int i = shocks.Count - 1; i >= 0; i--)
            if (clock >= shocks[i].start + shocks[i].life) shocks.RemoveAt(i);

        if (layers.Count == 0 && puffs.Count == 0 && sparkles.Count == 0 && needles.Count == 0
            && shocks.Count == 0 && cues.Count == 0)
        {
            active = false;
            meshRenderer.enabled = false;
            backRenderer.enabled = false;
            if (onFinished != null) onFinished.Invoke();
            return;
        }

        BuildMeshes();
        ApplyMaterial();
    }

    // ------------------------------------------------------------------ setup

    void Init()
    {
        if (initialized) return;
        initialized = true;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (spinShader == null) spinShader = Shader.Find("VFX/SpinSlash");
        if (spinShader == null)
        {
            Debug.LogError("[SpinSlashVFX] 'VFX/SpinSlash' 셰이더를 찾을 수 없습니다. SpinSlash.shader 를 프로젝트에 넣고 Spin Shader 칸에 지정하세요.", this);
            enabled = false;
            return;
        }

        mat = new Material(spinShader);
        mat.name = "SpinSlash (Instance)";
        mat.hideFlags = HideFlags.DontSave;

        mesh = NewMesh("SpinSlashFront");
        meshFilter.sharedMesh = mesh;
        SetupRenderer(meshRenderer, sortingLayerName, sortingOrder);

        // 원의 먼 쪽 절반은 캐릭터 뒤에 그려져야 해서 정렬 순서가 다른 별도의 렌더러로 그림
        GameObject backGo = new GameObject("SpinSlashBack");
        backGo.transform.SetParent(transform, false);
        backFilter = backGo.AddComponent<MeshFilter>();
        backRenderer = backGo.AddComponent<MeshRenderer>();
        backMesh = NewMesh("SpinSlashBack");
        backFilter.sharedMesh = backMesh;
        SetupRenderer(backRenderer, backSortingLayerName, backSortingOrder);
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

    float Rand(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    static float SStep(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// 범위 좌표(단위원 = 베는 범위의 가장자리, gu 는 앞, gv 는 화면 안쪽) -> 이 오브젝트 기준 위치.
    /// 범위의 중심은 캐릭터보다 앞쪽에 있어서, 캐릭터 기준으로 앞은 길고 뒤는 짧음.
    /// </summary>
    Vector3 Ground(float gu, float gv, float height)
    {
        float rx = (frontReach + backReach) * 0.5f;
        float cx = (frontReach - backReach) * 0.5f;
        return new Vector3(cx + gu * rx, gv * depthReach * groundTilt + height, 0f);
    }

    void ApplyMaterial()
    {
        mat.SetColor(CoreColorId, coreColor);
        mat.SetColor(MidColorId, midColor);
        mat.SetColor(EdgeColorId, edgeColor);
        mat.SetFloat(GlowId, glow);
        mat.SetFloat(StreakId, streak);
        mat.SetFloat(AlphaId, alpha);
    }

    // ------------------------------------------------------------------ scheduling

    void AddPuff(Vector3 pos, Vector3 vel, float size, float start, float life)
    {
        Puff p = new Puff();
        p.pos = pos; p.vel = vel; p.size = size; p.start = start; p.life = Mathf.Max(life, 0.05f);
        p.seed = Rand(0f, 10f);
        puffs.Add(p);
    }

    void AddSparkle(Vector3 pos, float size, float start, float life, float rot)
    {
        Sparkle s = new Sparkle();
        s.pos = pos; s.size = size; s.start = start; s.life = Mathf.Max(life, 0.02f); s.rot = rot;
        sparkles.Add(s);
    }

    void AddLayer(float start, float spin, float lingerTime, float radius, float layerThickness, float trail,
                  float sweep, float startAngle, float lines, bool strong)
    {
        Layer L = new Layer();
        L.start = start; L.spin = Mathf.Max(spin, 0.02f); L.linger = Mathf.Max(lingerTime, 0.02f);
        L.radius = radius; L.thickness = layerThickness; L.trail = trail; L.sweep = sweep;
        L.startAngle = startAngle; L.lines = lines; L.strong = strong;
        L.seed = Rand(0f, 10f);
        layers.Add(L);

        // 칼이 지나가는 자리마다, 지나가는 그 순간에 바람 먼지가 일어남
        if (windAmount <= 0f) return;
        float a = 0.3f;
        while (a < sweep)
        {
            float x = 1f - Mathf.Sqrt(Mathf.Max(1f - a / sweep, 0f));       // 칼끝이 이 각도에 닿는 때
            float ang = startAngle + a;
            float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
            Vector3 outward = new Vector3(cs, sn * 0.4f, 0f);
            AddPuff(Ground(cs * radius * 0.92f, sn * radius * 0.92f, 0.1f),
                    outward * Rand(0.5f, 1.8f) + new Vector3(0f, Rand(0.2f, 0.8f), 0f),
                    Rand(0.8f, 1.4f) * (strong ? 1.35f : 1f), start + x * L.spin, Rand(0.35f, 0.6f));
            a += (strong ? Rand(0.28f, 0.4f) : Rand(0.42f, 0.6f)) / windAmount;
        }
    }

    /// <summary>3타의 충격: 바닥 충격파 + 사방으로 튀는 불티 + 섬광 + 크게 이는 바람</summary>
    void AddFinisherBurst(float hit)
    {
        if (finisherShockRing)
        {
            Shock sh = new Shock();
            sh.start = hit; sh.life = 0.38f; sh.seed = Rand(0f, 10f);
            shocks.Add(sh);
        }

        for (int i = 0; i < finisherSparks; i++)
        {
            float ang = (i + Rand(0f, 1f)) / finisherSparks * Tau;
            float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
            Vector3 dir = new Vector3(cs, sn * groundTilt + Rand(0f, 0.25f), 0f).normalized;
            Needle nd = new Needle();
            nd.pos = Ground(cs * 0.9f, sn * 0.9f, bladeHeight * Rand(0.3f, 1f));
            nd.vel = dir * Rand(6f, 13f);
            nd.size = Rand(0.5f, 1.0f);
            nd.start = hit + Rand(0f, 0.05f);
            nd.life = Rand(0.22f, 0.42f);
            needles.Add(nd);
        }

        // 전방과 그 양옆에 큰 섬광
        for (int i = 0; i < 3; i++)
        {
            float ang = i == 0 ? 0f : (i == 1 ? 0.9f : -0.9f);
            AddSparkle(Ground(Mathf.Cos(ang) * 0.92f, Mathf.Sin(ang) * 0.92f, bladeHeight),
                       Rand(1.6f, 2.6f), hit + Rand(0f, 0.05f), 0.18f, Rand(0f, 1.57f));
        }
        for (int i = 0; i < 10; i++)
        {
            float ang = Rand(0f, Tau);
            AddSparkle(Ground(Mathf.Cos(ang) * Rand(0.5f, 1.1f), Mathf.Sin(ang) * Rand(0.5f, 1.1f), Rand(0.2f, 2.0f)),
                       Rand(0.25f, 0.55f), hit + Rand(0.05f, 0.45f), Rand(0.2f, 0.4f), 0f);
        }

        if (windAmount > 0f)
        {
            int count = Mathf.RoundToInt(14f * windAmount);
            for (int i = 0; i < count; i++)
            {
                float ang = (i + Rand(0f, 1f)) / count * Tau;
                float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                AddPuff(Ground(cs * 0.85f, sn * 0.85f, 0.1f),
                        new Vector3(cs, sn * 0.4f, 0f) * Rand(1.5f, 3.5f) + new Vector3(0f, Rand(0.3f, 1.0f), 0f),
                        Rand(1.2f, 2.0f), hit + Rand(0f, 0.06f), Rand(0.45f, 0.75f));
            }
        }
    }

    // ------------------------------------------------------------------ mesh

    /// <summary>회전 참격 한 겹의 현재 상태. 보이지 않으면 false.</summary>
    bool LayerState(Layer L, out float head, out float tail, out float c1, out float wk, out float glowK)
    {
        head = tail = c1 = wk = glowK = 0f;
        float t = clock - L.start;
        float total = L.spin + L.linger;
        if (t <= 0f || t >= total) return false;

        float inv = 1f - Mathf.Min(t / L.spin, 1f);
        head = L.sweep * (1f - inv * inv);                              // 칼끝: 빠르게 돌다가 끝에서 감속
        float catchUp = SStep(L.spin * 0.85f, total, t);                // 다 돈 뒤에는 꼬리가 칼끝을 따라잡으며 사라짐
        tail = Mathf.Max(0f, head - L.trail * (1f - catchUp));
        wk = 1f - 0.5f * SStep(L.spin, total, t);

        // 마무리 일격은 더 오래 하얗게 달아오른 채로 남음
        c1 = Mathf.Lerp(1.3f, L.strong ? 0.62f : 0.42f, SStep(0f, L.spin * (L.strong ? 1.2f : 0.6f), t));
        glowK = (L.strong ? 1.6f : 1f) * (1f - SStep(L.spin * 0.8f, total, t));
        return true;
    }

    void BuildMeshes()
    {
        front.Clear();
        back.Clear();
        Vector4 none = Vector4.zero;

        // ---- 바닥 충격파 (뒤쪽 렌더러: 캐릭터 발밑에 깔림)
        for (int i = 0; i < shocks.Count; i++)
        {
            float t = (clock - shocks[i].start) / shocks[i].life;
            if (t <= 0f || t >= 1f) continue;
            AddGroundQuad(back, -1.3f, 1.3f, -1.3f, 1.3f, 0.05f, new Vector4(4f, t, shocks[i].seed, 0f), none, none);
        }

        // ---- 회전 참격: 먼 쪽 절반은 뒤쪽 렌더러, 가까운 쪽 절반은 앞쪽 렌더러
        for (int i = 0; i < layers.Count; i++)
        {
            Layer L = layers[i];
            float head, tail, c1, wk, glowK;
            if (!LayerState(L, out head, out tail, out c1, out wk, out glowK)) continue;

            Vector4 mode = new Vector4(0f, head, tail, L.startAngle);
            Vector4 prm = new Vector4(L.radius, L.thickness, c1, wk);
            Vector4 prm2 = new Vector4(glowK, L.lines, L.seed, 1f);
            Vector4 prm2Hidden = new Vector4(glowK, L.lines, L.seed, 0f);

            // 두 절반이 만나는 자리(gv = 0)를 SeamBand 만큼 겹침: 뒤쪽은 그 띠까지 다 그리고, 앞쪽은 띠 안에서 서서히 나타남.
            // 반듯한 경계선으로 나누면 그 높이에 선 캐릭터 위에서 참격이 칼로 자른 듯 끊겨 보이기 때문
            AddGroundQuad(back, -1.25f, 1.25f, -SeamBand, 1.25f, bladeHeight, mode, prm, prm2);
            AddGroundQuad(front, -1.25f, 1.25f, -1.25f, -SeamBand, bladeHeight, mode, prm, prm2);
            AddGroundStrip(front, -1.25f, 1.25f, -SeamBand, SeamBand, bladeHeight, mode, prm, prm2, prm2Hidden);
        }

        // ---- 바람 먼지: 떠오르며 커지고 옅어짐
        for (int i = 0; i < puffs.Count; i++)
        {
            Puff p = puffs[i];
            float age = clock - p.start;
            if (age <= 0f || age >= p.life) continue;

            float k = age / p.life;
            Vector3 pos = p.pos + p.vel * ((1f - Mathf.Exp(-2f * age)) / 2f);
            float halfSize = p.size * (0.6f + 0.6f * k) * 0.5f;
            float a = SStep(0f, 0.15f, k) * (1f - SStep(0.4f, 1f, k)) * 0.55f;
            AddQuad(front, pos, new Vector3(halfSize, 0f, 0f), new Vector3(0f, halfSize, 0f), new Vector4(1f, p.seed, age, a));
        }

        // ---- 불티: 바깥으로 튀어 나가며 감속, 짧아짐
        for (int i = 0; i < needles.Count; i++)
        {
            Needle nd = needles[i];
            float age = clock - nd.start;
            if (age <= 0f || age >= nd.life) continue;

            float k = age / nd.life;
            Vector3 pos = nd.pos + nd.vel * ((1f - Mathf.Exp(-4f * age)) / 4f);
            Vector3 dir = nd.vel.normalized;
            float halfLen = nd.size * (1f - 0.5f * k) * 0.5f;
            AddQuad(front, pos, dir * halfLen, new Vector3(-dir.y, dir.x, 0f) * (halfLen * 0.14f), new Vector4(3f, 1f - k * k, 0f, 0f));
        }

        // ---- 반짝임
        for (int i = 0; i < sparkles.Count; i++)
        {
            Sparkle sp = sparkles[i];
            float t = (clock - sp.start) / sp.life;
            if (t <= 0f || t >= 1f) continue;

            Vector3 ax = new Vector3(Mathf.Cos(sp.rot), Mathf.Sin(sp.rot), 0f) * (sp.size * 0.5f);
            AddQuad(front, sp.pos, ax, new Vector3(-ax.y, ax.x, 0f), new Vector4(2f, t, 0f, 0f));
        }

        front.Apply(mesh);
        back.Apply(backMesh);
    }

    // 범위 좌표의 사각형 (gu0..gu1, gv0..gv1). uv 에 범위 좌표를 그대로 실어 보냄
    void AddGroundQuad(Buffers b, float gu0, float gu1, float gv0, float gv1, float height, Vector4 mode, Vector4 prm, Vector4 prm2)
    {
        int q = b.verts.Count;
        AddVertex(b, Ground(gu0, gv0, height), new Vector2(gu0, gv0), mode, prm, prm2);
        AddVertex(b, Ground(gu0, gv1, height), new Vector2(gu0, gv1), mode, prm, prm2);
        AddVertex(b, Ground(gu1, gv0, height), new Vector2(gu1, gv0), mode, prm, prm2);
        AddVertex(b, Ground(gu1, gv1, height), new Vector2(gu1, gv1), mode, prm, prm2);
        b.tris.Add(q); b.tris.Add(q + 1); b.tris.Add(q + 2);
        b.tris.Add(q + 2); b.tris.Add(q + 1); b.tris.Add(q + 3);
    }

    const float SeamBand = 0.18f;        // 앞/뒤 절반이 겹치는 띠의 반폭 (범위 좌표)

    // AddGroundQuad 와 같지만, gv0 쪽 변과 gv1 쪽 변에 서로 다른 prm2 를 실음 (그 사이는 보간됨)
    void AddGroundStrip(Buffers b, float gu0, float gu1, float gv0, float gv1, float height, Vector4 mode, Vector4 prm, Vector4 prm2At0, Vector4 prm2At1)
    {
        int q = b.verts.Count;
        AddVertex(b, Ground(gu0, gv0, height), new Vector2(gu0, gv0), mode, prm, prm2At0);
        AddVertex(b, Ground(gu0, gv1, height), new Vector2(gu0, gv1), mode, prm, prm2At1);
        AddVertex(b, Ground(gu1, gv0, height), new Vector2(gu1, gv0), mode, prm, prm2At0);
        AddVertex(b, Ground(gu1, gv1, height), new Vector2(gu1, gv1), mode, prm, prm2At1);
        b.tris.Add(q); b.tris.Add(q + 1); b.tris.Add(q + 2);
        b.tris.Add(q + 2); b.tris.Add(q + 1); b.tris.Add(q + 3);
    }

    // 중심과 두 반축으로 사각형 하나. uv.x 는 ax 방향, uv.y 는 ay 방향
    void AddQuad(Buffers b, Vector3 center, Vector3 ax, Vector3 ay, Vector4 mode)
    {
        Vector4 none = Vector4.zero;
        int q = b.verts.Count;
        AddVertex(b, center - ax - ay, new Vector2(0f, 0f), mode, none, none);
        AddVertex(b, center - ax + ay, new Vector2(0f, 1f), mode, none, none);
        AddVertex(b, center + ax - ay, new Vector2(1f, 0f), mode, none, none);
        AddVertex(b, center + ax + ay, new Vector2(1f, 1f), mode, none, none);
        b.tris.Add(q); b.tris.Add(q + 1); b.tris.Add(q + 2);
        b.tris.Add(q + 2); b.tris.Add(q + 1); b.tris.Add(q + 3);
    }

    static void AddVertex(Buffers b, Vector3 local, Vector2 uv, Vector4 mode, Vector4 prm, Vector4 prm2)
    {
        b.verts.Add(local);
        b.uv0.Add(uv);
        b.uv1.Add(mode);
        b.uv2.Add(prm);
        b.uv3.Add(prm2);
    }

#if UNITY_EDITOR
    // 메뉴: GameObject > Effects > Spin Slash VFX (회전베기)
    [UnityEditor.MenuItem("GameObject/Effects/Spin Slash VFX", false, 10)]
    static void CreateFromMenu(UnityEditor.MenuCommand command)
    {
        GameObject go = new GameObject("SpinSlashVFX");
        SpinSlashVFX fx = go.AddComponent<SpinSlashVFX>();
        fx.previewLoop = true;               // Play 를 누르면 바로 반복 재생되도록. 실제 사용 시에는 끄세요
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Spin Slash VFX");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
