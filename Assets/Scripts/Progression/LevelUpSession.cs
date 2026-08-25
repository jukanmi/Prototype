using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Prototype.YG;

namespace Prototype
{
    /// <summary>
    /// 라운드를 클리어한 직후 뜨는 <b>레벨업 · 카드 획득</b> 화면.
    ///
    /// 두 장으로 돈다 — ① 단계를 고르는 판, ② 카드 세 장 중 하나를 고르는 판.
    /// 카드를 고르면 ①로 돌아오므로 남은 경험치로 <b>몇 번이든</b> 이어서 올릴 수 있다.
    /// [나가기]를 눌러야 세션이 닫히고, 그때서야 아레나 문이 열린다.
    ///
    /// <c>RebindUI</c>가 세워 둔 모달 패턴을 그대로 따른다 — 씬·프리팹 배선 없이 코드로만 짓고,
    /// 열려 있는 동안 <see cref="PlayerInputController.GameplaySuspended"/>로 조작을 잠근다.
    ///
    /// <b><see cref="TimeControl.Scale"/>은 건드리지 않는다.</b> 그 값의 주인은 불릿타임이라
    /// 여기서 0을 넣으면 소유자가 둘로 갈린다. 이 시점엔 적이 전부 죽어 있어 멈출 것도 없다.
    /// </summary>
    public class LevelUpSession : MonoBehaviour
    {
        /// <summary>StageResultUI · RebindUI(200)보다 위, SceneLoader 페이드(999)보다 아래.</summary>
        private const int SortingOrder = 210;

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color PanelColor = new Color(0.11f, 0.12f, 0.15f, 0.98f);
        private static readonly Color TierColor = new Color(0.24f, 0.30f, 0.38f);
        private static readonly Color ExitColor = new Color(0.30f, 0.32f, 0.36f);
        private static readonly Color TitleColor = new Color(1f, 0.86f, 0.45f);
        private static readonly Color SubColor = new Color(0.75f, 0.78f, 0.82f);
        private static readonly Color HintColor = new Color(1f, 0.82f, 0.4f);

        private const float PanelWidth = 1120f;
        private const float PanelHeight = 620f;

        private const float TierWidth = 260f;
        private const float TierHeight = 190f;
        private const float TierGap = 40f;

        private const float OfferGap = 40f;

        // ── 씬에 하나 ────────────────────────────────────

        public static LevelUpSession Instance { get; private set; }

        /// <summary>화면이 떠 있는가. 아레나 · 웨이브 · 승패 판정이 이 값 하나를 본다.</summary>
        public static bool IsOpen => Instance != null && Instance.isOpen;

        /// <summary>
        /// 라운드가 끝났다. <b>쓸 수 있는 경험치가 있을 때만</b> 연다 —
        /// 버튼이 전부 잠긴 판을 띄우고 [나가기]만 누르게 하는 것은 방해일 뿐이다.
        ///
        /// 씬에 컴포넌트가 없으면 <b>여기서 만든다.</b> 이 프로젝트의 전투 UI는 전부 씬 배선 0으로
        /// 스스로 지어지는데(<c>ComboBoardUI</c> · <c>RebindUI</c>), 이것만 배선을 요구하면
        /// 이미 저장된 스테이지 씬 아홉 개에서 레벨업이 조용히 안 뜬다.
        /// </summary>
        public static void RequestOpen()
        {
            if (Instance == null) Instance = Create();
            if (Instance == null || Instance.isOpen) return;

            Instance.Open();
        }

        private static LevelUpSession Create()
        {
            var go = new GameObject(nameof(LevelUpSession), typeof(RectTransform));
            return go.AddComponent<LevelUpSession>();
        }

        // ── 상태 ─────────────────────────────────────────

        [Tooltip("비우면 같은 GameObject 또는 씬에서 찾는다. 얻은 카드를 지금 판의 덱에 들이는 통로다.")]
        [SerializeField] private BulletTimeController bulletTime;

