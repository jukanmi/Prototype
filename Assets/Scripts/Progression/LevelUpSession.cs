// 레벨업 화면과 그 화면이 여는 모달 규약 · 덱 시작 모드.
// GameplayModal은 이 화면과 DeckBuilderUI만 구현한다.

using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

namespace Prototype
{
    // ══ LevelUpSession ═══════════════════════════════════════════

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

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color PanelColor = new Color(0.11f, 0.12f, 0.15f, 0.98f);
        private static readonly Color TierColor = new Color(0.24f, 0.30f, 0.38f);
        private static readonly Color ExitColor = new Color(0.30f, 0.32f, 0.36f);
        private static readonly Color TitleColor = new Color(1f, 0.86f, 0.45f);
        private static readonly Color SubColor = new Color(0.75f, 0.78f, 0.82f);
        private static readonly Color HintColor = new Color(1f, 0.82f, 0.4f);
        private static readonly Color FocusColor = new Color(1f, 0.86f, 0.45f);

        private const float PanelWidth = 1120f;
        private const float PanelHeight = 620f;

        private const float TierWidth = 260f;
        private const float TierHeight = 190f;
        private const float TierGap = 40f;

        private const float OfferGap = 40f;

        /// <summary>포커스 테두리가 칸 밖으로 내미는 두께.</summary>
        private const float FocusPad = 7f;

        /// <summary>단계 줄 다음 칸 — [나가기]. 줄 안 인덱스가 아니라 커서의 다른 상태다.</summary>
        private const int NoTier = MenuCursor.None;

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

        private Button exitButton;

        // 포커스 테두리. 칸마다 하나씩 두지 않고 판마다 하나를 옮겨 붙인다 —
        // 칸의 배경색은 잠금(단계)과 등급(카드)이 이미 쓰고 있어서 거기에 얹을 자리가 없다.
        private GameObject tierFocus;
        private GameObject offerFocus;

        private readonly Button[] tierButtons = new Button[ExpRules.TierCount];
        private readonly Text[] tierCostLabels = new Text[ExpRules.TierCount];
        private readonly Text[] tierChanceLabels = new Text[ExpRules.TierCount];

        private readonly CardOfferView[] offerViews = new CardOfferView[CardOfferRules.OfferCount];
        private List<CardOffer> offers = new List<CardOffer>();

        private bool isOpen;

        /// <summary>단계 줄에서 커서가 가리키는 칸. 쓸 수 있는 단계가 없으면 <see cref="NoTier"/>.</summary>
        private int tierCursor;

        /// <summary>커서가 [나가기]에 내려가 있는가. 단계 줄과 배타다.</summary>
        private bool exitFocused;

        /// <summary>카드 판에서 커서가 가리키는 칸.</summary>
        private int offerCursor;

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

            UiKit.EnsureEventSystem();
            UiKit.BuildCanvas(gameObject, UiLayer.LevelUp);

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

            // 창을 띄운 그 손이 방향키를 쥐고 있어도 커서가 공짜로 한 칸 밀리지 않게 기준을 새로 잡는다.
            PlayerInputController input = PlayerInputController.Instance;
            if (input != null) input.ResyncUiNavigate();

            tierCursor = 0;
            exitFocused = false;

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

        // ── 키보드 ───────────────────────────────────────

        /// <summary>
        /// 뜬 판에 맞춰 방향 · 결정 · 취소를 읽는다. 마우스는 버튼의 <c>onClick</c>이 그대로 받으므로
        /// 여기서는 커서만 본다 — 두 길이 같은 <c>PickTier</c> · <c>PickCard</c>로 들어간다.
        /// </summary>
        private void Update()
        {
            if (!isOpen) return;

            ClearEventSystemSelection();

            PlayerInputController input = PlayerInputController.Instance;
            if (input == null) return;

            if (offerPanel.activeSelf) HandleOfferInput(input);
            else HandleTierInput(input);
        }

