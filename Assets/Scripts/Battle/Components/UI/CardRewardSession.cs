// 카드 획득 화면 — 전투 · 정예 클리어 때 한 번, 카드 세 장 중 하나를 고른다.

using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

namespace Prototype
{
    // ══ CardRewardSession ═══════════════════════════════════════════

    /// <summary>
    /// 전투 · 정예 칸을 깼을 때 뜨는 <b>카드 획득</b> 화면. 단계 고르기 없이 곧바로 세 장이 나온다.
    /// 한 장을 고르거나 [받지 않고 나가기]로 닫는다 — 어느 쪽이든 그 칸의 보상은 이걸로 끝이다.
    ///
    /// 열고 닫는 모달 패턴은 <c>RebindUI</c>를 따른다 — 씬 배선 없이 코드로만 짓고,
    /// 열려 있는 동안 <see cref="PlayerInputController.GameplaySuspended"/>로 조작을 잠근다.
    ///
    /// <b><see cref="TimeControl.Scale"/>은 건드리지 않는다.</b> 그 값은 전투(전술 페이즈 · 스킬 컷인)가 쓴다.
    /// 이 시점엔 적이 전부 죽어 있어 멈출 것도 없다.
    ///
    /// 골격은 <see cref="UiKit.Modal"/>이다(기획서 5.3). 카드 칸의 포커스는 청록 띠, [받지 않고 나가기]의 포커스는
    /// 버튼 자신의 테두리(<see cref="UiFocusRim.Forced"/>) — 둘 다 <see cref="UiTheme.Focus"/> 한 색이다.
    /// </summary>
    public class CardRewardSession : MonoBehaviour
    {
        private static readonly Color SubColor = UiTheme.TextSub;
        private static readonly Color HintColor = UiTheme.Gold;
        private static readonly Color FocusColor = UiTheme.Focus;

        private const float PanelWidth = 1120f;
        private const float PanelHeight = 700f;
        private const float OfferGap = 40f;

        /// <summary>카드 줄의 세로 자리(본문 가운데 기준). 밑에 안내 두 줄이 들어간다.</summary>
        private const float OfferCenterY = 40f;

        /// <summary>포커스 테두리가 칸 밖으로 내미는 두께.</summary>
        private const float FocusPad = 7f;

        // ── 씬에 하나 ────────────────────────────────────

        private static CardRewardSession Instance { get; set; }

        /// <summary>화면이 떠 있는가. 승패 판정 · 모달 잠금이 이 값을 본다.</summary>
        public static bool IsOpen => Instance != null && Instance.isOpen;

#if UNITY_EDITOR
        // 에디트 모드(EditMode 테스트)에서도 꽂혀 있어야 예전 동작과 같다.
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterModal() => GameplayModal.Register(IsOpenNow);

        private static bool IsOpenNow() => IsOpen;

        /// <summary>
        /// 카드 획득 화면을 연다. 씬에 컴포넌트가 없으면 여기서 만든다 —
        /// 이 프로젝트의 전투 UI는 전부 씬 배선 0으로 스스로 지어진다.
        /// 뽑을 스킬이 하나도 없으면 열지 않는다.
        /// </summary>
        public static void RequestOpen()
        {
            if (Instance == null) Instance = Create();
            if (Instance == null || Instance.isOpen) return;

            Instance.Open();
        }

        private static CardRewardSession Create()
        {
            var go = new GameObject(nameof(CardRewardSession), typeof(RectTransform));
            return go.AddComponent<CardRewardSession>();
        }

        // ── 상태 ─────────────────────────────────────────

        [Tooltip("비우면 같은 GameObject 또는 씬에서 찾는다. 얻은 카드를 지금 판의 덱에 들이는 통로다.")]
        [SerializeField] private BulletTimeController bulletTime;

        private GameObject root;
        private GameObject focus;
        private Text hint;
        private Button exitButton;
        private UiFocusRim exitRim;

