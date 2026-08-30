using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.YG
{
    /// <summary>
    /// 메인화면의 파티 선택. <see cref="PartyCatalog"/>에 든 조합을 버튼으로 세운다.
    ///
    /// <see cref="BattleRestartUI"/> · <see cref="StageResultUI"/>와 같이 캔버스를 코드로 짓는다 —
    /// 씬 · 프리팹 배선이 필요 없고, MainMenu 씬을 다시 굽지 않아도 붙는다.
    ///
    /// <b>고르는 것은 조합뿐이다.</b> 칸마다 동료를 끼워 넣는 편집기는 아직 없다 —
    /// 씬 아홉 개를 옮기는 작업과 같은 판에 넣으면 실패 원인이 섞인다.
    /// 조합 에셋을 늘리는 것만으로 선택지가 늘어나므로 당장은 이걸로 충분하다.
    ///
    /// 조합이 하나뿐이거나 없으면 <b>스스로 숨는다</b> — 고를 것이 없는 화면에
    /// 버튼 하나를 띄워 봐야 누를 이유가 없다.
    /// </summary>
    public class PartySelectUI : MonoBehaviour
    {
        /// <summary>메인화면 기본 UI 위. 다른 캔버스와 다투지 않을 만큼만.</summary>
        private const int SortingOrder = 50;

        private const float ButtonWidth = 260f;
        private const float ButtonHeight = 46f;
        private const float Gap = 8f;
        private const float ScreenMargin = 24f;

        private static readonly Color PickedColor = new Color(0.24f, 0.48f, 0.72f);
        private static readonly Color PlainColor  = new Color(0.22f, 0.23f, 0.27f);
        private static readonly Color TitleColor  = new Color(0.82f, 0.84f, 0.88f);
        private static readonly Color NoteColor   = new Color(0.62f, 0.64f, 0.68f);

        private readonly List<(PartyLoadout loadout, Image bg)> entries =
            new List<(PartyLoadout, Image)>();

        private Text note;

        /// <summary>메인화면에 띄운다. 씬과 함께 언로드되도록 컨트롤러의 자식으로 붙인다.</summary>
        public static PartySelectUI Create(MonoBehaviour owner)
        {
            if (owner == null) return null;

            var go = new GameObject("PartySelectUI", typeof(RectTransform));
            go.transform.SetParent(owner.transform, false);

            return go.AddComponent<PartySelectUI>();
        }

        private void Start()
        {
            IReadOnlyList<PartyLoadout> all = PartyCatalog.All();

            // 고를 것이 없다. GameManager 가 기본 조합으로 알아서 간다.
            if (all.Count <= 1) { gameObject.SetActive(false); return; }

            Build(all);
            Select(Current(all));
        }

        /// <summary>지금 고른 조합. 아직 안 골랐으면 기본값.</summary>
        private static PartyLoadout Current(IReadOnlyList<PartyLoadout> all)
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Loadout != null) return gm.Loadout;

            return PartyCatalog.Default() ?? all[0];
        }

        private void Build(IReadOnlyList<PartyLoadout> all)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            gameObject.AddComponent<GraphicRaycaster>();

            RectTransform column = UiFactory.NewRect(transform, "Column");
            column.anchorMin = column.anchorMax = new Vector2(1f, 1f);
            column.pivot = new Vector2(1f, 1f);
            column.anchoredPosition = new Vector2(-ScreenMargin, -ScreenMargin);
            column.sizeDelta = new Vector2(ButtonWidth, 0f);

            float y = 0f;

            Text title = UiFactory.NewText(column, "Title", 22, TitleColor, FontStyle.Bold);
            Place(title.rectTransform, ref y, 34f);
            title.text = "파티";
            title.alignment = TextAnchor.MiddleLeft;

            foreach (PartyLoadout loadout in all)
            {
                if (loadout == null) continue;
                entries.Add((loadout, MakeButton(column, loadout, ref y)));
            }

            note = UiFactory.NewText(column, "Note", 15, NoteColor, FontStyle.Normal);
            Place(note.rectTransform, ref y, 30f);
            note.alignment = TextAnchor.UpperLeft;
        }

        private Image MakeButton(RectTransform column, PartyLoadout loadout, ref float y)
        {
            Image bg = UiFactory.NewImage(column, loadout.name, PlainColor, raycast: true);
            Place(bg.rectTransform, ref y, ButtonHeight);

            Text label = UiFactory.NewText(bg.rectTransform, "Label", 18, Color.white, FontStyle.Normal);
            UiFactory.Stretch(label.rectTransform, 10f);
            label.alignment = TextAnchor.MiddleLeft;
            label.text = $"{loadout.Label}  ({loadout.FilledCount}인)";

            PartyLoadout captured = loadout;
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => Select(captured));

            return bg;
        }

        private static void Place(RectTransform rect, ref float y, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(ButtonWidth, height);

            y += height + Gap;
        }

        /// <summary>
        /// 고른다. <see cref="GameManager.SelectLoadout"/>이 런 도중 교체를 거절하므로
        /// 여기서 따로 막지 않는다 — 메인화면에서는 언제나 런이 꺼져 있다.
        /// </summary>
        private void Select(PartyLoadout loadout)
        {
            GameManager.Instance?.SelectLoadout(loadout);

            foreach ((PartyLoadout l, Image bg) in entries)
                bg.color = ReferenceEquals(l, loadout) ? PickedColor : PlainColor;

            if (note == null) return;

            // 덱은 파티 장착 카드로 씨를 뿌린다. 16장이 아니면 손패가 자주 마르므로
            // 고르는 자리에서 바로 보여 준다 — 전투에 들어가서야 아는 값이 아니다.
            int cards = PartyAssembleRules.CardCount(loadout);
            note.text = cards == Deck.Size
                ? $"시작 덱 {cards}장"
                : $"시작 덱 {cards}장 (목표 {Deck.Size}장)";
        }
    }
}