        /// <summary>
        /// EventSystem의 선택은 <b>쓰지 않는다.</b> 그런데 버튼을 마우스로 한 번 누르면 그 버튼이
        /// 선택된 채 남고, 그때부터 Enter 한 번이 <c>InputSystemUIInputModule</c>을 통해 그 버튼을,
        /// 이 Update를 통해 커서가 가리키는 칸을 각각 눌러 한 프레임에 두 장이 들어온다.
        /// 커서를 하나로 두는 값은 여기서 치른다.
        /// </summary>
        private static void ClearEventSystemSelection()
        {
            EventSystem events = EventSystem.current;
            if (events == null || events.currentSelectedGameObject == null) return;

            events.SetSelectedGameObject(null);
        }

        private void HandleTierInput(PlayerInputController input)
        {
            if (input.CancelPressed)
            {
                // 같은 프레임에 스테이지 쪽이 이 ESC를 메인 메뉴 복귀로도 읽지 않게 표를 남긴다.
                GameplayModal.ConsumeCancel();
                Close();
                return;
            }

            Vector2Int step = input.UiNavigateStep;

            // 위 · 아래를 먼저 본다. 대각선으로 눌리면 두 축이 함께 서는데,
            // 줄을 옮기는 쪽이 같은 줄 안의 이동보다 우선이다.
            if (step.y < 0 && !exitFocused)
            {
                exitFocused = true;
                RefreshTierFocus();
            }
            else if (step.y > 0 && exitFocused && tierCursor != NoTier)
            {
                exitFocused = false;
                RefreshTierFocus();
            }
            else if (step.x != 0 && !exitFocused)
            {
                int next = MenuCursor.Step(tierCursor, ExpRules.TierCount, step.x, TierUnlocked);
                if (next != tierCursor)
                {
                    tierCursor = next;
                    RefreshTierFocus();
                }
            }

            if (!input.UiSubmitPressed) return;

            if (exitFocused || tierCursor == NoTier) { Close(); return; }

            PickTier(tierCursor);
        }

        private void HandleOfferInput(PlayerInputController input)
        {
            // 취소는 받지 않는다. 단계를 고른 그 순간 경험치가 이미 나갔으므로 돌아갈 자리가 없다.
            Vector2Int step = input.UiNavigateStep;

            if (step.x != 0)
            {
                int next = MenuCursor.Step(offerCursor, offers.Count, step.x, null);
                if (next != offerCursor)
                {
                    offerCursor = next;
                    RefreshOfferFocus();
                }
            }

            if (input.UiSubmitPressed) PickCard(offerCursor);
        }

        /// <summary>커서가 얹힐 수 있는 단계인가. 잠긴 단계는 건너뛴다.</summary>
        private static bool TierUnlocked(int tier) => Run.CanLevelUp(tier);

        private void RefreshTierFocus()
        {
            RectTransform target = exitFocused || tierCursor == NoTier
                ? (RectTransform)exitButton.transform
                : (RectTransform)tierButtons[tierCursor].transform;

            PlaceFocus(tierFocus, target);
        }

        private void RefreshOfferFocus()
        {
            if (offerCursor < 0 || offerCursor >= offers.Count)
            {
                offerFocus.SetActive(false);
                return;
            }

            PlaceFocus(offerFocus, offerViews[offerCursor].Root);
        }

        /// <summary>
        /// 테두리를 칸 자리로 옮기고 사방 <see cref="FocusPad"/>만큼 키운다.
        /// 판의 첫 자식이라 칸보다 <b>뒤에</b> 그려지므로 내민 만큼만 띠로 보인다.
        /// </summary>
        private static void PlaceFocus(GameObject focus, RectTransform target)
        {
            if (focus == null || target == null) return;

            focus.SetActive(true);

            var rect = (RectTransform)focus.transform;
            rect.anchorMin = target.anchorMin;
            rect.anchorMax = target.anchorMax;
            rect.pivot = target.pivot;
            rect.anchoredPosition = target.anchoredPosition;
            rect.sizeDelta = target.sizeDelta + new Vector2(FocusPad * 2f, FocusPad * 2f);
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

            // 방금 쓴 단계가 잠겼을 수 있다. 커서가 잠긴 칸에 얹힌 채로 남으면
            // Enter를 눌러도 아무 일이 없어 입력이 죽은 것처럼 보인다.
            tierCursor = MenuCursor.Clamp(tierCursor, ExpRules.TierCount, TierUnlocked);
            if (tierCursor == NoTier) exitFocused = true;

            RefreshTierFocus();
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

            // 자리를 잡은 뒤에 테두리를 얹는다 — Layout 전에는 칸의 anchoredPosition이 아직 0이다.
            offerCursor = 0;
            RefreshOfferFocus();

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
            root = UiKit.CreateImage(transform, "LevelUpRoot", BackdropColor);
            UiKit.Stretch((RectTransform)root.transform);

            // 뒤쪽 UI(손패 등)로 클릭이 새지 않게 막는다.
            root.GetComponent<Image>().raycastTarget = true;

            BuildTierPanel();
            BuildOfferPanel();
        }