        private GameObject root;
        private GameObject tierPanel;
        private GameObject offerPanel;

        private Text headerText;
        private Text offerTitle;
        private Text offerHint;

        private readonly Button[] tierButtons = new Button[ExpRules.TierCount];
        private readonly Text[] tierCostLabels = new Text[ExpRules.TierCount];
        private readonly Text[] tierChanceLabels = new Text[ExpRules.TierCount];

        private readonly CardOfferView[] offerViews = new CardOfferView[CardOfferRules.OfferCount];
        private List<CardOffer> offers = new List<CardOffer>();

        private bool isOpen;

        /// <summary>이번 선택지를 뽑은 단계. 합성 표시와 안내 문구가 이 값을 읽는다.</summary>
        private int pendingTier = -1;

        /// <summary>지금 굴러가는 런. <c>GameManager</c>가 없어도 null이 아니다.</summary>
        private static RunProgression Run => RunProgression.Current;

        // ── 수명 ─────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            if (bulletTime == null) bulletTime = GetComponent<BulletTimeController>();
            if (bulletTime == null) bulletTime = FindAnyObjectByType<BulletTimeController>();

            SimpleUI.EnsureEventSystem();
            SimpleUI.BuildCanvas(gameObject, SortingOrder);

            BuildUI();
            root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            // 화면이 사라지는데 잠금이 남아 있으면 조작이 영영 안 돌아온다.
            if (isOpen) SetSuspended(false);

            Instance = null;
        }

        // ── 열고 닫기 ────────────────────────────────────

        private void Open()
        {
            RunProgression run = Run;

            if (run.HighestAffordableTier < 0)
            {
                BattleLog.Log(LogCategory.State,
                    $"레벨업 건너뜀 — 경험치 {run.Exp}, 1단계에 {run.CostOf(0)} 필요", this);
                return;
            }

            isOpen = true;
            root.SetActive(true);
            SetSuspended(true);

            ShowTiers();
        }

        private void Close()
        {
            isOpen = false;
            pendingTier = -1;

            root.SetActive(false);
            SetSuspended(false);
        }

        private static void SetSuspended(bool value)
        {
            PlayerInputController input = PlayerInputController.Instance;
            if (input != null) input.GameplaySuspended = value;
        }

        // ── ① 단계 고르기 ────────────────────────────────

        private void ShowTiers()
        {
            pendingTier = -1;

            tierPanel.SetActive(true);
            offerPanel.SetActive(false);

            RunProgression run = Run;

            headerText.text = $"<b>Lv.{run.Level}</b>    경험치 <b>{run.Exp}</b>";

            for (int tier = 0; tier < ExpRules.TierCount; tier++)
            {
                int cost = run.CostOf(tier);
                bool can = run.CanLevelUp(tier);

                tierCostLabels[tier].text = $"{cost} EXP";
                tierChanceLabels[tier].text = $"황금 {ExpRules.GoldenChanceOf(tier) * 100f:0}%";

                tierButtons[tier].interactable = can;

                // 잠긴 단계는 색까지 죽인다. interactable만으로는 어떤 게 잠겼는지 잘 안 읽힌다.
                var image = tierButtons[tier].targetGraphic as Image;
                if (image != null) image.color = can ? TierColor : TierColor * 0.45f;
            }
        }

