using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 서릿발 VFX.
///   1타 (PlaySpread) : 시전자 발밑에서 바라보는 방향으로 부채꼴 얼음 장판이 냉기를 앞세우며 퍼져 나갑니다.
///   2타 (PlayBurst)  : 퍼져 있는 장판이 하얗게 달아오른 뒤, 가까운 곳부터 결정 조각 단위로 깨져 나가며
///                      냉기와 얼음 파편이 터져 오르고 장판이 사라집니다.
///
///   frost.PlaySpread(feetPosition, facingRight);   // 1타
///   frost.PlayBurst();                              // 2타 (장판이 남아 있을 때)
///   frost.PlayFull(feetPosition, facingRight);      // 1타 후 burstDelay 뒤에 2타까지 자동 재생
///   frost.Contains(enemyFeetPosition);              // 그 위치가 지금 장판 안인지 (데미지 판정용)
///
/// 이펙트는 월드 좌표에 고정되므로, 이 오브젝트가 캐릭터의 자식이어도 캐릭터를 따라 움직이지 않습니다.
/// 장판은 캐릭터 발밑에 깔려야 하므로 별도의 정렬 순서(Floor Sorting Order)를 씁니다.
///
/// 장판과 냉기는 이 오브젝트의 로컬 XY 평면에 그려집니다. 넘겨받은 월드 위치는 그 평면으로 (로컬 Z 를 따라) 눌러서 씁니다 —
/// 오브젝트를 카메라 쪽으로 돌려 두면 3D 공간의 점이 화면에 보이는 자리 그대로 놓입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FrostFieldVFX : MonoBehaviour
{
    public enum FinishAction
    {
        None,      // 그대로 대기 (다시 호출해서 재사용)
        Disable,   // 비활성화 (오브젝트 풀링)
        Destroy    // 오브젝트 삭제 (Instantiate 해서 쓰는 경우)
    }

    [Header("Shader (컴포넌트를 붙이면 자동으로 채워짐)")]
    public Shader frostShader;

    [Header("장판 모양")]
    [Tooltip("부채꼴의 길이 (월드 유닛)")]
    [Min(0.5f)] public float radius = 7f;
    [Tooltip("부채꼴이 벌어진 각도의 절반 (바닥 기준, 도)")]
    [Range(5f, 80f)] public float halfAngle = 28f;
    [Tooltip("바닥이 화면에서 세로로 눌려 보이는 비율. 카메라가 바닥을 비스듬히 내려다볼수록 작게")]
    [Range(0.1f, 1f)] public float groundTilt = 0.42f;
    [Tooltip("얼음 결정 무늬 한 칸의 크기 (월드 유닛). 2타에서 이 칸 단위로 깨집니다")]
    [Min(0.1f)] public float cellSize = 0.55f;

    [Header("1타 - 확산")]
    [Tooltip("장판이 끝까지 퍼지는 시간")]
    [Min(0.05f)] public float spreadTime = 0.35f;
    [Tooltip("2타를 쓰지 않았을 때 장판이 남아 있는 시간")]
    [Min(0f)] public float fieldLifetime = 3f;
    [Tooltip("2타 없이 수명이 다했을 때 장판이 옅어지며 사라지는 시간")]
    [Min(0.05f)] public float fieldFade = 0.45f;
    [Tooltip("퍼지는 동안 앞쪽에서 피어오르는 냉기의 양 (초당)")]
    public float spreadMistRate = 45f;
    [Tooltip("퍼진 뒤 장판 위에서 조금씩 피어오르는 냉기의 양 (초당)")]
    public float idleMistRate = 4f;
    [Tooltip("1타 냉기의 불투명도. 낮을수록 옅어서 장판이 잘 보임")]
    [Range(0f, 1f)] public float spreadMistOpacity = 0.35f;

    [Header("2타 - 장판 파쇄")]
    [Tooltip("깨지기 직전 장판이 하얗게 달아오르는 시간")]
    [Min(0f)] public float chargeTime = 0.15f;
    [Tooltip("가까운 곳부터 먼 곳까지 깨져 나가는 데 걸리는 시간")]
    [Min(0.02f)] public float burstWaveTime = 0.25f;
    [Tooltip("냉기와 파편이 터져 오르는 지점의 수")]
    [Range(1, 40)] public int burstCount = 12;
    [Tooltip("지점마다 터져 오르는 냉기 덩이 수")]
    [Range(0, 10)] public int burstPuffs = 3;
    [Tooltip("2타에서 터져 오르는 냉기의 불투명도. 1타보다 진하되, 깨지는 장판이 비쳐 보일 정도로")]
    [Range(0f, 1f)] public float burstMistOpacity = 0.55f;
    [Tooltip("지점마다 튀어 오르는 얼음 파편 수")]
    [Range(0, 12)] public int burstShards = 5;

    [Header("PlayFull 전용")]
    [Tooltip("장판이 다 퍼진 뒤 2타가 시작되기까지의 간격")]
    [Min(0f)] public float burstDelay = 0.55f;

    [Header("Color")]
    [ColorUsage(false, true)] public Color coreColor = Color.white;
    [ColorUsage(false, true)] public Color midColor = new Color(0.66f, 0.93f, 1f);
    [ColorUsage(false, true)] public Color edgeColor = new Color(0.13f, 0.50f, 1f);
    [Range(0f, 1f)] public float alpha = 1f;

    [Header("Playback")]
    public bool useUnscaledTime = false;
    [Tooltip("0 이면 쓸 때마다 배치가 달라지고, 다른 값이면 항상 같은 배치")]
    public int seed = 0;
    public FinishAction onFinishedAction = FinishAction.None;

    [Header("Sorting")]
    [Tooltip("냉기, 파편, 반짝임 (캐릭터보다 앞)")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 100;
    [Tooltip("바닥 장판 (배경보다 앞, 캐릭터보다 뒤가 되도록 맞추세요)")]
    public string floorSortingLayerName = "Default";
    public int floorSortingOrder = -1;

    [Header("Preview")]
    [Tooltip("켜두면 1타 + 2타를 계속 반복 재생 (룩 조정용)")]
    public bool previewLoop = false;
    public float previewPause = 0.6f;

    [Header("Events")]
    [Tooltip("장판이 끝까지 다 퍼진 순간")]
    public UnityEvent onSpreadEnd;
    [Tooltip("장판이 깨지기 시작하는 순간 (2타 데미지, 카메라 흔들림 등)")]
    public UnityEvent onBurst;
    public UnityEvent onFinished;

    public bool IsPlaying { get { return active; } }

    /// <summary>장판이 지금 퍼져 있는 정도 (0 = 없음, 1 = 끝까지)</summary>
    public float Front
    {
        get
        {
            if (!active || fieldStart < 0f) return 0f;
            float t = Mathf.Clamp01((clock - fieldStart) / spreadTime);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }
    }

    // ------------------------------------------------------------------ internals

    const float CellJitter = 0.18f;      // 결정 칸마다 깨지는 타이밍이 어긋나는 폭 (셰이더의 값과 같아야 함)

    static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    struct Puff      // 냉기 한 덩이
    {
        public Vector3 pos, vel;
        public float size, start, life, seed, opacity;
    }

    struct Sparkle   // 반짝임 / 섬광
    {
        public Vector3 pos;
        public float size, start, life, rot;
    }

    struct Shard     // 깨져 나온 얼음 조각
    {
        public Vector3 pos, vel;
        public float rot, angVel, size, start, life;
    }

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    MeshFilter floorFilter;
    MeshRenderer floorRenderer;
    Mesh mesh, floorMesh;
    Material mat;
    bool initialized;
    System.Random rng;

    readonly List<Puff> puffs = new List<Puff>();
    readonly List<Sparkle> sparkles = new List<Sparkle>();
    readonly List<Shard> shards = new List<Shard>();

    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector2> uv0 = new List<Vector2>();
    readonly List<Vector4> uv1 = new List<Vector4>();
    readonly List<Vector4> uv2 = new List<Vector4>();
    readonly List<int> tris = new List<int>();

    readonly Vector3[] floorVerts = new Vector3[4];
    readonly List<Vector2> floorUv0 = new List<Vector2>(4);
    readonly List<Vector4> floorUv1 = new List<Vector4>(4);
    readonly List<Vector4> floorUv2 = new List<Vector4>(4);
    static readonly int[] QuadTris = { 0, 1, 2, 2, 1, 3 };

    float clock;
    bool active;
    Vector3 origin;
    float dirSign = 1f;
    float fieldSeed;
    float fieldStart = -1f;
    float burstStart = -1f;         // 2타를 시작한 시각 (없으면 음수)
    float pendingBurstTime = -1f;   // PlayFull 에서 2타를 시작할 시각 (없으면 음수)
    bool spreadEndFired, burstFired;
    float mistAcc;
    float previewTimer;

    /// <summary>깨지는 파동이 지금 어디까지 왔는지 (반지름 대비). 아직 시작 전이면 음수.</summary>
    float BreakFront
    {
        get
        {
            if (burstStart < 0f) return -1f;
            float tb = clock - burstStart - chargeTime;
            return tb > 0f ? tb / burstWaveTime : -1f;
        }
    }

    // ------------------------------------------------------------------ public API

    /// <summary>1타: feetPosition(시전자 발밑)에서 바라보는 방향으로 장판을 퍼뜨립니다.</summary>
    public void PlaySpread(Vector3 feetPosition, bool facingRight)
    {
        Init();
        if (mat == null) return;

        ClearAll();
        rng = seed != 0 ? new System.Random(seed) : new System.Random();

        origin = ToPlane(feetPosition);
        dirSign = facingRight ? 1f : -1f;
        fieldSeed = Rand(0f, 10f);
        fieldStart = clock;
        active = true;

        mesh.Clear();
        meshRenderer.enabled = true;
        floorRenderer.enabled = true;
        BuildFloor();
        ApplyMaterial();
    }

    /// <summary>1타: 이 오브젝트의 위치에서 바라보는 방향으로 퍼뜨립니다. (애니메이션 이벤트용)</summary>
    public void PlaySpreadForward()
    {
        PlaySpread(transform.position, transform.lossyScale.x >= 0f);     // 캐릭터를 scale.x 로 뒤집는 경우를 따라감
    }

    /// <summary>2타: 퍼져 있는 장판을 깨뜨립니다.</summary>
    public void PlayBurst()
    {
        if (!active || fieldStart < 0f)
        {
            Debug.LogWarning("[FrostFieldVFX] 퍼져 있는 장판이 없습니다. PlaySpread 를 먼저 호출하세요.", this);
            return;
        }
        if (burstStart >= 0f) return;                    // 이미 깨지는 중

        burstStart = clock;
        pendingBurstTime = -1f;
        float first = burstStart + chargeTime;
        float half = halfAngle * Mathf.Deg2Rad;
        float reach = Front;                             // 아직 다 퍼지기 전에 터뜨리면 퍼진 데까지만

        // 부채꼴 안에 고리 모양으로 골고루 배치 (먼 고리일수록 더 많이).
        // 각 지점은 깨지는 파동이 그 거리를 지나는 순간에 터짐
        int n = Mathf.Max(1, burstCount);
        int rings = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(n / 1.6f)));
        for (int ring = 0; ring < rings; ring++)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(n * (ring + 1) * 2f / (rings * (rings + 1))));
            float ringR = (ring + 0.5f) / rings;
            for (int j = 0; j < count; j++)
            {
                float th = Mathf.Lerp(-half, half, (j + 0.5f) / count) * 0.82f + Rand(-0.06f, 0.06f);
                float rr = (0.18f + 0.78f * ringR + Rand(-0.07f, 0.07f)) * reach;      // 반지름 대비
                AddEruption(GroundPolar(rr * radius, th), first + rr * burstWaveTime);
            }
        }
    }

    /// <summary>1타를 재생하고, 장판이 다 퍼진 뒤 burstDelay 후에 2타까지 이어서 재생합니다.</summary>
    public void PlayFull(Vector3 feetPosition, bool facingRight)
    {
        PlaySpread(feetPosition, facingRight);
        if (active) pendingBurstTime = clock + spreadTime + burstDelay;
    }

    /// <summary>worldPosition(바닥 위의 점, 보통 적의 발밑)이 지금 장판 안에 있는지. 이미 깨져 나간 자리는 false.</summary>
    public bool Contains(Vector3 worldPosition)
    {
        if (!active || fieldStart < 0f) return false;
        Vector3 p = ToPlane(worldPosition);
        float gx = (p.x - origin.x) * dirSign;
        float gz = (p.y - origin.y) / Mathf.Max(groundTilt, 0.01f);
        float r = Mathf.Sqrt(gx * gx + gz * gz) / radius;
        if (r > Front) return false;
        if (BreakFront > r + CellJitter) return false;
        return Mathf.Abs(Mathf.Atan2(gz, gx)) <= halfAngle * Mathf.Deg2Rad;
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
        frostShader = Shader.Find("VFX/FrostField");
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
                if (previewTimer <= 0f) PlayFull(transform.position, transform.lossyScale.x >= 0f);
            }
            return;
        }

        clock += dt;

        float spreadT = (clock - fieldStart) / spreadTime;

        if (!spreadEndFired && spreadT >= 1f)
        {
            spreadEndFired = true;
            if (onSpreadEnd != null) onSpreadEnd.Invoke();
            if (!active) return;                         // 이벤트 안에서 Stop() 을 부른 경우
        }

        if (pendingBurstTime >= 0f && clock >= pendingBurstTime) PlayBurst();

        if (burstStart >= 0f && !burstFired && clock >= burstStart + chargeTime)
        {
            burstFired = true;
            if (onBurst != null) onBurst.Invoke();
            if (!active) return;
        }

        SpawnMist(spreadT, dt);

        // ---- 수명이 끝난 요소 정리
        for (int i = puffs.Count - 1; i >= 0; i--)
            if (clock >= puffs[i].start + puffs[i].life) puffs.RemoveAt(i);
        for (int i = sparkles.Count - 1; i >= 0; i--)
            if (clock >= sparkles[i].start + sparkles[i].life) sparkles.RemoveAt(i);
        for (int i = shards.Count - 1; i >= 0; i--)
            if (clock >= shards[i].start + shards[i].life) shards.RemoveAt(i);

        // ---- 끝났는지: 장판이 다 사라지고, 떠 있는 것도 없을 때
        float fieldEnd;
        if (burstStart >= 0f) fieldEnd = burstStart + chargeTime + burstWaveTime * 1.4f;
        else if (pendingBurstTime >= 0f) fieldEnd = float.MaxValue;
        else fieldEnd = fieldStart + spreadTime + fieldLifetime + fieldFade;

        if (clock >= fieldEnd && puffs.Count == 0 && sparkles.Count == 0 && shards.Count == 0)
        {
            Finish();
            return;
        }

        BuildFloor();
        BuildMesh();
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
        puffs.Clear();
        sparkles.Clear();
        shards.Clear();
        fieldStart = -1f;
        burstStart = -1f;
        pendingBurstTime = -1f;
        spreadEndFired = false;
        burstFired = false;
        mistAcc = 0f;
    }

    // ------------------------------------------------------------------ setup

    void Init()
    {
        if (initialized) return;
        initialized = true;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (frostShader == null) frostShader = Shader.Find("VFX/FrostField");
        if (frostShader == null)
        {
            Debug.LogError("[FrostFieldVFX] 'VFX/FrostField' 셰이더를 찾을 수 없습니다. FrostField.shader 를 프로젝트에 넣고 Frost Shader 칸에 지정하세요.", this);
            enabled = false;
            return;
        }

        mat = new Material(frostShader);
        mat.name = "FrostField (Instance)";
        mat.hideFlags = HideFlags.DontSave;

        mesh = NewMesh("FrostFieldMesh");
        meshFilter.sharedMesh = mesh;
        SetupRenderer(meshRenderer, sortingLayerName, sortingOrder);

        // 바닥 장판은 캐릭터 뒤에 깔려야 해서 정렬 순서가 다른 별도의 렌더러로 그림
        GameObject floorGo = new GameObject("FrostFloor");
        floorGo.transform.SetParent(transform, false);
        floorFilter = floorGo.AddComponent<MeshFilter>();
        floorRenderer = floorGo.AddComponent<MeshRenderer>();
        floorMesh = NewMesh("FrostFloorMesh");
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

    /// <summary>바닥 좌표(앞으로 gx, 옆으로 gz) -> 로컬 평면 위치. 바닥은 화면에서 세로로 눌려 보임.</summary>
    Vector3 Ground(float gx, float gz)
    {
        return origin + new Vector3(dirSign * gx, gz * groundTilt, 0f);
    }

    Vector3 GroundPolar(float r, float theta)
    {
        return Ground(r * Mathf.Cos(theta), r * Mathf.Sin(theta));
    }

    void ApplyMaterial()
    {
        mat.SetColor(CoreColorId, coreColor);
        mat.SetColor(MidColorId, midColor);
        mat.SetColor(EdgeColorId, edgeColor);
        mat.SetFloat(AlphaId, alpha);
    }

    // ------------------------------------------------------------------ spawning

    void AddPuff(Vector3 pos, Vector3 vel, float size, float start, float life, float opacity)
    {
        Puff p = new Puff();
        p.pos = pos; p.vel = vel; p.size = size; p.start = start; p.life = Mathf.Max(life, 0.05f); p.opacity = opacity;
        p.seed = Rand(0f, 10f);
        puffs.Add(p);
    }

    void AddSparkle(Vector3 pos, float size, float start, float life, float rot)
    {
        Sparkle s = new Sparkle();
        s.pos = pos; s.size = size; s.start = start; s.life = Mathf.Max(life, 0.02f); s.rot = rot;
        sparkles.Add(s);
    }

    /// <summary>한 지점에서 얼음 기운이 터져 오름: 섬광 + 위로 솟는 냉기 + 튀어 오르는 얼음 조각 + 반짝임</summary>
    void AddEruption(Vector3 basePos, float start)
    {
        AddSparkle(basePos + new Vector3(0f, 0.15f, 0f), Rand(1.5f, 2.3f), start, 0.18f, Rand(0f, 1.57f));

        for (int k = 0; k < burstPuffs; k++)
        {
            AddPuff(basePos + new Vector3(Rand(-0.6f, 0.6f), Rand(0f, 0.3f), 0f),
                    new Vector3(Rand(-1.0f, 1.0f), Rand(1.0f, 3.6f), 0f), Rand(1.1f, 2.1f), start + Rand(0f, 0.05f), Rand(0.4f, 0.75f), burstMistOpacity);
        }

        for (int k = 0; k < burstShards; k++)
        {
            Shard sh = new Shard();
            sh.pos = basePos + new Vector3(Rand(-0.5f, 0.5f), Rand(-0.1f, 0.1f), 0f);
            sh.vel = Polar(Rand(0.5f, 2.64f), Rand(2.0f, 7.5f));       // 위쪽 반원 방향으로
            sh.rot = Rand(0f, 6.283f);
            sh.angVel = Rand(-12f, 12f);
            sh.size = Rand(0.14f, 0.38f);
            sh.start = start + Rand(0f, 0.04f);
            sh.life = Rand(0.4f, 0.8f);
            shards.Add(sh);
        }

        for (int k = 0; k < 3; k++)
        {
            AddSparkle(basePos + new Vector3(Rand(-0.9f, 0.9f), Rand(0.2f, 2.2f), 0f),
                       Rand(0.25f, 0.55f), start + Rand(0.05f, 0.45f), Rand(0.2f, 0.4f), 0f);
        }
    }

    /// <summary>퍼지는 동안에는 앞쪽 가장자리에서 많이, 퍼진 뒤에는 장판 위에서 조금씩 냉기가 피어오름</summary>
    void SpawnMist(float spreadT, float dt)
    {
        if (burstStart >= 0f) return;                    // 2타가 시작되면 분출 쪽 냉기만
        float half = halfAngle * Mathf.Deg2Rad;

        if (spreadT >= 0f && spreadT < 1f)
        {
            float inv = 1f - spreadT;
            float front = 1f - inv * inv * inv;
            mistAcc += dt * spreadMistRate;
            while (mistAcc >= 1f)
            {
                mistAcc -= 1f;
                Vector3 pos = GroundPolar(front * radius * Rand(0.8f, 1.02f), Rand(-half, half)) + new Vector3(0f, Rand(0f, 0.35f), 0f);
                AddPuff(pos, new Vector3(dirSign * Rand(0.5f, 2.5f), Rand(0.1f, 0.7f), 0f), Rand(1.3f, 2.5f), clock, Rand(0.45f, 0.85f), spreadMistOpacity);
            }
        }
        else if (pendingBurstTime >= 0f || clock < fieldStart + spreadTime + fieldLifetime)
        {
            // (2타 없이 수명이 다해 사라지는 중에는 더 피어오르지 않음)
            mistAcc += dt * idleMistRate;
            while (mistAcc >= 1f)
            {
                mistAcc -= 1f;
                Vector3 pos = GroundPolar(radius * Mathf.Sqrt(Rand(0.05f, 1f)), Rand(-half, half) * 0.9f);
                AddPuff(pos, new Vector3(Rand(-0.2f, 0.2f), Rand(0.2f, 0.6f), 0f), Rand(0.6f, 1.1f), clock, Rand(0.6f, 1.0f), spreadMistOpacity);
            }
        }
    }

    // ------------------------------------------------------------------ mesh

    void BuildFloor()
    {
        // ---- 장판의 현재 상태
        float front = Front;
        float charge = 0f;
        float fade = 1f;

        if (burstStart >= 0f)
        {
            charge = SStep(0f, Mathf.Max(chargeTime, 0.001f), clock - burstStart);
        }
        else if (pendingBurstTime < 0f)
        {
            fade = 1f - SStep(0f, fieldFade, clock - (fieldStart + spreadTime + fieldLifetime));   // 2타 없이 수명이 다함
        }

        // ---- 부채꼴을 덮는 사각형 하나 (모양은 셰이더가 잘라냄)
        float half = halfAngle * Mathf.Deg2Rad;
        float gx0 = -0.05f * radius;
        float gx1 = 1.12f * radius;
        float gzMax = radius * Mathf.Min(1.25f, 1.12f * Mathf.Sin(Mathf.Min(half + 0.2f, 1.5708f)) + 0.12f);

        floorVerts[0] = ToLocal(Ground(gx0, -gzMax));
        floorVerts[1] = ToLocal(Ground(gx0, gzMax));
        floorVerts[2] = ToLocal(Ground(gx1, -gzMax));
        floorVerts[3] = ToLocal(Ground(gx1, gzMax));

        floorUv0.Clear(); floorUv1.Clear(); floorUv2.Clear();
        floorUv0.Add(new Vector2(gx0, -gzMax) / radius);
        floorUv0.Add(new Vector2(gx0, gzMax) / radius);
        floorUv0.Add(new Vector2(gx1, -gzMax) / radius);
        floorUv0.Add(new Vector2(gx1, gzMax) / radius);

        Vector4 mode = new Vector4(0f, front, charge, fade);
        Vector4 prm = new Vector4(half, radius / Mathf.Max(cellSize, 0.05f), fieldSeed, BreakFront);
        for (int i = 0; i < 4; i++) { floorUv1.Add(mode); floorUv2.Add(prm); }

        floorMesh.Clear();
        floorMesh.vertices = floorVerts;
        floorMesh.SetUVs(0, floorUv0);
        floorMesh.SetUVs(1, floorUv1);
        floorMesh.SetUVs(2, floorUv2);
        floorMesh.triangles = QuadTris;
        floorMesh.RecalculateBounds();
    }

    void BuildMesh()
    {
        verts.Clear(); uv0.Clear(); uv1.Clear(); uv2.Clear(); tris.Clear();
        Vector4 none = Vector4.zero;

        // ---- 냉기: 떠오르며 커지고 옅어짐
        for (int i = 0; i < puffs.Count; i++)
        {
            Puff p = puffs[i];
            float age = clock - p.start;
            if (age <= 0f || age >= p.life) continue;

            float k = age / p.life;
            Vector3 pos = p.pos + p.vel * ((1f - Mathf.Exp(-2f * age)) / 2f);
            float halfSize = p.size * (0.6f + 0.6f * k) * 0.5f;
            float a = SStep(0f, 0.15f, k) * (1f - SStep(0.45f, 1f, k)) * p.opacity;
            AddQuad(pos, new Vector3(halfSize, 0f, 0f), new Vector3(0f, halfSize, 0f), new Vector4(1f, p.seed, age, a), none);
        }

        // ---- 얼음 파편: 튀어 오르다 떨어지며 작아짐
        for (int i = 0; i < shards.Count; i++)
        {
            Shard sh = shards[i];
            float age = clock - sh.start;
            if (age <= 0f || age >= sh.life) continue;

            float k = age / sh.life;
            Vector3 pos = sh.pos + sh.vel * ((1f - Mathf.Exp(-2.5f * age)) / 2.5f) + new Vector3(0f, -4f * age * age, 0f);
            float rot = sh.rot + sh.angVel * age;
            float size = sh.size * (1f - 0.5f * k);

            Vector4 mode = new Vector4(3f, 1f - k * k, 0f, 0f);
            int q = verts.Count;
            AddVertex(pos + Polar(rot, size), new Vector2(1f, 0f), mode, none);           // uv = 무게중심 좌표
            AddVertex(pos + Polar(rot + 2.3f, size * 0.55f), new Vector2(0f, 1f), mode, none);
            AddVertex(pos + Polar(rot + 4.1f, size * 0.75f), new Vector2(0f, 0f), mode, none);
            tris.Add(q); tris.Add(q + 1); tris.Add(q + 2);
        }

        // ---- 반짝임
        for (int i = 0; i < sparkles.Count; i++)
        {
            Sparkle sp = sparkles[i];
            float t = (clock - sp.start) / sp.life;
            if (t <= 0f || t >= 1f) continue;

            Vector3 ax = Polar(sp.rot, sp.size * 0.5f);
            AddQuad(sp.pos, ax, new Vector3(-ax.y, ax.x, 0f), new Vector4(2f, t, 0f, 0f), none);
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetUVs(2, uv2);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }

    // 중심과 두 반축으로 사각형 하나. uv.x 는 ax 방향, uv.y 는 ay 방향
    void AddQuad(Vector3 center, Vector3 ax, Vector3 ay, Vector4 mode, Vector4 prm)
    {
        int q = verts.Count;
        AddVertex(center - ax - ay, new Vector2(0f, 0f), mode, prm);
        AddVertex(center - ax + ay, new Vector2(0f, 1f), mode, prm);
        AddVertex(center + ax - ay, new Vector2(1f, 0f), mode, prm);
        AddVertex(center + ax + ay, new Vector2(1f, 1f), mode, prm);
        tris.Add(q); tris.Add(q + 1); tris.Add(q + 2);
        tris.Add(q + 2); tris.Add(q + 1); tris.Add(q + 3);
    }

    void AddVertex(Vector3 world, Vector2 uv, Vector4 mode, Vector4 prm)
    {
        verts.Add(ToLocal(world));
        uv0.Add(uv);
        uv1.Add(mode);
        uv2.Add(prm);
    }

    // 메시를 짜는 좌표가 이미 로컬 평면이므로 그대로 씀
    static Vector3 ToLocal(Vector3 p)
    {
        p.z = 0f;
        return p;
    }

    /// <summary>월드 위치 -> 이 오브젝트의 로컬 XY 평면 (로컬 Z 를 따라 눌러 붙임)</summary>
    Vector3 ToPlane(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        local.z = 0f;
        return local;
    }

#if UNITY_EDITOR
    // 메뉴: GameObject > Effects > Frost Field VFX (서릿발)
    [UnityEditor.MenuItem("GameObject/Effects/Frost Field VFX", false, 10)]
    static void CreateFromMenu(UnityEditor.MenuCommand command)
    {
        GameObject go = new GameObject("FrostFieldVFX");
        FrostFieldVFX fx = go.AddComponent<FrostFieldVFX>();
        fx.previewLoop = true;               // Play 를 누르면 바로 반복 재생되도록. 실제 사용 시에는 끄세요
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Frost Field VFX");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
