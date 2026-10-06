using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 갈고리 사슬 VFX.
/// 한 점(시전자의 손)에서 여러 가닥의 사슬이 부채꼴로 뻗어 나가 대상에 꽂히고, 잠시 팽팽하게 당겨졌다가 회수됩니다.
/// 빈 오브젝트에 이 컴포넌트만 붙이면 메시와 머티리얼을 알아서 만들며, Fire(...) 를 호출할 때마다 한 번 재생합니다.
///
///   chain.Fire(handTransform, enemyTransform);   // 손과 적을 따라다님
///   chain.Fire(originPos, targetPos);            // 고정된 두 점
///
/// 방향 반전은 따로 필요 없습니다. 시작점과 대상의 위치만으로 방향이 정해집니다.
///
/// 사슬은 이 오브젝트의 로컬 XY 평면에 그려집니다. 넘겨받은 월드 위치는 그 평면으로 (로컬 Z 를 따라) 눌러서 씁니다 —
/// 오브젝트를 카메라 쪽으로 돌려 두면 3D 공간의 점이 화면에 보이는 자리 그대로 놓입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HookChainVFX : MonoBehaviour
{
    public enum EndMode
    {
        Retract,   // 갈고리가 시전자에게 되돌아옴
        Fade       // 그 자리에서 사라짐
    }

    public enum FinishAction
    {
        None,      // 그대로 대기 (다시 Fire 해서 재사용)
        Disable,   // 비활성화 (오브젝트 풀링)
        Destroy    // 오브젝트 삭제 (Instantiate 해서 쓰는 경우)
    }

    [Header("Shader (컴포넌트를 붙이면 자동으로 채워짐)")]
    public Shader chainShader;

    [Header("Chains")]
    [Range(1, 12)] public int chainCount = 5;
    [Tooltip("갈고리들이 꽂히는 범위의 폭 (진행 방향에 수직, 월드 유닛)")]
    public float spread = 2.4f;
    [Tooltip("가닥마다 길이가 달라지는 정도 (진행 방향, 월드 유닛)")]
    public float depthJitter = 0.5f;
    [Tooltip("0 이면 쏠 때마다 배치가 달라지고, 다른 값이면 항상 같은 배치")]
    public int seed = 0;

    [Header("Timing")]
    [Min(0.02f)] public float launchTime = 0.14f;
    [Tooltip("가닥 사이의 발사 간격")]
    [Min(0f)] public float stagger = 0.03f;
    [Min(0f)] public float holdTime = 0.40f;
    [Min(0.02f)] public float retractTime = 0.20f;
    public EndMode endMode = EndMode.Retract;
    [Tooltip("켜면 Release() 를 호출할 때까지 꽂힌 채로 유지 (잡기 스킬)")]
    public bool holdUntilReleased = false;
    public bool useUnscaledTime = false;
    public FinishAction onFinishedAction = FinishAction.None;

    [Header("Look")]
    [Tooltip("고리 하나의 길이 (월드 유닛)")]
    [Min(0.02f)] public float linkSize = 0.22f;
    [Min(0.05f)] public float tipLength = 1.0f;
    [Tooltip("발사/회수 때 사슬이 출렁이는 정도")]
    public float waveAmplitude = 0.28f;
    public Color baseColor = new Color(0.10f, 0.17f, 0.34f);
    public Color lightColor = new Color(0.50f, 0.76f, 0.98f);
    public Color outlineColor = new Color(0.02f, 0.05f, 0.20f);
    public Color glintColor = new Color(1f, 1f, 1f);
    [ColorUsage(false, true)] public Color glowColor = new Color(0.13f, 0.50f, 1f);
    [Range(0f, 2f)] public float glow = 0.45f;
    [Range(0f, 1f)] public float alpha = 1f;

    [Header("Impact Flash")]
    public float flashSize = 1.25f;
    [Min(0.01f)] public float flashTime = 0.14f;

    [Header("Target (Fire() 를 인자 없이 호출할 때 사용)")]
    [Tooltip("사슬이 나오는 곳. 비우면 이 오브젝트의 위치")]
    public Transform originTransform;
    public Transform targetTransform;
    [Tooltip("회수할 때 대상을 시전자 쪽으로 끌어옴 (대상을 Transform 으로 넘긴 경우에만)")]
    public bool pullTarget = false;
    [Tooltip("끌어올 때 시전자로부터 이 거리에서 멈춤")]
    public float pullStopDistance = 1.2f;

    [Header("Sorting")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 100;

    [Header("Preview")]
    [Tooltip("켜두면 계속 반복 발사 (룩 조정용). 대상이 없으면 아래 오프셋 위치로 쏩니다")]
    public bool previewLoop = false;
    public Vector2 previewTargetOffset = new Vector2(5.5f, -0.4f);
    public float previewPause = 0.6f;

    [Header("Events")]
    [Tooltip("첫 갈고리가 꽂히는 순간 (데미지, 카메라 흔들림, 사운드 등)")]
    public UnityEvent onHit;
    public UnityEvent onFinished;

    public bool IsPlaying { get { return playing; } }

    // ------------------------------------------------------------------ internals

    const float StripAspect = 1.4f;      // 사슬 띠의 폭 = linkSize * 1.4 (셰이더의 _Aspect 와 같아야 함)
    const int Segments = 20;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int LightColorId = Shader.PropertyToID("_LightColor");
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int GlintColorId = Shader.PropertyToID("_GlintColor");
    static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly int AspectId = Shader.PropertyToID("_Aspect");

    struct Chain
    {
        public Vector3 offset;     // 대상 위치 기준 갈고리 끝점 (로컬 평면)
        public float delay;
        public float phase;
        public float flashRot;
        public Vector3 tip;        // 현재 갈고리 끝 위치 (로컬 평면)
    }

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;
    Material mat;
    bool initialized;

    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector2> uv0 = new List<Vector2>();
    readonly List<Vector2> uv1 = new List<Vector2>();
    readonly List<Color> cols = new List<Color>();
    readonly List<int> tris = new List<int>();

    Chain[] chains = new Chain[0];
    Transform curOriginT, curTargetT;
    Vector3 curOriginP, curTargetP;
    bool playing;
    bool hitInvoked;
    bool releaseRequested;
    float time;
    float retractStart;        // < 0 이면 아직 정해지지 않음 (holdUntilReleased)
    Vector3 pullFrom;
    bool pullFromSet;
    float previewTimer;

    /// <summary>넣어 두면 Transform 대신 매 프레임 이 값을 시작점/대상 위치(월드)로 씁니다. (몸통 중심을 따라갈 때)</summary>
    public System.Func<Vector3> originProvider, targetProvider;

    Vector3 OriginWorld { get { return originProvider != null ? originProvider() : curOriginT != null ? curOriginT.position : curOriginP; } }
    Vector3 TargetWorld { get { return targetProvider != null ? targetProvider() : curTargetT != null ? curTargetT.position : curTargetP; } }

    // 메시를 짜는 좌표 = 이 오브젝트의 로컬 XY 평면
    Vector3 OriginPos { get { return ToPlane(OriginWorld); } }
    Vector3 TargetPos { get { return ToPlane(TargetWorld); } }

    Vector3 ToPlane(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        local.z = 0f;
        return local;
    }

    // ------------------------------------------------------------------ public API

    /// <summary>인스펙터의 Origin/Target Transform 을 사용해 발사합니다.</summary>
    public void Fire()
    {
        if (targetTransform == null)
        {
            Debug.LogWarning("[HookChainVFX] Target Transform 이 비어 있습니다. Fire(origin, target) 을 쓰거나 대상을 지정하세요.", this);
            return;
        }
        Fire(originTransform != null ? originTransform : transform, targetTransform);
    }

    /// <summary>두 Transform 을 따라다니며 발사합니다. (손 → 적)</summary>
    public void Fire(Transform origin, Transform target)
    {
        if (origin == null || target == null) return;
        curOriginT = origin;
        curTargetT = target;
        Begin();
    }

    /// <summary>고정된 두 점 사이로 발사합니다.</summary>
    public void Fire(Vector3 origin, Vector3 target)
    {
        curOriginT = null;
        curTargetT = null;
        curOriginP = origin;
        curTargetP = target;
        Begin();
    }

    /// <summary>holdUntilReleased 가 켜져 있을 때, 꽂힌 사슬을 회수하기 시작합니다.</summary>
    public void Release()
    {
        releaseRequested = true;
    }

    /// <summary>즉시 숨기고 멈춥니다.</summary>
    public void Stop()
    {
        playing = false;
        if (meshRenderer != null) meshRenderer.enabled = false;
    }

    /// <summary>i 번째 갈고리 끝의 현재 월드 위치.</summary>
    public Vector3 GetTipPosition(int index)
    {
        if (index < 0 || index >= chains.Length) return OriginWorld;
        return transform.TransformPoint(chains[index].tip);
    }

    // ------------------------------------------------------------------ lifecycle

    void Reset()
    {
        // 에디터에서 컴포넌트를 붙일 때 호출됨. 여기서 참조를 걸어두면 빌드에도 셰이더가 포함됩니다.
        chainShader = Shader.Find("VFX/HookChain");
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

        if (!playing)
        {
            if (previewLoop && mat != null)
            {
                previewTimer -= dt;
                if (previewTimer <= 0f)
                {
                    if (targetTransform != null) Fire();
                    else Fire(transform.position, transform.position + (Vector3)previewTargetOffset);
                }
            }
            return;
        }

        time += dt;

        // ---- 회수 시작 시점
        float lastArrival = (chains.Length - 1) * stagger + launchTime;
        if (retractStart < 0f && releaseRequested && time >= lastArrival) retractStart = time;
        float rt = retractStart >= 0f ? (time - retractStart) / Mathf.Max(retractTime, 0.0001f) : -1f;

        if (!hitInvoked && time >= launchTime)
        {
            hitInvoked = true;
            if (onHit != null) onHit.Invoke();
        }

        // ---- 대상 끌어오기
        bool pulling = pullTarget && curTargetT != null && endMode == EndMode.Retract;
        if (pulling && rt > 0f)
        {
            if (!pullFromSet) { pullFrom = curTargetT.position; pullFromSet = true; }
            Vector3 o = OriginWorld;
            Vector3 away = pullFrom - o;
            float dist = away.magnitude;
            Vector3 stopPos = dist > 1e-4f ? o + away / dist * Mathf.Min(pullStopDistance, dist) : pullFrom;
            float k = Mathf.Clamp01(rt);
            curTargetT.position = Vector3.Lerp(pullFrom, stopPos, k * k);
        }

        if (rt >= 1f)
        {
            Finish();
            return;
        }

        BuildMesh(rt, pulling);
        ApplyMaterial();
    }

    void Finish()
    {
        playing = false;
        meshRenderer.enabled = false;
        previewTimer = previewPause;
        if (onFinished != null) onFinished.Invoke();

        switch (onFinishedAction)
        {
            case FinishAction.Disable: gameObject.SetActive(false); break;
            case FinishAction.Destroy: Destroy(gameObject); break;
        }
    }

    // ------------------------------------------------------------------ setup

    void Init()
    {
        if (initialized) return;
        initialized = true;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (chainShader == null) chainShader = Shader.Find("VFX/HookChain");
        if (chainShader == null)
        {
            Debug.LogError("[HookChainVFX] 'VFX/HookChain' 셰이더를 찾을 수 없습니다. HookChain.shader 를 프로젝트에 넣고 Chain Shader 칸에 지정하세요.", this);
            enabled = false;
            return;
        }

        mat = new Material(chainShader);
        mat.name = "HookChain (Instance)";
        mat.hideFlags = HideFlags.DontSave;

        mesh = new Mesh();
        mesh.name = "HookChainMesh";
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

    void Begin()
    {
        Init();
        if (mat == null) return;

        int n = Mathf.Max(1, chainCount);
        if (chains.Length != n) chains = new Chain[n];

        Vector3 o = OriginPos;
        Vector3 d = TargetPos - o;
        d.z = 0f;
        Vector3 dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

        System.Random rng = seed != 0 ? new System.Random(seed) : new System.Random();

        // 발사 순서를 섞어서 가닥마다 지연을 다르게
        int[] order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }

        for (int i = 0; i < n; i++)
        {
            float f = n > 1 ? (i / (float)(n - 1)) * 2f - 1f : 0f;     // -1 .. 1
            Chain c = new Chain();
            c.offset = perp * (f * spread * 0.5f + Rand(rng, -0.12f, 0.12f))
                     + dir * Rand(rng, -depthJitter, depthJitter);
            c.phase = Rand(rng, 0f, 6.28f);
            c.flashRot = Rand(rng, 0f, 1.57f);
            c.tip = o;
            chains[i] = c;
        }
        for (int k = 0; k < n; k++) chains[order[k]].delay = k * stagger;

        time = 0f;
        hitInvoked = false;
        releaseRequested = false;
        pullFromSet = false;
        retractStart = holdUntilReleased ? -1f : (n - 1) * stagger + launchTime + holdTime;
        playing = true;

        BuildMesh(-1f, false);
        ApplyMaterial();
        meshRenderer.enabled = true;
    }

    static float Rand(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    void ApplyMaterial()
    {
        mat.SetColor(BaseColorId, baseColor);
        mat.SetColor(LightColorId, lightColor);
        mat.SetColor(OutlineColorId, outlineColor);
        mat.SetColor(GlintColorId, glintColor);
        mat.SetColor(GlowColorId, glowColor);
        mat.SetFloat(GlowId, glow);
        mat.SetFloat(AlphaId, alpha);
        mat.SetFloat(AspectId, StripAspect);
    }

    // ------------------------------------------------------------------ mesh

    void BuildMesh(float rt, bool pulling)
    {
        verts.Clear(); uv0.Clear(); uv1.Clear(); cols.Clear(); tris.Clear();

        Vector3 origin = OriginPos;
        Vector3 target = TargetPos;

        float rtc = Mathf.Clamp01(rt);

        for (int i = 0; i < chains.Length; i++)
        {
            float t = time - chains[i].delay;
            if (t <= 0f) { chains[i].tip = origin; continue; }

            Vector3 end = target + chains[i].offset;

            // ---- 발사: 빠르게 튀어나가 감속하며 도착
            float lk = Mathf.Clamp01(t / launchTime);
            float inv = 1f - lk;
            float reach = 1f - inv * inv * inv;
            float amp = waveAmplitude * inv * inv;
            float a = 1f;

            // ---- 유지: 꽂힌 직후 팽팽하게 떨림
            float holdT = t - launchTime;
            if (holdT > 0f) amp += 0.05f * Mathf.Exp(-holdT * 9f) * Mathf.Sin(holdT * 70f);

            // ---- 회수 / 소멸
            if (rt > 0f)
            {
                if (endMode == EndMode.Fade)
                {
                    a = 1f - rtc;
                }
                else if (pulling)
                {
                    float x = Mathf.Clamp01((rtc - 0.7f) / 0.3f);   // 대상에 꽂힌 채 딸려오다가 끝에서 사라짐
                    a = 1f - x * x * (3f - 2f * x);
                }
                else
                {
                    reach = 1f - rtc * rtc * rtc;
                    amp += waveAmplitude * 0.7f * rtc;
                }
            }

            Vector3 tip = Vector3.LerpUnclamped(origin, end, reach);
            chains[i].tip = tip;

            AddChain(origin, tip, amp, chains[i].phase, a);

            // ---- 적중 섬광
            float ft = (t - launchTime) / flashTime;
            if (ft >= 0f && ft < 1f && flashSize > 0f) AddFlash(end, chains[i].flashRot, ft);
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }

    void AddChain(Vector3 origin, Vector3 tip, float amp, float phase, float a)
    {
        Vector3 d = tip - origin;
        float length = d.magnitude;
        if (length < 1e-3f) return;

        Vector3 dir = d / length;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        float up = perp.y >= 0f ? 1f : -1f;              // 셰이더가 항상 화면 위쪽을 밝게 칠하도록

        float tipLen = Mathf.Min(tipLength, length);     // 막 나오는 순간에는 촉도 작게
        float chainLen = Mathf.Max(0f, length - tipLen * 0.85f);

        // ---- 사슬 띠 (uv.x 는 갈고리 쪽에서부터 센 고리 수 -> 고리가 갈고리와 함께 움직임)
        if (chainLen > 1e-3f)
        {
            float halfW = linkSize * StripAspect * 0.5f;
            int start = verts.Count;
            for (int k = 0; k <= Segments; k++)
            {
                float s = k / (float)Segments;
                float dist = s * chainLen;
                float wave = amp * Mathf.Sin(Mathf.PI * s) * Mathf.Sin(s * 9f - time * 40f + phase);
                Vector3 c = origin + dir * dist + perp * wave;
                float u = (chainLen - dist) / linkSize;
                Color col = new Color(1f, 1f, 1f, a * Mathf.Clamp01(dist / 0.4f));   // 손 쪽은 부드럽게 시작

                AddVertex(c - perp * halfW, new Vector2(u, 0f), new Vector2(0f, up), col);
                AddVertex(c + perp * halfW, new Vector2(u, 1f), new Vector2(0f, up), col);
            }
            for (int k = 0; k < Segments; k++)
            {
                int v = start + k * 2;
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
                tris.Add(v + 2); tris.Add(v + 1); tris.Add(v + 3);
            }
        }

        // ---- 갈고리 촉 (셰이더 좌표: 날 끝 = 1.0, 쿼드는 -0.05 .. 1.05 를 덮음)
        Vector3 b0 = tip - dir * (tipLen * 1.05f);
        Vector3 b1 = tip + dir * (tipLen * 0.05f);
        Vector3 w = perp * (tipLen * 0.44f);
        Color tipCol = new Color(1f, 1f, 1f, a);
        Vector2 tipMode = new Vector2(1f, up);
        int q = verts.Count;
        AddVertex(b0 - w, new Vector2(0f, 0f), tipMode, tipCol);
        AddVertex(b0 + w, new Vector2(0f, 1f), tipMode, tipCol);
        AddVertex(b1 - w, new Vector2(1f, 0f), tipMode, tipCol);
        AddVertex(b1 + w, new Vector2(1f, 1f), tipMode, tipCol);
        tris.Add(q); tris.Add(q + 1); tris.Add(q + 2);
        tris.Add(q + 2); tris.Add(q + 1); tris.Add(q + 3);
    }

    void AddFlash(Vector3 center, float rot, float progress)
    {
        float h = flashSize * 0.5f;
        Vector3 ax = new Vector3(Mathf.Cos(rot), Mathf.Sin(rot), 0f) * h;
        Vector3 ay = new Vector3(-ax.y, ax.x, 0f);
        Vector2 mode = new Vector2(2f, progress);
        Color col = Color.white;
        int q = verts.Count;
        AddVertex(center - ax - ay, new Vector2(0f, 0f), mode, col);
        AddVertex(center - ax + ay, new Vector2(0f, 1f), mode, col);
        AddVertex(center + ax - ay, new Vector2(1f, 0f), mode, col);
        AddVertex(center + ax + ay, new Vector2(1f, 1f), mode, col);
        tris.Add(q); tris.Add(q + 1); tris.Add(q + 2);
        tris.Add(q + 2); tris.Add(q + 1); tris.Add(q + 3);
    }

    void AddVertex(Vector3 local, Vector2 uv, Vector2 mode, Color col)
    {
        verts.Add(local);
        uv0.Add(uv);
        uv1.Add(mode);
        cols.Add(col);
    }

#if UNITY_EDITOR
    // 메뉴: GameObject > Effects > Hook Chain VFX
    [UnityEditor.MenuItem("GameObject/Effects/Hook Chain VFX", false, 10)]
    static void CreateFromMenu(UnityEditor.MenuCommand command)
    {
        GameObject go = new GameObject("HookChainVFX");
        HookChainVFX fx = go.AddComponent<HookChainVFX>();
        fx.previewLoop = true;               // Play 를 누르면 바로 반복 발사되도록. 실제 사용 시에는 끄세요
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Hook Chain VFX");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
