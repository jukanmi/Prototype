using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 마나 스피어 VFX (벨트스크롤용).
/// 긴 직선 범위를 꿰뚫는 마력창이 레이저처럼 전체 길이로 한 번에 나타났다가 가늘어지며 사라집니다.
/// 발사하는 순간 시전자 쪽에는 후폭풍이 입니다:
///   - 마력창이 나가는 지점을 맨 앞 꼭짓점으로 하는 입체적인 반구. 앞뒤보다 위아래가 긴 완만한 곡면이라 시전자를 감쌉니다.
///     발사 순간 나타나 그 크기 그대로 서서히 사라집니다
///   - 반구가 바닥에 닿는 자리의 반쪽 고리
///   - 바닥을 따라 시전자 양옆을 지나 뒤로 벌어지는 세모꼴 바람 (꼭짓점이 시전자 앞)
///   - 반동으로 일어나 바닥을 따라 뒤로 밀려 나가는 연기
///
///   spear.Fire(muzzlePosition, facingRight ? Vector3.right : Vector3.left, 11.5f);   // 위치, 방향, 길이
///   spear.FireTo(muzzlePosition, endPosition);                                       // 시작점과 끝점
///   spear.FireForward(11.5f);                                                        // 이 오브젝트(시전자 발밑) 기준으로 바라보는 방향 (애니메이션 이벤트용)
///   spear.Contains(enemyPosition, 0.6f);                                             // 그 위치가 방금 쏜 직선 범위 안인지 (데미지 판정용)
///
/// 반구는 발사 지점(Fire 에 넘긴 위치)에 맞춰지고, 바닥의 위치와 연기는 발사 지점이 시전자 발밑에서
/// Muzzle Offset 만큼(앞으로 x, 위로 y) 떨어져 있다고 보고 자리를 잡습니다.
/// 이펙트는 발사한 순간의 월드 좌표에 고정되고, 바닥 효과의 먼 쪽 절반은 캐릭터 뒤에 그려지도록 정렬 순서를 둘로 나눠 씁니다.
///
/// 이 오브젝트의 로컬 XY 평면에 그려집니다. 넘겨받은 월드 위치는 그 평면으로 (로컬 Z 를 따라) 눌러서 씁니다 —
/// 오브젝트를 카메라 쪽으로 돌려 두면 3D 공간의 점이 화면에 보이는 자리 그대로 놓입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ManaSpearVFX : MonoBehaviour
{
    public enum FinishAction
    {
        None,      // 그대로 대기 (다시 Fire 해서 재사용)
        Disable,   // 비활성화 (오브젝트 풀링)
        Destroy    // 오브젝트 삭제 (Instantiate 해서 쓰는 경우)
    }

    [Header("Shader (컴포넌트를 붙이면 자동으로 채워짐)")]
    public Shader spearShader;

    [Header("마력창")]
    [Tooltip("자루의 굵기 (월드 유닛). 창날은 이보다 약 1.7배 넓습니다")]
    [Min(0.05f)] public float beamWidth = 0.55f;
    [Tooltip("끝의 창날 부분 길이 (월드 유닛)")]
    [Min(0.1f)] public float headLength = 1.6f;
    [Tooltip("나타난 뒤 온전한 굵기로 머무는 시간")]
    [Min(0f)] public float holdTime = 0.07f;
    [Tooltip("가늘어지며 사라지는 시간")]
    [Min(0.03f)] public float fadeTime = 0.30f;
    [Tooltip("자루 양옆을 따라 깜빡이는 가는 선의 세기")]
    [Range(0f, 1f)] public float sideLines = 1f;
    [Tooltip("사라질 때 경로에 남아 흩날리는 불티의 양 (길이 1 유닛당)")]
    [Min(0f)] public float motesPerUnit = 1.3f;

    [Header("발사 지점")]
    [Tooltip("시전자 발밑에서 발사 지점까지: x = 바라보는 방향으로 앞쪽, y = 바닥에서의 높이. 바닥의 위치와 연기가 이는 자리를 정하는 데 쓰입니다")]
    public Vector2 muzzleOffset = new Vector2(0.6f, 1.0f);
    [Tooltip("바닥이 화면에서 세로로 눌려 보이는 비율. 카메라가 바닥을 비스듬히 내려다볼수록 작게")]
    [Range(0.1f, 1f)] public float groundTilt = 0.42f;

    [Header("후폭풍 - 반구")]
    [Tooltip("반구의 위아래 반지름 (월드 유닛). 발사 지점 높이에서 위아래로 이만큼 펼쳐집니다. 0 이면 후폭풍 전체가 없음")]
    [Min(0f)] public float blastRadius = 1.65f;
    [Tooltip("위아래 반지름에 대한 앞뒤 깊이의 비율. 1 = 둥근 반구, 1 보다 작으면 세로로 긴 완만한 곡면, 크면 탄두처럼 앞으로 뾰족")]
    [Range(0.3f, 3f)] public float blastElongation = 0.7f;
    [Tooltip("발사 방향을 중심으로 벌어지는 각도의 절반. 90 이면 정확히 반구")]
    [Range(20f, 170f)] public float blastHalfAngle = 90f;
    [Tooltip("반구와 바닥 고리가 나타난 뒤 완전히 사라질 때까지의 시간")]
    [Min(0.05f)] public float blastTime = 0.45f;
    [Tooltip("반구가 바닥에 닿는 자리에 깔리는 반쪽 고리")]
    public bool floorRing = true;
    [Tooltip("앞쪽으로 부채꼴로 튀는 불티 수")]
    [Range(0, 40)] public int blastSparks = 12;

    [Header("후폭풍 - 바닥 바람")]
    [Tooltip("바닥을 쓸고 뒤로 지나가는 바람의 길이 (월드 유닛). 0 이면 없음")]
    [Min(0f)] public float windLength = 3.6f;
    [Tooltip("세모꼴이 벌어진 각도의 절반 (바닥 기준, 도)")]
    [Range(5f, 70f)] public float windHalfAngle = 30f;
    [Min(0.05f)] public float windTime = 0.40f;

    [Header("후폭풍 - 연기")]
    [Tooltip("연기의 양 (0 = 없음)")]
    [Range(0f, 3f)] public float smokeAmount = 1f;
    public Color smokeColor = new Color(0.74f, 0.70f, 0.86f);
    [Tooltip("연기 덩이의 크기 배율")]
    [Min(0.1f)] public float smokeSize = 1f;

    [Header("Color")]
    [ColorUsage(false, true)] public Color coreColor = Color.white;
    [ColorUsage(false, true)] public Color midColor = new Color(0.72f, 0.45f, 1f);
    [ColorUsage(false, true)] public Color edgeColor = new Color(0.50f, 0f, 1f);          // #8000FF
    [Range(0f, 2f)] public float glow = 1f;
    [Range(0f, 1f)] public float alpha = 1f;

    [Header("Playback")]
    public bool useUnscaledTime = false;
    [Tooltip("0 이면 쏠 때마다 결이 달라지고, 다른 값이면 항상 같은 모양")]
    public int seed = 0;
    public FinishAction onFinishedAction = FinishAction.None;

    [Header("Sorting")]
    [Tooltip("마력창, 반구, 바닥 효과의 가까운 쪽 절반, 불티 (캐릭터보다 앞)")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 100;
    [Tooltip("바닥 효과(고리, 바람)의 먼 쪽 절반 (배경보다 앞, 캐릭터보다 뒤가 되도록 맞추세요)")]
    public string backSortingLayerName = "Default";
    public int backSortingOrder = -1;

    [Header("Preview")]
    [Tooltip("켜두면 계속 반복 발사 (룩 조정용)")]
    public bool previewLoop = false;
    public float previewLength = 11.5f;
    public float previewPause = 0.7f;

    [Header("Events")]
    [Tooltip("발사한 순간 (전체 범위에 즉시 닿으므로 데미지도 이때)")]
    public UnityEvent onFire;
    public UnityEvent onFinished;

    public bool IsPlaying { get { return active; } }

    // ------------------------------------------------------------------ internals

    const float BodyK = 0.62f * 0.58f;   // 쿼드 반높이에 대한 자루 반폭의 비율 (셰이더의 수치와 같아야 함)
    const float RingK = 0.92f;           // 쿼드 반크기에 대한 반구/고리의 반지름 비율 (셰이더의 수치와 같아야 함)

    static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int LinesId = Shader.PropertyToID("_Lines");
    static readonly int SmokeColorId = Shader.PropertyToID("_SmokeColor");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    struct Beam
    {
        public Vector3 origin, dir;
        public float length, start, seed;
    }

    struct Blast     // 후폭풍 한 번 (반구 + 바닥 고리 + 바닥 바람)
    {
        public Vector3 center;      // 반구의 중심 (발사 지점에서 반구의 깊이만큼 뒤)
        public float casterX;       // 시전자의 발밑 x
        public float groundY, sign, start, seed;
    }

    struct Puff      // 연기 한 덩이
    {
        public Vector3 pos, vel;
        public float size, start, life, seed;
        public bool behind;         // true = 바닥의 먼 쪽이라 캐릭터 뒤에 그림
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

    struct Glow
    {
        public Vector3 pos;
        public float size, start, life;
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

    readonly List<Beam> beams = new List<Beam>();
    readonly List<Blast> blasts = new List<Blast>();
    readonly List<Puff> puffs = new List<Puff>();
    readonly List<Sparkle> sparkles = new List<Sparkle>();
    readonly List<Needle> needles = new List<Needle>();
    readonly List<Glow> glows = new List<Glow>();
    readonly Buffers front = new Buffers();
    readonly Buffers back = new Buffers();

    float clock;
    bool active;
    Vector3 lastOrigin, lastDir;    // 로컬 평면
    float lastLength;
    float previewTimer;

    // ------------------------------------------------------------------ public API

    /// <summary>origin 에서 direction 방향으로 length 길이의 마력창을 쏩니다.</summary>
    public void Fire(Vector3 origin, Vector3 direction, float length)
    {
        Init();
        if (mat == null) return;

        // 월드 -> 로컬 평면. 깊이 방향 성분은 화면에서 눌려 보이므로 길이도 그만큼 줄여 끝점을 맞춤
        if (direction.sqrMagnitude < 1e-8f) return;
        Vector3 worldOrigin = origin;
        origin = ToPlane(worldOrigin);
        Vector3 planeDir = ToPlane(worldOrigin + direction.normalized) - origin;
        length *= planeDir.magnitude;
        direction = planeDir;

        direction.z = 0f;
        if (direction.sqrMagnitude < 1e-8f || length <= 0.01f) return;
        Vector3 dir = direction.normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

        if (rng == null || !active) rng = seed != 0 ? new System.Random(seed) : new System.Random();
        float now = clock;

        lastOrigin = origin;
        lastDir = dir;
        lastLength = length;

        // ---- 마력창
        Beam b = new Beam();
        b.origin = origin; b.dir = dir; b.length = length; b.start = now; b.seed = Rand(0f, 10f);
        beams.Add(b);

        // ---- 후폭풍: 바닥과 나란하게, 좌우 중 쏜 쪽을 향함
        if (blastRadius > 0f)
        {
            float sign = dir.x >= 0f ? 1f : -1f;
            Blast bl = new Blast();
            // 반구의 맨 앞 꼭짓점이 발사 지점에 오도록, 중심을 반구의 깊이만큼 뒤로 둠
            float depth = blastRadius * Mathf.Max(blastElongation, 0.05f);
            bl.center = new Vector3(origin.x - sign * depth, origin.y, origin.z);
            bl.casterX = origin.x - sign * muzzleOffset.x;
            bl.groundY = origin.y - muzzleOffset.y;
            bl.sign = sign;
            bl.start = now;
            bl.seed = Rand(0f, 10f);
            blasts.Add(bl);

            SpawnSmoke(bl, origin, now);

            float half = blastHalfAngle * Mathf.Deg2Rad;
            for (int i = 0; i < blastSparks; i++)
            {
                float ang = Rand(-half, half) * 0.9f;
                Vector3 dv = dir * Mathf.Cos(ang) + perp * Mathf.Sin(ang);
                AddNeedle(origin + dv * 0.3f, dv * Rand(5f, 13f), Rand(0.4f, 0.9f), now + Rand(0f, 0.04f), Rand(0.18f, 0.34f));
            }
        }

        // ---- 발사 지점과 창끝의 섬광
        AddSparkle(origin + dir * 0.2f, 2.6f, now, 0.18f, Rand(0f, 1.57f));
        AddSparkle(origin + dir * length, 1.3f, now, 0.16f, 0f);
        Glow g = new Glow();
        g.pos = origin + dir * 0.2f; g.size = 3.2f; g.start = now; g.life = 0.25f;
        glows.Add(g);

        // ---- 사라질 때 경로에 남아 흩날리는 불티와 반짝임
        int motes = Mathf.Min(Mathf.RoundToInt(length * motesPerUnit), 200);
        for (int i = 0; i < motes; i++)
        {
            Vector3 pos = origin + dir * (length * Rand(0.03f, 0.97f)) + perp * Rand(-0.28f, 0.28f);
            AddNeedle(pos, dir * Rand(0.5f, 3f) + perp * Rand(-0.6f, 0.6f), Rand(0.25f, 0.55f),
                      now + holdTime + Rand(0f, fadeTime * 0.6f), Rand(0.25f, 0.5f));
        }
        int twinkles = Mathf.Min(Mathf.RoundToInt(length * motesPerUnit * 0.4f), 80);
        for (int i = 0; i < twinkles; i++)
        {
            Vector3 pos = origin + dir * (length * Rand(0.03f, 0.97f)) + perp * Rand(-0.35f, 0.35f);
            AddSparkle(pos, Rand(0.25f, 0.5f), now + Rand(0.05f, 0.35f), Rand(0.18f, 0.35f), 0f);
        }

        if (!active)
        {
            active = true;
            meshRenderer.enabled = true;
            backRenderer.enabled = true;
        }
        BuildMeshes();
        ApplyMaterial();

        if (onFire != null) onFire.Invoke();
    }

    /// <summary>from 에서 to 까지 꿰뚫는 마력창을 쏩니다.</summary>
    public void FireTo(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        Fire(from, d, d.magnitude);
    }

    /// <summary>이 오브젝트(시전자 발밑) + muzzleOffset 에서, 바라보는 방향으로 length 길이를 쏩니다. (애니메이션 이벤트용)</summary>
    public void FireForward(float length)
    {
        float sign = transform.lossyScale.x >= 0f ? 1f : -1f;      // 캐릭터를 scale.x 로 뒤집는 경우를 따라감
        Vector3 forward = transform.right * sign;
        Vector3 origin = transform.position + forward * muzzleOffset.x + transform.up * muzzleOffset.y;
        Fire(origin, forward, length);
    }

    /// <summary>worldPosition 이 가장 최근에 쏜 직선 범위(중심선에서 halfWidth 이내) 안에 있는지.</summary>
    public bool Contains(Vector3 worldPosition, float halfWidth)
    {
        if (lastLength <= 0f) return false;
        Vector3 d = ToPlane(worldPosition) - lastOrigin;
        float along = d.x * lastDir.x + d.y * lastDir.y;
        float across = -d.x * lastDir.y + d.y * lastDir.x;
        return along >= 0f && along <= lastLength && Mathf.Abs(across) <= halfWidth;
    }

    /// <summary>즉시 숨기고 멈춥니다.</summary>
    public void Stop()
    {
        beams.Clear(); blasts.Clear(); puffs.Clear(); sparkles.Clear(); needles.Clear(); glows.Clear();
        active = false;
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (backRenderer != null) backRenderer.enabled = false;
    }

    // ------------------------------------------------------------------ lifecycle

    void Reset()
    {
        // 에디터에서 컴포넌트를 붙일 때 호출됨. 여기서 참조를 걸어두면 빌드에도 셰이더가 포함됩니다.
        spearShader = Shader.Find("VFX/ManaSpear");
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
                if (previewTimer <= 0f) FireForward(previewLength);
            }
            return;
        }

        clock += dt;

        // ---- 수명이 끝난 요소 정리
        float beamLife = holdTime + fadeTime;
        float blastLife = Mathf.Max(blastTime, windLength > 0f ? windTime : 0f);
        for (int i = beams.Count - 1; i >= 0; i--)
            if (clock >= beams[i].start + beamLife) beams.RemoveAt(i);
        for (int i = blasts.Count - 1; i >= 0; i--)
            if (clock >= blasts[i].start + blastLife) blasts.RemoveAt(i);
        for (int i = puffs.Count - 1; i >= 0; i--)
            if (clock >= puffs[i].start + puffs[i].life) puffs.RemoveAt(i);
        for (int i = sparkles.Count - 1; i >= 0; i--)
            if (clock >= sparkles[i].start + sparkles[i].life) sparkles.RemoveAt(i);
        for (int i = needles.Count - 1; i >= 0; i--)
            if (clock >= needles[i].start + needles[i].life) needles.RemoveAt(i);
        for (int i = glows.Count - 1; i >= 0; i--)
            if (clock >= glows[i].start + glows[i].life) glows.RemoveAt(i);

        if (beams.Count == 0 && blasts.Count == 0 && puffs.Count == 0 && sparkles.Count == 0 && needles.Count == 0 && glows.Count == 0)
        {
            Finish();
            return;
        }

        BuildMeshes();
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

        if (spearShader == null) spearShader = Shader.Find("VFX/ManaSpear");
        if (spearShader == null)
        {
            Debug.LogError("[ManaSpearVFX] 'VFX/ManaSpear' 셰이더를 찾을 수 없습니다. ManaSpear.shader 를 프로젝트에 넣고 Spear Shader 칸에 지정하세요.", this);
            enabled = false;
            return;
        }

        mat = new Material(spearShader);
        mat.name = "ManaSpear (Instance)";
        mat.hideFlags = HideFlags.DontSave;

        mesh = NewMesh("ManaSpearFront");
        meshFilter.sharedMesh = mesh;
        SetupRenderer(meshRenderer, sortingLayerName, sortingOrder);

        // 바닥 효과의 먼 쪽 절반은 캐릭터 뒤에 그려져야 해서 정렬 순서가 다른 별도의 렌더러로 그림
        GameObject backGo = new GameObject("ManaSpearBack");
        backGo.transform.SetParent(transform, false);
        backFilter = backGo.AddComponent<MeshFilter>();
        backRenderer = backGo.AddComponent<MeshRenderer>();
        backMesh = NewMesh("ManaSpearBack");
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

    void ApplyMaterial()
    {
        mat.SetColor(CoreColorId, coreColor);
        mat.SetColor(MidColorId, midColor);
        mat.SetColor(EdgeColorId, edgeColor);
        mat.SetFloat(GlowId, glow);
        mat.SetFloat(LinesId, sideLines);
        mat.SetColor(SmokeColorId, smokeColor);
        mat.SetFloat(AlphaId, alpha);
    }

    /// <summary>
    /// 반동으로 일어나는 연기: 바닥을 따라 시전자 양옆을 지나 뒤로 밀려 나가는 덩이들 + 발사 지점에서 피어오르는 몇 덩이.
    /// 바닥의 먼 쪽(화면 위쪽)으로 가는 덩이는 캐릭터 뒤에, 가까운 쪽은 앞에 그려집니다.
    /// </summary>
    void SpawnSmoke(Blast bl, Vector3 origin, float now)
    {
        if (smokeAmount <= 0f) return;

        int floorCount = Mathf.RoundToInt(12f * smokeAmount);
        for (int i = 0; i < floorCount; i++)
        {
            float side = Rand(0f, 1f) < 0.5f ? 1f : -1f;          // +1 = 먼 쪽, -1 = 가까운 쪽
            float f = Rand(-0.3f, 0.8f);                           // 시전자 발밑에서 앞쪽으로
            float z = side * Rand(0.3f, 1.1f);                     // 옆으로 (바닥 기준)
            Puff p = new Puff();
            p.pos = new Vector3(bl.casterX + bl.sign * f, bl.groundY + z * groundTilt + Rand(0.05f, 0.3f), origin.z);
            p.vel = new Vector3(-bl.sign * Rand(2.5f, 6.5f), side * Rand(0.5f, 2.0f) * groundTilt + Rand(0.2f, 0.7f), 0f);
            p.size = Rand(0.9f, 1.7f) * smokeSize;
            p.start = now + Rand(0f, 0.10f);
            p.life = Rand(0.5f, 0.95f);
            p.seed = Rand(0f, 10f);
            p.behind = side > 0f;
            puffs.Add(p);
        }

        int muzzleCount = Mathf.RoundToInt(4f * smokeAmount);
        for (int i = 0; i < muzzleCount; i++)
        {
            Puff p = new Puff();
            p.pos = origin + new Vector3(Rand(-0.2f, 0.3f) * bl.sign, Rand(-0.3f, 0.3f), 0f);
            p.vel = new Vector3(-bl.sign * Rand(0.5f, 2.0f), Rand(0.5f, 1.8f), 0f);
            p.size = Rand(0.8f, 1.3f) * smokeSize;
            p.start = now + Rand(0f, 0.06f);
            p.life = Rand(0.4f, 0.7f);
            p.seed = Rand(0f, 10f);
            p.behind = false;
            puffs.Add(p);
        }
    }

    void AddSparkle(Vector3 pos, float size, float start, float life, float rot)
    {
        Sparkle s = new Sparkle();
        s.pos = pos; s.size = size; s.start = start; s.life = life; s.rot = rot;
        sparkles.Add(s);
    }

    void AddNeedle(Vector3 pos, Vector3 vel, float size, float start, float life)
    {
        Needle n = new Needle();
        n.pos = pos; n.vel = vel; n.size = size; n.start = start; n.life = life;
        needles.Add(n);
    }

    // ------------------------------------------------------------------ mesh

    void BuildMeshes()
    {
        front.Clear();
        back.Clear();
        Vector4 none = Vector4.zero;
        Vector2 uvMin = new Vector2(0f, 0f);
        Vector2 uvMax = new Vector2(1f, 1f);

        // ---- 발사 지점의 빛무리 (가장 뒤)
        for (int i = 0; i < glows.Count; i++)
        {
            float t = (clock - glows[i].start) / glows[i].life;
            if (t < 0f || t >= 1f) continue;
            float h = glows[i].size * 0.5f;
            AddQuad(front, glows[i].pos, new Vector3(h, 0f, 0f), new Vector3(0f, h, 0f), uvMin, uvMax,
                    new Vector4(4f, (1f - t) * (1f - t), 0f, 0f), none);
        }

        // ---- 마력창: 첫 프레임부터 전체 길이. 잠깐 하얗게 부풀었다가 가늘어지며 사라짐
        float halfHeight = beamWidth * 0.5f / BodyK;
        for (int i = 0; i < beams.Count; i++)
        {
            Beam b = beams[i];
            float t = clock - b.start;
            if (t < 0f || t >= holdTime + fadeTime) continue;

            float fadeT = Mathf.Clamp01((t - holdTime) / fadeTime);
            float flash = 1f - SStep(0f, holdTime + fadeTime * 0.35f, t);
            float wk = (1f + 0.35f * Mathf.Exp(-t * 30f)) * (1f - fadeT * fadeT);

            Vector3 perp = new Vector3(-b.dir.y, b.dir.x, 0f);
            float headFrac = Mathf.Min(headLength / b.length, 0.5f);
            AddQuad(front, b.origin + b.dir * (b.length * 0.5f), b.dir * (b.length * 0.5f), perp * halfHeight, uvMin, uvMax,
                    new Vector4(0f, fadeT, wk, flash),
                    new Vector4(b.length / (2f * halfHeight), b.seed, t, headFrac));
        }

        // ---- 후폭풍
        for (int i = 0; i < blasts.Count; i++) AddBlast(blasts[i]);

        // ---- 연기: 뒤로 밀려 나가며 감속, 부풀면서 옅어짐
        for (int i = 0; i < puffs.Count; i++)
        {
            Puff p = puffs[i];
            float age = clock - p.start;
            if (age <= 0f || age >= p.life) continue;

            float k = age / p.life;
            Vector3 pos = p.pos + p.vel * ((1f - Mathf.Exp(-2.5f * age)) / 2.5f);
            float halfSize = p.size * (0.55f + 0.75f * k) * 0.5f;
            float a = SStep(0f, 0.15f, k) * (1f - SStep(0.35f, 1f, k)) * 0.75f;
            AddQuad(p.behind ? back : front, pos, new Vector3(halfSize, 0f, 0f), new Vector3(0f, halfSize, 0f), uvMin, uvMax,
                    new Vector4(7f, p.seed, age, a), none);
        }

        // ---- 불티: 튀어 나가며 감속, 짧아짐
        for (int i = 0; i < needles.Count; i++)
        {
            Needle nd = needles[i];
            float age = clock - nd.start;
            if (age <= 0f || age >= nd.life) continue;

            float k = age / nd.life;
            Vector3 pos = nd.pos + nd.vel * ((1f - Mathf.Exp(-4f * age)) / 4f);
            Vector3 dir = nd.vel.normalized;
            float halfLen = nd.size * (1f - 0.5f * k) * 0.5f;
            AddQuad(front, pos, dir * halfLen, new Vector3(-dir.y, dir.x, 0f) * (halfLen * 0.15f), uvMin, uvMax,
                    new Vector4(3f, 1f - k * k, 0f, 0f), none);
        }

        // ---- 섬광 / 반짝임
        for (int i = 0; i < sparkles.Count; i++)
        {
            Sparkle sp = sparkles[i];
            float t = (clock - sp.start) / sp.life;
            if (t <= 0f || t >= 1f) continue;

            Vector3 ax = new Vector3(Mathf.Cos(sp.rot), Mathf.Sin(sp.rot), 0f) * (sp.size * 0.5f);
            AddQuad(front, sp.pos, ax, new Vector3(-ax.y, ax.x, 0f), uvMin, uvMax, new Vector4(2f, t, 0f, 0f), none);
        }

        front.Apply(mesh);
        back.Apply(backMesh);
    }

    /// <summary>
    /// 후폭풍 한 번을 그립니다. 바닥 위의 것(고리, 바람)은 화면에서 세로로 눌린 쿼드에 그려 바닥에 누운 것처럼 보이게 하고,
    /// 먼 쪽 절반(화면 위쪽)은 뒤쪽 렌더러에, 가까운 쪽 절반은 앞쪽 렌더러에 나눠 담습니다.
    /// </summary>
    void AddBlast(Blast bl)
    {
        Vector4 none = Vector4.zero;
        float R = blastRadius;
        float h = Mathf.Max(muzzleOffset.y, 0f);
        float el = Mathf.Max(blastElongation, 0.05f);                           // 위아래 반지름에 대한 앞뒤 깊이의 비율
        float side = Mathf.Sqrt(Mathf.Max(R * R - h * h, 0.25f * R * R));      // 반구가 바닥에 남기는 자국의 옆쪽 반폭
        float reach = side * el;                                                // 앞쪽으로 바닥에 닿는 곳까지의 거리
        float halfAngle = blastHalfAngle * Mathf.Deg2Rad;
        Vector3 groundPoint = new Vector3(bl.center.x, bl.groundY, bl.center.z);
        Vector3 forward = new Vector3(bl.sign, 0f, 0f);

        float t = (clock - bl.start) / blastTime;
        if (t > 0f && t < 1f)
        {
            // 바닥의 반쪽 고리: 반구의 밑동
            if (floorRing)
            {
                float hs = side / RingK;
                Vector4 mode = new Vector4(5f, t, halfAngle, bl.seed);
                Vector3 ax = forward * (hs * el);
                Vector3 ay = new Vector3(0f, hs * groundTilt, 0f);
                AddQuad(back, groundPoint, ax, ay, new Vector2(0f, 0.5f), new Vector2(1f, 1f), mode, none);
                AddQuad(front, groundPoint, ax, ay, new Vector2(0f, 0f), new Vector2(1f, 0.5f), mode, none);
            }

            // 반구: 맨 앞 꼭짓점이 발사 지점. 앞뒤로 눌린(또는 늘인) 쿼드에 그려 위아래가 긴 완만한 곡면이 됨.
            // 바닥 아래로 내려가는 부분은 셰이더가 가림
            float hd = R / RingK;
            AddQuad(front, bl.center, forward * (hd * el), new Vector3(0f, hd, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f),
                    new Vector4(1f, t, halfAngle, bl.seed + 3f), new Vector4(-h / hd, groundTilt, 0f, 0f));
        }

        // 바닥 바람: 꼭짓점은 반구가 바닥에 닿는 자리(시전자 앞), 거기서 시전자 양옆을 지나 뒤로 벌어짐
        float tw = (clock - bl.start) / windTime;
        if (windLength > 0f && tw > 0f && tw < 1f)
        {
            float L = windLength;
            float phi = windHalfAngle * Mathf.Deg2Rad;
            float zMax = Mathf.Tan(phi) * 1.25f;                  // 길이 대비
            Vector3 apex = groundPoint + forward * reach;
            Vector4 mode = new Vector4(6f, tw, phi, bl.seed + 5f);

            AddGroundPatch(back, apex, bl.sign, L, 0f, zMax, mode);      // 먼 쪽 (화면 위쪽)
            AddGroundPatch(front, apex, bl.sign, L, -zMax, 0f, mode);    // 가까운 쪽
        }
    }

    // 바닥 좌표의 사각형: 꼭짓점에서 뒤쪽으로 0..1.05, 옆으로 z0..z1 (모두 길이 대비). uv 에 바닥 좌표를 그대로 실음
    void AddGroundPatch(Buffers b, Vector3 apex, float sign, float length, float z0, float z1, Vector4 mode)
    {
        Vector4 none = Vector4.zero;
        const float backMax = 1.05f;
        int q = b.verts.Count;
        AddVertex(b, GroundPoint(apex, sign, length, 0f, z0), new Vector2(0f, z0), mode, none);
        AddVertex(b, GroundPoint(apex, sign, length, 0f, z1), new Vector2(0f, z1), mode, none);
        AddVertex(b, GroundPoint(apex, sign, length, backMax, z0), new Vector2(backMax, z0), mode, none);
        AddVertex(b, GroundPoint(apex, sign, length, backMax, z1), new Vector2(backMax, z1), mode, none);
        b.tris.Add(q); b.tris.Add(q + 1); b.tris.Add(q + 2);
        b.tris.Add(q + 2); b.tris.Add(q + 1); b.tris.Add(q + 3);
    }

    // 바닥 좌표(뒤쪽으로 backDist, 화면 안쪽으로 z) -> 월드 위치. 바닥은 화면에서 세로로 눌려 보임
    Vector3 GroundPoint(Vector3 apex, float sign, float length, float backDist, float z)
    {
        return new Vector3(apex.x - sign * backDist * length, apex.y + z * length * groundTilt, apex.z);
    }

    // 중심과 두 반축으로 사각형 하나 (uvMin..uvMax 구간만). uv.x 는 ax 방향, uv.y 는 ay 방향
    void AddQuad(Buffers b, Vector3 center, Vector3 ax, Vector3 ay, Vector2 uvMin, Vector2 uvMax, Vector4 mode, Vector4 prm)
    {
        int q = b.verts.Count;
        AddVertex(b, center + ax * (uvMin.x * 2f - 1f) + ay * (uvMin.y * 2f - 1f), new Vector2(uvMin.x, uvMin.y), mode, prm);
        AddVertex(b, center + ax * (uvMin.x * 2f - 1f) + ay * (uvMax.y * 2f - 1f), new Vector2(uvMin.x, uvMax.y), mode, prm);
        AddVertex(b, center + ax * (uvMax.x * 2f - 1f) + ay * (uvMin.y * 2f - 1f), new Vector2(uvMax.x, uvMin.y), mode, prm);
        AddVertex(b, center + ax * (uvMax.x * 2f - 1f) + ay * (uvMax.y * 2f - 1f), new Vector2(uvMax.x, uvMax.y), mode, prm);
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
    // 메뉴: GameObject > Effects > Mana Spear VFX (마나 스피어)
    [UnityEditor.MenuItem("GameObject/Effects/Mana Spear VFX", false, 10)]
    static void CreateFromMenu(UnityEditor.MenuCommand command)
    {
        GameObject go = new GameObject("ManaSpearVFX");
        ManaSpearVFX fx = go.AddComponent<ManaSpearVFX>();
        fx.previewLoop = true;               // Play 를 누르면 바로 반복 발사되도록. 실제 사용 시에는 끄세요
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Mana Spear VFX");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