        /// <summary>단계 버튼을 눌렀다. 경험치를 태우고 선택지를 짠다.</summary>
        private void PickTier(int tier)
        {
            RunProgression run = Run;
            if (!run.Spend(tier)) return;

            pendingTier = tier;

            // 모집단은 <b>파티</b>가 정한다. 지금 든 카드에서 긁으면 0장으로 시작하는
            // 테스트 모드에서 뽑을 것이 없어 레벨업이 헛돈다 —
            // 카드가 없어서 레벨업을 하는데 카드가 없어서 못 받는 꼴이 된다.
            List<SkillData> pool = bulletTime != null
                ? bulletTime.AvailableSkillPool()
                : SkillCatalog.Pool(null, run.Cards);

            // 합성 판정만 지금 든 카드를 본다. 같은 카드를 이미 두 장 쥐고 있는지는 덱의 사실이다.
            offers = CardOfferRules.Build(pool, run.Cards, tier, () => Random.value);

            BattleLog.Log(LogCategory.State,
                $"<b>레벨업</b> {ExpRules.TierNumber(tier)}단계 — Lv.{run.Level} · " +
                $"잔여 경험치 {run.Exp} · 선택지 {offers.Count}장", this);

            if (offers.Count == 0)
            {
                // 뽑을 스킬이 하나도 없다. 레벨은 이미 올랐으므로 그대로 ①로 돌린다.
                BattleLog.Warn(LogCategory.State,
                    "선택지를 짤 스킬이 없다 — Assets/Data/Resources/SkillCatalog.asset을 확인할 것", this);
                ShowTiers();
                return;
            }

            ShowOffers();
        }

        // ── ② 카드 고르기 ────────────────────────────────

        private void ShowOffers()
        {
            tierPanel.SetActive(false);
            offerPanel.SetActive(true);

            offerTitle.text = $"<b>{ExpRules.TierNumber(pendingTier)}단계</b>  —  황금 확률 " +
                              $"{ExpRules.GoldenChanceOf(pendingTier) * 100f:0}%";

            bool anyFuse = false;

            for (int i = 0; i < offerViews.Length; i++)
            {
                if (i >= offers.Count) { offerViews[i].Hide(); continue; }

                CardOffer offer = offers[i];
                offerViews[i].Show(in offer);
                offerViews[i].SetInteractable(true);

                anyFuse |= offer.fuses;
            }

            Layout(offerViews, offers.Count, CardOfferView.Width, OfferGap, -30f);

            offerHint.text = anyFuse
                ? "★ 표시는 합성 — 고르면 들고 있던 같은 카드 2장이 사라지고 황금 1장이 된다"
                : "한 장을 고른다";
        }

        private void PickCard(int index)
        {
            if (index < 0 || index >= offers.Count) return;

            RunProgression run = Run;

            CardOffer pick = offers[index];

            // 고른 뒤 두 번 눌리지 않게 곧바로 잠근다. 한 프레임에 두 번 들어오면
            // 카드가 두 장 들어오고 합성이 두 번 일어난다.
            for (int i = 0; i < offerViews.Length; i++) offerViews[i].SetInteractable(false);

            ComboCard granted = run.Grant(in pick);
            if (granted == null) { ShowTiers(); return; }

            // 런 덱에 들어간 그 카드를 <b>손패에 바로</b> 꽂는다. 합성이면 재료 두 장도
            // 지금 판(덱 · 손패 · 버린 더미)에서 같이 걷어낸다.
            if (bulletTime != null)
                bulletTime.GrantCard(granted,
                    pick.fuses ? pick.data : null,
                    CardOfferRules.FuseCopies - 1);

            BattleLog.Log(LogCategory.State,
                $"카드 선택 — {pick.data.skillName}" +
                (pick.fuses ? " <color=#FFD166>(합성 → 황금)</color>"
                            : pick.golden ? " <color=#FFD166>(황금)</color>" : "") +
                $" | 런 덱 {run.Cards.Count}장", this);

            offers.Clear();
            ShowTiers();
        }

        // ── 짓기 ─────────────────────────────────────────

        private void BuildUI()
        {
            root = SimpleUI.CreateImage(transform, "LevelUpRoot", BackdropColor);
            SimpleUI.Stretch((RectTransform)root.transform);

            // 뒤쪽 UI(손패 등)로 클릭이 새지 않게 막는다.
            root.GetComponent<Image>().raycastTarget = true;

            BuildTierPanel();
            BuildOfferPanel();
        }

