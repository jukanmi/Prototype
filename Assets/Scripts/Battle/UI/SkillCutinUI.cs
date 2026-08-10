using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 스킬 발동 직전에 화면 왼쪽으로 밀려 들어오는 컷인.
    /// 초상화가 먼저, 스킬명이 <see cref="LabelDelay"/>만큼 뒤따라 들어와 층을 이룬다.
    /// </summary>
    public class SkillCutinUI : MonoBehaviour, ISkillCutin
    {
        [Header("타이밍 — 재생 중 인스펙터에서 조절한다")]
        [Tooltip("화면 밖 → 제자리. ease-out.")]
        [SerializeField] private float slideIn = 0.15f;

        [Tooltip("제자리에 머무는 시간. 스킬명을 읽을 여유 — 감이 안 맞으면 여기부터 만진다.")]
        [SerializeField] private float hold = 0.6f;

        [Tooltip("제자리 → 화면 밖. ease-in.")]
        [SerializeField] private float slideOut = 0.15f;

        [Tooltip("스킬명 라벨이 초상화보다 늦게 들어오는 간격. 두 요소가 층을 이뤄 들어온다.")]
        [SerializeField] private float labelDelay = 0.08f;

        public float SlideIn => slideIn;
        public float Hold => hold;
        public float SlideOut => slideOut;
        public float LabelDelay => labelDelay;

        /// <summary>컷인 전체 길이. 늦게 나가는 라벨까지 기다린다.</summary>
        public float Duration => slideIn + hold + slideOut + labelDelay;

        /// <summary>
        /// 경과 시간을 0(화면 밖 대기 위치) ~ 1(등장 위치)로 접는다.
        /// <paramref name="delay"/>만큼 곡선 전체가 뒤로 밀린다 — 라벨이 초상화를 뒤따르게.
        ///
        /// 타이밍을 인자로 받는 static이다. 컴포넌트를 세우지 않고도 곡선 모양을 검증할 수 있고,
        /// 인스펙터에서 값을 바꿔도 계산 자체는 그대로 쓰인다.
        /// </summary>
        public static float SlideAmount(float elapsed, float delay, float slideIn, float hold, float slideOut)
        {
            float t = elapsed - delay;

            if (t <= 0f) return 0f;

            // 구간 길이가 0이면 그 구간의 조건이 곧바로 거짓이 되어 건너뛴다 — 0으로 나누지 않는다.
            if (t < slideIn)
            {
                // ease-out: 빠르게 들어와 부드럽게 멈춘다.
                float x = t / slideIn;
                return 1f - (1f - x) * (1f - x);
            }

            if (t < slideIn + hold) return 1f;

            if (t < slideIn + hold + slideOut)
            {
                // ease-in: 천천히 떨어졌다 빠르게 빠진다.
                float x = (t - slideIn - hold) / slideOut;
                return 1f - x * x;
            }

            return 0f;
        }

        /// <summary>이 컴포넌트의 현재 타이밍으로 진행률을 낸다.</summary>
        public float SlideAmount(float elapsed, float delay)
            => SlideAmount(elapsed, delay, slideIn, hold, slideOut);

        /// <summary>인스펙터에서 음수를 넣어도 곡선이 뒤집히지 않게 막는다.</summary>
        private void OnValidate()
        {
            slideIn = Mathf.Max(0f, slideIn);
            hold = Mathf.Max(0f, hold);
            slideOut = Mathf.Max(0f, slideOut);
            labelDelay = Mathf.Max(0f, labelDelay);
        }

        [Tooltip("초상화 패널 한 변의 길이(1920×1080 기준).")]
        [SerializeField] private float portraitSize = 360f;

        [Tooltip("등장했을 때 초상화 패널의 왼쪽 여백.")]
        [SerializeField] private float portraitShownX = 48f;

        [Tooltip("등장했을 때 스킬명 라벨의 왼쪽 여백. 초상화 오른쪽에 겹친다.")]
        [SerializeField] private float labelShownX = 300f;

        /// <summary>에디트모드에서 Time.deltaTime이 0으로 나와 코루틴이 멈추는 걸 막는 하한.</summary>
        private const float MinStep = 1f / 240f;

        private const float PortraitHiddenX = -400f;
        private const float LabelHiddenX = -660f;

        private RectTransform portraitRect;
        private RectTransform labelRect;
        private Image portraitImage;
        private Text nameLabel;      // 초상화가 없을 때만 켜는 동료 이름
        private Text skillLabel;
        private GameObject panel;

        private float savedScale;

        public bool IsPlaying { get; private set; }

        /// <summary>패널이 화면에 켜져 있는지. RecentHitEnemyHUD.IsVisible과 같은 선례.</summary>
        public bool IsVisible => panel != null && panel.activeSelf;

        /// <summary>초상화가 비었을 때 쓰는 직업 색.</summary>
        public static Color RoleColor(Role role)
        {
            switch (role)
            {
                case Role.Tanker:  return new Color32(0x3D, 0x6E, 0xA8, 0xFF);
                case Role.Warrior: return new Color32(0xB0, 0x48, 0x3C, 0xFF);
                case Role.Archer:  return new Color32(0x3F, 0x8F, 0x5B, 0xFF);
                default:           return new Color32(0x7A, 0x4F, 0xA8, 0xFF);   // Wizard
            }
        }

        private void Awake() => EnsureBuilt();

        /// <summary>
        /// 오브젝트가 비활성화되거나 파괴될 때의 안전판.
        /// ComboExecutor.Abort()가 유일한 Cancel 호출자였는데 실제 프로덕션 코드에는 그 호출자가
        /// 없다(테스트에서만 부른다) — 즉 SetActive(false)나 파괴로 재생 도중 패널이 사라지면
        /// TimeControl.Scale이 0에 묶인 채로 아무도 되돌리지 않아 게임이 영구 정지한다.
        /// RecentHitEnemyHUD도 같은 자리에서 정리한다.
        /// </summary>
        private void OnDisable() => Cancel();

        /// <summary>
        /// 캔버스가 아직 없으면 짓는다.
        /// Awake에 의존하지 않는 이유: 에디트모드 테스트가 <see cref="Play"/>를 곧바로 부르는
        /// 경로가 있어 그때 참조가 비어 있으면 터진다.
        /// </summary>
        private void EnsureBuilt()
        {
            if (panel != null) return;

            BuildUI();
            SetVisible(false);
        }

        /// <summary>
        /// 컷인 재생. <b>호출 즉시</b> 시간을 멈추고 패널을 세운다 —
        /// 반환된 열거자를 펌프하지 않는 호출자도 상태를 관측할 수 있어야 하기 때문
        /// (<see cref="ISkillCutin.Play"/> 계약).
        /// </summary>
        public IEnumerator Play(Ally caster, SkillData data)
        {
            EnsureBuilt();

            // 겹쳐 들어오면 앞의 것을 정리하고 시작한다. 저장한 배율이 덮어써지는 걸 막는다.
            if (IsPlaying) Cancel();

            savedScale = TimeControl.Scale;
            TimeControl.Scale = 0f;
            IsPlaying = true;

            Dress(caster, data);
            SetVisible(true);
            Layout(0f);

            return Animate();
        }

        /// <summary>재생을 즉시 끝낸다. 정지시킨 시간을 반드시 되돌린다.</summary>
        public void Cancel()
        {
            if (!IsPlaying) return;

            IsPlaying = false;
            TimeControl.Scale = savedScale;
            SetVisible(false);
        }

        private IEnumerator Animate()
        {
            float elapsed = 0f;

            while (IsPlaying && elapsed < Duration)
            {
                Layout(elapsed);
                yield return null;

                elapsed += Mathf.Max(MinStep, TimeControl.UnscaledDeltaTime);
            }

            Cancel();
        }

        /// <summary>경과 시간에 맞춰 두 요소의 가로 위치를 다시 그린다. 알파는 건드리지 않는다.</summary>
        private void Layout(float elapsed)
        {
            float p = SlideAmount(elapsed, 0f);
            float l = SlideAmount(elapsed, LabelDelay);

            // EnsureBuilt가 Play보다 먼저 반드시 돌아 portraitRect/labelRect를 채워 둔다 —
            // Dress와 마찬가지로 null 검사 없이 역참조해도 안전하다.
            portraitRect.anchoredPosition = new Vector2(Mathf.Lerp(PortraitHiddenX, portraitShownX, p), 0f);
            labelRect.anchoredPosition = new Vector2(Mathf.Lerp(LabelHiddenX, labelShownX, l), 90f);
        }

        /// <summary>이번 컷인에 쓸 얼굴과 글자를 채운다.</summary>
        private void Dress(Ally caster, SkillData data)
        {
            Sprite portrait = caster != null ? caster.Portrait : null;
            Role role = caster != null ? caster.Role : Role.Wizard;

            portraitImage.sprite = portrait;
            portraitImage.color = portrait != null ? Color.white : RoleColor(role);

            // 그림이 없을 때만 이름을 띄운다 — 누구 차례인지는 알아야 한다.
            nameLabel.gameObject.SetActive(portrait == null);
            nameLabel.text = caster != null ? caster.name : string.Empty;

            // skillName이 비어 있으면 에셋 이름으로 대신한다. 빈 컷인이 뜨는 것보다 낫다.
            skillLabel.text = data == null
                ? string.Empty
                : (string.IsNullOrEmpty(data.skillName) ? data.name : data.skillName);
        }

        private void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("SkillCutinCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // RecentHitEnemyHUD(2) 위, DeckInspectorUI(20) 아래.
            canvas.sortingOrder = 10;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            panel = new GameObject("SkillCutinPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            Stretch(panel.GetComponent<RectTransform>());

            // ── 초상화 ──
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(panel.transform, false);
            portraitImage = portraitGo.GetComponent<Image>();
            portraitImage.raycastTarget = false;

            portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0f, 0.5f);
            portraitRect.anchorMax = new Vector2(0f, 0.5f);
            portraitRect.pivot = new Vector2(0f, 0.5f);
            portraitRect.sizeDelta = new Vector2(portraitSize, portraitSize);
            portraitRect.anchoredPosition = new Vector2(PortraitHiddenX, 0f);

            nameLabel = CreateText(portraitGo.transform, "CasterName", 28, TextAnchor.LowerCenter);
            RectTransform nameRect = nameLabel.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.offsetMin = new Vector2(8f, 14f);
            nameRect.offsetMax = new Vector2(-8f, 60f);

            // ── 스킬명 ──
            skillLabel = CreateText(panel.transform, "SkillName", 44, TextAnchor.MiddleLeft);
            labelRect = skillLabel.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.sizeDelta = new Vector2(640f, 72f);
            labelRect.anchoredPosition = new Vector2(LabelHiddenX, 90f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(Transform parent, string name, int size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            // 밝은 배경 위에서도 읽히도록 검은 외곽선을 깐다.
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            return text;
        }
    }
}
