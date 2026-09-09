using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Prototype
{
    /// <summary>
    /// 조작키 설정 화면. <see cref="ComboBoardUI"/> · <see cref="DeckInspectorUI"/>와 같은 방식으로
    /// 씬/프리팹 연결 없이 코드로만 만든다 — 아무 GameObject에 이 컴포넌트만 붙이면 동작한다.
    ///
    /// 열려 있는 동안 게임 조작을 잠근다. 안 잠그면 키 목록을 보는 내내 뒤에서
    /// 캐릭터가 움직이고 카드가 집힌다.
    /// </summary>
    public class RebindUI : MonoBehaviour
    {
        // ── 줄 치수 ───────────────────────────────────────
        // 창 · 뷰포트 · 버튼 치수는 프리팹이 쥔다. 여기 남은 건 줄 하나의 내부 배분뿐이다 —
        // 줄은 바인딩 개수만큼 코드로 찍으므로.

        /// <summary>줄 폭. 프리팹의 Content(600)보다 좁게 잡아 스크롤바 자리를 남긴다.</summary>
        private const float RowWidth = 580f;

        private const float RowHeight = 30f;
        private const float RowSpacing = 3f;
        private const float LabelWidth = 300f;
        private const float KeyWidth = 150f;
        private const float ChangeWidth = 90f;

        // ── 색 ───────────────────────────────────────────
        // 줄 · 프리셋 버튼 · 상태 문구에만 쓴다. 창 · 버튼 색은 프리팹으로 옮겼다.

        private static readonly Color HeaderColor = new Color(0.18f, 0.20f, 0.25f);
        private static readonly Color RowColor = new Color(0.17f, 0.19f, 0.23f);
        private static readonly Color ChangeColor = new Color(0.26f, 0.34f, 0.42f);
        private static readonly Color PresetColor = new Color(0.24f, 0.36f, 0.30f);
        private static readonly Color HintColor = new Color(1f, 0.82f, 0.4f);
        private static readonly Color WarnColor = new Color(1f, 0.5f, 0.45f);
        private static readonly Color SubColor = new Color(0.72f, 0.76f, 0.8f);

        [Tooltip("설정 화면에 버튼으로 뜨는 키 세팅들. 비워 두면 프리셋 줄이 안 나온다.")]
        [SerializeField] private KeyBindingPreset[] presets;

        [Tooltip("플레이어가 없는 씬(부트 · 메인메뉴)에서 쓸 액션 자산. " +
                 "플레이어가 있으면 그쪽 인스턴스를 우선한다.")]
        [SerializeField] private InputActionAsset fallbackActions;

        // ── 배선 ─────────────────────────────────────────
        // 화면은 프리팹이 쥔다. 자리 · 크기 · 색을 바꾸려면 CombatManager 프리팹을 연다.

        [Header("배선")]
        [Tooltip("화면 구석의 [조작키] 버튼.")]
        [SerializeField] private Button _openButton;

        [Tooltip("배경 막까지 포함한 설정 창 전체. 프리팹에서는 꺼 둔다.")]
        [SerializeField] private GameObject _panelRoot;

        [Tooltip("줄이 쌓이는 곳. Viewport 아래의 Content.")]
        [SerializeField] private RectTransform _content;

        [Tooltip("창 아래쪽 안내 문구.")]
        [SerializeField] private Text _statusText;

        [Tooltip("키를 받는 동안 목록을 덮는 판. 프리팹에서는 꺼 둔다.")]
        [SerializeField] private GameObject _listeningRoot;

        [Tooltip("프리셋 · 기본값 · 닫기가 나란히 서는 줄. HorizontalLayoutGroup 이 자리를 잡는다.")]
        [SerializeField] private RectTransform _footerRow;

        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _closeButton;

        /// <summary>한 줄 = 바꿀 수 있는 바인딩 하나.</summary>
        private class Row
        {
            public InputAction action;
            public int bindingIndex;
            public Text keyLabel;
        }

        private readonly List<Row> _rows = new List<Row>();

        private RebindManager _manager;
        private InputActionAsset _actions;

        /// <summary>ESC로 창을 닫는 길. 플레이어가 없는 씬에서도 있어야 해서 직접 잡는다.</summary>
        private InputAction _cancelAction;

        /// <summary>배선이 빈 채로 돌 때 경고를 한 번만 낸다.</summary>
        private bool _warned;

        private bool Wired =>
            _panelRoot != null && _content != null && _statusText != null
            && _listeningRoot != null && _footerRow != null;

        private bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        private void Start()
        {
            UiKit.EnsureEventSystem();

            if (!Wired)
            {
                Warn();
                return;
            }

            // 버튼은 인스펙터가 아니라 여기서 묶는다 — 대상 메서드가 private이라
            // UnityEvent 배선이 안 된다. 프리팹에는 참조만 꽂는다.
            if (_openButton != null) _openButton.onClick.AddListener(Open);
            if (_resetButton != null) _resetButton.onClick.AddListener(ResetAll);
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);

            BuildPresetButtons();

            _panelRoot.SetActive(false);
            _listeningRoot.SetActive(false);
        }

        /// <summary>
        /// 프리셋 개수는 인스펙터가 정하므로 프리팹으로 못 걷어낸다.
        /// 자리는 <see cref="_footerRow"/>의 HorizontalLayoutGroup이 잡는다 —
        /// 옛 코드가 손으로 하던 slot 폭 계산이 여기서 사라졌다.
        /// </summary>
        private void BuildPresetButtons()
        {
            if (presets == null) return;

            int index = 0;

            foreach (KeyBindingPreset preset in presets)
            {
                if (preset == null) continue;

                KeyBindingPreset captured = preset;
                Button b = BuildButton(_footerRow, $"Preset_{preset.name}", preset.DisplayName,
                    0f, 38f, PresetColor);

                // [기본값] · [닫기]는 프리팹에 이미 서 있다. 프리셋은 그 앞에 끼운다.
                b.transform.SetSiblingIndex(index++);
                b.onClick.AddListener(() => ApplyPreset(captured));
            }
        }

        /// <summary>배선이 비면 조용히 아무것도 안 하는 대신 한 번 알린다.</summary>
        private void Warn()
        {
            if (_warned) return;

            _warned = true;
            Debug.LogWarning(
                "[RebindUI] 배선이 비어 있다 — 조작키 설정 화면이 안 뜬다. " +
                "CombatManager 프리팹의 RebindCanvas 배선을 확인할 것.", this);
        }

        private void OnDestroy()
        {
            // 화면이 사라지는데 대기가 남아 있으면 조작이 잠긴 채로 굳는다.
            if (_manager != null && _manager.IsListening) _manager.Abort();
            SetSuspended(false);
        }

        private void Update()
        {
            if (!IsOpen) return;

            // 키를 받는 중에는 ESC가 대기 취소다. 그건 RebindManager가 디바이스에서 직접 듣는다.
            if (_manager != null && _manager.IsListening) return;

            if (_cancelAction != null && _cancelAction.WasPressedThisFrame()) Close();
        }

        // ── 열고 닫기 ────────────────────────────────────

        public void Open()
        {
            if (!EnsureManager()) return;

            // 플레이어가 없는 씬에서는 UI 맵을 켜 줄 사람이 없다. ESC로 닫을 길을 여기서 연다.
            _cancelAction.actionMap.Enable();

            _panelRoot.SetActive(true);
            SetSuspended(true);

            RebuildRows();
            SetStatus("바꿀 키의 [변경]을 누른 뒤 새 키를 누른다.", SubColor);
        }

        public void Close()
        {
            if (_manager != null && _manager.IsListening) _manager.Abort();

            _panelRoot.SetActive(false);
            SetSuspended(false);
        }

        private static void SetSuspended(bool value)
        {
            PlayerInputController input = PlayerInputController.Instance;
            if (input != null) input.GameplaySuspended = value;
        }

        /// <summary>
        /// 플레이어가 있으면 <see cref="PlayerInputController"/>가 들고 있는 그 인스턴스를 쓴다 —
        /// 따로 잡으면 화면에 보이는 키와 실제로 먹는 키가 갈린다.
        ///
        /// 플레이어가 없는 씬(부트 · 메인메뉴)에서는 직렬화해 둔 자산에 직접 얹는다.
        /// 바뀐 키는 PlayerPrefs로 나가고, 전투 씬이 올라올 때
        /// <c>PlayerInputController.Awake</c>가 그걸 다시 얹으므로 결과는 같다.
        /// </summary>
        private bool EnsureManager()
        {
            PlayerInputController input = PlayerInputController.Instance;
            InputActionAsset resolved = input != null && input.Actions != null
                ? input.Actions
                : fallbackActions;

            if (resolved == null)
            {
                Debug.LogWarning(
                    "[RebindUI] 쓸 액션 자산이 없다. 플레이어가 없는 씬이면 Fallback Actions를 채워라.", this);
                return false;
            }

            if (_manager == null || _actions != resolved)
            {
                _actions = resolved;
                _manager = new RebindManager(_actions);
                _cancelAction = _actions
                    .FindActionMap(InputActionNames.UI.Map, throwIfNotFound: true)
                    .FindAction(InputActionNames.UI.Cancel, throwIfNotFound: true);
            }

            return true;
        }

        // ── 키 받기 ──────────────────────────────────────

        private void BeginRebind(Row row)
        {
            if (_manager == null || _manager.IsListening) return;

            _listeningRoot.SetActive(true);
            SetStatus($"{InputDisplayNames.Binding(row.action, row.bindingIndex)} — 새 키를 누른다. ESC 취소.", HintColor);

            _manager.Begin(row.action, row.bindingIndex, result =>
            {
                _listeningRoot.SetActive(false);
                RefreshKeys();

                switch (result)
                {
                    case RebindManager.Result.Applied:
                        SetStatus($"{InputDisplayNames.Binding(row.action, row.bindingIndex)} → " +
                                  $"{InputDisplayNames.Key(row.action, row.bindingIndex)}", SubColor);
                        break;

                    case RebindManager.Result.Canceled:
                        SetStatus("취소했다.", SubColor);
                        break;

                    case RebindManager.Result.Conflict:
                        SetStatus($"이미 {_manager.LastConflictLabel} 이(가) 쓰는 키다. 되돌렸다.", WarnColor);
                        break;
                }
            });
        }

        private void ApplyPreset(KeyBindingPreset preset)
        {
            if (_manager == null || _manager.IsListening) return;

            int applied = _manager.ApplyPreset(preset);
            RefreshKeys();

            SetStatus($"{preset.DisplayName} 적용 — 항목 {applied}개", SubColor);
        }

        private void ResetAll()
        {
            if (_manager == null || _manager.IsListening) return;

            _manager.ResetAll();
            RefreshKeys();

            SetStatus("기본 키로 되돌렸다.", SubColor);
        }

        private void SetStatus(string message, Color color)
        {
            if (_statusText == null) return;

            _statusText.text = message;
            _statusText.color = color;
        }

        // ── 목록 ─────────────────────────────────────────

        private void RebuildRows()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            _rows.Clear();

            string currentMap = null;
            float y = 0f;

            foreach ((InputAction action, int bindingIndex) in InputRebindRules.Enumerate(_actions))
            {
                if (action.actionMap.name != currentMap)
                {
                    currentMap = action.actionMap.name;
                    BuildHeader(InputDisplayNames.Map(currentMap), ref y);
                }

                BuildRow(action, bindingIndex, ref y);
            }

            // y는 줄을 쌓으며 아래로 내려간 값이라 음수다. 높이는 그 절댓값이다 —
            // 음수를 그대로 넣으면 ScrollRect가 내용이 없다고 보고 스크롤이 안 먹는다.
            _content.sizeDelta = new Vector2(_content.sizeDelta.x, -y);
        }

        private void RefreshKeys()
        {
            foreach (Row row in _rows)
                row.keyLabel.text = InputDisplayNames.Key(row.action, row.bindingIndex);
        }

        private void BuildHeader(string label, ref float y)
        {
            var go = CreatePanel(_content, $"Header_{label}", HeaderColor);
            var rect = go.GetComponent<RectTransform>();
            Anchor(rect, RowWidth, RowHeight, y);

            Text text = BuildText(go.transform, "Label", RowWidth - 20f, RowHeight, 15, HintColor, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleLeft;
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchoredPosition = new Vector2(10f, 0f);

            y -= RowHeight + RowSpacing;
        }

        private void BuildRow(InputAction action, int bindingIndex, ref float y)
        {
            var go = CreatePanel(_content, $"Row_{action.name}_{bindingIndex}", RowColor);
            var rect = go.GetComponent<RectTransform>();
            Anchor(rect, RowWidth, RowHeight, y);

            Text label = BuildText(go.transform, "Label", LabelWidth, RowHeight, 14, SubColor, FontStyle.Normal);
            label.alignment = TextAnchor.MiddleLeft;
            label.text = InputDisplayNames.Binding(action, bindingIndex);
            label.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(-RowWidth / 2f + LabelWidth / 2f + 12f, 0f);

            Text key = BuildText(go.transform, "Key", KeyWidth, RowHeight, 14, Color.white, FontStyle.Bold);
            key.text = InputDisplayNames.Key(action, bindingIndex);
            key.GetComponent<RectTransform>().anchoredPosition = new Vector2(40f, 0f);

            var row = new Row { action = action, bindingIndex = bindingIndex, keyLabel = key };
            _rows.Add(row);

            Button change = BuildButton(go.transform, "Change", "변경", ChangeWidth, RowHeight - 6f, ChangeColor);
            change.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(RowWidth / 2f - ChangeWidth / 2f - 12f, 0f);
            change.onClick.AddListener(() => BeginRebind(row));

            y -= RowHeight + RowSpacing;
        }

        /// <summary>세로로 쌓는 목록. 위쪽 가운데를 기준으로 y만큼 내린다.</summary>
        private static void Anchor(RectTransform rect, float width, float height, float y)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        // ── 조각 ─────────────────────────────────────────

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Button BuildButton(Transform parent, string name, string label,
            float width, float height, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            go.GetComponent<Image>().color = color;

            Text text = BuildText(go.transform, "Label", width, height, 14, Color.white, FontStyle.Bold);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.text = label;

            return go.GetComponent<Button>();
        }

        private static Text BuildText(Transform parent, string name, float width, float height,
            int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);

            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }
    }
}