        private void BuildTierPanel()
        {
            tierPanel = Panel("TierPanel", PanelWidth, PanelHeight * 0.72f);

            Text title = SimpleUI.CreateText(tierPanel.transform, "Title", "레벨 업", 44, TitleColor);
            SimpleUI.Place(title, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(600f, 56f));

            headerText = SimpleUI.CreateText(tierPanel.transform, "Header", "", 26, SubColor);
            headerText.supportRichText = true;
            SimpleUI.Place(headerText, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(700f, 36f));

            float span = ExpRules.TierCount * TierWidth + (ExpRules.TierCount - 1) * TierGap;
            float left = -span * 0.5f + TierWidth * 0.5f;

            for (int tier = 0; tier < ExpRules.TierCount; tier++)
            {
                int captured = tier;

                Button button = SimpleUI.CreateButton(tierPanel.transform, $"Tier_{tier}", "", TierColor,
                    new Vector2(TierWidth, TierHeight), 26);
                button.onClick.AddListener(() => PickTier(captured));

                // CreateButton이 끼워 준 기본 라벨은 세 줄을 담지 못한다. 끄고 직접 쌓는다.
                Text stock = button.GetComponentInChildren<Text>();
                if (stock != null) stock.gameObject.SetActive(false);

                SimpleUI.Place(button, new Vector2(0.5f, 0.5f),
                    new Vector2(left + tier * (TierWidth + TierGap), -20f),
                    new Vector2(TierWidth, TierHeight));

                Text name = SimpleUI.CreateText(button.transform, "Name",
                    $"{ExpRules.TierNumber(tier)}단계", 30, Color.white);
                SimpleUI.Place(name, new Vector2(0.5f, 0.5f), new Vector2(0f, 46f), new Vector2(TierWidth, 40f));

                tierCostLabels[tier] = SimpleUI.CreateText(button.transform, "Cost", "", 24, HintColor);
                SimpleUI.Place(tierCostLabels[tier], new Vector2(0.5f, 0.5f), new Vector2(0f, 2f),
                    new Vector2(TierWidth, 34f));

                tierChanceLabels[tier] = SimpleUI.CreateText(button.transform, "Chance", "", 20, SubColor);
                SimpleUI.Place(tierChanceLabels[tier], new Vector2(0.5f, 0.5f), new Vector2(0f, -38f),
                    new Vector2(TierWidth, 30f));

                tierButtons[tier] = button;
            }

            Button exit = SimpleUI.CreateButton(tierPanel.transform, "Exit", "나가기", ExitColor,
                new Vector2(220f, 58f), 24);
            exit.onClick.AddListener(Close);
            SimpleUI.Place(exit, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(220f, 58f));
        }

        private void BuildOfferPanel()
        {
            offerPanel = Panel("OfferPanel", PanelWidth, PanelHeight);

            offerTitle = SimpleUI.CreateText(offerPanel.transform, "Title", "", 34, TitleColor);
            offerTitle.supportRichText = true;
            SimpleUI.Place(offerTitle, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(900f, 48f));

            for (int i = 0; i < offerViews.Length; i++)
            {
                int captured = i;

                offerViews[i] = new CardOfferView();
                offerViews[i].Build(offerPanel.transform, i);
                offerViews[i].OnPicked += _ => PickCard(captured);
            }

            offerHint = SimpleUI.CreateText(offerPanel.transform, "Hint", "", 20, HintColor);
            SimpleUI.Place(offerHint, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1000f, 32f));

            offerPanel.SetActive(false);
        }

        private GameObject Panel(string name, float width, float height)
        {
            GameObject go = SimpleUI.CreateImage(root.transform, name, PanelColor);
            SimpleUI.Place(go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            return go;
        }

        /// <summary>가운데를 기준으로 n칸을 가로로 늘어놓는다.</summary>
        private static void Layout(CardOfferView[] views, int count, float width, float gap, float centerY)
        {
            if (count <= 0) return;

            float span = count * width + (count - 1) * gap;
            float left = -span * 0.5f + width * 0.5f;

            for (int i = 0; i < count; i++)
            {
                RectTransform rect = views[i].Root;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(left + i * (width + gap), centerY);
            }
        }
    }
}