        private readonly CardOfferView[] offerViews = new CardOfferView[CardOfferRules.OfferCount];
        private List<CardOffer> offers = new List<CardOffer>();

        private bool isOpen;

        /// <summary>카드 줄에서 커서가 가리키는 칸.</summary>
        private int cursor;

        /// <summary>커서가 [받지 않고 나가기]에 내려가 있는가. 카드 줄과 배타다.</summary>
        private bool exitFocused;

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
            if (isOpen) PlayerInputController.SetSuspended(false);

            Instance = null;
        }

        // ── 열고 닫기 ────────────────────────────────────

        private void Open()
        {
            // 모집단은 <b>파티</b>가 정한다. 지금 든 카드에서 긁으면 0장으로 시작하는
            // 테스트 모드에서 뽑을 것이 없다.
            List<SkillData> pool = bulletTime != null
                ? bulletTime.AvailableSkillPool()
                : SkillCatalog.Pool(null, RunProgression.Current.Cards);

            offers = CardOfferRules.Build(pool, () => Random.value);

            if (offers.Count == 0)
            {
                BattleLog.Warn(LogCategory.State,
                    "카드 보상을 짤 스킬이 없다 — Assets/Data/Resources/SkillCatalog.asset을 확인할 것", this);
                return;
            }

            isOpen = true;
            root.SetActive(true);
            PlayerInputController.SetSuspended(true);

            // 창을 띄운 그 손이 방향키를 쥐고 있어도 커서가 공짜로 한 칸 밀리지 않게 기준을 새로 잡는다.
            PlayerInputController input = PlayerInputController.Instance;
            if (input != null) input.ResyncUiNavigate();

            for (int i = 0; i < offerViews.Length; i++)
            {
                if (i >= offers.Count) { offerViews[i].Hide(); continue; }

                CardOffer offer = offers[i];
                offerViews[i].Show(in offer);
                offerViews[i].SetInteractable(true);
            }

            exitButton.interactable = true;
            Layout(offerViews, offers.Count, CardOfferView.Width, OfferGap, OfferCenterY);

            // 자리를 잡은 뒤에 테두리를 얹는다 — Layout 전에는 칸의 anchoredPosition이 아직 0이다.
            cursor = 0;
            exitFocused = false;
            RefreshFocus();

            hint.text = "카드 한 장을 고른다. 고르지 않고 나가도 된다";

            BattleLog.Log(LogCategory.State, $"<b>카드 획득</b> — 선택지 {offers.Count}장", this);
        }

        private void Close()
        {
            isOpen = false;

            root.SetActive(false);
            PlayerInputController.SetSuspended(false);
        }

        // ── 키보드 ───────────────────────────────────────

        /// <summary>
        /// 방향 · 결정 · 취소를 읽는다. 마우스는 버튼의 <c>onClick</c>이 그대로 받으므로
        /// 여기서는 커서만 본다 — 두 길이 같은 <c>PickCard</c> · <c>Close</c>로 들어간다.
        /// </summary>
        private void Update()
        {
            if (!isOpen) return;

            ClearEventSystemSelection();

            PlayerInputController input = PlayerInputController.Instance;
            if (input == null) return;

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
                RefreshFocus();
            }
            else if (step.y > 0 && exitFocused)
            {
                exitFocused = false;
                RefreshFocus();
            }
            else if (step.x != 0 && !exitFocused)
            {
                int next = MenuCursor.Step(cursor, offers.Count, step.x, null);
                if (next != cursor)
                {
                    cursor = next;
                    RefreshFocus();
                }
            }

            if (!input.UiSubmitPressed) return;

