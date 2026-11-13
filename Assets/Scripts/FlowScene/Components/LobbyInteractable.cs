using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>마을 오브젝트가 하는 일. 오브젝트 하나 = 기능 하나.</summary>
    public enum LobbyStation
    {
        /// <summary>미궁 입구 — 런 시작.</summary>
        Depart,

        /// <summary>용병 게시판 — 편성 · 고용.</summary>
        Party,

        /// <summary>마법사 — 설정.</summary>
        Settings,

        /// <summary>유산 제단 — 영구 강화.</summary>
        Altar,

        /// <summary>기록판 — 런 수 · 최고 층.</summary>
        Records,
    }

    /// <summary>
    /// 마을에 놓는 상호작용 지점. <b>판정만 들고 일은 안 한다</b> — 가까이 섰는지는
    /// <see cref="Nearest"/>가, 눌렀을 때 무엇을 여는지는 <see cref="LobbyController"/>가 정한다.
    /// 그래서 씬에는 종류 · 이름 · 반경만 적으면 되고 UnityEvent 배선이 없다.
    ///
    /// 그림이 생기기 전까지는 머리 위에 이름표(<see cref="TextMesh"/>)를 띄워 자리를 보인다.
    /// </summary>
    public class LobbyInteractable : MonoBehaviour
    {
        [SerializeField] private LobbyStation station;
        [SerializeField] private string label = "";

        [Tooltip("바닥(XZ) 기준 반경. 이 안에 들어오면 키 안내가 뜬다.")]
        [SerializeField] private float radius = 1.5f;

        private static readonly List<LobbyInteractable> all = new List<LobbyInteractable>();

        public LobbyStation Station => station;
        public string Label => string.IsNullOrEmpty(label) ? station.ToString() : label;

        /// <summary>테스트 · 코드로 지을 때의 이음매.</summary>
        public void Configure(LobbyStation value, string text, float range)
        {
            station = value;
            label = text;
            radius = range;
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        private void Start()
        {
            var go = new GameObject("NameTag");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 2.2f, 0f);

            // 카메라가 위에서 내려다보므로 같은 각도로 눕힌다. 안 눕히면 납작하게 보인다.
            Camera cam = Camera.main;
            if (cam != null) go.transform.rotation = cam.transform.rotation;

            // 폰트 · 재질을 안 꽂으면 TextMesh 는 아무것도 안 그린다. UiKit 과 같은 내장 폰트를 쓴다.
            var text = go.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = $"[{Label}]";
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.08f;
            text.fontSize = 48;
            text.color = new Color(1f, 0.86f, 0.45f);
        }

        /// <summary>
        /// <paramref name="position"/>에서 반경 안에 있는 가장 가까운 지점. 없으면 null.
        /// 높이(Y)는 안 본다 — 점프 중에도 안내가 깜빡이지 않게.
        /// </summary>
        public static LobbyInteractable Nearest(Vector3 position)
        {
            LobbyInteractable best = null;
            float bestSq = float.MaxValue;

            foreach (LobbyInteractable i in all)
            {
                Vector3 d = i.transform.position - position;
                float sq = d.x * d.x + d.z * d.z;
                if (sq > i.radius * i.radius || sq >= bestSq) continue;

                best = i;
                bestSq = sq;
            }

            return best;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.86f, 0.45f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
