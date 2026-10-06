using UnityEngine;

/// <summary>
/// 3연격 베기 VFX 컨트롤러.
///   1타: 횡베기 (눕힌 타원으로 크게 휘두른 칼이 그대로 머리 위 뒤쪽까지 올라가는, 끊김 없는 한 줄기)
///   2타: 등 뒤에서 머리 위를 거쳐 전방 발치까지, 시전자를 중심으로 약 270도를 크게 내려베기
///   3타: 등 뒤 아래로 당긴 칼이 발치와 전방을 거쳐 머리 뒤통수까지, 둥근 원을 그리며 끊김 없이 올려베기
///
/// 캐릭터의 자식으로 두고(위치 = 몸통 중심) 공격할 때 PlayNext() 또는 PlayHit(1~3) 을 호출하세요.
/// 각 타는 자식 오브젝트(Hit1/Hit2/Hit3) 아래의 SlashVFX 들로 이루어져 있어서,
/// 인스펙터에서 줄기별로 크기·각도·색을 따로 조절할 수 있습니다.
/// </summary>
[DisallowMultipleComponent]
public class ComboSlashVFX : MonoBehaviour
{
    [Header("Shaders (컴포넌트를 붙이면 자동으로 채워짐)")]
    public Shader slashShader;
    public Shader sparkShader;

    [Header("Hits (비어 있으면 기본 3연격을 자동 생성)")]
    [Tooltip("타격별 루트. 그 아래에 있는 모든 SlashVFX 가 함께 재생됩니다")]
    public Transform[] hitRoots = new Transform[0];

    [Header("Combo")]
    [Tooltip("마지막 타격 후 이 시간이 지나면 PlayNext() 가 다시 1타부터 시작")]
    public float comboResetTime = 0.9f;
    public bool useUnscaledTime = false;

    [Header("Auto Build Settings (자동 생성할 때만 적용)")]
    [Tooltip("전체 크기 배율. 기본값은 키 약 1.9 유닛 캐릭터 기준입니다")]
    public float buildScale = 1f;
    public string sortingLayerName = "Default";
    public int sortingOrder = 100;

    [Header("Preview")]
    [Tooltip("켜두면 1 → 2 → 3타를 계속 반복 재생 (룩 조정용)")]
    public bool previewLoop = false;
    public float previewInterval = 0.42f;
    public float previewPause = 0.8f;

    /// <summary>마지막으로 재생한 타격 번호 (1부터, 아직 없으면 0)</summary>
    public int CurrentHit { get; private set; }

    public int HitCount { get { return layers != null ? layers.Length : 0; } }

    /// <summary>줄기 중 하나라도 아직 그려지고 있는지 (풀링용)</summary>
    public bool IsPlaying
    {
        get
        {
            if (layers == null) return false;
            for (int i = 0; i < layers.Length; i++)
                for (int k = 0; k < layers[i].Length; k++)
                    if (layers[i][k] != null && layers[i][k].IsPlaying) return true;
            return false;
        }
    }

    SlashVFX[][] layers;
    int nextIndex;
    float lastHitTime = -999f;
    float previewTimer;

    float Now { get { return useUnscaledTime ? Time.unscaledTime : Time.time; } }

    // ------------------------------------------------------------------ public API

    /// <summary>다음 타격을 재생하고 그 번호(1~3)를 돌려줍니다. 콤보가 끊겼으면 1타부터.</summary>
    public int PlayNext()
    {
        if (Now - lastHitTime > comboResetTime) nextIndex = 0;
        int hitNumber = nextIndex + 1;
        PlayHit(hitNumber);
        return hitNumber;
    }

