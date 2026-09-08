using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Prototype
{
    /// <summary>
    /// 코드로 짓는 런타임 UI의 공용 조각. 배틀 씬 UI가 전부 씬·프리팹 배선 없이
    /// 스스로 만들어지므로(<see cref="BattleRestartUI"/>, <see cref="StageResultUI"/>)
    /// 같은 40줄을 여러 벌 들고 있지 않도록 여기 모은다.
    ///
    /// TMP 대신 레거시 <see cref="Text"/>를 쓰는 이유 — TMP Essential Resources 가
    /// 임포트돼 있지 않으면 런타임에 글자가 아예 안 나온다. 배틀 씬의 다른 런타임
    /// UI(ComboBoardUI)와도 같은 방식이다.
    /// </summary>
    public static class SimpleUI
    {
        /// <summary>화면을 덮는 캔버스 하나. 정렬 순서는 부르는 쪽이 정한다.</summary>
        public static Canvas BuildCanvas(GameObject host, int sortingOrder)
        {
            Canvas canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            host.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>
        /// Boot 씬을 거쳐 들어오면 EventSystem 이 이미 있다. 배틀 씬만 단독으로 Play 했을 때를 위한 보험.
        /// 중복 생성되면 UI 입력이 불안정해지므로 없을 때만 만든다.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static Button CreateButton(Transform parent, string name, string label, Color color,
                                          Vector2 size, int fontSize)
        {
            GameObject go = CreateImage(parent, name, color);
            ((RectTransform)go.transform).sizeDelta = size;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();

            Text text = CreateText(go.transform, "Label", label, fontSize, Color.white);
            Stretch((RectTransform)text.transform);

            return button;
        }

        public static GameObject CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;

            return go;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;   // 버튼 클릭을 라벨이 가로채지 않도록

            return text;
        }

        /// <summary>부모를 꽉 채운다.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>앵커·피벗을 한 점으로 모으고 그 자리에 놓는다.</summary>
        public static RectTransform Place(Component target, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)target.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            return rect;
        }
    }
}
