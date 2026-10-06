using UnityEngine;

/// <summary>
/// 벨트스크롤용 베기 VFX 한 줄기.
/// 빈 오브젝트에 이 컴포넌트만 붙이면 쿼드 메시, 머티리얼, 불꽃 파티클을 알아서 만들고 재생합니다.
///
/// 각도 기준 (오른쪽을 보는 캐릭터): 0 = 정면, 90 = 머리 위, -90 = 발밑, 180 = 등 뒤.
/// arcStart 에서 시작해 arcLength 만큼 쓸고 지나가며, 양수는 반시계(올려베기), 음수는 시계(내려베기) 방향입니다.
/// size 를 가로로 납작하게 잡으면 눕힌 타원이 되어 횡베기처럼 보입니다.
/// lowerSquash 를 1 보다 작게 하면 호의 아래쪽 절반만 납작해져서, 횡으로 벤 뒤 크게 들어 올리는 궤적이 한 줄기로 이어집니다.
/// 오브젝트의 위치 = 호의 중심입니다. 왼쪽을 볼 때는 SetFacing(false) 를 호출하세요.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SlashVFX : MonoBehaviour
{
    public enum FinishAction
    {
        Destroy,   // 재생 후 오브젝트 삭제 (Instantiate 해서 쓰는 경우)
        Disable,   // 재생 후 비활성화 (오브젝트 풀링)
        Loop,      // 계속 반복 (룩 조정용 미리보기)
        None       // 아무것도 안 함 (ComboSlashVFX 의 자식으로 재사용하는 경우)
    }

    public enum Preset { Horizontal, Downward, Upward }

    [Header("Shaders (컴포넌트를 붙이면 자동으로 채워짐)")]
    public Shader slashShader;
    public Shader sparkShader;

    [Header("Playback")]
    [Min(0.05f)] public float duration = 0.35f;
    [Tooltip("Play() 후 이 시간만큼 기다렸다가 나타남 (여러 줄기를 겹쳐 쓸 때)")]
    [Min(0f)] public float startDelay = 0f;
    public bool playOnEnable = true;
    [Tooltip("히트스톱 등으로 Time.timeScale 을 건드려도 정상 속도로 재생")]
    public bool useUnscaledTime = false;
    [Tooltip("0 = 부드럽게. 12~24 로 두면 스프라이트 애니메이션처럼 프레임이 끊겨 보입니다")]
    [Range(0, 60)] public int steppedFps = 0;
    public FinishAction onFinished = FinishAction.Destroy;

    [Header("Shape")]
    [Tooltip("이펙트 전체 크기 (월드 유닛)")]
    public Vector2 size = new Vector2(4.6f, 4.6f);
    [Tooltip("베기 시작 각도. 0 = 정면, 90 = 머리 위, -90 = 발밑")]
    [Range(-180f, 180f)] public float arcStart = -75f;
    [Tooltip("쓸고 지나가는 각도. 양수 = 반시계(올려베기), 음수 = 시계(내려베기)")]
    [Range(-330f, 330f)] public float arcLength = 185f;
    [Range(0.3f, 0.92f)] public float radius = 0.86f;
    [Range(0.05f, 0.8f)] public float thickness = 0.45f;
    [Range(0f, 0.4f)] public float spiral = 0.12f;
    [Tooltip("호의 아래쪽 절반만 세로로 누르는 비율 (1 = 그대로). 아래는 납작한 횡베기, 위는 큰 호로 끊김 없이 이어짐")]
    [Range(0.2f, 1f)] public float lowerSquash = 1f;
    [Tooltip("베는 쪽 끝을 휘어진 뾰족한 모양 대신 곧은 선으로 비스듬히 잘린 면으로 만듦. 사라질 때도 가늘어지지 않고 꼬리부터 줄어들어, 잘린 면이 마지막까지 남음")]
    public bool straightEnd = true;
    [Tooltip("잘린 끝 면의 기울기. 양수 = 바깥 날이 앞섬, 음수 = 안쪽 가장자리가 앞섬, 0 = 호의 중심을 향하는 선")]
    [Range(-0.3f, 0.3f)] public float endSlant = 0.12f;
    [Range(0f, 0.06f)] public float outline = 0f;

    [Header("Color")]
    [ColorUsage(false, true)] public Color coreColor = Color.white;
    [ColorUsage(false, true)] public Color midColor = new Color(0.66f, 0.93f, 1f);
    [ColorUsage(false, true)] public Color edgeColor = new Color(0.13f, 0.50f, 1f);
    public Color outlineColor = new Color(0.03f, 0.08f, 0.30f);
    [Range(0f, 1f)] public float alpha = 1f;

    [Header("Detail")]
    [Tooltip("흰색 띠가 차지하는 비율")]
    [Range(0.1f, 1f)] public float whiteRatio = 0.42f;
    [Range(0f, 1f)] public float streak = 0.75f;
    [Range(4f, 60f)] public float streakDensity = 22f;
    [Range(0f, 2f)] public float glow = 0.6f;
    [Range(0f, 1f)] public float accentLines = 0f;
    [Tooltip("0 = 매끈하게. 스프라이트의 Pixels Per Unit 값을 넣으면 같은 크기의 도트로 그려집니다")]
    [Min(0f)] public float pixelsPerUnit = 0f;

    [Header("Sparks")]
    public bool sparks = true;
    [Range(0, 40)] public int sparkCount = 14;
    public Vector2 sparkSpeed = new Vector2(4f, 11f);
    public Vector2 sparkLifetime = new Vector2(0.15f, 0.35f);
    public Vector2 sparkSize = new Vector2(0.05f, 0.11f);

    [Header("Sorting")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 100;

    public bool IsPlaying { get { return playing || lingering; } }

    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly int ArcStartId = Shader.PropertyToID("_ArcStart");
    static readonly int ArcLengthId = Shader.PropertyToID("_ArcLength");
    static readonly int RadiusId = Shader.PropertyToID("_Radius");
    static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    static readonly int SpiralId = Shader.PropertyToID("_Spiral");
    static readonly int LowerSquashId = Shader.PropertyToID("_LowerSquash");
    static readonly int StraightEndId = Shader.PropertyToID("_StraightEnd");
    static readonly int EndSlantId = Shader.PropertyToID("_EndSlant");
    static readonly int OutlineId = Shader.PropertyToID("_Outline");
    static readonly int WhiteRatioId = Shader.PropertyToID("_WhiteRatio");
    static readonly int StreakId = Shader.PropertyToID("_Streak");
    static readonly int StreakFreqId = Shader.PropertyToID("_StreakFreq");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    static readonly int LinesId = Shader.PropertyToID("_Lines");
    static readonly int PixelGridId = Shader.PropertyToID("_PixelGrid");

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;
    Material slashMat;
    Material sparkMat;
    ParticleSystem sparkPs;
    Vector2 builtSize;

    bool initialized;
    bool playing;
    bool lingering;   // 본체는 끝났고 불꽃이 사라지길 기다리는 중
    float time;
    int sparksEmitted;

    // ------------------------------------------------------------------ public API

    /// <summary>프리팹을 생성해서 바로 재생합니다.</summary>
    public static SlashVFX Spawn(SlashVFX prefab, Vector3 position, bool facingRight)
    {
        SlashVFX fx = Instantiate(prefab, position, Quaternion.identity);
        fx.SetFacing(facingRight);
        return fx;
    }

    /// <summary>캐릭터가 보는 방향에 맞춰 좌우 반전합니다.</summary>
    public void SetFacing(bool facingRight)
    {
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (facingRight ? 1f : -1f);
        transform.localScale = s;
    }

    /// <summary>처음부터 다시 재생합니다.</summary>
    public void Play()
    {
        Init();
        if (slashMat == null) return;

        time = -Mathf.Max(0f, startDelay);
        sparksEmitted = 0;
        playing = true;
        lingering = false;

        ApplyMaterial(0f);
        meshRenderer.enabled = time >= 0f;

        if (sparkPs != null)
        {
            sparkPs.Clear(true);
            sparkPs.Play(true);
        }
    }

    /// <summary>모양 값을 기본 프리셋으로 바꿉니다 (색은 건드리지 않음).</summary>
    public void ApplyPreset(Preset preset)
    {
        switch (preset)
        {
            case Preset.Horizontal:   // 몸 뒤쪽에서 앞으로 크게 휘두르는 횡베기 (납작한 타원)
                size = new Vector2(5.8f, 2.3f); arcStart = -178f; arcLength = 190f; thickness = 0.62f; duration = 0.26f; lowerSquash = 1f; endSlant = 0.12f;
                break;
            case Preset.Downward:     // 머리 뒤에서 머리 위를 거쳐 전방 아래로
                size = new Vector2(5.0f, 5.0f); arcStart = 125f; arcLength = -225f; thickness = 0.50f; duration = 0.32f; lowerSquash = 1f; endSlant = 0.12f;
                break;
            case Preset.Upward:       // 뒤쪽 아래에서 전방을 거쳐 곧게 위로 (세로로 긴 타원, 머리 위에서 끝남). 끝 면은 앞으로 기운 비스듬한 선
                size = new Vector2(3.8f, 5.8f); arcStart = -120f; arcLength = 215f; thickness = 0.50f; duration = 0.36f; lowerSquash = 0.6f; endSlant = -0.05f;
                break;
        }
    }

    [ContextMenu("Preset: Horizontal (횡베기)")]
    void PresetHorizontal() { ApplyPresetFromMenu(Preset.Horizontal); }

    [ContextMenu("Preset: Downward (내려베기)")]
    void PresetDownward() { ApplyPresetFromMenu(Preset.Downward); }

    [ContextMenu("Preset: Upward (올려베기)")]
    void PresetUpward() { ApplyPresetFromMenu(Preset.Upward); }

    void ApplyPresetFromMenu(Preset preset)
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Apply Slash Preset");
#endif
        ApplyPreset(preset);
    }

    // ------------------------------------------------------------------ lifecycle

    void Reset()
    {
        // 에디터에서 컴포넌트를 붙일 때 호출됨. 여기서 참조를 걸어두면 빌드에도 셰이더가 포함됩니다.
        slashShader = Shader.Find("VFX/AnimeSlash");
        sparkShader = Shader.Find("VFX/SlashSpark");
    }

    void Awake()
    {
        Init();
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    void OnDisable()
    {
        playing = false;
        lingering = false;
        if (meshRenderer != null) meshRenderer.enabled = false;
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (slashMat != null) Destroy(slashMat);
        if (sparkMat != null) Destroy(sparkMat);
    }

    void Update()
    {
        // 불꽃 파티클은 유니티 시계로 돈다. 본체와 같이 불릿타임에 멈추도록 게임 시계 배율을 따라감
        if (sparkPs != null && !useUnscaledTime)
        {
            ParticleSystem.MainModule m = sparkPs.main;
            m.simulationSpeed = Prototype.TimeControl.Scale;
        }

        if (lingering)
        {
            if (sparkPs == null || sparkPs.particleCount == 0) Finish();
            return;
        }
        if (!playing) return;

        time += useUnscaledTime ? Time.unscaledDeltaTime : Prototype.TimeControl.DeltaTime;
        if (time < 0f) return;                       // startDelay 대기 중
        if (!meshRenderer.enabled) meshRenderer.enabled = true;

        float dur = Mathf.Max(duration, 0.0001f);
        float t = Mathf.Clamp01(time / dur);

        float shown = t;
        if (steppedFps > 0 && t < 1f)
        {
            float steppedTime = Mathf.Floor(time * steppedFps) / steppedFps;
            shown = Mathf.Clamp01(steppedTime / dur);
        }

        ApplyMaterial(shown);
        EmitSparks(t);

        if (t >= 1f)
        {
            playing = false;
            lingering = true;
            meshRenderer.enabled = false;
        }
    }

    void Finish()
    {
        lingering = false;
        switch (onFinished)
        {
            case FinishAction.Destroy: Destroy(gameObject); break;
            case FinishAction.Disable: gameObject.SetActive(false); break;
            case FinishAction.Loop: Play(); break;
        }
    }

    // ------------------------------------------------------------------ setup

    void Init()
    {
        if (initialized) return;
        initialized = true;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (slashShader == null) slashShader = Shader.Find("VFX/AnimeSlash");
        if (slashShader == null)
        {
            Debug.LogError("[SlashVFX] 'VFX/AnimeSlash' 셰이더를 찾을 수 없습니다. AnimeSlash.shader 를 프로젝트에 넣고 Slash Shader 칸에 지정하세요.", this);
            enabled = false;
            return;
        }

        slashMat = new Material(slashShader);
        slashMat.name = "AnimeSlash (Instance)";
        slashMat.hideFlags = HideFlags.DontSave;

        BuildMesh();

        meshRenderer.sharedMaterial = slashMat;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder;
        meshRenderer.enabled = false;

        if (sparks) BuildSparks();
    }

    void BuildMesh()
    {
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "SlashQuad";
            mesh.hideFlags = HideFlags.DontSave;
        }

        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        mesh.Clear();
        mesh.vertices = new[]
        {
            new Vector3(-hx, -hy, 0f), new Vector3(hx, -hy, 0f),
            new Vector3(-hx, hy, 0f), new Vector3(hx, hy, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
        builtSize = size;
    }

    void BuildSparks()
    {
        if (sparkShader == null) sparkShader = Shader.Find("VFX/SlashSpark");
        if (sparkShader == null)
        {
            Debug.LogWarning("[SlashVFX] 'VFX/SlashSpark' 셰이더가 없어 불꽃 파티클을 생략합니다.", this);
            return;
        }

        sparkMat = new Material(sparkShader);
        sparkMat.name = "SlashSpark (Instance)";
        sparkMat.hideFlags = HideFlags.DontSave;

        GameObject go = new GameObject("Sparks");
        go.transform.SetParent(transform, false);
        sparkPs = go.AddComponent<ParticleSystem>();
        sparkPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 자동 방출은 끄고, EmitSparks() 에서 칼끝 위치에 맞춰 직접 방출합니다.
        ParticleSystem.MainModule main = sparkPs.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        main.maxParticles = 128;
        main.useUnscaledTime = useUnscaledTime;

        ParticleSystem.EmissionModule emission = sparkPs.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = sparkPs.shape;
        shape.enabled = false;

        // 빠르게 튀어나갔다가 급격히 느려지도록
        ParticleSystem.LimitVelocityOverLifetimeModule limit = sparkPs.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 1.5f;
        limit.dampen = 0.12f;

        ParticleSystem.SizeOverLifetimeModule sol = sparkPs.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Stretch;
        psr.lengthScale = 2.5f;
        psr.velocityScale = 0.06f;
        psr.cameraVelocityScale = 0f;
        psr.sharedMaterial = sparkMat;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows = false;
        psr.sortingLayerName = sortingLayerName;
        psr.sortingOrder = sortingOrder + 1;
    }

    // ------------------------------------------------------------------ per-frame

    void ApplyMaterial(float progress)
    {
        if (builtSize != size) BuildMesh();

        slashMat.SetFloat(ProgressId, progress);
        slashMat.SetColor(CoreColorId, coreColor);
        slashMat.SetColor(MidColorId, midColor);
        slashMat.SetColor(EdgeColorId, edgeColor);
        slashMat.SetColor(OutlineColorId, outlineColor);
        slashMat.SetFloat(AlphaId, alpha);
        slashMat.SetFloat(ArcStartId, arcStart);
        slashMat.SetFloat(ArcLengthId, arcLength);
        slashMat.SetFloat(RadiusId, radius);
        slashMat.SetFloat(ThicknessId, thickness);
        slashMat.SetFloat(SpiralId, spiral);
        slashMat.SetFloat(LowerSquashId, lowerSquash);
        slashMat.SetFloat(StraightEndId, straightEnd ? 1f : 0f);
        slashMat.SetFloat(EndSlantId, endSlant);
        slashMat.SetFloat(OutlineId, outline);
        slashMat.SetFloat(WhiteRatioId, whiteRatio);
        slashMat.SetFloat(StreakId, streak);
        slashMat.SetFloat(StreakFreqId, streakDensity);
        slashMat.SetFloat(GlowId, glow);
        slashMat.SetFloat(LinesId, accentLines);
        slashMat.SetVector(PixelGridId, pixelsPerUnit > 0f
            ? new Vector4(size.x * pixelsPerUnit, size.y * pixelsPerUnit, 0f, 0f)
            : Vector4.zero);
    }

    void EmitSparks(float t)
    {
        if (sparkPs == null || sparkCount <= 0) return;

        // 셰이더의 head 곡선과 동일: 칼끝이 지나가는 순간 그 자리에서 불꽃이 튐
        float x = Mathf.Clamp01(t / 0.40f);
        float head = 1f - (1f - x) * (1f - x) * (1f - x);
        int target = Mathf.RoundToInt(sparkCount * head);
        float sweepSign = arcLength >= 0f ? 1f : -1f;

        while (sparksEmitted < target)
        {
            sparksEmitted++;

            float s = Mathf.Clamp01(head - Random.Range(0f, 0.12f));
            float ang = (arcStart + arcLength * s) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));        // 바깥 방향
            Vector2 tan = new Vector2(-dir.y, dir.x) * sweepSign;              // 베는 진행 방향
            float ySquash = dir.y < 0f ? lowerSquash : 1f;                     // 아래쪽 절반은 눌린 궤적 위에서

            float rr = radius * (1f - spiral * (1f - s)) * Random.Range(0.80f, 1.02f);
            Vector3 localPos = new Vector3(dir.x * rr * size.x * 0.5f, dir.y * rr * size.y * 0.5f * ySquash, 0f);

            Vector2 v2 = tan * Random.Range(0.6f, 1f) + dir * Random.Range(0.1f, 0.7f);
            Vector3 worldDir = transform.TransformVector(new Vector3(v2.x * size.x, v2.y * size.y * ySquash, 0f));
            if (worldDir.sqrMagnitude < 1e-8f) continue;
            worldDir.Normalize();

            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            ep.position = transform.TransformPoint(localPos);
            ep.velocity = worldDir * Random.Range(sparkSpeed.x, sparkSpeed.y);
            ep.startLifetime = Random.Range(sparkLifetime.x, sparkLifetime.y);
            ep.startSize = Random.Range(sparkSize.x, sparkSize.y);
            ep.startColor = Color.Lerp(midColor, coreColor, Random.value);
            sparkPs.Emit(ep, 1);
        }
    }

#if UNITY_EDITOR
    // 메뉴: GameObject > Effects > Slash VFX (Upward)
    [UnityEditor.MenuItem("GameObject/Effects/Slash VFX (Upward)", false, 10)]
    static void CreateFromMenu(UnityEditor.MenuCommand command)
    {
        GameObject go = new GameObject("SlashVFX_Upward");
        SlashVFX fx = go.AddComponent<SlashVFX>();
        fx.ApplyPreset(Preset.Upward);
        fx.onFinished = FinishAction.Loop;   // 씬에 놓고 바로 확인할 수 있도록 반복 재생
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Slash VFX");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