    /// <summary>지정한 타격(1~3)을 재생합니다. 애니메이션 이벤트에서 바로 호출할 수 있습니다.</summary>
    public void PlayHit(int hitNumber)
    {
        if (layers == null) Setup();

        int index = hitNumber - 1;
        if (index < 0 || index >= layers.Length) return;

        SlashVFX[] hit = layers[index];
        for (int i = 0; i < hit.Length; i++)
        {
            if (hit[i] != null) hit[i].Play();
        }

        CurrentHit = hitNumber;
        nextIndex = hitNumber % layers.Length;
        lastHitTime = Now;
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

    // ------------------------------------------------------------------ lifecycle

    void Reset()
    {
        slashShader = Shader.Find("VFX/AnimeSlash");
        sparkShader = Shader.Find("VFX/SlashSpark");
    }

    void Awake()
    {
        Setup();
    }

    void Update()
    {
        if (!previewLoop || layers == null || layers.Length == 0) return;

        previewTimer -= useUnscaledTime ? Time.unscaledDeltaTime : Prototype.TimeControl.DeltaTime;
        if (previewTimer > 0f) return;

        int hitNumber = nextIndex + 1;
        PlayHit(hitNumber);
        previewTimer = hitNumber >= layers.Length ? previewInterval + previewPause : previewInterval;
    }

    void Setup()
    {
        if (hitRoots == null || hitRoots.Length == 0) BuildDefaultHits();

        layers = new SlashVFX[hitRoots.Length][];
        for (int i = 0; i < hitRoots.Length; i++)
        {
            layers[i] = hitRoots[i] != null
                ? hitRoots[i].GetComponentsInChildren<SlashVFX>(true)
                : new SlashVFX[0];

            // 자식 줄기는 재사용해야 하므로 스스로 사라지지 않게 함
            for (int k = 0; k < layers[i].Length; k++)
            {
                SlashVFX fx = layers[i][k];
                if (fx.onFinished != SlashVFX.FinishAction.None) fx.onFinished = SlashVFX.FinishAction.None;
            }
        }
    }

    // ------------------------------------------------------------------ default combo

    /// <summary>기본 3연격 줄기들을 자식으로 만듭니다. (오른쪽을 보는 캐릭터 기준)</summary>
    [ContextMenu("Build Default Hits")]
    public void BuildDefaultHits()
    {
        if (hitRoots != null && hitRoots.Length > 0)
        {
            Debug.LogWarning("[ComboSlashVFX] 이미 Hit 가 있습니다. 다시 만들려면 자식 오브젝트를 지우고 Hit Roots 를 비운 뒤 실행하세요.", this);
            return;
        }

        // ---- 1타: 횡베기
        Transform h1 = NewHit("Hit1_Horizontal");
        // 등 뒤에서 발치를 지나 정면으로 납작하게 휘두른 뒤(아래쪽 절반), 끊기지 않고 머리 위 뒤쪽까지 크게 올라감(위쪽 절반).
        // 아래쪽 절반만 납작한 달걀 모양 궤적 하나로 그려서 이음매가 없습니다.
        //            이름         중심 위치                    크기                      시작각   길이    두께   시간   지연
        SlashVFX sweep =
        AddLayer(h1, "Sweep",    new Vector2(0.25f, 0f),    new Vector2(5.8f, 4.2f), -178f,  320f, 0.50f, 0.38f, 0.00f);
        sweep.lowerSquash = 0.55f;

        // ---- 2타: 등 뒤 -> 머리 위 -> 전방 -> 발치까지, 시전자를 중심으로 270도 내려베기 (시계 방향)
        // 호의 중심을 시전자 몸에 두고, 바닥 아래로 너무 파고들지 않도록 아래쪽 절반만 조금 눌렀습니다.
        Transform h2 = NewHit("Hit2_Downward");
        SlashVFX down =
        AddLayer(h2, "Slash",    new Vector2(0.25f, 0.55f), new Vector2(5.2f, 5.2f), -170f, -270f, 0.50f, 0.36f, 0.00f);
        down.lowerSquash = 0.75f;

        // ---- 3타: 등 뒤 아래 -> 발치 -> 전방 -> 머리 위 -> 뒤통수까지, 원 하나로 끊김 없이 올려베기
        // (칼을 뒤로 당겨 잡는 궤적을 따로 두지 않고, 호의 시작을 등 뒤까지 늘려 한 줄기로 이었습니다)
        Transform h3 = NewHit("Hit3_Upward");
        AddLayer(h3, "Slash",    new Vector2(0.3f, 0.05f),  new Vector2(4.4f, 4.4f), -170f,  320f, 0.50f, 0.40f, 0.00f);

        hitRoots = new[] { h1, h2, h3 };

#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    Transform NewHit(string hitName)
    {
        GameObject go = new GameObject(hitName);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    SlashVFX AddLayer(Transform parent, string layerName, Vector2 offset, Vector2 size,
                      float arcStart, float arcLength, float thickness, float duration, float delay)
    {
        // 값을 다 채우기 전에 Awake/OnEnable 이 돌지 않도록 꺼둔 채로 만든 뒤 켭니다.
        GameObject go = new GameObject(layerName);
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(offset.x, offset.y, 0f) * buildScale;

        SlashVFX fx = go.AddComponent<SlashVFX>();
        if (slashShader != null) fx.slashShader = slashShader;
        if (sparkShader != null) fx.sparkShader = sparkShader;

        fx.playOnEnable = false;
        fx.onFinished = SlashVFX.FinishAction.None;
        fx.useUnscaledTime = useUnscaledTime;
        fx.size = size * buildScale;
        fx.arcStart = arcStart;
        fx.arcLength = arcLength;
        fx.thickness = thickness;
        fx.duration = duration;
        fx.startDelay = delay;
        fx.sortingLayerName = sortingLayerName;
        fx.sortingOrder = sortingOrder;

        go.SetActive(true);
        return fx;
    }

#if UNITY_EDITOR
    // 메뉴: GameObject > Effects > Slash Combo VFX (3 Hits)
    [UnityEditor.MenuItem("GameObject/Effects/Slash Combo VFX (3 Hits)", false, 10)]
    static void CreateFromMenu(UnityEditor.MenuCommand command)
    {
        GameObject go = new GameObject("ComboSlashVFX");
        ComboSlashVFX combo = go.AddComponent<ComboSlashVFX>();
        combo.previewLoop = true;            // Play 를 누르면 바로 반복 재생되도록. 실제 사용 시에는 끄세요
        combo.BuildDefaultHits();
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Slash Combo VFX");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