            if (exitFocused) Close();
            else PickCard(cursor);
        }

        /// <summary>
        /// EventSystem의 선택은 <b>쓰지 않는다.</b> 버튼을 마우스로 한 번 누르면 그 버튼이
        /// 선택된 채 남고, 그때부터 Enter 한 번이 그 버튼과 커서가 가리키는 칸을 각각 눌러
        /// 한 프레임에 두 번 처리된다. 커서를 하나로 두는 값은 여기서 치른다.
        /// </summary>
        private static void ClearEventSystemSelection()
        {
            EventSystem events = EventSystem.current;
            if (events == null || events.currentSelectedGameObject == null) return;

            events.SetSelectedGameObject(null);
        }

        private void RefreshFocus()
        {
            exitRim.Forced = exitFocused;

            RectTransform target = !exitFocused && cursor >= 0 && cursor < offers.Count
                ? offerViews[cursor].Root
                : null;

            if (target == null)
            {
                focus.SetActive(false);
                return;
            }

            focus.SetActive(true);

            // 본문의 첫 자식이라 칸보다 <b>뒤에</b> 그려지므로 내민 만큼만 띠로 보인다.
            var rect = (RectTransform)focus.transform;
            rect.anchorMin = target.anchorMin;
            rect.anchorMax = target.anchorMax;
            rect.pivot = target.pivot;
            rect.anchoredPosition = target.anchoredPosition;
            rect.sizeDelta = target.sizeDelta + new Vector2(FocusPad * 2f, FocusPad * 2f);
        }

        private void PickCard(int index)
        {
            if (!isOpen || index < 0 || index >= offers.Count) return;

            CardOffer pick = offers[index];

            // 고른 뒤 두 번 눌리지 않게 곧바로 잠근다.
            for (int i = 0; i < offerViews.Length; i++) offerViews[i].SetInteractable(false);
            exitButton.interactable = false;

            ComboCard granted = RunProgression.Current.Grant(in pick);

            // 런 덱에 들어간 그 카드를 <b>손패에 바로</b> 꽂는다.
            if (granted != null && bulletTime != null) bulletTime.GrantCard(granted);

            BattleLog.Log(LogCategory.State,
                $"카드 선택 — {pick.data.skillName} | 런 덱 {RunProgression.Current.Cards.Count}장", this);

            offers.Clear();
            Close();
        }

        // ── 짓기 ─────────────────────────────────────────

        private void BuildUI()
        {
            // 바탕막이 뒤쪽 UI(손패 등)로 클릭이 새지 않게 막는다.
            UiKit.ModalParts m = UiKit.Modal(transform, "스킬 카드 획득", new Vector2(PanelWidth, PanelHeight));
            root = m.Backdrop.gameObject;
            RectTransform body = m.Body;

            // 포커스 띠 한 장. <b>본문의 첫 자식</b>이라야 칸보다 뒤에 그려진다.
            focus = UiKit.CreateImage(body, "Focus", FocusColor);
            focus.GetComponent<Image>().raycastTarget = false;
            focus.transform.SetAsFirstSibling();
            focus.SetActive(false);

            for (int i = 0; i < offerViews.Length; i++)
            {
                int captured = i;

                offerViews[i] = new CardOfferView();
                offerViews[i].Build(body, i);
                offerViews[i].OnPicked += _ => PickCard(captured);
            }

            hint = UiKit.CreateText(body, "Hint", "", UiTheme.Size.Body, HintColor);
            UiKit.Place(hint, new Vector2(0.5f, 0.5f), new Vector2(0f, -186f), new Vector2(1000f, 32f));

            Text keys = UiKit.CreateText(body, "Keys",
                "좌우 방향키  카드    아래 방향키  나가기    Enter  결정    ESC  나가기", UiTheme.Size.Caption, SubColor);
            UiKit.Place(keys, new Vector2(0.5f, 0.5f), new Vector2(0f, -226f), new Vector2(900f, 26f));

            exitButton = UiKit.NewButton(m.Buttons, "Exit", "받지 않고 나가기", ButtonKind.Ghost,
                                         new Vector2(280f, UiTheme.ButtonHeight.Medium));
            exitButton.onClick.AddListener(Close);
            exitRim = exitButton.GetComponent<UiFocusRim>();
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
