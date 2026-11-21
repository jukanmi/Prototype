using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.EditorTools
{
    /// <summary>
    /// BattleInput 프리팹의 주인공 체력 줄(RowTemplate)을 <see cref="HudLayout"/>대로 다시 짓는다.
    ///
    /// 초상화는 얇은 테두리 틀 안에 앉히되 머리가 틀 위로 삐져나오게 하고(RectMask2D가 위만 열려 있다),
    /// 막대는 꺾인 모양(<see cref="SlantedBar"/>)으로 틀 오른쪽 변에 딱 붙인다.
    /// 이름 · 바탕판은 지운다. 숫자 글자와 얼굴 스프라이트는 있던 것을 옮겨 쓴다.
    ///
    /// 손으로 프리팹을 고치다 표와 어긋났을 때 이걸 다시 돌리면 된다 — 어긋남은
    /// <c>BattleInputPrefabTests</c>가 잡는다.
    /// </summary>
    public static class PlayerHudBuilder
    {
        [MenuItem("Prototype/HUD/Rebuild player health row")]
        public static void Rebuild()
        {
            string path = PrefabLocator.BattleInputPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var hud = root.GetComponentInChildren<PartyHealthHUD>(true);
                var so = new SerializedObject(hud);
                var panel = (RectTransform)so.FindProperty("panel").objectReferenceValue;
                var row = (RectTransform)so.FindProperty("rowTemplate").objectReferenceValue;

                panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
                panel.anchoredPosition = new Vector2(HudLayout.MarginX, HudLayout.MarginBottom);
                panel.sizeDelta = new Vector2(HudLayout.Width, HudLayout.Height);
                row.sizeDelta = new Vector2(HudLayout.Width, HudLayout.Height);

                // 얼굴 스프라이트는 살린다 — 옛 자리(Frame/Face)든 새 자리든.
                Sprite faceSprite = null;
                foreach (string facePath in new[] { PartyHealthHUD.PathFace, "Frame/Face" })
                {
                    Transform f = row.Find(facePath);
                    if (f != null && f.GetComponent<Image>() is Image img && img.sprite != null) faceSprite = img.sprite;
                }

                // 숫자 글자는 살려서 새 막대 밑으로 옮긴다.
                Transform oldBack = row.Find(PartyHealthHUD.PathBarBack);
                Text numbers = oldBack != null ? oldBack.Find("Numbers")?.GetComponent<Text>() : null;
                if (numbers != null) numbers.transform.SetParent(row, false);

                foreach (string gone in new[] { "Name", "Plate", PartyHealthHUD.PathBarBack, PartyHealthHUD.PathFrame })
                {
                    Transform t = row.Find(gone);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }

                SlantedBar back = Bar(row, "BarBack", PartyHealthHUD.BarBorderColor);
                Place(back.rectTransform, new Vector2(HudLayout.BarX, HudLayout.HpY),
                      new Vector2(HudLayout.HpWidth, HudLayout.HpHeight));

                Inset(Bar(back.transform, "Track", UiTheme.InkDeep).rectTransform);
                Inset(Bar(back.transform, "Trail", UiTheme.TextMain.WithAlpha(0.9f)).rectTransform);
                Inset(Bar(back.transform, "Fill", UiTheme.Hp).rectTransform);

                if (numbers == null)
                    numbers = UiKit.NewText(back.transform, "Numbers", 20, UiTheme.TextMain, FontStyle.Bold);
                numbers.transform.SetParent(back.transform, false);
                numbers.alignment = TextAnchor.MiddleCenter;
                var nr = numbers.rectTransform;
                nr.anchorMin = Vector2.zero;
                nr.anchorMax = Vector2.one;
                // 꺾인 끝만큼 오른쪽을 비워 글자가 곧은 구간 가운데 오게 한다.
                nr.offsetMin = Vector2.zero;
                nr.offsetMax = new Vector2(-HudLayout.HpHeight * HudLayout.SlantRatio, 0f);

                BuildFrame(row, faceSprite);

                row.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log($"[PlayerHudBuilder] {path} 체력 줄을 다시 지었다.");
        }

        private static readonly Color FrameRim  = new Color(0.42f, 0.50f, 0.66f, 1f);
        private static readonly Color FrameBack = new Color(0.10f, 0.13f, 0.22f, 0.95f);

        /// <summary>
        /// 틀(테두리) → 안쪽 바탕 → 붉은 막 → 자르는 상자 → 얼굴. 형제 순서가 그리는 순서다.
        /// 자르는 상자는 좌 · 우 · 아래를 테두리 안쪽에 맞추고 위만 <see cref="HudLayout.PortraitOverflow"/>만큼 열어 둔다.
        /// 틀은 줄의 맨 앞(마지막 형제)이라 삐져나온 머리가 막대 위에 겹쳐도 가려지지 않는다.
        /// </summary>
        private static void BuildFrame(RectTransform row, Sprite faceSprite)
        {
            float s = HudLayout.FrameSize, b = HudLayout.FrameBorder;

            Image frame = Img(row, "Frame", FrameRim);
            Place(frame.rectTransform, Vector2.zero, new Vector2(s, s));

            UiKit.Stretch(Img(frame.transform, "Back", FrameBack).rectTransform, b);
            UiKit.Stretch(Img(frame.transform, "Flash", UiTheme.Hp.WithAlpha(0f)).rectTransform, b);

            var clipGo = new GameObject("Clip", typeof(RectTransform), typeof(RectMask2D));
            clipGo.layer = row.gameObject.layer;
            clipGo.transform.SetParent(frame.transform, false);
            var clip = (RectTransform)clipGo.transform;
            clip.anchorMin = Vector2.zero;
            clip.anchorMax = Vector2.one;
            clip.offsetMin = new Vector2(b, b);
            clip.offsetMax = new Vector2(-b, HudLayout.PortraitOverflow);

            Image face = Img(clip, "Face", Color.white);
            face.sprite = faceSprite;
            face.preserveAspect = true;
            UiKit.Stretch(face.rectTransform);

            frame.transform.SetAsLastSibling();
        }

        private static Image Img(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static SlantedBar Bar(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SlantedBar));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            SlantedBar bar = go.GetComponent<SlantedBar>();
            bar.color = color;
            bar.raycastTarget = false;
            bar.SlantRatio = HudLayout.SlantRatio;
            return bar;
        }

        private static void Place(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        /// <summary>테두리 두께만큼 안으로 들인다. 꺾임 각은 높이 비례라 안쪽도 같은 각이다.</summary>
        private static void Inset(RectTransform r) => UiKit.Stretch(r, HudLayout.Border);
    }
}
