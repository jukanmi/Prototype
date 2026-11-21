using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>선택지 한 줄 — 보이는 글과 지금 고를 수 있는가.</summary>
    public readonly struct DialogueChoice
    {
        public readonly string Label;
        public readonly bool Enabled;

        public DialogueChoice(string label, bool enabled = true)
        {
            Label = label;
            Enabled = enabled;
        }
    }

    /// <summary>
    /// 대화창 — 화면 아래 상자에 말하는 사람과 글, 상자 위에 그 사람 그림(<see cref="Npc.Portrait"/>). 한 번에 하나만 뜬다.
    ///
    /// <list type="bullet">
    /// <item><b>대사</b>(<see cref="Open"/>) — 상호작용 키(F) · Enter · 클릭으로 넘기고 마지막 장 다음에 닫힌다.
    /// 끝까지 읽으면 <c>onFinished</c>를 부른다 — 대화 뒤에 여는 창(제단 · 상점 · 이벤트)이 여기 걸린다.</item>
    /// <item><b>선택지</b>(<see cref="Ask"/>) — 글 아래 선택지 줄. 방향키 · WASD로 고르고 F · Enter로 정한다
    /// (<see cref="KeyMenu"/>). 닫힌 뒤 고른 번호를 넘긴다.</item>
    /// </list>
    ///
    /// ESC는 바로 닫는다 — <c>onFinished</c>도 선택도 부르지 않는다. 떠 있는 동안 조작을 잠근다(다른 창과 같다).
    ///
    /// <b>연 프레임에는 넘기지도 고르지도 않는다.</b> 말을 건 F, 앞 창을 닫은 Enter가 같은 프레임에 이 창까지 넘기면
    /// 첫 장을 못 보고 지나간다.
    /// </summary>
    public class DialogueWindow : MonoBehaviour
    {
        private const float BoxWidth = 1400f;
        private const float LinesHeight = 240f;
        private const float BoxBottom = 40f;
        private const float Margin = 40f;
        private const float BodyTop = 76f;
        private const float LinesBodyHeight = 120f;
        private const float AskBodyHeight = 96f;
        private const float ChoiceHeight = 50f;
        private const float ChoiceGap = 8f;
        private const float HintBand = 56f;

        /// <summary>그림 높이. 몸 시트는 한 장에 여백이 커서(캐릭터가 아래 절반) 넉넉히 키운다.</summary>
        private const float PortraitHeight = 440f;

        private const float PortraitMaxWidth = 700f;

        /// <summary>그림 발끝이 상자 뒤로 숨는 깊이 — 상자 위에 선 것처럼 보인다.</summary>
        private const float PortraitTuck = 10f;

        private static readonly Color ChoiceColor = new Color(0.17f, 0.19f, 0.24f, 1f);
        private static readonly Color DisabledColor = new Color(0.45f, 0.47f, 0.50f);

        private static DialogueWindow current;

        private string[] lines;
        private int index;
        private Action onFinished;
        private Action<int> onPick;
        private Npc face;
        private int openedFrame;

        private RectTransform panel;
        private Button box;
        private Text body;
        private Text hint;
        private Image portrait;
        private float portraitBottom;
        private Sprite shown;

        private KeyMenu menu;
        private readonly List<DialogueChoice> choices = new List<DialogueChoice>();
        private readonly List<Text> choiceLabels = new List<Text>();

        public static bool IsOpen => current != null;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterModal() => GameplayModal.Register(IsOpenNow);

        private static bool IsOpenNow() => IsOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => current = null;

        private static string InteractKey => InputHints.Key(InputActionNames.Gameplay.Map, InputActionNames.Gameplay.Interact);

        // ── 여는 쪽 ─────────────────────────────────────

        /// <summary>
        /// 대사를 연다. 이미 떠 있으면 안 연다.
        /// 대사가 없으면 읽을 것이 없으니 <paramref name="onFinished"/>를 바로 부른다.
        /// </summary>
        /// <param name="face">말하는 사람. 있으면 상자 위에 그 사람 그림이 선다.</param>
        public static void Open(string speaker, string[] lines, Action onFinished = null, Npc face = null)
        {
            if (current != null) return;
            if (lines == null || lines.Length == 0) { onFinished?.Invoke(); return; }

            DialogueWindow w = Create(face);
            w.lines = lines;
            w.onFinished = onFinished;
            w.BuildBox(speaker, LinesHeight, null);
            w.BuildLines();
        }

        /// <summary>
        /// 글 한 토막과 선택지를 연다. 고르면 창이 닫힌 뒤 <paramref name="onPick"/>에 번호를 넘긴다.
        /// 잠긴 선택지(<see cref="DialogueChoice.Enabled"/>)는 커서가 건너뛰고 눌리지도 않는다.
        /// 선택지가 없으면 글만 대사로 보인다.
        /// </summary>
        /// <param name="corner">상자 오른쪽 위 한 줄(가진 골드 등). 비우면 없다.</param>
        public static void Ask(string speaker, string text, IReadOnlyList<DialogueChoice> options, Action<int> onPick,
                               Npc face = null, string corner = null)
        {
            if (current != null) return;
            if (options == null || options.Count == 0)
            {
                Open(speaker, new[] { text }, null, face);
                return;
            }

            DialogueWindow w = Create(face);
            w.onPick = onPick;
            w.BuildBox(speaker, BodyTop + AskBodyHeight + options.Count * (ChoiceHeight + ChoiceGap) + HintBand, corner);
            w.BuildChoices(text, options);
        }

        private static DialogueWindow Create(Npc face)
        {
            // 부모 없이 활성 씬에 짓는다 — 그 씬과 함께 내려간다(SceneLoader가 활성 씬을 맞춘다).
            current = new GameObject(nameof(DialogueWindow), typeof(RectTransform)).AddComponent<DialogueWindow>();
            current.face = face;
            current.openedFrame = Time.frameCount;
            return current;
        }

        // ── 짓기 ────────────────────────────────────────

        private void BuildBox(string speaker, float height, string corner)
        {
            UiKit.EnsureEventSystem();
            UiKit.BuildCanvas(gameObject, UiLayer.NodeWindow, match: 1f);

            // 그림을 상자보다 먼저 깐다 — 상자가 발끝을 덮어 그 위에 선 것처럼 보인다.
            portrait = UiKit.NewImage(transform, "Portrait", Color.white);
            portrait.preserveAspect = true;
            portrait.rectTransform.anchorMin = portrait.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            portrait.rectTransform.pivot = new Vector2(0.5f, 0f);
            portrait.gameObject.SetActive(false);
            portraitBottom = BoxBottom + height - PortraitTuck;

            Image image = UiKit.NewImage(transform, "Box", NodeWindows.PanelColor, raycast: true);
            panel = UiKit.Place(image, new Vector2(0.5f, 0f), new Vector2(0f, BoxBottom), new Vector2(BoxWidth, height));

            Text speakerText = UiKit.NewText(panel, "Speaker", 30, NodeWindows.TitleColor, FontStyle.Bold);
            speakerText.text = speaker;
            speakerText.alignment = TextAnchor.UpperLeft;
            UiKit.Place(speakerText, new Vector2(0f, 1f), new Vector2(Margin, -24f), new Vector2(BoxWidth - Margin * 2f, 40f));

            if (!string.IsNullOrEmpty(corner))
            {
                Text cornerText = UiKit.NewText(panel, "Corner", 24, NodeWindows.TitleColor, FontStyle.Normal);
                cornerText.text = corner;
                cornerText.alignment = TextAnchor.UpperRight;
                UiKit.Place(cornerText, new Vector2(1f, 1f), new Vector2(-Margin, -28f), new Vector2(400f, 34f));
            }

            hint = UiKit.NewText(panel, "Hint", 22, NodeWindows.SubColor, FontStyle.Normal);
            hint.alignment = TextAnchor.LowerRight;
            UiKit.Place(hint, new Vector2(1f, 0f), new Vector2(-30f, 18f), new Vector2(900f, 30f));

            RefreshPortrait();
        }

        private Text Body(float height)
        {
            Text t = UiKit.NewText(panel, "Body", 28, NodeWindows.BodyColor, FontStyle.Normal);
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Place(t, new Vector2(0f, 1f), new Vector2(Margin, -BodyTop), new Vector2(BoxWidth - Margin * 2f, height));
            return t;
        }

        private void BuildLines()
        {
            // 상자 전체가 버튼이다 — 클릭 · Enter(EventSystem 제출)로 넘긴다. F는 Update가 본다.
            box = panel.gameObject.AddComponent<Button>();
            box.targetGraphic = panel.GetComponent<Image>();
            box.navigation = new Navigation { mode = Navigation.Mode.None };   // 방향키로 뒤쪽 버튼에 포커스가 새지 않게
            box.onClick.AddListener(Next);

            body = Body(LinesBodyHeight);

            PlayerInputController.SetSuspended(true);
            ShowPage();
        }

        private void BuildChoices(string text, IReadOnlyList<DialogueChoice> options)
        {
            body = Body(AskBodyHeight);
            body.text = text;

            menu = new KeyMenu(panel);
            var rowSize = new Vector2(BoxWidth - Margin * 2f - 40f, ChoiceHeight);
            float top = -(BodyTop + AskBodyHeight);

            for (int i = 0; i < options.Count; i++)
            {
                int captured = i;
                DialogueChoice choice = options[i];
                choices.Add(choice);

                Button row = UiKit.CreateButton(panel, $"Choice_{i}", "", ChoiceColor, rowSize, 26);
                RectTransform rect = UiKit.Place(row, new Vector2(0.5f, 1f), new Vector2(0f, top - i * (ChoiceHeight + ChoiceGap)), rowSize);
                row.navigation = new Navigation { mode = Navigation.Mode.None };
                row.interactable = choice.Enabled;
                row.onClick.AddListener(() => Pick(captured));

                Text label = row.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(24f, 0f);
                choiceLabels.Add(label);

                menu.Add(rect, i, () => choices[captured].Enabled, () => Pick(captured));
                Style(i, false);
            }

            menu.Moved += (from, to) =>
            {
                Style(from, false);
                Style(to, true);
            };

            hint.text = $"방향키 · WASD  고르기     [{InteractKey}]  선택     ESC  닫기";

            PlayerInputController.SetSuspended(true);
            menu.Open();
        }

        /// <summary>지금 보는 선택지는 금색 굵은 글에 ▶ — 테두리(<see cref="KeyMenu"/>)와 겹쳐 어느 줄인지 확실히 보인다.</summary>
        private void Style(int i, bool focused)
        {
            if (i < 0 || i >= choiceLabels.Count) return;

            DialogueChoice choice = choices[i];
            Text t = choiceLabels[i];

            t.text = (focused ? "▶  " : "     ") + choice.Label;
            t.fontStyle = focused ? FontStyle.Bold : FontStyle.Normal;
            t.color = !choice.Enabled ? DisabledColor : focused ? NodeWindows.TitleColor : NodeWindows.BodyColor;
        }

        // ── 넘기기 · 고르기 ─────────────────────────────

        private void ShowPage()
        {
            body.text = lines[index];
            hint.text = index < lines.Length - 1 ? $"[{InteractKey}] 다음 ▶" : $"[{InteractKey}] 닫기";
            NodeWindows.Focus(box);
        }

        /// <summary>다음 장. 마지막 장이었으면 닫고 <see cref="onFinished"/>를 부른다.</summary>
        private void Next()
        {
            // 이미 닫혔다(한 프레임의 클릭 + 키가 두 번 부른다), 또는 이 창을 연 그 키다.
            if (current != this || Time.frameCount == openedFrame) return;

            if (++index < lines.Length) { ShowPage(); return; }

            // 잠금을 먼저 풀고 부른다. 거꾸로면 이어서 연 창(제단 등)이 건 잠금을 이 창이 닫히며 풀어 버린다.
            Close();
            onFinished?.Invoke();
        }

        private void Pick(int i)
        {
            if (current != this || Time.frameCount == openedFrame) return;
            if (i < 0 || i >= choices.Count || !choices[i].Enabled) return;

            Close();
            onPick?.Invoke(i);
        }

        /// <summary>잠금을 바로 푼다. 오브젝트가 지워지는 건 프레임 끝이라 그때까지 기다리지 않는다.</summary>
        private void Close()
        {
            if (current == this)
            {
                current = null;
                PlayerInputController.SetSuspended(false);
            }

            Destroy(gameObject);
        }

        private void Update()
        {
            if (current != this) return;

            RefreshPortrait();

            PlayerInputController input = PlayerInputController.Instance;
            if (input != null && input.CancelPressed && !GameplayModal.CancelConsumedThisFrame)
            {
                GameplayModal.ConsumeCancel();
                Close();
                return;
            }

            if (menu != null)
            {
                menu.Tick();
                return;
            }

            // 대사는 F로도 넘긴다. Enter · 클릭은 상자 버튼이 받는다(EventSystem).
            if (input != null && input.InteractKeyPressed)
            {
                Next();
                if (current != this) return;
            }

            // 빈 곳을 클릭하면 선택이 풀려 Enter가 안 먹는다. 되잡는다.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != box.gameObject)
                NodeWindows.Focus(box);
        }

        /// <summary>
        /// 그림을 말하는 사람의 지금 스프라이트로 맞춘다. 몸이 움직이면(애니메이션) 같이 움직인다.
        /// 크기는 스프라이트 한 장의 비율 그대로 높이만 맞춘다 — 상자 왼쪽 위에 발을 딛고 선다.
        /// </summary>
        private void RefreshPortrait()
        {
            Sprite s = face != null ? face.Portrait : null;
            if (s == shown) return;
            shown = s;

            portrait.gameObject.SetActive(s != null);
            if (s == null) return;

            portrait.sprite = s;

            // 가로로 긴 시트(공격 모션 등)가 상자 절반을 덮지 않게 폭을 묶는다. 비율은 그대로 줄인다.
            float height = PortraitHeight;
            float width = height * s.rect.width / Mathf.Max(1f, s.rect.height);
            if (width > PortraitMaxWidth)
            {
                height *= PortraitMaxWidth / width;
                width = PortraitMaxWidth;
            }

            RectTransform r = portrait.rectTransform;
            r.sizeDelta = new Vector2(width, height);
            r.anchoredPosition = new Vector2(-BoxWidth * 0.5f + Margin + 60f + width * 0.5f, portraitBottom);
        }

        /// <summary>닫히지 않은 채 지워졌다(씬이 내려갔다). 잠금만 푼다.</summary>
        private void OnDestroy()
        {
            if (current != this) return;

            current = null;
            PlayerInputController.SetSuspended(false);
        }
    }
}