        private void BuildTierPanel()
        {
            tierPanel = Panel("TierPanel", PanelWidth, PanelHeight * 0.72f);
            tierFocus = Focus(tierPanel.transform);

            Text title = UiKit.CreateText(tierPanel.transform, "Title", "레벨 업", 44, TitleColor);
            UiKit.Place(title, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(600f, 56f));

            headerText = UiKit.CreateText(tierPanel.transform, "Header", "", 26, SubColor);
            headerText.supportRichText = true;
            UiKit.Place(headerText, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(700f, 36f));

            float span = ExpRules.TierCount * TierWidth + (ExpRules.TierCount - 1) * TierGap;
            float left = -span * 0.5f + TierWidth * 0.5f;

            for (int tier = 0; tier < ExpRules.TierCount; tier++)
            {
                int captured = tier;

                Button button = UiKit.CreateButton(tierPanel.transform, $"Tier_{tier}", "", TierColor,
                    new Vector2(TierWidth, TierHeight), 26);
                button.onClick.AddListener(() => PickTier(captured));

                // CreateButton이 끼워 준 기본 라벨은 세 줄을 담지 못한다. 끄고 직접 쌓는다.
                Text stock = button.GetComponentInChildren<Text>();
                if (stock != null) stock.gameObject.SetActive(false);

                UiKit.Place(button, new Vector2(0.5f, 0.5f),
                    new Vector2(left + tier * (TierWidth + TierGap), -20f),
                    new Vector2(TierWidth, TierHeight));

                Text name = UiKit.CreateText(button.transform, "Name",
                    $"{ExpRules.TierNumber(tier)}단계", 30, Color.white);
                UiKit.Place(name, new Vector2(0.5f, 0.5f), new Vector2(0f, 46f), new Vector2(TierWidth, 40f));

                tierCostLabels[tier] = UiKit.CreateText(button.transform, "Cost", "", 24, HintColor);
                UiKit.Place(tierCostLabels[tier], new Vector2(0.5f, 0.5f), new Vector2(0f, 2f),
                    new Vector2(TierWidth, 34f));

                tierChanceLabels[tier] = UiKit.CreateText(button.transform, "Chance", "", 20, SubColor);
                UiKit.Place(tierChanceLabels[tier], new Vector2(0.5f, 0.5f), new Vector2(0f, -38f),
                    new Vector2(TierWidth, 30f));

                tierButtons[tier] = button;
            }

            exitButton = UiKit.CreateButton(tierPanel.transform, "Exit", "나가기", ExitColor,
                new Vector2(220f, 58f), 24);
            exitButton.onClick.AddListener(Close);
            UiKit.Place(exitButton, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(220f, 58f));

            Text keys = UiKit.CreateText(tierPanel.transform, "Keys",
                "좌우 방향키  단계    아래 방향키  나가기    Enter  결정    ESC  닫기", 18, SubColor);
            UiKit.Place(keys, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(900f, 26f));
        }

        private void BuildOfferPanel()
        {
            offerPanel = Panel("OfferPanel", PanelWidth, PanelHeight);
            offerFocus = Focus(offerPanel.transform);

            offerTitle = UiKit.CreateText(offerPanel.transform, "Title", "", 34, TitleColor);
            offerTitle.supportRichText = true;
            UiKit.Place(offerTitle, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(900f, 48f));

            for (int i = 0; i < offerViews.Length; i++)
            {
                int captured = i;

                offerViews[i] = new CardOfferView();
                offerViews[i].Build(offerPanel.transform, i);
                offerViews[i].OnPicked += _ => PickCard(captured);
            }

            offerHint = UiKit.CreateText(offerPanel.transform, "Hint", "", 20, HintColor);
            UiKit.Place(offerHint, new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(1000f, 32f));

            Text keys = UiKit.CreateText(offerPanel.transform, "Keys",
                "좌우 방향키  이동    Enter  선택", 18, SubColor);
            UiKit.Place(keys, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(900f, 26f));

            offerPanel.SetActive(false);
        }

