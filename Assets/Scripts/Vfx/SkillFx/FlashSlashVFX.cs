using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 일섬 VFX.
///   1타 (PlayDash)    : 돌진 궤적. 시작점에서 도착점까지 한 줄기 빛이 그어지고 속도선이 따라붙습니다.
///   2타 (PlaySlashes) : 지나온 경로 위에 가늘고 긴 참격이 하나씩 그어져 쌓이고, 주변 공간에 금이 번집니다.
///                       마지막 참격 뒤 잠깐 멈췄다가 모든 참격이 한 번에 사라지면서 금 간 공간이 깨져 흩어집니다.
///                       공간은 참격이 지나간 선을 따라 갈라집니다 (조각의 변이 참격의 결을 따름).
///                       refractBackground 를 켜면 조각 하나하나가 깨진 거울처럼 실제 배경(캐릭터 포함)을 어긋나고 일그러지게 비추고,
///                       깨질 때는 그 조각들이 배경을 비춘 채 흩어집니다. 뒤의 배경은 그대로 남습니다.
///
///   flash.PlayDash(from, to);     // 1타. from = 돌진 시작 위치, to = 적을 지나친 도착 위치
///   flash.PlaySlashes();          // 2타. 방금 돌진한 경로를 그대로 사용 (경로를 직접 줄 때는 PlaySlashesBetween)
///   flash.PlayFull(from, to);     // 1타 후 slashDelay 뒤에 2타까지 자동 재생
///   flash.Shatter();              // manualShatter 를 켰을 때, 원하는 순간(납도 모션 등)에 깨뜨림
///
/// 방향 반전은 따로 필요 없습니다. 두 위치만으로 방향이 정해집니다.
/// 이펙트는 월드 좌표에 고정되므로, 이 오브젝트가 캐릭터의 자식이어도 캐릭터를 따라 움직이지 않습니다.
///
/// 궤적은 이 오브젝트의 로컬 XY 평면에 그려집니다. 넘겨받은 월드 위치는 그 평면으로 (로컬 Z 를 따라) 눌러서 씁니다 —
/// 오브젝트를 카메라 쪽으로 돌려 두면 3D 공간의 점이 화면에 보이는 자리 그대로 놓입니다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]      // 카메라를 옮기는 다른 스크립트들보다 나중에 돌아서, 배경 캡처가 한 프레임 밀리지 않게
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FlashSlashVFX : MonoBehaviour
{
    public enum FinishAction
    {
        None,      // 그대로 대기 (다시 호출해서 재사용)
        Disable,   // 비활성화 (오브젝트 풀링)
        Destroy    // 오브젝트 삭제 (Instantiate 해서 쓰는 경우)
    }

    [Header("Shader (컴포넌트를 붙이면 자동으로 채워짐)")]
    public Shader flashShader;

    [Header("1타 - 돌진")]
    [Tooltip("궤적이 시작점에서 도착점까지 그어지는 시간")]
    [Min(0.02f)] public float dashTime = 0.10f;
    [Tooltip("그어진 뒤 궤적이 남아 있는 시간")]
    [Min(0f)] public float dashLinger = 0.30f;
    public float dashWidth = 0.32f;
    [Range(0, 12)] public int speedLines = 5;
    [Tooltip("넣어두면 돌진하는 동안 이 Transform 을 시작점에서 도착점으로 옮깁니다 (캐릭터 이동을 직접 처리한다면 비워두세요)")]
    public Transform mover;

    [Header("2타 - 다중 참격")]
    [Range(1, 40)] public int slashCount = 12;
    [Tooltip("참격 사이의 간격 (초)")]
    [Min(0.005f)] public float slashInterval = 0.04f;
    public float slashWidth = 0.20f;
    [Tooltip("참격 길이 범위 (최소, 최대)")]
    public Vector2 slashLength = new Vector2(3.5f, 6.0f);
    [Tooltip("경로에 대한 참격의 기울기 범위 (도)")]
    public Vector2 slashAngle = new Vector2(18f, 68f);
    [Tooltip("참격 중심이 경로에서 위아래로 벗어나는 정도")]
    public float slashSpread = 0.45f;
    [Tooltip("N 번째마다 보조색(보라) 참격. 0 이면 사용 안 함")]
    [Range(0, 10)] public int accentEvery = 4;
    [Tooltip("참격이 그어질 때 튀는 작은 파편 수")]
    [Range(0, 8)] public int shardsPerSlash = 2;
    [Tooltip("마지막에 경로를 따라 크게 한 번 베는 마무리 참격")]
    public bool finalSlash = true;
    [Tooltip("그어진 뒤 남아 있는 동안의 굵기 (처음 굵기 대비)")]
    [Range(0.1f, 1f)] public float holdWidth = 0.5f;

    [Header("2타 - 한 번에 소멸")]
    [Tooltip("마지막 참격이 그어진 뒤 깨지기까지 멈춰 있는 시간")]
    [Min(0f)] public float holdBeforeShatter = 0.22f;
    [Tooltip("켜면 자동으로 깨지지 않고 Shatter() 를 부를 때까지 남아 있음 (납도 모션에 맞출 때)")]
    public bool manualShatter = false;
    [Tooltip("모든 참격이 번쩍이며 사라지는 데 걸리는 시간")]
    [Min(0.02f)] public float vanishTime = 0.10f;

    [Header("2타 - 유리 파쇄")]
    public bool glassShatter = true;
    [Tooltip("깨지기 전, 참격 주변에 금이 번져 보이게 함")]
    public bool showCracks = true;
    [Tooltip("조각 하나의 최대 크기 (월드 유닛). 참격 선으로 먼저 가른 뒤, 이보다 큰 조각은 참격의 결을 따라 더 쪼갭니다")]
    [Min(0.2f)] public float glassCellSize = 0.9f;
    [Tooltip("참격에서 이 거리 안쪽의 공간만 깨짐")]
    public float glassRange = 0.7f;
    [Tooltip("유리 면의 불투명도 (배경 굴절을 쓰지 않을 때만 적용)")]
    [Range(0f, 1f)] public float glassFill = 0.12f;
    [Tooltip("조각이 흩어지며 남아 있는 시간 (최소, 최대)")]
    public Vector2 glassLife = new Vector2(0.4f, 0.8f);
    [Tooltip("조각이 흩어지는 속도 (최소, 최대)")]
    public Vector2 glassSpeed = new Vector2(1.0f, 4.5f);
    public float glassGravity = 5f;
    [Tooltip("금이 참격에서 바깥으로 번지는 속도 (유닛당 지연, 초)")]
    public float crackDelay = 0.12f;

    [Header("2타 - 배경 굴절 (유리 파쇄가 켜져 있을 때)")]
    [Tooltip("금 간 조각마다 실제 배경이 어긋나 보이고, 깨질 때 배경을 싣고 날아감. 재생 중에만 장면을 한 번 더 그립니다")]
    public bool refractBackground = true;
    [Tooltip("배경을 찍어올 카메라. 비우면 Main Camera")]
    public Camera sourceCamera;
    [Tooltip("조각에 비친 배경이 참격 선을 따라 미끄러지듯 어긋나는 정도 (화면 너비 대비 비율). 선의 양쪽이 서로 반대로 어긋남")]
    [Range(0f, 0.08f)] public float refractStrength = 0.025f;
    [Tooltip("거울 왜곡: 조각마다 비친 배경이 조금씩 돌아가고 늘어나며, 날아가는 동안 기울기에 따라 비친 상이 미끄러짐. 0 이면 왜곡 없음")]
    [Range(0f, 1f)] public float mirrorDistortion = 0.6f;
    [Tooltip("조각 테두리 선의 진하기. 0 이면 선 없음")]
    [Range(0f, 1f)] public float edgeLineAlpha = 0.14f;
    [Tooltip("조각 테두리 선이 번지는 폭 (픽셀 정도)")]
    [Range(0.5f, 6f)] public float edgeLineWidth = 1.6f;
    [Tooltip("조각에 섞이는 푸른 기운. 0 이면 배경 그대로")]
    [Range(0f, 0.5f)] public float glassTint = 0.03f;
    [Tooltip("배경 캡처 해상도 배율. 낮추면 가볍지만 조각 속 배경이 흐려집니다")]
    [Range(0.25f, 1f)] public float captureScale = 1f;
    [Tooltip("재생 중 이 오브젝트가 옮겨가는 레이어. 배경 캡처에서는 이 레이어가 제외됩니다 (기본 1 = TransparentFX). 메인 카메라는 이 레이어를 그려야 합니다")]
    [Range(0, 31)] public int captureExcludeLayer = 1;
    [Tooltip("조각 속 배경이 위아래로 뒤집혀 보이면 켜세요")]
    public bool flipCaptureY = false;

    [Header("PlayFull 전용")]
    [Tooltip("돌진이 끝난 뒤 2타가 시작되기까지의 간격")]
    [Min(0f)] public float slashDelay = 0.14f;

    [Header("Color")]
    [ColorUsage(false, true)] public Color coreColor = Color.white;
    [ColorUsage(false, true)] public Color midColor = new Color(0.66f, 0.93f, 1f);
    [ColorUsage(false, true)] public Color edgeColor = new Color(0.13f, 0.50f, 1f);
    [ColorUsage(false, true)] public Color accentColor = new Color(0.55f, 0.35f, 1f);
    [Range(0f, 2f)] public float glow = 0.7f;
    [Range(0f, 1f)] public float alpha = 1f;

    [Header("Playback")]
    public bool useUnscaledTime = false;
    [Tooltip("0 이면 쓸 때마다 배치가 달라지고, 다른 값이면 항상 같은 배치")]
    public int seed = 0;
    public FinishAction onFinishedAction = FinishAction.None;

    [Header("Sorting")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 100;

    [Header("Preview")]
    [Tooltip("켜두면 1타 + 2타를 계속 반복 재생 (룩 조정용)")]
    public bool previewLoop = false;
    public Vector2 previewOffset = new Vector2(6.5f, 0f);
    public float previewPause = 0.6f;

    [Header("Events")]
    [Tooltip("돌진 궤적이 도착점에 닿는 순간")]
    public UnityEvent onDashEnd;
    [Tooltip("2타의 참격 하나하나가 그어지는 순간 (다단 히트 데미지, 사운드 등)")]
    public UnityEvent onSlashHit;
    [Tooltip("모든 참격이 사라지며 공간이 깨지는 순간 (마무리 데미지, 카메라 흔들림 등)")]
    public UnityEvent onShatter;
    public UnityEvent onFinished;

    public bool IsPlaying { get { return active; } }

    // ------------------------------------------------------------------ internals

    const float DrawDuration = 0.06f;    // 남는 참격 하나가 끝에서 끝까지 그어지는 시간

    static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly int GlassTintId = Shader.PropertyToID("_GlassTint");
    static readonly int SceneTexId = Shader.PropertyToID("_FlashSceneTex");
    static readonly int RefractId = Shader.PropertyToID("_Refract");
    static readonly int FlipYId = Shader.PropertyToID("_FlipY");

    struct Streak
    {
        public Vector3 a, b;
        public float halfWidth, start, life, bias, accent, wipe, phase;
        public bool hold;          // true = 깨질 때까지 남아 있는 참격
        public bool hitEvent, fired;
    }

    struct Shard       // 참격이 그어질 때 튀는 작은 파편
    {
        public Vector3 pos, vel;
        public float rot, angVel, size, start, life, accent;
    }

    struct Piece       // 금 간 공간을 이루는 조각 (볼록 다각형). 꼭짓점은 pieceVerts[first .. first + count - 1]
    {
        public int first, count;
        public Vector3 c, vel;
        public Vector2 shift;          // 비친 배경이 어긋나는 방향 (참격 선을 따라, 선의 어느 쪽인지에 따라 반대로)
        public float reveal, angVel, tumble, axis, delay, life, accent, near;
        public float warpRot, warpSx, warpSy, sweep;       // 거울 왜곡: 비친 상의 회전, 가로/세로 배율, 기울 때 상이 미끄러지는 거리
    }

    struct Flash
    {
        public Vector3 pos;
        public float rot, size, start, life;
    }

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;
    Material mat;
    bool initialized;
    System.Random rng;

    readonly List<Streak> streaks = new List<Streak>();
    readonly List<Shard> shards = new List<Shard>();
    readonly List<Piece> pieces = new List<Piece>();
    readonly List<Vector3> pieceVerts = new List<Vector3>();
    readonly List<Vector3> ringPos = new List<Vector3>();      // 조각 하나를 그릴 때 쓰는 임시 버퍼
    readonly List<Vector3> ringSrc = new List<Vector3>();
    readonly List<Flash> flashes = new List<Flash>();

    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector2> uv0 = new List<Vector2>();
    readonly List<Vector4> uv1 = new List<Vector4>();
    readonly List<Vector4> uv2 = new List<Vector4>();
    readonly List<Vector4> uv3 = new List<Vector4>();
    readonly List<int> tris = new List<int>();

    // 배경 캡처
    Camera captureCam;
    Camera captureSource;
    RenderTexture captureRT;
    bool capturing;
    int savedLayer;
    bool warnedNoCamera;

    float clock;
    bool active;
    Vector3 lastFrom, lastTo;       // 로컬 평면 좌표
    bool hasPath;
    float dashEndTime = -1f;        // onDashEnd 를 부를 시각 (없으면 음수)
    float pendingSlashTime = -1f;   // PlayFull 에서 2타를 시작할 시각 (없으면 음수)
    float shatterTime = -1f;        // 참격이 한 번에 사라지는 시각 (정해지지 않았으면 음수)
    bool shatterFired;
    Vector3 shatterCenter;
    float moverStart = -1f;
    Vector3 moverFrom, moverTo;
    float previewTimer;

    // ------------------------------------------------------------------ public API

    /// <summary>1타: from 에서 to 까지 돌진 궤적을 그립니다.</summary>
    public void PlayDash(Vector3 from, Vector3 to)
    {
        Init();
        if (mat == null) return;
        EnsureRng();

        from = ToPlane(from);
        to = ToPlane(to);
        lastFrom = from;
        lastTo = to;
        hasPath = true;

        Vector3 d = to - from;
        d.z = 0f;
        float length = d.magnitude;
        if (length < 1e-3f) return;
        Vector3 dir = d / length;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        float now = clock;

        // 본 궤적: 머리(도착점 쪽)가 두껍고 꼬리가 가늘게 빠짐
        float life = dashTime + dashLinger;
        AddStreak(from, to, dashWidth, now, life, 0.78f, 0f, dashTime / life, false, false);

        // 속도선: 경로와 나란한 짧은 선들
        for (int i = 0; i < speedLines; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            Vector3 off = perp * (side * Rand(0.15f, 0.85f));
            float f0 = Rand(0f, 0.4f);
            float f1 = Mathf.Min(f0 + Rand(0.4f, 0.6f), 1.05f);
            AddStreak(from + dir * (length * f0) + off, from + dir * (length * f1) + off,
                      0.10f, now + Rand(0f, dashTime * 0.6f), 0.22f, 0.7f, 0f, 0.4f, false, false);
        }

        AddFlash(to, Rand(0f, 1.57f), 1.6f, now + dashTime, 0.14f);
        dashEndTime = now + dashTime;

        if (mover != null)
        {
            moverStart = now;
            moverFrom = mover.position;
            moverTo = mover.position + transform.TransformVector(to - from);
        }

        Activate();
    }

    /// <summary>1타: 이 오브젝트의 위치에서 바라보는 방향으로 distance 만큼 돌진합니다. (애니메이션 이벤트용)</summary>
    public void PlayDashForward(float distance)
    {
        float sign = transform.lossyScale.x >= 0f ? 1f : -1f;      // 캐릭터를 scale.x 로 뒤집는 경우를 따라감
        Vector3 from = transform.position;
        PlayDash(from, from + transform.right * (sign * distance));
    }

    /// <summary>2타: 방금 돌진한 경로를 여러 번 벱니다.</summary>
    public void PlaySlashes()
    {
        if (!hasPath)
        {
            Debug.LogWarning("[FlashSlashVFX] 아직 돌진 경로가 없습니다. PlayDash 를 먼저 호출하거나 PlaySlashesBetween(from, to) 를 쓰세요.", this);
            return;
        }
        PlaySlashesInPlane(lastFrom, lastTo);
    }

    /// <summary>2타: from - to 경로를 여러 번 벱니다.</summary>
    public void PlaySlashesBetween(Vector3 from, Vector3 to)
    {
        PlaySlashesInPlane(ToPlane(from), ToPlane(to));
    }

    void PlaySlashesInPlane(Vector3 from, Vector3 to)
    {
        Init();
        if (mat == null) return;
        EnsureRng();

        lastFrom = from;
        lastTo = to;
        hasPath = true;

        Vector3 d = to - from;
        d.z = 0f;
        float length = d.magnitude;
        if (length < 1e-3f) return;
        Vector3 dir = d / length;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        float baseAng = Mathf.Atan2(dir.y, dir.x);
        float now = clock;
        Vector3 mid = (from + to) * 0.5f;

        // 이전 2타가 이미 깨지는 중이라면 그 잔해는 정리하고 새로 시작
        if (shatterFired)
        {
            pieces.Clear();
            pieceVerts.Clear();
            for (int i = streaks.Count - 1; i >= 0; i--)
                if (streaks[i].hold) streaks.RemoveAt(i);
        }
        int firstHeld = streaks.Count;

        // 경로 위 위치를 골고루 나누되 순서는 섞어서, 참격이 이리저리 튀는 것처럼 보이게
        int n = Mathf.Max(1, slashCount);
        int[] order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }

        for (int i = 0; i < n; i++)
        {
            float f = 0.12f + 0.76f * (order[i] + Rand(0.1f, 0.9f)) / n;
            Vector3 center = from + dir * (length * f) + perp * Rand(-slashSpread, slashSpread);

            float sign = i % 2 == 0 ? 1f : -1f;                      // 번갈아 반대 기울기 -> X 자로 교차
            float ang = baseAng + Rand(slashAngle.x, slashAngle.y) * Mathf.Deg2Rad * sign;
            if (Rand(0f, 1f) < 0.5f) ang += Mathf.PI;                // 긋는 방향도 무작위
            Vector3 dv = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            float halfLen = Rand(slashLength.x, slashLength.y) * 0.5f;

            float accent = accentEvery > 0 && i % accentEvery == accentEvery - 1 ? 1f : 0f;
            float start = now + i * slashInterval;

            AddStreak(center - dv * halfLen, center + dv * halfLen, slashWidth * Rand(0.8f, 1.2f),
                      start, 1f, 0.5f, accent, 0f, true, true);
            AddShards(center, shardsPerSlash, start + 0.03f, accent, 2f, 6.5f);
        }

        float last = now + (n - 1) * slashInterval;
        if (finalSlash)
        {
            last = now + n * slashInterval + 0.08f;
            AddStreak(from - dir * 0.8f, to + dir * 0.8f, slashWidth * 2.2f, last, 1f, 0.5f, 0f, 0f, true, true);
            AddFlash(mid, 0f, 2.0f, last + 0.02f, 0.14f);
        }

        // 마지막 참격이 다 그어지고 잠깐 멈춘 뒤 전부 한 번에 사라짐
        shatterCenter = mid;
        shatterFired = false;
        shatterTime = manualShatter ? -1f : last + DrawDuration + holdBeforeShatter;

        if (glassShatter)
        {
            BuildGlass(firstHeld, mid);
            BeginCapture();
        }

        Activate();
    }

    /// <summary>1타를 재생하고, 돌진이 끝난 뒤 slashDelay 후에 2타까지 이어서 재생합니다.</summary>
    public void PlayFull(Vector3 from, Vector3 to)
    {
        PlayDash(from, to);
        if (active) pendingSlashTime = clock + dashTime + slashDelay;
    }

    /// <summary>남아 있는 참격을 지금 한 번에 없애고 공간을 깨뜨립니다. (manualShatter 를 켰을 때 사용)</summary>
    public void Shatter()
    {
        if (!active || shatterFired) return;
        if (shatterTime < 0f || shatterTime > clock) shatterTime = clock;
    }

    /// <summary>즉시 숨기고 멈춥니다.</summary>
    public void Stop()
    {
        streaks.Clear();
        shards.Clear();
        pieces.Clear();
        pieceVerts.Clear();
        flashes.Clear();
        dashEndTime = -1f;
        pendingSlashTime = -1f;
        shatterTime = -1f;
        shatterFired = false;
        moverStart = -1f;
        active = false;
        EndCapture();
        if (meshRenderer != null) meshRenderer.enabled = false;
    }

    // ------------------------------------------------------------------ lifecycle

    void Reset()
    {
        // 에디터에서 컴포넌트를 붙일 때 호출됨. 여기서 참조를 걸어두면 빌드에도 셰이더가 포함됩니다.
        flashShader = Shader.Find("VFX/FlashSlash");
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
        if (captureCam != null) Destroy(captureCam.gameObject);
        ReleaseCaptureTexture();
    }

    void LateUpdate()
    {
        // 메인 카메라가 이번 프레임에 움직인 뒤의 상태를 그대로 따라가도록 매 프레임 맞춤
        if (capturing) SyncCaptureCamera();
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
                    PlayFull(transform.position, transform.position + (Vector3)previewOffset);
            }
            return;
        }

        clock += dt;

        // ---- 돌진 중 캐릭터 이동
        if (moverStart >= 0f)
        {
            float k = Mathf.Clamp01((clock - moverStart) / dashTime);
            float inv = 1f - k;
            if (mover != null) mover.position = Vector3.Lerp(moverFrom, moverTo, 1f - inv * inv * inv);
            if (k >= 1f) moverStart = -1f;
        }

        if (dashEndTime >= 0f && clock >= dashEndTime)
        {
            dashEndTime = -1f;
            if (onDashEnd != null) onDashEnd.Invoke();
            if (!active) return;                     // 이벤트 안에서 Stop() 을 부른 경우
        }

        if (pendingSlashTime >= 0f && clock >= pendingSlashTime)
        {
            pendingSlashTime = -1f;
            PlaySlashesInPlane(lastFrom, lastTo);
        }

        // ---- 한 번에 소멸하는 순간
        if (shatterTime >= 0f && !shatterFired && clock >= shatterTime)
        {
            shatterFired = true;
            AddFlash(shatterCenter, 0f, 3.4f, shatterTime, 0.18f);
            if (onShatter != null) onShatter.Invoke();
            if (!active) return;
        }

        // ---- 참격이 그어지는 순간 이벤트, 수명이 끝난 요소 정리
        int hits = 0;
        for (int i = streaks.Count - 1; i >= 0; i--)
        {
            Streak s = streaks[i];
            bool expired = s.hold
                ? shatterFired && clock >= shatterTime + vanishTime
                : clock >= s.start + s.life;
            if (expired) { streaks.RemoveAt(i); continue; }
            if (s.hitEvent && !s.fired && clock >= s.start)
            {
                s.fired = true;
                streaks[i] = s;
                hits++;
            }
        }
        if (onSlashHit != null)
        {
            for (int i = 0; i < hits; i++) onSlashHit.Invoke();
        }
        if (!active) return;                         // 이벤트 안에서 Stop() 을 부른 경우

        for (int i = shards.Count - 1; i >= 0; i--)
            if (clock >= shards[i].start + shards[i].life) shards.RemoveAt(i);
        for (int i = flashes.Count - 1; i >= 0; i--)
            if (clock >= flashes[i].start + flashes[i].life) flashes.RemoveAt(i);
        if (shatterFired)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
                if (clock >= shatterTime + pieces[i].delay + pieces[i].life) pieces.RemoveAt(i);
            if (pieces.Count == 0) pieceVerts.Clear();
        }

        if (streaks.Count == 0 && shards.Count == 0 && flashes.Count == 0 && pieces.Count == 0
            && pendingSlashTime < 0f && dashEndTime < 0f && moverStart < 0f)
        {
            Finish();
            return;
        }

        BuildMesh();
        ApplyMaterial();
    }

    void Finish()
    {
        active = false;
        shatterTime = -1f;
        shatterFired = false;
        EndCapture();
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

        if (flashShader == null) flashShader = Shader.Find("VFX/FlashSlash");
        if (flashShader == null)
        {
            Debug.LogError("[FlashSlashVFX] 'VFX/FlashSlash' 셰이더를 찾을 수 없습니다. FlashSlash.shader 를 프로젝트에 넣고 Flash Shader 칸에 지정하세요.", this);
            enabled = false;
            return;
        }

        mat = new Material(flashShader);
        mat.name = "FlashSlash (Instance)";
        mat.hideFlags = HideFlags.DontSave;

        mesh = new Mesh();
        mesh.name = "FlashSlashMesh";
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

    void EnsureRng()
    {
        // 이펙트가 완전히 끝난 뒤 새로 시작할 때만 난수를 초기화 (seed 가 있으면 매번 같은 배치)
        if (rng == null || !active) rng = seed != 0 ? new System.Random(seed) : new System.Random();
    }

    void Activate()
    {
        if (active) return;
        active = true;
        mesh.Clear();
        meshRenderer.enabled = true;
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

    static Vector3 Polar(float angle, float radius)
    {
        return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
    }

    void AddStreak(Vector3 a, Vector3 b, float halfWidth, float start, float life,
                   float bias, float accent, float wipe, bool hold, bool hitEvent)
    {
        Streak s = new Streak();
        s.a = a; s.b = b;
        s.halfWidth = halfWidth; s.start = start; s.life = Mathf.Max(life, 0.01f);
        s.bias = bias; s.accent = accent; s.wipe = wipe;
        s.phase = Rand(0f, 6.28f);
        s.hold = hold; s.hitEvent = hitEvent; s.fired = false;
        streaks.Add(s);
    }

    void AddShards(Vector3 pos, int count, float start, float accent, float speedMin, float speedMax)
    {
        for (int i = 0; i < count; i++)
        {
            Shard sh = new Shard();
            sh.pos = pos;
            sh.vel = Polar(Rand(0f, 6.283f), Rand(speedMin, speedMax));
            sh.rot = Rand(0f, 6.283f);
            sh.angVel = Rand(-14f, 14f);
            sh.size = Rand(0.12f, 0.30f);
            sh.start = start;
            sh.life = Rand(0.3f, 0.55f);
            sh.accent = accent;
            shards.Add(sh);
        }
    }

    void AddFlash(Vector3 pos, float rot, float size, float start, float life)
    {
        Flash f = new Flash();
        f.pos = pos; f.rot = rot; f.size = size; f.start = start; f.life = Mathf.Max(life, 0.01f);
        flashes.Add(f);
    }

    /// <summary>
    /// 참격의 결대로 공간을 가릅니다.
    ///   1) 참격 하나하나가 지나간 직선으로, 그 참격이 실제로 닿은 범위의 공간을 자릅니다.
    ///   2) 그래도 큰 조각은 가장 가까운 참격과 나란한 방향으로 더 쪼갭니다 (길쭉한 조각은 가로질러 끊음).
    /// 그래서 조각의 변이 전부 참격 선을 따르거나 그와 나란하고, 서로 빈틈없이 맞물립니다.
    /// </summary>
    void BuildGlass(int firstHeld, Vector3 mid)
    {
        Vector2 lo = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 hi = new Vector2(float.MinValue, float.MinValue);
        for (int i = firstHeld; i < streaks.Count; i++)
        {
            if (!streaks[i].hold) continue;
            lo = Vector2.Min(lo, Vector2.Min(streaks[i].a, streaks[i].b));
            hi = Vector2.Max(hi, Vector2.Max(streaks[i].a, streaks[i].b));
        }
        if (lo.x > hi.x) return;
        float margin = Mathf.Max(glassRange, 0f) + 0.2f;
        lo -= new Vector2(margin, margin);
        hi += new Vector2(margin, margin);

        List<List<Vector2>> polys = new List<List<Vector2>>();
        List<Vector2> rect = new List<Vector2>();
        rect.Add(lo); rect.Add(new Vector2(hi.x, lo.y)); rect.Add(hi); rect.Add(new Vector2(lo.x, hi.y));
        polys.Add(rect);

        // 1) 참격 선으로 자르기: 선을 무한히 늘이지 않고, 참격이 닿은 범위(조금 넉넉히)에 있는 조각만
        for (int i = firstHeld; i < streaks.Count; i++)
        {
            if (!streaks[i].hold) continue;
            Vector2 a = streaks[i].a;
            Vector2 ab = (Vector2)streaks[i].b - a;
            float len = ab.magnitude;
            if (len < 1e-4f) continue;
            Vector2 d = ab / len;
            Vector2 n = new Vector2(-d.y, d.x);

            List<List<Vector2>> next = new List<List<Vector2>>();
            for (int k = 0; k < polys.Count; k++)
            {
                float f = Vector2.Dot(PolyCenter(polys[k]) - a, d) / len;
                if (f >= -0.12f && f <= 1.12f) SplitPoly(polys[k], a, n, next);
                else next.Add(polys[k]);
            }
            polys = next;
        }

        // 2) 아직 큰 조각은 가장 가까운 참격의 결을 따라 더 쪼갬
        float cell = Mathf.Max(glassCellSize, 0.2f);
        List<List<Vector2>> done = new List<List<Vector2>>();
        int guard = 0;
        while (polys.Count > 0 && done.Count + polys.Count < MaxPieces && guard < 4000)
        {
            guard++;
            List<Vector2> poly = polys[polys.Count - 1];
            polys.RemoveAt(polys.Count - 1);

            Vector2 c = PolyCenter(poly);
            float best;
            int bi = NearestStreak(c, firstHeld, out best);
            if (bi < 0) continue;

            float rad = 0f;
            for (int k = 0; k < poly.Count; k++) rad = Mathf.Max(rad, (poly[k] - c).magnitude);
            if (best - rad > glassRange) continue;                   // 어느 참격과도 멀리 떨어진 조각은 버림

            Vector2 d = ((Vector2)(streaks[bi].b - streaks[bi].a)).normalized;
            Vector2 n = new Vector2(-d.y, d.x);
            float alMin = float.MaxValue, alMax = float.MinValue, acMin = float.MaxValue, acMax = float.MinValue;
            for (int k = 0; k < poly.Count; k++)
            {
                float al = Vector2.Dot(poly[k] - c, d);
                float ac = Vector2.Dot(poly[k] - c, n);
                alMin = Mathf.Min(alMin, al); alMax = Mathf.Max(alMax, al);
                acMin = Mathf.Min(acMin, ac); acMax = Mathf.Max(acMax, ac);
            }
            float extAlong = alMax - alMin;
            float extAcross = acMax - acMin;
            if (Mathf.Max(extAlong, extAcross) <= cell * 1.5f)
            {
                done.Add(poly);
                continue;
            }

            // 결과 나란하게 가르되, 결을 따라 길쭉한 조각은 비스듬히 가로질러 끊음
            float ang;
            if (extAlong > 1.8f * extAcross) ang = Rand(55f, 90f) * Mathf.Deg2Rad * (Rand(0f, 1f) < 0.5f ? 1f : -1f);
            else ang = Rand(-14f, 14f) * Mathf.Deg2Rad;
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            Vector2 cutDir = new Vector2(d.x * ca - d.y * sa, d.x * sa + d.y * ca);
            Vector2 cutN = new Vector2(-cutDir.y, cutDir.x);

            int before = polys.Count;
            SplitPoly(poly, c + cutN * (Rand(-0.15f, 0.15f) * Mathf.Min(extAlong, extAcross)), cutN, polys);
            if (polys.Count - before < 2)
            {
                while (polys.Count > before) polys.RemoveAt(polys.Count - 1);     // 못 갈랐으면 그대로 확정
                done.Add(poly);
            }
        }
        done.AddRange(polys);

        for (int i = 0; i < done.Count; i++) TryAddPiece(done[i], firstHeld, mid);
    }

    const int MaxPieces = 260;

    static Vector2 PolyCenter(List<Vector2> poly)
    {
        Vector2 s = Vector2.zero;
        for (int k = 0; k < poly.Count; k++) s += poly[k];
        return s / poly.Count;
    }

    static float PolyArea(List<Vector2> poly)
    {
        float s = 0f;
        for (int k = 0; k < poly.Count; k++)
        {
            Vector2 p = poly[k], q = poly[(k + 1) % poly.Count];
            s += p.x * q.y - q.x * p.y;
        }
        return 0.5f * Mathf.Abs(s);
    }

    // 볼록 다각형을 직선(점 pt 를 지나고 법선이 n)으로 자르고, 생긴 조각들을 output 에 추가
    static void SplitPoly(List<Vector2> poly, Vector2 pt, Vector2 n, List<List<Vector2>> output)
    {
        List<Vector2> neg = new List<Vector2>();
        List<Vector2> pos = new List<Vector2>();
        int count = poly.Count;
        for (int k = 0; k < count; k++)
        {
            Vector2 a = poly[k], b = poly[(k + 1) % count];
            float da = Vector2.Dot(a - pt, n);
            float db = Vector2.Dot(b - pt, n);
            if (da >= 0f) pos.Add(a);
            if (da <= 0f) neg.Add(a);
            if ((da > 0f && db < 0f) || (da < 0f && db > 0f))
            {
                Vector2 x = a + (b - a) * (da / (da - db));
                pos.Add(x);
                neg.Add(x);
            }
        }
        if (neg.Count >= 3 && PolyArea(neg) > 1e-5f) output.Add(neg);
        if (pos.Count >= 3 && PolyArea(pos) > 1e-5f) output.Add(pos);
    }

    // c 에서 가장 가까운 (깨질 때까지 남는) 참격. 바늘 끝부분은 너무 가늘어서 제외. 없으면 -1
    int NearestStreak(Vector2 c, int firstHeld, out float best)
    {
        best = float.MaxValue;
        int bestIndex = -1;
        for (int i = firstHeld; i < streaks.Count; i++)
        {
            if (!streaks[i].hold) continue;
            Vector2 a = Vector2.Lerp(streaks[i].a, streaks[i].b, 0.1f);
            Vector2 b = Vector2.Lerp(streaks[i].a, streaks[i].b, 0.9f);
            Vector2 ab = b - a;
            Vector2 pc = c - a;
            float h = Mathf.Clamp01(Vector2.Dot(pc, ab) / Mathf.Max(Vector2.Dot(ab, ab), 1e-6f));
            float dist = (pc - ab * h).magnitude;
            if (dist < best) { best = dist; bestIndex = i; }
        }
        return bestIndex;
    }

    void TryAddPiece(List<Vector2> poly, int firstHeld, Vector3 mid)
    {
        Vector2 c2 = PolyCenter(poly);
        float best;
        int bi = NearestStreak(c2, firstHeld, out best);
        if (bi < 0 || best > glassRange || PolyArea(poly) < 0.006f) return;

        Vector3 c = new Vector3(c2.x, c2.y, 0f);
        Vector2 d = ((Vector2)(streaks[bi].b - streaks[bi].a)).normalized;
        Vector2 n = new Vector2(-d.y, d.x);
        float side = Vector2.Dot(c2 - (Vector2)streaks[bi].a, n) >= 0f ? 1f : -1f;      // 참격 선의 어느 쪽인지

        Vector3 outward = c - mid;
        outward.z = 0f;
        outward = outward.sqrMagnitude > 1e-8f ? outward.normalized : Vector3.up;
        Vector3 along = new Vector3(d.x, d.y, 0f) * side;

        Piece g = new Piece();
        g.first = pieceVerts.Count;
        g.count = poly.Count;
        for (int k = 0; k < poly.Count; k++) pieceVerts.Add(new Vector3(poly[k].x, poly[k].y, 0f));
        g.c = c;
        // 바깥으로 튀면서, 참격 선을 따라 (양쪽이 서로 반대로) 미끄러짐
        g.vel = outward * Rand(glassSpeed.x, glassSpeed.y) + along * Rand(0.5f, 2.2f) + Polar(Rand(0f, 6.283f), Rand(0f, 1f));
        g.shift = d * (side * Rand(0.4f, 1f));
        g.reveal = streaks[bi].start + DrawDuration + best * crackDelay;
        g.angVel = Rand(-5f, 5f);
        g.tumble = Rand(5f, 14f);
        g.axis = Rand(0f, 3.1416f);
        g.delay = Rand(0f, 0.06f);
        g.life = Mathf.Max(Rand(glassLife.x, glassLife.y), 0.05f);
        g.accent = streaks[bi].accent;
        g.near = 1f - best / Mathf.Max(glassRange, 1e-4f);
        g.warpRot = Rand(-8f, 8f) * Mathf.Deg2Rad;
        g.warpSx = Rand(-0.14f, 0.14f);
        g.warpSy = Rand(-0.14f, 0.14f);
        g.sweep = Rand(0.3f, 0.8f);
        pieces.Add(g);
    }

    // ------------------------------------------------------------------ background capture

    /// <summary>
    /// 메인 카메라와 똑같은 보조 카메라로, 이 이펙트만 뺀 장면을 텍스처에 그려 둡니다.
    /// 유리 조각은 그 텍스처를 화면 좌표로 읽어서 "어긋난 배경"을 보여 줍니다.
    /// </summary>
    void BeginCapture()
    {
        if (!refractBackground || pieces.Count == 0) return;

        Camera src = sourceCamera != null ? sourceCamera : Camera.main;
        if (src == null)
        {
            if (!warnedNoCamera)
            {
                warnedNoCamera = true;
                Debug.LogWarning("[FlashSlashVFX] 배경을 찍어올 카메라가 없습니다 (MainCamera 태그 또는 Source Camera 지정). 굴절 없이 재생합니다.", this);
            }
            return;
        }
        captureSource = src;

        if (captureCam == null)
        {
            GameObject go = new GameObject("FlashSlash Capture Camera");
            go.hideFlags = HideFlags.HideAndDontSave;
            captureCam = go.AddComponent<Camera>();
            captureCam.enabled = false;
        }

        int w = Mathf.Max(16, Mathf.RoundToInt(src.pixelWidth * captureScale));
        int h = Mathf.Max(16, Mathf.RoundToInt(src.pixelHeight * captureScale));
        if (captureRT == null || captureRT.width != w || captureRT.height != h)
        {
            ReleaseCaptureTexture();
            captureRT = new RenderTexture(w, h, 24);
            captureRT.name = "FlashSlash Scene Capture";
            captureRT.wrapMode = TextureWrapMode.Clamp;
            captureRT.filterMode = FilterMode.Bilinear;
        }

        if (!capturing)
        {
            savedLayer = gameObject.layer;
            gameObject.layer = captureExcludeLayer;      // 보조 카메라가 이 이펙트를 다시 찍지 않도록
            capturing = true;
        }
        SyncCaptureCamera();
    }

    void SyncCaptureCamera()
    {
        if (captureCam == null || captureSource == null) { capturing = false; return; }

        captureCam.CopyFrom(captureSource);              // 위치, 투영, 배경색 등을 메인 카메라와 똑같이
        captureCam.rect = new Rect(0f, 0f, 1f, 1f);
        captureCam.targetTexture = captureRT;
        captureCam.cullingMask = captureSource.cullingMask & ~(1 << captureExcludeLayer);
        captureCam.depth = captureSource.depth - 10f;    // 메인 카메라보다 먼저 그려지도록
        captureCam.enabled = true;
    }

    void EndCapture()
    {
        if (captureCam != null) captureCam.enabled = false;
        if (capturing)
        {
            capturing = false;
            gameObject.layer = savedLayer;
        }
    }

    void ReleaseCaptureTexture()
    {
        if (captureRT == null) return;
        if (captureCam != null) captureCam.targetTexture = null;
        captureRT.Release();
        Destroy(captureRT);
        captureRT = null;
    }

    void ApplyMaterial()
    {
        mat.SetColor(CoreColorId, coreColor);
        mat.SetColor(MidColorId, midColor);
        mat.SetColor(EdgeColorId, edgeColor);
        mat.SetColor(AccentColorId, accentColor);
        mat.SetFloat(GlowId, glow);
        mat.SetFloat(AlphaId, alpha);
        mat.SetFloat(GlassTintId, glassTint);
        mat.SetTexture(SceneTexId, capturing ? captureRT : null);
        mat.SetFloat(RefractId, capturing ? 1f : 0f);
        mat.SetFloat(FlipYId, flipCaptureY ? 1f : 0f);
    }

    // ------------------------------------------------------------------ mesh

    /// <summary>참격 하나의 현재 상태. 보이지 않으면 false.</summary>
    bool StreakParams(Streak s, out float draw, out float glowK, out float c1, out float wk)
    {
        draw = glowK = c1 = wk = 0f;
        float age = clock - s.start;
        if (age <= 0f) return false;

        if (!s.hold)
        {
            // 그어진 뒤 스스로 가늘어지며 사라지는 선 (돌진 궤적, 속도선)
            float t = age / s.life;
            if (t >= 1f) return false;
            float lk = 1f - SStep(0.25f, 1f, t);
            draw = Mathf.Clamp01(t / Mathf.Max(s.wipe, 0.02f));
            glowK = 1f - SStep(0.4f, 1f, t);
            c1 = Mathf.Lerp(1.2f, 0.45f, SStep(0f, 0.3f, t)) * (1f - 0.5f * SStep(0.6f, 1f, t));
            wk = lk * Mathf.Sqrt(lk);
            return true;
        }

        // 깨질 때까지 남아 있는 참격: 번쩍 그어진 뒤 가는 빛줄기로 가라앉아 유지
        float k = SStep(0f, 0.18f, age);
        wk = Mathf.Lerp(1f, holdWidth, k);
        c1 = Mathf.Lerp(1.2f, 0.5f, k);
        glowK = Mathf.Lerp(1f, 0.75f, k) + 0.08f * Mathf.Sin(clock * 40f + s.phase);

        if (shatterFired)
        {
            // 한 번에 소멸: 전부 하얗게 부풀었다가 순식간에 꺼짐
            float x = (clock - shatterTime) / vanishTime;
            if (x >= 1f) return false;
            float rest = 1f - (x - 0.3f) / 0.7f;
            float pulse = x < 0.3f ? Mathf.Lerp(1f, 2.2f, x / 0.3f) : 2.2f * rest * rest;
            wk = holdWidth * pulse;
            c1 = 1.2f;
            glowK = 1.5f * (1f - x);
        }

        draw = Mathf.Clamp01(age / DrawDuration);
        return true;
    }

    void BuildMesh()
    {
        verts.Clear(); uv0.Clear(); uv1.Clear(); uv2.Clear(); uv3.Clear(); tris.Clear();

        // 굴절로 어긋나는 양은 화면 너비 기준. 세로는 화면 비율만큼 보정해서 가로세로 같은 픽셀 수가 되게
        float aspect = capturing && captureSource != null ? captureSource.aspect : 1f;

        // ---- 공간의 조각 (참격보다 먼저 = 뒤에 그려짐). 뒤의 배경은 그대로 두고, 그 위에 배경을 비춘 조각을 얹음
        float distort = mirrorDistortion;
        for (int i = 0; i < pieces.Count; i++)
        {
            Piece g = pieces[i];
            float age = shatterFired ? clock - shatterTime - g.delay : -1f;

            // 비친 배경이 참격 선을 따라 어긋나는 양 (화면 비율 단위)
            float refractAmount = refractStrength * (0.3f + 0.7f * g.near);
            Vector2 refractUV = new Vector2(g.shift.x * refractAmount, g.shift.y * refractAmount * aspect);

            // 거울 왜곡: 조각 중심을 기준으로 비친 상을 조금 돌리고 늘림
            float wr = g.warpRot * distort;
            float wc = Mathf.Cos(wr), ws = Mathf.Sin(wr);
            float wsx = 1f + g.warpSx * distort, wsy = 1f + g.warpSy * distort;

            ringPos.Clear();
            ringSrc.Clear();

            if (age <= 0f)
            {
                // 아직 깨지기 전: 조각은 제자리에 있고, 비친 배경만 서서히 어긋나고 일그러짐 (금 간 거울)
                if (!showCracks || clock < g.reveal) continue;
                float ramp = Mathf.Clamp01((clock - g.reveal) / 0.08f);
                float lineA = edgeLineAlpha * (0.4f + 0.6f * g.near) * Mathf.Clamp01((clock - g.reveal) / 0.05f);
                for (int k = 0; k < g.count; k++)
                {
                    Vector3 v = pieceVerts[g.first + k];
                    ringPos.Add(v);
                    ringSrc.Add(g.c + Warp(v - g.c, wc, ws, wsx, wsy, ramp));
                }
                AddMirrorPiece(g.c, g.c, refractUV * ramp, new Vector4(3f, 1f, edgeLineWidth, 0f), new Vector4(0f, g.accent, lineA, capturing ? ramp : 1f));
                continue;
            }
            if (age >= g.life) continue;

            // 깨진 뒤: 바깥으로 튀며 떨어지고, 빙글 돌면서 뒤집히듯 납작해졌다 펴짐
            float lifeK = age / g.life;
            Vector3 off = g.vel * ((1f - Mathf.Exp(-2.5f * age)) / 2.5f) + new Vector3(0f, -0.5f * glassGravity * age * age, 0f);
            float rot = g.angVel * age;
            float sx = Mathf.Cos(g.tumble * age);
            float squash = 1f - Mathf.Max(Mathf.Abs(sx), 0.12f);
            Vector3 ax = Polar(g.axis + rot, 1f);
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            float scale = 1f - 0.55f * lifeK;
            Vector3 center = g.c + off;

            // 조각이 기울면 거기 비친 상도 미끄러짐 (기우는 축에 수직인 방향으로)
            Vector3 sweep = new Vector3(-ax.y, ax.x, 0f) * (g.sweep * distort * Mathf.Sin(g.tumble * age));
            for (int k = 0; k < g.count; k++)
            {
                Vector3 v = pieceVerts[g.first + k];
                ringPos.Add(center + Tumble(v - g.c, cs, sn, scale, ax, squash));
                ringSrc.Add(g.c + Warp(v - g.c, wc, ws, wsx, wsy, 1f) + sweep);
            }

            float glint = Mathf.Pow(Mathf.Abs(sx), 12f) * Mathf.Clamp01(age / 0.12f);     // 정면을 향하는 순간 반짝 (깨지는 첫 순간은 제외)
            float fade = 1f - SStep(0.5f, 1f, lifeK);
            AddMirrorPiece(center, g.c + sweep, refractUV,
                           new Vector4(3f, 0.75f + 0.25f * Mathf.Abs(sx), edgeLineWidth, 0.15f * glint),
                           new Vector4(glassFill + 0.35f * glint, g.accent, Mathf.Clamp01(edgeLineAlpha * 1.3f), fade));
        }

        // ---- 참격선: 길이 방향 = uv.x, 폭 방향 = uv.y
        for (int i = 0; i < streaks.Count; i++)
        {
            Streak s = streaks[i];
            float draw, glowK, c1, wk;
            if (!StreakParams(s, out draw, out glowK, out c1, out wk)) continue;

            Vector3 d = s.b - s.a;
            d.z = 0f;
            float length = d.magnitude;
            if (length < 1e-4f) continue;
            Vector3 w = new Vector3(-d.y, d.x, 0f) / length * s.halfWidth;

            Vector4 mode = new Vector4(0f, draw, glowK, 0f);
            Vector4 prm = new Vector4(s.bias, s.accent, c1, wk);
            int q = verts.Count;
            AddVertex(s.a - w, new Vector2(0f, 0f), mode, prm);
            AddVertex(s.a + w, new Vector2(0f, 1f), mode, prm);
            AddVertex(s.b - w, new Vector2(1f, 0f), mode, prm);
            AddVertex(s.b + w, new Vector2(1f, 1f), mode, prm);
            tris.Add(q); tris.Add(q + 1); tris.Add(q + 2);
            tris.Add(q + 2); tris.Add(q + 1); tris.Add(q + 3);
        }

        // ---- 작은 파편: 튀어나가며 감속, 회전하면서 작아짐
        for (int i = 0; i < shards.Count; i++)
        {
            Shard sh = shards[i];
            float age = clock - sh.start;
            if (age <= 0f || age >= sh.life) continue;

            float k = age / sh.life;
            Vector3 pos = sh.pos + sh.vel * ((1f - Mathf.Exp(-3f * age)) / 3f);
            float rot = sh.rot + sh.angVel * age;
            float size = sh.size * (1f - 0.6f * k);
            Vector3 s0 = pos + Polar(rot, size), s1 = pos + Polar(rot + 2.3f, size * 0.55f), s2 = pos + Polar(rot + 4.1f, size * 0.75f);
            AddTriangle(s0, s1, s2, 0.85f, sh.accent, 1f, 1f - k * k, 0.04f);
        }

        // ---- 섬광
        for (int i = 0; i < flashes.Count; i++)
        {
            Flash f = flashes[i];
            float t = (clock - f.start) / f.life;
            if (t < 0f || t >= 1f) continue;

            Vector3 ax = Polar(f.rot, f.size * 0.5f);
            Vector3 ay = new Vector3(-ax.y, ax.x, 0f);
            Vector4 mode = new Vector4(2f, t, 0f, 0f);
            Vector4 prm = new Vector4(0f, 0f, 0f, 1f);
            int q = verts.Count;
            AddVertex(f.pos - ax - ay, new Vector2(0f, 0f), mode, prm);
            AddVertex(f.pos - ax + ay, new Vector2(0f, 1f), mode, prm);
            AddVertex(f.pos + ax - ay, new Vector2(1f, 0f), mode, prm);
            AddVertex(f.pos + ax + ay, new Vector2(1f, 1f), mode, prm);
            tris.Add(q); tris.Add(q + 1); tris.Add(q + 2);
            tris.Add(q + 2); tris.Add(q + 1); tris.Add(q + 3);
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetUVs(2, uv2);
        mesh.SetUVs(3, uv3);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }

    // 조각의 한 꼭짓점: 회전 -> 축소 -> 한 축으로 눌러서 3D 로 뒤집히는 것처럼
    static Vector3 Tumble(Vector3 r, float cs, float sn, float scale, Vector3 axis, float squash)
    {
        Vector3 v = new Vector3(r.x * cs - r.y * sn, r.x * sn + r.y * cs, 0f) * scale;
        return v - axis * (Vector3.Dot(v, axis) * squash);
    }

    // 거울 왜곡: 조각 중심 기준의 위치 r 을 돌리고 늘린 자리. k = 0 이면 그대로, 1 이면 전부 적용
    static Vector3 Warp(Vector3 r, float cs, float sn, float scaleX, float scaleY, float k)
    {
        Vector3 w = new Vector3((r.x * cs - r.y * sn) * scaleX, (r.x * sn + r.y * cs) * scaleY, 0f);
        return r + (w - r) * k;
    }

    // 조각 하나(ringPos / ringSrc 에 담긴 볼록 다각형)를 중심에서 부채꼴로 나눠 그림.
    // uv.x = 중심의 무게 (바깥 변에서 0, 중심에서 1) -> 셰이더가 바깥 변까지의 거리를 픽셀로 재서 테두리 선을 그림
    // centerSrc / ringSrc : 그 조각이 비출 배경의 위치,  refractUV : 화면 비율 단위로 더 어긋나는 양
    void AddMirrorPiece(Vector3 centerPos, Vector3 centerSrc, Vector2 refractUV, Vector4 mode, Vector4 prm)
    {
        int n = ringPos.Count;
        if (n < 3) return;
        int q = verts.Count;
        AddVertex(centerPos, new Vector2(1f, 0f), mode, prm, centerSrc, refractUV);
        for (int k = 0; k < n; k++)
            AddVertex(ringPos[k], new Vector2(0f, 0f), mode, prm, ringSrc[k], refractUV);
        for (int k = 0; k < n; k++)
        {
            tris.Add(q); tris.Add(q + 1 + k); tris.Add(q + 1 + (k + 1) % n);
        }
    }

    // 작은 파편 하나 (삼각형). uv = 무게중심 좌표
    void AddTriangle(Vector3 p0, Vector3 p1, Vector3 p2, float fill, float accent, float rimAlpha, float fade, float rimWidth)
    {
        Vector4 mode = new Vector4(1f, 0f, rimWidth, 0f);
        Vector4 prm = new Vector4(fill, accent, rimAlpha, fade);
        int q = verts.Count;
        AddVertex(p0, new Vector2(1f, 0f), mode, prm);
        AddVertex(p1, new Vector2(0f, 1f), mode, prm);
        AddVertex(p2, new Vector2(0f, 0f), mode, prm);
        tris.Add(q); tris.Add(q + 1); tris.Add(q + 2);
    }

    void AddVertex(Vector3 local, Vector2 uv, Vector4 mode, Vector4 prm)
    {
        AddVertex(local, uv, mode, prm, local, Vector2.zero);
    }

    /// <summary>월드 위치 -> 이 오브젝트의 로컬 XY 평면 (로컬 Z 를 따라 눌러 붙임)</summary>
    Vector3 ToPlane(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        local.z = 0f;
        return local;
    }

    // 메시를 짜는 좌표가 이미 로컬 평면이므로 그대로 씀
    void AddVertex(Vector3 local, Vector2 uv, Vector4 mode, Vector4 prm, Vector3 srcLocal, Vector2 refractUV)
    {
        local.z = 0f;
        srcLocal.z = 0f;
        verts.Add(local);
        uv0.Add(uv);
        uv1.Add(mode);
        uv2.Add(prm);
        uv3.Add(new Vector4(srcLocal.x, srcLocal.y, refractUV.x, refractUV.y));
    }

#if UNITY_EDITOR
    // 메뉴: GameObject > Effects > Flash Slash VFX (일섬)
    [UnityEditor.MenuItem("GameObject/Effects/Flash Slash VFX", false, 10)]
    static void CreateFromMenu(UnityEditor.MenuCommand command)
    {
        GameObject go = new GameObject("FlashSlashVFX");
        FlashSlashVFX fx = go.AddComponent<FlashSlashVFX>();
        fx.previewLoop = true;               // Play 를 누르면 바로 반복 재생되도록. 실제 사용 시에는 끄세요
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Flash Slash VFX");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
