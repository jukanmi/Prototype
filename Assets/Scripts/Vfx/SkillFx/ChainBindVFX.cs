using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 사슬 속박 VFX.
/// 대상을 둘러싼 보이지 않는 구를 사슬 두 줄이 비스듬히(완만한 X자로) 감습니다.
/// 각 사슬은 구를 한 바퀴 도는 닫힌 고리 모양이라서, 카메라 쪽 절반은 대상 앞에 밝게,
/// 뒤쪽 절반은 대상 뒤에 어둡게 그려지고, 고리들이 구를 따라 돌면서 입체감을 냅니다.
/// 크게 나타났다가 곧바로 줄어들며 조여들고, 시간이 다하거나 Release() 를 부르면 고리 조각으로 끊어져 흩어집니다.
///
///   bind.Bind(enemyTransform, 3f);     // 대상을 따라다니며 3초간 속박
///   bind.Bind(worldPosition, 3f);      // 고정된 위치에
///   bind.Release();                    // 지속시간이 남았어도 지금 풀기
///
/// 지속시간에 0 이하를 넣으면 Release() 를 부를 때까지 유지합니다.
/// 사슬의 뒤쪽 절반은 대상 뒤에 그려져야 하므로 정렬 순서를 둘로 나눠 씁니다.
///
/// 이 오브젝트의 로컬 XY 평면에 그려집니다. 넘겨받은 월드 위치는 그 평면으로 (로컬 Z 를 따라) 눌러서 씁니다 —
/// 오브젝트를 카메라 쪽으로 돌려 두면 3D 공간의 점이 화면에 보이는 자리 그대로 놓입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ChainBindVFX : MonoBehaviour
{
    public enum FinishAction
    {
        None,      // 그대로 대기 (다시 Bind 해서 재사용)
        Disable,   // 비활성화 (오브젝트 풀링)
        Destroy    // 오브젝트 삭제 (Instantiate 해서 쓰는 경우)
    }

    [Header("Shader (컴포넌트를 붙이면 자동으로 채워짐)")]
    public Shader bindShader;

    [Header("사슬 모양")]
    [Tooltip("사슬이 수평에서 기울어진 각도. 두 줄이 +각도 / -각도로 교차합니다 (45 보다 작을수록 완만한 X)")]
    [Range(5f, 60f)] public float tiltAngle = 27.5f;
    [Tooltip("조여든 뒤, 사슬이 감고 있는 구의 반지름 (월드 유닛)")]
    [Min(0.5f)] public float radius = 2.5f;
    [Tooltip("사슬 고리가 카메라 쪽으로 기울어 벌어져 보이는 각도. 0 이면 직선으로 겹쳐 보이고, 클수록 앞뒤 절반이 넓게 벌어집니다")]
    [Range(0f, 40f)] public float openAngle = 14f;
    [Tooltip("처음 나타날 때의 크기 배율. 이 크기에서 줄어들며 조여듭니다")]
    [Range(1f, 3f)] public float appearScale = 1.6f;
    [Tooltip("고리 하나의 길이 (월드 유닛)")]
    [Min(0.05f)] public float linkSize = 0.50f;
    [Tooltip("대상 위치에서 구의 중심까지의 오프셋 (보통 몸통 중심)")]
    public Vector2 targetOffset = new Vector2(0f, 1.15f);

    [Header("타이밍")]
    [Tooltip("사슬이 앞쪽에서부터 구를 돌아가며 그려지는 시간")]
    [Min(0.02f)] public float appearTime = 0.10f;
    [Tooltip("크게 나온 사슬이 줄어들며 조여드는 시간")]
    [Min(0.02f)] public float tightenTime = 0.17f;
    [Tooltip("풀릴 때 끊어져 사라지는 시간")]
    [Min(0.05f)] public float releaseTime = 0.24f;

    [Header("고리의 흐름")]
    [Tooltip("묶여 있는 동안 고리가 사슬을 따라 구 둘레를 도는 속도 (초당 고리 수). 음수면 반대 방향")]
    public float scrollSpeed = 5f;
    [Tooltip("조여드는 순간의 흐름 속도 (빠르게 당겨지는 느낌)")]
    public float tightenScrollSpeed = 34f;
    [Tooltip("켜면 두 사슬이 서로 반대 방향으로 돎")]
    public bool counterScroll = false;

    [Header("부가 효과")]
    [Tooltip("사슬이 감고 있는 구를 옅은 테두리빛으로 보여 줌 (0 = 안 보임)")]
    [Range(0f, 1f)] public float sphereAlpha = 0.16f;
    [Tooltip("조여들 때 대상 쪽으로 좁혀지는 고리의 처음 지름 (0 = 없음)")]
    [Min(0f)] public float closingRingSize = 5.2f;
    [Tooltip("중심 빛무리의 지름 (0 = 없음)")]
    [Min(0f)] public float crossGlowSize = 3.0f;
    [Tooltip("묶여 있는 동안 사슬 위에서 반짝이는 빈도 (초당)")]
    [Min(0f)] public float sparkleRate = 6f;
    [Tooltip("풀릴 때 사슬 한 줄에서 튀어 나가는 고리 조각 수")]
    [Range(0, 20)] public int breakPieces = 8;

    [Header("Color")]
    [ColorUsage(false, true)] public Color coreColor = Color.white;
    [ColorUsage(false, true)] public Color midColor = new Color(0.72f, 0.45f, 1f);
    [ColorUsage(false, true)] public Color edgeColor = new Color(0.50f, 0f, 1f);          // #8000FF
    [Range(0f, 2f)] public float glow = 1f;
    [Range(0f, 1f)] public float alpha = 1f;

    [Header("Playback")]
    public bool useUnscaledTime = false;
    [Tooltip("0 이면 쓸 때마다 결이 달라지고, 다른 값이면 항상 같은 모양")]
    public int seed = 0;
    public FinishAction onFinishedAction = FinishAction.None;

    [Header("Sorting")]
    [Tooltip("사슬의 카메라 쪽 절반, 구, 섬광, 조각 (대상보다 앞)")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 100;
    [Tooltip("사슬의 뒤쪽 절반 (배경보다 앞, 대상보다 뒤가 되도록 맞추세요)")]
    public string backSortingLayerName = "Default";
    public int backSortingOrder = -1;

    [Header("Preview")]
    [Tooltip("켜두면 이 오브젝트 위치에 계속 반복 재생 (룩 조정용)")]
    public bool previewLoop = false;
    public float previewDuration = 1.5f;
    public float previewPause = 0.6f;

    [Header("Events")]
    [Tooltip("사슬이 다 조여든 순간 (속박 적용, 데미지, 사운드 등)")]
    public UnityEvent onTightened;
    [Tooltip("사슬이 끊어지기 시작하는 순간 (속박 해제)")]
    public UnityEvent onReleased;
    public UnityEvent onFinished;

    public bool IsPlaying { get { return active; } }

    /// <summary>사슬이 조여든 뒤부터 풀리기 전까지 true</summary>
    public bool IsBound { get { return active && tightenedFired && !releasedFired; } }

    // ------------------------------------------------------------------ internals

    const float StripAspect = 1.6f;      // 사슬 띠의 폭 = 고리 길이 * 1.6 (셰이더의 _Aspect 와 같아야 함)
    const int HalfSegments = 28;         // 고리의 절반(앞 또는 뒤)을 나누는 조각 수
    const float Tau = 6.2831853f;

    static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly int AspectId = Shader.PropertyToID("_Aspect");

    struct Sparkle
    {
        public Vector3 pos;
        public float size, start, life, rot;
    }

    struct Piece     // 끊어져 날아가는 고리 조각
    {
        public Vector3 pos, vel;
        public float rot, angVel, size, start, life;
    }

    // 메시 하나를 채우는 버퍼 묶음 (앞쪽용 / 뒤쪽용)
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
    MeshFilter backFilter;
    MeshRenderer backRenderer;
    Mesh mesh, backMesh;
    Material mat;
    bool initialized;
    System.Random rng;

    readonly List<Sparkle> sparkles = new List<Sparkle>();
    readonly List<Piece> pieces = new List<Piece>();
    readonly Buffers front = new Buffers();
    readonly Buffers back = new Buffers();

    float clock;
    bool active;
    Transform targetT;
    Vector3 targetPos;              // 로컬 평면
    float startTime;
    float releaseStart = -1f;       // 끊어지기 시작하는 시각 (정해지지 않았으면 음수)
    float scroll;                   // 고리가 흘러간 누적 거리 (고리 단위)
    float sparkleAcc;
    bool tightenedFired, releasedFired;
    float previewTimer;

    Vector3 Center { get { return targetPos + (Vector3)targetOffset; } }

    // ------------------------------------------------------------------ public API

    /// <summary>대상을 따라다니며 duration 초 동안 묶습니다. (0 이하면 Release() 를 부를 때까지)</summary>
    public void Bind(Transform target, float duration)
    {
        if (target == null) return;
        targetT = target;
        targetPos = ToPlane(target.position);
        Begin(duration);
    }

    /// <summary>고정된 위치(대상의 발밑 기준, targetOffset 이 더해짐)에 duration 초 동안 재생합니다.</summary>
    public void Bind(Vector3 worldPosition, float duration)
    {
        targetT = null;
        targetPos = ToPlane(worldPosition);
        Begin(duration);
    }

    /// <summary>지금 사슬을 끊습니다.</summary>
    public void Release()
    {
        if (!active || releasedFired) return;
        if (releaseStart < 0f || releaseStart > clock) releaseStart = clock;
    }

    /// <summary>즉시 숨기고 멈춥니다.</summary>
    public void Stop()
    {
        sparkles.Clear();
        pieces.Clear();
        active = false;
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (backRenderer != null) backRenderer.enabled = false;
    }

    // ------------------------------------------------------------------ lifecycle

    void Reset()
    {
        // 에디터에서 컴포넌트를 붙일 때 호출됨. 여기서 참조를 걸어두면 빌드에도 셰이더가 포함됩니다.
        bindShader = Shader.Find("VFX/ChainBind");
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

        if (!active)
        {
            if (previewLoop && mat != null)
            {
                previewTimer -= dt;
                if (previewTimer <= 0f) Bind(transform.position, previewDuration);
            }
            return;
        }

        clock += dt;
        if (targetT != null) targetPos = ToPlane(targetT.position);       // 대상이 사라지면 마지막 위치에 남음

        float t = clock - startTime;
        float holdStart = appearTime + tightenTime;

        // ---- 다 조여든 순간
        if (!tightenedFired && t >= holdStart)
        {
            tightenedFired = true;
            AddSparkle(Center, 3.0f, clock, 0.20f, 0f);
            AddSparkle(Center, 1.8f, clock + 0.03f, 0.18f, 0.785f);
            if (onTightened != null) onTightened.Invoke();
            if (!active) return;                         // 이벤트 안에서 Stop() 을 부른 경우
        }

        // ---- 끊어지기 시작하는 순간
        if (!releasedFired && releaseStart >= 0f && clock >= releaseStart)
        {
            releasedFired = true;
            SpawnBreak();
            if (onReleased != null) onReleased.Invoke();
            if (!active) return;
        }

        float curRadius, flash, dissolve, wipe, speed;
        bool chainsVisible = ChainState(t, out curRadius, out flash, out dissolve, out wipe, out speed);
        scroll += speed * dt;

        // ---- 묶여 있는 동안 앞쪽 사슬 위에서 이따금 반짝임
        if (tightenedFired && !releasedFired && sparkleRate > 0f)
        {
            sparkleAcc += dt * sparkleRate;
            while (sparkleAcc >= 1f)
            {
                sparkleAcc -= 1f;
                Vector3 pos = LoopPoint(rng.Next(2), Rand(0.3f, 2.84f), radius) + new Vector3(Rand(-0.2f, 0.2f), Rand(-0.2f, 0.2f), 0f);
                AddSparkle(pos, Rand(0.3f, 0.6f), clock, Rand(0.25f, 0.45f), 0f);
            }
        }

        // ---- 수명이 끝난 요소 정리
        for (int i = sparkles.Count - 1; i >= 0; i--)
            if (clock >= sparkles[i].start + sparkles[i].life) sparkles.RemoveAt(i);
        for (int i = pieces.Count - 1; i >= 0; i--)
            if (clock >= pieces[i].start + pieces[i].life) pieces.RemoveAt(i);

        if (!chainsVisible && sparkles.Count == 0 && pieces.Count == 0)
        {
            Finish();
            return;
        }

        BuildMeshes(t, chainsVisible, curRadius, flash, dissolve, wipe);
        ApplyMaterial();
    }

    void Finish()
    {
        active = false;
        meshRenderer.enabled = false;
        backRenderer.enabled = false;
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

        if (bindShader == null) bindShader = Shader.Find("VFX/ChainBind");
        if (bindShader == null)
        {
            Debug.LogError("[ChainBindVFX] 'VFX/ChainBind' 셰이더를 찾을 수 없습니다. ChainBind.shader 를 프로젝트에 넣고 Bind Shader 칸에 지정하세요.", this);
            enabled = false;
            return;
        }

        mat = new Material(bindShader);
        mat.name = "ChainBind (Instance)";
        mat.hideFlags = HideFlags.DontSave;

        mesh = NewMesh("ChainBindFront");
        meshFilter.sharedMesh = mesh;
        SetupRenderer(meshRenderer, sortingLayerName, sortingOrder);

        // 사슬의 뒤쪽 절반은 대상 뒤에 그려져야 해서 정렬 순서가 다른 별도의 렌더러로 그림
        GameObject backGo = new GameObject("ChainBindBack");
        backGo.transform.SetParent(transform, false);
        backFilter = backGo.AddComponent<MeshFilter>();
        backRenderer = backGo.AddComponent<MeshRenderer>();
        backMesh = NewMesh("ChainBindBack");
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

    void Begin(float duration)
    {
        Init();
        if (mat == null) return;

        rng = seed != 0 ? new System.Random(seed) : new System.Random();
        sparkles.Clear();
        pieces.Clear();

        startTime = clock;
        releaseStart = duration > 0f ? startTime + appearTime + tightenTime + duration : -1f;
        scroll = Rand(0f, 2f);
        sparkleAcc = 0f;
        tightenedFired = false;
        releasedFired = false;
        active = true;

        mesh.Clear();
        backMesh.Clear();
        meshRenderer.enabled = true;
        backRenderer.enabled = true;
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

    void ApplyMaterial()
    {
        mat.SetColor(CoreColorId, coreColor);
        mat.SetColor(MidColorId, midColor);
        mat.SetColor(EdgeColorId, edgeColor);
        mat.SetFloat(GlowId, glow);
        mat.SetFloat(AlphaId, alpha);
        mat.SetFloat(AspectId, StripAspect);
    }

    void AddSparkle(Vector3 pos, float size, float start, float life, float rot)
    {
        Sparkle s = new Sparkle();
        s.pos = pos; s.size = size; s.start = start; s.life = life; s.rot = rot;
        sparkles.Add(s);
    }

    // ------------------------------------------------------------------ geometry

    /// <summary>
    /// index 번째 사슬 고리의 축. dir = 화면에서 고리가 길게 뻗은 방향, up = 그에 수직이며 화면 위쪽을 향하는 방향.
    /// (index 0 = 오른쪽 위로 올라가는 사슬, 1 = 오른쪽 아래로 내려가는 사슬)
    /// </summary>
    void LoopBasis(int index, out Vector3 dir, out Vector3 up)
    {
        float a = tiltAngle * Mathf.Deg2Rad;
        dir = new Vector3(Mathf.Cos(a), index == 0 ? Mathf.Sin(a) : -Mathf.Sin(a), 0f);
        up = new Vector3(-dir.y, dir.x, 0f);
        if (up.y < 0f) up = -up;
    }

    /// <summary>
    /// 사슬 고리 위의 한 점. theta 0..π 는 카메라 쪽 절반(축보다 아래로 처짐), π..2π 는 뒤쪽 절반(축보다 위).
    /// 구를 도는 원을 비스듬히 본 모양이라 화면에서는 납작한 타원이 됩니다.
    /// </summary>
    Vector3 LoopPoint(int index, float theta, float loopRadius)
    {
        Vector3 dir, up;
        LoopBasis(index, out dir, out up);
        float open = Mathf.Sin(openAngle * Mathf.Deg2Rad);
        return Center + (dir * Mathf.Cos(theta) - up * (open * Mathf.Sin(theta))) * loopRadius;
    }

    /// <summary>풀리는 순간: 섬광 + 사슬을 따라 고리 조각이 바깥으로 튀어 나감</summary>
    void SpawnBreak()
    {
        Vector3 c = Center;
        AddSparkle(c, 2.6f, clock, 0.20f, 0.4f);

        for (int ci = 0; ci < 2; ci++)
        {
            Vector3 dir, up;
            LoopBasis(ci, out dir, out up);
            float rot = Mathf.Atan2(dir.y, dir.x);
            for (int k = 0; k < breakPieces; k++)
            {
                Piece p = new Piece();
                p.pos = LoopPoint(ci, Rand(0f, Tau), radius);
                Vector3 outward = p.pos - c;
                outward = outward.sqrMagnitude > 1e-8f ? outward.normalized : Vector3.up;
                p.vel = outward * Rand(1.5f, 5f) + new Vector3(Rand(-1f, 1f), Rand(-0.5f, 2.5f), 0f);
                p.rot = rot;
                p.angVel = Rand(-9f, 9f);
                p.size = linkSize * 1.5f;
                p.start = clock + Rand(0f, 0.08f);
                p.life = Rand(0.3f, 0.55f);
                pieces.Add(p);
            }
        }
    }

    // ------------------------------------------------------------------ timeline

    /// <summary>
    /// 사슬의 현재 상태. 끊어져 다 사라졌으면 false.
    /// curRadius = 지금 감고 있는 구의 반지름, flash = 하얗게 달아오른 정도, dissolve = 끊어진 정도,
    /// wipe = 그려진 정도, speed = 고리가 도는 속도
    /// </summary>
    bool ChainState(float t, out float curRadius, out float flash, out float dissolve, out float wipe, out float speed)
    {
        float big = radius * appearScale;
        float fastSpeed = tightenScrollSpeed * (scrollSpeed < 0f ? -1f : 1f);
        curRadius = radius; flash = 0f; dissolve = 0f; wipe = 1f; speed = scrollSpeed;

        // 4) 끊어짐: 살짝 느슨해지며 고리가 하나씩 떨어져 나감
        if (releasedFired)
        {
            float x = (clock - releaseStart) / releaseTime;
            if (x >= 1f) return false;
            curRadius = radius * (1f + 0.10f * x);
            flash = 1f - SStep(0f, 0.5f, x) * 0.6f;
            dissolve = SStep(0.05f, 1f, x);
            return true;
        }

        // 1) 등장: 크게, 앞쪽 가운데에서부터 구를 돌아가며 그려짐 (하얗게 달아오른 상태)
        if (t < appearTime)
        {
            float inv = 1f - Mathf.Clamp01(t / appearTime);
            curRadius = big;
            flash = 1f;
            wipe = 1f - inv * inv;
            speed = fastSpeed;
            return true;
        }

        // 2) 조임: 큰 고리가 줄어들며 살짝 지나쳤다가 제자리로 (팽팽하게 당겨지는 느낌)
        float holdStart = appearTime + tightenTime;
        if (t < holdStart)
        {
            float x = (t - appearTime) / tightenTime;
            float m = x - 1f;
            float backEase = 1f + 2.4f * m * m * m + 1.4f * m * m;
            curRadius = Mathf.LerpUnclamped(big, radius, backEase);
            flash = 1f - SStep(0.2f, 1f, x);
            speed = Mathf.Lerp(fastSpeed, scrollSpeed, SStep(0.3f, 1f, x));
            return true;
        }

        // 3) 유지: 고리가 일정한 속도로 구 둘레를 돎
        flash = 0.25f * Mathf.Exp(-(t - holdStart) * 9f);
        return true;
    }

    // ------------------------------------------------------------------ mesh

    void BuildMeshes(float t, bool chainsVisible, float curRadius, float flash, float dissolve, float wipe)
    {
        front.Clear();
        back.Clear();
        Vector4 none = Vector4.zero;
        Vector3 c = Center;

        if (chainsVisible)
        {
            // ---- 사슬이 감고 있는 구 (옅은 테두리빛)
            if (sphereAlpha > 0f)
            {
                float sa = sphereAlpha * wipe * (1f - dissolve) * (1f + flash);
                AddQuad(front, c, new Vector3(curRadius, 0f, 0f), new Vector3(0f, curRadius, 0f), new Vector4(5f, sa, 0f, 0f), none);
            }

            // ---- 조여드는 고리: 사슬이 나온 직후부터 다 조여들 때까지
            float ringStart = appearTime * 0.5f;
            float ringEnd = appearTime + tightenTime + 0.02f;
            if (closingRingSize > 0f && !releasedFired && t > ringStart && t < ringEnd)
            {
                float h = closingRingSize * 0.5f;
                AddQuad(front, c, new Vector3(h, 0f, 0f), new Vector3(0f, h, 0f),
                        new Vector4(3f, (t - ringStart) / (ringEnd - ringStart), 0f, 0f), none);
            }

            // ---- 중심의 빛무리 (천천히 숨쉬듯 밝아졌다 어두워짐)
            if (crossGlowSize > 0f)
            {
                float h = crossGlowSize * 0.5f;
                float ga = (0.22f + 0.06f * Mathf.Sin(clock * 7f) + flash * 0.9f) * (1f - dissolve);
                AddQuad(front, c, new Vector3(h, 0f, 0f), new Vector3(0f, h, 0f), new Vector4(1f, ga, 0f, 0f), none);
            }

            // ---- 사슬 두 줄. 한 바퀴의 고리 수는 짝수로 맞춰서 무늬가 이음매 없이 이어지게 함
            int links = 2 * Mathf.Max(2, Mathf.RoundToInt(Tau * radius / (2f * linkSize)));
            float linkLength = Tau * curRadius / links;
            float halfWidth = linkLength * StripAspect * 0.5f;
            Vector4 prm = new Vector4(links, dissolve, wipe, 0f);

            for (int ci = 0; ci < 2; ci++)
            {
                float flow = (ci == 1 && counterScroll ? -scroll : scroll) + ci * 0.5f;
                AddLoopHalf(front, ci, 0f, curRadius, halfWidth, links, flow, flash, prm);          // 카메라 쪽 절반
                AddLoopHalf(back, ci, Mathf.PI, curRadius, halfWidth, links, flow, flash, prm);     // 뒤쪽 절반
            }
        }

        // ---- 끊어진 고리 조각: 튀어 나가다 떨어지며 사라짐
        for (int i = 0; i < pieces.Count; i++)
        {
            Piece p = pieces[i];
            float age = clock - p.start;
            if (age <= 0f || age >= p.life) continue;

            float k = age / p.life;
            Vector3 pos = p.pos + p.vel * ((1f - Mathf.Exp(-3f * age)) / 3f) + new Vector3(0f, -4f * age * age, 0f);
            float rot = p.rot + p.angVel * age;
            Vector3 ax = new Vector3(Mathf.Cos(rot), Mathf.Sin(rot), 0f) * (p.size * 0.5f);
            AddQuad(front, pos, ax, new Vector3(-ax.y, ax.x, 0f) * 0.62f, new Vector4(4f, 1f - k * k, 0f, 0f), none);
        }

        // ---- 섬광 / 반짝임
        for (int i = 0; i < sparkles.Count; i++)
        {
            Sparkle sp = sparkles[i];
            float st = (clock - sp.start) / sp.life;
            if (st <= 0f || st >= 1f) continue;

            Vector3 ax = new Vector3(Mathf.Cos(sp.rot), Mathf.Sin(sp.rot), 0f) * (sp.size * 0.5f);
            AddQuad(front, sp.pos, ax, new Vector3(-ax.y, ax.x, 0f), new Vector4(2f, st, 0f, 0f), none);
        }

        front.Apply(mesh);
        back.Apply(backMesh);
    }

    /// <summary>
    /// 사슬 고리의 절반(thetaStart 부터 반 바퀴)을 띠로 만듭니다.
    /// 띠의 폭 방향은 항상 고리 축에 수직이라서, 구의 옆구리를 돌아갈 때 고리가 자연스럽게 눌려 보입니다.
    /// uv.x 는 구 둘레를 따라 잰 고리 단위 위치 + 흐름: 메시는 그대로인데 고리 무늬만 사슬을 따라 돕니다.
    /// </summary>
    void AddLoopHalf(Buffers b, int index, float thetaStart, float loopRadius, float halfWidth, int links, float flow, float flash, Vector4 prm)
    {
        Vector3 dir, up;
        LoopBasis(index, out dir, out up);
        Vector3 c = Center;
        float open = Mathf.Sin(openAngle * Mathf.Deg2Rad);

        int start = b.verts.Count;
        for (int k = 0; k <= HalfSegments; k++)
        {
            float theta = thetaStart + Mathf.PI * k / HalfSegments;
            float cs = Mathf.Cos(theta), sn = Mathf.Sin(theta);      // sn = 깊이: +1 카메라 쪽, -1 뒤쪽
            Vector3 center = c + (dir * cs - up * (open * sn)) * loopRadius;
            Vector3 w = up * (halfWidth * (1f + 0.15f * sn));         // 가까운 쪽 고리가 조금 더 크게
            float a01 = theta / Tau;
            float u = a01 * links + flow;

            Vector4 mode = new Vector4(0f, a01, sn, flash);
            AddVertex(b, center - w, new Vector2(u, 0f), mode, prm);
            AddVertex(b, center + w, new Vector2(u, 1f), mode, prm);
        }
        for (int k = 0; k < HalfSegments; k++)
        {
            int q = start + k * 2;
            b.tris.Add(q); b.tris.Add(q + 1); b.tris.Add(q + 2);
            b.tris.Add(q + 2); b.tris.Add(q + 1); b.tris.Add(q + 3);
        }
    }

    // 중심과 두 반축으로 사각형 하나. uv.x 는 ax 방향, uv.y 는 ay 방향
    void AddQuad(Buffers b, Vector3 center, Vector3 ax, Vector3 ay, Vector4 mode, Vector4 prm)
    {
        int q = b.verts.Count;
        AddVertex(b, center - ax - ay, new Vector2(0f, 0f), mode, prm);
        AddVertex(b, center - ax + ay, new Vector2(0f, 1f), mode, prm);
        AddVertex(b, center + ax - ay, new Vector2(1f, 0f), mode, prm);
        AddVertex(b, center + ax + ay, new Vector2(1f, 1f), mode, prm);
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
    // 메뉴: GameObject > Effects > Chain Bind VFX (사슬 속박)
    [UnityEditor.MenuItem("GameObject/Effects/Chain Bind VFX", false, 10)]
    static void CreateFromMenu(UnityEditor.MenuCommand command)
    {
        GameObject go = new GameObject("ChainBindVFX");
        ChainBindVFX fx = go.AddComponent<ChainBindVFX>();
        fx.previewLoop = true;               // Play 를 누르면 바로 반복 재생되도록. 실제 사용 시에는 끄세요
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Chain Bind VFX");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
