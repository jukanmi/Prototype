using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 숄더차지 VFX (벨트스크롤용).
/// 전방의 적을 밀어내며 돌격하는 스킬의 이펙트입니다. 캐릭터를 움직이지는 않고, 움직이는 캐릭터를 따라가며 그립니다.
///   1) 시작      : 발밑에서 기운이 뒤로 튀고(가시, 먼지, 바닥 충격파), 캐릭터 실루엣이 커지며 번쩍입니다(오라).
///   2) 돌진 중   : 어깨 앞에 초승달 모양의 충격파가 붙어 가고, 뒤로 속도선, 먼지, 바닥의 자국, 잔상이 남습니다.
///                  실제로 움직이는 동안에만 나오므로, 선딜 자세에서는 충격파가 뜨지 않습니다.
///   3) 도착      : 충격파가 앞으로 밀려 나가며 사라지고, 멈춰 선 자리에서 섬광과 줄기, 바닥 충격파, 먼지가 앞쪽으로 퍼집니다.
///
///   charge.Play(characterTransform, facing, 0.3f);   // 캐릭터를 0.3초 동안 따라감. facing: 오른쪽 +1, 왼쪽 -1
///   charge.Play(characterTransform, facing, 0f);     // 시간을 정하지 않고, End() 를 부를 때까지
///   charge.End();                                    // 돌진이 끝남 (벽에 막혔을 때 등)
///   charge.PlayHit(enemyPosition);                   // 적을 들이받은 자리에 타격 섬광
///   charge.PlayBetween(from, to, 0.3f);              // 캐릭터 없이 두 점 사이를 스스로 지나가며 재생
///
/// 잔상과 오라는 Character Sprite 칸에 캐릭터의 SpriteRenderer 를 넣어야 나옵니다 (비워 두면 나머지만 재생).
/// 바닥에 깔리는 것(자국, 충격파, 먼지)은 캐릭터 뒤에 그려져야 하므로 별도의 정렬 순서(Floor Sorting Order)를 씁니다.
///
/// 충격파 · 먼지 등은 이 오브젝트의 로컬 XY 평면에 그려집니다. 넘겨받은 월드 위치는 그 평면으로 (로컬 Z 를 따라) 눌러서 씁니다 —
/// 오브젝트를 카메라 쪽으로 돌려 두면 3D 공간의 점이 화면에 보이는 자리 그대로 놓입니다. (잔상은 캐릭터 스프라이트의 월드 자세를 그대로 복사)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ShoulderChargeVFX : MonoBehaviour
{
    public enum FinishAction
    {
        None,      // 그대로 대기 (다시 Play 해서 재사용)
        Disable,   // 비활성화 (오브젝트 풀링)
        Destroy    // 오브젝트 삭제 (Instantiate 해서 쓰는 경우)
    }

    [Header("Shader (컴포넌트를 붙이면 자동으로 채워짐)")]
    public Shader chargeShader;
    [Tooltip("잔상/오라용. Character Sprite 를 쓰지 않으면 없어도 됩니다")]
    public Shader silhouetteShader;

    [Header("크기")]
    [Tooltip("캐릭터의 키 (월드 유닛). 이펙트 전체의 크기와 높이가 이 값을 따라갑니다")]
    [Min(0.2f)] public float bodyHeight = 1.8f;
    [Tooltip("따라갈 위치가 발밑이 아닐 때의 보정 (x 는 바라보는 방향 기준)")]
    public Vector2 footOffset = Vector2.zero;
    [Tooltip("어깨 앞 충격파의 세로 길이")]
    [Min(0.2f)] public float shockHeight = 2.6f;
    [Tooltip("충격파의 앞뒤 두께 (클수록 더 둥글게 휨)")]
    [Min(0.1f)] public float shockDepth = 1.15f;
    [Tooltip("충격파를 몸에서 얼마나 앞에 둘지")]
    public float shockForward = 0.2f;
    [Tooltip("바닥이 화면에서 세로로 눌려 보이는 비율. 카메라가 바닥을 비스듬히 내려다볼수록 작게")]
    [Range(0.1f, 1f)] public float groundTilt = 0.42f;

    [Header("돌진 중")]
    [Tooltip("이 속도(유닛/초)보다 빠르게 움직일 때만 충격파, 속도선, 먼지, 잔상이 나옴")]
    [Min(0f)] public float minSpeed = 1f;
    [Tooltip("속도선의 양 (초당)")]
    [Min(0f)] public float lineRate = 70f;
    [Tooltip("발밑 먼지의 양 (초당)")]
    [Min(0f)] public float dustRate = 45f;
    [Tooltip("바닥에 남는 자국의 폭 (바닥 위에서의 깊이 방향 길이, 0 이면 없음)")]
    [Min(0f)] public float skidWidth = 0.75f;

    [Header("시작 / 도착")]
    [Tooltip("시작할 때 발밑에서 뒤로 튀는 기운의 날 개수 (0 이면 없음)")]
    [Range(0, 20)] public int startSpikes = 7;
    [Range(0, 40)] public int endRays = 16;
    [Tooltip("도착했을 때 충격파가 앞으로 밀려 나가며 사라지는 시간")]
    [Min(0.02f)] public float releaseTime = 0.22f;
    [Tooltip("그동안 충격파가 앞으로 나아가는 거리")]
    public float releaseDistance = 1.3f;

    [Header("잔상 / 오라 (선택)")]
    [Tooltip("캐릭터의 SpriteRenderer. 넣으면 그 스프라이트 모양으로 잔상과 시작 오라를 만듭니다")]
    public SpriteRenderer characterSprite;
    [Tooltip("잔상을 남기는 간격 (초)")]
    [Min(0.01f)] public float afterimageInterval = 0.045f;
    [Min(0.02f)] public float afterimageLife = 0.20f;
    public Color afterimageColor = new Color(0.13f, 0.50f, 1f, 0.6f);
    [Tooltip("시작할 때 실루엣이 이 배율까지 커지며 사라짐 (1 이하면 오라 없음)")]
    [Min(0f)] public float startAuraScale = 1.45f;
    public Color startAuraColor = new Color(0.66f, 0.93f, 1f, 0.85f);
    [Range(1, 32)] public int maxAfterimages = 12;

    [Header("Color")]
    [ColorUsage(false, true)] public Color coreColor = Color.white;
    [ColorUsage(false, true)] public Color midColor = new Color(0.66f, 0.93f, 1f);
    [ColorUsage(false, true)] public Color edgeColor = new Color(0.13f, 0.50f, 1f);
    public Color dustColor = new Color(0.74f, 0.80f, 0.90f);
    [Range(0f, 2f)] public float glow = 1f;
    [Range(0f, 1f)] public float alpha = 1f;

    [Header("Playback")]
    public bool useUnscaledTime = false;
    [Tooltip("0 이면 쓸 때마다 결이 달라지고, 다른 값이면 항상 같은 모양")]
    public int seed = 0;
    public FinishAction onFinishedAction = FinishAction.None;

    [Header("Sorting")]
    [Tooltip("충격파, 속도선, 가시, 섬광 (캐릭터보다 앞)")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 100;
    [Tooltip("바닥의 자국과 충격파, 먼지, 몸 뒤의 빛 (배경보다 앞, 캐릭터보다 뒤가 되도록 맞추세요)")]
    public string floorSortingLayerName = "Default";
    public int floorSortingOrder = -1;

    [Header("Preview")]
    [Tooltip("켜두면 이 오브젝트 위치에서 앞으로 계속 반복 재생 (룩 조정용)")]
    public bool previewLoop = false;
    [Tooltip("음수면 왼쪽으로")]
    public float previewDistance = 6f;
    public float previewDuration = 0.34f;
    public float previewPause = 0.7f;

    [Header("Events")]
    [Tooltip("돌진이 끝나 충격파를 내보내는 순간 (마무리 넉백, 카메라 흔들림 등)")]
    public UnityEvent onEnd;
    public UnityEvent onFinished;

    public bool IsPlaying { get { return active; } }

    /// <summary>돌진하는 동안 (Play 부터 End 전까지) true</summary>
    public bool IsCharging { get { return active && charging; } }

    /// <summary>지금 따라가고 있는 발밑 위치 (월드)</summary>
    public Vector3 Position { get { return pos; } }

    /// <summary>바라보는 방향: 오른쪽 +1, 왼쪽 -1</summary>
    public float Facing { get { return facing; } }

    // ------------------------------------------------------------------ internals

    static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    static readonly int SmokeColorId = Shader.PropertyToID("_SmokeColor");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");

    // 남겨진 것들은 모두 월드 좌표로 들고 있어서, 캐릭터가 지나간 자리에 그대로 남음

    struct Spike     // 바닥에서 튀는 기운의 날
    {
        public Vector3 basePos, dir;
        public float length, width, start, life;
    }

    struct Needle    // 속도선(vel = 0) 또는 튀어 나가는 줄기
    {
        public Vector3 pos, vel, dir;
        public float size, start, life;
    }

    struct Dust
    {
        public Vector3 pos, vel;
        public float size, start, life, seed;
    }

    struct Sparkle
    {
        public Vector3 pos;
        public float size, start, life, rot;
    }

    struct Shock     // 바닥 충격파
    {
        public Vector3 pos;
        public float start, life, radius, seed;
    }

    struct Glow
    {
        public Vector3 pos;
        public float size, start, life;
    }

    class Ghost      // 캐릭터 스프라이트의 잔상 하나
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Vector3 baseScale;
        public Color color;
        public float start, life, scale0, scale1;
        public bool live;
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
    Material mat, ghostMat;
    MaterialPropertyBlock ghostBlock;
    bool initialized;
    System.Random rng;

    readonly List<Spike> spikes = new List<Spike>();
    readonly List<Needle> needles = new List<Needle>();
    readonly List<Dust> dusts = new List<Dust>();
    readonly List<Sparkle> sparkles = new List<Sparkle>();
    readonly List<Shock> shocks = new List<Shock>();
    readonly List<Glow> glows = new List<Glow>();
    readonly List<Ghost> ghosts = new List<Ghost>();
    readonly Buffers front = new Buffers();
    readonly Buffers floor = new Buffers();

    float clock;
    bool active, charging;
    Transform followT;
    bool simulated;
    Vector3 simFrom, simTo;         // 로컬 평면
    float simDuration;
    Vector3 pos, startPos;
    float facing = 1f;
    float startTime;
    float endAt = -1f;              // 시작 후 몇 초에 자동으로 끝낼지 (정해지지 않았으면 음수)
    float endTime = -1f;            // 실제로 끝난 시각
    float bowAlpha;                 // 어깨 앞 충격파의 세기 (움직이는 동안 1 로, 멈추면 0 으로)
    float fxSeed;
    float lineAcc, dustAcc, ghostAcc;
    float previewTimer;

    float Size { get { return bodyHeight / 1.8f; } }      // 기준 키(1.8)에 대한 배율

    Vector3 Fwd(float forward, float up)
    {
        return new Vector3(facing * forward, up, 0f);
    }

    // ------------------------------------------------------------------ public API

    /// <summary>
    /// follow(캐릭터, 보통 발밑이 피벗)를 따라가며 재생합니다. facing: 오른쪽 +1, 왼쪽 -1.
    /// duration 초 뒤에 스스로 끝내고(End), 0 이하면 End() 를 부를 때까지 계속합니다.
    /// </summary>
    public void Play(Transform follow, float facingSign, float duration)
    {
        if (follow == null) return;
        followT = follow;
        simulated = false;
        facing = facingSign < 0f ? -1f : 1f;
        Begin(FollowPosition(), duration > 0f ? duration : -1f);
    }

    /// <summary>캐릭터 없이, from(발밑)에서 to 까지 duration 초 동안 스스로 지나가며 재생합니다.</summary>
    public void PlayBetween(Vector3 from, Vector3 to, float duration)
    {
        followT = null;
        simulated = true;
        simFrom = ToPlane(from);
        simTo = ToPlane(to);
        simDuration = Mathf.Max(duration, 0.02f);
        facing = simTo.x < simFrom.x ? -1f : 1f;
        Begin(simFrom, simDuration);
    }

    /// <summary>돌진을 지금 끝냅니다 (충격파를 앞으로 내보내며 마무리).</summary>
    public void End()
    {
        if (!active || !charging) return;
        DoEnd(clock - startTime);
    }

    /// <summary>적을 들이받은 자리(worldPosition)에 타격 섬광을 냅니다. 재생 중일 때만 동작합니다.</summary>
    public void PlayHit(Vector3 worldPosition)
    {
        if (!active) return;
        float t = clock - startTime;
        float k = Size;
        Vector3 hitPos = ToPlane(worldPosition);
        AddSparkle(hitPos, 1.9f * k, t, 0.10f, 0.6f);
        for (int i = 0; i < 7; i++)
        {
            float ang = Rand(-1.1f, 1.1f);
            Vector3 d = new Vector3(facing * Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            AddNeedle(hitPos + d * (0.2f * k), d * (Rand(5f, 11f) * k), d, Rand(0.4f, 0.9f) * k, t, Rand(0.12f, 0.20f));
        }
    }

    /// <summary>즉시 숨기고 멈춥니다.</summary>
    public void Stop()
    {
        ClearAll();
        active = false;
        charging = false;
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (floorRenderer != null) floorRenderer.enabled = false;
    }

    // ------------------------------------------------------------------ lifecycle

    void Reset()
    {
        // 에디터에서 컴포넌트를 붙일 때 호출됨. 여기서 참조를 걸어두면 빌드에도 셰이더가 포함됩니다.
        chargeShader = Shader.Find("VFX/ShoulderCharge");
        silhouetteShader = Shader.Find("VFX/SpriteSilhouette");
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
        for (int i = 0; i < ghosts.Count; i++)
            if (ghosts[i].go != null) Destroy(ghosts[i].go);
        ghosts.Clear();
        if (mesh != null) Destroy(mesh);
        if (floorMesh != null) Destroy(floorMesh);
        if (mat != null) Destroy(mat);
        if (ghostMat != null) Destroy(ghostMat);
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Prototype.TimeControl.DeltaTime;

        if (!active)
        {
            if (previewLoop && mat != null)
            {
                previewTimer -= dt;
                if (previewTimer <= 0f)
                    PlayBetween(transform.position, transform.position + new Vector3(previewDistance, 0f, 0f), previewDuration);
            }
            return;
        }

        clock += dt;
        float t = clock - startTime;

        if (charging)
        {
            // ---- 이번 프레임의 위치와 속도
            Vector3 newPos = pos;
            if (simulated) newPos = Vector3.Lerp(simFrom, simTo, Mathf.Clamp01(t / simDuration));
            else if (followT != null) newPos = FollowPosition();

            float dx = newPos.x - pos.x;
            float dy = newPos.y - pos.y;
            float speed = dt > 1e-5f ? Mathf.Sqrt(dx * dx + dy * dy) / dt : 0f;
            pos = newPos;

            // ---- 실제로 움직이는 동안에만: 충격파가 켜지고, 뒤로 흔적이 남음
            bool moving = speed > minSpeed;
            bowAlpha = Mathf.Clamp01(bowAlpha + (moving ? dt / 0.06f : -dt / 0.10f));
            if (moving) SpawnTrail(t, dt);

            // ---- 끝: 시간이 다 됐거나, 따라가던 대상이 사라짐
            if ((endAt >= 0f && t >= endAt) || (!simulated && followT == null))
            {
                DoEnd(t);
                if (!active) return;                     // 이벤트 안에서 Stop() 을 부른 경우
            }
        }

        // ---- 수명이 끝난 요소 정리
        for (int i = spikes.Count - 1; i >= 0; i--)
            if (t >= spikes[i].start + spikes[i].life) spikes.RemoveAt(i);
        for (int i = needles.Count - 1; i >= 0; i--)
            if (t >= needles[i].start + needles[i].life) needles.RemoveAt(i);
        for (int i = dusts.Count - 1; i >= 0; i--)
            if (t >= dusts[i].start + dusts[i].life) dusts.RemoveAt(i);
        for (int i = sparkles.Count - 1; i >= 0; i--)
            if (t >= sparkles[i].start + sparkles[i].life) sparkles.RemoveAt(i);
        for (int i = shocks.Count - 1; i >= 0; i--)
            if (t >= shocks[i].start + shocks[i].life) shocks.RemoveAt(i);
        for (int i = glows.Count - 1; i >= 0; i--)
            if (t >= glows[i].start + glows[i].life) glows.RemoveAt(i);

        bool ghostsLive = UpdateGhosts(t);

        if (!charging && t >= endTime + Mathf.Max(releaseTime, 0.35f) && !ghostsLive
            && spikes.Count == 0 && needles.Count == 0 && dusts.Count == 0
            && sparkles.Count == 0 && shocks.Count == 0 && glows.Count == 0)
        {
            Finish();
            return;
        }

        BuildMeshes(t);
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
        spikes.Clear(); needles.Clear(); dusts.Clear(); sparkles.Clear(); shocks.Clear(); glows.Clear();
        lineAcc = dustAcc = ghostAcc = 0f;
        bowAlpha = 0f;
        endAt = -1f;
        endTime = -1f;

        for (int i = 0; i < ghosts.Count; i++)
        {
            ghosts[i].live = false;
            if (ghosts[i].sr != null) ghosts[i].sr.enabled = false;
        }
    }

    // ------------------------------------------------------------------ setup

    void Init()
    {
        if (initialized) return;
        initialized = true;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (chargeShader == null) chargeShader = Shader.Find("VFX/ShoulderCharge");
        if (chargeShader == null)
        {
            Debug.LogError("[ShoulderChargeVFX] 'VFX/ShoulderCharge' 셰이더를 찾을 수 없습니다. ShoulderCharge.shader 를 프로젝트에 넣고 Charge Shader 칸에 지정하세요.", this);
            enabled = false;
            return;
        }

        mat = new Material(chargeShader);
        mat.name = "ShoulderCharge (Instance)";
        mat.hideFlags = HideFlags.DontSave;

        // 잔상용 머티리얼 (셰이더가 없으면 잔상만 생략)
        if (silhouetteShader == null) silhouetteShader = Shader.Find("VFX/SpriteSilhouette");
        if (silhouetteShader != null)
        {
            ghostMat = new Material(silhouetteShader);
            ghostMat.name = "SpriteSilhouette (Instance)";
            ghostMat.hideFlags = HideFlags.DontSave;
            ghostBlock = new MaterialPropertyBlock();
        }

        mesh = NewMesh("ShoulderChargeFront");
        meshFilter.sharedMesh = mesh;
        SetupRenderer(meshRenderer, sortingLayerName, sortingOrder);

        // 바닥에 깔리는 것들은 캐릭터 뒤에 그려져야 해서 정렬 순서가 다른 별도의 렌더러로 그림
        GameObject floorGo = new GameObject("ShoulderChargeFloor");
        floorGo.transform.SetParent(transform, false);
        floorFilter = floorGo.AddComponent<MeshFilter>();
        floorRenderer = floorGo.AddComponent<MeshRenderer>();
        floorMesh = NewMesh("ShoulderChargeFloor");
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

    Vector3 FollowPosition()
    {
        Vector3 p = ToPlane(followT.position);
        p.x += facing * footOffset.x;
        p.y += footOffset.y;
        return p;
    }

    void Begin(Vector3 startPosition, float autoEnd)
    {
        Init();
        if (mat == null) return;

        ClearAll();
        rng = seed != 0 ? new System.Random(seed) : new System.Random();
        startTime = clock;
        endAt = autoEnd;
        pos = startPosition;
        startPos = startPosition;
        fxSeed = Rand(0f, 10f);
        active = true;
        charging = true;
        meshRenderer.enabled = true;
        floorRenderer.enabled = true;

        SpawnStart(0f);
        BuildMeshes(0f);
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
        mat.SetColor(SmokeColorId, dustColor);
        mat.SetFloat(GlowId, glow);
        mat.SetFloat(AlphaId, alpha);
    }

    // ------------------------------------------------------------------ spawning (시각은 모두 Play 이후의 초)

    // degrees: 바라보는 방향이 0도, 위쪽이 90도
    void AddSpike(Vector3 basePos, float degrees, float length, float width, float start, float life)
    {
        float a = degrees * Mathf.Deg2Rad;
        Spike s = new Spike();
        s.basePos = basePos;
        s.dir = new Vector3(facing * Mathf.Cos(a), Mathf.Sin(a), 0f);
        s.length = length; s.width = width; s.start = start; s.life = life;
        spikes.Add(s);
    }

    void AddNeedle(Vector3 p, Vector3 vel, Vector3 dir, float size, float start, float life)
    {
        Needle n = new Needle();
        n.pos = p; n.vel = vel; n.dir = dir; n.size = size; n.start = start; n.life = life;
        needles.Add(n);
    }

    void AddDust(Vector3 p, Vector3 vel, float size, float start, float life)
    {
        Dust d = new Dust();
        d.pos = p; d.vel = vel; d.size = size; d.start = start; d.life = life; d.seed = Rand(0f, 10f);
        dusts.Add(d);
    }

    void AddSparkle(Vector3 p, float size, float start, float life, float rot)
    {
        Sparkle s = new Sparkle();
        s.pos = p; s.size = size; s.start = start; s.life = life; s.rot = rot;
        sparkles.Add(s);
    }

    void AddShock(Vector3 p, float start, float life, float radius)
    {
        Shock s = new Shock();
        s.pos = p; s.start = start; s.life = life; s.radius = radius; s.seed = Rand(0f, 10f);
        shocks.Add(s);
    }

    void AddGlow(Vector3 p, float size, float start, float life)
    {
        Glow g = new Glow();
        g.pos = p; g.size = size; g.start = start; g.life = life;
        glows.Add(g);
    }

    /// <summary>시작: 발밑에서 기운이 뒤로 튀고, 실루엣이 커지며 번쩍임</summary>
    void SpawnStart(float t)
    {
        float k = Size;
        float h = bodyHeight;

        AddSparkle(pos + Fwd(0.3f * k, h * 0.65f), 2.6f * k, t, 0.16f, 0f);
        AddGlow(pos + Fwd(0f, h * 0.5f), 4.2f * k, t, 0.25f);
        AddShock(pos, t, 0.30f, 1.5f * k);

        for (int i = 0; i < startSpikes; i++)
        {
            float deg = 92f + (i + Rand(0.1f, 0.9f)) / startSpikes * 80f;        // 위에서 뒤쪽으로 펼쳐짐
            AddSpike(pos + Fwd(-0.1f * k, 0.08f * k), deg, Rand(0.8f, 1.6f) * k, Rand(0.16f, 0.26f) * k, t + Rand(0f, 0.03f), Rand(0.20f, 0.30f));
        }

        for (int i = 0; i < 6; i++)
        {
            AddDust(pos + Fwd(-Rand(0f, 0.5f) * k, Rand(0.05f, 0.25f) * k), Fwd(-Rand(1f, 3f) * k, Rand(0.4f, 1.2f) * k),
                    Rand(0.6f, 1.0f) * k, t, Rand(0.4f, 0.6f));
        }

        if (startAuraScale > 1f) SpawnGhost(t, 0.24f, 1f, startAuraScale, startAuraColor);
    }

    /// <summary>돌진 중 계속 남기는 것들: 속도선, 발밑 먼지, 잔상</summary>
    void SpawnTrail(float t, float dt)
    {
        float k = Size;
        float h = bodyHeight;

        lineAcc += dt * lineRate;
        while (lineAcc >= 1f)
        {
            lineAcc -= 1f;
            AddNeedle(pos + Fwd(-Rand(0.1f, 1.3f) * k, Rand(0.1f, 1.1f) * h), Vector3.zero, Fwd(1f, 0f),
                      Rand(1.0f, 2.4f) * k, t, Rand(0.10f, 0.20f));
        }

        dustAcc += dt * dustRate;
        while (dustAcc >= 1f)
        {
            dustAcc -= 1f;
            AddDust(pos + Fwd(-Rand(0f, 0.5f) * k, Rand(0.05f, 0.22f) * k), Fwd(-Rand(0.3f, 1.8f) * k, Rand(0.3f, 1.0f) * k),
                    Rand(0.5f, 0.9f) * k, t, Rand(0.35f, 0.6f));
        }

        ghostAcc += dt;
        if (ghostAcc >= afterimageInterval)
        {
            ghostAcc = 0f;
            SpawnGhost(t, afterimageLife, 1f, 1f, afterimageColor);
        }
    }

    /// <summary>도착: 충격파를 앞으로 내보내고, 멈춰 선 자리에서 섬광과 줄기, 바닥 충격파, 먼지가 퍼짐</summary>
    void DoEnd(float t)
    {
        charging = false;
        endTime = t;
        bowAlpha = Mathf.Max(bowAlpha, 0.7f);            // 막혀서 거의 못 움직였더라도 충격파는 내보냄

        float k = Size;
        float h = bodyHeight;
        Vector3 frontPos = pos + Fwd(1.0f * k, h * 0.55f);

        AddSparkle(frontPos, 3.4f * k, t, 0.18f, 0f);
        AddSparkle(frontPos, 2.2f * k, t + 0.03f, 0.16f, 0.785f);
        AddGlow(frontPos, 5.5f * k, t, 0.28f);
        AddShock(pos + Fwd(0.6f * k, 0f), t, 0.40f, 2.4f * k);
        AddShock(pos + Fwd(0.6f * k, 0f), t + 0.06f, 0.34f, 1.5f * k);

        for (int i = 0; i < endRays; i++)
        {
            float ang = Rand(-22f, 62f) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(facing * Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            AddNeedle(frontPos + d * (0.3f * k), d * (Rand(6f, 15f) * k), d, Rand(0.5f, 1.2f) * k, t + Rand(0f, 0.04f), Rand(0.16f, 0.30f));
        }

        for (int i = 0; i < 9; i++)
        {
            AddDust(pos + Fwd(Rand(-0.2f, 0.9f) * k, Rand(0.05f, 0.3f) * k), Fwd(Rand(0.5f, 3.5f) * k, Rand(0.3f, 1.3f) * k),
                    Rand(0.7f, 1.2f) * k, t, Rand(0.45f, 0.7f));
        }

        if (onEnd != null) onEnd.Invoke();
    }

    // ------------------------------------------------------------------ afterimages

    /// <summary>캐릭터 스프라이트의 지금 모양을 한 장 남김 (Character Sprite 가 없으면 아무것도 하지 않음)</summary>
    void SpawnGhost(float t, float life, float scale0, float scale1, Color color)
    {
        if (characterSprite == null || characterSprite.sprite == null || ghostMat == null) return;

        Ghost g = GetFreeGhost();
        if (g == null) return;

        Transform src = characterSprite.transform;
        Vector3 s = src.lossyScale;
        if (characterSprite.flipX) s.x = -s.x;           // 반전은 스케일로 옮김 (셰이더가 _Flip 을 쓰지 않으므로)
        if (characterSprite.flipY) s.y = -s.y;

        g.go.layer = characterSprite.gameObject.layer;
        g.go.transform.position = src.position;
        g.go.transform.rotation = src.rotation;
        g.go.transform.localScale = s * scale0;
        g.sr.sprite = characterSprite.sprite;
        g.sr.sortingLayerID = characterSprite.sortingLayerID;
        g.sr.sortingOrder = characterSprite.sortingOrder - 1;        // 캐릭터 바로 뒤
        g.baseScale = s;
        g.color = color;
        g.start = t; g.life = Mathf.Max(life, 0.02f); g.scale0 = scale0; g.scale1 = scale1;
        g.live = true;
        SetGhostColor(g, color.a);
        g.sr.enabled = true;
    }

    Ghost GetFreeGhost()
    {
        Ghost oldest = null;
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            Ghost g = ghosts[i];
            if (g.go == null) { ghosts.RemoveAt(i); continue; }      // 씬이 바뀌며 사라진 경우
            if (!g.live) return g;
            if (oldest == null || g.start < oldest.start) oldest = g;
        }

        if (ghosts.Count >= maxAfterimages) return oldest;           // 꽉 찼으면 가장 오래된 것을 다시 씀

        Ghost n = new Ghost();
        n.go = new GameObject("ShoulderChargeGhost");        // 씬 최상위에 둠 (캐릭터나 이 오브젝트를 따라 움직이면 안 되므로)
        n.sr = n.go.AddComponent<SpriteRenderer>();
        n.sr.sharedMaterial = ghostMat;
        n.sr.enabled = false;
        ghosts.Add(n);
        return n;
    }

    void SetGhostColor(Ghost g, float a)
    {
        Color c = g.color;
        c.a = a * alpha;
        g.sr.GetPropertyBlock(ghostBlock);
        ghostBlock.SetColor(GhostColorId, c);
        g.sr.SetPropertyBlock(ghostBlock);
    }

    /// <summary>잔상들을 흐리게/크게 갱신. 아직 남아 있는 것이 있으면 true</summary>
    bool UpdateGhosts(float t)
    {
        bool any = false;
        for (int i = 0; i < ghosts.Count; i++)
        {
            Ghost g = ghosts[i];
            if (!g.live || g.go == null) continue;

            float k = (t - g.start) / g.life;
            if (k >= 1f)
            {
                g.live = false;
                g.sr.enabled = false;
                continue;
            }

            any = true;
            k = Mathf.Max(k, 0f);
            float inv = 1f - k;
            g.go.transform.localScale = g.baseScale * Mathf.Lerp(g.scale0, g.scale1, 1f - inv * inv);
            SetGhostColor(g, g.color.a * inv * Mathf.Sqrt(inv));
        }
        return any;
    }

    // ------------------------------------------------------------------ timeline

    /// <summary>어깨 앞 충격파의 위치/크기/세기. 그릴 것이 없으면 false</summary>
    bool BowState(float t, out Vector3 c, out float hw, out float hh, out float a, out float heat)
    {
        hw = shockDepth;
        hh = shockHeight * 0.5f;
        float up = bodyHeight * 0.58f;

        if (charging)
        {
            c = pos + Fwd(shockForward, up);
            a = bowAlpha;
            heat = 1f;
            return a > 0.003f;
        }

        // 도착: 앞으로 밀려 나가며 커지고, 파랗게 식으며 사라짐
        float x = (t - endTime) / Mathf.Max(releaseTime, 0.01f);
        if (x >= 1f)
        {
            c = pos;
            a = 0f;
            heat = 0f;
            return false;
        }

        float e = 1f - (1f - x) * (1f - x);
        float s = 1f + 0.35f * e;
        c = pos + Fwd(shockForward + releaseDistance * e, up);
        hw *= s;
        hh *= s;
        a = bowAlpha * (1f - x * x);
        heat = 1f - x;
        return a > 0.003f;
    }

    // ------------------------------------------------------------------ mesh

    void BuildMeshes(float t)
    {
        front.Clear();
        floor.Clear();
        Vector4 none = Vector4.zero;
        float size = Size;

        // ================= 바닥 / 캐릭터 뒤 =================

        // 돌진 자국: 출발점에서 발밑까지
        float skidFade = charging ? 1f : 1f - SStep(0f, 0.35f, t - endTime);
        Vector3 path = pos - startPos;
        path.z = 0f;
        float pathLen = path.magnitude;
        if (skidWidth > 0f && pathLen > 0.05f && skidFade > 0.003f)
        {
            Vector3 d = path / pathLen;
            float hl = pathLen * 0.5f + 0.2f;
            AddQuad(floor, (startPos + pos) * 0.5f, d * hl, new Vector3(-d.y, d.x, 0f) * (skidWidth * 0.5f * groundTilt),
                    new Vector4(7f, skidFade, hl * 2f, fxSeed), none);
        }

        for (int i = 0; i < shocks.Count; i++)
        {
            float st = (t - shocks[i].start) / shocks[i].life;
            if (st <= 0f || st >= 1f) continue;
            AddGroundQuad(floor, shocks[i].pos, shocks[i].radius, 1.05f, new Vector4(5f, st, shocks[i].seed, 0f));
        }

        // 돌진하는 몸을 감싸는 빛 (캐릭터 뒤에서 비춤)
        float bodyGlow = bowAlpha * (charging ? 1f : 1f - Mathf.Clamp01((t - endTime) / 0.15f));
        if (bodyGlow > 0.003f)
        {
            AddQuad(floor, pos + Fwd(0.1f * size, bodyHeight * 0.5f), new Vector3(1.9f * size, 0f, 0f), new Vector3(0f, 1.5f * size, 0f),
                    new Vector4(6f, 0.55f * bodyGlow, 0f, 0f), none);
        }

        for (int i = 0; i < dusts.Count; i++)
        {
            Dust du = dusts[i];
            float age = t - du.start;
            if (age <= 0f || age >= du.life) continue;

            float k = age / du.life;
            float inv = 1f - k;
            Vector3 p = du.pos + du.vel * ((1f - Mathf.Exp(-3f * age)) / 3f);
            float hs = du.size * (0.45f + 0.55f * (1f - inv * inv)) * 0.5f;      // 피어오르며 커짐
            float a = SStep(0f, 0.12f, k) * Mathf.Pow(inv, 1.2f) * 0.75f;
            AddQuad(floor, p, new Vector3(hs, 0f, 0f), new Vector3(0f, hs, 0f), new Vector4(2f, du.seed, age, a), none);
        }

        // ================= 앞쪽 =================
        for (int i = 0; i < glows.Count; i++)
        {
            float gt = (t - glows[i].start) / glows[i].life;
            if (gt < 0f || gt >= 1f) continue;
            float h = glows[i].size * 0.5f;
            AddQuad(front, glows[i].pos, new Vector3(h, 0f, 0f), new Vector3(0f, h, 0f), new Vector4(6f, (1f - gt) * (1f - gt), 0f, 0f), none);
        }

        // 속도선 / 튀는 줄기
        for (int i = 0; i < needles.Count; i++)
        {
            Needle nd = needles[i];
            float age = t - nd.start;
            if (age <= 0f || age >= nd.life) continue;

            float k = age / nd.life;
            Vector3 p = nd.pos + nd.vel * ((1f - Mathf.Exp(-4f * age)) / 4f);
            float halfLen = nd.size * (1f - 0.5f * k) * 0.5f;
            float halfWid = Mathf.Max(halfLen * 0.07f, 0.022f);
            AddQuad(front, p, nd.dir * halfLen, new Vector3(-nd.dir.y, nd.dir.x, 0f) * halfWid, new Vector4(4f, 1f - k * k, 0f, 0f), none);
        }

        // 기운의 날: 빠르게 뻗었다가, 뿌리부터 놓으며 끝으로 오그라들고 가늘어짐
        for (int i = 0; i < spikes.Count; i++)
        {
            Spike sp = spikes[i];
            float age = t - sp.start;
            if (age <= 0f || age >= sp.life) continue;

            float k = age / sp.life;
            float g = 1f - Mathf.Min(k / 0.25f, 1f);
            float len = sp.length * (1f - g * g);
            float root = 0.75f * SStep(0.35f, 1f, k);
            float halfLen = len * (1f - root) * 0.5f;
            if (halfLen < 0.005f) continue;
            AddQuad(front, sp.basePos + sp.dir * (len * root + halfLen), sp.dir * halfLen, new Vector3(-sp.dir.y, sp.dir.x, 0f) * sp.width,
                    new Vector4(1f, 1f - SStep(0.3f, 1f, k), 0f, 0f), none);
        }

        // 어깨 앞의 충격파 (uv.x 가 앞쪽을 향하도록 가로축을 바라보는 방향으로)
        Vector3 bowPos;
        float bowHw, bowHh, bowA, bowHeat;
        if (BowState(t, out bowPos, out bowHw, out bowHh, out bowA, out bowHeat))
        {
            AddQuad(front, bowPos, new Vector3(facing * bowHw, 0f, 0f), new Vector3(0f, bowHh, 0f),
                    new Vector4(0f, t, bowA, fxSeed), new Vector4(bowHeat, 0f, 0f, 0f));
        }

        // 섬광
        for (int i = 0; i < sparkles.Count; i++)
        {
            Sparkle s = sparkles[i];
            float st = (t - s.start) / s.life;
            if (st <= 0f || st >= 1f) continue;

            float hs = s.size * 0.5f;
            Vector3 ax = new Vector3(Mathf.Cos(s.rot) * hs, Mathf.Sin(s.rot) * hs, 0f);
            AddQuad(front, s.pos, ax, new Vector3(-ax.y, ax.x, 0f), new Vector4(3f, st, 0f, 0f), none);
        }

        front.Apply(mesh);
        floor.Apply(floorMesh);
    }

    // 바닥에 누운 사각형 (중심 c, 좌우 반지름 r). uv 에 바닥 좌표(-extent..extent)를 그대로 실음
    void AddGroundQuad(Buffers b, Vector3 c, float r, float extent, Vector4 mode)
    {
        Vector4 none = Vector4.zero;
        float hx = r * extent;
        float hy = r * extent * groundTilt;
        int q = b.verts.Count;
        AddVertex(b, c + new Vector3(-hx, -hy, 0f), new Vector2(-extent, -extent), mode, none);
        AddVertex(b, c + new Vector3(-hx, hy, 0f), new Vector2(-extent, extent), mode, none);
        AddVertex(b, c + new Vector3(hx, -hy, 0f), new Vector2(extent, -extent), mode, none);
        AddVertex(b, c + new Vector3(hx, hy, 0f), new Vector2(extent, extent), mode, none);
        b.tris.Add(q); b.tris.Add(q + 1); b.tris.Add(q + 2);
        b.tris.Add(q + 2); b.tris.Add(q + 1); b.tris.Add(q + 3);
    }

    // 중심과 두 반축으로 사각형 하나. uv.x 는 ax 방향, uv.y 는 ay 방향
    void AddQuad(Buffers b, Vector3 p, Vector3 ax, Vector3 ay, Vector4 mode, Vector4 prm)
    {
        int q = b.verts.Count;
        AddVertex(b, p - ax - ay, new Vector2(0f, 0f), mode, prm);
        AddVertex(b, p - ax + ay, new Vector2(0f, 1f), mode, prm);
        AddVertex(b, p + ax - ay, new Vector2(1f, 0f), mode, prm);
        AddVertex(b, p + ax + ay, new Vector2(1f, 1f), mode, prm);
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
    // 메뉴: GameObject > Effects > Shoulder Charge VFX (숄더차지)
    [UnityEditor.MenuItem("GameObject/Effects/Shoulder Charge VFX", false, 10)]
    static void CreateFromMenu(UnityEditor.MenuCommand command)
    {
        GameObject go = new GameObject("ShoulderChargeVFX");
        ShoulderChargeVFX fx = go.AddComponent<ShoulderChargeVFX>();
        fx.previewLoop = true;               // Play 를 누르면 바로 반복 재생되도록. 실제 사용 시에는 끄세요
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Shoulder Charge VFX");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