        /// <summary>
        /// 포커스 테두리 한 장. <b>판의 첫 자식</b>이라야 칸보다 뒤에 그려져
        /// 내민 만큼만 띠로 보인다 — 앞에 있으면 카드를 통째로 덮는다.
        /// </summary>
        private static GameObject Focus(Transform panel)
        {
            GameObject go = UiKit.CreateImage(panel, "Focus", FocusColor);
            go.GetComponent<Image>().raycastTarget = false;
            go.transform.SetAsFirstSibling();
            go.SetActive(false);
            return go;
        }

        private GameObject Panel(string name, float width, float height)
        {
            GameObject go = UiKit.CreateImage(root.transform, name, PanelColor);
            UiKit.Place(go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
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

    // ══ GameplayModal ═══════════════════════════════════════════

    /// <summary>
    /// 게임플레이를 <b>멈춰 세워야 하는</b> 전면 UI가 떠 있는가.
    ///
    /// 스테이지 진행(웨이브 시계 · 아레나 정비 시계)과 승패 판정이 각자 개별 UI를 알게 두면,
    /// 모달이 하나 늘 때마다 세 군데를 똑같이 고쳐야 하고 한 군데를 빠뜨리면
    /// "카드를 고르는 사이에 등 뒤에서 웨이브가 쏟아지는" 식으로만 드러난다.
    /// 그래서 물어보는 창구를 하나로 둔다.
    /// </summary>
    public static class GameplayModal
    {
        private static int cancelConsumedFrame = -1;

        public static bool IsOpen => LevelUpSession.IsOpen || DeckBuilderUI.IsOpen;

        /// <summary>
        /// 이번 프레임의 ESC를 모달이 이미 썼는가.
        ///
        /// <see cref="IsOpen"/>만으로는 모자란다 — 스크립트 실행 순서는 정해져 있지 않아서,
        /// 모달이 ESC로 닫힌 프레임에 스테이지 쪽 Update가 <b>나중에</b> 돌면 이미 닫힌 창을 보고
        /// "모달 없음 + ESC 눌림"으로 읽어 런을 통째로 버리고 메인 메뉴로 나간다.
        /// </summary>
        public static bool CancelConsumedThisFrame => cancelConsumedFrame == Time.frameCount;

        /// <summary>모달이 ESC로 닫혔다. 닫기 <b>직전</b>에 부른다.</summary>
        public static void ConsumeCancel() => cancelConsumedFrame = Time.frameCount;
    }

    // ══ DeckStartupMode ═══════════════════════════════════════════

    /// <summary>
    /// 전투를 <b>어떤 덱으로 시작하는가</b>. 런의 첫 전투 씬에서 한 번만 정해진다
    /// (<see cref="RunProgression.Seeded"/>), 그 뒤로는 런 덱이 그대로 이어진다.
    ///
    /// <see cref="Party"/> 외의 둘은 <b>디버그용</b>이다. 지금 이 게임은 16장을 쥐고 시작하는데,
    /// 그러면 특정 카드 한 장이나 황금 카드의 감각을 따로 떼어 볼 방법이 없다 —
    /// 손패 4칸이 늘 다른 12장과 섞여 나오기 때문이다.
    /// </summary>
    public enum DeckStartupMode
    {
        /// <summary>파티 4명의 장착 카드 16장. 게임의 실제 시작이다.</summary>
        Party,

        /// <summary>
        /// <b>테스트 모드</b> — 0장으로 시작한다. 손패가 비므로 카드는 오직 레벨업으로만 들어온다.
        /// 성장 곡선과 레벨업 보상만 떼어 볼 때 쓴다.
        /// </summary>
        Empty,

        /// <summary>
        /// <b>디버그 모드</b> — 시작할 때 화면에서 직접 짠다(<see cref="DeckBuilderUI"/>).
        /// 보고 싶은 카드만, 원하는 장수만 넣을 수 있다.
        /// </summary>
        Pick,
    }
}
